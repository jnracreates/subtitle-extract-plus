#pragma warning disable CA1819 // Properties should not return arrays

using System.Linq;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Configuration;

/// <summary>
/// How to choose a default subtitle stream when none of the preferred
/// languages is present in the file.
/// </summary>
public enum DefaultLanguageFallback
{
    /// <summary>Don't mark any sidecar as default.</summary>
    Skip = 0,

    /// <summary>Use the stream the container flags as default.</summary>
    StreamDefault = 1,

    /// <summary>Use the first subtitle stream in the container.</summary>
    FirstStream = 2,

    /// <summary>Use the configured LastResortLanguage.</summary>
    LastResort = 3
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    private static readonly CheckboxItem[] _allSubtitleCodecs =
    [
        new("ass", "ASS (Advanced SSA) subtitle (.ass & .ssa files - often found on anime)"),
        new("DVDSUB", "DVD subtitles"),
        new("subrip", "SubRip subtitle (.srt files - most common type of subtitles)"),
        new("PGSSUB", "HDMV Presentation Graphic Stream subtitles (often found on Blu-ray)"),
        new("DVBSUB", "DVB subtitles"),
        new("eia_608", "EIA-608 closed captions"),
        new("jacosub", "JACOsub subtitle"),
        new("microdvd", "MicroDVD subtitle"),
        new("mov_text", "MOV text"),
        new("mpl2", "MPL2 subtitle"),
        new("pjs", "PJS (Phoenix Japanimation Society) subtitle"),
        new("realtext", "RealText subtitle"),
        new("sami", "SAMI subtitle"),
        new("stl", "Spruce subtitle format"),
        new("subviewer", "SubViewer subtitle"),
        new("subviewer1", "SubViewer v1 subtitle"),
        new("text", "raw UTF-8 text"),
        new("vplayer", "VPlayer subtitle"),
        new("webvtt", "WebVTT subtitle"),
        new("xsub", "XSUB"),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether or not to extract subtitles and attachments as part of library scan.
    /// default = false.
    /// </summary>
    public bool ExtractionDuringLibraryScan { get; set; } = false;

    /// <summary>
    /// Gets or sets the list of selected libraries to extract subtitles from (empty means all).
    /// </summary>
    public string[] SelectedSubtitlesLibraries { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of selected libraries to extract attachments from (empty means all).
    /// </summary>
    public string[] SelectedAttachmentsLibraries { get; set; } = [];

    /// <summary>
    /// Gets all available subtitle codecs.
    /// </summary>
    public CheckboxItem[] AllSubtitleCodecs => _allSubtitleCodecs;

    /// <summary>
    /// Gets or sets the list of codecs to include when extracting subtitle from a media.
    /// </summary>
    public string[] SelectedCodecs { get; set; } = _allSubtitleCodecs.Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).ToArray()!;

    /// <summary>
    /// Gets or sets a value indicating whether advanced codec selection mode is enabled.
    /// </summary>
    public bool IsAdvancedMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether advanced codec selection mode is enabled.
    /// </summary>
    public bool IncludeTextSubtitles { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether advanced codec selection mode is enabled.
    /// </summary>
    public bool IncludeGraphicalSubtitles { get; set; } = true;

    /// <summary>
    /// Gets or sets the ISO 639-1 language codes to extract. Empty means all languages.
    /// </summary>
    public string[] SelectedLanguages { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional path prefix. When set, only items under this
    /// path are processed. Empty = process everything. Useful for testing on
    /// a single movie or season without walking the whole library.
    /// </summary>
    public string PathFilter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether forced subtitles are extracted.
    /// </summary>
    public bool IncludeForcedSubtitles { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether non-forced subtitles are extracted.
    /// </summary>
    public bool IncludeNonForcedSubtitles { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether subtitles should be written next to the media file
    /// instead of into Jellyfin's internal cache. When false, the legacy behaviour is used.
    /// </summary>
    public bool SaveWithMedia { get; set; } = true;

    /// <summary>
    /// Gets or sets the ranked list of language codes (ISO 639-1 or 639-2)
    /// used to pick the default sidecar. First match wins. Empty = fall back
    /// to <see cref="FallbackBehavior"/> immediately.
    /// </summary>
    public string[] PreferredDefaultLanguages { get; set; } = ["en"];

    /// <summary>
    /// Gets or sets what to do when none of the preferred languages is
    /// present in the file.
    /// </summary>
    public DefaultLanguageFallback FallbackBehavior { get; set; } = DefaultLanguageFallback.StreamDefault;

    /// <summary>
    /// Gets or sets the language code used when
    /// <see cref="FallbackBehavior"/> is <see cref="DefaultLanguageFallback.LastResort"/>.
    /// </summary>
    public string LastResortLanguage { get; set; } = "en";

    /// <summary>
    /// Gets or sets a value indicating whether new items added to the
    /// library are extracted immediately (debounced) in addition to the
    /// daily scheduled task.
    /// </summary>
    public bool ExtractOnItemAdded { get; set; }

    /// <summary>
    /// Gets or sets how long (in seconds) to wait after the last
    /// <c>ItemAdded</c> event before processing. Prevents an ffmpeg storm
    /// during bulk library imports.
    /// </summary>
    public int ItemAddedDebounceSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether files whose media mtime/size
    /// haven't changed since the last successful run are skipped.
    /// </summary>
    public bool UseExtractionCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache lifetime in days. 0 = never expires.
    /// </summary>
    public int ExtractionCacheTtlDays { get; set; }
}
