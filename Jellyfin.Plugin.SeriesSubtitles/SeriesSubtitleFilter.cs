using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeriesSubtitles.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeriesSubtitles;

/// <summary>
/// Filtre global de l'API.
/// 1. Avant la demande de lecture (POST /Items/{id}/PlaybackInfo) : si l'épisode appartient à une
///    série configurée, la piste de sous-titres de la langue choisie est injectée dans la demande.
///    Le serveur fait alors tout le reste lui-même (y compris l'incrustation si nécessaire).
/// 2. Après les réponses qui décrivent un épisode : la piste par défaut est remplacée, pour que
///    la fiche de l'épisode présélectionne la bonne langue.
/// </summary>
public class SeriesSubtitleFilter : IAsyncActionFilter
{
    private const string ItemIdArgument = "itemId";
    private const string SubtitleIndexArgument = "subtitleStreamIndex";
    private const string MediaSourceIdArgument = "mediaSourceId";
    private const string LiveStreamIdArgument = "liveStreamId";
    private const string BodyArgument = "playbackInfoDto";

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<SeriesSubtitleFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesSubtitleFilter"/> class.
    /// </summary>
    /// <param name="libraryManager">Bibliothèque Jellyfin.</param>
    /// <param name="logger">Journal.</param>
    public SeriesSubtitleFilter(ILibraryManager libraryManager, ILogger<SeriesSubtitleFilter> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var config = Plugin.Instance?.Configuration;
        if (config?.Rules is null || config.Rules.Length == 0)
        {
            await next().ConfigureAwait(false);
            return;
        }

        PlaybackRequest? request = null;
        try
        {
            request = PreparePlaybackRequest(context, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Series Subtitles : erreur avant la demande de lecture");
        }

        var executed = await next().ConfigureAwait(false);

        try
        {
            PatchResult(executed, request, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Series Subtitles : erreur lors de la modification de la réponse");
        }
    }

    private static string[]? FindCodes(PluginConfiguration config, Guid seriesId)
    {
        foreach (var rule in config.Rules)
        {
            if (rule is not null && Guid.TryParse(rule.SeriesId, out var id) && id == seriesId)
            {
                return SubtitleSelector.ParseCodes(rule.LanguageCodes);
            }
        }

        return null;
    }

    private static object? GetArgument(ActionExecutingContext context, string name)
    {
        return context.ActionArguments.TryGetValue(name, out var value) ? value : null;
    }

    // Le corps de la requête (PlaybackInfoDto) vit dans Jellyfin.Api, qui n'est pas
    // référencé par les plugins : on le lit donc par réflexion.
    private static object? ReadBodyProperty(object? body, string propertyName)
    {
        return body?.GetType().GetProperty(propertyName)?.GetValue(body);
    }

    private PlaybackRequest? PreparePlaybackRequest(ActionExecutingContext context, PluginConfiguration config)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
            || !string.Equals(descriptor.ControllerName, "MediaInfo", StringComparison.Ordinal)
            || GetArgument(context, ItemIdArgument) is not Guid itemId)
        {
            return null;
        }

        if (_libraryManager.GetItemById(itemId) is not Episode episode)
        {
            return null;
        }

        var codes = FindCodes(config, episode.SeriesId);
        if (codes is null)
        {
            return null;
        }

        var request = new PlaybackRequest(codes);

        // GET /Items/{id}/PlaybackInfo n'accepte pas de piste : seule la réponse sera corrigée.
        var parameters = descriptor.Parameters;
        if (!parameters.Any(p => string.Equals(p.Name, SubtitleIndexArgument, StringComparison.Ordinal))
            || !parameters.Any(p => string.Equals(p.Name, MediaSourceIdArgument, StringComparison.Ordinal)))
        {
            return request;
        }

        var body = GetArgument(context, BodyArgument);

        var liveStreamId = (GetArgument(context, LiveStreamIdArgument) as string)
            ?? (ReadBodyProperty(body, "LiveStreamId") as string);
        if (!string.IsNullOrEmpty(liveStreamId))
        {
            request.Done = true;
            return request;
        }

        int? explicitIndex = (GetArgument(context, SubtitleIndexArgument) as int?)
            ?? (ReadBodyProperty(body, "SubtitleStreamIndex") as int?);
        if (explicitIndex.HasValue && !config.OverrideClientChoice)
        {
            // Le lecteur a choisi une piste précise (ou "aucun") : on la respecte.
            request.Done = true;
            return request;
        }

        var requestedSourceId = GetArgument(context, MediaSourceIdArgument) as string;
        if (string.IsNullOrWhiteSpace(requestedSourceId))
        {
            requestedSourceId = ReadBodyProperty(body, "MediaSourceId") as string;
        }

        var sources = episode.GetMediaSources(false);
        MediaSourceInfo? target = null;
        if (!string.IsNullOrWhiteSpace(requestedSourceId))
        {
            target = sources.FirstOrDefault(s => string.Equals(s.Id, requestedSourceId, StringComparison.OrdinalIgnoreCase));
        }
        else if (sources.Count == 1)
        {
            target = sources[0];
        }

        if (target is null)
        {
            // Plusieurs versions du fichier et aucune précisée : la réponse sera corrigée après coup.
            return request;
        }

        var stream = SubtitleSelector.Pick(target.MediaStreams, codes);
        if (stream is null)
        {
            _logger.LogDebug("Series Subtitles : aucun sous-titre {Codes} pour {Episode}", string.Join(",", codes), episode.Name);
            request.Done = true;
            return request;
        }

        // Le serveur n'applique la piste demandée que si la source est nommée dans la demande.
        context.ActionArguments[SubtitleIndexArgument] = stream.Index;
        context.ActionArguments[MediaSourceIdArgument] = target.Id;
        request.Done = true;

        _logger.LogDebug("Series Subtitles : piste {Index} imposée pour {Episode}", stream.Index, episode.Name);
        return request;
    }

