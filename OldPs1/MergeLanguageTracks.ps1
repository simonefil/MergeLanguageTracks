<#
.SYNOPSIS
    Unisce tracce audio e sottotitoli da file MKV in lingue diverse.

.DESCRIPTION
    Dato una cartella sorgente con episodi TV e una cartella lingua con gli stessi
    episodi in un'altra lingua, questo script estrae le tracce audio e sottotitoli
    dalla cartella lingua e le unisce agli episodi sorgente.

    Supporta sincronizzazione automatica tramite audio fingerprinting.

.PARAMETER SourceFolder
    Alias: -s
    Cartella contenente i file MKV sorgente (gli episodi principali).

.PARAMETER LanguageFolder
    Alias: -l
    Cartella contenente i file MKV con le tracce nella lingua alternativa.

.PARAMETER TargetLanguage
    Alias: -t
    Codice lingua ISO 639-2 da estrarre (es. "ita", "eng", "jpn", "ger").

.PARAMETER MatchPattern
    Alias: -m
    Pattern regex per estrarre l'identificativo episodio per il matching.
    Default: "S(\d+)E(\d+)" per il formato standard SxxExx.

.PARAMETER OutputMode
    Alias: -o
    "Overwrite" per sovrascrivere i file sorgente, "Destination" per salvare in cartella separata.

.PARAMETER DestinationFolder
    Alias: -d
    Cartella di output quando OutputMode e' "Destination".

.PARAMETER AudioDelay
    Alias: -ad
    Delay manuale in millisecondi per le tracce audio. Default: 0.
    Se AutoSync e' attivo, viene sommato al delay calcolato automaticamente.

.PARAMETER SubtitleDelay
    Alias: -sd
    Delay manuale in millisecondi per i sottotitoli. Default: 0.
    Se AutoSync e' attivo, viene sommato al delay calcolato automaticamente.

.PARAMETER AutoSync
    Alias: -as
    Abilita sincronizzazione automatica tramite audio fingerprinting.
    Analizza silenzi e picchi audio per calcolare il delay ottimale.

.PARAMETER AudioCodec
    Alias: -ac
    Filtra le tracce audio per codec (es. "E-AC-3", "AAC", "AC3", "DTS").

.PARAMETER SubOnly
    Alias: -so
    Importa solo sottotitoli, ignora completamente le tracce audio.

.PARAMETER KeepSourceAudioLangs
    Alias: -ksa
    Array di codici lingua da MANTENERE nelle tracce audio del file sorgente.
    Le altre lingue vengono rimosse.

.PARAMETER KeepSourceSubtitleLangs
    Alias: -kss
    Array di codici lingua da MANTENERE nei sottotitoli del file sorgente.
    Le altre lingue vengono rimosse.

.PARAMETER MkvMergePath
    Alias: -mkv
    Percorso dell'eseguibile mkvmerge. Default: cerca nel PATH.

.PARAMETER MkvExtractPath
    Alias: -mkvx
    Percorso dell'eseguibile mkvextract. Default: cerca nel PATH.

.PARAMETER ToolsFolder
    Alias: -tools
    Cartella per i tool scaricati (ffmpeg). Default: cartella dello script.

.PARAMETER Recursive
    Alias: -r
    Cerca ricorsivamente nelle sottocartelle. Default: $true.

.PARAMETER DryRun
    Alias: -dry, -n
    Mostra cosa verrebbe fatto senza eseguire nulla.
    Con AutoSync, mostra comunque il delay calcolato.

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\Serie\EN" -l "D:\Serie\IT" -t ita -d "D:\Output" -as

    Unisce le tracce italiane agli episodi inglesi con sync automatico.
    I file vengono salvati in D:\Output.

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -d "D:\Out" -as -DryRun

    Mostra cosa verrebbe fatto (dry run) incluso il delay calcolato.

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -ac "E-AC-3" -d "D:\Out" -as

    Importa solo tracce audio E-AC-3 (Dolby Digital Plus) in italiano.

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -so -d "D:\Out" -as

    Importa SOLO sottotitoli italiani (niente audio).

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -ksa eng -d "D:\Out" -as

    Aggiunge italiano e RIMUOVE tutte le tracce audio tranne inglese dal sorgente.
    Utile per sostituire una traccia italiana esistente con una nuova.

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -ksa eng,jpn -kss eng -d "D:\Out"

    Mantiene solo audio eng/jpn e sub eng dal sorgente, aggiunge ita dal language file.

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -m "(\d+)x(\d+)" -d "D:\Out"

    Usa pattern custom per matching (es. "1x01" invece di "S01E01").

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -ad -500 -sd -500 -d "D:\Out"

    Applica delay manuale di -500ms a audio e sottotitoli.

.EXAMPLE
    .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -o Overwrite -as

    ATTENZIONE: Sovrascrive i file originali! Usare con cautela.

.NOTES
    Requisiti:
    - MKVToolNix (mkvmerge, mkvextract) nel PATH o specificato con -mkv/-mkvx
    - ffmpeg/ffprobe per AutoSync (scaricato automaticamente se non presente)

    Funzionamento AutoSync:
    L'algoritmo analizza i primi 5 minuti di audio di entrambi i file,
    rileva silenzi e picchi di volume (transients), e trova l'offset
    che massimizza la corrispondenza tra i pattern audio.
    Funziona anche con lingue diverse perche' musica, effetti sonori
    e silenzi sono identici tra versioni doppiate.

    Precisione: ~1ms (ricerca in 3 fasi: coarse 500ms, fine 10ms, ultra-fine 1ms)

.LINK
    https://mkvtoolnix.download/
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [Alias("h", "?")]
    [switch]$Help,

    [Parameter(Mandatory = $false)]
    [Alias("s")]
    [string]$SourceFolder,

    [Parameter(Mandatory = $false)]
    [Alias("l")]
    [string]$LanguageFolder,

    [Parameter(Mandatory = $false)]
    [Alias("t")]
    [string]$TargetLanguage,

    [Parameter(Mandatory = $false)]
    [Alias("m")]
    [string]$MatchPattern = "S(\d+)E(\d+)",

    [Parameter(Mandatory = $false)]
    [Alias("o")]
    [ValidateSet("Overwrite", "Destination")]
    [string]$OutputMode = "Destination",

    [Parameter(Mandatory = $false)]
    [Alias("d")]
    [string]$DestinationFolder,

    [Parameter(Mandatory = $false)]
    [Alias("ad")]
    [int]$AudioDelay = 0,

    [Parameter(Mandatory = $false)]
    [Alias("sd")]
    [int]$SubtitleDelay = 0,

    [Parameter(Mandatory = $false)]
    [Alias("as")]
    [switch]$AutoSync,

    [Parameter(Mandatory = $false)]
    [Alias("ac")]
    [string]$AudioCodec,

    [Parameter(Mandatory = $false)]
    [Alias("so")]
    [switch]$SubOnly,

    [Parameter(Mandatory = $false)]
    [Alias("ksa")]
    [string[]]$KeepSourceAudioLangs,

    [Parameter(Mandatory = $false)]
    [Alias("kss")]
    [string[]]$KeepSourceSubtitleLangs,

    [Parameter(Mandatory = $false)]
    [Alias("mkv")]
    [string]$MkvMergePath = "mkvmerge",

    [Parameter(Mandatory = $false)]
    [Alias("mkvx")]
    [string]$MkvExtractPath = "mkvextract",

    [Parameter(Mandatory = $false)]
    [Alias("tools")]
    [string]$ToolsFolder,

    [Parameter(Mandatory = $false)]
    [Alias("r")]
    [switch]$Recursive = $true,

    [Parameter(Mandatory = $false)]
    [Alias("dry", "n")]
    [switch]$DryRun
)

#region Help

