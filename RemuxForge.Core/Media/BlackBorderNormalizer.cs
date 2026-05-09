using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Media.Ffmpeg;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemuxForge.Core.Media
{
    /// <summary>
    /// Normalizza bordi neri stabili rilevati sull'intera durata del file
    /// </summary>
    public class BlackBorderNormalizer
    {
        #region Costanti

        private const int BLACK_THRESHOLD = 18;
        private const int MIN_CONTENT_AVG = 30;
        private const int MIN_BORDER_CONTENT_CONTRAST = 12;
        private const int MIN_BORDER_MARGIN = 4;
        private const int MAX_AUTO_CROP_DIVISOR = 6;
        private const int SAMPLE_DURATION_MS = 1000;

        #endregion

        #region Variabili di classe

        private readonly string _ffmpegPath;
        private readonly VideoSyncConfig _videoSyncConfig;
        private readonly FfmpegConfig _ffmpegConfig;
        private readonly LogSection _logSection;
        private readonly VideoGeometryAnalyzer _geometryAnalyzer;
        private readonly object _lock;
        private readonly Dictionary<string, BorderCropProfile> _cache;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="ffmpegPath">Percorso ffmpeg</param>
        /// <param name="videoSyncConfig">Configurazione estrazione frame</param>
        /// <param name="ffmpegConfig">Configurazione ffmpeg</param>
        /// <param name="logSection">Sezione log da usare</param>
        /// <param name="geometryAnalyzer">Analyzer geometria condiviso con il match visuale</param>
        public BlackBorderNormalizer(string ffmpegPath, VideoSyncConfig videoSyncConfig, FfmpegConfig ffmpegConfig, LogSection logSection, VideoGeometryAnalyzer geometryAnalyzer)
        {
            this._ffmpegPath = ffmpegPath;
            this._videoSyncConfig = videoSyncConfig;
            this._ffmpegConfig = ffmpegConfig;
            this._logSection = logSection;
            this._geometryAnalyzer = geometryAnalyzer;
            this._lock = new object();
            this._cache = new Dictionary<string, BorderCropProfile>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Svuota i profili crop dell'analisi corrente
        /// </summary>
        public void Reset()
        {
            lock (this._lock)
            {
                this._cache.Clear();
            }
        }

        /// <summary>
        /// Prepara il profilo crop del file usando campioni distribuiti sulla durata completa
        /// </summary>
        public void PrepareFile(string filePath, int durationMs, bool cropTo43)
        {
            BorderCropProfile profile;
            List<byte[]> frames;
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            lock (this._lock)
            {
                if (this._cache.ContainsKey(filePath))
                {
                    return;
                }
            }

            frames = this.ExtractGlobalSampleFrames(filePath, durationMs, cropTo43);
            profile = this.BuildProfile(frames);
            this.StoreCropProfile(filePath, profile);

            if (profile.Enabled)
            {
                this._geometryAnalyzer.UpdateCropProfile(filePath, profile.Left, this._videoSyncConfig.FrameWidth - 1 - profile.Right, profile.Top, this._videoSyncConfig.FrameHeight - 1 - profile.Bottom);
                ConsoleHelper.Write(this._logSection, LogLevel.Debug, "  Auto-crop globale (" + this.GetLogFileName(filePath) + "): L" + profile.Left + " R" + (this._videoSyncConfig.FrameWidth - 1 - profile.Right) + " T" + profile.Top + " B" + (this._videoSyncConfig.FrameHeight - 1 - profile.Bottom));
            }
            else
            {
                ConsoleHelper.Write(this._logSection, LogLevel.Debug, "  Auto-crop globale (" + this.GetLogFileName(filePath) + "): nessun bordo stabile");
            }
        }

        /// <summary>
        /// Applica ai frame il profilo crop gia' calcolato sul file completo
        /// </summary>
        public void Normalize(string filePath, List<byte[]> frames)
        {
            BorderCropProfile profile;
            int width = this._videoSyncConfig.FrameWidth;
            int height = this._videoSyncConfig.FrameHeight;

            if (frames == null || frames.Count == 0)
            {
                return;
            }

            profile = this.GetCachedCropProfile(filePath);
            if (profile == null || !profile.Enabled)
            {
                return;
            }

            this.ApplyBorderCropProfile(frames, profile, width, height);
        }

        #endregion

        #region Metodi privati - Profilo globale

        /// <summary>
        /// Estrae i frame campione globali ai punti 20/40/60/80% della durata
        /// </summary>
        /// <param name="filePath">File video da campionare</param>
        /// <param name="durationMs">Durata nota in millisecondi, oppure 0 per leggerla da ffmpeg</param>
        /// <param name="cropTo43">Compatibilita' storica del servizio estrazione frame</param>
        /// <returns>Frame grayscale campionati</returns>
        private List<byte[]> ExtractGlobalSampleFrames(string filePath, int durationMs, bool cropTo43)
        {
            List<byte[]> result = new List<byte[]>();
            int[] percentages = new int[] { 20, 40, 60, 80 };
            int actualDurationMs = durationMs;
            FrameExtractionService extractor;
            List<byte[]> frames;
            if (actualDurationMs <= 0)
            {
                // La durata puo' mancare in alcuni percorsi: la leggiamo solo quando serve davvero
                FfmpegVideoInfoReader reader = new FfmpegVideoInfoReader(this._ffmpegPath, this._ffmpegConfig, this._logSection);
                reader.TryRead(filePath, out actualDurationMs, out _);
            }

            if (actualDurationMs <= SAMPLE_DURATION_MS)
            {
                return result;
            }

            extractor = new FrameExtractionService(this._ffmpegPath, this._videoSyncConfig, this._ffmpegConfig, this._logSection);

            for (int i = 0; i < percentages.Length; i++)
            {
                // Ogni campione e' centrato sulla percentuale scelta, con clamp agli estremi del file
                int centerMs = (int)((long)actualDurationMs * percentages[i] / 100);
                int startMs = centerMs - (SAMPLE_DURATION_MS / 2);
                if (startMs < 0) { startMs = 0; }
                if (startMs + SAMPLE_DURATION_MS > actualDurationMs) { startMs = actualDurationMs - SAMPLE_DURATION_MS; }
                if (startMs < 0) { startMs = 0; }

                extractor.ExtractSegment(filePath, startMs, SAMPLE_DURATION_MS / 1000.0, 1.0, cropTo43, out frames, out _);
                if (frames != null && frames.Count > 0)
                {
                    result.Add(frames[0]);
                }
            }

            return result;
        }

        /// <summary>
        /// Costruisce il profilo crop solo se i bordi sono stabili su tutti i campioni globali
        /// </summary>
        /// <param name="frames">Frame campione gia' estratti</param>
        /// <returns>Profilo crop, disabilitato se i bordi non sono affidabili</returns>
        private BorderCropProfile BuildProfile(List<byte[]> frames)
        {
            BorderCropProfile profile = new BorderCropProfile();
            int width = this._videoSyncConfig.FrameWidth;
            int height = this._videoSyncConfig.FrameHeight;
            int leftMargin;
            int rightMargin;
            int topMargin;
            int bottomMargin;
            if (frames == null || frames.Count < 3)
            {
                return profile;
            }

            // Ogni lato e' valutato indipendentemente: non assumiamo bordi simmetrici
            leftMargin = this.DetectVerticalSideMargin(frames, true, width, height);
            rightMargin = this.DetectVerticalSideMargin(frames, false, width, height);
            topMargin = this.DetectHorizontalSideMargin(frames, true, width, height);
            bottomMargin = this.DetectHorizontalSideMargin(frames, false, width, height);

            profile.Left = leftMargin;
            profile.Right = width - 1 - rightMargin;
            profile.Top = topMargin;
            profile.Bottom = height - 1 - bottomMargin;
            profile.CropWidth = profile.Right - profile.Left + 1;
            profile.CropHeight = profile.Bottom - profile.Top + 1;
            profile.Enabled = leftMargin > 0 || rightMargin > 0 || topMargin > 0 || bottomMargin > 0;

            if (profile.CropWidth < width / 2 || profile.CropHeight < height / 2)
            {
                // Guard rail contro falsi positivi catastrofici su scene buie o titoli quasi neri
                profile = new BorderCropProfile();
            }

            return profile;
        }

        /// <summary>
        /// Rileva il margine nero stabile su lato sinistro o destro
        /// </summary>
        /// <param name="frames">Frame campione</param>
        /// <param name="leftSide">True per sinistra, false per destra</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="height">Altezza frame</param>
        /// <returns>Margine stabile in pixel</returns>
        private int DetectVerticalSideMargin(List<byte[]> frames, bool leftSide, int width, int height)
        {
            List<int> margins = new List<int>();
            int[] scanYs = new int[] { height / 4, height / 2, (height * 3) / 4 };
            int maxMargin = width / MAX_AUTO_CROP_DIVISOR;

            for (int f = 0; f < frames.Count; f++)
            {
                for (int i = 0; i < scanYs.Length; i++)
                {
                    // Scansioniamo tre righe per frame per evitare che un dettaglio locale condizioni il crop
                    margins.Add(this.MeasureVerticalLineMargin(frames[f], leftSide, width, scanYs[i], maxMargin));
                }
            }

            return this.StableMargin(margins);
        }

        /// <summary>
        /// Rileva il margine nero stabile su lato superiore o inferiore
        /// </summary>
        /// <param name="frames">Frame campione</param>
        /// <param name="topSide">True per alto, false per basso</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="height">Altezza frame</param>
        /// <returns>Margine stabile in pixel</returns>
        private int DetectHorizontalSideMargin(List<byte[]> frames, bool topSide, int width, int height)
        {
            List<int> margins = new List<int>();
            int[] scanXs = new int[] { width / 4, width / 2, (width * 3) / 4 };
            int maxMargin = height / MAX_AUTO_CROP_DIVISOR;

            for (int f = 0; f < frames.Count; f++)
            {
                for (int i = 0; i < scanXs.Length; i++)
                {
                    // Scansioniamo tre colonne per frame per verificare che il bordo non sia locale
                    margins.Add(this.MeasureHorizontalLineMargin(frames[f], topSide, width, height, scanXs[i], maxMargin));
                }
            }

            return this.StableMargin(margins);
        }

        /// <summary>
        /// Misura quanti pixel neri contigui ci sono su una riga da sinistra o destra
        /// </summary>
        /// <param name="frame">Frame grayscale</param>
        /// <param name="leftSide">True per scansione da sinistra</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="y">Riga da analizzare</param>
        /// <param name="maxMargin">Limite massimo crop consentito</param>
        /// <returns>Margine valido, oppure 0</returns>
        private int MeasureVerticalLineMargin(byte[] frame, bool leftSide, int width, int y, int maxMargin)
        {
            int margin = 0;
            int start = leftSide ? 0 : width - 1;
            int step = leftSide ? 1 : -1;
            int x = start;

            while (margin < maxMargin && x >= 0 && x < width && frame[y * width + x] <= BLACK_THRESHOLD)
            {
                margin++;
                x += step;
            }

            // Un bordo e' valido solo se subito dopo esiste contenuto sufficientemente piu' luminoso
            if (margin < MIN_BORDER_MARGIN || !this.HasVerticalContentContrast(frame, leftSide, width, y, margin))
            {
                margin = 0;
            }

            return margin;
        }

        /// <summary>
        /// Misura quanti pixel neri contigui ci sono su una colonna dall'alto o dal basso
        /// </summary>
        /// <param name="frame">Frame grayscale</param>
        /// <param name="topSide">True per scansione dall'alto</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="height">Altezza frame</param>
        /// <param name="x">Colonna da analizzare</param>
        /// <param name="maxMargin">Limite massimo crop consentito</param>
        /// <returns>Margine valido, oppure 0</returns>
        private int MeasureHorizontalLineMargin(byte[] frame, bool topSide, int width, int height, int x, int maxMargin)
        {
            int margin = 0;
            int start = topSide ? 0 : height - 1;
            int step = topSide ? 1 : -1;
            int y = start;

            while (margin < maxMargin && y >= 0 && y < height && frame[y * width + x] <= BLACK_THRESHOLD)
            {
                margin++;
                y += step;
            }

            // Un bordo e' valido solo se subito dopo esiste contenuto sufficientemente piu' luminoso
            if (margin < MIN_BORDER_MARGIN || !this.HasHorizontalContentContrast(frame, topSide, width, height, x, margin))
            {
                margin = 0;
            }

            return margin;
        }

        /// <summary>
        /// Riduce le misure raccolte a un margine stabile usando soglia di presenza e tolleranza sulla mediana
        /// </summary>
        /// <param name="margins">Misure raccolte sui frame campione</param>
        /// <returns>Margine stabile, oppure 0</returns>
        private int StableMargin(List<int> margins)
        {
            int result = 0;
            List<int> nonZero = new List<int>();
            int median;
            int tolerance;
            int stableCount = 0;
            int requiredCount;
            if (margins == null || margins.Count == 0)
            {
                return result;
            }

            for (int i = 0; i < margins.Count; i++)
            {
                if (margins[i] >= MIN_BORDER_MARGIN)
                {
                    nonZero.Add(margins[i]);
                }
            }

            // Almeno il 75% delle misure totali deve indicare un bordo reale
            requiredCount = (margins.Count * 75 + 99) / 100;
            if (nonZero.Count < requiredCount)
            {
                return result;
            }

            nonZero.Sort();
            median = nonZero[nonZero.Count / 2];
            tolerance = Math.Max(2, median / 10);

            // La stabilita' richiede che le misure non oscillino troppo attorno alla mediana
            for (int i = 0; i < nonZero.Count; i++)
            {
                if (Math.Abs(nonZero[i] - median) <= tolerance)
                {
                    stableCount++;
                }
            }

            if (stableCount >= requiredCount)
            {
                result = median;
            }

            return result;
        }

        /// <summary>
        /// Verifica che dopo il bordo verticale ci sia contenuto con contrasto sufficiente
        /// </summary>
        /// <param name="frame">Frame grayscale</param>
        /// <param name="leftSide">True per lato sinistro</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="y">Riga analizzata</param>
        /// <param name="margin">Margine misurato</param>
        /// <returns>True se il margine non e' solo una scena scura</returns>
        private bool HasVerticalContentContrast(byte[] frame, bool leftSide, int width, int y, int margin)
        {
            bool result;
            int borderAvg;
            int contentAvg;
            int borderStart = leftSide ? Math.Max(0, margin - 4) : Math.Min(width - 4, width - margin);
            int contentStart = leftSide ? margin : Math.Max(0, width - margin - 4);

            // Confrontiamo una piccola fascia bordo con la fascia immediatamente interna
            borderAvg = this.AverageHorizontalRun(frame, width, y, borderStart, 4);
            contentAvg = this.AverageHorizontalRun(frame, width, y, contentStart, 4);
            result = contentAvg >= MIN_CONTENT_AVG && contentAvg >= borderAvg + MIN_BORDER_CONTENT_CONTRAST;

            return result;
        }

        /// <summary>
        /// Verifica che dopo il bordo orizzontale ci sia contenuto con contrasto sufficiente
        /// </summary>
        /// <param name="frame">Frame grayscale</param>
        /// <param name="topSide">True per lato superiore</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="height">Altezza frame</param>
        /// <param name="x">Colonna analizzata</param>
        /// <param name="margin">Margine misurato</param>
        /// <returns>True se il margine non e' solo una scena scura</returns>
        private bool HasHorizontalContentContrast(byte[] frame, bool topSide, int width, int height, int x, int margin)
        {
            bool result;
            int borderAvg;
            int contentAvg;
            int borderStart = topSide ? Math.Max(0, margin - 4) : Math.Min(height - 4, height - margin);
            int contentStart = topSide ? margin : Math.Max(0, height - margin - 4);

            // Confrontiamo una piccola fascia bordo con la fascia immediatamente interna
            borderAvg = this.AverageVerticalRun(frame, width, height, x, borderStart, 4);
            contentAvg = this.AverageVerticalRun(frame, width, height, x, contentStart, 4);
            result = contentAvg >= MIN_CONTENT_AVG && contentAvg >= borderAvg + MIN_BORDER_CONTENT_CONTRAST;

            return result;
        }

        #endregion

        #region Metodi privati - Cache/applicazione

        /// <summary>
        /// Recupera il profilo crop calcolato per il file
        /// </summary>
        /// <param name="filePath">File video</param>
        /// <returns>Profilo crop, oppure null</returns>
        private BorderCropProfile GetCachedCropProfile(string filePath)
        {
            BorderCropProfile profile;
            lock (this._lock)
            {
                this._cache.TryGetValue(filePath, out profile);
            }

            return profile;
        }

        /// <summary>
        /// Memorizza il profilo crop in cache per la durata dell'analisi corrente
        /// </summary>
        /// <param name="filePath">File video</param>
        /// <param name="profile">Profilo da memorizzare</param>
        private void StoreCropProfile(string filePath, BorderCropProfile profile)
        {
            lock (this._lock)
            {
                if (!this._cache.ContainsKey(filePath))
                {
                    this._cache.Add(filePath, profile);
                }
            }
        }

        /// <summary>
        /// Costruisce un nome file compatto per i log autocrop
        /// </summary>
        /// <param name="filePath">Path completo</param>
        /// <returns>Nome cartella/file</returns>
        private string GetLogFileName(string filePath)
        {
            string result = Path.GetFileName(filePath);
            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                result = Path.GetFileName(directory) + "/" + result;
            }

            return result;
        }

        /// <summary>
        /// Applica il profilo crop e riporta ogni frame alla risoluzione di lavoro
        /// </summary>
        /// <param name="frames">Frame da modificare in-place</param>
        /// <param name="profile">Profilo crop</param>
        /// <param name="width">Larghezza frame di lavoro</param>
        /// <param name="height">Altezza frame di lavoro</param>
        private void ApplyBorderCropProfile(List<byte[]> frames, BorderCropProfile profile, int width, int height)
        {
            byte[] output;

            for (int i = 0; i < frames.Count; i++)
            {
                // Ogni frame viene riscalato alla geometria originale per non rompere il matcher esistente
                output = new byte[width * height];
                this.ResizeCropNearest(frames[i], output, width, height, profile.Left, profile.Top, profile.CropWidth, profile.CropHeight);
                frames[i] = output;
            }
        }

        /// <summary>
        /// Calcola la luminanza media di una sequenza orizzontale
        /// </summary>
        /// <param name="frame">Frame grayscale</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="y">Riga</param>
        /// <param name="startX">Colonna iniziale</param>
        /// <param name="count">Numero pixel da leggere</param>
        /// <returns>Luminanza media, oppure 255 senza campioni</returns>
        private int AverageHorizontalRun(byte[] frame, int width, int y, int startX, int count)
        {
            int endX = Math.Min(width, startX + count);
            int sum = 0;
            int samples = 0;
            if (startX < 0) { startX = 0; }

            for (int x = startX; x < endX; x++)
            {
                sum += frame[y * width + x];
                samples++;
            }

            return samples > 0 ? sum / samples : 255;
        }

        /// <summary>
        /// Calcola la luminanza media di una sequenza verticale
        /// </summary>
        /// <param name="frame">Frame grayscale</param>
        /// <param name="width">Larghezza frame</param>
        /// <param name="height">Altezza frame</param>
        /// <param name="x">Colonna</param>
        /// <param name="startY">Riga iniziale</param>
        /// <param name="count">Numero pixel da leggere</param>
        /// <returns>Luminanza media, oppure 255 senza campioni</returns>
        private int AverageVerticalRun(byte[] frame, int width, int height, int x, int startY, int count)
        {
            int endY = Math.Min(height, startY + count);
            int sum = 0;
            int samples = 0;
            if (startY < 0) { startY = 0; }

            for (int y = startY; y < endY; y++)
            {
                sum += frame[y * width + x];
                samples++;
            }

            return samples > 0 ? sum / samples : 255;
        }

        /// <summary>
        /// Esegue crop e resize nearest-neighbor su frame grayscale
        /// </summary>
        /// <param name="input">Frame sorgente</param>
        /// <param name="output">Frame destinazione</param>
        /// <param name="width">Larghezza output</param>
        /// <param name="height">Altezza output</param>
        /// <param name="cropLeft">X iniziale crop</param>
        /// <param name="cropTop">Y iniziale crop</param>
        /// <param name="cropWidth">Larghezza crop</param>
        /// <param name="cropHeight">Altezza crop</param>
        private void ResizeCropNearest(byte[] input, byte[] output, int width, int height, int cropLeft, int cropTop, int cropWidth, int cropHeight)
        {
            int srcY;
            int srcX;
            int srcRow;
            int dstRow;
            for (int y = 0; y < height; y++)
            {
                srcY = cropTop + ((y * cropHeight) / height);
                srcRow = srcY * width;
                dstRow = y * width;

                for (int x = 0; x < width; x++)
                {
                    srcX = cropLeft + ((x * cropWidth) / width);
                    output[dstRow + x] = input[srcRow + srcX];
                }
            }
        }

        #endregion

        #region Classi annidate

        /// <summary>
        /// Profilo crop globale calcolato per un file durante l'analisi corrente
        /// </summary>
        private class BorderCropProfile
        {
            /// <summary>
            /// True se il profilo deve essere applicato
            /// </summary>
            public bool Enabled;

            /// <summary>
            /// Coordinata sinistra inclusiva del contenuto
            /// </summary>
            public int Left;

            /// <summary>
            /// Coordinata destra inclusiva del contenuto
            /// </summary>
            public int Right;

            /// <summary>
            /// Coordinata superiore inclusiva del contenuto
            /// </summary>
            public int Top;

            /// <summary>
            /// Coordinata inferiore inclusiva del contenuto
            /// </summary>
            public int Bottom;

            /// <summary>
            /// Larghezza crop
            /// </summary>
            public int CropWidth;

            /// <summary>
            /// Altezza crop
            /// </summary>
            public int CropHeight;
        }

        #endregion
    }
}