    private void PatchResult(ActionExecutedContext executed, PlaybackRequest? request, PluginConfiguration config)
    {
        if (executed.Result is not ObjectResult objectResult || objectResult.Value is null)
        {
            return;
        }

        switch (objectResult.Value)
        {
            case PlaybackInfoResponse response:
                if (request is null || request.Done || response.MediaSources is null)
                {
                    return;
                }

                foreach (var source in response.MediaSources)
                {
                    ApplyDefault(source, request.Codes, true);
                }

                break;

            case BaseItemDto dto:
                PatchDto(dto, config);
                break;

            case QueryResult<BaseItemDto> queryResult:
                if (queryResult.Items is not null)
                {
                    foreach (var dto in queryResult.Items)
                    {
                        PatchDto(dto, config);
                    }
                }

                break;

            case IEnumerable<BaseItemDto> dtos:
                foreach (var dto in dtos)
                {
                    PatchDto(dto, config);
                }

                break;
        }
    }

    private static void PatchDto(BaseItemDto? dto, PluginConfiguration config)
    {
        if (dto?.MediaSources is null || dto.SeriesId is not Guid seriesId)
        {
            return;
        }

        var codes = FindCodes(config, seriesId);
        if (codes is null)
        {
            return;
        }

        foreach (var source in dto.MediaSources)
        {
            ApplyDefault(source, codes, false);
        }
    }

    private static void ApplyDefault(MediaSourceInfo? source, string[] codes, bool keepBurnedInChoice)
    {
        if (source?.MediaStreams is null)
        {
            return;
        }

        var stream = SubtitleSelector.Pick(source.MediaStreams, codes);
        if (stream is null || source.DefaultSubtitleStreamIndex == stream.Index)
        {
            return;
        }

        if (keepBurnedInChoice)
        {
            // L'adresse de lecture est déjà calculée : on ne touche pas aux pistes incrustées dans l'image.
            if (stream.DeliveryMethod == SubtitleDeliveryMethod.Encode)
            {
                return;
            }

            var current = source.MediaStreams.FirstOrDefault(s => s.Index == source.DefaultSubtitleStreamIndex);
            if (current is not null && current.DeliveryMethod == SubtitleDeliveryMethod.Encode)
            {
                return;
            }
        }

        source.DefaultSubtitleStreamIndex = stream.Index;
    }

    private sealed class PlaybackRequest
    {
        public PlaybackRequest(string[] codes)
        {
            Codes = codes;
        }

        public string[] Codes { get; }

        // true = rien à corriger dans la réponse (déjà traité, ou choix du lecteur respecté).
        public bool Done { get; set; }
    }
}
