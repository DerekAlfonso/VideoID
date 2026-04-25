namespace VideoID.Core.Models;

/// <summary>A single face region found in one video frame, with its embedding vector.</summary>
public sealed class DetectedFace
{
    /// <summary>Source video this face came from.</summary>
    public required string VideoPath { get; init; }

    /// <summary>Frame index within the video.</summary>
    public int FrameIndex { get; init; }

    /// <summary>Timestamp of the frame.</summary>
    public TimeSpan Timestamp { get; init; }

    /// <summary>Bounding box of the face within the original frame (pixels).</summary>
    public System.Drawing.Rectangle BoundingBox { get; init; }

    /// <summary>Detection confidence from the SSD network (0-1).</summary>
    public float Confidence { get; init; }

    /// <summary>128-dimensional face embedding from the recognition network.</summary>
    public float[] Embedding { get; init; } = [];

    /// <summary>JPEG thumbnail of the cropped face region.</summary>
    public byte[] Thumbnail { get; init; } = [];
}
