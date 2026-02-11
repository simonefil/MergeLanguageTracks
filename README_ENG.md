# MergeLanguageTracks

Cross-platform console application to merge audio tracks and subtitles from MKV files in different languages.

## What is it for?

It allows you to combine audio tracks and subtitles from MKV files of different releases, useful when you have a version with superior video quality but want to integrate audio or subtitles from another version.

The application automatically processes entire seasons, matching corresponding episodes and applying automatic synchronization to compensate for possible editing differences between releases.

## Typical Use Cases

**1. Add Italian dubbing to an English release**

You have a US/UK release with excellent video and want to add Italian audio from an ITA release.

```bash
MergeLanguageTracks -s "D:\Series.ENG" -l "D:\Series.ITA" -t ita -d "D:\Output" -as
```

**2. Overwrite the source files**

If you don't want a separate output folder, use **-o** to directly overwrite the source files. Useful when you already have a backup or are working on copies.

```bash
MergeLanguageTracks -s "D:\Series.ENG" -l "D:\Series.ITA" -t ita -o -as
```

**3. Replace a lossy track with a lossless one**

The file already has Italian audio but it's a lossy AC3. You found a release with Italian DTS-HD MA and want to replace it.

```bash
MergeLanguageTracks -s "D:\Series" -l "D:\Series.ITA.HDMA" -t ita -ac "DTS-HD MA" -ksa eng,jpn -d "D:\Output" -as
```

With **-ksa eng,jpn** you keep only English and Japanese from the source, discarding the lossy Italian. With **-ac "DTS-HD MA"** you only take the lossless track from the Italian release.

**4. Multilanguage remux from different releases**

Start from the US Blu-ray (best encode) and add audio from European releases. Each step takes the previous output as source.

```bash
MergeLanguageTracks -s "D:\Movie.US" -l "D:\Movie.ITA" -t ita -d "D:\Temp1" -as
MergeLanguageTracks -s "D:\Temp1" -l "D:\Movie.FRA" -t fra -d "D:\Temp2" -as
MergeLanguageTracks -s "D:\Temp2" -l "D:\Movie.GER" -t ger -d "D:\Output" -as
```

**5. Anime with non-standard naming**

Many fansubs use the format "- 05" instead of S01E05. With **-m** you specify a custom regex for matching. Here you only take subtitles because the fansub has better subs but worse video.

```bash
MergeLanguageTracks -s "D:\Anime.BD" -l "D:\Anime.Fansub" -t ita -m "- (\d+)" -so -d "D:\Output" -as
```

**6. Daily show with dates in the filename**

For shows with date-based naming (e.g. Show.2024.03.15.mkv), the pattern captures year, month and day as the episode ID.

```bash
MergeLanguageTracks -s "D:\Show.US" -l "D:\Show.ITA" -t ita -m "(\d{4})\.(\d{2})\.(\d{2})" -d "D:\Output"
```

**7. Filter subtitles from the source**

The source file has 10 subtitle tracks in languages you don't need. With **-kss** you keep only the ones you want from the source, while with **-t** you import the missing ones from the language release.

```bash
MergeLanguageTracks -s "D:\Series.ENG" -l "D:\Series.ITA" -t ita -so -kss eng -d "D:\Output" -as
```

**8. Anime: keep only Japanese audio and import eng+ita**

You have a Japanese BD with dual audio (jpn+eng) and many subtitles. You want to keep only the Japanese audio, discard all existing subs, and import English and Italian audio and subtitles from a multilanguage release. The trick **-kss und** discards all subtitles from the source because no track has language "und".

```bash
MergeLanguageTracks -s "D:\Anime.BD.JPN" -l "D:\Anime.ITA" -t eng,ita -ksa jpn -kss und -d "D:\Output" -as
```

**9. Dry run on a complex configuration**

Before launching a complex merge on an entire season, verify with **-n** that the matching works and the tracks are correct.

```bash
MergeLanguageTracks -s "D:\Series.ENG" -l "D:\Series.ITA" -t ita -ac "E-AC-3" -ksa eng -kss eng -d "D:\Output" -as -at 600 -n
```

## How AutoSync Works

Often releases in different languages have different cuts: longer intros, deleted scenes, different credits. If you do a direct merge, the audio goes out of sync.