if ($Help) {
    $helpText = @"

USAGE: MergeLanguageTracks.ps1 [OPTIONS]

Unisce tracce audio e sottotitoli da file MKV in lingue diverse.
Supporta sincronizzazione automatica tramite audio fingerprinting.

OPZIONI OBBLIGATORIE:
  -s,  -SourceFolder <path>      Cartella con i file MKV sorgente
  -l,  -LanguageFolder <path>    Cartella con i file MKV nella lingua da importare
  -t,  -TargetLanguage <code>    Codice lingua ISO 639-2 (es: ita, eng, jpn, ger)

OPZIONI OUTPUT:
  -d,  -DestinationFolder <path> Cartella di output (default: richiesta)
  -o,  -OutputMode <mode>        "Destination" (default) o "Overwrite"

OPZIONI SYNC:
  -as, -AutoSync                 Abilita sync automatico (audio fingerprinting)
  -ad, -AudioDelay <ms>          Delay manuale audio in ms (sommato ad auto se -as)
  -sd, -SubtitleDelay <ms>       Delay manuale sottotitoli in ms

OPZIONI FILTRO:
  -ac, -AudioCodec <codec>       Importa solo audio con codec specifico (es: E-AC-3, DTS)
  -so, -SubOnly                  Importa solo sottotitoli (ignora audio)
  -ksa, -KeepSourceAudioLangs    Lingue audio da mantenere nel sorgente (es: eng,jpn)
  -kss, -KeepSourceSubtitleLangs Lingue sub da mantenere nel sorgente

OPZIONI MATCHING:
  -m,  -MatchPattern <regex>     Pattern per matching episodi (default: S(\d+)E(\d+))
  -r,  -Recursive                Cerca ricorsivamente nelle sottocartelle (default: true)

OPZIONI TOOL:
  -mkv,  -MkvMergePath <path>    Percorso mkvmerge (default: cerca in PATH)
  -mkvx, -MkvExtractPath <path>  Percorso mkvextract (default: cerca in PATH)
  -tools, -ToolsFolder <path>    Cartella per tool scaricati (ffmpeg)

ALTRE OPZIONI:
  -DryRun, -dry, -n              Mostra cosa verrebbe fatto senza eseguire
  -h, -Help                      Mostra questo messaggio

CODEC AUDIO (per -ac):
  Dolby:
    AC-3        Dolby Digital (DD, AC3)
    E-AC-3      Dolby Digital Plus (DD+, EAC3, include Atmos lossy)
    TrueHD      Dolby TrueHD (include Atmos lossless)
    MLP         Meridian Lossless Packing (base di TrueHD)

  DTS:
    DTS         DTS Core / Digital Surround
    DTS-HD MA   DTS-HD Master Audio (lossless)
    DTS-HD HR   DTS-HD High Resolution
    DTS-ES      DTS Extended Surround
    DTS:X       DTS:X (object-based, estensione di DTS-HD MA)

  Lossless:
    FLAC        Free Lossless Audio Codec
    PCM         Audio non compresso (LPCM, WAV)
    ALAC        Apple Lossless

  Lossy:
    AAC         Advanced Audio Coding (LC, HE-AAC, HE-AACv2)
    MP3         MPEG Audio Layer 3
    MP2         MPEG Audio Layer 2
    Opus        Opus (WebM, alta qualita' a basso bitrate)
    Vorbis      Ogg Vorbis

  IMPORTANTE: il matching e' ESATTO, non parziale!
        -ac "DTS"      -> matcha SOLO DTS core, NON DTS-HD MA
        -ac "DTS-HD"   -> matcha DTS-HD MA e DTS-HD HR
        -ac "DTS-HDMA" -> matcha SOLO DTS-HD Master Audio
        -ac "ATMOS"    -> matcha TrueHD e E-AC-3 (entrambi possono avere Atmos)

  Alias comuni accettati:
        EAC3, DDP, DD+ -> E-AC-3
        AC3, DD        -> AC-3
        DTSX           -> DTS:X
        LPCM, WAV      -> PCM

ESEMPI:
  # Unisci tracce italiane con auto-sync
  .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -d "D:\Out" -as

  # Dry run (mostra cosa farebbe senza eseguire)
  .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -d "D:\Out" -as -DryRun

  # Solo audio E-AC-3 italiano
  .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -ac "E-AC-3" -d "D:\Out" -as

  # Solo sottotitoli (no audio)
  .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -so -d "D:\Out" -as

  # Sostituisci traccia ita esistente (rimuovi vecchia, aggiungi nuova)
  .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -ksa eng -d "D:\Out" -as

  # Mantieni solo eng/jpn audio e eng sub dal sorgente
  .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -ksa eng,jpn -kss eng -d "D:\Out"

  # Pattern custom (1x01 invece di S01E01)
  .\MergeLanguageTracks.ps1 -s "D:\EN" -l "D:\IT" -t ita -m "(\d+)x(\d+)" -d "D:\Out"

CODICI LINGUA (ISO 639-2):
  Comuni: ita, eng, jpn, ger/deu, fra/fre, spa, por, rus, chi/zho, kor
  Altri:  ara, hin, pol, tur, nld/dut, swe, nor, dan, fin, hun, ces/cze
  Speciali: und (undefined), mul (multiple), zxx (no language)

REQUISITI:
  - MKVToolNix (mkvmerge, mkvextract) nel PATH
  - ffmpeg/ffprobe per AutoSync (scaricato automaticamente se mancante)

NOTE:
  AutoSync analizza i primi 5 min di audio, rileva silenzi e picchi di volume,
  e trova l'offset ottimale. Funziona anche con lingue diverse perche' musica,
  effetti sonori e silenzi sono identici tra versioni doppiate.
  Precisione: ~1ms (ricerca in 3 fasi: 500ms -> 10ms -> 1ms)

"@
    Write-Host $helpText
    exit 0
}

#endregion

#region Normalizzazione Path e Setup

# Verifica parametri obbligatori quando non e' help
if (-not $SourceFolder -or -not $LanguageFolder -or -not $TargetLanguage) {
    Write-Host "Errore: parametri obbligatori mancanti." -ForegroundColor Red
    Write-Host "Uso: .\MergeLanguageTracks.ps1 -s <source> -l <lang> -t <lingua> -d <dest> [-as] [-DryRun]" -ForegroundColor Yellow
    Write-Host "     Usa -Help per vedere tutte le opzioni." -ForegroundColor DarkGray
    exit 1
}

# Valida che le cartelle esistano
if (-not (Test-Path $SourceFolder -PathType Container)) {
    Write-Host "Errore: cartella sorgente non trovata: $SourceFolder" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $LanguageFolder -PathType Container)) {
    Write-Host "Errore: cartella lingua non trovata: $LanguageFolder" -ForegroundColor Red
    exit 1
}

# Valida formato lingua
if ($TargetLanguage -notmatch "^[a-z]{2,3}$") {
    Write-Host "Errore: lingua non valida '$TargetLanguage'. Usa codice ISO 639-2 (es: ita, eng, jpn)" -ForegroundColor Red
    exit 1
}

$ScriptFolder = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ScriptFolder)) {
    $ScriptFolder = Get-Location
}

if ([string]::IsNullOrWhiteSpace($ToolsFolder)) {
    $ToolsFolder = Join-Path $ScriptFolder "tools"
}

function Get-NormalizedPath {
    # Normalizza un path risolvendo riferimenti relativi e rimuovendo slash finali
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    $resolved = [System.IO.Path]::GetFullPath($Path)
    return $resolved.TrimEnd('\', '/')
}

$SourceFolder = Get-NormalizedPath $SourceFolder
$LanguageFolder = Get-NormalizedPath $LanguageFolder
if (-not [string]::IsNullOrWhiteSpace($DestinationFolder)) {
    $DestinationFolder = Get-NormalizedPath $DestinationFolder
}

#endregion

#region Funzioni Download Tool

function Get-FfmpegExecutables {
    <#
    .SYNOPSIS
        Restituisce i path di ffmpeg e ffprobe, scaricandoli se necessario.
    #>
    param([string]$ToolsFolder)

    $ffmpegPath = Join-Path $ToolsFolder "ffmpeg.exe"
    $ffprobePath = Join-Path $ToolsFolder "ffprobe.exe"

    # Controlla se gia' presenti nella cartella tools
    if ((Test-Path $ffmpegPath) -and (Test-Path $ffprobePath)) {
        return @{ Ffmpeg = $ffmpegPath; Ffprobe = $ffprobePath }
    }

    # Controlla se presenti nel PATH
    $ffmpegInPath = Get-Command "ffmpeg" -ErrorAction SilentlyContinue
    $ffprobeInPath = Get-Command "ffprobe" -ErrorAction SilentlyContinue
    if ($ffmpegInPath -and $ffprobeInPath) {
        return @{ Ffmpeg = $ffmpegInPath.Source; Ffprobe = $ffprobeInPath.Source }
    }

    # Scarica ffmpeg
    Write-Host "`n  Download ffmpeg in corso..." -ForegroundColor Yellow

    if (-not (Test-Path $ToolsFolder)) {
        [System.IO.Directory]::CreateDirectory($ToolsFolder) | Out-Null
    }

    try {
        # Usa build essentials da gyan.dev (piu' piccola, contiene ffmpeg + ffprobe)
        $ffmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
        $zipPath = Join-Path $ToolsFolder "ffmpeg.zip"
        $extractPath = Join-Path $ToolsFolder "ffmpeg_temp"

        Write-Host "  Download da: $ffmpegUrl" -ForegroundColor DarkGray

        # Download usando .NET WebClient
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($ffmpegUrl, $zipPath)

        Write-Host "  Estrazione in corso..." -ForegroundColor DarkGray

        # Estrai
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        if (Test-Path $extractPath) {
            [System.IO.Directory]::Delete($extractPath, $true)
        }
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extractPath)

        # Trova e copia gli eseguibili nella root di tools
        $ffmpegExe = Get-ChildItem -Path $extractPath -Filter "ffmpeg.exe" -Recurse | Select-Object -First 1
        $ffprobeExe = Get-ChildItem -Path $extractPath -Filter "ffprobe.exe" -Recurse | Select-Object -First 1

        if ($ffmpegExe -and $ffprobeExe) {
            [System.IO.File]::Copy($ffmpegExe.FullName, $ffmpegPath, $true)
            [System.IO.File]::Copy($ffprobeExe.FullName, $ffprobePath, $true)
            Write-Host "  ffmpeg scaricato in: $ToolsFolder" -ForegroundColor Green
        }

        # Pulizia file temporanei
        [System.IO.File]::Delete($zipPath)
        [System.IO.Directory]::Delete($extractPath, $true)

        if ((Test-Path $ffmpegPath) -and (Test-Path $ffprobePath)) {
            return @{ Ffmpeg = $ffmpegPath; Ffprobe = $ffprobePath }
        }
    }
    catch {
        Write-Warning "Impossibile scaricare ffmpeg: $_"
        Write-Warning "Scaricalo manualmente da https://www.gyan.dev/ffmpeg/builds/"
        return $null
    }

    return $null
}

