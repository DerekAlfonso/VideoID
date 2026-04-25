using Emgu.CV;
using Emgu.CV.CvEnum;
using VideoID.Core.Models;
using System.Drawing;
using System.Drawing.Imaging;

namespace VideoID.Core.Processing;

/// <summary>
/// Thread-safe incremental face clusterer.
/// Maps each new DetectedFace to an existing FaceIdentity (or creates one)
/// using cosine distance on the 128-d embedding centroid.
/// </summary>
public sealed class FaceClusterer
{
    private readonly ProcessingOptions _options;
    private readonly List<FaceIdentity> _identities = [];
    private readonly ReaderWriterLockSlim _rwLock = new();

    public FaceClusterer(ProcessingOptions options) => _options = options;

    public IReadOnlyList<FaceIdentity> Identities
    {
        get
        {
            _rwLock.EnterReadLock();
            try { return [.. _identities]; }
            finally { _rwLock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// Find the nearest existing identity or create a new one.
    /// Returns the matched or newly-created identity.
    /// </summary>
    public FaceIdentity AddFace(DetectedFace face)
    {
        _rwLock.EnterUpgradeableReadLock();
        try
        {
            FaceIdentity? best = null;
            float bestDist = float.MaxValue;

            foreach (var identity in _identities)
            {
                float dist = FaceEmbeddingExtractor.CosineDistance(
                    face.Embedding, identity.EmbeddingCentroid);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = identity;
                }
            }

            if (best != null && bestDist <= _options.FaceMatchThreshold)
            {
                _rwLock.EnterWriteLock();
                try
                {
                    MergeInto(best, face);
                    return best;
                }
                finally { _rwLock.ExitWriteLock(); }
            }
            else
            {
                _rwLock.EnterWriteLock();
                try
                {
                    var newIdentity = CreateIdentity(face);
                    _identities.Add(newIdentity);
                    return newIdentity;
                }
                finally { _rwLock.ExitWriteLock(); }
            }
        }
        finally { _rwLock.ExitUpgradeableReadLock(); }
    }

    /// <summary>Load existing identities (e.g. from database on startup).</summary>
    public void LoadIdentities(IEnumerable<FaceIdentity> identities)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _identities.Clear();
            _identities.AddRange(identities);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    private static FaceIdentity CreateIdentity(DetectedFace face)
    {
        var identity = new FaceIdentity
        {
            EmbeddingCentroid = (float[])face.Embedding.Clone(),
            Thumbnail = face.Thumbnail,
            FaceCount = 1
        };
        AddAppearance(identity, face);
        return identity;
    }

    private static void MergeInto(FaceIdentity identity, DetectedFace face)
    {
        // Incremental centroid update: new_centroid = (old * n + new) / (n + 1)
        int n = identity.FaceCount;
        for (int i = 0; i < identity.EmbeddingCentroid.Length; i++)
            identity.EmbeddingCentroid[i] =
                (identity.EmbeddingCentroid[i] * n + face.Embedding[i]) / (n + 1);

        identity.FaceCount++;
        identity.UpdatedAt = DateTimeOffset.UtcNow;

        // Replace thumbnail with a higher-confidence shot (larger bounding box ≈ better quality)
        if (face.Thumbnail.Length > identity.Thumbnail.Length)
            identity.Thumbnail = face.Thumbnail;

        AddAppearance(identity, face);
    }

    private static void AddAppearance(FaceIdentity identity, DetectedFace face)
    {
        identity.SourceVideos.Add(face.VideoPath);
        if (!identity.Appearances.TryGetValue(face.VideoPath, out var times))
        {
            times = [];
            identity.Appearances[face.VideoPath] = times;
        }
        times.Add(face.Timestamp);
    }

    public void Clear()
    {
        _rwLock.EnterWriteLock();
        try { _identities.Clear(); }
        finally { _rwLock.ExitWriteLock(); }
    }
}
