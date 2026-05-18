# ![icon](icons/icon-48.png) RemuxForge

Applicazione cross-platform per due workflow MKV separati:

- **Remux**: unisce tracce audio e sottotitoli da file MKV in lingue diverse, con sincronizzazione automatica tra release con montaggio o velocita' differenti.
- **Split**: taglia MKV HEVC/AVC in segmenti frame-perfect, preservando VFR, capitoli, audio e sottotitoli.

Disponibile in due interfacce: CLI (riga di comando) e WebUI (interfaccia web). La CLI richiede sempre `--mode remux` oppure `--mode split`; la WebUI mostra lo switch `Remux | Split` in alto e ricorda l'ultima modalita' selezionata.

## Funzionalita' principali

- Modalita Remux per importare audio e sottotitoli da altre release, anche su intere stagioni
- Sincronizzazione: speed correction per velocita' globali diverse, frame-sync per delay costante, Deep Analysis per delay costante piu' tagli/aggiunte
- Filtro per lingua, codec audio, sottotitoli, sia in importazione che in mantenimento dal sorgente
- Post-processing audio verso FLAC, LPCM, AAC o Opus, con normalizzazione peak, 24bit -> 16bit e rinomina tracce
- Encoding video post-merge con profili personalizzabili (x264, x265, SVT-AV1)
- Accelerazione GPU opzionale per decodifica video nelle fasi di analisi
- Due interfacce: CLI scriptabile e WebUI per browser e server headless
- Deploy via Docker con supporto GPU opzionale
- Modalita Split per pattern capitoli, range espliciti, split-at, trim, capitoli singoli e sorgenti da cartella

## Requisiti

