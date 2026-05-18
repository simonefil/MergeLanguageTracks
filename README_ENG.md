# ![icon](icons/icon-48.png) RemuxForge

Cross-platform application for two separate MKV workflows:

- **Remux**: merges audio tracks and subtitles from MKV files in different languages, with automatic synchronization between releases with different editing or speed.
- **Split**: cuts HEVC/AVC MKV files into frame-perfect segments while preserving VFR, chapters, audio and subtitles.

Available through two interfaces: CLI (command line) and WebUI (web interface). The CLI always requires `--mode remux` or `--mode split`; the WebUI shows a `Remux | Split` switch at the top and remembers the last selected mode.

## Key features

- Remux mode for importing audio and subtitles from other releases, including full seasons
- Synchronization: speed correction for global speed differences, frame-sync for constant delay, Deep Analysis for constant delay plus cuts/insertions
- Filter by language, audio codec, subtitles, both for import and for keeping from source
- Audio post-processing to FLAC, LPCM, AAC or Opus, with peak normalization, 24bit -> 16bit and track renaming
- Post-merge video encoding with customizable profiles (x264, x265, SVT-AV1)
- Optional GPU acceleration for video decoding during analysis phases
- Two interfaces: scriptable CLI and WebUI for browser and headless servers
- Docker deployment with optional GPU support
- Split mode for chapter patterns, explicit ranges, split-at, trim, single chapters and folder sources

## Requirements

