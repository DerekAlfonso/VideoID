using TagLib;
using VideoID.Core.Models;

namespace VideoID.Core.Storage;

/// <summary>
/// Writes face-identity names back to video files as metadata tags.
/// Supports MP4/M4V (iTunes atoms), MKV (Matroska tags), AVI, and MP3 ID3v2.
/// </summary>
public sealed class VideoTagWriter
{
    /// <summary>
    /// Writes the names of all named <paramref name="identities"/> as the
    /// "Performers" / "Artists" tag of the video file, plus a custom comment.
    /// </summary>
    public void WriteFaceNames(string videoPath, IEnumerable<FaceIdentity> identities)
    {
        if (!System.IO.File.Exists(videoPath))
            throw new System.IO.FileNotFoundException(videoPath);

        var namedPeople = identities
            .Where(i => i.IsNamed)
            .Select(i => i.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToArray();

        if (namedPeople.Length == 0) return;

        using var file = TagLib.File.Create(videoPath);
        var tag = file.Tag;

        // Performers/Artists is the most universally supported person-list tag
        tag.Performers = namedPeople;

        // Also store in Comment for broad reader compatibility
        var existing = tag.Comment ?? "";
        const string prefix = "Faces: ";
        var faceComment = prefix + string.Join(", ", namedPeople);

        if (!existing.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            tag.Comment = string.IsNullOrWhiteSpace(existing)
                ? faceComment
                : existing.TrimEnd() + "\n" + faceComment;

        // Write custom people tag for MP4 (iTunes atom 'prsn' / freeform '----')
        if (file.TagTypes.HasFlag(TagTypes.Apple))
        {
            var appleTag = (TagLib.Mpeg4.AppleTag)file.GetTag(TagTypes.Apple);
            SetAppleCustomTag(appleTag, "com.videoid.faces", string.Join(";", namedPeople));
        }

        file.Save();
    }

    /// <summary>
    /// Read currently stored face names from a video file's metadata.
    /// </summary>
    public IReadOnlyList<string> ReadFaceNames(string videoPath)
    {
        if (!System.IO.File.Exists(videoPath)) return [];

        using var file = TagLib.File.Create(videoPath);
        return file.Tag.Performers ?? [];
    }

    private static void SetAppleCustomTag(TagLib.Mpeg4.AppleTag tag, string mean, string value) =>
        tag.SetDashBox("com.videoid", "faces", value);
}
