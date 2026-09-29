<img width="1246" height="238" alt="subtitleextractpluslogo" src="https://github.com/user-attachments/assets/7a9f004b-e093-4fad-ade4-35fa58686970" />

# SubtitleExtractPlus

A fork of [Jellyfin Subtitle Extract](https://github.com/jellyfin/jellyfin-plugin-subtitle-extract) with per-language and forced-track filtering, configurable default-language preference, an extraction cache, on-add extraction, and ISO 639-1 output filenames.

## Why this fork?

The official Jellyfin Subtitle Extract plugin extracts every embedded subtitle stream to Jellyfin's internal cache. This fork gives you control over *which* tracks get extracted, *which one* becomes the default, and *when* extraction happens — and can optionally write subtitles **next to your media files** using Jellyfin's external subtitle naming convention (`.en.srt`, `.en.default.srt`, `.en.forced.srt`):

- **Language filtering** — only extract the languages you want (`en`, `fr`, `de`, …)
- **Forced / non-forced filtering** — extract only forced tracks, only full tracks, or both
- **Default-language selection** — ranked list of preferred languages; the first one present in the file becomes the `.default.` sidecar, regardless of the container's own default flag
- **Configurable fallback** — when none of your preferred languages is present, choose: skip, use the container default, use the first stream, or fall back to a specified last-resort language
- **Extraction cache** — files whose size and mtime haven't changed since the last run are skipped, so re-runs and on-add extraction are effectively free
- **On-add extraction** — new episodes and movies are extracted automatically (debounced) as they land in the library
- **ISO 639-1 output** — writes `.en.srt` not `.eng.srt`, matching Jellyfin's convention
- **Save with media (optional)** — when enabled, subtitles land next to the video using Jellyfin's external subtitle convention. When disabled, they go to Jellyfin's internal cache instead (same as the official plugin).
- **Per-stream extraction** — each subtitle track is extracted with its own ffmpeg process, so an empty or malformed track cannot cascade failures into others
- **Path filter** — limit extraction to a single movie or season for testing

## Installation

### From a Jellyfin plugin repository (recommended)

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**
2. Click **+** to add a new repository
3. Enter the following URL: `https://raw.githubusercontent.com/jnracreates/subtitle-extract-plus/master/manifest.json`
4. Save, then go to **Catalog**, find **Subtitle Extract Plus**, and install
5. Restart Jellyfin

### Manual install

1. Download the latest release zip from the [Releases page](https://github.com/jnracreates/subtitle-extract-plus/releases)
2. Extract into `<jellyfin-config>/plugins/Subtitle Extract Plus_<version>/` (create the folder if it doesn't exist)
3. Ensure the plugin folder and its contents are owned by the user Jellyfin runs as (e.g. `chown -R 1000:1000`)
4. Restart Jellyfin

## Configuration

After installing, go to **Dashboard → Plugins → Subtitle Extract Plus → Settings**.

### Subtitle types

| Setting | Description |
|---|---|
| **Text subtitles** | SRT, ASS/SSA, WebVTT, and other text-based formats. |
| **Graphical subtitles** | PGS, VOBSUB, DVD. Note: image-based subs cannot be converted to SRT without OCR — only text streams are extracted. |

### Languages and default selection

| Setting | Description |
|---|---|
| **Languages to extract** | Comma-separated ISO 639-1 codes (`en, fr, de`). Empty = all languages. |
| **Preferred default languages** | Ranked list. The first language found in the file becomes the `.default.` sidecar. Example: `en, fr, uk`. Empty = fall back to the setting below. |
| **Fallback when none is present** | Choose: *Skip* (don't mark any default), *Use container default* (respect the file's own default flag), *Use first stream*, or *Use last-resort language*. |
| **Last-resort language** | Only used when the fallback above is set to "last-resort language". |

### Track selection

| Setting | Description |
|---|---|
| **Include forced subtitles** | Extract forced tracks (e.g. alien dialogue in an English film). |
| **Include non-forced subtitles** | Extract full subtitle tracks. |

### Output

| Setting | Description |
|---|---|
| **Save subtitles next to media files** | When enabled, writes `.en.srt` next to the video. When disabled, uses Jellyfin's internal cache. |
| **Path filter** | Limit extraction to items whose path starts with this string. Empty = process everything. Useful for testing on one movie or season. |

### New item extraction

| Setting | Description |
|---|---|
| **Extract when items are added to the library** | Runs the extractor on new episodes and movies shortly after they appear. Useful if you've disabled embedded subtitle display in your library. |
| **Debounce (seconds)** | How long to wait after the last item-added event before starting. Default 30. Prevents an ffmpeg storm during bulk imports. |

### Cache

| Setting | Description |
|---|---|
| **Skip files whose media hasn't changed** | Enables the extraction cache, keyed on media file size and mtime. |
| **Cache TTL (days)** | `0` = never expires. |
<img width="822" height="1158" alt="subexpls1" src="https://github.com/user-attachments/assets/440ea433-ff49-438b-8cec-1c2fd16ce572" />
<img width="822" height="985" alt="subexpls2" src="https://github.com/user-attachments/assets/62c2b9e6-934c-43ab-830d-39a4e9ed720f" />


Then run **Dashboard → Scheduled Tasks → Extract Subtitles Plus → Run**, or set up a daily schedule trigger.

## Recommended setup

For a Jellyfin library where you want a single, curated subtitle list:

1. Set your TV/Movie libraries to **Allow text subtitles** for embedded subs.
2. In the plugin, configure:
   - **Languages to extract:** `en` (or your preference)
   - **Preferred default languages:** `en`
   - **Include forced:** on
   - **Include non-forced:** off (for forced-only workflow)
   - **Save next to media:** on
   - **Extract on item added:** on
   - **Use extraction cache:** on
3. Run the scheduled task once to backfill existing items.
4. From then on, new items are extracted automatically.

## How default-language selection works

Given a file with subtitle streams in this order — `fr, pl, uk` — and a configuration of:

- **Preferred default languages:** `en, uk`
- **Fallback:** *Skip*

The plugin picks `uk` (the first preferred language actually present) and marks it as the default sidecar. Files without any preferred language produce non-default sidecars for the remaining languages (subject to your language filter), but no `.default.` sidecar.

## Library setting note

Leave embedded subtitles enabled in your library settings (**Dashboard → Libraries → [your library] → Edit → "Allow Text"** or **"Allow All"**).

The plugin reads subtitle stream metadata from Jellyfin's database. If you set the library to **"Allow None"**, Jellyfin strips subtitle streams from the item's metadata, the plugin sees nothing to extract, and the task completes without processing anything.

With embedded subtitles allowed, Jellyfin still prefers any external `.en.srt` / `.en.forced.srt` the plugin wrote over extracting from the container. Playback extraction only happens for files the plugin hasn't processed yet.

## How extraction caching works

The cache is keyed on the media file's size and mtime. Every run:

- If size and mtime match the last successful extraction → skipped (fast)
- If the file has changed → re-extracted
- Files where every stream was filtered out by your forced/non-forced settings are *not* cached, so flipping those settings picks them up on the next run without a manual cache clear

The cache lives at `<jellyfin-config>/plugins/configurations/subtitle-extract-cache.json`. Delete it to force a full re-extraction on the next run.

## Re-extracting

To force re-extraction of a specific file:

- **Without the cache:** delete the generated `.srt` next to the media file and run the task. The plugin checks for existing files and will re-extract anything missing.
- **With the cache:** also delete the cache file (or disable the cache setting temporarily) — otherwise the file is skipped because its media mtime hasn't changed.

## Requirements

- Jellyfin 12.0 or later
- .NET 10.0 SDK (for building from source)

## Building from source

```bash
git clone https://github.com/jnracreates/subtitle-extract-plus.git
cd subtitle-extract-plus
dotnet build --configuration Release
```

The compiled DLL is at
`Jellyfin.Plugin.SubtitleExtract/bin/Release/net10.0/SubtitleExtractPlus.dll`

## Changelog

### 5.0.0.0

- Ranked default-language preference with configurable fallback chain
- Extraction cache (size + mtime keyed)
- On-add extraction with debounce
- Per-stream ffmpeg extraction (empty-track resilience)
- Jellyfin 12 `IPluginServiceRegistrator` support
- Fixed: config save failing silently on enum serialization
- Fixed: extraction skipped when the library exposes no embedded subtitle streams
- Fixed: empty forced tracks causing whole-file extraction failure

### 4.0.0.0

- Per-language and forced/non-forced track selection
- Save subtitles next to media files using Jellyfin's external subtitle naming convention
- ISO 639-1 output filenames
- Path filter for testing

## Credits

Forked from [jellyfin-plugin-subtitle-extract](https://github.com/jellyfin/jellyfin-plugin-subtitle-extract) by the Jellyfin team. Licensed under the MIT License.
