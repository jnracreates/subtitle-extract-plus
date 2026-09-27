<img width="1246" height="238" alt="subtitleextractpluslogo" src="https://github.com/user-attachments/assets/7a9f004b-e093-4fad-ade4-35fa58686970" />


# SubtitleExtractPlus

A fork of [Jellyfin Subtitle Extract](https://github.com/jellyfin/jellyfin-plugin-subtitle-extract) with per-language and forced-track filtering, ISO 639-1 output filenames, and a path filter for testing.

## Why this fork?

The official Jellyfin Subtitle Extract plugin extracts every embedded subtitle stream to Jellyfin's internal cache. This fork gives you control over *which* tracks get extracted, and can optionally write subtitles **next to your media files** using Jellyfin's external subtitle naming convention (`.en.srt`, `.en.default.srt`, `.en.forced.srt`):

- **Language filtering** — only extract the languages you want (`en`, `fr`, `de`, …)
- **Forced / non-forced filtering** — extract only forced tracks, only full tracks, or both
- **ISO 639-1 output** — writes `.en.srt` not `.eng.srt`, matching Jellyfin's convention
- **Save with media (optional)** — when enabled, subtitles land next to the video using Jellyfin's external subtitle convention. When disabled, they go to Jellyfin's internal cache instead (same as the official plugin).
- **Path filter** — limit extraction to a single movie or season for testing
- **Idempotent** — skips files that already have a subtitle, so re-runs are fast

## Installation

### From a Jellyfin plugin repository (recommended)

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**
2. Click **+** to add a new repository
3. Enter the following URL: https://raw.githubusercontent.com/jnracreates/subtitle-extract-plus/master/manifest.json
4. Save, then go to **Catalog**, find **Subtitle Extract Plus**, and install

### Manual install

1. Download the latest `SubtitleExtractPlus.dll` from the [Releases page](https://github.com/jnracreates/subtitle-extract-plus/releases)
2. Create a folder called `SubtitleExtractPlus` inside your Jellyfin plugins directory
3. Place the DLL inside that folder
4. Restart Jellyfin

## Configuration

After installing, go to **Dashboard → Plugins → Subtitle Extract Plus → Settings**.

| Setting | Description |
|---|---|
| **Languages to extract** | Comma-separated ISO 639-1 codes (`en, fr, de`). Empty = all languages. |
| **Include forced subtitles** | Extract forced tracks (e.g. alien dialogue in an English film). |
| **Include non-forced subtitles** | Extract full subtitle tracks. |
| **Save subtitles next to media files** | When enabled, writes `.en.srt` next to the video. When disabled, uses Jellyfin's internal cache. |
| **Path filter** | Limit extraction to items whose path starts with this string. Empty = process everything. Useful for testing on one movie. |

<img width="826" height="1154" alt="subexpls1" src="https://github.com/user-attachments/assets/a8d744cb-2584-4e84-8880-330c2c81e946" />
<img width="829" height="1124" alt="subexpls2" src="https://github.com/user-attachments/assets/9f7c7348-83b0-4aa2-8660-37e257ae8887" />


Then run **Dashboard → Scheduled Tasks → Extract Subtitles Plus → Run**, or set up a daily schedule trigger.

## Re-extracting

To force re-extraction of a specific file, delete the generated `.srt` next to the media file and run the task again. The plugin checks for existing files on disk and will re-extract anything missing.

## Requirements

- Jellyfin 12.0 or later
- .NET 10.0 SDK (for building from source)

## Building from source

```bash
git clone https://github.com/jnracreates/subtitle-extract-plus.git
cd subtitle-extract-plus
dotnet build --configuration Release
```
The compiled DLL is at Jellyfin.Plugin.SubtitleExtract/bin/Release/net10.0/SubtitleExtractPlus.dll.
Credits

Forked from [jellyfin-plugin-subtitle-extract](https://github.com/jellyfin/jellyfin-plugin-subtitle-extract) by the Jellyfin team. Licensed under the MIT License.

## Related projects

**[ytfinall](https://github.com/jnracreates/ytfinall)** — Self-hosted YouTube → Jellyfin downloader with per-user libraries, a web UI, and a browser extension. Users log in with their Jellyfin account, add channels or single videos, and get them downloaded into a private library only they can see.
