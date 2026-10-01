using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleExtractPlus.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Providers;

/// <summary>
/// Extracts embedded subtitles while library scanning for immediate access in web player.
/// </summary>
public class SubtitleExtractionProvider : ICustomMetadataProvider<Episode>,
    ICustomMetadataProvider<Movie>,
    ICustomMetadataProvider<Video>,
    IHasItemChangeMonitor,
    IHasOrder,
    IForcedProvider
{
    private readonly ILogger<SubtitleExtractionProvider> _logger;

    private readonly ISubtitleEncoder _encoder;

    private readonly IMediaEncoder _mediaEncoder;

    private readonly ILanguageSelector _languageSelector;

    private readonly IExtractionCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleExtractionProvider"/> class.
    /// </summary>
    /// <param name="subtitleEncoder"><see cref="ISubtitleEncoder"/> instance.</param>
    /// <param name="mediaEncoder"><see cref="IMediaEncoder"/> instance for the ffmpeg path.</param>
    /// <param name="languageSelector">Language selector for default-sidecar selection.</param>
    /// <param name="cache">Extraction cache.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    public SubtitleExtractionProvider(
        ISubtitleEncoder subtitleEncoder,
        IMediaEncoder mediaEncoder,
        ILanguageSelector languageSelector,
        IExtractionCache cache,
        ILogger<SubtitleExtractionProvider> logger)
    {
        _logger = logger;
        _encoder = subtitleEncoder;
        _mediaEncoder = mediaEncoder;
        _languageSelector = languageSelector;
        _cache = cache;
    }

    /// <inheritdoc />
    public string Name => "Subtitle Extraction";

    /// <summary>
    /// Gets the order in which the provider should be called. (Core provider is = 100).
    /// </summary>
    public int Order => 1000;

    /// <inheritdoc/>
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        if (item.IsFileProtocol)
        {
            var file = directoryService.GetFile(item.Path);
            if (file is not null && item.HasChanged(file.LastWriteTimeUtc))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public Task<ItemUpdateType> FetchAsync(Episode item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        return FetchSubtitles(item, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        return FetchSubtitles(item, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ItemUpdateType> FetchAsync(Video item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        return FetchSubtitles(item, cancellationToken);
    }

    private async Task<ItemUpdateType> FetchSubtitles(BaseItem item, CancellationToken cancellationToken)
    {
        var config = SubtitleExtractPlugin.Current!.Configuration;
        if (config.ExtractionDuringLibraryScan)
        {
            _logger.LogDebug("Extracting subtitles for: {Video}", item.Path);

            foreach (var mediaSource in item.GetMediaSources(false))
            {
                if (!SubtitleTrackFilter.MatchesLanguageAndForcedFilter(mediaSource, config))
                {
                    continue;
                }

                if (config.SaveWithMedia)
                {
                    await SubtitleExtractor.ExtractToMediaFolderAsync(item, mediaSource, config, _mediaEncoder, _logger, _languageSelector, _cache, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _encoder.ExtractAllExtractableSubtitles(mediaSource, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogDebug("Finished subtitle extraction for: {Video}", item.Path);
        }

        return ItemUpdateType.None;
    }
}