#endregion

#region Validazione Parametri

if ($OutputMode -eq "Destination" -and [string]::IsNullOrWhiteSpace($DestinationFolder)) {
    throw "DestinationFolder e' obbligatorio quando OutputMode e' 'Destination'"
}

if ($OutputMode -eq "Destination" -and -not (Test-Path $DestinationFolder -PathType Container)) {
    Write-Host "Creazione cartella destinazione: $DestinationFolder" -ForegroundColor Yellow
    New-Item -Path $DestinationFolder -ItemType Directory -Force | Out-Null
}

# Verifica mkvmerge
try {
    $null = & $MkvMergePath --version 2>&1
    Write-Host "Trovato mkvmerge: $MkvMergePath" -ForegroundColor Green
}
catch {
    throw "mkvmerge non trovato. Installa MKVToolNix o specifica -MkvMergePath/-mkv"
}

# Verifica mkvextract (necessario per AutoSync)
if ($AutoSync) {
    try {
        $null = & $MkvExtractPath --version 2>&1
        Write-Host "Trovato mkvextract: $MkvExtractPath" -ForegroundColor Green
    }
    catch {
        throw "mkvextract non trovato. Necessario per AutoSync. Installa MKVToolNix o specifica -MkvExtractPath/-mkvx"
    }

    # Ottieni o scarica ffmpeg (necessario per analisi audio)
    $ffmpegPaths = Get-FfmpegExecutables -ToolsFolder $ToolsFolder
    if (-not $ffmpegPaths) {
        throw "ffmpeg/ffprobe non trovati e impossibile scaricarli. Installali manualmente."
    }
    Write-Host "Trovato ffmpeg: $($ffmpegPaths.Ffmpeg)" -ForegroundColor Green
    Write-Host "Trovato ffprobe: $($ffmpegPaths.Ffprobe)" -ForegroundColor Green

    # Salva i path in variabili d'ambiente per uso nelle funzioni
    $env:AUTOSYNC_FFMPEG_PATH = $ffmpegPaths.Ffmpeg
    $env:AUTOSYNC_FFPROBE_PATH = $ffmpegPaths.Ffprobe
}

#endregion

#region Mappatura Codec e Lingue

# Mappa codec: alias utente -> pattern esatto mkvmerge
# Il valore e' un array di pattern che matchano ESATTAMENTE quel codec
$script:CodecMap = @{
    # Dolby
    "AC3"       = @("AC-3")
    "AC-3"      = @("AC-3")
    "DD"        = @("AC-3")
    "EAC3"      = @("E-AC-3")
    "E-AC-3"    = @("E-AC-3")
    "DD+"       = @("E-AC-3")
    "DDP"       = @("E-AC-3")
    "TRUEHD"    = @("TrueHD")
    "ATMOS"     = @("TrueHD", "E-AC-3")  # Atmos puo' essere in TrueHD o E-AC-3
    "MLP"       = @("MLP")

    # DTS - IMPORTANTE: matching esatto per distinguere DTS core da DTS-HD
    "DTS"       = @("DTS")  # Solo DTS core, NON DTS-HD
    "DTS-HD"    = @("DTS-HD Master Audio", "DTS-HD High Resolution")
    "DTS-HD MA" = @("DTS-HD Master Audio")
    "DTS-HDMA"  = @("DTS-HD Master Audio")
    "DTS-HD HR" = @("DTS-HD High Resolution")
    "DTS-HDHR"  = @("DTS-HD High Resolution")
    "DTS-ES"    = @("DTS-ES")
    "DTS:X"     = @("DTS:X")
    "DTSX"      = @("DTS:X")

    # Lossless
    "FLAC"      = @("FLAC")
    "PCM"       = @("PCM")
    "LPCM"      = @("PCM")
    "WAV"       = @("PCM")
    "ALAC"      = @("ALAC")

    # Lossy
    "AAC"       = @("AAC")
    "HE-AAC"    = @("AAC")  # mkvmerge li riporta come AAC
    "MP3"       = @("MPEG Audio", "MP3")
    "MP2"       = @("MP2", "MPEG Audio Layer 2")
    "OPUS"      = @("Opus")
    "VORBIS"    = @("Vorbis")
}


function Get-CodecPatterns {
    # Restituisce i pattern esatti per un codec specificato dall'utente
    param([string]$UserCodec)

    $normalized = $UserCodec.ToUpper().Trim()

    if ($script:CodecMap.ContainsKey($normalized)) {
        return $script:CodecMap[$normalized]
    }

    # Prova anche senza trattini/spazi
    $normalized = $normalized -replace '[\s\-:]', ''
    foreach ($key in $script:CodecMap.Keys) {
        $keyNorm = $key -replace '[\s\-:]', ''
        if ($keyNorm -eq $normalized) {
            return $script:CodecMap[$key]
        }
    }

    return $null
}

