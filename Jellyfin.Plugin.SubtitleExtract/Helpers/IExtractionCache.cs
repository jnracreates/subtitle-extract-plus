using System.IO;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Helpers;

/// <summary>
/// Persisted cache of successfully-processed media files, so the scheduled
/// task and on-add handler don't re-run ffmpeg on unchanged files.
/// </summary>
public interface IExtractionCache
{
    /// <summary>Returns true when the media file is unchanged since the last successful run.</summary>
    /// <param name="key">Item id + media source id.</param>
    /// <param name="mediaFile">The media file on disk.</param>
    /// <returns>True if the cache entry is still valid.</returns>
    bool IsFresh(string key, FileInfo mediaFile);

    /// <summary>Returns the current entry for the given key, or <c>null</c> if not present.</summary>
    /// <param name="key">Item id + media source id.</param>
    /// <returns>The cache entry, or <c>null</c>.</returns>
    CacheEntry? GetEntry(string key);

    /// <summary>Records a successful extraction.</summary>
    /// <param name="key">Item id + media source id.</param>
    /// <param name="mediaFile">The media file on disk.</param>
    /// <param name="defaultLanguage">Chosen default language, if any.</param>
    /// <param name="streamIndices">Stream indices that were written.</param>
    /// <param name="writtenSidecars">Paths of sidecar files written during this run.</param>
    void MarkProcessed(string key, FileInfo mediaFile, string? defaultLanguage, int[] streamIndices, string[] writtenSidecars);

    /// <summary>Persists the cache to disk.</summary>
    void Save();
}
