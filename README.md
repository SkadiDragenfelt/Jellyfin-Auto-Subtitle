# Series Subtitles — subtitle rules per library or per series (Jellyfin plugin)

Choose which subtitles a video starts with, once, instead of setting it on every episode:

- **Per library**: for example *Anime* in French, *Movies* with no subtitles. New videos added to the library follow the rule automatically.
- **Per series**: one or several series at a time. A series rule wins over the rule of its library.

Works on Jellyfin 12 and 10.11.

## Install

1. In Jellyfin, open **Dashboard → Plugins → Repositories** and add a new repository with this URL:

   ```
   https://raw.githubusercontent.com/SkadiDragenfelt/Jellyfin-Auto-Subtitle/main/manifest.json
   ```

2. Open the plugin **Catalog**, install **Series Subtitles**, then restart Jellyfin.

Jellyfin picks the right build by itself: plugin 2.x for Jellyfin 12, plugin 1.x for Jellyfin 10.11.

### Manual install

Download the zip for your server from the [latest release](https://github.com/SkadiDragenfelt/Jellyfin-Auto-Subtitle/releases/latest),
extract the DLL into a `SeriesSubtitles` folder inside the server's `plugins` folder, then restart Jellyfin.

- Linux: `/var/lib/jellyfin/plugins/`
- Docker: `/config/plugins/`
- Windows: `C:\ProgramData\Jellyfin\Server\plugins\` or `%LOCALAPPDATA%\jellyfin\plugins\`

## Use

**Dashboard → Plugins → Series Subtitles**. Changes are saved immediately, no restart needed.

- **Library rules**: pick a library, pick a language or *No subtitles*, add the rule.
- **Series rules**: filter the list, tick one or several series (or *Select all shown*), pick a language
  or *No subtitles*, add the rule.
- **Apply rules even when the player asks for another track**: off by default, so a rule sets the
  default track and subtitles can still be changed during playback. Turn it on to always apply the rule
  when playback starts.

## How it works

- When a video starts, the player asks the server for playback information
  (`POST /Items/{id}/PlaybackInfo`). The plugin writes the subtitle track required by the rule into
  that request; the server then does its usual work, including burning subtitles in when the player needs it.
- In responses describing a video, the default track is replaced, so the item page preselects
  the right track.
- When several tracks match the language, full subtitles win (neither forced nor hearing impaired),
  then the track flagged as default.

## Limits

- A rule applies to every user of the server.
- A track without a language tag ("Undetermined") cannot be recognised. Name external files like `Episode.fr.srt`.
- When a video has no subtitles in the chosen language, nothing changes for that video.
- A player that ignores the default track sent by the server is not affected.

## Build

| Jellyfin server | SDK | Command |
| --- | --- | --- |
| 12.x | .NET 10 | `dotnet publish Jellyfin.Plugin.SeriesSubtitles -c Release -o out` |
| 10.11.x | .NET 9 | `dotnet publish Jellyfin.Plugin.SeriesSubtitles -c Release -p:JellyfinVersion=10.11.0 -o out` |

Every push to `main` builds both versions, publishes them as a GitHub release and updates `manifest.json`
(see `.github/workflows/build.yml`).

## Files

- `Plugin.cs`: plugin declaration and settings page registration.
- `PluginServiceRegistrator.cs`: hooks the filter into the server API.
- `SeriesSubtitleFilter.cs`: applies the rules to playback requests and item responses.
- `SubtitleSelector.cs`: picks the track required by a rule.
- `Configuration/`: stored settings and dashboard page.
