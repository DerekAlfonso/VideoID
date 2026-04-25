using VideoID.Core.Models;

namespace VideoID.Core.Services;

/// <summary>
/// Scans one or more directories recursively for video files matching the
/// configured extensions.
/// </summary>
public sealed class VideoScanService
{
    private readonly ProcessingOptions _options;

    public VideoScanService(ProcessingOptions options) => _options = options;

    /// <summary>Return all video files found under every configured directory.</summary>
    public IReadOnlyList<VideoFile> Scan()
    {
        var results = new List<VideoFile>();

        foreach (var dir in _options.VideoDirectories)
        {
            if (!Directory.Exists(dir)) continue;

            var files = Directory
                .EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Where(f => _options.VideoExtensions.Contains(
                    Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f);

            foreach (var path in files)
                results.Add(new VideoFile { FilePath = path });
        }

        return results;
    }

    /// <summary>Validate that all required model files are present.</summary>
    public (bool Ok, IReadOnlyList<string> Missing) ValidateModels()
    {
        var required = new[]
        {
            "deploy_face.prototxt",
            "res10_300x300_ssd.caffemodel",
            "openface_nn4.small2.v1.t7"
        };

        var missing = required
            .Where(f => !File.Exists(Path.Combine(_options.ModelDirectory, f)))
            .ToList();

        return (missing.Count == 0, missing);
    }
}