# Lista completa codici ISO 639-2 (bibliographic + terminology)
$script:ValidLanguages = @(
    # A
    "aar", "abk", "ace", "ach", "ada", "ady", "afa", "afh", "afr", "ain",
    "aka", "akk", "alb", "sqi", "ale", "alg", "alt", "amh", "ang", "anp",
    "apa", "ara", "arc", "arg", "arm", "hye", "arn", "arp", "art", "arw",
    "asm", "ast", "ath", "aus", "ava", "ave", "awa", "aym", "aze",
    # B
    "bad", "bai", "bak", "bal", "bam", "ban", "baq", "eus", "bas", "bat",
    "bej", "bel", "bem", "ben", "ber", "bho", "bih", "bik", "bin", "bis",
    "bla", "bnt", "bod", "tib", "bos", "bra", "bre", "btk", "bua", "bug",
    "bul", "bur", "mya", "byn",
    # C
    "cad", "cai", "car", "cat", "cau", "ceb", "cel", "ces", "cze", "cha",
    "chb", "che", "chg", "chi", "zho", "chk", "chm", "chn", "cho", "chp",
    "chr", "chu", "chv", "chy", "cmc", "cnr", "cop", "cor", "cos", "cpe",
    "cpf", "cpp", "cre", "crh", "crp", "csb", "cus", "cym", "wel",
    # D
    "dak", "dan", "dar", "day", "del", "den", "deu", "ger", "dgr", "din",
    "div", "doi", "dra", "dsb", "dua", "dum", "dut", "nld", "dyu", "dzo",
    # E
    "efi", "egy", "eka", "ell", "gre", "elx", "eng", "enm", "epo", "est",
    "ewe", "ewo",
    # F
    "fan", "fao", "fas", "per", "fat", "fij", "fil", "fin", "fiu", "fon",
    "fra", "fre", "frm", "fro", "frr", "frs", "fry", "ful", "fur",
    # G
    "gaa", "gay", "gba", "gem", "geo", "kat", "gez", "gil", "gla", "gle",
    "glg", "glv", "gmh", "goh", "gon", "gor", "got", "grb", "grc", "grn",
    "gsw", "guj", "gwi",
    # H
    "hai", "hat", "hau", "haw", "heb", "her", "hil", "him", "hin", "hit",
    "hmn", "hmo", "hrv", "hsb", "hun", "hup",
    # I
    "iba", "ibo", "ice", "isl", "ido", "iii", "ijo", "iku", "ile", "ilo",
    "ina", "inc", "ind", "ine", "inh", "ipk", "ira", "iro", "ita",
    # J
    "jav", "jbo", "jpn", "jpr", "jrb",
    # K
    "kaa", "kab", "kac", "kal", "kam", "kan", "kar", "kas", "kau", "kaw",
    "kaz", "kbd", "kha", "khi", "khm", "kho", "kik", "kin", "kir", "kmb",
    "kok", "kom", "kon", "kor", "kos", "kpe", "krc", "krl", "kro", "kru",
    "kua", "kum", "kur", "kut",
    # L
    "lad", "lah", "lam", "lao", "lat", "lav", "lez", "lim", "lin", "lit",
    "lol", "loz", "ltz", "lua", "lub", "lug", "lui", "lun", "luo", "lus",
    # M
    "mac", "mkd", "mad", "mag", "mah", "mai", "mak", "mal", "man", "mao",
    "mri", "map", "mar", "mas", "may", "msa", "mdf", "mdr", "men", "mga",
    "mic", "min", "mis", "mkh", "mlg", "mlt", "mnc", "mni", "mno", "moh",
    "mon", "mos", "mul", "mun", "mus", "mwl", "mwr", "myn",
    # N
    "nah", "nai", "nap", "nau", "nav", "nbl", "nde", "ndo", "nds", "nep",
    "new", "nia", "nic", "niu", "nno", "nob", "nog", "non", "nor", "nqo",
    "nso", "nub", "nwc", "nya", "nym", "nyn", "nyo", "nzi",
    # O
    "oci", "oji", "ori", "orm", "osa", "oss", "ota", "oto",
    # P
    "paa", "pag", "pal", "pam", "pan", "pap", "pau", "peo", "phi", "phn",
    "pli", "pol", "pon", "por", "pra", "pro", "pus",
    # Q
    "que",
    # R
    "raj", "rap", "rar", "roa", "roh", "rom", "ron", "rum", "run", "rup", "rus",
    # S
    "sad", "sag", "sah", "sai", "sal", "sam", "san", "sas", "sat", "scn",
    "sco", "sel", "sem", "sga", "sgn", "shn", "sid", "sin", "sio", "sit",
    "sla", "slo", "slk", "slv", "sma", "sme", "smi", "smj", "smn", "smo",
    "sms", "sna", "snd", "snk", "sog", "som", "son", "sot", "spa", "srd",
    "srn", "srp", "srr", "ssa", "ssw", "suk", "sun", "sus", "sux", "swa",
    "swe", "syc", "syr",
    # T
    "tah", "tai", "tam", "tat", "tel", "tem", "ter", "tet", "tgk", "tgl",
    "tha", "tig", "tir", "tiv", "tkl", "tlh", "tli", "tmh", "tog", "ton",
    "tpi", "tsi", "tsn", "tso", "tuk", "tum", "tup", "tur", "tut", "tvl",
    "twi", "tyv",
    # U
    "udm", "uga", "uig", "ukr", "umb", "und", "urd", "uzb",
    # V
    "vai", "ven", "vie", "vol", "vot",
    # W
    "wak", "wal", "war", "was", "wen", "wln", "wol",
    # X
    "xal", "xho",
    # Y
    "yao", "yap", "yid", "yor",
    # Z
    "zap", "zbl", "zen", "zgh", "zha", "znd", "zul", "zun", "zxx", "zza"
)

function Test-ValidLanguage {
    # Valida codice lingua contro lista completa ISO 639-2
    param([string]$Lang)
    $normalized = $Lang.ToLower().Trim()
    return $script:ValidLanguages -contains $normalized
}

function Get-SimilarLanguages {
    # Trova codici lingua simili a quello inserito (per suggerimenti)
    param([string]$Lang, [int]$MaxResults = 3)

    $normalized = $Lang.ToLower().Trim()
    $suggestions = @()

    # Mappa nomi comuni -> codici
    $commonNames = @{
        "italian" = "ita"; "italiano" = "ita"
        "english" = "eng"; "inglese" = "eng"
        "japanese" = "jpn"; "giapponese" = "jpn"
        "german" = "ger"; "tedesco" = "ger"; "deutsch" = "deu"
        "french" = "fra"; "francese" = "fra"; "français" = "fra"
        "spanish" = "spa"; "spagnolo" = "spa"; "español" = "spa"
        "portuguese" = "por"; "portoghese" = "por"
        "russian" = "rus"; "russo" = "rus"
        "chinese" = "chi"; "cinese" = "chi"
        "korean" = "kor"; "coreano" = "kor"
        "arabic" = "ara"; "arabo" = "ara"
        "dutch" = "dut"; "olandese" = "dut"
        "polish" = "pol"; "polacco" = "pol"
        "turkish" = "tur"; "turco" = "tur"
        "greek" = "gre"; "greco" = "gre"
        "hebrew" = "heb"; "ebraico" = "heb"
        "hindi" = "hin"
        "thai" = "tha"; "tailandese" = "tha"
        "vietnamese" = "vie"; "vietnamita" = "vie"
        "swedish" = "swe"; "svedese" = "swe"
        "norwegian" = "nor"; "norvegese" = "nor"
        "danish" = "dan"; "danese" = "dan"
        "finnish" = "fin"; "finlandese" = "fin"
        "hungarian" = "hun"; "ungherese" = "hun"
        "czech" = "cze"; "ceco" = "cze"
        "romanian" = "ron"; "rumeno" = "ron"
        "bulgarian" = "bul"; "bulgaro" = "bul"
        "croatian" = "hrv"; "croato" = "hrv"
        "serbian" = "srp"; "serbo" = "srp"
        "ukrainian" = "ukr"; "ucraino" = "ukr"
        "indonesian" = "ind"; "indonesiano" = "ind"
        "malay" = "may"; "malese" = "may"
        "latin" = "lat"; "latino" = "lat"
        "undefined" = "und"; "unknown" = "und"
    }

    # Controlla se e' un nome comune
    if ($commonNames.ContainsKey($normalized)) {
        $suggestions += $commonNames[$normalized]
    }

    # Cerca codici che iniziano con le stesse lettere
    foreach ($code in $script:ValidLanguages) {
        if ($code.StartsWith($normalized.Substring(0, [Math]::Min(2, $normalized.Length)))) {
            if ($suggestions -notcontains $code) {
                $suggestions += $code
            }
        }
        if ($suggestions.Count -ge $MaxResults) { break }
    }

    # Cerca codici che contengono l'input
    if ($suggestions.Count -lt $MaxResults) {
        foreach ($code in $script:ValidLanguages) {
            if ($code -like "*$normalized*" -or $normalized -like "*$code*") {
                if ($suggestions -notcontains $code) {
                    $suggestions += $code
                }
            }
            if ($suggestions.Count -ge $MaxResults) { break }
        }
    }

    return $suggestions | Select-Object -First $MaxResults
}

# Validazione codec se specificato
if ($AudioCodec) {
    $codecPatterns = Get-CodecPatterns -UserCodec $AudioCodec
    if (-not $codecPatterns) {
        Write-Host "Errore: codec '$AudioCodec' non riconosciuto." -ForegroundColor Red
        Write-Host "Codec validi: $($script:CodecMap.Keys -join ', ')" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "Codec selezionato: $AudioCodec -> matcha: $($codecPatterns -join ', ')" -ForegroundColor Green
}

# Validazione lingua target
if (-not (Test-ValidLanguage $TargetLanguage)) {
    Write-Host "Errore: lingua '$TargetLanguage' non riconosciuta." -ForegroundColor Red
    $suggestions = Get-SimilarLanguages -Lang $TargetLanguage
    if ($suggestions) {
        Write-Host "Forse intendevi: $($suggestions -join ', ')?" -ForegroundColor Yellow
    } else {
        Write-Host "Usa codici ISO 639-2 (es: ita, eng, jpn, ger, fra, spa)" -ForegroundColor Yellow
    }
    exit 1
}

# Validazione lingue da mantenere
if ($KeepSourceAudioLangs) {
    foreach ($lang in $KeepSourceAudioLangs) {
        if (-not (Test-ValidLanguage $lang)) {
            Write-Host "Errore: lingua '$lang' in -KeepSourceAudioLangs non riconosciuta." -ForegroundColor Red
            $suggestions = Get-SimilarLanguages -Lang $lang
            if ($suggestions) {
                Write-Host "Forse intendevi: $($suggestions -join ', ')?" -ForegroundColor Yellow
            }
            exit 1
        }
    }
}

if ($KeepSourceSubtitleLangs) {
    foreach ($lang in $KeepSourceSubtitleLangs) {
        if (-not (Test-ValidLanguage $lang)) {
            Write-Host "Errore: lingua '$lang' in -KeepSourceSubtitleLangs non riconosciuta." -ForegroundColor Red
            $suggestions = Get-SimilarLanguages -Lang $lang
            if ($suggestions) {
                Write-Host "Forse intendevi: $($suggestions -join ', ')?" -ForegroundColor Yellow
            }
            exit 1
        }
    }
}

#endregion

#region Funzioni Utility

function Get-EpisodeIdentifier {
    # Estrae l'identificativo episodio dal nome file usando il pattern regex
    param([string]$FileName, [string]$Pattern)
    if ($FileName -match $Pattern) {
        return $Matches[1..($Matches.Count - 1)] -join "_"
    }
    return $null
}

function Get-MkvTrackInfo {
    # Ottiene informazioni sulle tracce di un file MKV in formato JSON
    param([string]$FilePath)
    $jsonOutput = & $MkvMergePath -J $FilePath 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Impossibile leggere info tracce per: $FilePath"
        return $null
    }
    return $jsonOutput | ConvertFrom-Json
}

