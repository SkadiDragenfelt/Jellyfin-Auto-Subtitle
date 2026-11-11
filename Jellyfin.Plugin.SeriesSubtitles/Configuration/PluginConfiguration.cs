using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SeriesSubtitles.Configuration;

/// <summary>
/// Réglages du plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether la langue de la série remplace aussi
    /// un choix de sous-titres fait explicitement dans le lecteur.
    /// </summary>
    public bool OverrideClientChoice { get; set; }

    /// <summary>
    /// Gets or sets les règles : une série, une langue.
    /// </summary>
    public SeriesSubtitleRule[] Rules { get; set; } = Array.Empty<SeriesSubtitleRule>();
}

/// <summary>
/// Associe une série à une langue de sous-titres.
/// </summary>
public class SeriesSubtitleRule
{
    /// <summary>
    /// Gets or sets l'identifiant Jellyfin de la série.
    /// </summary>
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets le nom de la série (affichage uniquement).
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets le nom de la langue (affichage uniquement).
    /// </summary>
    public string LanguageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets les codes de langue équivalents, séparés par des virgules (ex. "fre,fra,fr").
    /// </summary>
    public string LanguageCodes { get; set; } = string.Empty;
}