- [MKVToolNix](https://mkvtoolnix.download/) (`mkvmerge`, `mkvextract`, `mkvpropedit`)
- [ffmpeg](https://ffmpeg.org/) (`ffmpeg`, `ffprobe`)
- [mediainfo CLI](https://mediainfo.sourceforge.net/) (`mediainfo`)
- Locale UTF-8 su Linux

I percorsi dei tool possono essere rilevati automaticamente o configurati dalla WebUI in **Impostazioni > Percorsi tool**.

**Piattaforme:**

| Piattaforma | Architetture |
|-------------|-------------|
| Windows | x64 |
| Linux | x64, ARM64 |
| macOS | x64, ARM64 |
| Docker | x64 (immagine con mkvtoolnix, ffmpeg e mediainfo preinstallati) |

## Installazione e avvio

### Desktop: CLI

Scaricare l'archivio per la propria piattaforma dalla [pagina release](https://github.com/simonefil/RemuxForge/releases), estrarre ed eseguire.

- **Windows**: aprire un terminale e lanciare `RemuxForge.Cli.exe` con parametri
- **Linux/macOS**: `chmod +x RemuxForge.Cli && ./RemuxForge.Cli` con parametri

Lanciando senza parametri viene mostrato l'help CLI. Con parametri si esegue in modalita' CLI.

### Desktop: WebUI

Scaricare l'archivio WebUI dalla [pagina release](https://github.com/simonefil/RemuxForge/releases), estrarre ed eseguire.

- **Windows**: doppio click su `RemuxForge.Web.exe`
- **Linux/macOS**: `chmod +x RemuxForge.Web && ./RemuxForge.Web`

Aprire `http://localhost:5000` nel browser. La porta e' configurabile con la variabile d'ambiente `REMUXFORGE_PORT` oppure, se la variabile non e' impostata, con `--port <numero>`.

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

La decodifica video durante le fasi di analisi (speed correction, frame-sync, Deep Analysis) puo' essere accelerata via GPU se l'opzione avanzata `Hardware Acceleration` e' abilitata. Quando attiva, ffmpeg usa `-hwaccel auto` e seleziona automaticamente il backend disponibile nel container.

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

## Modalita Remux

La modalita Remux importa audio e sottotitoli da una release MKV a un'altra. Gestisce matching episodi, filtri lingua/codec, sincronizzazione, post-processing audio, rinomina tracce, encoding video e report finale.

La sincronizzazione ha tre percorsi distinti:

- **Speed correction**: corregge differenze di velocita' globali, per esempio PAL/NTSC. Default `off`; in `auto` lavora solo su CFR affidabili, mentre su VFR serve `manual` con stretch factor esplicito.
- **Frame-sync**: calcola un solo delay costante. Usarlo quando i due video hanno lo stesso montaggio e differiscono solo per offset iniziale.
- **Deep Analysis**: calcola delay iniziale e operazioni di taglio/inserimento. Usarla quando ci sono scene mancanti, aggiunte o montaggi diversi.

Frame-sync e Deep Analysis sono mutuamente esclusivi. Se una release ha sia speed mismatch sia edit locali, impostare lo stretch globale manualmente e usare Deep Analysis per la mappa degli edit.

### WebUI

La WebUI lavora per batch: configurazione cartelle, scan/matching, analisi, merge e controllo del risultato. Il dettaglio dell'episodio mostra stato, delay, operazioni previste, tracce risultanti e tempi delle fasi principali.

#### Tasti rapidi

| Tasto | Azione |
|-------|--------|
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

#### Menu contestuale

Cliccando con il tasto destro su un episodio (o premendo Enter) si apre un menu contestuale con le seguenti voci:

- **Delay**: modifica il delay manuale per l'episodio selezionato
- **MediaInfo sorgente**: mostra il report MediaInfo completo del file sorgente
- **MediaInfo lingua**: mostra il report MediaInfo del file lingua
- **MediaInfo risultato**: mostra il report MediaInfo del file risultante (disponibile dopo il merge)

Le voci MediaInfo sono visibili solo se il tool mediainfo e' configurato e il file corrispondente esiste. Il report mostra tutte le informazioni sulle tracce (codec, canali, bitrate, risoluzione, lingua, ecc.) e puo' essere copiato negli appunti.

#### Menu

- **File**: Configurazione (F2), Esci (Ctrl+Q)
- **Azioni**: Scan file (F5), Analizza selezionato (F6), Analizza tutti (F7), Skip/Unskip (F8), Processa selezionato (F9), Processa tutti (F10)
- **Impostazioni**: Percorsi tool, Audio, Profili encoding, Avanzate
- **Vista**: Pipeline (mostra la sequenza di operazioni che verranno eseguite in base alla configurazione corrente: sync, conversione, merge, encoding)
- **Tema**: cambio tema grafico (8 temi)
- **Aiuto**: Info

#### Configurazione (F2)

Il dialog di configurazione raggruppa tutte le opzioni di elaborazione:

![Dialog configurazione](images/config.png)

- **Cartelle**: Source, Lingua, Destinazione, con pulsante browse per ciascuna. Checkbox per sovrascrivere sorgente e ricerca ricorsiva
- **Lingua e Tracce**: Lingua target, Codec audio, Keep source audio/codec/sub, Solo sottotitoli, Solo audio
- **Sincronizzazione**: Speed correction (`off`/`auto`/`manual` con stretch fisso), Frame-sync, Deep Analysis e delay manuali. Frame-sync e Deep Analysis sono esclusivi.
- **Matching**: Pattern match (regex) ed estensioni file
- **Post-processing audio**: Formato audio (flac/lpcm/aac/opus), scope audio, Audio source fill, 24bit -> 16bit, normalizzazione e rinomina audio
- **Post-processing video**: Profilo encoding

#### Menu Impostazioni

- **Percorsi tool**: Percorsi di mkvmerge, mkvextract, mkvpropedit, ffmpeg, ffprobe, mediainfo e cartella file temporanei. I tool vengono cercati automaticamente all'avvio. ffmpeg puo' essere scaricato dall'interfaccia su Windows/Linux; su macOS usare Homebrew o configurare il path manualmente
- **Audio**: Livello compressione FLAC e bitrate AAC/Opus per layout canali (mono, stereo, 5.1, 7.1)
- **Profili encoding**: Gestione profili di encoding video (aggiungi, modifica, elimina). I profili sono salvati in appsettings.json
- **Avanzate**: tuning operativo essenziale per analisi, frame-sync, Deep Analysis, timeout e accelerazione hardware. Le sezioni Expert espongono solo le soglie principali; i parametri algoritmici interni restano nel file di configurazione.

La WebUI mostra una doppia progress bar: avanzamento globale del batch e avanzamento dell'episodio corrente. La barra episodio espone i substep principali di speed correction, frame-sync, Deep Analysis, conversione, taglia-cuci e merge. Lo stato e' condiviso tra browser/tab collegati alla stessa istanza.

![Gestione profili encoding](images/encoding.png)

![Configurazione WebUI](images/config-webui.png)

### CLI

La CLI e' pensata per batch scriptabili di merge/sync. Usa sempre `--mode remux`.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -fs
```

#### Parametri obbligatori

| Short | Long | Descrizione |
|-------|------|-------------|
| | --mode | Deve essere `remux` |
| -s | --source | Cartella con i file MKV sorgente |
| -t | --target-language | Codice lingua delle tracce da importare (es: ita). Separare con virgola per piu' lingue: ita,eng |

#### Sorgente

| Short | Long | Descrizione |
|-------|------|-------------|
| -l | --language | Cartella con i file MKV da cui prendere le tracce. Se omesso, usa la cartella sorgente |

#### Output (mutuamente esclusivi, uno obbligatorio)

| Short | Long | Descrizione |
|-------|------|-------------|
| -d | --destination | Cartella dove salvare i file risultanti |
| -o | --overwrite | Sovrascrive i file sorgente |

#### Sync

| Short | Long | Descrizione |
|-------|------|-------------|
| -fs | --framesync | Sincronizzazione tramite confronto visivo frame (scene-cut) |
| | --framesync-diagnostics | Scrive diagnostica JSON frame-sync in `.remux-forge/framesync-diagnostics` |
| -da | --deep-analysis | Analisi completa per file con edit diversi (mutuamente esclusiva con -fs) |
| | --deep-analysis-diagnostics | Scrive diagnostica JSON Deep Analysis in `.remux-forge/deepanalysis-diagnostics` |
| | --speed-correction | Modalita' correzione velocita': off, auto, manual. Default: off |
| | --stretch-factor | Fattore fisso per speed-correction manual, esempio 25025/24000 |
| | --no-speed-correction | Compatibilita': disattiva la correzione velocita' |
| -ad | --audio-delay | Delay manuale in ms per l'audio (sommato a frame-sync/speed se attivi) |
| -sd | --subtitle-delay | Delay manuale in ms per i sottotitoli |
| | --audio-source-fill-threshold-ms | Soglia in ms per riempire audio importato con segmenti audio source |
| | --audio-source-fill-language | Lingua audio source da usare per i segmenti di riempimento |
| | --audio-source-fill-modes | Modalita' di riempimento: `start`, `end`, `insert-silence`. Richiede `--audio-format` e `--audio-scope lang|all` |

La correzione velocita' e' disattivata di default. In `auto` viene usata solo quando i metadati CFR sono affidabili; con file VFR e' necessario impostare `manual` e un fattore esplicito.

#### Filtri

| Short | Long | Descrizione |
|-------|------|-------------|
| -ac | --audio-codec | Codec audio da importare dal file lingua. Separa con virgola: DTS,E-AC-3 |
| -so | --sub-only | Importa solo sottotitoli, ignora l'audio |
| -ao | --audio-only | Importa solo audio, ignora i sottotitoli |
| -ksa | --keep-source-audio | Lingue audio da MANTENERE nel sorgente (le altre vengono rimosse) |
| -ksac | --keep-source-audio-codec | Codec audio da MANTENERE nel sorgente. Separa con virgola: DTS,TrueHD |
| -kss | --keep-source-subs | Lingue sub da MANTENERE nel sorgente |

#### Matching

| Short | Long | Descrizione | Default |
|-------|------|-------------|---------|
| -m | --match-pattern | Regex per matching episodi | S(\d+)E(\d+) |
| -r | --recursive | Cerca nelle sottocartelle | attivo |
| -nr | --no-recursive | Disabilita la ricerca ricorsiva | |
| -ext | --extensions | Estensioni file da cercare. Separa con virgola: mkv,mp4,avi | mkv |

#### Audio post-processing e encoding

| Short | Long | Descrizione |
|-------|------|-------------|
| | --audio-format | Formato finale audio: flac, lpcm, aac, opus. Se impostato senza `--audio-scope`, in CLI lo scope predefinito e' `all` |
| | --audio-scope | Tracce da processare: `disabled`, `lang`, `all` |
| | --audio-24-to-16 | Converte 24bit -> 16bit con soxr/shibata (flac/lpcm) |
| | --audio-peak-normalize | Peak normalization globale multicanale |
| | --audio-peak-target-db | Target peak in dB |
| | --audio-rename-scope | Rinomina audio finale: disabled, lang, all |
| -ep | --encoding-profile | Profilo encoding video post-merge (definito in appsettings.json) |

#### Altro

| Short | Long | Descrizione |
|-------|------|-------------|
| -n | --dry-run | Mostra cosa farebbe senza eseguire |
| -h | --help | Mostra l'help integrato |
| -mkv | --mkvmerge-path | Percorso custom di mkvmerge (default: cerca nel PATH) |

### Sincronizzazione

Le release dello stesso contenuto possono differire per velocita' di riproduzione, offset iniziale o montaggio interno. RemuxForge separa questi casi invece di applicare una correzione unica a tutti i problemi.

**Quale metodo usare:**

| Situazione | Metodo | Opzione | Note |
|------------|--------|---------|------|
| Stessa release, solo lingua diversa | Nessuno (merge diretto) | | Le tracce sono gia' allineate |
| PAL vs NTSC o altra velocita' globale diversa | Correzione velocita' | `--speed-correction auto` su CFR affidabili, `manual --stretch-factor ...` su VFR | Default off |
| Offset costante | Frame-Sync | `-fs` | Calcola un delay fisso valido per tutto il file |
| Scene tagliate, aggiunte o montaggio diverso | Deep Analysis | `-da` | Genera una mappa di cut/insert sulle tracce importate |

Frame-Sync e Deep Analysis sono mutuamente esclusivi. La correzione velocita' e' indipendente e puo' essere `off`, `auto` o `manual`; su VFR la modalita' `auto` fallisce in modo controllato.

### Correzione velocita' (off/auto/manual)

Compensa una differenza di velocita' globale tra source e lang, per esempio una traccia ricavata da PAL da portare su una release NTSC/BD. Non corregge scene mancanti o aggiunte.

La modalita' di default e' `off`. In `auto` il rilevamento confronta gli FPS dei due file tramite MediaInfo/mkvmerge e procede solo quando entrambi i file sono CFR affidabili. In `manual` il fattore viene indicato esplicitamente con `--stretch-factor`, ad esempio `25025/24000`, ed e' la modalita' corretta per sorgenti VFR o metadati ambigui.

Quando la correzione e' attiva, il flusso procede con:

1. Risolve il timing video con MediaInfo come fonte primaria
2. Blocca `auto` se uno dei file e' VFR o se i metadati non sono coerenti
3. Calcola o applica lo stretch factor
4. Applica lo stretch tramite mkvmerge alle tracce importate, senza ricodifica

Se uno dei file ha frame rate variabile (VFR), usare `manual`: il valore medio o il `default_duration` del container non bastano per decidere automaticamente uno stretch affidabile.

### Frame-Sync

Calcola un offset fisso per riallineare tracce quando source e lang hanno lo stesso montaggio ma un delay iniziale diverso.

Attivabile con **-fs** da CLI o dal checkbox nella configurazione WebUI.

1. Estrae i frame iniziali da entrambi i file (2 minuti dal sorgente, 3 dalla lingua)
2. Individua i tagli scena in entrambi i file
3. Per ogni coppia di tagli, calcola quale sarebbe il delay se corrispondessero allo stesso momento. Il delay che riceve piu' "voti" coerenti viene selezionato come candidato
4. Verifica il candidato confrontando la firma visiva attorno ai tagli: se i frame prima e dopo sono simili tra i due file, il match e' confermato
5. Conferma in 9 punti lungo il video. Servono almeno 5 punti validi su 9

Quando abilitato in configurazione, il fingerprint audio globale puo' confermare o bocciare candidati deboli. Non sostituisce la verifica video e non trasforma Frame-Sync in una correzione di tagli.

Frame-Sync non applica cut o insert. Se il drift cambia durante l'episodio, il risultato viene scartato e serve la Deep Analysis.

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

Se Deep Analysis trova solo un delay costante, RemuxForge applica il delay tramite mkvmerge senza ricodificare l'audio. Se invece la mappa contiene cut o insert sulle tracce audio importate, serve un formato audio di uscita (`--audio-format` oppure Formato audio in WebUI); in assenza del formato, l'episodio fallisce invece di produrre un audio non modificato.

Deep Analysis e' fail-safe: se una traccia richiesta non puo' essere riscritta o validata, l'episodio fallisce invece di importare una traccia non editata. I codec audio senza encoder ffmpeg utilizzabile per taglia-cuci non vengono importati con fallback silenzioso.

### Audio source fill

Quando l'audio importato non copre una porzione presente nel source, RemuxForge puo' riempire quella parte usando una traccia audio gia' presente nel source, anche in un'altra lingua. La funzione si abilita impostando una soglia in millisecondi, una lingua source e una o piu' modalita':

| Modalita | Quando interviene | Risultato |
|----------|-------------------|-----------|
| `start` | Il delay audio iniziale positivo supera la soglia | Anteponde alla traccia importata il segmento iniziale preso dal source |
| `end` | La traccia importata termina prima del source oltre la soglia | Appende il segmento finale preso dal source |
| `insert-silence` | Deep Analysis genera un `INSERT_SILENCE` oltre la soglia | Usa il segmento source corrispondente invece di inserire silenzio |

Da CLI si configura con `--audio-source-fill-threshold-ms`, `--audio-source-fill-language` e `--audio-source-fill-modes`. Richiede anche un formato audio e uno scope audio attivo (`--audio-format ... --audio-scope lang|all`). In WebUI compare nel blocco Post-processing audio solo dopo aver scelto Formato audio. Se il riempimento e' richiesto ma la traccia source indicata non e' disponibile, l'episodio fallisce invece di produrre una traccia incompleta.

### Delay manuale

I parametri **-ad** (audio delay) e **-sd** (subtitle delay) specificano un offset in millisecondi che viene **sommato** al risultato di frame-sync o speed correction. Nella WebUI e' possibile impostare delay diversi per singolo episodio.

### Audio post-processing

Processa le tracce audio selezionate durante il merge. Attivabile da CLI con `--audio-format flac|lpcm|aac|opus` e `--audio-scope lang|all`, oppure dai campi "Formato audio" e "Audio" nella configurazione WebUI. In CLI, se viene indicato solo `--audio-format`, lo scope diventa `all`.

FLAC e LPCM sono lossless; AAC e Opus sono lossy. Le tracce Atmos/DTS:X possono essere copiate, ma non processate.

Quando piu' tracce vengono processate nello stesso episodio, le conversioni audio vengono eseguite in parallelo con limite interno sulle operazioni simultanee.

Opzioni aggiuntive:

- `--audio-24-to-16`: riduce 24-bit a 16-bit con soxr/shibata, solo FLAC/LPCM
- `--audio-peak-normalize`: peak normalization globale multicanale
- `--audio-peak-target-db`: target peak in dB, default -1.0
- `--audio-rename-scope disabled|lang|all`: rinomina audio finale

**Bitrate di default:**

| Formato | Impostazione | Default |
|---------|-------------|---------|
| FLAC | Livello compressione (0-12) | 8 |
| AAC Mono | kbps | 128 |
| AAC Stereo | kbps | 256 |
| AAC 5.1 | kbps | 768 |
| AAC 7.1 | kbps | 1024 |
| Opus Mono | kbps | 128 |
| Opus Stereo | kbps | 256 |
| Opus 5.1 | kbps | 510 |
| Opus 7.1 | kbps | 768 |

I valori sono configurabili in `appsettings.json` o dal menu **Impostazioni > Audio** nella WebUI.

### Rinomina tracce

La rinomina audio si controlla con `--audio-rename-scope disabled|lang|all`. Le tracce processate ricevono un titolo descrittivo che include codec, layout canali, sample rate e bitrate/bit depth dove significativo.

**Formato del nome generato:**

| Tipo | Formato | Esempio |
|------|---------|---------|
| Traccia originale | `Codec Layout BitDepth/SampleRate` | `DTS 5.1 24bit/48kHz` |
| Processata FLAC | `FLAC Layout BitDepth/SampleRate` | `FLAC 5.1 24bit/48kHz` |
| Processata LPCM | `LPCM Layout BitDepth/SampleRate` | `LPCM 5.1 24bit/48kHz` |
| Processata AAC | `AAC Layout SampleRate Bitrate` | `AAC 5.1 48kHz 768kbps` |
| Processata Opus | `Opus Layout SampleRate Bitrate` | `Opus 5.1 48kHz 510kbps` |

Il layout canali viene formattato come 1.0 (mono), 2.0 (stereo), 5.1, 7.1. Le informazioni mancanti vengono omesse.

### Encoding video

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

### Accelerazione GPU

RemuxForge puo' usare ffmpeg con `-hwaccel auto` per accelerare la **decodifica video** durante le fasi di analisi (correzione velocita', frame-sync, Deep Analysis). L'opzione e' disattivata di default e si abilita dalla WebUI in `Impostazioni Avanzate > Ffmpeg > Hardware Acceleration`.

| Backend | Piattaforma | GPU |
|---------|-------------|-----|
| NVDEC | Linux, Windows | NVIDIA |
| VAAPI | Linux | Intel, AMD |
| VideoToolbox | macOS | Apple Silicon, Intel |

L'**encoding video** usa encoder software (libx264, libx265, libsvtav1). Encoder hardware come NVENC, VAAPI encode o VideoToolbox encode non sono supportati.

**Docker:** per abilitare l'accelerazione GPU nel container, vedere la sezione [Docker con accelerazione GPU](#docker-con-accelerazione-gpu).

### Casi d'uso

**1. Aggiungere doppiaggio italiano a release inglese**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -fs
```

**2. Sovrascrivere i file sorgente**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -o -fs
```

**3. Sostituire una traccia lossy con una lossless**

Il file ha gia' l'italiano AC3 lossy. Vuoi sostituirlo con DTS-HD MA da un'altra release.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie" -l "D:\Serie.ITA.HDMA" -t ita -ac "DTS-HD MA" -ksa eng,jpn -d "D:\Output" -fs
```

Con **-ksa eng,jpn** mantieni solo inglese e giapponese dal sorgente. Con **-ac "DTS-HD MA"** prendi solo la traccia lossless dalla release italiana.

**4. Remux multilingua da release diverse**

Ogni passaggio prende come sorgente l'output del precedente.

```bash
RemuxForge.Cli --mode remux -s "D:\Film.US" -l "D:\Film.ITA" -t ita -d "D:\Temp1" -fs
RemuxForge.Cli --mode remux -s "D:\Temp1" -l "D:\Film.FRA" -t fra -d "D:\Temp2" -fs
RemuxForge.Cli --mode remux -s "D:\Temp2" -l "D:\Film.GER" -t ger -d "D:\Output" -fs
```

**5. Anime con naming non standard**

Molti fansub usano "- 05" invece di S01E05. Con **-m** specifichi una regex custom. Con **-so** prendi solo i sottotitoli.

```bash
RemuxForge.Cli --mode remux -s "D:\Anime.BD" -l "D:\Anime.Fansub" -t ita -m "- (\d+)" -so -d "D:\Output" -fs
```

**6. Daily show con date nel nome file**

```bash
RemuxForge.Cli --mode remux -s "D:\Show.US" -l "D:\Show.ITA" -t ita -m "(\d{4})\.(\d{2})\.(\d{2})" -d "D:\Output"
```

**7. Filtrare sottotitoli dal sorgente**

Il sorgente ha 10 tracce sub in lingue inutili. Con **-kss** tieni solo quelle che vuoi.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -so -kss eng -d "D:\Output" -fs
```

**8. Anime: tenere solo audio giapponese e importare eng+ita**

Il trucco **-kss und** scarta tutti i sottotitoli dal sorgente perche' nessuna traccia ha lingua "und".

```bash
RemuxForge.Cli --mode remux -s "D:\Anime.BD.JPN" -l "D:\Anime.ITA" -t eng,ita -ksa jpn -kss und -d "D:\Output" -fs
```

**9. Dry run su configurazione complessa**

Con **-n** verifica matching e tracce senza eseguire.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3" -ksa eng -kss eng -d "D:\Output" -fs -n
```

**10. Tenere solo tracce DTS dal sorgente**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksac DTS -d "D:\Output" -fs
```

**11. Tenere solo audio inglese lossless dal sorgente**

Combinando **-ksa** e **-ksac**, mantieni solo tracce che soddisfano entrambi i criteri.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ksa eng -ksac "DTS-HDMA,TrueHD" -d "D:\Output" -fs
```

**12. Importare piu' codec dal file lingua**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ac "E-AC-3,DTS" -d "D:\Output" -fs
```

**13. Singola sorgente: applicare delay e filtrare tracce**

Senza **-l**, l'applicazione usa la cartella sorgente anche come lingua. Permette di remuxare con filtri e delay senza una release separata.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie" -t ita -ksa jpn,eng -kss eng,jpn -ad 960 -sd 960 -o
```

**14. Processare le tracce importate in FLAC durante il merge**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita --audio-format flac --audio-scope lang -d "D:\Output" -fs
```

**15. Processare tutte le tracce in Opus mantenendo solo l'inglese dal sorgente**

Le tracce TrueHD Atmos e DTS:X vengono mantenute intatte.

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita --audio-format opus --audio-scope all -ksa eng -d "D:\Output" -fs
```

**16. Merge + encoding video con profilo x265**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -ep "x265_CRF24" -d "D:\Output" -fs
```

**17. Merge + audio post-processing + encoding video**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita --audio-format flac --audio-scope all -ep "svtav1_CRF30" -ksa eng -d "D:\Output" -fs
```

**18. Deep Analysis per file con scene diverse**

```bash
RemuxForge.Cli --mode remux -s "D:\Serie.ENG" -l "D:\Serie.ITA" -t ita -d "D:\Output" -da
```

### Report

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
  Episode     Audio          Subtitles      Size      Delay       FrmSync   FSConf   Deep      Speed     Merge
  ----------------------------------------------------------------------------------------------------
  01_05       eng,jpn,ita    eng,ita        4.3 GB    +150ms      -         -        3 ops     1250ms    12500ms
```

**Colonne Result Files:**
- **Delay**: offset applicato alle tracce importate
- **FrmSync**: tempo di elaborazione frame-sync (se attivo, altrimenti "-")
- **FSConf**: confidenza del risultato frame-sync in percentuale (se disponibile, altrimenti "-")
- **Deep**: numero di operazioni taglia-cuci generate dalla Deep Analysis (se attiva, altrimenti "-")
- **Speed**: tempo di elaborazione speed correction (se attiva, altrimenti "-")
- **Merge**: tempo di esecuzione mkvmerge

In modalita' dry run, Size e Merge mostrano "N/A" perche' il merge non viene eseguito.

### Codec audio

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

### Codici lingua

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

### Pattern regex per matching episodi

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


## Modalita Split

La modalita Split taglia file MKV HEVC e AVC in modo frame-perfect, preservando il bitstream originale byte-per-byte dove possibile. Quando il punto di taglio non cade su un keyframe, viene ricodificato solo il GOP iniziale del segmento; il resto del video viene copiato intatto. Audio e sottotitoli vengono rimuxati senza re-encoding.

La CLI usa sempre `--mode split`. Il parametro `--source` puo' indicare un singolo file MKV oppure una cartella:

- **File**: esegue lo split sul singolo file.
- **Cartella**: esegue batch su tutti gli MKV trovati nella cartella, applicando lo stesso pattern e le stesse opzioni a ogni file. La ricerca segue `--recursive`/`--no-recursive` e `--extensions`.

`--source-raw` e' disponibile solo quando `--source` punta a un singolo file.

### WebUI Split

In Split la WebUI e' dedicata al taglio MKV e non mostra i comandi Remux. La schermata mantiene tre aree:

- **Input**: lista dei file MKV preparati dallo scan. La sorgente file/cartella e le opzioni di taglio si configurano con F2.
- **Dettaglio del file**: riepilogo del file selezionato, segmenti calcolati e impostazioni di taglio applicate.
- **Log**: output operativo dello scan e dello split.

#### Tasti rapidi Split

| Tasto | Azione |
|-------|--------|
| F2 | Apre configurazione Split |
| F5 | Scan input |
| F10 | Split tutti i file preparati |
| F12 | Richiede stop dell'operazione corrente |

I tasti Remux per analisi, skip e merge selezionato non fanno parte del workflow Split.

#### Menu Split

- **File**: Configurazione Split (F2)
- **Azioni**: Scan input (F5), Split tutti (F10), Stop (F12)
- **Impostazioni**: Percorsi tool
- **Vista**: Pipeline (mostra la sequenza di operazioni Split in base alla configurazione corrente)
- **Tema**: cambio tema grafico
- **Aiuto**: Info

#### Configurazione Split

La configurazione Split espone solo le opzioni essenziali: sorgente file o cartella, cartella output, pattern capitoli, range espliciti, split-at, trim start/end, chapters-each, template output, snap, force e dry-run. Quando la sorgente e' una cartella, la scansione usa le stesse opzioni per tutti i file trovati.

### CLI Split

La CLI Split usa sempre `--mode split`. I parametri Remux come `--target-language`, `--framesync`, `--deep-analysis`, filtri codec e post-processing audio non si applicano a questa modalita.

```bash
RemuxForge.Cli --mode split --source "INPUT.mkv" [modalita di taglio] [opzioni]
```

Va scelta **una** modalita di taglio fra quelle elencate sotto.

#### Parametri obbligatori Split

| Short | Long | Descrizione |
|-------|------|-------------|
| | --mode | Deve essere `split` |
| | --source | File MKV o cartella sorgente |

#### Parametri principali Split

| Short | Long | Descrizione |
|-------|------|-------------|
| | --output-dir | Cartella output |
| | --source-raw | File sorgente PTS alternativo, solo single-file |
| | --output-template | Template output custom |
| | --snap | `off`, `before`, `after`, `nearest` |
| | --force | Sovrascrive output esistenti |
| -n | --dry-run | Mostra i segmenti senza scrivere file |

### Modalita di taglio Split

#### `--pattern "5,5,5,6"`

Raggruppa i capitoli del file in segmenti secondo il pattern. La somma dei numeri deve coincidere con il numero di capitoli.

```bash
RemuxForge.Cli --mode split --source "bleach_disc1.mkv" --pattern "5,5,5,6"
```

Su cartella, lo stesso pattern viene applicato a ogni MKV trovato:

```bash
RemuxForge.Cli --mode split --source "D:\\Dischi" --pattern "5,5,5,6" --output-dir "D:\\Output"
```

#### `--ranges "T1-T2,T3-T4,..."`

Definisce intervalli espliciti. `T` puo' essere in formato `HH:MM:SS.mmm`, `MM:SS.mmm`, secondi decimali, `f<numero>` per frame index, oppure `END`.

Esempi:

```bash
RemuxForge.Cli --mode split --source "input.mkv" --ranges "00:00:00-00:21:40,00:21:40-00:43:20,00:43:20-END"
RemuxForge.Cli --mode split --source "input.mkv" --ranges "f0-f29970,f29970-END"
```

Se viene passato un unico intervallo, parte la modalita trim: l'output viene scritto accanto al file di input con suffisso `_trimmed`, salvo `--output-dir` o template custom.

#### `--split-at "T1,T2,..."`

Scorciatoia per dividere in `N+1` segmenti ai timecode indicati. Duplicati o punti fuori dalla durata del file generano errore.

```bash
RemuxForge.Cli --mode split --source "concert.mkv" --split-at "00:21:40,00:43:20"
```

Equivale a:

```bash
RemuxForge.Cli --mode split --source "concert.mkv" --ranges "0-00:21:40,00:21:40-00:43:20,00:43:20-END"
```

#### `--trim-start T` e `--trim-end T`

Scorciatoie per il trim. Sono combinabili fra loro.

```bash
RemuxForge.Cli --mode split --source "input.mkv" --trim-start 00:01:30
RemuxForge.Cli --mode split --source "input.mkv" --trim-end 00:45:00
RemuxForge.Cli --mode split --source "input.mkv" --trim-start 00:01:30 --trim-end 00:45:00
```

#### `--chapters-each`

Crea un segmento per ogni capitolo del file sorgente. Richiede che il file abbia capitoli.

```bash
RemuxForge.Cli --mode split --source "film.mkv" --chapters-each
```

### Nomi dei file di output Split

Il default dipende dalla modalita:

| Modalita | Template default |
|----------|------------------|
| `--pattern` | `{source_name}.part{n:02d}.mkv` |
| `--ranges` con piu' segmenti | `{source_name}.part{n:02d}.mkv` |
| `--ranges` / `--split-at` | `{source_name}.part{n:02d}.mkv` |
| `--trim-start` / `--trim-end` | `{source_name}_trimmed.mkv` accanto all'input |
| `--chapters-each` | `{source_name}.ch{n:02d}.mkv` |

Si puo' sovrascrivere con:

- `--output-template "..."` per un template custom. Variabili disponibili: `{source_name}`, `{n}`, `{n:02d}`, `{n+213}`, `{n+213:03d}`, `{n-1}`, `{start}`, `{end}`, `{chapter_name}`.

Esempi:

```bash
--output-template "{source_name}.part{n:02d}.mkv"
--output-template "Bleach.S12E{n+213:03d}.mkv"
--output-template "{chapter_name}.mkv"
```

### Altre opzioni Split

| Opzione | Descrizione |
|---------|-------------|
| `--source FILE\|DIR` | File MKV o cartella sorgente. Obbligatorio in modalita Split. |
| `--source-raw FILE` | Usa un altro file per estrarre i PTS, utile quando l'input e' un re-encode di un sorgente VFR. Solo single-file. |
| `--output-dir DIR` | Cartella di destinazione. Default: cartella del file di input. |
| `--snap off\|before\|after\|nearest` | Se attivo, sposta lo start al keyframe piu' vicino secondo la direzione scelta, evitando la ricodifica del GOP iniziale. Default `off` frame-perfect. |
| `--force` | Sovrascrive file di output esistenti. Senza `--force`, i segmenti gia' presenti vengono saltati. |
| `--log FILE` | Duplica lo stdout su file, in append con header timestamp. |
| `--dry-run` / `-n` | Stampa segmenti e azioni senza eseguire il taglio. |
| `--recursive` / `--no-recursive` | Controlla la scansione delle sottocartelle quando `--source` e' una cartella. |
| `--extensions` | Estensioni cercate in batch, default `mkv`. |

### Esempi Split

Split di un MKV multi-episodio con pattern di capitoli:

```bash
RemuxForge.Cli --mode split --source "disc1.mkv" --pattern "5,5,5,6"
```

Trim dei primi 90 secondi:

```bash
RemuxForge.Cli --mode split --source "input.mkv" --trim-start 00:01:30
```

Split manuale in tre parti uguali per un video di 60 minuti:

```bash
RemuxForge.Cli --mode split --source "concert.mkv" --split-at "20:00,40:00"
```

Estrazione di ogni singolo capitolo:

```bash
RemuxForge.Cli --mode split --source "film.mkv" --chapters-each --output-template "Film.ch{n:02d}.mkv"
```

Taglio veloce allineato al keyframe piu' vicino, senza ricodifica:

```bash
RemuxForge.Cli --mode split --source "source.mkv" --ranges "00:02:00-00:05:00" --snap nearest
```

Batch su cartella usando lo stesso pattern per ogni disco/file:

```bash
RemuxForge.Cli --mode split --source "D:\\Dischi" --pattern "5,5,5,6" --output-template "Serie.{source_name}.part{n:02d}.mkv" --output-dir "D:\\Episodi"
```

### Come funziona Split

1. `mkvextract timestamps_v2` estrae i PTS del sorgente, preservando i timecode anche su VFR.
2. `mkvextract` produce il bitstream raw dell'unico flusso video.
3. `ffprobe` mappa posizione e dimensione di ogni frame nel bitstream.
4. Per ogni segmento:
   - se lo start coincide con un keyframe, copia direttamente il range di byte dal raw;
   - altrimenti, ricodifica solo il GOP iniziale fino al keyframe successivo con gli stessi `pix_fmt` e parametri di colore del sorgente, reinserisce i parameter set originali e concatena il resto byte-per-byte.
5. `mkvmerge` rimuxa video, tutte le tracce audio, tutti i sottotitoli e capitoli in un nuovo MKV con timecode V2. Nel fast path non-FLAC, i capitoli vengono applicati dopo lo split con `mkvpropedit`.

I capitoli con nome generico tipo `Chapter 15` vengono rinumerati da 1 all'interno di ogni segmento; i nomi significativi, per esempio `Opening` o `Act 1`, vengono preservati.

Il risultato e' un taglio frame-perfect senza ricodifica dell'intero file, a costo di una ricodifica minima del solo GOP iniziale quando il cut non cade su un keyframe.

### Approfondimento: perche' serve una pipeline dedicata

Il caso d'uso originale era splittare un file VFR in piu' parti preservando al frame l'allineamento con i capitoli originali. Su CFR il problema e' piu' semplice, ma il VFR rompe molte strade ovvie.

**PTS non affidabili dopo un re-encode.** Molte pipeline di re-encode non preservano i PTS del sorgente e generano timestamp uniformi che non riflettono il timing VFR reale, per esempio soft telecine con frame da 33 ms e 50 ms. Se i confini di taglio vengono calcolati sui PTS del file ricodificato, i cut cadono nel punto sbagliato. La soluzione e' estrarre i PTS originali dal sorgente e riapplicarli tramite `mkvmerge --timestamps`, quando i frame sono in corrispondenza 1:1. Da qui il flag `--source-raw`.

**ffmpeg non gestisce bene VFR su MKV in stream copy.** Quando fa `-c:v copy`, il muxer Matroska di ffmpeg puo' riscrivere timestamp come se fossero CFR, distruggendo il VFR. Anche opzioni come `-fps_mode passthrough`, `-copyts` e `-muxpreload` non risolvono in modo affidabile questo scenario.

**mkvmerge taglia solo ai keyframe.** `mkvmerge` gestisce il VFR nativamente, ma `--split parts` taglia ai keyframe video. In MKV i frame sono raggruppati in cluster e mkvmerge crea nuovi cluster in corrispondenza dei keyframe per permettere il seeking. Se il chapter cade tra due keyframe, mkvmerge include frame extra fino al keyframe successivo.

**Non si puo' iniziare un file video da un non-keyframe senza ricodifica.** HEVC e H.264 usano predizione inter-frame: frame P e B dipendono dai frame precedenti, quindi un file video deve iniziare da un keyframe. Con HEVC open-GOP, i keyframe possono essere CRA e non IDR; dopo un CRA esistono frame RASL che referenziano frame precedenti e possono essere scartati dal decoder se il segmento parte nel punto sbagliato.

La soluzione di Split bypassa ffmpeg e mkvmerge per il taglio video vero e proprio e lavora sul bitstream grezzo:

1. estrae traccia video raw e `timestamps_v2`;
2. mappa byte offset e dimensione di ogni frame;
3. calcola il range frame dai PTS del sorgente;
4. copia byte-per-byte quando lo start cade su keyframe;
5. ricodifica solo i pochi frame dal taglio al keyframe successivo quando serve;
6. concatena a livello binario e rimuxa con `mkvmerge` applicando i timecode VFR dal sorgente.

I frame al confine subiscono una ricodifica ad alta qualita'; tutto il resto resta copia byte-per-byte. La ricodifica usa `keyint=1` e `bframes=0`, cioe' ogni frame diventa un I-frame indipendente, per permettere il taglio a qualunque posizione e ridurre i problemi con CRA/RASL.

Nota: con sorgenti HEVC open-GOP, alcuni decoder possono comunque segnalare warning su reference mancanti attorno ai CRA/RASL. Il tool verifica il conteggio frame e mantiene il resto video byte-per-byte, ma non trasforma un GOP HEVC open-GOP in un GOP chiuso.

## Configurazione (appsettings.json)

Tutte le impostazioni persistenti sono salvate in `.remux-forge/appsettings.json`. Il file viene creato automaticamente con valori di default. La cartella `.remux-forge` si trova nella directory dell'eseguibile, o nel percorso indicato da `REMUXFORGE_DATA_DIR`.

I nuovi campi aggiunti in aggiornamenti successivi vengono integrati automaticamente senza sovrascrivere i valori utente esistenti.

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

**Sezioni:**

- **Tools**: percorsi di mkvmerge, mkvextract, mkvpropedit, ffmpeg, ffprobe, mediainfo e cartella temporanei. Rilevati automaticamente all'avvio, modificabili dal menu Impostazioni > Percorsi tool
- **Flac**: livello compressione FLAC (0 = veloce, 12 = massima compressione)
- **Opus.Bitrate**: bitrate Opus in kbps per layout canali (range: 64-768)
- **Aac.Bitrate**: bitrate AAC in kbps per layout canali (range: 32-1536)
- **Ui.Theme**: tema grafico selezionato. Temi validi: `dark`, `nord`, `dos-blue`, `matrix`, `cyberpunk`, `solarized-dark`, `solarized-light`, `cybergum`, `everforest`
- **Ui.LastMode**: ultima modalita selezionata nella WebUI (`remux` o `split`)
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

## Nota sull'uso di LLM

Durante lo sviluppo di RemuxForge e' stato usato supporto basato su LLM per:

- integrazione e aggiornamento della documentazione README
- supporto al design e alla rifinitura della WebUI
- assistenza in eventuali refactor estesi quando necessari

## Buy me a coffee!

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/simonefil)
