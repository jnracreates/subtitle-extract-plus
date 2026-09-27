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
    /// track cannot cause failures in other tracks.
    /// </summary>
    /// <param name="item">The media item.</param>
    /// <param name="mediaSource">The media source.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="mediaEncoder">The media encoder, used for the ffmpeg path.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task ExtractToMediaFolderAsync(
        BaseItem item,
        MediaSourceInfo mediaSource,
        PluginConfiguration config,
        IMediaEncoder mediaEncoder,
        ILogger logger,
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

        foreach (var stream in mediaSource.MediaStreams.Where(s => s.Type == MediaStreamType.Subtitle))
        {
            if (!ShouldExtract(stream, config))
            {
                continue;
            }

            if (!stream.IsTextSubtitleStream)
            {
                logger.LogDebug(
                    "Skipping non-text subtitle stream {Index} ({Codec}) in {Path} — SRT conversion not possible without OCR",
                                stream.Index,
                                stream.Codec,
                                mediaPath);
                continue;
            }

            var lang = NormalizeToIso1(stream.Language);
            var fileName = BuildFileName(mediaFileName, lang, stream.IsForced, stream.IsDefault, OutputFormat);
            var outputPath = Path.Combine(mediaDirectory, fileName);

            if (File.Exists(outputPath))
            {
                logger.LogDebug("Subtitle already exists: {Path}", outputPath);
                continue;
            }

            try
            {
                await ExtractSingleStreamAsync(ffmpegPath, mediaPath, stream.Index, outputPath, logger, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to extract subtitle stream {Index} from {Path}",
                    stream.Index,
                    mediaPath);
            }
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
                "-nostdin -hide_banner -y -i \"{0}\" -map 0:{1} -an -vn -c:s srt -flush_packets 1 \"{2}\"",
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