function Test-LanguageMatch {
    # Verifica se una traccia corrisponde alla lingua specificata
    param([object]$Track, [string]$Language)
    $trackLang = $Track.properties.language
    $trackLangIetf = $Track.properties.language_ietf
    return ($trackLang -eq $Language) -or ($trackLangIetf -like "$Language*") -or ($trackLangIetf -eq $Language)
}

function Test-LanguageInList {
    # Verifica se una traccia corrisponde a una delle lingue nella lista
    param([object]$Track, [string[]]$Languages)
    foreach ($lang in $Languages) {
        if (Test-LanguageMatch -Track $Track -Language $lang) { return $true }
    }
    return $false
}

function Get-FilteredTracks {
    # Filtra le tracce per tipo, lingua e opzionalmente codec
    param(
        [object]$MkvInfo,
        [string]$Language,
        [string]$TrackType,
        [string]$Codec = $null
    )

    $tracks = @()

    # Ottieni pattern esatti per il codec se specificato
    $codecPatterns = $null
    if ($Codec -and $TrackType -eq "audio") {
        $codecPatterns = Get-CodecPatterns -UserCodec $Codec
    }

    foreach ($track in $MkvInfo.tracks) {
        if ($track.type -ne $TrackType) { continue }
        if (-not (Test-LanguageMatch -Track $track -Language $Language)) { continue }

        # Filtro codec per tracce audio - matching ESATTO
        if ($codecPatterns) {
            $trackCodec = $track.codec
            $codecMatch = $false

            foreach ($pattern in $codecPatterns) {
                # Match esatto (case-insensitive)
                if ($trackCodec -eq $pattern) {
                    $codecMatch = $true
                    break
                }
            }

            if (-not $codecMatch) { continue }
        }

        $tracks += @{
            Id = $track.id
            Type = $track.type
            Codec = $track.codec
            Language = $track.properties.language
            LanguageIetf = $track.properties.language_ietf
            Name = $track.properties.track_name
            Default = $track.properties.default_track
            Forced = $track.properties.forced_track
        }
    }
    return $tracks
}

function Get-SourceTrackIds {
    # Ottiene gli ID delle tracce sorgente da mantenere in base ai filtri lingua
    param([object]$MkvInfo, [string]$TrackType, [string[]]$KeepLanguages)

    $trackIds = @()
    foreach ($track in $MkvInfo.tracks) {
        if ($track.type -ne $TrackType) { continue }
        if ($null -eq $KeepLanguages -or $KeepLanguages.Count -eq 0) {
            # Nessun filtro: mantieni tutte
            $trackIds += $track.id
        }
        elseif (Test-LanguageInList -Track $track -Languages $KeepLanguages) {
            # Mantieni solo le lingue specificate
            $trackIds += $track.id
        }
    }
    return $trackIds
}

function Get-FirstSubtitleTrackId {
    # Trova il primo sottotitolo disponibile, preferendo quelli testuali
    param([object]$MkvInfo, [string]$Language = $null)

    # Prima cerca sottotitoli testuali (SRT, ASS, SSA)
    foreach ($track in $MkvInfo.tracks) {
        if ($track.type -ne "subtitles") { continue }
        if ($track.codec -notlike "*PGS*" -and $track.codec -notlike "*VOBSUB*" -and $track.codec -notlike "*HDMV*") {
            if ($Language) {
                if (Test-LanguageMatch -Track $track -Language $Language) {
                    return $track.id
                }
            } else {
                return $track.id
            }
        }
    }
    # Fallback: qualsiasi sottotitolo
    foreach ($track in $MkvInfo.tracks) {
        if ($track.type -eq "subtitles") {
            if ($Language) {
                if (Test-LanguageMatch -Track $track -Language $Language) {
                    return $track.id
                }
            } else {
                return $track.id
            }
        }
    }
    return $null
}

function ConvertTo-Milliseconds {
    # Converte timestamp SRT (00:01:23,456) in millisecondi
    param([string]$SrtTimestamp)
    if ($SrtTimestamp -match '(\d+):(\d+):(\d+)[,.](\d+)') {
        $h = [int]$Matches[1]
        $m = [int]$Matches[2]
        $s = [int]$Matches[3]
        $ms = [int]$Matches[4].PadRight(3, '0').Substring(0, 3)
        return ($h * 3600000) + ($m * 60000) + ($s * 1000) + $ms
    }
    return 0
}

function ConvertTo-AssMilliseconds {
    # Converte timestamp ASS (H:MM:SS.CC centisecondi) in millisecondi
    param([string]$AssTimestamp)
    if ($AssTimestamp -match '(\d+):(\d{2}):(\d{2})\.(\d{2})') {
        $h = [int]$Matches[1]
        $m = [int]$Matches[2]
        $s = [int]$Matches[3]
        $cs = [int]$Matches[4]  # centisecondi (1/100 di secondo)
        return ($h * 3600000) + ($m * 60000) + ($s * 1000) + ($cs * 10)
    }
    return 0
}

