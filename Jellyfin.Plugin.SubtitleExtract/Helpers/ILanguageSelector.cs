using System.Collections.Generic;
using Jellyfin.Plugin.SubtitleExtractPlus.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Helpers;

/// <summary>
/// Picks the subtitle stream that should be written as the default sidecar,
/// based on the user's ranked language preference list and a configurable
/// fallback chain.
/// </summary>
public interface ILanguageSelector
{
    /// <summary>
    /// Selects the default subtitle stream, or <c>null</c> if nothing should
    /// be marked as default (e.g. <see cref="DefaultLanguageFallback.Skip"/>).
    /// </summary>
    /// <param name="subtitleStreams">Subtitle streams from the media source.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>The stream to mark as default, or <c>null</c>.</returns>
    MediaStream? SelectDefault(
        IReadOnlyList<MediaStream> subtitleStreams,
        PluginConfiguration config,
        ILogger logger);
}
