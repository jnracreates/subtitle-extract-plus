
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

Then run **Dashboard → Scheduled Tasks → Extract Subtitles Plus → Run**, or set up a daily schedule trigger.

## Requirements

- Jellyfin 12.0 or later
- .NET 10.0 SDK (for building from source)

## Building from source

```bash
git clone https://github.com/jnracreates/subtitle-extract-plus.git
cd subtitle-extract-plus
dotnet build --configuration Release

The compiled DLL is at Jellyfin.Plugin.SubtitleExtract/bin/Release/net10.0/SubtitleExtractPlus.dll.
Credits

Forked from [jellyfin-plugin-subtitle-extract](https://github.com/jellyfin/jellyfin-plugin-subtitle-extract) by the Jellyfin team. Licensed under the MIT License.
