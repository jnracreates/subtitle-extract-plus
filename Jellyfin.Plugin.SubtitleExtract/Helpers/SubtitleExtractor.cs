using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleExtract.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtract.Helpers;

/// <summary>
/// Extracts individual subtitle tracks and writes them next to the media file using
/// Jellyfin's external subtitle naming convention.
/// </summary>
public static class SubtitleExtractor
{
    private const string OutputFormat = "srt";

    /// <summary>
    /// Extracts every matching subtitle stream from a media source to the media folder.
    /// </summary>
    /// <param name="item">The item the media source belongs to.</param>
    /// <param name="mediaSource">The media source.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="encoder">The subtitle encoder.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public static async Task ExtractToMediaFolderAsync(
        BaseItem item,
        MediaSourceInfo mediaSource,
        PluginConfiguration config,
        ISubtitleEncoder encoder,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var mediaPath = mediaSource.Path ?? item.Path;
        if (string.IsNullOrEmpty(mediaPath))
        {
            return;
        }

        var mediaDir = Path.GetDirectoryName(mediaPath);
        if (string.IsNullOrEmpty(mediaDir))
        {
            return;
        }

        var mediaFileName = Path.GetFileNameWithoutExtension(mediaPath);
        if (string.IsNullOrEmpty(mediaFileName))
        {
            return;
        }

        foreach (var stream in mediaSource.MediaStreams.Where(s => s.Type == MediaStreamType.Subtitle))
        {
            if (!ShouldExtract(stream, config))
            {
                continue;
            }

            if (!stream.IsTextSubtitleStream)
            {
                logger.LogDebug(
                    "Skipping graphical subtitle stream {Index} in {Path} (OCR not supported)",
                    stream.Index,
                    mediaPath);
                continue;
            }

            if (string.IsNullOrEmpty(stream.Language))
            {
                logger.LogDebug(
                    "Skipping stream {Index} in {Path}: no language code, cannot build a Jellyfin-compliant filename",
                    stream.Index,
                    mediaPath);
                continue;
            }

            var fileName = BuildFileName(mediaFileName, stream.Language, stream.IsForced, stream.IsDefault, OutputFormat);
            var outputPath = Path.Combine(mediaDir, fileName);

            if (File.Exists(outputPath))
            {
                logger.LogDebug("Subtitle already exists, skipping: {Path}", outputPath);
                continue;
            }

            try
            {
                using var subtitleStream = await encoder.GetSubtitles(
                    item,
                    mediaSource.Id,
                    stream.Index,
                    OutputFormat,
                    0,
                    0,
                    false,
                    cancellationToken).ConfigureAwait(false);

                using var fileStream = File.Create(outputPath);
                await subtitleStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                logger.LogInformation("Extracted subtitle: {Path}", outputPath);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Failed to write subtitle file: {Path}", outputPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "No write permission for subtitle file: {Path}", outputPath);
            }
        }
    }

    /// <summary>
    /// Builds a filename following Jellyfin's external subtitle convention:
    /// {mediaName}.{language}[.default][.forced].{ext}.
    /// </summary>
    /// <param name="mediaName">The media file's name without extension.</param>
    /// <param name="language">The ISO language code.</param>
    /// <param name="isForced">Whether the track is forced.</param>
    /// <param name="isDefault">Whether the track is default.</param>
    /// <param name="extension">The file extension without dot.</param>
    /// <returns>The constructed filename.</returns>
    private static string BuildFileName(string mediaName, string language, bool isForced, bool isDefault, string extension)
    {
        var parts = new List<string> { mediaName, language };

        if (isDefault)
        {
            parts.Add("default");
        }

        if (isForced)
        {
            parts.Add("forced");
        }

        parts.Add(extension);
        return string.Join(".", parts);
    }

    private static bool ShouldExtract(MediaStream stream, PluginConfiguration config)
    {
        if (config.SelectedLanguages.Length > 0)
        {
            if (string.IsNullOrEmpty(stream.Language) ||
                !config.SelectedLanguages.Contains(stream.Language, StringComparer.OrdinalIgnoreCase))
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
}
