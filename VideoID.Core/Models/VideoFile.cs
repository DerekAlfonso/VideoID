namespace VideoID.Core.Models;

public enum VideoProcessingState
{
    Pending,
    Scanning,
    Completed,
    Failed
}

public sealed class VideoFile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public long FileSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalFrames { get; set; }
    public double Fps { get; set; }
    public VideoProcessingState State { get; set; } = VideoProcessingState.Pending;
    public int FramesProcessed { get; set; }
    public int FacesFound { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Face identity IDs discovered in this video (populated after processing).</summary>
    public List<Guid> IdentityIds { get; set; } = [];

    public double ProgressPercent => TotalFrames > 0
        ? Math.Min(100.0, FramesProcessed * 100.0 / TotalFrames)
        : 0;
}
