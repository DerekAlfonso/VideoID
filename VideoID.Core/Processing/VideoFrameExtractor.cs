using Emgu.CV;
using Emgu.CV.CvEnum;
using VideoID.Core.Models;
using System.Runtime.CompilerServices;

namespace VideoID.Core.Processing;

/// <summary>Frame data yielded from a video file.</summary>
public sealed class VideoFrame : IDisposable
{
    public required Mat Image { get; init; }
    public int FrameIndex { get; init; }
    public TimeSpan Timestamp { get; init; }
    public required string VideoPath { get; init; }

    public void Dispose() => Image.Dispose();
}

/// <summary>
/// Extracts frames from a video using OpenCV VideoCapture.
/// Supports configurable frame-skip and runs on a background thread.
/// </summary>
public sealed class VideoFrameExtractor
{
    private readonly ProcessingOptions _options;

    public VideoFrameExtractor(ProcessingOptions options) => _options = options;

    /// <summary>
    /// Probes the video and fills in metadata on the VideoFile object.
    /// </summary>
    public void Probe(VideoFile video)
    {
        using var cap = new VideoCapture(video.FilePath);
        if (!cap.IsOpened)
            throw new InvalidOperationException($"Cannot open video: {video.FilePath}");

        video.Fps        = cap.Get(CapProp.Fps);
        video.TotalFrames = (int)cap.Get(CapProp.FrameCount);
        video.FileSizeBytes = new FileInfo(video.FilePath).Length;

        double fps = video.Fps > 0 ? video.Fps : 25;
        video.Duration = TimeSpan.FromSeconds(video.TotalFrames / fps);
    }

    /// <summary>
    /// Asynchronously yields frames spaced <see cref="ProcessingOptions.FrameSkip"/> apart.
    /// Each Mat must be disposed by the caller.
    /// </summary>
    public async IAsyncEnumerable<VideoFrame> ExtractFramesAsync(
        VideoFile video,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield(); // move off the calling (UI) thread

        using var cap = new VideoCapture(video.FilePath);
        if (!cap.IsOpened)
            throw new InvalidOperationException($"Cannot open video: {video.FilePath}");

        double fps = cap.Get(CapProp.Fps);
        if (fps <= 0) fps = 25;

        int frameIndex = 0;
        int skip = Math.Max(1, _options.FrameSkip);

        while (!ct.IsCancellationRequested)
        {
            // Seek to next target frame for efficiency (avoids decoding skipped frames)
            cap.Set(CapProp.PosFrames, frameIndex);

            using var frame = new Mat();
            if (!cap.Read(frame) || frame.IsEmpty) break;

            var timestamp = TimeSpan.FromSeconds(frameIndex / fps);

            // Apply optional scale-down before handing the frame to the detector
            Mat output;
            if (_options.DetectionScale < 1.0f && _options.DetectionScale > 0)
            {
                output = new Mat();
                CvInvoke.Resize(frame, output,
                    new System.Drawing.Size(
                        (int)(frame.Width  * _options.DetectionScale),
                        (int)(frame.Height * _options.DetectionScale)));
            }
            else
            {
                output = frame.Clone();
            }

            yield return new VideoFrame
            {
                Image      = output,
                FrameIndex = frameIndex,
                Timestamp  = timestamp,
                VideoPath  = video.FilePath
            };

            frameIndex += skip;
        }
    }
}
