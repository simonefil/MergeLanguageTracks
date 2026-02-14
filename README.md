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

**2. Sovrascrivere i file sorgente**

Se non vuoi una cartella di output separata, usa **-o** per sovrascrivere direttamente i file sorgente. Utile quando hai gia' un backup o lavori su copie.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -o -as
```

**3. Sostituire una traccia lossy con una lossless**

Il file ha gia' l'italiano ma e' un AC3 lossy. Hai trovato una release con DTS-HD MA italiano e vuoi sostituirlo.

```bash
MergeLanguageTracks -s "D:\Serie" -l "D:\Serie.ITA.HDMA" -t ita -ac "DTS-HD MA" -ksa eng,jpn -d "D:\Output" -as
```

Con **-ksa eng,jpn** mantieni solo inglese e giapponese dal sorgente, buttando via l'italiano lossy. Con **-ac "DTS-HD MA"** prendi solo la traccia lossless dalla release italiana.

**4. Remux multilingua da release diverse**

Parti dal Blu-ray US (miglior encode) e aggiungi audio da release europee. Ogni passaggio prende come sorgente l'output del precedente.

```bash
MergeLanguageTracks -s "D:\Film.US" -l "D:\Film.ITA" -t ita -d "D:\Temp1" -as
MergeLanguageTracks -s "D:\Temp1" -l "D:\Film.FRA" -t fra -d "D:\Temp2" -as
MergeLanguageTracks -s "D:\Temp2" -l "D:\Film.GER" -t ger -d "D:\Output" -as
```

**5. Anime con naming non standard**

Molti fansub usano il formato "- 05" invece di S01E05. Con **-m** specifichi una regex custom per il matching. Qui prendi solo i sottotitoli perche' il fansub ha i sub migliori ma il video peggiore.

```bash
MergeLanguageTracks -s "D:\Anime.BD" -l "D:\Anime.Fansub" -t ita -m "- (\d+)" -so -d "D:\Output" -as
```

**6. Daily show con date nel nome file**

Per show con naming basato su date (es. Show.2024.03.15.mkv), il pattern cattura anno, mese e giorno come ID episodio.

```bash
MergeLanguageTracks -s "D:\Show.US" -l "D:\Show.ITA" -t ita -m "(\d{4})\.(\d{2})\.(\d{2})" -d "D:\Output"
```

**7. Filtrare sottotitoli dal sorgente**

Il file sorgente ha 10 tracce sub in lingue che non ti servono. Con **-kss** tieni solo quelle che vuoi dal sorgente, mentre con **-t** importi quelle mancanti dalla release lingua.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -so -kss eng -d "D:\Output" -as
```

**8. Anime: tenere solo audio giapponese e importare eng+ita**

Hai un BD giapponese con dual audio (jpn+eng) e molti sottotitoli. Vuoi tenere solo l'audio giapponese, scartare tutti i sub esistenti, e importare audio e sottotitoli inglesi e italiani da una release multilingua. Il trucco **-kss und** scarta tutti i sottotitoli dal sorgente perche' nessuna traccia ha lingua "und".

```bash
MergeLanguageTracks -s "D:\Anime.BD.JPN" -l "D:\Anime.ITA" -t eng,ita -ksa jpn -kss und -d "D:\Output" -as
```

**9. Dry run su configurazione complessa**

Prima di lanciare un merge complesso su una stagione intera, verifica con **-n** che il matching funzioni e le tracce siano quelle giuste.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3" -ksa eng -kss eng -d "D:\Output" -as -at 600 -n
```

**10. Tenere solo tracce DTS dal sorgente**

Il file sorgente ha piu' tracce audio in codec diversi (AC3, DTS, TrueHD). Vuoi tenere solo le tracce DTS indipendentemente dalla lingua.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksac DTS -d "D:\Output" -as
```

**11. Tenere solo audio inglese lossless dal sorgente**

Combinando **-ksa** e **-ksac**, mantieni dal sorgente solo le tracce che soddisfano entrambi i criteri: lingua inglese E codec DTS-HD MA o TrueHD.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksa eng -ksac "DTS-HDMA,TrueHD" -d "D:\Output" -as
```

**12. Importare piu' codec dal file lingua**

Il file lingua ha sia E-AC-3 che DTS italiano. Vuoi importare entrambi.

```bash
MergeLanguageTracks -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3,DTS" -d "D:\Output" -as
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

