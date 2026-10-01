using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleExtractPlus.Helpers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtractPlus.Services;

/// <summary>
/// Subscribes to library ItemAdded events and queues new videos for subtitle
/// extraction, debounced so a bulk import doesn't spawn an ffmpeg storm.
/// </summary>
public sealed class ItemAddedSubscriber : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILanguageSelector _languageSelector;
    private readonly IExtractionCache _cache;
    private readonly ILogger<ItemAddedSubscriber> _logger;

    // item id -> first-seen UTC.
    private readonly ConcurrentDictionary<Guid, DateTime> _pending = new();
    private readonly Timer _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemAddedSubscriber"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager, source of ItemAdded events.</param>
    /// <param name="mediaEncoder">Media encoder, used for the ffmpeg path.</param>
    /// <param name="languageSelector">Picks the default subtitle stream.</param>
    /// <param name="cache">Extraction cache.</param>
    /// <param name="logger">Logger.</param>
    public ItemAddedSubscriber(
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder,
        ILanguageSelector languageSelector,
        IExtractionCache cache,
        ILogger<ItemAddedSubscriber> logger)
    {
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _languageSelector = languageSelector;
        _cache = cache;
        _logger = logger;
        _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        _logger.LogInformation("[SubExtract+] Subscribed to ItemAdded events");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Dispose();
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (!SubtitleExtractPlugin.Current.Configuration.ExtractOnItemAdded)
        {
            return;
        }

        if (e.Item is not (Episode or Movie))
        {
            return;
        }

        _pending[e.Item.Id] = DateTime.UtcNow;
        _logger.LogDebug("[SubExtract+] Queued {Name} for extraction", e.Item.Name);
    }

    private void OnTick(object? state)
    {
        if (_pending.IsEmpty)
        {
            return;
        }

        var config = SubtitleExtractPlugin.Current.Configuration;
        var cutoff = DateTime.UtcNow.AddSeconds(-config.ItemAddedDebounceSeconds);

        var ready = new List<Guid>();
        foreach (var kv in _pending)
        {
            if (kv.Value <= cutoff)
            {
                ready.Add(kv.Key);
            }
        }

        foreach (var id in ready)
        {
            if (!_pending.TryRemove(id, out _))
            {
                continue;
            }

            // Timer already runs on a ThreadPool thread; fire and forget.
            _ = ProcessAsync(id);
        }
    }

    private async Task ProcessAsync(Guid itemId)
    {
        try
        {
            var item = _libraryManager.GetItemById(itemId);
            if (item is null)
            {
                return;
            }

            var mediaSources = item.GetMediaSources(false);
            if (mediaSources.Count == 0)
            {
                return;
            }

            var mediaSource = mediaSources[0];

            await SubtitleExtractor.ExtractToMediaFolderAsync(
                item,
                mediaSource,
                SubtitleExtractPlugin.Current.Configuration,
                _mediaEncoder,
                _logger,
                _languageSelector,
                _cache,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SubExtract+] On-add extraction failed for item {Id}", itemId);
        }
    }
}
