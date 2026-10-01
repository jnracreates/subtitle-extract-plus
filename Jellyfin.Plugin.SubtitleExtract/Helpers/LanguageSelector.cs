using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.SubtitleExtractPlus.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Helpers;

/// <inheritdoc />
public class LanguageSelector : ILanguageSelector
{
    // ffprobe reports ISO 639-2/B codes; users typically type 639-1.
    // Map both to a canonical 639-2/B code for comparison.
    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "eng", ["eng"] = "eng",
            ["fr"] = "fre", ["fre"] = "fre", ["fra"] = "fre",
            ["de"] = "ger", ["ger"] = "ger", ["deu"] = "ger",
            ["es"] = "spa", ["spa"] = "spa",
            ["pt"] = "por", ["por"] = "por",
            ["it"] = "ita", ["ita"] = "ita",
            ["nl"] = "dut", ["dut"] = "dut", ["nld"] = "dut",
            ["pl"] = "pol", ["pol"] = "pol",
            ["ru"] = "rus", ["rus"] = "rus",
            ["uk"] = "ukr", ["ukr"] = "ukr",
            ["ja"] = "jpn", ["jpn"] = "jpn",
            ["ko"] = "kor", ["kor"] = "kor",
            ["zh"] = "chi", ["chi"] = "chi", ["zho"] = "chi",
            ["ar"] = "ara", ["ara"] = "ara",
            ["he"] = "heb", ["heb"] = "heb",
            ["tr"] = "tur", ["tur"] = "tur",
            ["sv"] = "swe", ["swe"] = "swe",
            ["no"] = "nor", ["nor"] = "nor",
            ["da"] = "dan", ["dan"] = "dan",
            ["fi"] = "fin", ["fin"] = "fin",
            ["cs"] = "cze", ["cze"] = "cze", ["ces"] = "cze",
            ["sk"] = "slo", ["slo"] = "slo", ["slk"] = "slo",
            ["hu"] = "hun", ["hun"] = "hun",
            ["ro"] = "rum", ["rum"] = "rum", ["ron"] = "rum",
            ["bg"] = "bul", ["bul"] = "bul",
            ["el"] = "gre", ["gre"] = "gre", ["ell"] = "gre",
            ["hi"] = "hin", ["hin"] = "hin",
            ["ta"] = "tam", ["tam"] = "tam",
            ["te"] = "tel", ["tel"] = "tel",
            ["th"] = "tha", ["tha"] = "tha",
            ["vi"] = "vie", ["vie"] = "vie",
            ["id"] = "ind", ["ind"] = "ind",
            ["ms"] = "may", ["may"] = "may", ["msa"] = "may",
            ["et"] = "est", ["est"] = "est",
            ["lv"] = "lav", ["lav"] = "lav",
            ["lt"] = "lit", ["lit"] = "lit",
            ["sl"] = "slv", ["slv"] = "slv",
        };

    private static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        code = code.Trim();
        return Aliases.TryGetValue(code, out var canonical)
            ? canonical
            : code.ToLowerInvariant();
    }

    private static bool Matches(MediaStream stream, string canonicalCode)
    {
        if (string.IsNullOrWhiteSpace(stream.Language))
        {
            return false;
        }

        return Normalize(stream.Language) == canonicalCode;
    }

    private static bool IsSdh(MediaStream stream) =>
        stream.IsHearingImpaired ||
        (stream.Title?.Contains("SDH", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <inheritdoc />
    public MediaStream? SelectDefault(
        IReadOnlyList<MediaStream> subtitleStreams,
        PluginConfiguration config,
        ILogger logger)
    {
        if (subtitleStreams.Count == 0)
        {
            logger.LogDebug("[SubExtract+] No subtitle streams to select from.");
            return null;
        }

        // 1. Ranked preference list.
        foreach (var raw in config.PreferredDefaultLanguages ?? Array.Empty<string>())
        {
            var wanted = Normalize(raw);
            if (string.IsNullOrEmpty(wanted))
            {
                continue;
            }

            // Respect the user's forced/non-forced filter first.
            var candidates = subtitleStreams
                .Where(s => Matches(s, wanted))
                .Where(s => !s.IsForced || config.IncludeForcedSubtitles)
                .Where(s => s.IsForced || config.IncludeNonForcedSubtitles)
                .ToList();

            // Among valid candidates, prefer non-forced, non-SDH.
            var match = candidates
                            .Where(s => !s.IsForced && !IsSdh(s))
                            .OrderBy(s => s.Index)
                            .FirstOrDefault()
                        ?? candidates.OrderBy(s => s.Index).FirstOrDefault();

            if (match != null)
            {
                logger.LogInformation(
                    "[SubExtract+] Default language selected by preference list: {Lang} (stream #{Idx}).",
                    raw,
                    match.Index);
                return match;
            }
        }

        // 2. Fallback chain.
        switch (config.FallbackBehavior)
        {
            case DefaultLanguageFallback.Skip:
                logger.LogInformation(
                    "[SubExtract+] No preferred language found; FallbackBehavior=Skip, no default sidecar will be marked.");
                return null;

            case DefaultLanguageFallback.StreamDefault:
                var flagged = subtitleStreams.FirstOrDefault(s =>
                    s.IsDefault
                    && (!s.IsForced || config.IncludeForcedSubtitles)
                    && (s.IsForced || config.IncludeNonForcedSubtitles));
                if (flagged != null)
                {
                    logger.LogInformation(
                        "[SubExtract+] No preferred language found; using container default (stream #{Idx}, {Lang}).",
                        flagged.Index,
                        flagged.Language);
                    return flagged;
                }

                goto case DefaultLanguageFallback.FirstStream;

            case DefaultLanguageFallback.FirstStream:
                var first = subtitleStreams
                    .Where(s => (!s.IsForced || config.IncludeForcedSubtitles)
                             && (s.IsForced || config.IncludeNonForcedSubtitles))
                    .OrderBy(s => s.Index)
                    .FirstOrDefault();

                if (first == null)
                {
                    logger.LogInformation(
                        "[SubExtract+] No extractable stream to mark as default after applying forced/non-forced filter.");
                    return null;
                }

                logger.LogInformation(
                    "[SubExtract+] No preferred language found; falling back to first stream #{Idx} ({Lang}).",
                    first.Index,
                    first.Language);
                return first;

            case DefaultLanguageFallback.LastResort:
                var last = Normalize(config.LastResortLanguage);
                var lastMatch = subtitleStreams
                    .Where(s => Matches(s, last))
                    .Where(s => !s.IsForced || config.IncludeForcedSubtitles)
                    .Where(s => s.IsForced || config.IncludeNonForcedSubtitles)
                    .OrderBy(s => s.Index)
                    .FirstOrDefault();

                if (lastMatch != null)
                {
                    logger.LogInformation(
                        "[SubExtract+] No preferred language found; using last-resort {Lang} (stream #{Idx}).",
                        config.LastResortLanguage,
                        lastMatch.Index);
                    return lastMatch;
                }

                logger.LogWarning(
                    "[SubExtract+] FallbackBehavior=LastResort but {Lang} not present in file; no default sidecar will be marked.",
                    config.LastResortLanguage);
                return null;

            default:
                return null;
        }
    }
}