function Get-AutoSyncOffset {
    <#
    .SYNOPSIS
        Calcola l'offset di sync usando audio fingerprinting via ffmpeg.
        Funziona con lingue diverse perche' confronta musica/effetti/silenzi,
        non il contenuto vocale.
    #>
    param(
        [string]$SourceVideo,
        [string]$LanguageFile,
        [object]$SourceMkvInfo,
        [object]$LanguageMkvInfo,
        [string]$Language,
        [string]$TempFolder
    )

    # Crea cartella temporanea
    if (-not (Test-Path $TempFolder)) {
        [System.IO.Directory]::CreateDirectory($TempFolder) | Out-Null
    }

    $tempSourceAudio = Join-Path $TempFolder "source_audio.wav"
    $tempLangAudio = Join-Path $TempFolder "lang_audio.wav"

    try {
        $ffmpegPath = $env:AUTOSYNC_FFMPEG_PATH
        if (-not $ffmpegPath) {
            $ffmpegPath = "ffmpeg"
        }

        # Estrai primi 5 minuti di audio da entrambi i file (mono, 8kHz per velocita')
        Write-Host "  Estrazione campioni audio (5 min) per confronto..." -ForegroundColor DarkGray

        # Audio sorgente
        $sourceArgs = @("-i", $SourceVideo, "-vn", "-ac", "1", "-ar", "8000", "-t", "300", "-y", $tempSourceAudio)
        $null = & $ffmpegPath @sourceArgs 2>&1

        # Audio lingua
        $langArgs = @("-i", $LanguageFile, "-vn", "-ac", "1", "-ar", "8000", "-t", "300", "-y", $tempLangAudio)
        $null = & $ffmpegPath @langArgs 2>&1

        if (-not (Test-Path $tempSourceAudio) -or -not (Test-Path $tempLangAudio)) {
            Write-Warning "  Impossibile estrarre audio"
            return $null
        }

        Write-Host "  Analisi cross-correlazione audio..." -ForegroundColor DarkGray

        $ffprobePath = $env:AUTOSYNC_FFPROBE_PATH
        if (-not $ffprobePath) {
            $ffprobePath = "ffprobe"
        }

        Write-Host "  Analisi pattern audio..." -ForegroundColor DarkGray

        $sourceMarkers = @()
        $langMarkers = @()

        # Metodo 1: Rilevamento silenzi
        $sourceSilenceArgs = @("-i", $tempSourceAudio, "-af", "silencedetect=noise=-35dB:d=0.3", "-f", "null", "-")
        $sourceSilenceOut = & $ffmpegPath @sourceSilenceArgs 2>&1 | Out-String

        $langSilenceArgs = @("-i", $tempLangAudio, "-af", "silencedetect=noise=-35dB:d=0.3", "-f", "null", "-")
        $langSilenceOut = & $ffmpegPath @langSilenceArgs 2>&1 | Out-String

        # Marker inizio silenzio
        $silenceStartRegex = [regex]'silence_start:\s*([\d.]+)'
        foreach ($match in $silenceStartRegex.Matches($sourceSilenceOut)) {
            $sourceMarkers += @{ Time = [double]$match.Groups[1].Value; Type = "silence_start" }
        }
        foreach ($match in $silenceStartRegex.Matches($langSilenceOut)) {
            $langMarkers += @{ Time = [double]$match.Groups[1].Value; Type = "silence_start" }
        }

        # Marker fine silenzio (quando riprende il suono - molto affidabile per sync)
        $silenceEndRegex = [regex]'silence_end:\s*([\d.]+)'
        foreach ($match in $silenceEndRegex.Matches($sourceSilenceOut)) {
            $sourceMarkers += @{ Time = [double]$match.Groups[1].Value; Type = "silence_end" }
        }
        foreach ($match in $silenceEndRegex.Matches($langSilenceOut)) {
            $langMarkers += @{ Time = [double]$match.Groups[1].Value; Type = "silence_end" }
        }

        # Metodo 2: Rilevamento picchi volume usando astats (livelli RMS per segmento)
        $sourceAstatsArgs = @("-i", $tempSourceAudio, "-af", "astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.RMS_level:file=-", "-f", "null", "-")
        $sourceAstatsOut = & $ffmpegPath @sourceAstatsArgs 2>&1 | Out-String

        $langAstatsArgs = @("-i", $tempLangAudio, "-af", "astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.RMS_level:file=-", "-f", "null", "-")
        $langAstatsOut = & $ffmpegPath @langAstatsArgs 2>&1 | Out-String

        # Parsing livelli RMS e rilevamento transient (aumenti improvvisi > 6dB)
        $rmsRegex = [regex]'pts_time:([\d.]+).*?lavfi\.astats\.Overall\.RMS_level=(-?[\d.]+)'
        $sourceRmsLevels = @()
        $langRmsLevels = @()

        $sourceAstatsFlat = $sourceAstatsOut -replace "`n", " "
        $langAstatsFlat = $langAstatsOut -replace "`n", " "

        foreach ($match in $rmsRegex.Matches($sourceAstatsFlat)) {
            $sourceRmsLevels += @{ Time = [double]$match.Groups[1].Value; Level = [double]$match.Groups[2].Value }
        }
        foreach ($match in $rmsRegex.Matches($langAstatsFlat)) {
            $langRmsLevels += @{ Time = [double]$match.Groups[1].Value; Level = [double]$match.Groups[2].Value }
        }

        # Rileva transient (aumenti improvvisi di volume)
        for ($i = 1; $i -lt $sourceRmsLevels.Count; $i++) {
            $prev = $sourceRmsLevels[$i-1].Level
            $curr = $sourceRmsLevels[$i].Level
            # Transient = aumento improvviso di 6dB o piu'
            if ($prev -lt -50) { $prev = -50 }  # Floor per valori -inf
            if ($curr -lt -50) { $curr = -50 }
            if (($curr - $prev) -gt 6) {
                $sourceMarkers += @{ Time = $sourceRmsLevels[$i].Time; Type = "peak" }
            }
        }

        for ($i = 1; $i -lt $langRmsLevels.Count; $i++) {
            $prev = $langRmsLevels[$i-1].Level
            $curr = $langRmsLevels[$i].Level
            if ($prev -lt -50) { $prev = -50 }
            if ($curr -lt -50) { $curr = -50 }
            if (($curr - $prev) -gt 6) {
                $langMarkers += @{ Time = $langRmsLevels[$i].Time; Type = "peak" }
            }
        }

        $sourceTimes = $sourceMarkers | ForEach-Object { $_.Time }
        $langTimes = $langMarkers | ForEach-Object { $_.Time }

        # Statistiche marker trovati
        $srcSilenceStart = ($sourceMarkers | Where-Object { $_.Type -eq "silence_start" }).Count
        $srcSilenceEnd = ($sourceMarkers | Where-Object { $_.Type -eq "silence_end" }).Count
        $srcPeaks = ($sourceMarkers | Where-Object { $_.Type -eq "peak" }).Count

        $langSilenceStart = ($langMarkers | Where-Object { $_.Type -eq "silence_start" }).Count
        $langSilenceEnd = ($langMarkers | Where-Object { $_.Type -eq "silence_end" }).Count
        $langPeaks = ($langMarkers | Where-Object { $_.Type -eq "peak" }).Count

        Write-Host "  Source: $srcSilenceStart silence_start, $srcSilenceEnd silence_end, $srcPeaks transients" -ForegroundColor DarkGray
        Write-Host "  Language: $langSilenceStart silence_start, $langSilenceEnd silence_end, $langPeaks transients" -ForegroundColor DarkGray
        Write-Host "  Totale marker: $($sourceMarkers.Count) source, $($langMarkers.Count) language" -ForegroundColor DarkGray

        if ($sourceMarkers.Count -lt 5 -or $langMarkers.Count -lt 5) {
            Write-Warning "  Marker audio insufficienti per sync affidabile"
            return $null
        }

        # Ricerca in 3 fasi per trovare l'offset migliore
        # Fase 1: Ricerca grossolana (-60s a +60s, step 500ms)
        Write-Host "  Fase 1: Ricerca grossolana..." -ForegroundColor DarkGray
        $bestOffset = 0
        $bestScore = 0

        for ($tryOffset = -60000; $tryOffset -le 60000; $tryOffset += 500) {
            $score = 0
            $tryOffsetSec = $tryOffset / 1000.0

            foreach ($sourceTime in $sourceTimes) {
                foreach ($langTime in $langTimes) {
                    $diff = [Math]::Abs(($langTime + $tryOffsetSec) - $sourceTime)
                    if ($diff -lt 0.5) {
                        $score++
                    }
                }
            }

            if ($score -gt $bestScore) {
                $bestScore = $score
                $bestOffset = $tryOffset
            }
        }

        Write-Host "  Risultato grossolano: ${bestOffset}ms ($bestScore match)" -ForegroundColor DarkGray

        # Fase 2: Ricerca fine (intorno al risultato, step 10ms)
        Write-Host "  Fase 2: Ricerca fine..." -ForegroundColor DarkGray
        $fineStart = $bestOffset - 2000
        $fineEnd = $bestOffset + 2000
        $fineBestOffset = $bestOffset
        $fineBestScore = 0

        for ($tryOffset = $fineStart; $tryOffset -le $fineEnd; $tryOffset += 10) {
            $score = 0
            $tryOffsetSec = $tryOffset / 1000.0

            foreach ($sourceTime in $sourceTimes) {
                foreach ($langTime in $langTimes) {
                    $diff = [Math]::Abs(($langTime + $tryOffsetSec) - $sourceTime)
                    if ($diff -lt 0.2) {  # Matching piu' stretto per fase fine
                        $score += (0.2 - $diff)  # Score pesato - piu' vicino = meglio
                    }
                }
            }

            if ($score -gt $fineBestScore) {
                $fineBestScore = $score
                $fineBestOffset = $tryOffset
            }
        }

        Write-Host "  Risultato fine: ${fineBestOffset}ms (score: $([Math]::Round($fineBestScore, 2)))" -ForegroundColor DarkGray

        # Fase 3: Ricerca ultra-fine (precisione 1ms)
        Write-Host "  Fase 3: Ricerca ultra-fine..." -ForegroundColor DarkGray
        $ultraStart = $fineBestOffset - 100
        $ultraEnd = $fineBestOffset + 100
        $ultraBestOffset = $fineBestOffset
        $ultraBestScore = 0

        for ($tryOffset = $ultraStart; $tryOffset -le $ultraEnd; $tryOffset += 1) {
            $score = 0
            $tryOffsetSec = $tryOffset / 1000.0

            foreach ($sourceTime in $sourceTimes) {
                foreach ($langTime in $langTimes) {
                    $diff = [Math]::Abs(($langTime + $tryOffsetSec) - $sourceTime)
                    if ($diff -lt 0.15) {
                        $score += (0.15 - $diff)
                    }
                }
            }

            if ($score -gt $ultraBestScore) {
                $ultraBestScore = $score
                $ultraBestOffset = $tryOffset
            }
        }

        Write-Host "  Risultato ultra-fine: ${ultraBestOffset}ms (score: $([Math]::Round($ultraBestScore, 2)))" -ForegroundColor DarkGray

        if ($bestScore -lt 3) {
            Write-Warning "  Sync a bassa confidenza (solo $bestScore match grossolani)"
        }

        return [int]$ultraBestOffset
    }
    finally {
        # Pulizia file temporanei
        foreach ($f in @($tempSourceAudio, $tempLangAudio)) {
            if (Test-Path $f) { try { [System.IO.File]::Delete($f) } catch {} }
        }
    }
}

