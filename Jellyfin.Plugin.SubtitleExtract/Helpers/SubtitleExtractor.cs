using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleExtractPlus.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Helpers;

/// <summary>
/// Extracts individual subtitle tracks and writes them next to the media file using
/// Jellyfin's external subtitle naming convention. Calls ffmpeg directly per stream
/// so that empty tracks cannot cascade failures into other streams.
/// </summary>
public static class SubtitleExtractor
{
    private const string OutputFormat = "srt";

    private static readonly Dictionary<string, string> Iso1Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "en",
        ["fra"] = "fr",
        ["fre"] = "fr",
        ["deu"] = "de",
        ["ger"] = "de",
        ["spa"] = "es",
        ["ita"] = "it",
        ["por"] = "pt",
        ["jpn"] = "ja",
        ["kor"] = "ko",
        ["chi"] = "zh",
        ["zho"] = "zh",
        ["rus"] = "ru",
        ["ara"] = "ar",
        ["hin"] = "hi",
        ["nld"] = "nl",
        ["dut"] = "nl",
        ["swe"] = "sv",
        ["nor"] = "no",
        ["dan"] = "da",
        ["fin"] = "fi",
        ["pol"] = "pl",
        ["tur"] = "tr",
        ["heb"] = "he",
        ["tha"] = "th",
        ["vie"] = "vi",
        ["ukr"] = "uk",
        ["ell"] = "el",
        ["gre"] = "el",
        ["ces"] = "cs",
        ["cze"] = "cs",
        ["hun"] = "hu",
        ["ron"] = "ro",
        ["rum"] = "ro",
        ["ind"] = "id",
        ["msa"] = "ms",
        ["may"] = "ms",
        ["fil"] = "tl",
        ["tgl"] = "tl",
        ["slk"] = "sk",
        ["slo"] = "sk",
        ["slv"] = "sl",
        ["hrv"] = "hr",
        ["srp"] = "sr",
        ["bul"] = "bg",
        ["lit"] = "lt",
        ["lav"] = "lv",
        ["est"] = "et",
        ["cat"] = "ca",
        ["glg"] = "gl",
        ["eus"] = "eu",
        ["baq"] = "eu",
        ["gle"] = "ga",
        ["cym"] = "cy",
        ["wel"] = "cy",
        ["isl"] = "is",
        ["ice"] = "is",
    };

    /// <summary>
    /// Normalizes an ISO 639-2/B code to its ISO 639-1 equivalent if known.
    /// </summary>
    /// <param name="code">The language code to normalize.</param>
    /// <returns>The normalized 2-letter code, or the input unchanged.</returns>
    internal static string NormalizeToIso1(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return code;
        }

        return Iso1Map.TryGetValue(code, out var iso1) ? iso1 : code;
    }

    /// <summary>
    /// Extracts every matching subtitle stream from a media source to the media folder.
    /// Each stream is extracted with its own ffmpeg call, so an empty or unreadable
    /// track cannot cause failures in other tracks. The language selector picks
    /// which stream receives the ".default." filename suffix, and the extraction
    /// cache skips unchanged files.
    /// </summary>
    /// <param name="item">The media item.</param>
    /// <param name="mediaSource">The media source.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="mediaEncoder">The media encoder, used for the ffmpeg path.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="languageSelector">Picks the default subtitle stream.</param>
    /// <param name="cache">Tracks successfully-processed media files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task ExtractToMediaFolderAsync(
        BaseItem item,
        MediaSourceInfo mediaSource,
        PluginConfiguration config,
        IMediaEncoder mediaEncoder,
        ILogger logger,
        ILanguageSelector languageSelector,
        IExtractionCache cache,
        CancellationToken cancellationToken)
    {
        var mediaPath = item.Path;
        if (string.IsNullOrEmpty(mediaPath) || !File.Exists(mediaPath))
        {
            logger.LogWarning("Media file does not exist: {Path}", mediaPath);
            return;
        }

        var mediaDirectory = Path.GetDirectoryName(mediaPath);
        if (string.IsNullOrEmpty(mediaDirectory))
        {
            logger.LogWarning("Cannot determine directory for {Path}", mediaPath);
            return;
        }

        var mediaFileName = Path.GetFileNameWithoutExtension(mediaPath);
        var ffmpegPath = mediaEncoder.EncoderPath;

        var mediaFile = new FileInfo(mediaPath);
        var cacheKey = string.Format(
            CultureInfo.InvariantCulture,
            "{0:N}:{1}",
            item.Id,
            mediaSource.Id);

        if (cache.IsFresh(cacheKey, mediaFile))
        {
            logger.LogDebug("Extraction cache hit for {Path}; skipping", mediaPath);
            return;
        }

        // Delete sidecars written during the previous run so we regenerate them
        // from the current version of the media file. This handles the case where
        // the file was recompressed (e.g. by FileFlows or HandBrake) and its
        // subtitle streams changed. Only paths we recorded are touched — manually
        // added sidecars are never deleted.
        var staleSidecars = cache.GetEntry(cacheKey)?.WrittenSidecars ?? Array.Empty<string>();
        if (staleSidecars.Length > 0)
        {
            logger.LogDebug(
                "Cleaning {Count} stale sidecar(s) for {Path}",
                staleSidecars.Length,
                mediaPath);

            foreach (var oldPath in staleSidecars)
            {
                try
                {
                    if (File.Exists(oldPath))
                    {
                        File.Delete(oldPath);
                        logger.LogDebug("Deleted stale sidecar: {Path}", oldPath);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not delete stale sidecar: {Path}", oldPath);
                }
            }
        }

        // Use subtitle-relative indexing so ffmpeg's `-map 0:s:N` is unambiguous.
        // Jellyfin's MediaStream.Index can differ from ffmpeg's absolute stream
        // numbering on some releases, which caused wrong-stream extraction.
        // Also skip external streams — they're .srt files on disk, not inside the
        // container, so ffmpeg doesn't count them for -map 0:s:N.
        var subtitleStreams = mediaSource.MediaStreams
            .Where(s => s.Type == MediaStreamType.Subtitle && !s.IsExternal)
            .ToList();

        var defaultStream = languageSelector.SelectDefault(subtitleStreams, config, logger);
        var defaultIndex = defaultStream?.Index;
        var extractedIndices = new List<int>();
        var writtenSidecars = new List<string>();

        for (var subtitleIndex = 0; subtitleIndex < subtitleStreams.Count; subtitleIndex++)
        {
            var stream = subtitleStreams[subtitleIndex];

            if (!ShouldExtract(stream, config))
            {
                continue;
            }

            if (!stream.IsTextSubtitleStream)
            {
                logger.LogDebug(
                    "Skipping non-text subtitle stream (subtitle index {SubIndex}, codec {Codec}) in {Path} — SRT conversion not possible without OCR",
                    subtitleIndex,
                    stream.Codec,
                    mediaPath);
                continue;
            }

            var lang = NormalizeToIso1(stream.Language);
            var isDefault = stream.Index == defaultIndex;
            var fileName = BuildFileName(mediaFileName, lang, stream.IsForced, isDefault, OutputFormat);
            var outputPath = Path.Combine(mediaDirectory, fileName);

            // Note: no File.Exists check here. The cache is the source of truth
            // for whether a file is current. If we've reached this point, either
            // the cache was stale (media file changed) or there's no cache entry
            // at all — both cases mean we should re-extract.

            logger.LogDebug(
                "Extracting subtitle stream (subtitle index {SubIndex}, codec {Codec}, lang {Lang}, forced {Forced}, default {Default}) from {Path}",
                subtitleIndex,
                stream.Codec,
                stream.Language,
                stream.IsForced,
                stream.IsDefault,
                mediaPath);

            try
            {
                await ExtractSingleStreamAsync(ffmpegPath, mediaPath, subtitleIndex, outputPath, logger, cancellationToken).ConfigureAwait(false);
                extractedIndices.Add(stream.Index);

                if (File.Exists(outputPath))
                {
                    writtenSidecars.Add(outputPath);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to extract subtitle stream (subtitle index {SubIndex}) from {Path}",
                    subtitleIndex,
                    mediaPath);
            }
        }

        // Only cache when we actually extracted something OR when there is
        // genuinely nothing extractable in the file. If we skipped every
        // stream because of the user's forced/non-forced filters, leave the
        // cache cold so a future config change can pick the file up.
        var anyExtractable = subtitleStreams.Any(s => ShouldExtract(s, config));
        if (extractedIndices.Count > 0 || !anyExtractable)
        {
            cache.MarkProcessed(
                cacheKey,
                mediaFile,
                defaultStream?.Language,
                extractedIndices.ToArray(),
                writtenSidecars.ToArray());
            cache.Save();
        }
    }

    private static async Task ExtractSingleStreamAsync(
        string ffmpegPath,
        string inputPath,
        int streamIndex,
        string outputPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var tempOutput = Path.Combine(
            Path.GetTempPath(),
            string.Format(CultureInfo.InvariantCulture, "subextract-{0:N}.srt", Guid.NewGuid()));

        try
        {
            var args = string.Format(
                CultureInfo.InvariantCulture,
                "-nostdin -hide_banner -y -i \"{0}\" -map 0:s:{1} -an -vn -c:s srt -flush_packets 1 \"{2}\"",
                inputPath,
                streamIndex,
                tempOutput);

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stderr = await stderrTask.ConfigureAwait(false);

            if (!File.Exists(tempOutput))
            {
                logger.LogDebug(
                    "ffmpeg produced no output for stream {Index} in {Path} (likely empty track). Exit code {Code}. Stderr tail: {Stderr}",
                    streamIndex,
                    inputPath,
                    process.ExitCode,
                    Truncate(stderr, 300));
                return;
            }

            var info = new FileInfo(tempOutput);
            if (info.Length == 0)
            {
                logger.LogDebug(
                    "Subtitle stream {Index} in {Path} produced an empty file — skipping",
                    streamIndex,
                    inputPath);
                return;
            }

            File.Copy(tempOutput, outputPath, overwrite: true);
            logger.LogInformation("Extracted subtitle: {Path}", outputPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempOutput))
                {
                    File.Delete(tempOutput);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    private static string BuildFileName(
        string mediaFileName,
        string lang,
        bool isForced,
        bool isDefault,
        string outputFormat)
    {
        var parts = new List<string> { mediaFileName };

        if (!string.IsNullOrEmpty(lang))
        {
            parts.Add(lang);
        }

        if (isDefault)
        {
            parts.Add("default");
        }

        if (isForced)
        {
            parts.Add("forced");
        }

        return string.Join('.', parts) + "." + outputFormat;
    }

    private static bool ShouldExtract(MediaStream stream, PluginConfiguration config)
    {
        if (stream.Type != MediaStreamType.Subtitle)
        {
            return false;
        }

        if (config.SelectedLanguages.Length > 0)
        {
            if (string.IsNullOrEmpty(stream.Language))
            {
                return false;
            }

            var streamLang = NormalizeToIso1(stream.Language);
            var matchFound = false;
            foreach (var candidate in config.SelectedLanguages)
            {
                if (string.Equals(NormalizeToIso1(candidate), streamLang, StringComparison.OrdinalIgnoreCase))
                {
                    matchFound = true;
                    break;
                }
            }

            if (!matchFound)
            {
                return false;
            }
        }

        if (stream.IsForced && !config.IncludeForcedSubtitles)
        {
            return false;
        }

        if (!stream.IsForced && !config.IncludeNonForcedSubtitles)
        {
            return false;
        }

        return true;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength), "…");
    }
}
