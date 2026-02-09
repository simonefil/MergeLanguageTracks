# MergeLanguageTracks

Applicazione console cross-platform per unire tracce audio e sottotitoli da MKV in lingue diverse.

## A cosa serve?

Consente di combinare tracce audio e sottotitoli da file MKV di release diverse, utile quando si dispone di una versione con video di qualita' superiore ma si desidera integrare l'audio o i sottotitoli da un'altra versione.

L'applicazione elabora automaticamente intere stagioni, abbinando gli episodi corrispondenti e applicando la sincronizzazione automatica per compensare eventuali differenze di montaggio tra le release.

## Casi d'uso tipici

**1. Aggiungere doppiaggio italiano a release inglese**

Hai una release US/UK con video ottimo e vuoi aggiungere l'audio italiano da una release ITA.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -as
```

**2. Aggiungere solo sottotitoli**

La release che hai non ha sub italiani, ma un'altra versione si'.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -so -d "D:\Output" -as
```

**3. Sostituire una traccia lossy con una lossless**

Il file ha gia' l'italiano ma e' un AC3 lossy. Hai trovato una release con DTS-HD MA italiano e vuoi sostituirlo.

```bash
MergeLanguageTracks -s "D:\Serie" -l "D:\Serie.ITA.HDMA" -t ita -ac "DTS-HD MA" -ksa eng,jpn -d "D:\Output" -as
```

Con **-ksa eng,jpn** mantieni solo inglese e giapponese dal sorgente, buttando via l'italiano lossy. Con **-ac "DTS-HD MA"** prendi solo la traccia lossless dalla release italiana.

**4. Prendere solo un codec specifico**

Il file lingua ha sia AC3 che E-AC-3 italiano, tu vuoi solo l'E-AC-3.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3" -d "D:\Output" -as
```

**5. Film multilingua da release diverse**

Parti dal Blu-ray US (miglior encode) e aggiungi audio da release europee per creare un remux multilingua.

```bash
# Aggiungi italiano dalla release ITA
MergeLanguageTracks -s "D:\Film.US" -l "D:\Film.ITA" -t ita -d "D:\Temp1" -as

# Aggiungi francese dalla release FRA
MergeLanguageTracks -s "D:\Temp1" -l "D:\Film.FRA" -t fra -d "D:\Temp2" -as

# Aggiungi tedesco dalla release GER
MergeLanguageTracks -s "D:\Temp2" -l "D:\Film.GER" -t ger -d "D:\Output" -as
```

**6. Solo audio senza sottotitoli**

Vuoi importare solo le tracce audio, ignorando i sottotitoli.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ao -d "D:\Output" -as
```

**7. Dry run per vedere cosa farebbe**

Prima di lanciare su 50 episodi, controlla che faccia quello che vuoi.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -as -DryRun
```

**8. Elaborare file MP4 e AVI oltre a MKV**

L'output e' sempre MKV, ma i file sorgente possono avere estensioni diverse.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ext mkv,mp4,avi -d "D:\Output" -as
```

## Come funziona AutoSync

Spesso le release in lingue diverse hanno tagli differenti: intro piu' lunghe, scene tagliate, crediti diversi. Se fai un merge diretto, l'audio va fuori sync.

AutoSync risolve questo problema analizzando l'audio dei due file e calcolando automaticamente il delay necessario.

**Il principio e' semplice:**

Anche se il doppiaggio e' in lingue diverse, la colonna sonora di sottofondo (musica, effetti, esplosioni, silenzi) e' identica. L'applicazione confronta questi "marker" audio per trovare l'offset corretto.

**Come funziona tecnicamente:**

1. Estrae i primi 5 minuti di audio da entrambi i file (configurabile con **-at**)
2. Analizza l'audio cercando:
   - Inizi e fine dei silenzi (es. pause tra scene)
   - Picchi di volume improvvisi (esplosioni, colpi, musica che parte)
3. Confronta i pattern tra sorgente e lingua
4. Cerca l'offset che fa combaciare piu' marker possibili
5. Usa 3 fasi di ricerca per precisione al millisecondo:
   - Fase 1: ricerca grossolana (-60s a +60s, step 500ms)
   - Fase 2: ricerca fine (+/-2s dal risultato, step 10ms)
   - Fase 3: ricerca ultra-fine (+/-100ms, step 1ms)

