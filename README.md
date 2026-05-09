# ![icon](icons/icon-48.png) RemuxForge

Applicazione cross-platform per unire tracce audio e sottotitoli da file MKV in lingue diverse, con sincronizzazione automatica tra release con montaggio o velocita' differenti.

Disponibile in due modalita': CLI (riga di comando) e WebUI (interfaccia web).

## Funzionalita' principali

- Merge automatico di intere stagioni con matching episodi per nome file
- Sincronizzazione automatica: correzione velocita' PAL/NTSC, frame-sync per offset costante, deep analysis per edit diversi
- Filtro per lingua, codec audio, sottotitoli, sia in importazione che in mantenimento dal sorgente
- Conversione audio lossless a FLAC o Opus durante il merge
- Encoding video post-merge con profili personalizzabili (x264, x265, SVT-AV1)
- Accelerazione GPU opzionale per decodifica video nelle fasi di analisi
- Due interfacce: CLI scriptabile e WebUI per browser e server headless
- Deploy via Docker con supporto GPU opzionale
- Temi grafici scuro/chiaro per WebUI
- Configurazione persistente in `appsettings.json` con auto-merge dei nuovi campi

## Sincronizzazione

RemuxForge usa tre percorsi distinti:

- **Speed correction** corregge differenze di velocita' globali. Le modalita' sono `off`, `auto` e `manual`. `auto` viene bloccata se MediaInfo/ffprobe rileva VFR; in questi casi usare `manual` con stretch factor esplicito, per esempio `25025/24000`.
- **Frame-sync** cerca un offset costante tra source e lingua. E' pensato per essere veloce e non corregge tagli o aggiunte locali.
- **Deep analysis** e' il percorso pesante per sorgenti con edit diversi, tagli, aggiunte o drift locale. Usa una mappa timeline verificata e applica operazioni di taglia-cuci alle tracce importate. Frame-sync e deep analysis sono mutuamente esclusivi.

Se una release ha sia speed mismatch sia edit locali, lo stretch globale va impostato manualmente e la correzione degli edit va lasciata a Deep analysis. Speed correction deve andare in fail-safe sui casi con edit locali invece di forzare un sync ambiguo.

## Requisiti

