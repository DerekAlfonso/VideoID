using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using System.Drawing;

namespace VideoID.Core.Processing;

/// <summary>
/// Extracts 128-dimensional face embeddings using the OpenFace DNN model.
/// Thread-safe via locking; share a single instance across GPU workers.
/// </summary>
public sealed class FaceEmbeddingExtractor : IDisposable
{
    // OpenFace nn4.small2 model – produces 128-d L2-normalised embeddings
    private const string ModelFile = "openface_nn4.small2.v1.t7";

    private readonly Net _net;
    private readonly Size _inputSize = new(96, 96);
    private readonly object _lock = new();
    private bool _disposed;

    public const int EmbeddingDimension = 128;

    public FaceEmbeddingExtractor(string modelDirectory, bool useGpu)
    {
        var modelPath = Path.Combine(modelDirectory, ModelFile);
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"Face embedding model not found in '{modelDirectory}'. " +
                $"Expected: {ModelFile}");

        _net = DnnInvoke.ReadNetFromTorch(modelPath);

        if (useGpu && TryCuda())
        {
            _net.SetPreferableBackend(Emgu.CV.Dnn.Backend.Cuda);
            _net.SetPreferableTarget(Emgu.CV.Dnn.Target.Cuda);
        }
    }

    /// <summary>
    /// Compute the 128-d embedding for a face crop (BGR Mat).
    /// Returns a normalised float array of length 128.
    /// </summary>
    public float[] Extract(Mat faceCrop)
    {
        if (faceCrop.IsEmpty) return new float[EmbeddingDimension];

        using var resized = new Mat();
        CvInvoke.Resize(faceCrop, resized, _inputSize);

        // Blob: scale to [0,1], 96×96, RGB, no mean subtraction
        using var blob = DnnInvoke.BlobFromImage(
            resized,
            scaleFactor: 1.0 / 255.0,
            size: _inputSize,
            mean: new MCvScalar(0, 0, 0),
            swapRB: true,
            crop: false);

        lock (_lock)
        {
            _net.SetInput(blob);
            using var output = _net.Forward();

            // Output shape: [1, 128]
            var embedding = new float[EmbeddingDimension];
            for (int i = 0; i < EmbeddingDimension; i++)
                embedding[i] = (float)output.GetValue(0, i);

            return L2Normalize(embedding);
        }
    }

    /// <summary>
    /// Cosine distance between two L2-normalised embeddings in [0, 2].
    /// Lower distance = more similar faces.
    /// </summary>
    public static float CosineDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 2f;
        float dot = 0f;
        for (int i = 0; i < a.Length; i++) dot += a[i] * b[i];
        // Both vectors are already L2-normalised, so |a||b| = 1
        return 1f - dot;
    }

    private static float[] L2Normalize(float[] v)
    {
        float norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm < 1e-6f) return v;
        for (int i = 0; i < v.Length; i++) v[i] /= norm;
        return v;
    }

    private static bool TryCuda()
    {
        try { return Emgu.CV.Cuda.CudaInvoke.HasCuda; }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _net.Dispose();
        _disposed = true;
    }
}
