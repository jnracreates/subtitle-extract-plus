using Jellyfin.Plugin.SubtitleExtractPlus.Helpers;
using Jellyfin.Plugin.SubtitleExtractPlus.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtractPlus;

/// <summary>
/// Registers plugin services with Jellyfin's DI container.
/// This class must have a parameterless constructor because Jellyfin
/// instantiates it via <see cref="System.Activator.CreateInstance(System.Type)"/>.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ILanguageSelector, LanguageSelector>();

        serviceCollection.AddSingleton<IExtractionCache>(sp =>
        {
            var paths = sp.GetRequiredService<IApplicationPaths>();
            var logger = sp.GetRequiredService<ILogger<ExtractionCache>>();
            return new ExtractionCache(logger, paths.PluginConfigurationsPath);
        });

        serviceCollection.AddHostedService<ItemAddedSubscriber>();
    }
}
