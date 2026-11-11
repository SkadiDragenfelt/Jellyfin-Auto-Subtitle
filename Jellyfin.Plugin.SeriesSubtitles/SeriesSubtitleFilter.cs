using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeriesSubtitles.Configuration;
using MediaBrowser.Controller.Entities;
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
/// Global API filter.
/// 1. Before a playback request (POST /Items/{id}/PlaybackInfo): when a rule covers the video,
///    the subtitle track it asks for is injected into the request.
///    The server then does everything else itself (including burn-in when needed).
/// 2. After responses describing a video: the default track is replaced, so that the
///    item page preselects the right track.
/// A series rule wins over the rule of the library the series is in.
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
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="logger">Logger.</param>
    public SeriesSubtitleFilter(ILibraryManager libraryManager, ILogger<SeriesSubtitleFilter> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !HasRules(config))
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
            _logger.LogError(ex, "Series Subtitles: error while preparing the playback request");
        }

        var executed = await next().ConfigureAwait(false);

        try
        {
            PatchResult(executed, request, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Series Subtitles: error while patching the response");
        }
    }

    private static bool HasRules(PluginConfiguration config)
    {
        return (config.Rules is not null && config.Rules.Length > 0)
            || (config.LibraryRules is not null && config.LibraryRules.Length > 0);
    }

    private static object? GetArgument(ActionExecutingContext context, string name)
    {
        return context.ActionArguments.TryGetValue(name, out var value) ? value : null;
    }

    // The request body (PlaybackInfoDto) lives in Jellyfin.Api, which plugins do not
    // reference, so it is read through reflection.
    private static object? ReadBodyProperty(object? body, string propertyName)
    {
        return body?.GetType().GetProperty(propertyName)?.GetValue(body);
    }

    private SubtitleTarget? ResolveTarget(BaseItem? item, PluginConfiguration config)
    {
        if (item is not Video)
        {
            return null;
        }

        if (item is Episode episode && config.Rules is not null)
        {
            foreach (var rule in config.Rules)
            {
                if (rule is not null && Guid.TryParse(rule.SeriesId, out var seriesId) && seriesId == episode.SeriesId)
                {
                    var target = SubtitleTarget.Parse(rule.LanguageCodes);
                    if (target is not null)
                    {
                        return target;
                    }
                }
            }
        }

        if (config.LibraryRules is null || config.LibraryRules.Length == 0)
        {
            return null;
        }

        foreach (var folder in _libraryManager.GetCollectionFolders(item))
        {
            foreach (var rule in config.LibraryRules)
            {
                if (rule is null)
                {
                    continue;
                }

                var sameId = Guid.TryParse(rule.LibraryId, out var libraryId) && libraryId == folder.Id;
                var sameName = !string.IsNullOrEmpty(rule.LibraryName)
                    && string.Equals(rule.LibraryName, folder.Name, StringComparison.OrdinalIgnoreCase);
                if (sameId || sameName)
                {
                    var target = SubtitleTarget.Parse(rule.LanguageCodes);
                    if (target is not null)
                    {
                        return target;
                    }
                }
            }
        }

        return null;
    }

    private PlaybackRequest? PreparePlaybackRequest(ActionExecutingContext context, PluginConfiguration config)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
            || !string.Equals(descriptor.ControllerName, "MediaInfo", StringComparison.Ordinal)
            || GetArgument(context, ItemIdArgument) is not Guid itemId)
        {
            return null;
        }

        var item = _libraryManager.GetItemById(itemId);
        var target = ResolveTarget(item, config);
        if (item is null || target is null)
        {
            return null;
        }

        var request = new PlaybackRequest(target);

        // GET /Items/{id}/PlaybackInfo takes no track argument: only the response gets patched.
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
            // The player asked for a specific track (or "none"): respect it.
            request.Done = true;
            return request;
        }

        var requestedSourceId = GetArgument(context, MediaSourceIdArgument) as string;
        if (string.IsNullOrWhiteSpace(requestedSourceId))
        {
            requestedSourceId = ReadBodyProperty(body, "MediaSourceId") as string;
        }

        var sources = item.GetMediaSources(false);
        MediaSourceInfo? source = null;
        if (!string.IsNullOrWhiteSpace(requestedSourceId))
        {
            source = sources.FirstOrDefault(s => string.Equals(s.Id, requestedSourceId, StringComparison.OrdinalIgnoreCase));
        }
        else if (sources.Count == 1)
        {
            source = sources[0];
        }

        if (source is null)
        {
            // Several versions of the file and none specified: the response gets patched afterwards.
            return request;
        }

        var index = target.ResolveIndex(source.MediaStreams);
        if (!index.HasValue)
        {
            _logger.LogDebug("Series Subtitles: no {Codes} subtitle for {Item}", string.Join(",", target.Codes), item.Name);
            request.Done = true;
            return request;
        }

        // The server only applies the requested track when the media source is named in the request.
        context.ActionArguments[SubtitleIndexArgument] = index.Value;
        context.ActionArguments[MediaSourceIdArgument] = source.Id;
        request.Done = true;

        _logger.LogDebug("Series Subtitles: track {Index} forced for {Item}", index.Value, item.Name);
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
                    ApplyDefault(source, request.Target, true);
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

    private void PatchDto(BaseItemDto? dto, PluginConfiguration config)
    {
        // Only items sent with their media sources (item pages, mostly) need a lookup.
        if (dto?.MediaSources is null)
        {
            return;
        }

        var target = ResolveTarget(_libraryManager.GetItemById(dto.Id), config);
        if (target is null)
        {
            return;
        }

        foreach (var source in dto.MediaSources)
        {
            ApplyDefault(source, target, false);
        }
    }

    private static void ApplyDefault(MediaSourceInfo? source, SubtitleTarget target, bool keepBurnedInChoice)
    {
        if (source?.MediaStreams is null)
        {
            return;
        }

        var index = target.ResolveIndex(source.MediaStreams);
        if (!index.HasValue || source.DefaultSubtitleStreamIndex == index.Value)
        {
            return;
        }

        if (index.Value < 0 && !source.DefaultSubtitleStreamIndex.HasValue)
        {
            // Subtitles are already off.
            return;
        }

        if (keepBurnedInChoice)
        {
            // The stream URL is already built: leave burned-in tracks alone.
            var wanted = source.MediaStreams.FirstOrDefault(s => s.Index == index.Value);
            if (wanted is not null && wanted.DeliveryMethod == SubtitleDeliveryMethod.Encode)
            {
                return;
            }

            var current = source.MediaStreams.FirstOrDefault(s => s.Index == source.DefaultSubtitleStreamIndex);
            if (current is not null && current.DeliveryMethod == SubtitleDeliveryMethod.Encode)
            {
                return;
            }
        }

        source.DefaultSubtitleStreamIndex = index.Value;
    }

    private sealed class PlaybackRequest
    {
        public PlaybackRequest(SubtitleTarget target)
        {
            Target = target;
        }

        public SubtitleTarget Target { get; }

        // true = nothing to patch in the response (already handled, or player choice respected).
        public bool Done { get; set; }
    }
}
