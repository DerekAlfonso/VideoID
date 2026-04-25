using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using VideoID.Core.Models;
using VideoID.Core.Storage;

namespace VideoID.Core.Processing;

/// <summary>Events raised as processing progresses.</summary>
public sealed class PipelineProgressEventArgs : EventArgs
{
    public required VideoFile Video { get; init; }
    public int FramesProcessed { get; init; }
    public int FacesFound { get; init; }
}

public sealed class FaceDiscoveredEventArgs : EventArgs
{
    public required FaceIdentity Identity { get; init; }
    public bool IsNew { get; init; }
}

/// <summary>
/// Orchestrates the full GPU face-detection pipeline:
///
///  VideoFiles ──► FrameExtractors (N threads) ──► Channel&lt;VideoFrame&gt;
///                                                        │
///                                                        ▼
///                                          GpuFaceDetector workers (M threads)
///                                                        │
///                                                        ▼
///                                          FaceEmbeddingExtractor (shared, locked)
///                                                        │
///                                                        ▼
///                                          FaceClusterer ──► FaceDatabase
///
/// </summary>
public sealed class VideoProcessingPipeline : IAsyncDisposable
{
    private readonly ProcessingOptions _options;
    private readonly FaceClusterer _clusterer;
    private readonly FaceDatabase _db;
    private readonly VideoTagWriter _tagWriter;
    private readonly ILogger<VideoProcessingPipeline> _logger;

    public event EventHandler<PipelineProgressEventArgs>? Progress;
    public event EventHandler<FaceDiscoveredEventArgs>? FaceDiscovered;
    public event EventHandler<VideoFile>? VideoCompleted;
    public event EventHandler<Exception>? PipelineError;

    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public bool IsRunning => _runTask is { IsCompleted: false };

    public IReadOnlyList<FaceIdentity> Identities => _clusterer.Identities;

    public VideoProcessingPipeline(
        ProcessingOptions options,
        FaceClusterer clusterer,
        FaceDatabase db,
        VideoTagWriter tagWriter,
        ILogger<VideoProcessingPipeline> logger)
    {
        _options   = options;
        _clusterer = clusterer;
        _db        = db;
        _tagWriter = tagWriter;
        _logger    = logger;
    }

