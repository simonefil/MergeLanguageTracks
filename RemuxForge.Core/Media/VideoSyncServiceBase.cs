using RemuxForge.Core.Configuration;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Media.Ffmpeg;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;

namespace RemuxForge.Core.Media
{
    /// <summary>
    /// Classe base per servizi di sincronizzazione video tramite confronto frame
    /// </summary>
    public abstract class VideoSyncServiceBase
    {
        #region Variabili di classe

        /// <summary>
        /// Riferimento alla configurazione VideoSync (binding diretto, modifiche immediate)
        /// </summary>
        protected VideoSyncConfig _vsConfig;

        /// <summary>
        /// Riferimento alla configurazione Ffmpeg (binding diretto, modifiche immediate)
        /// </summary>
        protected FfmpegConfig _ffmpegConfig;

        /// <summary>
        /// Percorso eseguibile ffmpeg
        /// </summary>
        protected string _ffmpegPath;

        /// <summary>
        /// Sezione di log per messaggi
        /// </summary>
        private readonly LogSection _logSection;

        /// <summary>
        /// Croppa i frame del file sorgente a 4:3 centrato quando richiesto dall'autocrop geometry
        /// </summary>
        protected bool _cropSourceTo43;

        /// <summary>
        /// Croppa i frame del file lingua a 4:3 centrato quando richiesto dall'autocrop geometry
        /// </summary>
        protected bool _cropLangTo43;

        /// <summary>
        /// Analyzer geometry video condiviso dal servizio
        /// </summary>
        private readonly VideoGeometryAnalyzer _geometryAnalyzer;

        /// <summary>
        /// Normalizzatore bordi neri condiviso dal servizio
        /// </summary>
        private readonly BlackBorderNormalizer _blackBorderNormalizer;

        /// <summary>
        /// Rilevatore tagli scena condiviso dal servizio
        /// </summary>
        private readonly SceneCutDetector _sceneCutDetector;

        /// <summary>
        /// Calcolatore metriche visuali condiviso dal servizio
        /// </summary>
        private readonly VisualMetricCalculator _visualMetricCalculator;

        /// <summary>
        /// Ultima geometria diagnostica sorgente preparata dal servizio
        /// </summary>
        protected FrameSyncGeometryInfo _lastSourceGeometryInfo;

        /// <summary>
        /// Ultima geometria diagnostica lingua preparata dal servizio
        /// </summary>
        protected FrameSyncGeometryInfo _lastLanguageGeometryInfo;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="ffmpegPath">Percorso eseguibile ffmpeg</param>
        /// <param name="logSection">Sezione di log per messaggi</param>
        protected VideoSyncServiceBase(string ffmpegPath, LogSection logSection)
        {
            this._ffmpegPath = ffmpegPath;
            this._logSection = logSection;
            this._vsConfig = AppSettingsService.Instance.Settings.Advanced.VideoSync;
            this._ffmpegConfig = AppSettingsService.Instance.Settings.Advanced.Ffmpeg;
            this._cropSourceTo43 = false;
            this._cropLangTo43 = false;
            this._geometryAnalyzer = new VideoGeometryAnalyzer(this._ffmpegPath, this._ffmpegConfig, this._logSection);
            this._blackBorderNormalizer = new BlackBorderNormalizer(this._ffmpegPath, this._vsConfig, this._ffmpegConfig, this._logSection, this._geometryAnalyzer);
            this._sceneCutDetector = new SceneCutDetector(this._vsConfig);
            this._visualMetricCalculator = new VisualMetricCalculator(this._vsConfig);
            this._lastSourceGeometryInfo = null;
            this._lastLanguageGeometryInfo = null;
        }

        #endregion

        #region Metodi protetti

