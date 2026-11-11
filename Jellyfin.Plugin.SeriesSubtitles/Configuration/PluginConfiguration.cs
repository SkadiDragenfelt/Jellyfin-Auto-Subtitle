using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SeriesSubtitles.Configuration;

/// <summary>
/// Plugin settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether a rule also replaces
    /// a subtitle track explicitly requested by the player.
    /// </summary>
    public bool OverrideClientChoice { get; set; }

    /// <summary>
    /// Gets or sets the series rules. They win over library rules.
    /// </summary>
    public SeriesSubtitleRule[] Rules { get; set; } = Array.Empty<SeriesSubtitleRule>();

    /// <summary>
    /// Gets or sets the library rules, applied to every video of a library.
    /// </summary>
    public LibrarySubtitleRule[] LibraryRules { get; set; } = Array.Empty<LibrarySubtitleRule>();
}

/// <summary>
/// Maps a series to a subtitle language.
/// </summary>
public class SeriesSubtitleRule
{
    /// <summary>
    /// Gets or sets the Jellyfin id of the series.
    /// </summary>
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the series name (display only).
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language name (display only).
    /// </summary>
    public string LanguageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the equivalent language codes, comma separated (e.g. "fre,fra,fr"),
    /// or "none" to turn subtitles off.
    /// </summary>
    public string LanguageCodes { get; set; } = string.Empty;
}

/// <summary>
/// Maps a library to a subtitle language.
/// </summary>
public class LibrarySubtitleRule
{
    /// <summary>
    /// Gets or sets the Jellyfin id of the library.
    /// </summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the library name.
    /// </summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language name (display only).
    /// </summary>
    public string LanguageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the equivalent language codes, comma separated (e.g. "fre,fra,fr"),
    /// or "none" to turn subtitles off.
    /// </summary>
    public string LanguageCodes { get; set; } = string.Empty;
}
