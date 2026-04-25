namespace VideoID.Core.Models;

/// <summary>
/// A unique person identity, formed by clustering detected faces whose embeddings
/// are closer than the configured threshold.
/// </summary>
public sealed class FaceIdentity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-assigned name for this identity (null until named by the user).</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Centroid of all face embeddings belonging to this identity.
    /// Updated incrementally as new faces are merged in.
    /// </summary>
    public float[] EmbeddingCentroid { get; set; } = [];

    /// <summary>Best-quality face thumbnail for display in the UI.</summary>
    public byte[] Thumbnail { get; set; } = [];

    /// <summary>Total number of face detections merged into this identity.</summary>
    public int FaceCount { get; set; }

    /// <summary>Videos in which this identity appears.</summary>
    public HashSet<string> SourceVideos { get; set; } = [];

    /// <summary>Timestamps where this person was seen, keyed by video path.</summary>
    public Dictionary<string, List<TimeSpan>> Appearances { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>True when the user has assigned a name.</summary>
    public bool IsNamed => !string.IsNullOrWhiteSpace(Name);
}