Quando specifichi **-ac** o **-ksac** per filtrare i codec, il matching e' **ESATTO**, non parziale. Entrambi supportano valori multipli separati da virgola.

**Perche' e' importante:**

Se un file ha sia DTS (core) che DTS-HD MA, e tu scrivi **-ac "DTS"**, prende SOLO il DTS core, non il DTS-HD. Se vuoi il DTS-HD Master Audio, devi scrivere **-ac "DTS-HDMA"**. Se vuoi entrambi, scrivi **-ac "DTS,DTS-HDMA"**.

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

| Short | Long | Descrizione |
|-------|------|-------------|
| -s | --source | Cartella con i file MKV sorgente |
| -l | --language | Cartella con i file MKV da cui prendere le tracce |
| -t | --target-language | Codice lingua delle tracce da importare (es: ita) |

### Output (mutuamente esclusivi, uno obbligatorio)

| Short | Long | Descrizione |
|-------|------|-------------|
| -d | --destination | Cartella dove salvare i file risultanti |
| -o | --overwrite | Sovrascrive i file sorgente (flag, nessun valore) |

### Sync

| Short | Long | Descrizione |
|-------|------|-------------|
| -as | --auto-sync | Calcola automaticamente il delay |
| -ad | --audio-delay | Delay manuale in ms per l'audio (sommato ad auto-sync se attivo) |
| -sd | --subtitle-delay | Delay manuale in ms per i sottotitoli |
| -at | --analysis-time | Durata analisi audio in secondi (default: 300 = 5 min) |

### Filtri

| Short | Long | Descrizione |
|-------|------|-------------|
| -ac | --audio-codec | Codec audio da importare dal file lingua. Separa con virgola: DTS,E-AC-3 |
| -so | --sub-only | Importa solo sottotitoli, ignora l'audio |
| -ao | --audio-only | Importa solo audio, ignora i sottotitoli |
| -ksa | --keep-source-audio | Lingue audio da MANTENERE nel sorgente (le altre vengono rimosse) |
| -ksac | --keep-source-audio-codec | Codec audio da MANTENERE nel sorgente. Separa con virgola: DTS,TrueHD |
| -kss | --keep-source-subs | Lingue sub da MANTENERE nel sorgente |

### Matching

| Short | Long | Descrizione |
|-------|------|-------------|
| -m | --match-pattern | Regex per matching episodi. Default: S([0-9]+)E([0-9]+) |
| -r | --recursive | Cerca nelle sottocartelle (default: true) |
| -ext | --extensions | Estensioni file da cercare (default: mkv). Separa con virgola: mkv,mp4,avi |

### Pattern Regex Comuni

L'applicazione usa i gruppi catturati dalla regex per abbinare i file. Ogni gruppo tra parentesi viene concatenato per creare l'ID univoco dell'episodio.

| Formato | Esempio File | Pattern |
|---------|--------------|---------|
| Standard | Serie.S01E05.mkv | S([0-9]+)E([0-9]+) |
| Con punto | Serie.S01.E05.mkv | S([0-9]+)\.E([0-9]+) |
| Formato 1x05 | Serie.1x05.mkv | ([0-9]+)x([0-9]+) |
| Solo episodio | Anime - 05.mkv | - ([0-9]+) |
| Episodio 3 cifre | Anime - 005.mkv | - ([0-9]{3}) |
| Daily show | Show.2024.01.15.mkv | ([0-9]{4})\.([0-9]{2})\.([0-9]{2}) |

**Come funziona:** Il pattern **S([0-9]+)E([0-9]+)** cattura due gruppi (stagione e episodio). Per "S01E05" crea l'ID "01_05". File sorgente e lingua con lo stesso ID vengono abbinati.

### Altro

| Short | Long | Descrizione |
|-------|------|-------------|
| -n | --dry-run | Mostra cosa farebbe senza eseguire |
| -h | --help | Mostra l'help integrato |
| -mkv | --mkvmerge-path | Percorso custom di mkvmerge |
| -tools | --tools-folder | Cartella per ffmpeg scaricato |

**Note:**

- Tutti i parametri sono case-insensitive
- Supporta sia il formato short (-s) che long (--source)
- Supporta path di rete UNC (\\\\server\\share\\...)
- Il pattern di default S(\d+)E(\d+) matcha nomi tipo "Serie.S01E05.720p.mkv"