    /// <summary>Start processing a list of video files.</summary>
    public Task StartAsync(IReadOnlyList<VideoFile> videos, CancellationToken externalCt = default)
    {
        if (IsRunning) throw new InvalidOperationException("Pipeline already running.");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _runTask = RunAsync(videos, _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>Request graceful cancellation and wait for completion.</summary>
    public async Task StopAsync()
    {
        if (_cts is null || _runTask is null) return;
        await _cts.CancelAsync();
        try { await _runTask; }
        catch (OperationCanceledException) { }
    }

    // ─── core pipeline ───────────────────────────────────────────────────────

    private async Task RunAsync(IReadOnlyList<VideoFile> videos, CancellationToken ct)
    {
        // Shared channel from frame extractors → GPU detector workers
        var frameChannel = Channel.CreateBounded<VideoFrame>(
            new BoundedChannelOptions(capacity: _options.GpuWorkers * 4)
            {
                SingleWriter = false,
                SingleReader = false,
                FullMode      = BoundedChannelFullMode.Wait
            });

        // Shared channel from detectors → clusterer/db writer
        var faceChannel = Channel.CreateBounded<DetectedFace>(
            new BoundedChannelOptions(capacity: 256)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode      = BoundedChannelFullMode.Wait
            });

        var extractor = new VideoFrameExtractor(_options);

        FaceEmbeddingExtractor? embedder = null;
        try
        {
            embedder = new FaceEmbeddingExtractor(_options.ModelDirectory, _options.UseGpu);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Embedding model not found; using zero embeddings.");
        }

        using var _ = embedder;

        // Stage 1: frame extraction (max N parallel videos)
        var extractionTask = ProduceFramesAsync(videos, extractor, frameChannel, ct);

        // Stage 2: GPU face detection (M workers share the frame channel)
        var detectionTasks = Enumerable.Range(0, _options.GpuWorkers)
            .Select(_ => DetectFacesAsync(frameChannel, faceChannel, embedder, ct))
            .ToArray();

        var detectionDoneTask = Task.WhenAll(detectionTasks)
            .ContinueWith(_ => faceChannel.Writer.TryComplete(), TaskScheduler.Default);

        // Stage 3: single-threaded cluster + DB write (avoids contention on LiteDB writes)
        var clusterTask = ClusterAndPersistAsync(faceChannel, ct);

        await Task.WhenAll(extractionTask, detectionDoneTask, clusterTask);
    }

    // ─── Stage 1 ─────────────────────────────────────────────────────────────

    private async Task ProduceFramesAsync(
        IReadOnlyList<VideoFile> videos,
        VideoFrameExtractor extractor,
        Channel<VideoFrame> frameChannel,
        CancellationToken ct)
    {
        try
        {
            var semaphore = new SemaphoreSlim(_options.MaxParallelVideos);

            await Parallel.ForEachAsync(videos, ct, async (video, token) =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    video.State      = VideoProcessingState.Scanning;
                    video.StartedAt  = DateTimeOffset.UtcNow;

                    try { extractor.Probe(video); }
                    catch (Exception ex)
                    {
                        video.State        = VideoProcessingState.Failed;
                        video.ErrorMessage = ex.Message;
                        _logger.LogError(ex, "Failed to probe {File}", video.FileName);
                        VideoCompleted?.Invoke(this, video);
                        return;
                    }

                    await foreach (var frame in extractor.ExtractFramesAsync(video, token))
                    {
                        await frameChannel.Writer.WriteAsync(frame, token);
                        video.FramesProcessed++;
                        Progress?.Invoke(this, new PipelineProgressEventArgs
                        {
                            Video          = video,
                            FramesProcessed = video.FramesProcessed,
                            FacesFound     = video.FacesFound
                        });
                    }

                    video.State       = VideoProcessingState.Completed;
                    video.CompletedAt = DateTimeOffset.UtcNow;
                    VideoCompleted?.Invoke(this, video);
                }
                finally { semaphore.Release(); }
            });
        }
        finally
        {
            frameChannel.Writer.TryComplete();
        }
    }

    // ─── Stage 2 ─────────────────────────────────────────────────────────────

    private async Task DetectFacesAsync(
        Channel<VideoFrame> frameChannel,
        Channel<DetectedFace> faceChannel,
        FaceEmbeddingExtractor? embedder,
        CancellationToken ct)
    {
        GpuFaceDetector? detector = null;
        try
        {
            detector = new GpuFaceDetector(_options);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Cannot create face detector");
            return;
        }

        using var _ = detector;

        await foreach (var frame in frameChannel.Reader.ReadAllAsync(ct))
        {
            using (frame)
            {
                try
                {
                    var detections = detector.Detect(frame.Image);
                    foreach (var (box, conf) in detections)
                    {
                        var face = BuildDetectedFace(frame, box, conf, embedder);
                        await faceChannel.Writer.WriteAsync(face, ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Frame detection error on {Path}", frame.VideoPath);
                }
            }
        }
    }

    private DetectedFace BuildDetectedFace(
        VideoFrame frame,
        Rectangle box,
        float confidence,
        FaceEmbeddingExtractor? embedder)
    {
        float[] embedding = [];
        byte[] thumbnail  = [];

        using var faceCrop = new Mat(frame.Image, box);

        if (embedder != null)
            embedding = embedder.Extract(faceCrop);

        thumbnail = MatToJpegBytes(faceCrop, _options.ThumbnailSize);

        return new DetectedFace
        {
            VideoPath   = frame.VideoPath,
            FrameIndex  = frame.FrameIndex,
            Timestamp   = frame.Timestamp,
            BoundingBox = box,
            Confidence  = confidence,
            Embedding   = embedding,
            Thumbnail   = thumbnail
        };
    }

    // ─── Stage 3 ─────────────────────────────────────────────────────────────

    private async Task ClusterAndPersistAsync(
        Channel<DetectedFace> faceChannel,
        CancellationToken ct)
    {
        var videoFaces = new Dictionary<string, List<FaceIdentity>>();

        await foreach (var face in faceChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                int countBefore = _clusterer.Identities.Count;
                var identity = _clusterer.AddFace(face);
                bool isNew   = _clusterer.Identities.Count > countBefore;

                _db.UpsertIdentity(identity);

                FaceDiscovered?.Invoke(this, new FaceDiscoveredEventArgs
                {
                    Identity = identity,
                    IsNew    = isNew
                });

                if (!videoFaces.TryGetValue(face.VideoPath, out var list))
                    videoFaces[face.VideoPath] = list = [];
                if (!list.Contains(identity)) list.Add(identity);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Clustering error");
            }
        }

        // Write video metadata tags once per video
        if (_options.WriteTagsToFiles)
        {
            foreach (var (videoPath, identities) in videoFaces)
            {
                try { _tagWriter.WriteFaceNames(videoPath, identities); }
                catch (Exception ex) { _logger.LogWarning(ex, "Tag write failed for {Path}", videoPath); }
            }
        }
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static byte[] MatToJpegBytes(Mat mat, int maxSize)
    {
        if (mat.IsEmpty) return [];

        int w = mat.Width, h = mat.Height;
        double scale = (double)maxSize / Math.Max(w, h);
        int nw = (int)(w * scale), nh = (int)(h * scale);

        using var thumb = new Mat();
        CvInvoke.Resize(mat, thumb, new Size(nw, nh));

        var jpegParams = new[] { (int)ImwriteFlags.JpegQuality, 85 };
        return CvInvoke.Imencode(".jpg", thumb, jpegParams);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            await StopAsync();
            _cts.Dispose();
        }
    }
}
