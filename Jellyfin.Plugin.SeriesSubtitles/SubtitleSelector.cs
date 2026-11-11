using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SeriesSubtitles;

/// <summary>
/// What a rule asks for: subtitles in a language, or no subtitles at all.
/// </summary>
internal sealed class SubtitleTarget
{
    /// <summary>
    /// Value stored in a rule to turn subtitles off.
    /// </summary>
    public const string OffValue = "none";

    private SubtitleTarget(bool off, string[] codes)
    {
        Off = off;
        Codes = codes;
    }

    /// <summary>
    /// Gets a value indicating whether subtitles must be turned off.
    /// </summary>
    public bool Off { get; }

    /// <summary>
    /// Gets the accepted language codes.
    /// </summary>
    public string[] Codes { get; }

    /// <summary>
    /// Reads the value stored in a rule ("none" or "fre,fra,fr").
    /// </summary>
    /// <param name="value">Stored value.</param>
    /// <returns>The target, or null when the value is empty.</returns>
    public static SubtitleTarget? Parse(string? value)
    {
        if (string.Equals(value?.Trim(), OffValue, StringComparison.OrdinalIgnoreCase))
        {
            return new SubtitleTarget(true, Array.Empty<string>());
        }

        var codes = SubtitleSelector.ParseCodes(value);
        return codes is null ? null : new SubtitleTarget(false, codes);
    }

    /// <summary>
    /// Returns the track index to use: -1 for "no subtitles", null when no track matches.
    /// </summary>
    /// <param name="streams">Tracks of the file.</param>
    /// <returns>The index, or null.</returns>
    public int? ResolveIndex(IEnumerable<MediaStream>? streams)
    {
        return Off ? -1 : SubtitleSelector.Pick(streams, Codes)?.Index;
    }
}

/// <summary>
/// Picks the subtitle track matching a language.
/// </summary>
internal static class SubtitleSelector
{
    private static readonly char[] CodeSeparators = { ',', ';', ' ' };
    private static readonly char[] RegionSeparators = { '-', '_' };

    /// <summary>
    /// Turns "fre,fra,fr" into a list of lower-case codes.
    /// </summary>
    /// <param name="value">Comma separated codes.</param>
    /// <returns>The codes, or null when there are none.</returns>
    public static string[]? ParseCodes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var codes = value
            .Split(CodeSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(code => code.Trim().ToLowerInvariant())
            .Where(code => code.Length > 0)
            .Distinct()
            .ToArray();

        return codes.Length == 0 ? null : codes;
    }

    /// <summary>
    /// Returns the best subtitle track in the requested language:
    /// full subtitles first (neither forced nor hearing impaired), then the default track.
    /// </summary>
    /// <param name="streams">Tracks of the file.</param>
    /// <param name="codes">Accepted language codes.</param>
    /// <returns>The selected track, or null when none matches.</returns>
    public static MediaStream? Pick(IEnumerable<MediaStream>? streams, string[] codes)
    {
        if (streams is null)
        {
            return null;
        }

        return streams
            .Where(stream => stream.Type == MediaStreamType.Subtitle && Matches(stream.Language, codes))
            .OrderBy(stream => stream.IsForced)
            .ThenBy(stream => stream.IsHearingImpaired)
            .ThenByDescending(stream => stream.IsDefault)
            .ThenBy(stream => stream.Index)
            .FirstOrDefault();
    }

    private static bool Matches(string? language, string[] codes)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        var value = language.Trim().ToLowerInvariant();
        if (Array.IndexOf(codes, value) >= 0)
        {
            return true;
        }

        // "fr-FR", "pt_BR": also compare the part before the region.
        var cut = value.IndexOfAny(RegionSeparators);
        return cut > 0 && Array.IndexOf(codes, value.Substring(0, cut)) >= 0;
    }
}
