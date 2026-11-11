using System;
using System.Collections.Generic;
using Jellyfin.Plugin.SeriesSubtitles.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SeriesSubtitles;

/// <summary>
/// Impose une langue de sous-titres par série.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Chemins de l'application.</param>
    /// <param name="xmlSerializer">Sérialiseur XML.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Series Subtitles";

    /// <inheritdoc />
    public override string Description => "Impose une langue de sous-titres pour toute une série.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("59ae2e45-120d-496e-a601-bc16cb28e9f2");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "SeriesSubtitles",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        };
    }
}
