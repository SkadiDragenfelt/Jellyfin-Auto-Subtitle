using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SeriesSubtitles;

/// <summary>
/// Choisit la piste de sous-titres correspondant à une langue.
/// </summary>
internal static class SubtitleSelector
{
    private static readonly char[] CodeSeparators = { ',', ';', ' ' };
    private static readonly char[] RegionSeparators = { '-', '_' };

    /// <summary>
    /// Transforme "fre,fra,fr" en liste de codes en minuscules.
    /// </summary>
    /// <param name="value">Codes séparés par des virgules.</param>
    /// <returns>Les codes, ou null s'il n'y en a aucun.</returns>
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
    /// Renvoie la meilleure piste de sous-titres dans la langue demandée :
    /// d'abord les sous-titres complets (ni "forcés" ni malentendants), puis la piste par défaut.
    /// </summary>
    /// <param name="streams">Pistes du fichier.</param>
    /// <param name="codes">Codes de langue acceptés.</param>
    /// <returns>La piste retenue, ou null si aucune ne correspond.</returns>
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

        // "fr-FR", "pt_BR" : on compare aussi la partie avant la région.
        var cut = value.IndexOfAny(RegionSeparators);
        return cut > 0 && Array.IndexOf(codes, value.Substring(0, cut)) >= 0;
    }
}
