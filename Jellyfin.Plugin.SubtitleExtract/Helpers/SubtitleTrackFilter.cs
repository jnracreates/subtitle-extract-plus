using System;
using System.Linq;
using Jellyfin.Plugin.SubtitleExtract.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SubtitleExtract.Helpers;

/// <summary>
/// Helpers for filtering media sources by subtitle track properties.
/// </summary>
public static class SubtitleTrackFilter
{
    /// <summary>
    /// Returns true if the media source contains at least one subtitle stream matching the
    /// configured language and forced/non-forced filters.
    /// </summary>
    /// <param name="source">The media source.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>True if at least one track matches.</returns>
    public static bool MatchesLanguageAndForcedFilter(MediaSourceInfo source, PluginConfiguration config)
    {
        foreach (var stream in source.MediaStreams.Where(s => s.Type == MediaStreamType.Subtitle))
        {
            if (config.SelectedLanguages.Length > 0)
            {
                if (string.IsNullOrEmpty(stream.Language) ||
                    !config.SelectedLanguages.Contains(stream.Language, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (stream.IsForced && !config.IncludeForcedSubtitles)
            {
                continue;
            }

            if (!stream.IsForced && !config.IncludeNonForcedSubtitles)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
