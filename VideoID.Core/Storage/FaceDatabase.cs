using LiteDB;
using VideoID.Core.Models;

namespace VideoID.Core.Storage;

/// <summary>
/// Persistent face-identity store backed by LiteDB (embedded, zero-config).
/// Thread-safe — LiteDB serialises writes internally.
/// </summary>
public sealed class FaceDatabase : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<FaceIdentityRecord> _faces;
    private readonly ILiteCollection<VideoFileRecord> _videos;

    private const string FacesCollection  = "faces";
    private const string VideosCollection = "videos";

    public FaceDatabase(string databasePath)
    {
        _db    = new LiteDatabase(databasePath);
        _faces = _db.GetCollection<FaceIdentityRecord>(FacesCollection);
        _videos = _db.GetCollection<VideoFileRecord>(VideosCollection);

        _faces.EnsureIndex(x => x.Id, true);
        _videos.EnsureIndex(x => x.FilePath, true);
    }

    // ─── Face identities ─────────────────────────────────────────────────────

    public IEnumerable<FaceIdentity> LoadAllIdentities() =>
        _faces.FindAll().Select(ToModel);

    public FaceIdentity? FindIdentity(Guid id) =>
        _faces.FindById(id.ToString()) is { } r ? ToModel(r) : null;

    public void UpsertIdentity(FaceIdentity identity)
    {
        var record = ToRecord(identity);
        _faces.Upsert(record);
    }

    public void DeleteIdentity(Guid id) =>
        _faces.Delete(id.ToString());

    // ─── Video files ─────────────────────────────────────────────────────────

    public IEnumerable<VideoFile> LoadAllVideos() =>
        _videos.FindAll().Select(ToVideoModel);

    public void UpsertVideo(VideoFile video) =>
        _videos.Upsert(ToVideoRecord(video));

    // ─── Mapping ─────────────────────────────────────────────────────────────

    private static FaceIdentityRecord ToRecord(FaceIdentity m) => new()
    {
        Id               = m.Id.ToString(),
        Name             = m.Name,
        EmbeddingCentroid = m.EmbeddingCentroid,
        Thumbnail        = m.Thumbnail,
        FaceCount        = m.FaceCount,
        SourceVideos     = [.. m.SourceVideos],
        CreatedAt        = m.CreatedAt,
        UpdatedAt        = m.UpdatedAt
    };

    private static FaceIdentity ToModel(FaceIdentityRecord r) => new()
    {
        Id               = Guid.Parse(r.Id),
        Name             = r.Name,
        EmbeddingCentroid = r.EmbeddingCentroid ?? [],
        Thumbnail        = r.Thumbnail ?? [],
        FaceCount        = r.FaceCount,
        SourceVideos     = [.. r.SourceVideos ?? []],
        CreatedAt        = r.CreatedAt,
        UpdatedAt        = r.UpdatedAt
    };

    private static VideoFileRecord ToVideoRecord(VideoFile v) => new()
    {
        Id       = v.Id.ToString(),
        FilePath = v.FilePath,
        State    = v.State.ToString(),
        Duration = v.Duration.TotalSeconds
    };

    private static VideoFile ToVideoModel(VideoFileRecord r) => new()
    {
        FilePath = r.FilePath,
        State    = Enum.TryParse<VideoProcessingState>(r.State, out var s) ? s : VideoProcessingState.Pending,
        Duration = TimeSpan.FromSeconds(r.Duration)
    };

    public void Dispose() => _db.Dispose();

    // ─── Internal record types (LiteDB POCOs) ─────────────────────────────────

    private sealed class FaceIdentityRecord
    {
        [BsonId] public string Id { get; set; } = "";
        public string? Name { get; set; }
        public float[]? EmbeddingCentroid { get; set; }
        public byte[]? Thumbnail { get; set; }
        public int FaceCount { get; set; }
        public List<string>? SourceVideos { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class VideoFileRecord
    {
        [BsonId] public string Id { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string State { get; set; } = "";
        public double Duration { get; set; }
    }
}