- [MKVToolNix](https://mkvtoolnix.download/) (`mkvmerge`, `mkvextract`, `mkvpropedit`)
- [ffmpeg](https://ffmpeg.org/) (`ffmpeg`, `ffprobe`)
- [mediainfo CLI](https://mediainfo.sourceforge.net/) (`mediainfo`)
- UTF-8 locale on Linux

Tool paths can be auto-detected or configured from the WebUI in **Settings > Tool paths**.

**Platforms:**

| Platform | Architectures |
|----------|---------------|
| Windows | x64 |
| Linux | x64, ARM64 |
| macOS | x64, ARM64 |
| Docker | x64 (image with mkvtoolnix, ffmpeg and mediainfo preinstalled) |

## Installation and startup

### Desktop: CLI

Download the archive for your platform from the [release page](https://github.com/simonefil/RemuxForge/releases), extract and run.

- **Windows**: open a terminal and launch `RemuxForge.Cli.exe` with parameters
- **Linux/macOS**: `chmod +x RemuxForge.Cli && ./RemuxForge.Cli` with parameters

Launching without parameters shows the CLI help. With parameters it runs in CLI mode.

### Desktop: WebUI

Download the WebUI archive from the [release page](https://github.com/simonefil/RemuxForge/releases), extract and run.

- **Windows**: double click on `RemuxForge.Web.exe`
- **Linux/macOS**: `chmod +x RemuxForge.Web && ./RemuxForge.Web`

Open `http://localhost:5000` in your browser. The port is configurable with the `REMUXFORGE_PORT` environment variable or, if the variable is not set, with `--port <number>`.

### Docker

> **Note:** the following examples need to be adapted to your own configuration. The volume paths, port, and especially the user mapping (`user`) must correspond to a user with read/write permissions on the mounted folders. If the specified user does not have access to the storage, the container will not work.

```bash
docker run -d \
  --name remuxforge \
  -p 5000:5000 \
  -e REMUXFORGE_PORT=5000 \
  -e REMUXFORGE_DATA_DIR=/data \
  -v /path/to/config:/data:rw \
  -v /path/to/media:/media:rw \
  draknodd/remuxforge:latest
```

**Docker Compose:**

```yaml
services:
  remuxforge:
    image: draknodd/remuxforge:latest
    container_name: remuxforge
    restart: unless-stopped
    user: "1000:1000"  # adapt to your own user (id -u / id -g)
    ports:
      - "5000:5000"
    environment:
      - REMUXFORGE_PORT=5000
      - REMUXFORGE_DATA_DIR=/data
    volumes:
      - /path/to/config:/data:rw        # configuration folder
      - /path/to/media:/media:rw         # video files folder
```

### Docker with GPU acceleration

Video decoding during analysis phases (speed correction, frame-sync, Deep Analysis) can be accelerated via GPU if the advanced `Hardware Acceleration` option is enabled. When enabled, ffmpeg uses `-hwaccel auto` and automatically selects the available backend in the container.

**NVIDIA (NVDEC):**

Requires the [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html) installed on the host.

```bash
# Install nvidia-container-toolkit on the host
sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker

# Start the container with GPU access
docker run -d \
  --name remuxforge \
  --gpus all \
  -e NVIDIA_DRIVER_CAPABILITIES=compute,utility,video \
  -p 5000:5000 \
  -e REMUXFORGE_PORT=5000 \
  -e REMUXFORGE_DATA_DIR=/data \
  -v /path/to/config:/data:rw \
  -v /path/to/media:/media:rw \
  draknodd/remuxforge:latest
```

**Intel/AMD (VAAPI):**

```bash
docker run -d \
  --name remuxforge \
  --device /dev/dri:/dev/dri \
  -p 5000:5000 \
  -e REMUXFORGE_PORT=5000 \
  -e REMUXFORGE_DATA_DIR=/data \
  -v /path/to/config:/data:rw \
  -v /path/to/media:/media:rw \
  draknodd/remuxforge:latest
```

### Environment variables

| Variable | Description | Default |
|----------|-------------|---------|
| REMUXFORGE_PORT | WebUI HTTP port | 5000 |
| REMUXFORGE_DATA_DIR | Directory for configuration and data (.remux-forge) | Executable directory |
| REMUXFORGE_LOG_FILE | Log file path. When set, enables file logging | Not active |

![Main interface (Nord theme)](images/nord.png)

## Remux Mode

Remux mode imports audio tracks and subtitles from one MKV release into another. It handles episode matching, language/codec filters, synchronization, audio post-processing, track renaming, video encoding and final reports.

Synchronization has three separate paths:

- **Speed correction**: fixes global speed differences, for example PAL/NTSC. Default `off`; `auto` only runs on reliable CFR sources, while VFR requires `manual` with an explicit stretch factor.
- **Frame-sync**: calculates a single constant delay. Use it when both videos have the same edit and only differ by initial offset.
- **Deep Analysis**: calculates initial delay and cut/insert operations. Use it when scenes are missing, added or edited differently.

Frame-sync and Deep Analysis are mutually exclusive. If a release has both a speed mismatch and local edits, set the global stretch manually and use Deep Analysis for the edit map.

### WebUI

The WebUI is batch-oriented: folder configuration, scan/matching, analysis, merge and result inspection. The episode detail panel shows status, delay, planned operations, resulting tracks and timings for the main phases.

#### Shortcut Keys

| Key | Action |
|-----|--------|
| F2 | Open configuration |
| F5 | Scan folders and match episodes |
| F6 | Analyze selected episode |
| F7 | Analyze all pending episodes |
| F8 | Skip/Unskip selected episode |
| F9 | Merge selected episode |
| F10 | Merge all analyzed episodes |
| F12 | Request stop for the current operation |
| Enter | Episode context menu |
| Ctrl+Q | Exit |

#### Context Menu

Right-clicking on an episode (or pressing Enter) opens a context menu with the following options:

- **Delay**: edit the manual delay for the selected episode
- **MediaInfo source**: shows the full MediaInfo report for the source file
- **MediaInfo language**: shows the MediaInfo report for the language file
- **MediaInfo result**: shows the MediaInfo report for the resulting file (available after merge)

MediaInfo options are visible only if the mediainfo tool is configured and the corresponding file exists. The report shows all track information (codec, channels, bitrate, resolution, language, etc.) and can be copied to clipboard.

#### Menu

- **File**: Configuration (F2), Exit (Ctrl+Q)
- **Actions**: Scan files (F5), Analyze selected (F6), Analyze all (F7), Skip/Unskip (F8), Process selected (F9), Process all (F10)
- **Settings**: Tool paths, Audio, Encoding profiles, Advanced
- **View**: Pipeline (shows the sequence of operations that will be executed based on the current configuration: sync, conversion, merge, encoding)
- **Theme**: change graphical theme (8 themes)
- **Help**: Info

#### Configuration (F2)

The configuration dialog groups all processing options:

![Configuration dialog](images/config.png)

- **Folders**: Source, Language, Destination, with browse button for each. Checkbox for overwrite source and recursive search
- **Language and Tracks**: Target language, Audio codec, Keep source audio/codec/sub, Subtitles only, Audio only, Rename tracks
- **Synchronization**: Speed correction (`off`/`auto`/`manual` with fixed stretch), Frame-sync, Deep Analysis and manual delays. Frame-sync and Deep Analysis are exclusive.
- **Matching**: Match pattern (regex) and file extensions
- **Audio post-processing**: Audio format (flac/lpcm/aac/opus), audio scope, Audio source fill, 24bit -> 16bit, normalization and audio renaming
- **Video post-processing**: Encoding profile

#### Settings Menu

- **Tool paths**: Paths to mkvmerge, mkvextract, mkvpropedit, ffmpeg, ffprobe, mediainfo and temporary files folder. Tools are auto-detected at startup. ffmpeg can be downloaded from the interface on Windows/Linux; on macOS use Homebrew or configure the path manually
- **Audio**: FLAC compression level and AAC/Opus bitrate per channel layout (mono, stereo, 5.1, 7.1)
- **Encoding profiles**: Manage video encoding profiles (add, edit, delete). Profiles are saved in appsettings.json
- **Advanced**: essential operational tuning for analysis, frame-sync, Deep Analysis, timeouts and hardware acceleration. Expert sections expose only the main thresholds; internal algorithm parameters remain in the configuration file.

The WebUI shows two progress bars: global batch progress and current episode progress. The episode bar exposes the main substeps for speed correction, frame-sync, Deep Analysis, conversion, edit application and merge. State is shared across browser tabs connected to the same instance.

![Encoding profiles management](images/encoding.png)

![WebUI configuration](images/config-webui.png)

### CLI

The CLI is designed for scriptable merge/sync batches. Always use `--mode remux`.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -fs
```

#### Required Parameters

| Short | Long | Description |
|-------|------|-------------|
| | --mode | Must be `remux` |
| -s | --source | Folder with source MKV files |
| -t | --target-language | Language code of tracks to import (e.g.: ita). Separate with comma for multiple languages: ita,eng |

#### Source

| Short | Long | Description |
|-------|------|-------------|
| -l | --language | Folder with MKV files to take tracks from. If omitted, uses the source folder |

#### Output (mutually exclusive, one required)

| Short | Long | Description |
|-------|------|-------------|
| -d | --destination | Folder where resulting files will be saved |
| -o | --overwrite | Overwrite source files |

#### Sync

| Short | Long | Description |
|-------|------|-------------|
| -fs | --framesync | Synchronization via visual frame comparison (scene-cut) |
| | --framesync-diagnostics | Writes frame-sync JSON diagnostics to `.remux-forge/framesync-diagnostics` |
| -da | --deep-analysis | Full analysis for files with different edits (mutually exclusive with -fs) |
| | --deep-analysis-diagnostics | Writes Deep Analysis JSON diagnostics to `.remux-forge/deepanalysis-diagnostics` |
| | --speed-correction | Speed correction mode: off, auto, manual. Default: off |
| | --stretch-factor | Fixed factor for manual speed correction, for example 25025/24000 |
| | --no-speed-correction | Compatibility option: disables speed correction |
| -ad | --audio-delay | Manual delay in ms for audio (added to frame-sync/speed if active) |
| -sd | --subtitle-delay | Manual delay in ms for subtitles |
| | --audio-source-fill-threshold-ms | Threshold in ms for filling imported audio with source audio segments |
| | --audio-source-fill-language | Source audio language to use for fill segments |
| | --audio-source-fill-modes | Fill modes: `start`, `end`, `insert-silence`. Requires `--audio-format` and `--audio-scope lang|all` |

Speed correction is disabled by default. In `auto` it is used only when CFR metadata is reliable; with VFR files `manual` and an explicit factor are required.

#### Filters

| Short | Long | Description |
|-------|------|-------------|
| -ac | --audio-codec | Audio codec to import from language file. Separate with comma: DTS,E-AC-3 |
| -so | --sub-only | Import only subtitles, ignore audio |
| -ao | --audio-only | Import only audio, ignore subtitles |
| -ksa | --keep-source-audio | Audio languages to KEEP in the source (others are removed) |
| -ksac | --keep-source-audio-codec | Audio codecs to KEEP in the source. Separate with comma: DTS,TrueHD |
| -kss | --keep-source-subs | Subtitle languages to KEEP in the source |

#### Matching

| Short | Long | Description | Default |
|-------|------|-------------|---------|
| -m | --match-pattern | Regex for episode matching | S(\d+)E(\d+) |
| -r | --recursive | Search in subfolders | active |
| -nr | --no-recursive | Disable recursive search | |
| -ext | --extensions | File extensions to search for. Separate with comma: mkv,mp4,avi | mkv |

#### Audio Post-Processing and Encoding

| Short | Long | Description |
|-------|------|-------------|
| | --audio-format | Processed audio format: flac, lpcm, aac, opus. If set without `--audio-scope`, CLI defaults to `all` |
| | --audio-scope | Audio processing scope: `disabled`, `lang`, `all` |
| | --audio-24-to-16 | Convert 24bit -> 16bit with soxr/shibata (flac/lpcm) |
| | --audio-peak-normalize | Global multichannel peak normalization |
| | --audio-peak-target-db | Peak target in dB |
| | --audio-rename-scope | Final audio rename scope: disabled, lang, all |
| -ep | --encoding-profile | Video encoding profile post-merge (defined in appsettings.json) |

#### Other Options

| Short | Long | Description |
|-------|------|-------------|
| -n | --dry-run | Show what it would do without executing |
| -h | --help | Show built-in help |
| -mkv | --mkvmerge-path | Custom path to mkvmerge (default: searches PATH) |

### Synchronization

Releases of the same content can differ in playback speed, initial offset or internal editing. RemuxForge separates these cases instead of applying one correction to every problem.

**Which method to use:**

| Situation | Method | Option | Notes |
|-----------|--------|--------|-------|
| Same release, only different language | None (direct merge) | | Tracks are already aligned |
| PAL vs NTSC or another global speed difference | Speed Correction | `--speed-correction auto` on reliable CFR, `manual --stretch-factor ...` on VFR | Default off |
| Constant offset | Frame-Sync | `-fs` | Calculates a fixed delay valid for the entire file |
| Scenes cut, added or edited differently | Deep Analysis | `-da` | Generates a cut/insert map for imported tracks |

Frame-Sync and Deep Analysis are mutually exclusive. Speed correction is independent and can be `off`, `auto` or `manual`; on VFR, `auto` fails safely.

### Speed Correction (off/auto/manual)

Compensates for a global speed difference between source and lang, for example a track taken from PAL and moved to an NTSC/BD release. It does not fix missing or added scenes.

The default mode is `off`. In `auto`, detection compares FPS via MediaInfo/mkvmerge and proceeds only when both files have reliable CFR metadata. In `manual`, the factor is supplied explicitly with `--stretch-factor`, for example `25025/24000`, and is the correct mode for VFR sources or ambiguous metadata.

When correction is active, the flow proceeds with:

1. Resolves video timing with MediaInfo as the primary source
2. Blocks `auto` if either file is VFR or metadata is inconsistent
3. Calculates or applies the stretch factor
4. Applies the stretch via mkvmerge to imported tracks, without re-encoding

If either file has variable frame rate (VFR), use `manual`: the average value or container `default_duration` is not enough to decide a reliable stretch automatically.

### Frame-Sync

Calculates a fixed offset to realign tracks when source and lang have the same edit but a different initial delay.

Enabled with **-fs** from CLI or from the checkbox in WebUI configuration.

1. Extracts initial frames from both files (2 minutes from source, 3 from language)
2. Identifies scene cuts in both files
3. For each pair of cuts, calculates what the delay would be if they corresponded to the same moment. The delay that receives the most coherent "votes" is selected as the candidate
4. Verifies the candidate by comparing the visual signature around the cuts: if the frames before and after are similar between the two files, the match is confirmed
5. Confirms at 9 points along the video. At least 5 valid points out of 9 are required

When enabled in configuration, the global audio fingerprint can confirm or reject weak candidates. It does not replace video verification and does not turn Frame-Sync into a cut correction system.

Frame-Sync does not apply cuts or inserts. If drift changes during the episode, the result is rejected and Deep Analysis is required.

### Deep Analysis

Advanced synchronization for files with different editing: scenes added, removed or replaced between source and lang. Unlike Frame-Sync which calculates a fixed offset, Deep Analysis builds a verified timeline map across the whole video and generates cut-and-splice operations on imported tracks.

Enabled with **-da** from CLI or from the checkbox in WebUI configuration. Mutually exclusive with Frame-Sync.

The algorithm operates in 5 phases:

1. **Global stretch**: uses the manual stretch factor when configured; automatic stretch is allowed only with reliable CFR metadata
2. **Timeline anchors**: extracts audio/video anchors across the file and builds a map of local offsets
3. **Operation map**: detects plateaus and transitions where the offset changes, translating them into cuts or inserts
4. **Refinement**: at transition points, recalculates the offset change at the native frame rate resolution of the video
5. **Verification**: global alignment check on distributed points before accepting the map

For each misalignment point, it generates the necessary operations: insertion of silence where the source has extra content, removal of segments where the lang has extra content.

Imported audio tracks are processed through segment extraction, silence generation and concat. Subtitles are rewritten in their native format when supported: SRT, ASS/SSA, PGS/SUP and VobSub IDX/SUB. PGS and VobSub require mkvextract from the same MKVToolNix installation used for mkvmerge.

If Deep Analysis finds only a constant delay, RemuxForge applies that delay through mkvmerge without re-encoding audio. If the map contains cut or insert operations on imported audio tracks, an output audio format is required (`--audio-format` or Audio format in WebUI); without it, the episode fails instead of producing unmodified audio.

Deep Analysis is fail-safe: if a requested track cannot be rewritten or validated, the episode fails instead of importing an unedited track. Audio codecs without a usable ffmpeg encoder for cut-and-splice are not imported through a silent fallback.

### Audio source fill

When imported audio does not cover a portion that exists in the source, RemuxForge can fill that part using an audio track already present in the source, even in another language. The feature is enabled by setting a threshold in milliseconds, a source language and one or more modes:

| Mode | When it applies | Result |
|------|-----------------|--------|
| `start` | The positive initial audio delay exceeds the threshold | Prepends the initial segment from the source to the imported track |
| `end` | The imported track ends before the source by more than the threshold | Appends the final segment from the source |
| `insert-silence` | Deep Analysis generates an `INSERT_SILENCE` above the threshold | Uses the corresponding source segment instead of inserting silence |

From CLI it is configured with `--audio-source-fill-threshold-ms`, `--audio-source-fill-language` and `--audio-source-fill-modes`. It also requires an audio format and an active audio scope (`--audio-format ... --audio-scope lang|all`). In WebUI it appears in the Audio post-processing block only after selecting Audio format. If filling is requested but the selected source track is not available, the episode fails instead of producing an incomplete track.

### Manual delay

The parameters **-ad** (audio delay) and **-sd** (subtitle delay) specify an offset in milliseconds that is **added** to the frame-sync or speed correction result. In the WebUI it is possible to set different delays per episode.

### Audio Post-Processing

Processes selected audio tracks during merge. Enabled from CLI with `--audio-format flac|lpcm|aac|opus` and `--audio-scope lang|all`, or from the "Audio format" and "Audio" fields in WebUI configuration. In CLI, setting only `--audio-format` defaults the scope to `all`.

FLAC and LPCM are lossless; AAC and Opus are lossy. Atmos/DTS:X tracks can be copied, but not processed.

When multiple tracks are processed in the same episode, audio conversions run in parallel with an internal limit on simultaneous operations.

Additional options:

- `--audio-24-to-16`: reduce 24-bit to 16-bit with soxr/shibata, FLAC/LPCM only
- `--audio-peak-normalize`: global multichannel peak normalization
- `--audio-peak-target-db`: peak target in dB, default -1.0
- `--audio-rename-scope disabled|lang|all`: final audio rename scope

**Default bitrates:**

| Format | Setting | Default |
|--------|---------|---------|
| FLAC | Compression level (0-12) | 8 |
| AAC Mono | kbps | 128 |
| AAC Stereo | kbps | 256 |
| AAC 5.1 | kbps | 768 |
| AAC 7.1 | kbps | 1024 |
| Opus Mono | kbps | 128 |
| Opus Stereo | kbps | 256 |
| Opus 5.1 | kbps | 510 |
| Opus 7.1 | kbps | 768 |

Values are configurable in `appsettings.json` or from the **Settings > Audio** menu in WebUI.

### Track Renaming

Audio renaming is controlled by `--audio-rename-scope disabled|lang|all`. Processed tracks receive a descriptive title with codec, channel layout, sample rate and bitrate/bit depth where meaningful.

**Generated name format:**

| Type | Format | Example |
|------|--------|---------|
| Original track | `Codec Layout BitDepth/SampleRate` | `DTS 5.1 24bit/48kHz` |
| Processed FLAC | `FLAC Layout BitDepth/SampleRate` | `FLAC 5.1 24bit/48kHz` |
| Processed LPCM | `LPCM Layout BitDepth/SampleRate` | `LPCM 5.1 24bit/48kHz` |
| Processed AAC | `AAC Layout SampleRate Bitrate` | `AAC 5.1 48kHz 768kbps` |
| Processed Opus | `Opus Layout SampleRate Bitrate` | `Opus 5.1 48kHz 510kbps` |

Channel layout is formatted as 1.0 (mono), 2.0 (stereo), 5.1, 7.1. Missing information is omitted.

### Video Encoding

After the merge it is possible to re-encode the video with a custom encoding profile. Encoding happens in-place on the resulting file via ffmpeg: the video is re-encoded, audio and subtitles are copied without modification.

Enabled with **-ep "profile_name"** from CLI, or from the "Encoding profile" field in WebUI configuration.

Profiles are managed from the **Settings > Encoding profiles** menu in WebUI (add, edit, delete) and are saved in `appsettings.json`.

**Supported codecs:**

| Codec | Preset | CRF range | Rate control | Notes |
|-------|--------|-----------|--------------|-------|
| libx264 | ultrafast...placebo | 0-51 (default 23) | crf, bitrate | Supports 2-pass for bitrate |
| libx265 | ultrafast...placebo | 0-51 (default 28) | crf, bitrate | Supports 2-pass for bitrate |
| libsvtav1 | 0...13 | 0-63 (default 35) | crf, qp, bitrate | Film grain synthesis |

Encoding uses software encoders. GPU acceleration applies only to decoding during analysis/sync phases, not to encoding.

**Example profile:**

```json
{
  "Name": "x265_CRF24",
  "Codec": "libx265",
  "Preset": "medium",
  "Tune": "default",
  "Profile": "main10",
  "BitDepth": "10-bit: yuv420p10le",
  "RateMode": "crf",
  "CrfQp": 24,
  "Bitrate": 0,
  "Passes": 1,
  "FilmGrain": 0,
  "FilmGrainDenoise": false,
  "ExtraParams": ""
}
```

- **Name**: unique name, used to select the profile from CLI (`-ep "x265_CRF24"`)
- **Codec**: `libx264`, `libx265` or `libsvtav1`
- **Preset**: speed/quality. For x264/x265: from `ultrafast` to `placebo`. For svtav1: from `0` (slow) to `13` (fast)
- **Tune**: optimization for content type. For x264: `film`, `animation`, `grain`, etc. For svtav1: `0` (VQ), `1` (PSNR), `2` (SSIM). `default` for no tune
- **Profile**: encoder profile, x264/x265 only (`main`, `main10`, `high`, etc.). `default` for automatic
- **BitDepth**: bit depth and pixel format, e.g. `"10-bit: yuv420p10le"`. The part after `: ` is passed to ffmpeg as `-pix_fmt`
- **RateMode**: `crf` (constant quality), `qp` (svtav1 only), `bitrate` (target kbps)
- **CrfQp**: CRF or QP value depending on the rate mode
- **Bitrate**: target in kbps, used only with `RateMode: "bitrate"`
- **Passes**: `1` or `2`. 2-pass works only with x264/x265 in bitrate mode
- **FilmGrain**: film grain synthesis 0-50, svtav1 only
- **FilmGrainDenoise**: denoise before applying film grain, svtav1 only
- **ExtraParams**: additional ffmpeg parameters in free format, appended to the end of the command

### GPU Acceleration

RemuxForge can use ffmpeg with `-hwaccel auto` to accelerate **video decoding** during analysis phases (speed correction, frame-sync, Deep Analysis). The option is disabled by default and can be enabled in the WebUI under `Advanced Settings > Ffmpeg > Hardware Acceleration`.

| Backend | Platform | GPU |
|---------|----------|-----|
| NVDEC | Linux, Windows | NVIDIA |
| VAAPI | Linux | Intel, AMD |
| VideoToolbox | macOS | Apple Silicon, Intel |

**Video encoding** uses software encoders (libx264, libx265, libsvtav1). Hardware encoders such as NVENC, VAAPI encode or VideoToolbox encode are not supported.

**Docker:** to enable GPU acceleration in the container, see the [Docker with GPU acceleration](#docker-with-gpu-acceleration) section.

### Use Cases

**1. Add Italian dubbing to an English release**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -fs
```

**2. Overwrite source files**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -o -fs
```

**3. Replace a lossy track with a lossless one**

The file already has Italian AC3 lossy. You want to replace it with DTS-HD MA from another release.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie" -l "D:\Serie.ITA.HDMA" -t ita -ac "DTS-HD MA" -ksa eng,jpn -d "D:\Output" -fs
```

With **-ksa eng,jpn** you keep only English and Japanese from the source. With **-ac "DTS-HD MA"** you only take the lossless track from the Italian release.

**4. Multilanguage remux from different releases**

Each step takes the previous output as source.

```bash
RemuxForge.Cli --mode remux -s "D:\Film.US" -l "D:\Film.ITA" -t ita -d "D:\Temp1" -fs
RemuxForge.Cli --mode remux -s "D:\Temp1" -l "D:\Film.FRA" -t fra -d "D:\Temp2" -fs
RemuxForge.Cli --mode remux -s "D:\Temp2" -l "D:\Film.GER" -t ger -d "D:\Output" -fs
```

**5. Anime with non-standard naming**

Many fansubs use "- 05" instead of S01E05. With **-m** you specify a custom regex. With **-so** you take only subtitles.

```bash
RemuxForge.Cli --mode remux -s "D:\Anime.BD" -l "D:\Anime.Fansub" -t ita -m "- (\d+)" -so -d "D:\Output" -fs
```

**6. Daily show with dates in the filename**

```bash
RemuxForge.Cli --mode remux -s "D:\Show.US" -l "D:\Show.ITA" -t ita -m "(\d{4})\.(\d{2})\.(\d{2})" -d "D:\Output"
```

**7. Filter subtitles from the source**

The source has 10 subtitle tracks in useless languages. With **-kss** you keep only the ones you want.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -so -kss eng -d "D:\Output" -fs
```

**8. Anime: keep only Japanese audio and import eng+ita**

The trick **-kss und** discards all subtitles from the source because no track has language "und".

```bash
RemuxForge.Cli --mode remux -s "D:\Anime.BD.JPN" -l "D:\Anime.ITA" -t eng,ita -ksa jpn -kss und -d "D:\Output" -fs
```

**9. Dry run on a complex configuration**

With **-n** verify matching and tracks without executing.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3" -ksa eng -kss eng -d "D:\Output" -fs -n
```

**10. Keep only DTS tracks from the source**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksac DTS -d "D:\Output" -fs
```

**11. Keep only English lossless audio from the source**

By combining **-ksa** and **-ksac**, you keep only tracks matching both criteria.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksa eng -ksac "DTS-HDMA,TrueHD" -d "D:\Output" -fs
```

**12. Import multiple codecs from the language file**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3,DTS" -d "D:\Output" -fs
```

**13. Single source: apply delay and filter tracks**

Without **-l**, the application uses the source folder as language too. Allows remuxing with filters and delays without a separate release.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie" -t ita -ksa jpn,eng -kss eng,jpn -ad 960 -sd 960 -o
```

**14. Process imported tracks to FLAC during merge**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita --audio-format flac --audio-scope lang -d "D:\Output" -fs
```

**15. Process all tracks to Opus keeping only English from source**

TrueHD Atmos and DTS:X tracks are kept intact.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita --audio-format opus --audio-scope all -ksa eng -d "D:\Output" -fs
```

**16. Merge + video encoding with x265 profile**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ep "x265_CRF24" -d "D:\Output" -fs
```

**17. Merge + audio post-processing + video encoding**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita --audio-format flac --audio-scope all -ep "svtav1_CRF30" -ksa eng -d "D:\Output" -fs
```

**18. Deep Analysis for files with different scenes**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -da
```

### Report

At the end of processing a summary report is displayed. In WebUI the detail is visible in the side panel for each episode.

From CLI the report shows 3 tables:

```
========================================
  Report Dettagliato
========================================

SOURCE FILES:
  Episode     Audio               Subtitles           Size
  ----------------------------------------------------------------
  01_05       eng,jpn             eng                 4.2 GB

LANGUAGE FILES:
  Episode     Audio               Subtitles           Size
  ----------------------------------------------------------------
  01_05       ita                 ita                 2.1 GB

RESULT FILES:
  Episode     Audio          Subtitles      Size      Delay       FrmSync   FSConf   Deep      Speed     Merge
  ----------------------------------------------------------------------------------------------------
  01_05       eng,jpn,ita    eng,ita        4.3 GB    +150ms      -         -        3 ops     1250ms    12500ms
```

**Result Files columns:**
- **Delay**: offset applied to imported tracks
- **FrmSync**: frame-sync processing time (if active, otherwise "-")
- **FSConf**: frame-sync confidence percentage (if available, otherwise "-")
- **Deep**: number of cut-and-splice operations generated by Deep Analysis (if active, otherwise "-")
- **Speed**: speed correction processing time (if active, otherwise "-")
- **Merge**: mkvmerge execution time

In dry run mode, Size and Merge show "N/A" because the merge is not executed.

### Audio Codecs

When you specify **-ac** or **-ksac** to filter codecs, the matching is **EXACT**, not partial. Both support multiple comma-separated values.

If a file has both DTS (core) and DTS-HD MA, and you write **-ac "DTS"**, it takes ONLY the DTS core. If you want DTS-HD Master Audio, you must write **-ac "DTS-HDMA"**. If you want both: **-ac "DTS,DTS-HDMA"**.

Codec names are case-insensitive. If a codec is not recognized with direct lookup, a match without hyphens, spaces and colons is attempted.

**Dolby:**

| Codec | Alias | Description |
|-------|-------|-------------|
| AC-3 | AC3, DD | Dolby Digital, the classic lossy 5.1 |
| E-AC-3 | EAC3, DD+, DDP | Dolby Digital Plus, used for lossy Atmos on streaming |
| TrueHD | TRUEHD | Dolby TrueHD, lossless, used for Atmos on Blu-ray |
| MLP | | Meridian Lossless Packing (TrueHD base) |
| ATMOS | | Special alias: matches both TrueHD and E-AC-3 |

**DTS:**

| Codec | Alias | Description |
|-------|-------|-------------|
| DTS | | DTS Core/Digital Surround only (does NOT match DTS-HD) |
| DTS-HD | | Matches both DTS-HD Master Audio and DTS-HD High Resolution |
| DTS-HD MA | DTS-HDMA | DTS-HD Master Audio, lossless |
| DTS-HD HR | DTS-HDHR | DTS-HD High Resolution |
| DTS-ES | | DTS Extended Surround (6.1) |
| DTS:X | DTSX | Object-based, extension of DTS-HD MA |

**Lossless:**

| Codec | Alias | Description |
|-------|-------|-------------|
| FLAC | | Free Lossless Audio Codec |
| PCM | LPCM, WAV | Raw uncompressed audio |
| ALAC | | Apple Lossless |

**Lossy:**

| Codec | Alias | Description |
|-------|-------|-------------|
| AAC | HE-AAC | Advanced Audio Coding |
| MP3 | | MPEG Audio Layer 3 |
| MP2 | | MPEG Audio Layer 2 |
| Opus | OPUS | Opus (WebM) |
| Vorbis | VORBIS | Ogg Vorbis |

### Language Codes

Language codes are ISO 639-2 (3 letters). The most common ones:

| Code | Language |
|------|----------|
| ita | Italian |
| eng | English |
| jpn | Japanese |
| ger / deu | German |
| fra / fre | French |
| spa | Spanish |
| por | Portuguese |
| rus | Russian |
| chi / zho | Chinese |
| kor | Korean |
| und | Undefined (unspecified language) |

If you mistype a code, the application suggests the correct one:

```
Lingua 'italian' non riconosciuta.
Forse intendevi: ita?
```

### Regex Patterns for Episode Matching

The application uses captured groups from the regex to match files. Each group in parentheses is concatenated with "_" to create the unique episode ID.

| Format | Example file | Pattern |
|--------|--------------|---------|
| Standard | Serie.S01E05.mkv | S(\d+)E(\d+) |
| With dot | Serie.S01.E05.mkv | S(\d+)\.E(\d+) |
| Format 1x05 | Serie.1x05.mkv | (\d+)x(\d+) |
| Episode only | Anime - 05.mkv | - (\d+) |
| 3-digit episode | Anime - 005.mkv | - (\d{3}) |
| Daily show | Show.2024.01.15.mkv | (\d{4})\.(\d{2})\.(\d{2}) |

The pattern **S(\d+)E(\d+)** captures two groups (season and episode). For "S01E05" it creates the ID "01_05". Source and language files with the same ID are matched together.


## Split Mode

Split mode cuts HEVC and AVC MKV files frame-perfectly while preserving the original bitstream byte-for-byte whenever possible. If the cut point is not on a keyframe, only the initial GOP of the segment is re-encoded; the rest of the video is copied intact. Audio and subtitles are remuxed without re-encoding.

The CLI always uses `--mode split`. The `--source` parameter can point either to a single MKV file or to a folder:

- **File**: splits the single file.
- **Folder**: runs batch processing on all MKV files found in the folder, applying the same pattern and options to every file. Scanning follows `--recursive`/`--no-recursive` and `--extensions`.

`--source-raw` is available only when `--source` points to a single file.

### Split WebUI

In Split, the WebUI is dedicated to MKV cutting and does not show Remux commands. The screen keeps three areas:

- **Input**: list of MKV files prepared by scan. Source file/folder and cut options are configured with F2.
- **File details**: selected file summary, calculated segments and applied cut settings.
- **Log**: scan and split operation output.

#### Split Shortcut Keys

| Key | Action |
|-----|--------|
| F2 | Open Split configuration |
| F5 | Scan input |
| F10 | Split all prepared files |
| F12 | Request stop for the current operation |

Remux keys for analysis, skip and selected merge are not part of the Split workflow.

#### Split Menu

- **File**: Split configuration (F2)
- **Actions**: Scan input (F5), Split all (F10), Stop (F12)
- **Settings**: Tool paths
- **View**: Pipeline (shows the Split operation sequence based on the current configuration)
- **Theme**: change graphical theme
- **Help**: Info

#### Split Configuration

Split configuration exposes only essential options: source file or folder, output folder, chapter pattern, explicit ranges, split-at, trim start/end, chapters-each, output template, snap, force and dry-run. When the source is a folder, the scan applies the same options to all matched files.

### Split CLI

The Split CLI always uses `--mode split`. Remux parameters such as `--target-language`, `--framesync`, `--deep-analysis`, codec filters and audio post-processing do not apply to this mode.

```bash
RemuxForge.Cli --mode split --source "INPUT.mkv" [cut mode] [options]
```

You must choose exactly **one** cut mode from the list below.

#### Required Split Parameters

| Short | Long | Description |
|-------|------|-------------|
| | --mode | Must be `split` |
| | --source | Source MKV file or folder |

#### Main Split Parameters

| Short | Long | Description |
|-------|------|-------------|
| | --output-dir | Output folder |
| | --source-raw | Alternate PTS source file, single-file only |
| | --output-template | Custom output template |
| | --snap | `off`, `before`, `after`, `nearest` |
| | --force | Overwrite existing output files |
| -n | --dry-run | Show segments without writing files |

### Split Cut Modes

#### `--pattern "5,5,5,6"`

Groups file chapters into segments according to the pattern. The sum of the numbers must match the number of chapters.

```bash
RemuxForge.Cli --mode split --source "bleach_disc1.mkv" --pattern "5,5,5,6"
```

On a folder, the same pattern is applied to every MKV found:

```bash
RemuxForge.Cli --mode split --source "D:\\Discs" --pattern "5,5,5,6" --output-dir "D:\\Output"
```

#### `--ranges "T1-T2,T3-T4,..."`

Defines explicit intervals. `T` can be `HH:MM:SS.mmm`, `MM:SS.mmm`, decimal seconds, `f<number>` for frame index, or `END`.

Examples:

```bash
RemuxForge.Cli --mode split --source "input.mkv" --ranges "00:00:00-00:21:40,00:21:40-00:43:20,00:43:20-END"
RemuxForge.Cli --mode split --source "input.mkv" --ranges "f0-f29970,f29970-END"
```

If a single range is passed, trim mode is used: the output is written next to the input with the `_trimmed` suffix, unless `--output-dir` or a custom template is provided.

#### `--split-at "T1,T2,..."`

Shortcut to split into `N+1` segments at the given timecodes. Duplicate points or points outside file duration produce an error.

```bash
RemuxForge.Cli --mode split --source "concert.mkv" --split-at "00:21:40,00:43:20"
```

Equivalent to:

```bash
RemuxForge.Cli --mode split --source "concert.mkv" --ranges "0-00:21:40,00:21:40-00:43:20,00:43:20-END"
```

#### `--trim-start T` and `--trim-end T`

Trim shortcuts. They can be combined.

```bash
RemuxForge.Cli --mode split --source "input.mkv" --trim-start 00:01:30
RemuxForge.Cli --mode split --source "input.mkv" --trim-end 00:45:00
RemuxForge.Cli --mode split --source "input.mkv" --trim-start 00:01:30 --trim-end 00:45:00
```

#### `--chapters-each`

Creates one segment for each chapter in the source file. Requires chapters to be present.

```bash
RemuxForge.Cli --mode split --source "film.mkv" --chapters-each
```

### Split Output File Naming

The default depends on the mode:

| Mode | Default template |
|------|------------------|
| `--pattern` | `{source_name}.part{n:02d}.mkv` |
| `--ranges` with multiple segments | `{source_name}.part{n:02d}.mkv` |
| `--ranges` / `--split-at` | `{source_name}.part{n:02d}.mkv` |
| `--trim-start` / `--trim-end` | `{source_name}_trimmed.mkv` next to input |
| `--chapters-each` | `{source_name}.ch{n:02d}.mkv` |

It can be overridden with:

- `--output-template "..."` for a custom template. Available variables: `{source_name}`, `{n}`, `{n:02d}`, `{n+213}`, `{n+213:03d}`, `{n-1}`, `{start}`, `{end}`, `{chapter_name}`.

Examples:

```bash
--output-template "{source_name}.part{n:02d}.mkv"
--output-template "Bleach.S12E{n+213:03d}.mkv"
--output-template "{chapter_name}.mkv"
```

### Other Split Options

| Option | Description |
|--------|-------------|
| `--source FILE\|DIR` | Source MKV file or folder. Required in Split mode. |
| `--source-raw FILE` | Uses another file to extract PTS, useful when input is a re-encode of a VFR source. Single-file only. |
| `--output-dir DIR` | Destination folder. Default: input file folder. |
| `--snap off\|before\|after\|nearest` | If enabled, moves the start to the nearest keyframe according to the selected direction, avoiding initial GOP re-encoding. Default `off` frame-perfect. |
| `--force` | Overwrites existing output files. Without `--force`, existing segments are skipped. |
| `--log FILE` | Duplicates stdout to a log file, append mode with timestamp header. |
| `--dry-run` / `-n` | Prints segments and actions without cutting. |
| `--recursive` / `--no-recursive` | Controls subfolder scanning when `--source` is a folder. |
| `--extensions` | File extensions searched in batch mode, default `mkv`. |

### Split Examples

Split a multi-episode MKV by chapter pattern:

```bash
RemuxForge.Cli --mode split --source "disc1.mkv" --pattern "5,5,5,6"
```

Trim the first 90 seconds:

```bash
RemuxForge.Cli --mode split --source "input.mkv" --trim-start 00:01:30
```

Manual split into three equal parts for a 60-minute video:

```bash
RemuxForge.Cli --mode split --source "concert.mkv" --split-at "20:00,40:00"
```

Extract every chapter:

```bash
RemuxForge.Cli --mode split --source "film.mkv" --chapters-each --output-template "Film.ch{n:02d}.mkv"
```

Fast cut aligned to the nearest keyframe, without re-encoding:

```bash
RemuxForge.Cli --mode split --source "source.mkv" --ranges "00:02:00-00:05:00" --snap nearest
```

Batch a folder using the same pattern for every disc/file:

```bash
RemuxForge.Cli --mode split --source "D:\\Discs" --pattern "5,5,5,6" --output-template "Serie.{source_name}.part{n:02d}.mkv" --output-dir "D:\\Episodes"
```

### How Split Works

1. `mkvextract timestamps_v2` extracts source PTS, preserving timecodes including VFR.
2. `mkvextract` produces the raw bitstream of the single video stream.
3. `ffprobe` maps position and size of each frame in the bitstream.
4. For each segment:
   - if the start is on a keyframe, it directly copies the byte range from the raw stream;
   - otherwise, it re-encodes only the initial GOP up to the next keyframe with the same `pix_fmt` and color parameters as the source, reinserts the original parameter sets and concatenates the rest byte-for-byte.
5. `mkvmerge` remuxes video, all audio tracks, all subtitles and chapters into a new MKV with V2 timecodes. In the non-FLAC fast path, chapters are applied after the split with `mkvpropedit`.

Generic chapter names such as `Chapter 15` are renumbered from 1 inside each segment; meaningful names such as `Opening` or `Act 1` are preserved.

The result is a frame-perfect cut without re-encoding the whole file, with only minimal re-encoding of the initial GOP when the cut is not on a keyframe.

### Background: Why a Dedicated Split Pipeline Exists

The original use case was splitting a VFR file into multiple parts while preserving frame-level alignment with the original chapters. On CFR sources the problem is simpler, but VFR breaks many obvious approaches.

**Unreliable PTS after re-encode.** Many re-encode pipelines do not preserve source PTS and generate uniform timestamps that do not reflect real VFR timing, for example soft telecine with 33 ms and 50 ms frames. If cut boundaries are calculated from the re-encoded file PTS, cuts land in the wrong place. The solution is to extract original source PTS and reapply them with `mkvmerge --timestamps`, when frames have a 1:1 correspondence. This is why `--source-raw` exists.

**ffmpeg does not handle VFR MKV stream copy reliably.** With `-c:v copy`, ffmpeg's Matroska muxer may rewrite timestamps as CFR, destroying VFR. Options such as `-fps_mode passthrough`, `-copyts` and `-muxpreload` do not reliably solve this scenario.

**mkvmerge cuts only on keyframes.** `mkvmerge` handles VFR natively, but `--split parts` cuts on video keyframes. In MKV, frames are grouped into clusters and mkvmerge creates new clusters at keyframes to allow seeking. If a chapter falls between two keyframes, mkvmerge includes extra frames up to the next keyframe.

**A video file cannot start from a non-keyframe without re-encoding.** HEVC and H.264 use inter-frame prediction: P and B frames depend on previous frames, so a video file must start from a keyframe. With HEVC open-GOP, keyframes may be CRA instead of IDR; after a CRA there can be RASL frames referencing previous frames, and decoders may drop them if the segment starts in the wrong place.

Split solves this by bypassing ffmpeg and mkvmerge for the actual video cut and working directly on the raw bitstream:

1. extracts raw video track and `timestamps_v2`;
2. maps byte offset and size of every frame;
3. calculates frame ranges from source PTS;
4. copies byte-for-byte when the start is on a keyframe;
5. re-encodes only the few frames from the cut to the next keyframe when required;
6. concatenates at binary level and remuxes with `mkvmerge`, applying VFR source timecodes.

Boundary frames are re-encoded at high quality; everything else remains a byte-for-byte copy. Re-encoding uses `keyint=1` and `bframes=0`, meaning every frame becomes an independent I-frame, allowing cuts at any position and reducing CRA/RASL issues.

Note: with HEVC open-GOP sources, some decoders may still report warnings about missing references around CRA/RASL. The tool verifies frame count and keeps the rest of the video byte-for-byte, but it does not convert an HEVC open-GOP into a closed GOP.

## Configuration (appsettings.json)

All persistent settings are saved in `.remux-forge/appsettings.json`. The file is automatically created with default values. The `.remux-forge` folder is located in the executable's directory, or in the path specified by `REMUXFORGE_DATA_DIR`.

New fields added in subsequent updates are automatically merged without overwriting existing user values.

```json
{
  "Tools": {
    "MkvMergePath": "",
    "MkvExtractPath": "",
    "MkvPropEditPath": "",
    "FfmpegPath": "",
    "FfprobePath": "",
    "MediaInfoPath": "",
    "TempFolder": ""
  },
  "Flac": {
    "CompressionLevel": 8
  },
  "Opus": {
    "Bitrate": {
      "Mono": 128,
      "Stereo": 256,
      "Surround51": 510,
      "Surround71": 768
    }
  },
  "Aac": {
    "Bitrate": {
      "Mono": 128,
      "Stereo": 256,
      "Surround51": 768,
      "Surround71": 1024
    }
  },
  "Ui": {
    "Theme": "nord",
    "LastMode": "remux"
  },
  "EncodingProfiles": [],
  "Advanced": {
    "VideoSync": {
      "FrameWidth": 320,
      "FrameHeight": 240,
      "MseThreshold": 100.0,
      "MseMinThreshold": 0.05,
      "SsimThreshold": 0.55,
      "SsimMaxThreshold": 0.999,
      "NumCheckPoints": 9,
      "MinValidPoints": 5,
      "SceneCutThreshold": 50.0,
      "CutHalfWindow": 5,
      "CutSignatureLength": 10,
      "FingerprintCorrelationThreshold": 0.80,
      "MinSceneCuts": 3,
      "MinCutSpacingFrames": 24,
      "VerifySourceDurationSec": 10,
      "VerifyLangDurationSec": 15,
      "VerifySourceRetrySec": 20,
      "VerifyLangRetrySec": 30
    },
    "SpeedCorrection": {
      "SourceStartSec": 1,
      "SourceDurationSec": 120,
      "LangDurationSec": 180,
      "MinSpeedRatioDiff": 0.001,
      "MaxDurationDiffTelecine": 0.005
    },
    "FrameSync": {
      "MinDurationMs": 10000,
      "SourceStartSec": 1,
      "SourceDurationSec": 120,
      "LangDurationSec": 180,
      "MinValidPoints": 5,
      "GroupingToleranceFrames": 1,
      "MinEdgeCorrelation": 0.70,
      "MinBlockCorrelation": 0.72,
      "MinMotionCorrelation": 0.58,
      "MinBlurredCorrelation": 0.70,
      "MinHashSimilarity": 0.78,
      "MinDescriptorVotes": 2,
      "InitialMinMatchedCuts": 3,
      "InitialMinScore": 0.62,
      "CheckpointMinScore": 0.58,
      "FinalMinConfidence": 0.35,
      "InitialCheckpointDriftPenaltyFrames": 3,
      "InitialCheckpointDriftRejectFrames": 12,
      "InitialMinMargin": 0.05,
      "CheckpointMinMargin": 0.04,
      "StaticSegmentVarianceThreshold": 8.0,
      "BlackFrameRatioThreshold": 0.92,
      "AudioGlobalEnabled": true,
      "AudioGlobalSampleRate": 8000,
      "AudioGlobalWindowMs": 50,
      "AudioGlobalSearchRangeMs": 30000,
      "AudioGlobalCoarseStepMs": 100,
      "AudioGlobalMinScore": 0.62,
      "AudioGlobalMinMargin": 0.04,
      "AudioGlobalMinCoverage": 0.55,
      "AudioGlobalConfirmToleranceFrames": 2,
      "AudioGlobalRejectToleranceFrames": 8
    },
    "DeepAnalysis": {
      "CoarseFps": 2.0,
      "DenseScanFps": 1.0,
      "DenseScanSsimThreshold": 0.5,
      "DenseScanMinDipFrames": 2,
      "LinearScanWindowSec": 3.0,
      "LinearScanConfirmFrames": 5,
      "VerifyDipSsimThreshold": 0.2,
      "ProbeMultiMarginsSec": [5.0, 15.0, 25.0],
      "ProbeMinConsistentPoints": 2,
      "OffsetProbeDurationSec": 3.0,
      "OffsetProbeDeltas": [1000, 2000, 3000, 4000, 5000, -1000, -2000, -3000, -4000, -5000],
      "OffsetProbeMinSsim": 0.7,
      "MinOffsetChangeMs": 500,
      "MinConsecutiveStable": 5,
      "SceneThreshold": 0.3,
      "MatchToleranceMs": 250,
      "WideProbeToleranceSec": 15.0,
      "SceneExtractTimeoutMs": 600000,
      "GlobalVerifyPoints": 30,
      "GlobalVerifyMinRatio": 0.80,
      "VerifyMseMultiplier": 3.0,
      "InitialOffsetRangeSec": 30,
      "InitialOffsetStepSec": 0.5,
      "InitialVotingCuts": 50
    },
    "SubtitleEdit": {
      "FfmpegTimeoutMs": 300000
    },
    "Ffmpeg": {
      "HardwareAcceleration": false
    }
  }
}
```

**Sections:**

- **Tools**: paths to mkvmerge, mkvextract, mkvpropedit, ffmpeg, ffprobe, mediainfo and temporary files folder. Auto-detected at startup, editable from the Settings > Tool paths menu
- **Flac**: FLAC compression level (0 = fast, 12 = maximum compression)
- **Opus.Bitrate**: Opus bitrate in kbps per channel layout (range: 64-768)
- **Aac.Bitrate**: AAC bitrate in kbps per channel layout (range: 32-1536)
- **Ui.Theme**: selected graphical theme. Valid themes: `dark`, `nord`, `dos-blue`, `matrix`, `cyberpunk`, `solarized-dark`, `solarized-light`, `cybergum`, `everforest`
- **Ui.LastMode**: last selected WebUI mode (`remux` or `split`)
- **EncodingProfiles**: array of video encoding profiles (see the Video encoding section)
- **Advanced**: synchronization parameters. The WebUI exposes only operational tuning and essential Expert fields; internal algorithm parameters remain editable from the configuration file. Default values are calibrated for most cases.
- **Advanced.Ffmpeg.HardwareAcceleration**: enables `-hwaccel auto` for ffmpeg video analysis. Default: `false`

## Building from source

Requires .NET 10 SDK.

```bash
# Build CLI
dotnet build RemuxForge.Cli -c Release

# Build WebUI (requires libman for client-side libraries)
cd RemuxForge.Web && libman restore && cd ..
dotnet build RemuxForge.Web -c Release
```

**Publish as standalone executable (single file, compressed):**

```bash
# CLI
dotnet publish RemuxForge.Cli -c Release -r win-x64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r linux-x64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r linux-arm64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r osx-x64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r osx-arm64 --self-contained true

# WebUI
dotnet publish RemuxForge.Web -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true
dotnet publish RemuxForge.Web -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true
dotnet publish RemuxForge.Web -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true
dotnet publish RemuxForge.Web -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true
dotnet publish RemuxForge.Web -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true
```

**Docker:**

```bash
docker build -t remuxforge .
```

## LLM Usage Notice

During RemuxForge development, LLM-based assistance was used for:

- README documentation integration and updates
- WebUI design and refinement support
- assistance with broad refactors when needed

## Buy me a coffee!

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/simonefil)