        /// <summary>
        /// Estrae frame di un segmento video come byte array grayscale
        /// Ritorna timestamps assoluti reali (ms nel file sorgente) parsati da ffmpeg showinfo,
        /// rendendo il matching robusto anche su file VFR
        /// </summary>
        /// <param name="filePath">Percorso file video</param>
        /// <param name="startMs">Inizio estrazione in millisecondi</param>
        /// <param name="durationSec">Durata estrazione in secondi</param>
        /// <param name="targetFps">FPS target per normalizzazione (0 = passthrough senza decimazione)</param>
        /// <param name="cropTo43">Se true, croppa il frame a 4:3 centrato prima dello scale (rimuove pillarbox)</param>
        /// <param name="frames">Lista frame grayscale estratti (output)</param>
        /// <param name="timestampsMs">Array di timestamp assoluti in ms, uno per frame estratto (output)</param>
        protected void ExtractSegment(string filePath, int startMs, double durationSec, double targetFps, bool cropTo43, out List<byte[]> frames, out double[] timestampsMs)
        {
            FrameExtractionService extractor = new FrameExtractionService(this._ffmpegPath, this._vsConfig, this._ffmpegConfig, this._logSection);
            extractor.ExtractSegment(filePath, startMs, durationSec, targetFps, cropTo43, out frames, out timestampsMs);
            this.NormalizeBlackBorders(filePath, frames);
        }

