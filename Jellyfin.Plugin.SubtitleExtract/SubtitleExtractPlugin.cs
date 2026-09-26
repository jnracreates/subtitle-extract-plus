using System;
using System.Collections.Generic;
using Jellyfin.Plugin.SubtitleExtractPlus.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SubtitleExtractPlus;

/// <summary>
/// Plugin entrypoint.
/// </summary>
public class SubtitleExtractPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleExtractPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public SubtitleExtractPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Current = this;
    }

    /// <inheritdoc />
    public override string Name => "Subtitle Extract Plus";

    /// <inheritdoc />
    public override Guid Id => new("528acb30-72a8-4c27-b699-a27a80a3855c");

    /// <inheritdoc />
    public override string Description => "Fork of Subtitle Extract with per-language and forced/non-forced track selection, and the option to save extracted subtitles next to the media file using Jellyfin's external subtitle naming convention.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static SubtitleExtractPlugin Current { get; private set; } = null!;

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return [
            new PluginPageInfo
            {
                Name = "Subtitle Extract Plus",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        ];
    }
}