function Invoke-MkvMerge {
    # Costruisce il comando mkvmerge per unire le tracce
    param(
        [string]$SourceFile,
        [string]$LanguageFile,
        [string]$OutputFile,
        [array]$SourceAudioTrackIds,
        [array]$SourceSubtitleTrackIds,
        [array]$LangAudioTracks,
        [array]$LangSubtitleTracks,
        [int]$AudioDelayMs,
        [int]$SubtitleDelayMs,
        [bool]$FilterSourceAudio,
        [bool]$FilterSourceSubtitles
    )

    $mkvArgs = @()
    $mkvArgs += "-o"
    $mkvArgs += $OutputFile

    # Selezione tracce dal file sorgente
    if ($FilterSourceAudio -and $SourceAudioTrackIds.Count -gt 0) {
        $mkvArgs += "--audio-tracks"
        $mkvArgs += ($SourceAudioTrackIds -join ",")
    }
    elseif ($FilterSourceAudio -and $SourceAudioTrackIds.Count -eq 0) {
        $mkvArgs += "-A"  # Rimuovi tutto l'audio dal sorgente
    }

    if ($FilterSourceSubtitles -and $SourceSubtitleTrackIds.Count -gt 0) {
        $mkvArgs += "--subtitle-tracks"
        $mkvArgs += ($SourceSubtitleTrackIds -join ",")
    }
    elseif ($FilterSourceSubtitles -and $SourceSubtitleTrackIds.Count -eq 0) {
        $mkvArgs += "-S"  # Rimuovi tutti i sub dal sorgente
    }

    $mkvArgs += $SourceFile

    # Selezione tracce dal file lingua
    $langAudioTrackIds = $LangAudioTracks | ForEach-Object { $_.Id }
    $langSubtitleTrackIds = $LangSubtitleTracks | ForEach-Object { $_.Id }

    $mkvArgs += "-D"  # Niente video dal file lingua

    # Tracce audio
    if ($langAudioTrackIds.Count -gt 0) {
        $mkvArgs += "--audio-tracks"
        $mkvArgs += ($langAudioTrackIds -join ",")

        if ($AudioDelayMs -ne 0) {
            foreach ($trackId in $langAudioTrackIds) {
                $mkvArgs += "--sync"
                $mkvArgs += "${trackId}:${AudioDelayMs}"
            }
        }
    }
    else {
        $mkvArgs += "-A"
    }

    # Tracce sottotitoli
    if ($langSubtitleTrackIds.Count -gt 0) {
        $mkvArgs += "--subtitle-tracks"
        $mkvArgs += ($langSubtitleTrackIds -join ",")

        if ($SubtitleDelayMs -ne 0) {
            foreach ($trackId in $langSubtitleTrackIds) {
                $mkvArgs += "--sync"
                $mkvArgs += "${trackId}:${SubtitleDelayMs}"
            }
        }
    }
    else {
        $mkvArgs += "-S"
    }

    $mkvArgs += $LanguageFile

    # Formatta comando per visualizzazione
    $displayArgs = $mkvArgs | ForEach-Object {
        if ($_ -match '\s|\\\\') { "`"$_`"" } else { $_ }
    }

    return @{
        Command = "$MkvMergePath $($displayArgs -join ' ')"
        Arguments = $mkvArgs
    }
}

function Format-TrackInfo {
    # Formatta le informazioni traccia per output leggibile
    param([array]$Tracks, [string]$Type)
    if ($Tracks.Count -eq 0) { return "  Nessuna" }

    $output = @()
    foreach ($track in $Tracks) {
        $name = if ($track.Name) { " - `"$($track.Name)`"" } else { "" }
        $lang = if ($track.Language) { "[$($track.Language)]" } else { "[und]" }
        $output += "  Track $($track.Id): $($track.Codec) $lang$name"
    }
    return $output -join "`n"
}

function Format-TrackIdList {
    # Formatta lista ID tracce
    param([array]$TrackIds)
    if ($TrackIds.Count -eq 0) { return "Nessuna" }
    return $TrackIds -join ", "
}

function Format-Delay {
    # Formatta delay in millisecondi con segno
    param([int]$DelayMs)
    if ($DelayMs -eq 0) { return "0ms" }
    $sign = if ($DelayMs -gt 0) { "+" } else { "" }
    return "${sign}${DelayMs}ms"
}

#endregion

#region Elaborazione Principale

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  MKV Language Track Merger" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Configurazione:" -ForegroundColor Yellow
Write-Host "  Cartella sorgente:   $SourceFolder"
Write-Host "  Cartella lingua:     $LanguageFolder"
Write-Host "  Lingua target:       $TargetLanguage"
Write-Host "  Pattern matching:    $MatchPattern"
Write-Host "  Modalita' output:    $OutputMode"
if ($OutputMode -eq "Destination") {
    Write-Host "  Cartella output:     $DestinationFolder"
}
if ($AutoSync) {
    Write-Host "  Auto-sync:           ATTIVO (audio fingerprint)" -ForegroundColor Green
    if ($AudioDelay -ne 0 -or $SubtitleDelay -ne 0) {
        Write-Host "  Offset manuale:      Audio $(Format-Delay $AudioDelay), Sub $(Format-Delay $SubtitleDelay) (sommato ad auto)" -ForegroundColor DarkYellow
    }
} else {
    Write-Host "  Delay audio:         $(Format-Delay $AudioDelay)"
    Write-Host "  Delay sottotitoli:   $(Format-Delay $SubtitleDelay)"
}
if ($SubOnly) {
    Write-Host "  Solo sottotitoli:    SI (audio ignorato)" -ForegroundColor Cyan
}
elseif ($AudioCodec) {
    Write-Host "  Filtro codec audio:  $AudioCodec"
}
if ($KeepSourceAudioLangs) {
    Write-Host "  Mantieni audio src:  $($KeepSourceAudioLangs -join ', ')"
}
if ($KeepSourceSubtitleLangs) {
    Write-Host "  Mantieni sub src:    $($KeepSourceSubtitleLangs -join ', ')"
}
Write-Host ""

# Cartella temporanea per operazioni sync
$TempFolder = Join-Path $env:TEMP "MergeLanguageTracks"

# Trova tutti i file MKV sorgente
$sourceFiles = Get-ChildItem -Path $SourceFolder -Filter "*.mkv" -Recurse:$Recursive -File
Write-Host "Trovati $($sourceFiles.Count) file MKV sorgente`n" -ForegroundColor Green

# Costruisci indice file lingua
Write-Host "Indicizzazione cartella lingua..." -ForegroundColor Yellow
$languageFiles = Get-ChildItem -Path $LanguageFolder -Filter "*.mkv" -Recurse:$Recursive -File
$languageIndex = @{}

foreach ($langFile in $languageFiles) {
    $episodeId = Get-EpisodeIdentifier -FileName $langFile.Name -Pattern $MatchPattern
    if ($episodeId) {
        $languageIndex[$episodeId] = $langFile.FullName
    }
}

Write-Host "Indicizzati $($languageIndex.Count) file lingua`n" -ForegroundColor Green

# Statistiche elaborazione
$stats = @{
    Processed = 0
    Skipped = 0
    NoMatch = 0
    NoTracks = 0
    SyncFailed = 0
    Errors = 0
}

$filterSourceAudio = $null -ne $KeepSourceAudioLangs -and $KeepSourceAudioLangs.Count -gt 0
$filterSourceSubs = $null -ne $KeepSourceSubtitleLangs -and $KeepSourceSubtitleLangs.Count -gt 0

foreach ($sourceFile in $sourceFiles) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "Elaborazione: $($sourceFile.Name)" -ForegroundColor White

    $episodeId = Get-EpisodeIdentifier -FileName $sourceFile.Name -Pattern $MatchPattern

    if (-not $episodeId) {
        Write-Host "  [SKIP] Impossibile estrarre ID episodio dal nome file" -ForegroundColor Yellow
        $stats.Skipped++
        continue
    }

    Write-Host "  ID Episodio: $episodeId" -ForegroundColor DarkGray

    if (-not $languageIndex.ContainsKey($episodeId)) {
        Write-Host "  [SKIP] Nessun file lingua corrispondente" -ForegroundColor Yellow
        $stats.NoMatch++
        continue
    }

    $languageFile = $languageIndex[$episodeId]
    Write-Host "  Match: $(Split-Path $languageFile -Leaf)" -ForegroundColor DarkCyan

    # Ottieni info tracce
    $sourceMkvInfo = Get-MkvTrackInfo -FilePath $sourceFile.FullName
    $langMkvInfo = Get-MkvTrackInfo -FilePath $languageFile

    if (-not $langMkvInfo) {
        Write-Host "  [ERRORE] Impossibile leggere info tracce file lingua" -ForegroundColor Red
        $stats.Errors++
        continue
    }

    # Calcola offset auto-sync se abilitato
    $effectiveAudioDelay = $AudioDelay
    $effectiveSubDelay = $SubtitleDelay

    if ($AutoSync) {
        # Usa sempre audio fingerprinting - piu' affidabile del pattern matching sottotitoli
        # Funziona perche' musica, effetti sonori e silenzi sono identici tra doppiaggi
        Write-Host "`n  [AUTO-SYNC] Modalita': Audio fingerprinting (silenzi + picchi)" -ForegroundColor Cyan

        $autoOffset = Get-AutoSyncOffset `
            -SourceVideo $sourceFile.FullName `
            -LanguageFile $languageFile `
            -SourceMkvInfo $sourceMkvInfo `
            -LanguageMkvInfo $langMkvInfo `
            -Language $TargetLanguage `
            -TempFolder $TempFolder

        if ($null -ne $autoOffset) {
            Write-Host "  [AUTO-SYNC] Offset rilevato: $(Format-Delay $autoOffset)" -ForegroundColor Green

            # Somma offset manuale a quello automatico
            $effectiveAudioDelay = $autoOffset + $AudioDelay
            $effectiveSubDelay = $autoOffset + $SubtitleDelay

            if ($AudioDelay -ne 0 -or $SubtitleDelay -ne 0) {
                Write-Host "  [AUTO-SYNC] Offset finale (auto + manuale): Audio $(Format-Delay $effectiveAudioDelay), Sub $(Format-Delay $effectiveSubDelay)" -ForegroundColor DarkYellow
            }
        }
        else {
            Write-Host "  [AUTO-SYNC] Impossibile calcolare offset, uso valori manuali" -ForegroundColor Yellow
            $stats.SyncFailed++
        }
    }

    # Ottieni ID tracce sorgente da mantenere
    $sourceAudioIds = @()
    $sourceSubIds = @()

    if ($sourceMkvInfo) {
        if ($filterSourceAudio) {
            $sourceAudioIds = Get-SourceTrackIds -MkvInfo $sourceMkvInfo -TrackType "audio" -KeepLanguages $KeepSourceAudioLangs
            Write-Host "`n  Audio sorgente da mantenere: $(Format-TrackIdList $sourceAudioIds)" -ForegroundColor DarkYellow
        }
        if ($filterSourceSubs) {
            $sourceSubIds = Get-SourceTrackIds -MkvInfo $sourceMkvInfo -TrackType "subtitles" -KeepLanguages $KeepSourceSubtitleLangs
            Write-Host "  Sub sorgente da mantenere:   $(Format-TrackIdList $sourceSubIds)" -ForegroundColor DarkYellow
        }
    }

    # Ottieni tracce dal file lingua
    if ($SubOnly) {
        $audioTracks = @()  # Salta audio quando SubOnly e' attivo
    } else {
        $audioTracks = Get-FilteredTracks -MkvInfo $langMkvInfo -Language $TargetLanguage -TrackType "audio" -Codec $AudioCodec
    }
    $subtitleTracks = Get-FilteredTracks -MkvInfo $langMkvInfo -Language $TargetLanguage -TrackType "subtitles"

    Write-Host "`n  Audio file lingua ($TargetLanguage$(if($AudioCodec){" / $AudioCodec"})):" -ForegroundColor Magenta
    Write-Host (Format-TrackInfo -Tracks $audioTracks -Type "audio")

    Write-Host "`n  Sottotitoli file lingua ($TargetLanguage):" -ForegroundColor Magenta
    Write-Host (Format-TrackInfo -Tracks $subtitleTracks -Type "subtitle")

    if ($audioTracks.Count -eq 0 -and $subtitleTracks.Count -eq 0) {
        Write-Host "`n  [SKIP] Nessuna traccia corrispondente trovata" -ForegroundColor Yellow
        $stats.NoTracks++
        continue
    }

    # Determina path output
    if ($OutputMode -eq "Overwrite") {
        $tempOutput = [System.IO.Path]::Combine(
            [System.IO.Path]::GetDirectoryName($sourceFile.FullName),
            [System.IO.Path]::GetFileNameWithoutExtension($sourceFile.Name) + "_TEMP.mkv"
        )
        $finalOutput = $sourceFile.FullName
    }
    else {
        $normalizedSourceFile = Get-NormalizedPath $sourceFile.FullName
        $relativePath = $normalizedSourceFile.Substring($SourceFolder.Length).TrimStart('\', '/')
        $finalOutput = Join-Path $DestinationFolder $relativePath
        $tempOutput = $finalOutput

        $destDir = Split-Path $finalOutput -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
        }
    }

    $mergeCmd = Invoke-MkvMerge `
        -SourceFile $sourceFile.FullName `
        -LanguageFile $languageFile `
        -OutputFile $tempOutput `
        -SourceAudioTrackIds $sourceAudioIds `
        -SourceSubtitleTrackIds $sourceSubIds `
        -LangAudioTracks $audioTracks `
        -LangSubtitleTracks $subtitleTracks `
        -AudioDelayMs $effectiveAudioDelay `
        -SubtitleDelayMs $effectiveSubDelay `
        -FilterSourceAudio $filterSourceAudio `
        -FilterSourceSubtitles $filterSourceSubs

    Write-Host "`n  Output: $finalOutput" -ForegroundColor DarkGray
    Write-Host "  Delay applicato: Audio $(Format-Delay $effectiveAudioDelay), Sub $(Format-Delay $effectiveSubDelay)" -ForegroundColor DarkGray

    if ($DryRun) {
        Write-Host "`n  [DRY-RUN] Comando che verrebbe eseguito:" -ForegroundColor Cyan
        Write-Host "  $($mergeCmd.Command)" -ForegroundColor DarkGray
    }
    else {
        Write-Host "`n  Unione in corso..." -ForegroundColor Yellow

        $mkvOutput = & $MkvMergePath @($mergeCmd.Arguments) 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0 -or $exitCode -eq 1) {
            Write-Host "  [OK] Unione completata" -ForegroundColor Green

            if ($OutputMode -eq "Overwrite") {
                Remove-Item $sourceFile.FullName -Force
                Rename-Item $tempOutput -NewName (Split-Path $finalOutput -Leaf) -Force
                Write-Host "  [OK] File originale sostituito" -ForegroundColor Green
            }

            $stats.Processed++
        }
        else {
            Write-Host "  [ERRORE] mkvmerge fallito con codice $exitCode" -ForegroundColor Red
            if ($mkvOutput) {
                Write-Host "  Output: $mkvOutput" -ForegroundColor DarkRed
            }

            if (Test-Path $tempOutput) {
                Remove-Item $tempOutput -Force
            }

            $stats.Errors++
        }
    }
}

# Pulizia cartella temporanea
if (Test-Path $TempFolder) {
    Remove-Item $TempFolder -Recurse -Force -ErrorAction SilentlyContinue
}

#endregion

#region Riepilogo Finale

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Riepilogo" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Elaborati:     $($stats.Processed)" -ForegroundColor Green
Write-Host "  Saltati:       $($stats.Skipped)" -ForegroundColor Yellow
Write-Host "  Senza match:   $($stats.NoMatch)" -ForegroundColor Yellow
Write-Host "  Senza tracce:  $($stats.NoTracks)" -ForegroundColor Yellow
if ($AutoSync) {
    Write-Host "  Sync falliti:  $($stats.SyncFailed)" -ForegroundColor $(if ($stats.SyncFailed -gt 0) { "Yellow" } else { "Green" })
}
Write-Host "  Errori:        $($stats.Errors)" -ForegroundColor $(if ($stats.Errors -gt 0) { "Red" } else { "Green" })
Write-Host ""

#endregion
