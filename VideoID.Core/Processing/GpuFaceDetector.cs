using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using VideoID.Core.Models;
using System.Drawing;

namespace VideoID.Core.Processing;

/// <summary>
/// GPU-accelerated face detector using OpenCV DNN with the ResNet-SSD Caffe model.
/// Each instance owns its own Net handle and is NOT thread-safe; use one per GPU worker thread.
/// Falls back to CPU if CUDA is unavailable.
/// </summary>
public sealed class GpuFaceDetector : IDisposable
{
    // ResNet-SSD face detector trained on WIDER FACE dataset
    // Files: res10_300x300_ssd_iter_140000.caffemodel + deploy.prototxt
    private const string ProtoFile = "deploy_face.prototxt";
    private const string ModelFile = "res10_300x300_ssd.caffemodel";

    private readonly Net _net;
    private readonly ProcessingOptions _options;
    private readonly Size _inputSize = new(300, 300);
    private readonly MCvScalar _mean = new(104, 177, 123);
    private bool _disposed;

    public bool IsGpuEnabled { get; private init; }

    public GpuFaceDetector(ProcessingOptions options)
    {
        _options = options;
        var protoPath = Path.Combine(options.ModelDirectory, ProtoFile);
        var modelPath = Path.Combine(options.ModelDirectory, ModelFile);

        if (!File.Exists(protoPath) || !File.Exists(modelPath))
            throw new FileNotFoundException(
                $"Face detection model files not found in '{options.ModelDirectory}'. " +
                $"Expected: {ProtoFile}, {ModelFile}");

        _net = DnnInvoke.ReadNetFromCaffe(protoPath, modelPath);

        if (options.UseGpu && TryCudaBackend())
        {
            _net.SetPreferableBackend(Emgu.CV.Dnn.Backend.Cuda);
            _net.SetPreferableTarget(Emgu.CV.Dnn.Target.Cuda);
            IsGpuEnabled = true;
        }
        else
        {
            _net.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
            _net.SetPreferableTarget(Emgu.CV.Dnn.Target.Cpu);
            IsGpuEnabled = false;
        }
    }

    /// <summary>
    /// Detect faces in the supplied BGR frame.
    /// Returns bounding boxes and confidence scores for faces above the threshold.
    /// </summary>
    public IReadOnlyList<(Rectangle Box, float Confidence)> Detect(Mat frame)
    {
        if (frame.IsEmpty) return [];

        // Build 4D blob: scale to 300×300, subtract mean, no swap BGR→RGB
        using var blob = DnnInvoke.BlobFromImage(
            frame,
            scaleFactor: 1.0,
            size: _inputSize,
            mean: _mean,
            swapRB: false,
            crop: false);

        _net.SetInput(blob);
        using var detections = _net.Forward();

        // Output shape: [1, 1, N, 7]  columns: [batchId, classId, conf, x1, y1, x2, y2]
        var results = new List<(Rectangle, float)>();
        int rows = detections.SizeOfDimension[2];
        var frameW = frame.Width;
        var frameH = frame.Height;

        for (int i = 0; i < rows; i++)
        {
            float confidence = (float)detections.GetValue(0, 0, i, 2);
            if (confidence < _options.DetectionConfidence) continue;

            float x1 = (float)detections.GetValue(0, 0, i, 3) * frameW;
            float y1 = (float)detections.GetValue(0, 0, i, 4) * frameH;
            float x2 = (float)detections.GetValue(0, 0, i, 5) * frameW;
            float y2 = (float)detections.GetValue(0, 0, i, 6) * frameH;

            // Clamp to frame bounds
            int left   = Math.Max(0, (int)x1);
            int top    = Math.Max(0, (int)y1);
            int right  = Math.Min(frameW, (int)x2);
            int bottom = Math.Min(frameH, (int)y2);

            int w = right - left;
            int h = bottom - top;
            if (w < 20 || h < 20) continue;    // skip tiny detections

            results.Add((new Rectangle(left, top, w, h), confidence));
        }

        return results;
    }

    private static bool TryCudaBackend()
    {
        try
        {
            // CudaInvoke throws if CUDA is not available
            return Emgu.CV.Cuda.CudaInvoke.HasCuda;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _net.Dispose();
        _disposed = true;
    }
}