- [MKVToolNix](https://mkvtoolnix.download/): mkvmerge deve essere nel PATH o configurato manualmente. mkvextract e' richiesto per riscrivere sottotitoli bitmap PGS/VobSub in Deep Analysis.
- [ffmpeg](https://ffmpeg.org/): necessario per sync, conversione audio e encoding video. Se mancante, puo' essere scaricato automaticamente dal menu Impostazioni > Percorsi tool (bottone "Scarica")
- [mediainfo](https://mediainfo.sourceforge.net/): per il report dettagliato delle tracce (opzionale)
- Locale UTF-8 su Linux (necessario per nomi file con caratteri non-ASCII)

**Piattaforme:**

| Piattaforma | Architetture |
|-------------|-------------|
| Windows | x64 |
| Linux | x64, ARM64 |
| macOS | x64, ARM64 |
| Docker | x64 (immagine con mkvtoolnix, ffmpeg e mediainfo preinstallati) |

## Installazione e avvio

### Desktop: CLI

Scaricare l'archivio per la propria piattaforma dalla [pagina release](https://github.com/draknodd/RemuxForge/releases), estrarre ed eseguire.

- **Windows**: aprire un terminale e lanciare `RemuxForge.exe` con parametri
- **Linux/macOS**: `chmod +x RemuxForge && ./RemuxForge` con parametri

Lanciando senza parametri viene mostrato l'help CLI. Con parametri si esegue in modalita' CLI.

### Desktop: WebUI

Scaricare l'archivio WebUI dalla [pagina release](https://github.com/draknodd/RemuxForge/releases), estrarre ed eseguire.

- **Windows**: doppio click su `RemuxForge.Web.exe`
- **Linux/macOS**: `chmod +x RemuxForge.Web && ./RemuxForge.Web`

Aprire `http://localhost:5000` nel browser. La porta e' configurabile con la variabile d'ambiente `REMUXFORGE_PORT`.

### Docker

> **Nota:** gli esempi seguenti sono da adattare alla propria configurazione. I percorsi dei volumi, la porta, e soprattutto il mapping utente (`user`) devono corrispondere a un utente con permessi di lettura/scrittura sulle cartelle montate. Se l'utente specificato non ha accesso allo storage, il container non funzionera'.

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
    user: "1000:1000"  # adattare al proprio utente (id -u / id -g)
    ports:
      - "5000:5000"
    environment:
      - REMUXFORGE_PORT=5000
      - REMUXFORGE_DATA_DIR=/data
    volumes:
      - /path/to/config:/data:rw        # cartella configurazione
      - /path/to/media:/media:rw         # cartella file video
```

### Docker con accelerazione GPU

La decodifica video durante le fasi di analisi (speed correction, frame-sync, deep analysis) puo' essere accelerata via GPU se l'opzione avanzata `Hardware Acceleration` e' abilitata. Quando attiva, ffmpeg usa `-hwaccel auto` e seleziona automaticamente il backend disponibile nel container.

**NVIDIA (NVDEC):**

Richiede il [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html) installato sull'host.

```bash
# Installare nvidia-container-toolkit sull'host
sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker

# Avviare il container con accesso alla GPU
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

### Variabili d'ambiente

| Variabile | Descrizione | Default |
|-----------|-------------|---------|
| REMUXFORGE_PORT | Porta HTTP della WebUI | 5000 |
| REMUXFORGE_DATA_DIR | Directory per configurazione e dati (.remux-forge) | Directory dell'eseguibile |
| REMUXFORGE_LOG_FILE | Percorso file log. Se impostato, abilita il logging su file | Non attivo |

![Interfaccia principale (tema Nord)](images/nord.png)

### Tasti rapidi

| Tasto | Azione |
|-------|--------|
| F1 | Guida |
| F2 | Apre configurazione |
| F5 | Scan cartelle e matching episodi |
| F6 | Analizza episodio selezionato |
| F7 | Analizza tutti gli episodi pendenti |
| F8 | Skip/Unskip episodio selezionato |
| F9 | Merge episodio selezionato |
| F10 | Merge tutti gli episodi analizzati |
| F12 | Richiede stop dell'operazione corrente |
| Enter | Menu contestuale episodio |
| Ctrl+Q | Esci |

### Menu contestuale

Cliccando con il tasto destro su un episodio (o premendo Enter) si apre un menu contestuale con le seguenti voci:

- **Delay**: modifica il delay manuale per l'episodio selezionato
- **MediaInfo sorgente**: mostra il report MediaInfo completo del file sorgente
- **MediaInfo lingua**: mostra il report MediaInfo del file lingua
- **MediaInfo risultato**: mostra il report MediaInfo del file risultante (disponibile dopo il merge)

Le voci MediaInfo sono visibili solo se il tool mediainfo e' configurato e il file corrispondente esiste. Il report mostra tutte le informazioni sulle tracce (codec, canali, bitrate, risoluzione, lingua, ecc.) e puo' essere copiato negli appunti.

### Menu

- **File**: Configurazione (F2), Esci (Ctrl+Q)
- **Azioni**: Scan file (F5), Analizza selezionato (F6), Analizza tutti (F7), Skip/Unskip (F8), Processa selezionato (F9), Processa tutti (F10)
- **Impostazioni**: Percorsi tool, Conversione audio, Profili encoding, Avanzate
- **Vista**: Pipeline (mostra la sequenza di operazioni che verranno eseguite in base alla configurazione corrente: sync, conversione, merge, encoding)
- **Tema**: cambio tema grafico (8 temi)
- **Aiuto**: Guida (F1), Info

### Configurazione (F2)

Il dialog di configurazione raggruppa tutte le opzioni di elaborazione:

![Dialog configurazione](images/config.png)

- **Cartelle**: Source, Lingua, Destinazione, con pulsante browse per ciascuna. Checkbox per sovrascrivere sorgente e ricerca ricorsiva
- **Lingua e Tracce**: Lingua target, Codec audio, Keep source audio/codec/sub, Solo sottotitoli, Solo audio, Rinomina tracce
- **Sincronizzazione**: Speed correction (`off`/`auto`/`manual` con stretch fisso), Frame-sync, Deep analysis, Delay audio (ms), Delay sub (ms). Frame-sync e Deep analysis sono esclusivi.
- **Avanzate**: Pattern match (regex), Estensioni file, Converti audio (flac/opus), Profilo encoding

### Menu Impostazioni

- **Percorsi tool**: Percorsi di mkvmerge, ffmpeg, mediainfo e cartella file temporanei. I tool vengono cercati automaticamente all'avvio. ffmpeg puo' essere scaricato direttamente dall'interfaccia
- **Conversione audio**: Livello compressione FLAC e bitrate Opus per layout canali (mono, stereo, 5.1, 7.1)
- **Profili encoding**: Gestione profili di encoding video (aggiungi, modifica, elimina). I profili sono salvati in appsettings.json
- **Avanzate**: tuning operativo essenziale per analisi, frame-sync, deep analysis, timeout e accelerazione hardware. Le sezioni Expert espongono solo le soglie principali; i parametri algoritmici interni restano nel file di configurazione.

La WebUI mostra una doppia progress bar: avanzamento globale del batch e avanzamento dell'episodio corrente. La barra episodio espone i substep principali di speed correction, frame-sync, deep analysis, conversione, taglia-cuci e merge. Lo stato e' condiviso tra browser/tab collegati alla stessa istanza.

![Gestione profili encoding](images/encoding.png)

### Temi

Disponibili 8 temi selezionabili dal menu Tema:

| Nord (default) | DOS Blue |
|:-:|:-:|
| ![Nord](images/nord.png) | ![DOS Blue](images/dos.png) |

| Matrix | Cyberpunk |
|:-:|:-:|
| ![Matrix](images/matrix.png) | ![Cyberpunk](images/cyberpunk.png) |

| Solarized Dark | Solarized Light |
|:-:|:-:|
| ![Solarized Dark](images/solarized-dark.png) | ![Solarized Light](images/solarized-light.png) |

| Cybergum | Everforest |
|:-:|:-:|
| ![Cybergum](images/cybergum.png) | ![Everforest](images/everforest.png) |

## Interfaccia WebUI

Interfaccia web accessibile da browser, ideale per server headless, NAS o deploy Docker. Offre le funzionalita' complete: configurazione, scan, analisi, merge, impostazioni tool, conversione audio, profili encoding, impostazioni avanzate, pipeline view, temi e guida.

![Configurazione WebUI](images/config-webui.png)

## Interfaccia CLI

Per elaborazione da riga di comando, scriptabile e automatizzabile.

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -fs
```

### Parametri obbligatori

| Short | Long | Descrizione |
|-------|------|-------------|
| -s | --source | Cartella con i file MKV sorgente |
| -t | --target-language | Codice lingua delle tracce da importare (es: ita). Separare con virgola per piu' lingue: ita,eng |

### Sorgente

| Short | Long | Descrizione |
|-------|------|-------------|
| -l | --language | Cartella con i file MKV da cui prendere le tracce. Se omesso, usa la cartella sorgente |

### Output (mutuamente esclusivi, uno obbligatorio)

| Short | Long | Descrizione |
|-------|------|-------------|
| -d | --destination | Cartella dove salvare i file risultanti |
| -o | --overwrite | Sovrascrive i file sorgente |

### Sync

| Short | Long | Descrizione |
|-------|------|-------------|
| -fs | --framesync | Sincronizzazione tramite confronto visivo frame (scene-cut) |
| -da | --deep-analysis | Analisi completa per file con edit diversi (mutuamente esclusiva con -fs) |
| | --speed-correction | Modalita' correzione velocita': off, auto, manual. Default: off |
| | --stretch-factor | Fattore fisso per speed-correction manual, esempio 25025/24000 |
| | --no-speed-correction | Compatibilita': disattiva la correzione velocita' |
| -ad | --audio-delay | Delay manuale in ms per l'audio (sommato a frame-sync/speed se attivi) |
| -sd | --subtitle-delay | Delay manuale in ms per i sottotitoli |

La correzione velocita' e' disattivata di default. In `auto` viene usata solo quando i metadati CFR sono affidabili; con file VFR e' necessario impostare `manual` e un fattore esplicito.

### Filtri

| Short | Long | Descrizione |
|-------|------|-------------|
| -ac | --audio-codec | Codec audio da importare dal file lingua. Separa con virgola: DTS,E-AC-3 |
| -so | --sub-only | Importa solo sottotitoli, ignora l'audio |
| -ao | --audio-only | Importa solo audio, ignora i sottotitoli |
| -ksa | --keep-source-audio | Lingue audio da MANTENERE nel sorgente (le altre vengono rimosse) |
| -ksac | --keep-source-audio-codec | Codec audio da MANTENERE nel sorgente. Separa con virgola: DTS,TrueHD |
| -kss | --keep-source-subs | Lingue sub da MANTENERE nel sorgente |
| -rt | --rename-tracks | Rinomina tutte le tracce audio nel file risultante (vedi sezione Rinomina tracce) |

### Matching

| Short | Long | Descrizione | Default |
|-------|------|-------------|---------|
| -m | --match-pattern | Regex per matching episodi | S(\d+)E(\d+) |
| -r | --recursive | Cerca nelle sottocartelle | attivo |
| -nr | --no-recursive | Disabilita la ricerca ricorsiva | |
| -ext | --extensions | Estensioni file da cercare. Separa con virgola: mkv,mp4,avi | mkv |

### Conversione e encoding

| Short | Long | Descrizione |
|-------|------|-------------|
| -cf | --convert-format | Converte tracce lossless: flac o opus. TrueHD Atmos e DTS:X esclusi |
| -ep | --encoding-profile | Profilo encoding video post-merge (definito in appsettings.json) |

### Altro

| Short | Long | Descrizione |
|-------|------|-------------|
| -n | --dry-run | Mostra cosa farebbe senza eseguire |
| -h | --help | Mostra l'help integrato |
| -mkv | --mkvmerge-path | Percorso custom di mkvmerge (default: cerca nel PATH) |

## Sincronizzazione

Le release dello stesso contenuto possono differire per velocita' di riproduzione, taglio iniziale o montaggio interno. RemuxForge offre tre sistemi di sincronizzazione, tutti basati sull'analisi visiva dei frame video tramite ffmpeg. La decodifica GPU e' opzionale e disattivata di default.

**Quale metodo usare:**

| Situazione | Metodo | Opzione | Note |
|------------|--------|---------|------|
| Stessa release, solo lingua diversa | Nessuno (merge diretto) | | Le tracce sono gia' allineate |
| PAL vs NTSC (25 vs 23.976 fps) | Correzione velocita' | `--speed-correction auto` su CFR, `manual --stretch-factor ...` su VFR | Default off; auto viene bloccata su VFR |
| Offset costante (intro diversa, nero iniziale) | Frame-Sync | -fs | Calcola un delay fisso valido per tutto il file |
| Scene tagliate o aggiunte nel mezzo del video | Deep Analysis | -da | Genera operazioni di taglia-cuci sulle tracce |

Frame-Sync e Deep Analysis sono mutuamente esclusivi. La correzione velocita' e' indipendente e puo' essere `off`, `auto` o `manual`; su VFR la modalita' `auto` fallisce in modo controllato.

### Correzione velocita' (off/auto/manual)

Compensa la differenza tra release PAL (25 fps) e NTSC (23.976 fps), comune con serie TV e film europei. In queste situazioni l'audio di una versione e' leggermente piu' veloce dell'altra, e un merge diretto produrrebbe un desync crescente nel tempo.

La modalita' di default e' `off`. In `auto` il rilevamento confronta gli FPS dei due file tramite MediaInfo/mkvmerge e procede solo quando entrambi i file sono CFR affidabili. In `manual` il fattore viene indicato esplicitamente con `--stretch-factor`, ad esempio `25025/24000`, ed e' la modalita' corretta per sorgenti VFR o metadati ambigui.

Quando la correzione e' attiva, il flusso procede con:

1. Estrae i frame video iniziali da entrambi i file e li converte in immagini in scala di grigi a bassa risoluzione
2. Individua i tagli scena (cambi di inquadratura) in entrambi i file, che sono identici in entrambe le versioni indipendentemente dalla lingua
3. Abbina i tagli tra sorgente e lingua per calcolare il delay iniziale, compensando il drift dovuto alla differenza di velocita'
4. Verifica il risultato in 9 punti distribuiti lungo il video (10%, 20%, ... 90%). Per ogni punto estrae un segmento breve, trova i tagli scena locali e conferma che il delay calcolato sia corretto. Servono almeno 5 punti validi su 9
5. Applica il fattore di correzione tramite mkvmerge (time-stretching) alle tracce audio e sottotitoli importate, senza ricodifica

Se uno dei file ha frame rate variabile (VFR), la modalita' `auto` viene bloccata perche' il `default_duration` del container non e' affidabile. In questi casi va usato `manual` con un fattore deciso dall'utente.

### Frame-Sync

Calcola un offset fisso per riallineare tracce quando sorgente e lingua hanno lo stesso FPS ma un taglio iniziale diverso (intro piu' lunga, secondi di nero, crediti diversi all'inizio).

Attivabile con **-fs** da CLI o dal checkbox nella configurazione WebUI.

1. Estrae i frame iniziali da entrambi i file (2 minuti dal sorgente, 3 dalla lingua)
2. Individua i tagli scena in entrambi i file
3. Per ogni coppia di tagli, calcola quale sarebbe il delay se corrispondessero allo stesso momento. Il delay che riceve piu' "voti" coerenti viene selezionato come candidato
4. Verifica il candidato confrontando la firma visiva attorno ai tagli: se i frame prima e dopo sono simili tra i due file, il match e' confermato
5. Conferma in 9 punti lungo il video, come la correzione velocita'. Servono almeno 5 punti validi su 9

Frame-Sync non funziona se le differenze sono a meta' episodio (scene tagliate o aggiunte nel mezzo). In quel caso serve la Deep Analysis.

### Deep Analysis

Sincronizzazione avanzata per file con montaggio diverso: scene aggiunte, rimosse o sostituite tra source e lang. A differenza del Frame-Sync che calcola un offset fisso, la Deep Analysis costruisce una mappa timeline verificata sull'intero video e genera operazioni di taglia-cuci sulle tracce importate.

Attivabile con **-da** da CLI o dal checkbox nella configurazione WebUI. Mutuamente esclusiva con Frame-Sync.

L'algoritmo opera in 5 fasi:

1. **Stretch globale**: usa lo stretch manuale se configurato; lo stretch automatico viene consentito solo con metadati CFR affidabili
2. **Ancore timeline**: estrae ancore audio/video lungo il file e costruisce una mappa degli offset locali
3. **Mappa operazioni**: individua plateau e transizioni dove l'offset cambia, traducendoli in cut o insert
4. **Raffinamento**: nei punti di transizione, ricalcola il cambio offset alla risoluzione del frame rate nativo del video
5. **Verifica**: controllo globale dell'allineamento su punti distribuiti nel video prima di accettare la mappa

Per ogni punto di disallineamento, genera le operazioni necessarie: inserimento di silenzi dove il sorgente ha contenuto extra, taglio di segmenti dove il lang ha contenuto extra.

Le tracce audio importate vengono processate tramite estrazione segmenti, generazione silenzi e concat. I sottotitoli vengono riscritti nel formato nativo quando supportato: SRT, ASS/SSA, PGS/SUP e VobSub IDX/SUB. PGS e VobSub richiedono mkvextract disponibile nella stessa installazione MKVToolNix di mkvmerge.

Deep Analysis e' fail-safe: se una traccia richiesta non puo' essere riscritta o validata, l'episodio fallisce invece di importare una traccia non editata. I codec audio senza encoder ffmpeg utilizzabile per taglia-cuci non vengono importati con fallback silenzioso.

### Delay manuale

I parametri **-ad** (audio delay) e **-sd** (subtitle delay) specificano un offset in millisecondi che viene **sommato** al risultato di frame-sync o speed correction. Nella WebUI e' possibile impostare delay diversi per singolo episodio.

## Conversione audio

Converte le tracce audio lossless in FLAC o Opus durante il merge. Attivabile con **-cf flac** o **-cf opus** da CLI, oppure dal campo "Converti audio" nella configurazione WebUI.

**Codec convertibili:** DTS-HD Master Audio, DTS-HD High Resolution, TrueHD, PCM, ALAC, MLP, FLAC.

**Esclusi:** TrueHD Atmos e DTS:X perche' contengono metadati spaziali che verrebbero persi.

La conversione si applica sia alle tracce sorgente mantenute tramite **-ksa**/**-ksac** sia alle tracce importate dal file lingua (solo se lossless). Se il formato target e' FLAC e la traccia e' gia' FLAC, la conversione viene saltata.

La conversione richiesta e' fail-safe: se una traccia che deve essere convertita fallisce, l'episodio va in errore invece di usare automaticamente la traccia originale.

**Bitrate di default:**

| Formato | Impostazione | Default |
|---------|-------------|---------|
| FLAC | Livello compressione (0-12) | 8 |
| Opus Mono | kbps | 128 |
| Opus Stereo | kbps | 256 |
| Opus 5.1 | kbps | 510 |
| Opus 7.1 | kbps | 768 |

I valori sono configurabili in `appsettings.json` o dal menu **Impostazioni > Conversione audio** nella WebUI.

## Rinomina tracce

Quando la conversione audio e' attiva (**-cf**), le tracce convertite vengono automaticamente rinominate con un titolo descrittivo che include codec, layout canali, bit depth, sample rate e bitrate. Questo avviene sempre, senza bisogno di opzioni aggiuntive.

Con il flag **-rt** (o il checkbox "Rinomina tutte le tracce audio" in WebUI), la rinomina viene estesa anche alle tracce audio che non sono state convertite, sia dal file sorgente che dal file lingua. Utile per uniformare i nomi delle tracce nel file risultante quando i file originali hanno nomi inconsistenti o mancanti.

**Formato del nome generato:**

| Tipo | Formato | Esempio |
|------|---------|---------|
| Traccia originale | `Codec Layout BitDepth/SampleRate` | `DTS 5.1 24bit/48kHz` |
| Convertita FLAC | `FLAC Layout BitDepth/SampleRate` | `FLAC 5.1 24bit/48kHz` |
| Convertita Opus | `Opus Layout SampleRate Bitrate` | `Opus 5.1 48kHz 510kbps` |

Il layout canali viene formattato come 1.0 (mono), 2.0 (stereo), 5.1, 7.1. Le informazioni mancanti vengono omesse.

## Encoding video

Dopo il merge e' possibile ricodificare il video con un profilo di encoding personalizzato. L'encoding avviene in-place sul file risultato tramite ffmpeg: il video viene ricodificato, audio e sottotitoli vengono copiati senza modifiche.

Attivabile con **-ep "nome_profilo"** da CLI, oppure dal campo "Profilo encoding" nella configurazione WebUI.

I profili sono gestibili dal menu **Impostazioni > Profili encoding** nella WebUI (aggiungi, modifica, elimina) e vengono salvati in `appsettings.json`.

**Codec supportati:**

| Codec | Preset | CRF range | Rate control | Note |
|-------|--------|-----------|--------------|------|
| libx264 | ultrafast...placebo | 0-51 (default 23) | crf, bitrate | Supporta 2-pass per bitrate |
| libx265 | ultrafast...placebo | 0-51 (default 28) | crf, bitrate | Supporta 2-pass per bitrate |
| libsvtav1 | 0...13 | 0-63 (default 35) | crf, qp, bitrate | Film grain synthesis |

L'encoding usa encoder software. L'accelerazione GPU si applica solo alla decodifica nelle fasi di analisi/sync, non all'encoding.

**Esempio profilo:**

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

- **Name**: nome univoco, usato per selezionare il profilo da CLI (`-ep "x265_CRF24"`)
- **Codec**: `libx264`, `libx265` o `libsvtav1`
- **Preset**: velocita'/qualita'. Per x264/x265: da `ultrafast` a `placebo`. Per svtav1: da `0` (lento) a `13` (veloce)
- **Tune**: ottimizzazione per tipo di contenuto. Per x264: `film`, `animation`, `grain`, etc. Per svtav1: `0` (VQ), `1` (PSNR), `2` (SSIM). `default` per nessun tune
- **Profile**: profilo encoder, solo x264/x265 (`main`, `main10`, `high`, etc.). `default` per automatico
- **BitDepth**: profondita' bit e pixel format, es. `"10-bit: yuv420p10le"`. La parte dopo `: ` viene passata a ffmpeg come `-pix_fmt`
- **RateMode**: `crf` (qualita' costante), `qp` (solo svtav1), `bitrate` (target kbps)
- **CrfQp**: valore CRF o QP a seconda del rate mode
- **Bitrate**: target in kbps, usato solo con `RateMode: "bitrate"`
- **Passes**: `1` o `2`. Il 2-pass funziona solo con x264/x265 in modalita' bitrate
- **FilmGrain**: film grain synthesis 0-50, solo svtav1
- **FilmGrainDenoise**: denoise prima di applicare il film grain, solo svtav1
- **ExtraParams**: parametri ffmpeg aggiuntivi in formato libero, aggiunti alla fine del comando

## Accelerazione GPU

RemuxForge puo' usare ffmpeg con `-hwaccel auto` per accelerare la **decodifica video** durante le fasi di analisi (correzione velocita', frame-sync, deep analysis). L'opzione e' disattivata di default e si abilita dalla WebUI in `Impostazioni Avanzate > Ffmpeg > Hardware Acceleration`.

| Backend | Piattaforma | GPU |
|---------|-------------|-----|
| NVDEC | Linux, Windows | NVIDIA |
| VAAPI | Linux | Intel, AMD |
| VideoToolbox | macOS | Apple Silicon, Intel |

L'**encoding video** usa encoder software (libx264, libx265, libsvtav1). Encoder hardware come NVENC, VAAPI encode o VideoToolbox encode non sono supportati.

**Docker:** per abilitare l'accelerazione GPU nel container, vedere la sezione [Docker con accelerazione GPU](#docker-con-accelerazione-gpu).

## Casi d'uso

**1. Aggiungere doppiaggio italiano a release inglese**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -fs
```

**2. Sovrascrivere i file sorgente**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -o -fs
```

**3. Sostituire una traccia lossy con una lossless**

Il file ha gia' l'italiano AC3 lossy. Vuoi sostituirlo con DTS-HD MA da un'altra release.

```bash
RemuxForge -s "D:\Serie" -l "D:\Serie.ITA.HDMA" -t ita -ac "DTS-HD MA" -ksa eng,jpn -d "D:\Output" -fs
```

Con **-ksa eng,jpn** mantieni solo inglese e giapponese dal sorgente. Con **-ac "DTS-HD MA"** prendi solo la traccia lossless dalla release italiana.

**4. Remux multilingua da release diverse**

Ogni passaggio prende come sorgente l'output del precedente.

```bash
RemuxForge -s "D:\Film.US" -l "D:\Film.ITA" -t ita -d "D:\Temp1" -fs
RemuxForge -s "D:\Temp1" -l "D:\Film.FRA" -t fra -d "D:\Temp2" -fs
RemuxForge -s "D:\Temp2" -l "D:\Film.GER" -t ger -d "D:\Output" -fs
```

**5. Anime con naming non standard**

Molti fansub usano "- 05" invece di S01E05. Con **-m** specifichi una regex custom. Con **-so** prendi solo i sottotitoli.

```bash
RemuxForge -s "D:\Anime.BD" -l "D:\Anime.Fansub" -t ita -m "- (\d+)" -so -d "D:\Output" -fs
```

**6. Daily show con date nel nome file**

```bash
RemuxForge -s "D:\Show.US" -l "D:\Show.ITA" -t ita -m "(\d{4})\.(\d{2})\.(\d{2})" -d "D:\Output"
```

**7. Filtrare sottotitoli dal sorgente**

Il sorgente ha 10 tracce sub in lingue inutili. Con **-kss** tieni solo quelle che vuoi.

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -so -kss eng -d "D:\Output" -fs
```

**8. Anime: tenere solo audio giapponese e importare eng+ita**

Il trucco **-kss und** scarta tutti i sottotitoli dal sorgente perche' nessuna traccia ha lingua "und".

```bash
RemuxForge -s "D:\Anime.BD.JPN" -l "D:\Anime.ITA" -t eng,ita -ksa jpn -kss und -d "D:\Output" -fs
```

**9. Dry run su configurazione complessa**

Con **-n** verifica matching e tracce senza eseguire.

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3" -ksa eng -kss eng -d "D:\Output" -fs -n
```

**10. Tenere solo tracce DTS dal sorgente**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksac DTS -d "D:\Output" -fs
```

**11. Tenere solo audio inglese lossless dal sorgente**

Combinando **-ksa** e **-ksac**, mantieni solo tracce che soddisfano entrambi i criteri.

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksa eng -ksac "DTS-HDMA,TrueHD" -d "D:\Output" -fs
```

**12. Importare piu' codec dal file lingua**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3,DTS" -d "D:\Output" -fs
```

**13. Singola sorgente: applicare delay e filtrare tracce**

Senza **-l**, l'applicazione usa la cartella sorgente anche come lingua. Permette di remuxare con filtri e delay senza una release separata.

```bash
RemuxForge -s "D:\Serie" -t ita -ksa jpn,eng -kss eng,jpn -ad 960 -sd 960 -o
```

**14. Convertire tracce lossless in FLAC durante il merge**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -cf flac -d "D:\Output" -fs
```

**15. Convertire tracce lossless in Opus mantenendo solo l'inglese dal sorgente**

Le tracce TrueHD Atmos e DTS:X vengono mantenute intatte.

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -cf opus -ksa eng -d "D:\Output" -fs
```

**16. Merge + encoding video con profilo x265**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ep "x265_CRF24" -d "D:\Output" -fs
```

**17. Merge + conversione audio + encoding video**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -cf flac -ep "svtav1_CRF30" -ksa eng -d "D:\Output" -fs
```

**18. Deep analysis per file con scene diverse**

```bash
RemuxForge -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -da
```

## Report

A fine elaborazione viene mostrato un report riassuntivo. In WebUI il dettaglio e' visibile nel pannello laterale di ciascun episodio.

Da CLI il report mostra 3 tabelle:

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
  Episode     Audio          Subtitles      Size      Delay       FrmSync   Deep      Speed     Merge
  ----------------------------------------------------------------------------------------------------
  01_05       eng,jpn,ita    eng,ita        4.3 GB    +150ms      -         3 ops     1250ms    12500ms
```

**Colonne Result Files:**
- **Delay**: offset applicato alle tracce importate
- **FrmSync**: tempo di elaborazione frame-sync (se attivo, altrimenti "-")
- **Deep**: numero di operazioni taglia-cuci generate dalla deep analysis (se attiva, altrimenti "-")
- **Speed**: tempo di elaborazione speed correction (se attiva, altrimenti "-")
- **Merge**: tempo di esecuzione mkvmerge

In modalita' dry run, Size e Merge mostrano "N/A" perche' il merge non viene eseguito.

## Codec audio

Quando specifichi **-ac** o **-ksac** per filtrare i codec, il matching e' **ESATTO**, non parziale. Entrambi supportano valori multipli separati da virgola.

Se un file ha sia DTS (core) che DTS-HD MA, e scrivi **-ac "DTS"**, prende SOLO il DTS core. Se vuoi il DTS-HD Master Audio, devi scrivere **-ac "DTS-HDMA"**. Se vuoi entrambi: **-ac "DTS,DTS-HDMA"**.

I nomi codec sono case-insensitive. Se un codec non viene riconosciuto con lookup diretto, viene provato un match senza trattini, spazi e due punti.

**Dolby:**

| Codec | Alias | Descrizione |
|-------|-------|-------------|
| AC-3 | AC3, DD | Dolby Digital, il classico 5.1 lossy |
| E-AC-3 | EAC3, DD+, DDP | Dolby Digital Plus, usato per Atmos lossy su streaming |
| TrueHD | TRUEHD | Dolby TrueHD, lossless, usato per Atmos su Blu-ray |
| MLP | | Meridian Lossless Packing (base di TrueHD) |
| ATMOS | | Alias speciale: matcha sia TrueHD che E-AC-3 |

**DTS:**

| Codec | Alias | Descrizione |
|-------|-------|-------------|
| DTS | | Solo DTS Core/Digital Surround (NON matcha DTS-HD) |
| DTS-HD | | Matcha sia DTS-HD Master Audio che DTS-HD High Resolution |
| DTS-HD MA | DTS-HDMA | DTS-HD Master Audio, lossless |
| DTS-HD HR | DTS-HDHR | DTS-HD High Resolution |
| DTS-ES | | DTS Extended Surround (6.1) |
| DTS:X | DTSX | Object-based, estensione di DTS-HD MA |

**Lossless:**

| Codec | Alias | Descrizione |
|-------|-------|-------------|
| FLAC | | Free Lossless Audio Codec |
| PCM | LPCM, WAV | Audio raw non compresso |
| ALAC | | Apple Lossless |

**Lossy:**

| Codec | Alias | Descrizione |
|-------|-------|-------------|
| AAC | HE-AAC | Advanced Audio Coding |
| MP3 | | MPEG Audio Layer 3 |
| MP2 | | MPEG Audio Layer 2 |
| Opus | OPUS | Opus (WebM) |
| Vorbis | VORBIS | Ogg Vorbis |

## Codici lingua

I codici lingua sono ISO 639-2 (3 lettere). I piu' comuni:

| Codice | Lingua |
|--------|--------|
| ita | Italiano |
| eng | Inglese |
| jpn | Giapponese |
| ger / deu | Tedesco |
| fra / fre | Francese |
| spa | Spagnolo |
| por | Portoghese |
| rus | Russo |
| chi / zho | Cinese |
| kor | Coreano |
| und | Undefined (lingua non specificata) |

Se sbagli un codice, l'applicazione suggerisce quello corretto:

```
Lingua 'italian' non riconosciuta.
Forse intendevi: ita?
```

## Pattern regex per matching episodi

L'applicazione usa i gruppi catturati dalla regex per abbinare i file. Ogni gruppo tra parentesi viene concatenato con "_" per creare l'ID univoco dell'episodio.

| Formato | Esempio file | Pattern |
|---------|-------------|---------|
| Standard | Serie.S01E05.mkv | S(\d+)E(\d+) |
| Con punto | Serie.S01.E05.mkv | S(\d+)\.E(\d+) |
| Formato 1x05 | Serie.1x05.mkv | (\d+)x(\d+) |
| Solo episodio | Anime - 05.mkv | - (\d+) |
| Episodio 3 cifre | Anime - 005.mkv | - (\d{3}) |
| Daily show | Show.2024.01.15.mkv | (\d{4})\.(\d{2})\.(\d{2}) |

Il pattern **S(\d+)E(\d+)** cattura due gruppi (stagione e episodio). Per "S01E05" crea l'ID "01_05". File sorgente e lingua con lo stesso ID vengono abbinati.

## Configurazione (appsettings.json)

Tutte le impostazioni persistenti sono salvate in `.remux-forge/appsettings.json`. Il file viene creato automaticamente con valori di default. La cartella `.remux-forge` si trova nella directory dell'eseguibile, o nel percorso indicato da `REMUXFORGE_DATA_DIR`.

I nuovi campi aggiunti in aggiornamenti successivi vengono integrati automaticamente senza sovrascrivere i valori utente esistenti.

```json
{
  "Tools": {
    "MkvMergePath": "",
    "FfmpegPath": "",
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
  "Ui": {
    "Theme": "nord"
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
    "TrackSplit": {
      "FfmpegTimeoutMs": 300000
    },
    "Ffmpeg": {
      "HardwareAcceleration": false
    }
  }
}
```

**Sezioni:**

- **Tools**: percorsi degli eseguibili e cartella temporanei. Rilevati automaticamente all'avvio, modificabili dal menu Impostazioni > Percorsi tool
- **Flac**: livello compressione FLAC (0 = veloce, 12 = massima compressione)
- **Opus.Bitrate**: bitrate Opus in kbps per layout canali (range: 64-768)
- **Ui.Theme**: tema grafico selezionato. Temi validi: `dark`, `nord`, `dos-blue`, `matrix`, `cyberpunk`, `solarized-dark`, `solarized-light`, `cybergum`, `everforest`
- **EncodingProfiles**: array di profili encoding video (vedi sezione Encoding video)
- **Advanced**: parametri di sincronizzazione. La WebUI espone solo tuning operativo ed Expert essenziale; i parametri algoritmici interni restano modificabili dal file di configurazione. I valori di default sono calibrati per la maggior parte dei casi.
- **Advanced.Ffmpeg.HardwareAcceleration**: abilita `-hwaccel auto` per le analisi video ffmpeg. Default: `false`

## Build da sorgente

Richiede .NET 10 SDK.

```bash
# Build CLI
dotnet build RemuxForge.Cli -c Release

# Build WebUI (richiede libman per le librerie client-side)
cd RemuxForge.Web && libman restore && cd ..
dotnet build RemuxForge.Web -c Release
```

**Publish come eseguibile standalone (single file, compresso):**

```bash
# CLI
dotnet publish RemuxForge.Cli -c Release -r win-x64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r linux-x64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r linux-arm64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r osx-x64 --self-contained true
dotnet publish RemuxForge.Cli -c Release -r osx-arm64 --self-contained true

# WebUI
dotnet publish RemuxForge.Web -c Release -r win-x64 --self-contained true
dotnet publish RemuxForge.Web -c Release -r linux-x64 --self-contained true
dotnet publish RemuxForge.Web -c Release -r linux-arm64 --self-contained true
dotnet publish RemuxForge.Web -c Release -r osx-x64 --self-contained true
dotnet publish RemuxForge.Web -c Release -r osx-arm64 --self-contained true
```

**Docker:**

```bash
docker build -t remuxforge .
```

## Nota sull'uso di LLM

Durante lo sviluppo di RemuxForge e' stato usato supporto basato su LLM per:

- integrazione e aggiornamento della documentazione README
- supporto al design e alla rifinitura della WebUI
- assistenza in eventuali refactor estesi quando necessari

## Buy me a coffee!

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/simonefil)
