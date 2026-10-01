using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Helpers;

/// <inheritdoc />
public class ExtractionCache : IExtractionCache
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _cachePath;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly object _saveLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionCache"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="configDir">Directory in which to persist the cache file.</param>
    public ExtractionCache(ILogger logger, string configDir)
    {
        _logger = logger;
        _cachePath = Path.Combine(configDir, "subtitle-extract-cache.json");
        Load();
    }

    /// <inheritdoc />
    public bool IsFresh(string key, FileInfo mediaFile)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        try
        {
            if (!mediaFile.Exists)
            {
                return false;
            }

            if (mediaFile.Length != entry.MediaFileSize)
            {
                return false;
            }

            if (mediaFile.LastWriteTimeUtc != entry.MediaFileLastWriteUtc)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public CacheEntry? GetEntry(string key)
    {
        return _entries.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <inheritdoc />
    public void MarkProcessed(string key, FileInfo mediaFile, string? defaultLanguage, int[] streamIndices, string[] writtenSidecars)
    {
        try
        {
            _entries[key] = new CacheEntry
            {
                ProcessedUtc = DateTime.UtcNow,
                MediaFileSize = mediaFile.Exists ? mediaFile.Length : 0,
                MediaFileLastWriteUtc = mediaFile.Exists ? mediaFile.LastWriteTimeUtc : DateTime.MinValue,
                DefaultLanguage = defaultLanguage,
                ExtractedStreamIndices = streamIndices,
                WrittenSidecars = writtenSidecars,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SubExtract+] Could not update cache for {Key}.", key);
        }
    }

    /// <inheritdoc />
    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_entries, JsonOpts);
                File.WriteAllText(_cachePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SubExtract+] Could not save extraction cache.");
            }
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return;
            }

            var json = File.ReadAllText(_cachePath);
            var dict = JsonSerializer.Deserialize<ConcurrentDictionary<string, CacheEntry>>(json, JsonOpts);
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    _entries[kv.Key] = kv.Value;
                }

                _logger.LogInformation("[SubExtract+] Loaded {Count} cache entries.", _entries.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SubExtract+] Could not load extraction cache; starting empty.");
        }
    }
}