**Funziona anche per i sottotitoli!**

Se importi solo sub (**-so**), l'applicazione usa comunque l'audio per calcolare il sync. Prende una traccia audio qualsiasi dal file sorgente e una dal file lingua, le confronta, e applica il delay calcolato ai sottotitoli.

Non importa che lingua sia l'audio usato per il confronto: la musica e gli effetti sono sempre gli stessi.

**Quando NON funziona bene:**

- File con audio completamente diverso (es. versione cinematografica vs director's cut con scene rifatte)
- File molto corti (< 2-3 minuti) dove non ci sono abbastanza marker
- Audio con pochissimi silenzi e variazioni (raro, ma succede)
- Episodi con scene tagliate (non all'inizio)

In questi casi vedrai un warning "Low confidence" e conviene verificare manualmente o usare delay manuale.

## Report Dettagliato

A fine elaborazione viene mostrato un report con 3 tabelle:

```
========================================
  Report Dettagliato
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

**Colonne Result Files:**
- **Delay**: offset applicato alle tracce importate
- **FFmpeg**: tempo di estrazione/analisi audio (I/O bound)
- **AutoSync**: tempo di calcolo offset (CPU bound)
- **Merge**: tempo di esecuzione mkvmerge

In modalita' dry run, Size e Merge mostrano "N/A" perche' il merge non viene eseguito.

## Codec Audio

Quando specifichi **-ac** per filtrare i codec, il matching e' **ESATTO**, non parziale.

**Perche' e' importante:**

Se un file ha sia DTS (core) che DTS-HD MA, e tu scrivi **-ac "DTS"**, prende SOLO il DTS core, non il DTS-HD. Se vuoi il DTS-HD Master Audio, devi scrivere **-ac "DTS-HDMA"**.

**Dolby:**

- **AC-3** (alias: AC3, DD) - Dolby Digital, il classico 5.1 lossy
- **E-AC-3** (alias: EAC3, DD+, DDP) - Dolby Digital Plus, usato per Atmos lossy su streaming
- **TrueHD** - Dolby TrueHD, lossless, usato per Atmos su Blu-ray
- **MLP** - Il container interno di TrueHD (raramente serve specificarlo)

**DTS:**

- **DTS** - Solo DTS Core/Digital Surround (il 5.1 lossy base)
- **DTS-HD MA** (alias: DTS-HDMA) - DTS-HD Master Audio, lossless
- **DTS-HD HR** (alias: DTS-HDHR) - DTS-HD High Resolution, lossy ma migliore del core
- **DTS-ES** - DTS Extended Surround (6.1)
- **DTS:X** (alias: DTSX) - Object-based, estensione di DTS-HD MA

**Lossless:**

- **FLAC** - Il classico lossless open source
- **PCM** (alias: LPCM, WAV) - Audio raw non compresso
- **ALAC** - Apple Lossless (raro nei remux)

**Lossy:**

- **AAC** - Comune su streaming e webrip
- **MP3** - Ormai raro
- **Opus** - Usato in WebM, ottima qualita' a bitrate basso
- **Vorbis** - Ogg Vorbis

## Codici Lingua

I codici lingua sono ISO 639-2 (3 lettere). I piu' comuni:

- **ita** - Italiano
- **eng** - Inglese
- **jpn** - Giapponese
- **ger** o **deu** - Tedesco
- **fra** o **fre** - Francese
- **spa** - Spagnolo
- **por** - Portoghese
- **rus** - Russo
- **chi** o **zho** - Cinese
- **kor** - Coreano
- **und** - Undefined (lingua non specificata)

Se sbagli un codice, l'applicazione ti suggerisce quello corretto:

```
Errore: lingua 'italian' non riconosciuta.
Forse intendevi: ita?
```

## Requisiti

- [MKVToolNix](https://mkvtoolnix.download/) installato (mkvmerge deve essere nel PATH)
- ffmpeg per AutoSync - se non lo hai, viene scaricato automaticamente nella cartella **tools/**

**Piattaforme supportate:**

- Windows (x64)
- Linux (x64)
- macOS (x64, ARM64)

## Build

Richiede .NET 8.0 SDK.

```bash
# Build per la piattaforma corrente
dotnet build -c Release

# Publish come eseguibile standalone
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
```

## Riferimento Parametri

### Obbligatori

| Parametro | Alias | Descrizione |
|-----------|-------|-------------|
| -SourceFolder | -s | Cartella con i file MKV sorgente |
| -LanguageFolder | -l | Cartella con i file MKV da cui prendere le tracce |
| -TargetLanguage | -t | Codice lingua delle tracce da importare (es: ita) |

### Output

| Parametro | Alias | Descrizione |
|-----------|-------|-------------|
| -DestinationFolder | -d | Cartella dove salvare i file risultanti |
| -OutputMode | -o | "Destination" (default) o "Overwrite" per sovrascrivere i sorgenti |

### Sync

| Parametro | Alias | Descrizione |
|-----------|-------|-------------|
| -AutoSync | -as | Calcola automaticamente il delay |
| -AudioDelay | -ad | Delay manuale in ms per l'audio (sommato ad AutoSync se attivo) |
| -SubtitleDelay | -sd | Delay manuale in ms per i sottotitoli |
| -AnalysisTime | -at | Durata analisi audio in secondi (default: 300 = 5 min) |

### Filtri

| Parametro | Alias | Descrizione |
|-----------|-------|-------------|
| -AudioCodec | -ac | Importa solo tracce audio con questo codec |
| -SubOnly | -so | Importa solo sottotitoli, ignora l'audio |
| -AudioOnly | -ao | Importa solo audio, ignora i sottotitoli |
| -KeepSourceAudioLangs | -ksa | Lingue audio da MANTENERE nel sorgente (le altre vengono rimosse) |
| -KeepSourceSubtitleLangs | -kss | Lingue sub da MANTENERE nel sorgente |

### Matching

| Parametro | Alias | Descrizione |
|-----------|-------|-------------|
| -MatchPattern | -m | Regex per matching episodi. Default: S([0-9]+)E([0-9]+) |
| -Recursive | -r | Cerca nelle sottocartelle (default: true) |
| -FileExtensions | -ext | Estensioni file da cercare (default: mkv). Separa con virgola: mkv,mp4,avi |

### Pattern Regex Comuni

L'applicazione usa i gruppi catturati dalla regex per abbinare i file. Ogni gruppo tra parentesi viene concatenato per creare l'ID univoco dell'episodio.

| Formato | Esempio File | Pattern |
|---------|--------------|---------|
| Standard | Serie.S01E05.mkv | S([0-9]+)E([0-9]+) |
| Case insensitive | serie.s01e05.mkv | [Ss]([0-9]+)[Ee]([0-9]+) |
| Con punto | Serie.S01.E05.mkv | S([0-9]+)\.E([0-9]+) |
| Formato 1x05 | Serie.1x05.mkv | ([0-9]+)x([0-9]+) |
| Solo episodio | Anime - 05.mkv | - ([0-9]+) |
| Episodio 3 cifre | Anime - 005.mkv | - ([0-9]{3}) |
| Daily show | Show.2024.01.15.mkv | ([0-9]{4})\.([0-9]{2})\.([0-9]{2}) |

**Come funziona:** Il pattern **S([0-9]+)E([0-9]+)** cattura due gruppi (stagione e episodio). Per "S01E05" crea l'ID "01_05". File sorgente e lingua con lo stesso ID vengono abbinati.

### Altro

| Parametro | Alias | Descrizione |
|-----------|-------|-------------|
| -DryRun | -dry, -n | Mostra cosa farebbe senza eseguire |
| -Help | -h | Mostra l'help integrato |
| -MkvMergePath | -mkv | Percorso custom di mkvmerge |
| -ToolsFolder | -tools | Cartella per ffmpeg scaricato |

**Note:**

- Tutti i parametri sono case-insensitive
- Supporta path di rete UNC (\\\\server\\share\\...)
- Il pattern di default S(\d+)E(\d+) matcha nomi tipo "Serie.S01E05.720p.mkv"
