namespace VideoID.Core.Models;

public sealed class ProcessingOptions
{
    /// <summary>Number of frames to skip between face detection passes (1 = every frame).</summary>
    public int FrameSkip { get; set; } = 30;

    /// <summary>Maximum parallel video processor threads. Defaults to logical CPU count / 2.</summary>
    public int MaxParallelVideos { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Number of GPU face-detection workers sharing the CUDA context.</summary>
    public int GpuWorkers { get; set; } = 2;

    /// <summary>Cosine-distance threshold for matching a face to an existing identity (0-1, lower = stricter).</summary>
    public float FaceMatchThreshold { get; set; } = 0.40f;

    /// <summary>Minimum detection confidence from the SSD network (0-1).</summary>
    public float DetectionConfidence { get; set; } = 0.65f;

    /// <summary>Use CUDA GPU backend; falls back to CPU if unavailable.</summary>
    public bool UseGpu { get; set; } = true;

    /// <summary>Directory containing face-detection and face-embedding model files.</summary>
    public string ModelDirectory { get; set; } = "models";

    /// <summary>Directories to scan for video files.</summary>
    public List<string> VideoDirectories { get; set; } = [];

    /// <summary>Video file extensions to process.</summary>
    public HashSet<string> VideoExtensions { get; set; } =
        [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".webm"];

    /// <summary>Write discovered face names as metadata tags back to the source video file.</summary>
    public bool WriteTagsToFiles { get; set; } = true;

    /// <summary>Persist the face database to this path.</summary>
    public string DatabasePath { get; set; } = "faces.db";

    /// <summary>Scale input frame before detection (reduces GPU memory / increases speed).</summary>
    public float DetectionScale { get; set; } = 1.0f;

    /// <summary>Thumbnail size (pixels) stored for each unique face.</summary>
    public int ThumbnailSize { get; set; } = 128;
}
