#pragma warning disable CA1819 // Properties should not return arrays

using System;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Helpers;

/// <summary>
/// One row in the extraction cache, keyed by item id + media source id.
/// </summary>
public class CacheEntry
{
    /// <summary>Gets or sets the UTC timestamp of the last successful run.</summary>
    public DateTime ProcessedUtc { get; set; }

    /// <summary>Gets or sets the size of the media file at the time of the run.</summary>
    public long MediaFileSize { get; set; }

    /// <summary>Gets or sets the mtime of the media file at the time of the run.</summary>
    public DateTime MediaFileLastWriteUtc { get; set; }

    /// <summary>Gets or sets the language code chosen as default, if any.</summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>Gets or sets the stream indices successfully extracted.</summary>
    public int[] ExtractedStreamIndices { get; set; } = Array.Empty<int>();

    /// <summary>Gets or sets the sidecar paths written during the last successful run.</summary>
    public string[] WrittenSidecars { get; set; } = Array.Empty<string>();
}