        /// <summary>
        /// Ricerca binaria dell'indice del timestamp piu' vicino al target
        /// </summary>
        /// <param name="timestampsMs">Array di timestamp ordinato in modo crescente</param>
        /// <param name="targetMs">Timestamp target da cercare</param>
        /// <returns>Indice del timestamp piu' vicino, -1 se array vuoto</returns>
        protected static int NearestTimestampIndex(double[] timestampsMs, double targetMs)
        {
            int result = -1;
            int low;
            int high;
            int mid;
            double leftDist;
            double rightDist;
            if (timestampsMs != null && timestampsMs.Length > 0)
            {
                low = 0;
                high = timestampsMs.Length - 1;

                // Binary search per il primo indice con timestamp >= target
                while (low < high)
                {
                    mid = (low + high) / 2;
                    if (timestampsMs[mid] < targetMs)
                    {
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                // Confronta con indice precedente per scegliere il piu' vicino
                result = low;
                if (low > 0)
                {
                    leftDist = Math.Abs(timestampsMs[low - 1] - targetMs);
                    rightDist = Math.Abs(timestampsMs[low] - targetMs);
                    if (leftDist < rightDist)
                    {
                        result = low - 1;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Analizza e memorizza in cache la geometria video rilevante per il frame matching
        /// </summary>
        /// <param name="filePath">Percorso file video</param>
        /// <returns>Profilo geometria, o null se non rilevabile</returns>
        protected VideoGeometryProfile AnalyzeVideoGeometry(string filePath)
        {
            return this._geometryAnalyzer.Analyze(filePath);
        }

        /// <summary>
        /// Logga il confronto geometrico tra sorgente e lingua
        /// </summary>
        /// <param name="sourceGeometry">Geometria sorgente</param>
        /// <param name="languageGeometry">Geometria lingua</param>
        protected void LogVideoGeometryComparison(VideoGeometryProfile sourceGeometry, VideoGeometryProfile languageGeometry)
        {
            double aspectDiff;
            bool geometryMismatch;
            if (sourceGeometry == null || languageGeometry == null)
            {
                return;
            }

            aspectDiff = Math.Abs(sourceGeometry.DisplayAspect - languageGeometry.DisplayAspect);
            geometryMismatch = sourceGeometry.Width != languageGeometry.Width || sourceGeometry.Height != languageGeometry.Height || sourceGeometry.SarNum != languageGeometry.SarNum || sourceGeometry.SarDen != languageGeometry.SarDen || aspectDiff > 0.01;

            ConsoleHelper.Write(this._logSection, LogLevel.Debug, "  Geometry source: " + sourceGeometry.ToShortString());
            ConsoleHelper.Write(this._logSection, LogLevel.Debug, "  Geometry lang:   " + languageGeometry.ToShortString());

            if (geometryMismatch)
            {
                ConsoleHelper.Write(this._logSection, LogLevel.Notice, "  Geometry mismatch: normalizzazione SAR/DAR e auto-crop attive");
            }
        }

        /// <summary>
        /// Analizza geometria source/lang e applica il crop 4:3 automatico quando il confronto e' pillarbox vs 4:3 nativo
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="languageFile">File lingua</param>
        protected void PrepareGeometryDrivenCrop(string sourceFile, string languageFile)
        {
            VideoGeometryProfile sourceGeometry;
            VideoGeometryProfile languageGeometry;
            this._lastSourceGeometryInfo = null;
            this._lastLanguageGeometryInfo = null;
            this._cropSourceTo43 = false;
            this._cropLangTo43 = false;
            this._blackBorderNormalizer.Reset();

            sourceGeometry = this.AnalyzeVideoGeometry(sourceFile);
            languageGeometry = this.AnalyzeVideoGeometry(languageFile);

            this._blackBorderNormalizer.PrepareFile(sourceFile, 0, false);
            this._blackBorderNormalizer.PrepareFile(languageFile, 0, false);

            this.LogVideoGeometryComparison(sourceGeometry, languageGeometry);
            this.ApplyGeometryDrivenCrop(sourceGeometry, languageGeometry);

            if (this._cropSourceTo43 || this._cropLangTo43)
            {
                this._blackBorderNormalizer.Reset();
                this._blackBorderNormalizer.PrepareFile(sourceFile, 0, this._cropSourceTo43);
                this._blackBorderNormalizer.PrepareFile(languageFile, 0, this._cropLangTo43);
            }

            this._lastSourceGeometryInfo = this.BuildGeometryInfo(sourceGeometry, false, this._cropSourceTo43);
            this._lastLanguageGeometryInfo = this.BuildGeometryInfo(languageGeometry, false, this._cropLangTo43);
        }

        /// <summary>
        /// Applica crop 4:3 automatico quando la geometria indica un confronto pillarbox 16:9 contro nativo 4:3
        /// </summary>
        /// <param name="sourceGeometry">Geometria sorgente</param>
        /// <param name="languageGeometry">Geometria lingua</param>
        protected void ApplyGeometryDrivenCrop(VideoGeometryProfile sourceGeometry, VideoGeometryProfile languageGeometry)
        {
            bool source43 = this.IsDisplayAspect43(sourceGeometry);
            bool language43 = this.IsDisplayAspect43(languageGeometry);
            bool sourceWide = this.IsDisplayAspectWide(sourceGeometry);
            bool languageWide = this.IsDisplayAspectWide(languageGeometry);
            bool sourceSquare = this.IsSquarePixelGeometry(sourceGeometry);
            bool languageSquare = this.IsSquarePixelGeometry(languageGeometry);
            bool sourceHasBorders = sourceGeometry != null && sourceGeometry.HasBlackBorderCrop;
            bool languageHasBorders = languageGeometry != null && languageGeometry.HasBlackBorderCrop;

            if (sourceGeometry == null || languageGeometry == null)
            {
                return;
            }

            if (sourceWide && language43 && sourceSquare && sourceHasBorders)
            {
                this._cropSourceTo43 = true;
                ConsoleHelper.Write(this._logSection, LogLevel.Notice, "  Geometry crop source 4:3 attivo: source wide/pillarbox vs lang 4:3");
            }

            if (languageWide && source43 && languageSquare && languageHasBorders)
            {
                this._cropLangTo43 = true;
                ConsoleHelper.Write(this._logSection, LogLevel.Notice, "  Geometry crop lang 4:3 attivo: lang wide/pillarbox vs source 4:3");
            }
        }

        /// <summary>
        /// Verifica se la geometria display e' circa 4:3
        /// </summary>
        /// <param name="geometry">Profilo geometria</param>
        /// <returns>True se aspect circa 4:3</returns>
        protected bool IsDisplayAspect43(VideoGeometryProfile geometry)
        {
            bool result = false;
            if (geometry != null)
            {
                result = geometry.DisplayAspect >= 1.28 && geometry.DisplayAspect <= 1.39;
            }

            return result;
        }

        /// <summary>
        /// Verifica se la geometria display e' circa wide 16:9
        /// </summary>
        /// <param name="geometry">Profilo geometria</param>
        /// <returns>True se aspect circa 16:9</returns>
        protected bool IsDisplayAspectWide(VideoGeometryProfile geometry)
        {
            bool result = false;
            if (geometry != null)
            {
                result = geometry.DisplayAspect >= 1.70 && geometry.DisplayAspect <= 1.86;
            }

            return result;
        }

        /// <summary>
        /// Verifica se la geometria usa pixel quasi quadrati
        /// </summary>
        /// <param name="geometry">Profilo geometria</param>
        /// <returns>True se SAR circa 1:1</returns>
        protected bool IsSquarePixelGeometry(VideoGeometryProfile geometry)
        {
            bool result = false;
            double sar;

            if (geometry != null && geometry.SarDen > 0)
            {
                sar = geometry.SarNum / (double)geometry.SarDen;
                result = Math.Abs(sar - 1.0) <= 0.02;
            }

            return result;
        }

        /// <summary>
        /// Crea DTO diagnostico dalla geometria interna
        /// </summary>
        /// <param name="geometry">Profilo geometria interno</param>
        /// <param name="manualCropTo43">True se crop 4:3 richiesto manualmente</param>
        /// <param name="geometryCropTo43">True se crop 4:3 attivato dalla geometria</param>
        /// <returns>DTO diagnostico</returns>
        protected FrameSyncGeometryInfo BuildGeometryInfo(VideoGeometryProfile geometry, bool manualCropTo43, bool geometryCropTo43)
        {
            FrameSyncGeometryInfo result = null;
            if (geometry != null)
            {
                result = new FrameSyncGeometryInfo();
                result.FilePath = geometry.FilePath;
                result.Width = geometry.Width;
                result.Height = geometry.Height;
                result.SarNum = geometry.SarNum;
                result.SarDen = geometry.SarDen;
                result.DarNum = geometry.DarNum;
                result.DarDen = geometry.DarDen;
                result.DisplayWidth = geometry.DisplayWidth;
                result.DisplayHeight = geometry.DisplayHeight;
                result.DisplayAspect = geometry.DisplayAspect;
                result.HasBlackBorderCrop = geometry.HasBlackBorderCrop;
                result.CropLeft = geometry.CropLeft;
                result.CropRight = geometry.CropRight;
                result.CropTop = geometry.CropTop;
                result.CropBottom = geometry.CropBottom;
                result.ManualCropTo43 = manualCropTo43;
                result.GeometryCropTo43 = geometryCropTo43;
                if (manualCropTo43)
                {
                    result.CropMode = "manual_43";
                }
                else if (geometryCropTo43)
                {
                    result.CropMode = "geometry_43";
                }
                else if (geometry.HasBlackBorderCrop)
                {
                    result.CropMode = "black_border_autocrop";
                }
                else
                {
                    result.CropMode = "none";
                }
            }

            return result;
        }

        /// <summary>
        /// Rileva bordi neri stabili nel segmento e normalizza i frame croppando e riscalando
        /// alla risoluzione di analisi. Esegue lavoro solo se il crop rilevato e' significativo
        /// </summary>
        /// <param name="filePath">Percorso file, usato come chiave cache profilo</param>
        /// <param name="frames">Frame grayscale da normalizzare in-place</param>
        protected void NormalizeBlackBorders(string filePath, List<byte[]> frames)
        {
            this._blackBorderNormalizer.Normalize(filePath, frames);
        }

        /// <summary>
        /// Calcola MSE tra due frame grayscale
        /// </summary>
        /// <param name="frame1">Primo frame grayscale</param>
        /// <param name="frame2">Secondo frame grayscale</param>
        /// <returns>Valore MSE calcolato</returns>
        protected double ComputeMse(byte[] frame1, byte[] frame2)
        {
            return this._visualMetricCalculator.ComputeMse(frame1, frame2);
        }

        /// <summary>
        /// Calcola SSIM (Structural Similarity Index) tra due frame grayscale
        /// Restituisce un valore tra 0.0 (completamente diversi) e 1.0 (identici)
        /// Robusto rispetto a differenze di compressione, luminosita' e crop
        /// </summary>
        /// <param name="frame1">Primo frame grayscale</param>
        /// <param name="frame2">Secondo frame grayscale</param>
        /// <returns>Valore SSIM tra 0.0 e 1.0</returns>
        protected double ComputeSsim(byte[] frame1, byte[] frame2)
        {
            return this._visualMetricCalculator.ComputeSsim(frame1, frame2);
        }

        /// <summary>
        /// Calcola SSIM medio di una sequenza di frame consecutivi (confronto cross-file)
        /// </summary>
        /// <param name="sourceFrames">Lista frame sorgente</param>
        /// <param name="sourceStartIdx">Indice iniziale nei frame sorgente</param>
        /// <param name="langFrames">Lista frame lingua</param>
        /// <param name="langStartIdx">Indice iniziale nei frame lingua</param>
        /// <param name="sequenceLength">Numero di frame nella sequenza</param>
        /// <returns>SSIM medio della sequenza o 0.0 se frame insufficienti</returns>
        protected double ComputeSequenceSsim(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            return this._visualMetricCalculator.ComputeSequenceSsim(sourceFrames, sourceStartIdx, langFrames, langStartIdx, sequenceLength);
        }

        /// <summary>
        /// Calcola correlazione blur media su una sequenza
        /// </summary>
        protected double ComputeSequenceBlurredCorrelation(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            return this._visualMetricCalculator.ComputeSequenceBlurredCorrelation(sourceFrames, sourceStartIdx, langFrames, langStartIdx, sequenceLength);
        }

        /// <summary>
        /// Calcola correlazione edge media di una sequenza di frame consecutivi
        /// </summary>
        protected double ComputeSequenceEdgeCorrelation(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            return this._visualMetricCalculator.ComputeSequenceEdgeCorrelation(sourceFrames, sourceStartIdx, langFrames, langStartIdx, sequenceLength);
        }

        /// <summary>
        /// Calcola correlazione media dei fingerprint a blocchi su una sequenza di frame
        /// </summary>
        protected double ComputeSequenceBlockCorrelation(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            return this._visualMetricCalculator.ComputeSequenceBlockCorrelation(sourceFrames, sourceStartIdx, langFrames, langStartIdx, sequenceLength);
        }

        /// <summary>
        /// Calcola correlazione media edge-block su una sequenza
        /// </summary>
        protected double ComputeSequenceEdgeBlockCorrelation(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            return this._visualMetricCalculator.ComputeSequenceEdgeBlockCorrelation(sourceFrames, sourceStartIdx, langFrames, langStartIdx, sequenceLength);
        }

        /// <summary>
        /// Calcola correlazione media block-motion su una sequenza
        /// </summary>
        protected double ComputeSequenceBlockMotionCorrelation(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            return this._visualMetricCalculator.ComputeSequenceBlockMotionCorrelation(sourceFrames, sourceStartIdx, langFrames, langStartIdx, sequenceLength);
        }

        /// <summary>
        /// Calcola similarita' media aHash/dHash su una sequenza
        /// </summary>
        protected double ComputeSequenceHashSimilarity(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            return this._visualMetricCalculator.ComputeSequenceHashSimilarity(sourceFrames, sourceStartIdx, langFrames, langStartIdx, sequenceLength);
        }

        /// <summary>
        /// Rileva tagli di scena tramite MSE tra frame consecutivi
        /// </summary>
        /// <param name="frames">Lista frame grayscale</param>
        /// <returns>Lista indici frame dove avviene il taglio</returns>
        protected List<int> DetectSceneCuts(List<byte[]> frames)
        {
            return this._sceneCutDetector.Detect(frames);
        }

        /// <summary>
        /// Rileva tagli di scena tramite MSE tra frame consecutivi con soglia piu' permissiva
        /// Usato come fallback quando un segmento scuro/grainy non produce cut con la soglia conservativa
        /// </summary>
        /// <param name="frames">Lista frame grayscale</param>
        /// <returns>Lista indici frame dove avviene il taglio</returns>
        protected List<int> DetectSceneCutsRelaxed(List<byte[]> frames)
        {
            return this._sceneCutDetector.DetectRelaxed(frames);
        }

        /// <summary>
        /// Calcola il fingerprint temporale di un taglio di scena usando luma, edge e block-motion
        /// </summary>
        protected double[] ComputeTemporalFingerprint(List<byte[]> frames, int cutIndex)
        {
            return this._visualMetricCalculator.ComputeTemporalFingerprint(frames, cutIndex);
        }

        /// <summary>
        /// Calcola la correlazione di Pearson tra due fingerprint temporali
        /// </summary>
        protected double ComputeFingerprintCorrelation(double[] fp1, double[] fp2)
        {
            return this._visualMetricCalculator.ComputeFingerprintCorrelation(fp1, fp2);
        }

        #endregion

    }
}