AutoSync solves this problem by analyzing the audio of both files and automatically calculating the necessary delay.

**The principle is simple:**

Even though the dubbing is in different languages, the background soundtrack (music, effects, explosions, silences) is identical. The application compares these audio "markers" to find the correct offset.

**How it works technically:**

1. Extracts the first 5 minutes of audio from both files (configurable with **-at**)
2. Analyzes the audio looking for:
   - Silence starts and ends (e.g. pauses between scenes)
   - Sudden volume peaks (explosions, hits, music starting)
3. Compares patterns between source and language
4. Searches for the offset that matches the most markers possible
5. Uses 3 search phases for millisecond precision:
   - Phase 1: coarse search (-60s to +60s, step 500ms)
   - Phase 2: fine search (+/-2s from result, step 10ms)
   - Phase 3: ultra-fine search (+/-100ms, step 1ms)

**Works for subtitles too!**

If you only import subs (**-so**), the application still uses audio to calculate the sync. It takes any audio track from the source file and one from the language file, compares them, and applies the calculated delay to the subtitles.

It doesn't matter what language the audio used for comparison is: the music and effects are always the same.

**When it does NOT work well:**

- Files with completely different audio (e.g. theatrical version vs director's cut with remade scenes)
- Very short files (< 2-3 minutes) where there aren't enough markers
- Audio with very few silences and variations (rare, but it happens)
- Episodes with cut scenes (not at the beginning)

In these cases you'll see a "Low confidence" warning and it's recommended to verify manually or use manual delay.

## Detailed Report

At the end of processing, a report with 3 tables is displayed:

```
========================================
  Detailed Report
========================================

SOURCE FILES:
  Episode     Audio               Subtitles           Size
  ----------------------------------------------------------------
  01_05       eng,jpn             eng                 4.2 GB
  01_06       eng,jpn             eng                 4.1 GB

LANGUAGE FILES:
  Episode     Audio               Subtitles           Size
  ----------------------------------------------------------------
  01_05       ita                 ita                 2.1 GB
  01_06       ita                 ita                 2.0 GB

RESULT FILES:
  Episode     Audio          Subtitles      Size      Delay       FFmpeg    AutoSync  Merge
  --------------------------------------------------------------------------------------------
  01_05       eng,jpn,ita    eng,ita        4.3 GB    +150ms      6850ms    45ms      12500ms
  01_06       eng,jpn,ita    eng,ita        4.2 GB    +145ms      6920ms    42ms      11800ms
```

**Result Files columns:**
- **Delay**: offset applied to imported tracks
- **FFmpeg**: audio extraction/analysis time (I/O bound)
- **AutoSync**: offset calculation time (CPU bound)
- **Merge**: mkvmerge execution time

In dry run mode, Size and Merge show "N/A" because the merge is not executed.

## Audio Codecs

When you specify **-ac** to filter codecs, the matching is **EXACT**, not partial.

**Why this matters:**

If a file has both DTS (core) and DTS-HD MA, and you write **-ac "DTS"**, it takes ONLY the DTS core, not the DTS-HD. If you want DTS-HD Master Audio, you must write **-ac "DTS-HDMA"**.

**Dolby:**

- **AC-3** (alias: AC3, DD) - Dolby Digital, the classic lossy 5.1
- **E-AC-3** (alias: EAC3, DD+, DDP) - Dolby Digital Plus, used for lossy Atmos on streaming
- **TrueHD** - Dolby TrueHD, lossless, used for Atmos on Blu-ray
- **MLP** - The internal container of TrueHD (rarely needs to be specified)

**DTS:**

- **DTS** - DTS Core/Digital Surround only (the base lossy 5.1)
- **DTS-HD MA** (alias: DTS-HDMA) - DTS-HD Master Audio, lossless
- **DTS-HD HR** (alias: DTS-HDHR) - DTS-HD High Resolution, lossy but better than core
- **DTS-ES** - DTS Extended Surround (6.1)
- **DTS:X** (alias: DTSX) - Object-based, extension of DTS-HD MA

**Lossless:**

- **FLAC** - The classic open source lossless
- **PCM** (alias: LPCM, WAV) - Raw uncompressed audio
- **ALAC** - Apple Lossless (rare in remuxes)

**Lossy:**

- **AAC** - Common on streaming and webrips
- **MP3** - Rare nowadays
- **Opus** - Used in WebM, excellent quality at low bitrate
- **Vorbis** - Ogg Vorbis

## Language Codes

Language codes are ISO 639-2 (3 letters). The most common ones:

- **ita** - Italian
- **eng** - English
- **jpn** - Japanese
- **ger** or **deu** - German
- **fra** or **fre** - French
- **spa** - Spanish
- **por** - Portuguese
- **rus** - Russian
- **chi** or **zho** - Chinese
- **kor** - Korean
- **und** - Undefined (unspecified language)

If you mistype a code, the application suggests the correct one:

```
Error: language 'italian' not recognized.
Did you mean: ita?
```

## Requirements

- [MKVToolNix](https://mkvtoolnix.download/) installed (mkvmerge must be in PATH)
- ffmpeg for AutoSync - if you don't have it, it will be downloaded automatically to the **tools/** folder

**Supported platforms:**

- Windows (x64)
- Linux (x64)
- macOS (x64, ARM64)

## Build

Requires .NET 8.0 SDK.

```bash
# Build for the current platform
dotnet build -c Release

# Publish as standalone executable
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
```

## Parameter Reference

### Required

| Short | Long | Description |
|-------|------|-------------|
| -s | --source | Folder with source MKV files |
| -l | --language | Folder with MKV files to take tracks from |
| -t | --target-language | Language code of the tracks to import (e.g.: ita) |

### Output (mutually exclusive, one required)

| Short | Long | Description |
|-------|------|-------------|
| -d | --destination | Folder where resulting files will be saved |
| -o | --overwrite | Overwrite source files (flag, no value) |

### Sync

| Short | Long | Description |
|-------|------|-------------|
| -as | --auto-sync | Automatically calculate the delay |
| -ad | --audio-delay | Manual delay in ms for audio (added to auto-sync if active) |
| -sd | --subtitle-delay | Manual delay in ms for subtitles |
| -at | --analysis-time | Audio analysis duration in seconds (default: 300 = 5 min) |

### Filters

| Short | Long | Description |
|-------|------|-------------|
| -ac | --audio-codec | Only import audio tracks with this codec |
| -so | --sub-only | Only import subtitles, ignore audio |
| -ao | --audio-only | Only import audio, ignore subtitles |
| -ksa | --keep-source-audio | Audio languages to KEEP in the source (others are removed) |
| -kss | --keep-source-subs | Subtitle languages to KEEP in the source |

### Matching

| Short | Long | Description |
|-------|------|-------------|
| -m | --match-pattern | Regex for episode matching. Default: S([0-9]+)E([0-9]+) |
| -r | --recursive | Search in subfolders (default: true) |
| -ext | --extensions | File extensions to search for (default: mkv). Separate with comma: mkv,mp4,avi |

### Common Regex Patterns

The application uses captured groups from the regex to match files. Each group in parentheses is concatenated to create the unique episode ID.

| Format | Example File | Pattern |
|--------|--------------|---------|
| Standard | Series.S01E05.mkv | S([0-9]+)E([0-9]+) |
| With dot | Series.S01.E05.mkv | S([0-9]+)\.E([0-9]+) |
| Format 1x05 | Series.1x05.mkv | ([0-9]+)x([0-9]+) |
| Episode only | Anime - 05.mkv | - ([0-9]+) |
| 3-digit episode | Anime - 005.mkv | - ([0-9]{3}) |
| Daily show | Show.2024.01.15.mkv | ([0-9]{4})\.([0-9]{2})\.([0-9]{2}) |

**How it works:** The pattern **S([0-9]+)E([0-9]+)** captures two groups (season and episode). For "S01E05" it creates the ID "01_05". Source and language files with the same ID are matched together.

### Other

| Short | Long | Description |
|-------|------|-------------|
| -n | --dry-run | Show what it would do without executing |
| -h | --help | Show built-in help |
| -mkv | --mkvmerge-path | Custom path to mkvmerge |
| -tools | --tools-folder | Folder for downloaded ffmpeg |

**Notes:**

- All parameters are case-insensitive
- Supports both short (-s) and long (--source) format
- Supports UNC network paths (\\\\server\\share\\...)
- The default pattern S(\d+)E(\d+) matches names like "Series.S01E05.720p.mkv"
