using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace RemuxForge.Core
{
    /// <summary>
    /// Classe base per servizi di sincronizzazione video tramite confronto frame
    /// </summary>
    public abstract class VideoSyncServiceBase
    {
        #region Variabili di classe

        /// <summary>
        /// Larghezza frame per confronto MSE
        /// </summary>
        protected int _frameWidth;

        /// <summary>
        /// Altezza frame per confronto MSE
        /// </summary>
        protected int _frameHeight;

        /// <summary>
        /// Dimensione in byte di un singolo frame grayscale (derivato: FrameWidth * FrameHeight)
        /// </summary>
        protected int _frameSize;

        /// <summary>
        /// Soglia MSE massima per match valido
        /// </summary>
        protected double _mseThreshold;

        /// <summary>
        /// Soglia MSE minima, sotto cui il match e' ambiguo
        /// </summary>
        protected double _mseMinThreshold;

        /// <summary>
        /// Soglia SSIM minima per match cross-file valido
        /// </summary>
        protected double _ssimThreshold;

        /// <summary>
        /// Soglia SSIM massima, sopra cui il match e' ambiguo (frame identici/neri)
        /// </summary>
        protected double _ssimMaxThreshold;

        /// <summary>
        /// Numero di punti di verifica
        /// </summary>
        protected int _numCheckPoints;

        /// <summary>
        /// Minimo punti verifica riusciti per sync valido
        /// </summary>
        protected int _minValidPoints;

        /// <summary>
        /// Soglia MSE tra frame consecutivi per rilevare taglio di scena
        /// </summary>
        protected double _sceneCutThreshold;

        /// <summary>
        /// Frame prima e dopo il taglio per la firma
        /// </summary>
        protected int _cutHalfWindow;

        /// <summary>
        /// Lunghezza firma taglio di scena (2 * CutHalfWindow)
        /// </summary>
        protected int _cutSignatureLength;

        /// <summary>
        /// Lunghezza fingerprint temporale (derivato: CutSignatureLength - 1)
        /// </summary>
        protected int _fingerprintLength;

        /// <summary>
        /// Soglia minima correlazione Pearson per match fingerprint temporale
        /// </summary>
        protected double _fingerprintCorrelationThreshold;

        /// <summary>
        /// Minimo tagli di scena richiesti per analisi
        /// </summary>
        protected int _minSceneCuts;

        /// <summary>
        /// Distanza minima in frame tra due tagli consecutivi
        /// </summary>
        protected int _minCutSpacingFrames;

        /// <summary>
        /// Durata segmento source per verifica punto in secondi
        /// </summary>
        protected int _verifySourceDurationSec;

        /// <summary>
        /// Durata segmento lang per verifica punto in secondi
        /// </summary>
        protected int _verifyLangDurationSec;

        /// <summary>
        /// Durata segmento source per retry verifica in secondi
        /// </summary>
        protected int _verifySourceRetrySec;

        /// <summary>
        /// Durata segmento lang per retry verifica in secondi
        /// </summary>
        protected int _verifyLangRetrySec;

        /// <summary>
        /// Percorso eseguibile ffmpeg
        /// </summary>
        protected string _ffmpegPath;

        /// <summary>
        /// Sezione di log per messaggi
        /// </summary>
        private LogSection _logSection;

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

            // Carica parametri da configurazione
            VideoSyncConfig cfg = AppSettingsService.Instance.Settings.Advanced.VideoSync;
            this._frameWidth = cfg.FrameWidth;
            this._frameHeight = cfg.FrameHeight;
            this._frameSize = cfg.FrameWidth * cfg.FrameHeight;
            this._mseThreshold = cfg.MseThreshold;
            this._mseMinThreshold = cfg.MseMinThreshold;
            this._ssimThreshold = cfg.SsimThreshold;
            this._ssimMaxThreshold = cfg.SsimMaxThreshold;
            this._numCheckPoints = cfg.NumCheckPoints;
            this._minValidPoints = cfg.MinValidPoints;
            this._sceneCutThreshold = cfg.SceneCutThreshold;
            this._cutHalfWindow = cfg.CutHalfWindow;
            this._cutSignatureLength = cfg.CutSignatureLength;
            this._fingerprintLength = cfg.CutSignatureLength - 1;
            this._fingerprintCorrelationThreshold = cfg.FingerprintCorrelationThreshold;
            this._minSceneCuts = cfg.MinSceneCuts;
            this._minCutSpacingFrames = cfg.MinCutSpacingFrames;
            this._verifySourceDurationSec = cfg.VerifySourceDurationSec;
            this._verifyLangDurationSec = cfg.VerifyLangDurationSec;
            this._verifySourceRetrySec = cfg.VerifySourceRetrySec;
            this._verifyLangRetrySec = cfg.VerifyLangRetrySec;
        }

        #endregion

        #region Metodi protetti

        /// <summary>
        /// Estrae frame di un segmento video come byte array grayscale
        /// </summary>
        /// <param name="filePath">Percorso file video</param>
        /// <param name="startMs">Inizio estrazione in millisecondi</param>
        /// <param name="durationSec">Durata estrazione in secondi</param>
        /// <param name="targetFps">FPS target per normalizzazione (0 = usa fps nativo del file)</param>
        /// <returns>Lista di frame grayscale come byte array</returns>
        protected List<byte[]> ExtractSegment(string filePath, int startMs, double durationSec, double targetFps)
        {
            List<byte[]> frames = new List<byte[]>();
            Process process = null;
            double startSec = 0.0;
            string startFormatted = "";
            string durationFormatted = "";
            string resolution = "";
            string fpsFilter = "";
            Stream stdoutStream = null;
            bool reading = true;
            byte[] frameData = null;
            int totalRead = 0;
            int bytesRead = 0;

            try
            {
                // Formatta timestamp e durata
                startSec = startMs / 1000.0;
                startFormatted = startSec.ToString("F3", CultureInfo.InvariantCulture);
                durationFormatted = durationSec.ToString("F3", CultureInfo.InvariantCulture);
                resolution = this._frameWidth + "x" + this._frameHeight;

                // Comando ffmpeg per estrazione raw grayscale via pipe
                process = new Process();
                process.StartInfo.FileName = this._ffmpegPath;
                process.StartInfo.ArgumentList.Add("-nostdin");
                process.StartInfo.ArgumentList.Add("-hide_banner");
                process.StartInfo.ArgumentList.Add("-hwaccel");
                process.StartInfo.ArgumentList.Add("auto");
                process.StartInfo.ArgumentList.Add("-ss");
                process.StartInfo.ArgumentList.Add(startFormatted);
                process.StartInfo.ArgumentList.Add("-i");
                process.StartInfo.ArgumentList.Add(filePath);
                process.StartInfo.ArgumentList.Add("-t");
                process.StartInfo.ArgumentList.Add(durationFormatted);

                // Filtro fps per normalizzare frame rate diversi (telecine, HFR, etc.)
                if (targetFps > 0.0)
                {
                    fpsFilter = "fps=fps=" + targetFps.ToString("F6", CultureInfo.InvariantCulture);
                    process.StartInfo.ArgumentList.Add("-vf");
                    process.StartInfo.ArgumentList.Add(fpsFilter);
                }

                process.StartInfo.ArgumentList.Add("-s");
                process.StartInfo.ArgumentList.Add(resolution);
                process.StartInfo.ArgumentList.Add("-pix_fmt");
                process.StartInfo.ArgumentList.Add("gray");
                process.StartInfo.ArgumentList.Add("-f");
                process.StartInfo.ArgumentList.Add("rawvideo");
                process.StartInfo.ArgumentList.Add("-");
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                // Svuota stderr in thread separato
                // Catch silenzioso intenzionale: pipe puo' chiudersi se il processo termina
                Thread errThread = new Thread(() =>
                {
                    try { process.StandardError.ReadToEnd(); }
                    catch { }
                });
                errThread.Start();

                // Legge frame consecutivi dal flusso binario stdout
                stdoutStream = process.StandardOutput.BaseStream;

                while (reading)
                {
                    frameData = new byte[this._frameSize];
                    totalRead = 0;

                    // Legge esattamente _frameSize byte per ogni frame
                    while (totalRead < this._frameSize)
                    {
                        bytesRead = stdoutStream.Read(frameData, totalRead, this._frameSize - totalRead);
                        if (bytesRead == 0)
                        {
                            reading = false;
                            break;
                        }
                        totalRead += bytesRead;
                    }

                    // Aggiunge il frame solo se completo
                    if (totalRead == this._frameSize)
                    {
                        frames.Add(frameData);
                    }
                }

                errThread.Join();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write(this._logSection, LogLevel.Warning, "  Errore ExtractSegment: " + ex.Message);
            }
            finally
            {
                if (process != null) { process.Dispose(); process = null; }
            }

            return frames;
        }

        /// <summary>
        /// Calcola MSE tra due frame grayscale
        /// </summary>
        /// <param name="frame1">Primo frame grayscale</param>
        /// <param name="frame2">Secondo frame grayscale</param>
        /// <returns>Valore MSE calcolato</returns>
        protected double ComputeMse(byte[] frame1, byte[] frame2)
        {
            double sumSquaredDiff = 0.0;
            int length = frame1.Length;
            double diff = 0.0;

            for (int i = 0; i < length; i++)
            {
                diff = (double)frame1[i] - (double)frame2[i];
                sumSquaredDiff += diff * diff;
            }

            double mse = sumSquaredDiff / length;

            return mse;
        }

        /// <summary>
        /// Calcola SSIM (Structural Similarity Index) tra due frame grayscale.
        /// Restituisce un valore tra 0.0 (completamente diversi) e 1.0 (identici).
        /// Robusto rispetto a differenze di compressione, luminosita' e crop
        /// </summary>
        /// <param name="frame1">Primo frame grayscale</param>
        /// <param name="frame2">Secondo frame grayscale</param>
        /// <returns>Valore SSIM tra 0.0 e 1.0</returns>
        protected double ComputeSsim(byte[] frame1, byte[] frame2)
        {
            int length = frame1.Length;
            double mean1 = 0.0;
            double mean2 = 0.0;
            double variance1 = 0.0;
            double variance2 = 0.0;
            double covariance = 0.0;
            double diff1 = 0.0;
            double diff2 = 0.0;

            // Costanti SSIM (L=255, K1=0.01, K2=0.03)
            double C1 = 6.5025;   // (0.01 * 255)^2
            double C2 = 58.5225;  // (0.03 * 255)^2

            // Calcolo medie
            for (int i = 0; i < length; i++)
            {
                mean1 += frame1[i];
                mean2 += frame2[i];
            }
            mean1 /= length;
            mean2 /= length;

            // Calcolo varianze e covarianza
            for (int i = 0; i < length; i++)
            {
                diff1 = frame1[i] - mean1;
                diff2 = frame2[i] - mean2;
                variance1 += diff1 * diff1;
                variance2 += diff2 * diff2;
                covariance += diff1 * diff2;
            }
            variance1 /= length;
            variance2 /= length;
            covariance /= length;

            // Formula SSIM
            double numerator = (2.0 * mean1 * mean2 + C1) * (2.0 * covariance + C2);
            double denominator = (mean1 * mean1 + mean2 * mean2 + C1) * (variance1 + variance2 + C2);

            return numerator / denominator;
        }

        /// <summary>
        /// Calcola MSE medio di una sequenza di frame consecutivi
        /// </summary>
        /// <param name="sourceFrames">Lista frame sorgente</param>
        /// <param name="sourceStartIdx">Indice iniziale nei frame sorgente</param>
        /// <param name="langFrames">Lista frame lingua</param>
        /// <param name="langStartIdx">Indice iniziale nei frame lingua</param>
        /// <param name="sequenceLength">Numero di frame nella sequenza</param>
        /// <returns>MSE medio della sequenza o double.MaxValue se insufficienti</returns>
        protected double ComputeSequenceMse(List<byte[]> sourceFrames, int sourceStartIdx, List<byte[]> langFrames, int langStartIdx, int sequenceLength)
        {
            double totalMse = 0.0;
            int validFrames = 0;
            double result = double.MaxValue;
            int srcIdx = 0;
            int lngIdx = 0;

            for (int k = 0; k < sequenceLength; k++)
            {
                srcIdx = sourceStartIdx + k;
                lngIdx = langStartIdx + k;

                if (srcIdx >= sourceFrames.Count || lngIdx >= langFrames.Count)
                {
                    break;
                }

                totalMse += this.ComputeMse(sourceFrames[srcIdx], langFrames[lngIdx]);
                validFrames++;
            }

            if (validFrames >= sequenceLength)
            {
                result = totalMse / validFrames;
            }

            return result;
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
            double totalSsim = 0.0;
            int validFrames = 0;
            double result = 0.0;
            int srcIdx = 0;
            int lngIdx = 0;

            for (int k = 0; k < sequenceLength; k++)
            {
                srcIdx = sourceStartIdx + k;
                lngIdx = langStartIdx + k;

                if (srcIdx >= sourceFrames.Count || lngIdx >= langFrames.Count)
                {
                    break;
                }

                totalSsim += this.ComputeSsim(sourceFrames[srcIdx], langFrames[lngIdx]);
                validFrames++;
            }

            if (validFrames >= sequenceLength)
            {
                result = totalSsim / validFrames;
            }

            return result;
        }

        /// <summary>
        /// Rileva tagli di scena tramite MSE tra frame consecutivi
        /// </summary>
        /// <param name="frames">Lista frame grayscale</param>
        /// <returns>Lista indici frame dove avviene il taglio</returns>
        protected List<int> DetectSceneCuts(List<byte[]> frames)
        {
            List<int> cuts = new List<int>();
            double interMse = 0.0;
            int lastCutIdx = -this._minCutSpacingFrames;

            for (int i = 0; i < frames.Count - 1; i++)
            {
                interMse = this.ComputeMse(frames[i], frames[i + 1]);

                // Taglio se MSE supera soglia e distanza minima dal taglio precedente
                if (interMse > this._sceneCutThreshold && (i + 1 - lastCutIdx) >= this._minCutSpacingFrames)
                {
                    cuts.Add(i + 1);
                    lastCutIdx = i + 1;
                }
            }

            return cuts;
        }

        /// <summary>
        /// Calcola il fingerprint temporale di un taglio di scena:
        /// 9 valori di MSE inter-frame nella finestra di 10 frame attorno al taglio
        /// </summary>
        /// <param name="frames">Lista frame grayscale</param>
        /// <param name="cutIndex">Indice del frame dove avviene il taglio</param>
        /// <returns>Array di 9 valori MSE inter-frame, o null se indici fuori range</returns>
        protected double[] ComputeTemporalFingerprint(List<byte[]> frames, int cutIndex)
        {
            double[] fingerprint = null;
            int startIdx = cutIndex - this._cutHalfWindow;

            // Verifica che la finestra sia interamente contenuta nei frame
            if (startIdx >= 0 && startIdx + this._cutSignatureLength <= frames.Count)
            {
                fingerprint = new double[this._fingerprintLength];

                for (int i = 0; i < this._fingerprintLength; i++)
                {
                    fingerprint[i] = this.ComputeMse(frames[startIdx + i], frames[startIdx + i + 1]);
                }
            }

            return fingerprint;
        }

        /// <summary>
        /// Calcola la correlazione di Pearson tra due fingerprint temporali
        /// </summary>
        /// <param name="fp1">Primo fingerprint</param>
        /// <param name="fp2">Secondo fingerprint</param>
        /// <returns>Coefficiente di correlazione [-1, 1], o 0 se non calcolabile</returns>
        protected double ComputeFingerprintCorrelation(double[] fp1, double[] fp2)
        {
            double result = 0.0;
            double mean1 = 0.0;
            double mean2 = 0.0;
            double num = 0.0;
            double den1 = 0.0;
            double den2 = 0.0;
            double diff1 = 0.0;
            double diff2 = 0.0;
            double denominator = 0.0;

            // Calcola medie
            for (int i = 0; i < this._fingerprintLength; i++)
            {
                mean1 += fp1[i];
                mean2 += fp2[i];
            }
            mean1 /= this._fingerprintLength;
            mean2 /= this._fingerprintLength;

            // Calcola numeratore e denominatore della correlazione
            for (int i = 0; i < this._fingerprintLength; i++)
            {
                diff1 = fp1[i] - mean1;
                diff2 = fp2[i] - mean2;
                num += diff1 * diff2;
                den1 += diff1 * diff1;
                den2 += diff2 * diff2;
            }

            // Evita divisione per zero (fingerprint piatto = nessun taglio rilevabile)
            denominator = Math.Sqrt(den1 * den2);
            if (denominator > 0.0)
            {
                result = num / denominator;
            }

            return result;
        }

        #endregion
    }
}
