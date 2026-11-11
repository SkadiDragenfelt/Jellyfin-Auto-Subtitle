# Series Subtitles — one subtitle language per series (Jellyfin plugin)

Pick a series and a language in the dashboard: every episode of that series starts with subtitles
in that language, without setting it episode by episode.

> **Status:** builds for Jellyfin 12 and 10.11. Not yet tested on a live server — feedback welcome.

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

**Dashboard → Plugins → Series Subtitles**:

1. Filter and pick the series.
2. Pick the language.
3. Add the rule. It is saved immediately, no restart needed.

The settings page is currently in French.

The strict option forces the language even when the player asks for another track. When it is off,
the series language is the default and can still be changed during playback.

## How it works

- When an episode starts, the player asks the server for playback information
  (`POST /Items/{id}/PlaybackInfo`). The plugin writes the subtitle track of the chosen language into
  that request; the server then does its usual work, including burning subtitles in when the player needs it.
- In responses describing an episode, the default track is replaced, so the episode page preselects
  the right language.
- When several tracks match the language, full subtitles win (neither forced nor hearing impaired),
  then the track flagged as default.

## Limits

- A rule applies to every user of the server.
- A track without a language tag ("Undetermined") cannot be recognised. Name external files like `Episode.fr.srt`.
- When an episode has no subtitles in the chosen language, nothing changes for that episode.
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
- `SeriesSubtitleFilter.cs`: applies the rule to playback requests and episode responses.
- `SubtitleSelector.cs`: picks the track in the requested language.
- `Configuration/`: stored settings and dashboard page.
