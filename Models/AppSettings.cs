using System;
using System.IO;
using System.Text.Json;

namespace MergeLanguageTracks
{
    /// <summary>
    /// Impostazioni applicazione salvate in .mlt/appsettings.json
    /// </summary>
    public static class AppSettings
    {
        #region Costanti

        /// <summary>
        /// Livello compressione FLAC minimo
        /// </summary>
        public const int FLAC_COMPRESSION_MIN = 0;

        /// <summary>
        /// Livello compressione FLAC massimo
        /// </summary>
        public const int FLAC_COMPRESSION_MAX = 12;

        /// <summary>
        /// Livello compressione FLAC di default (massimo)
        /// </summary>
        public const int FLAC_COMPRESSION_DEFAULT = 12;

        /// <summary>
        /// Bitrate Opus minimo in kbps
        /// </summary>
        public const int OPUS_BITRATE_MIN = 64;

        /// <summary>
        /// Bitrate Opus massimo in kbps
        /// </summary>
        public const int OPUS_BITRATE_MAX = 768;

        /// <summary>
        /// Bitrate Opus di default per mono (1 canale) in kbps
        /// </summary>
        public const int OPUS_DEFAULT_MONO = 128;

        /// <summary>
        /// Bitrate Opus di default per stereo (2 canali) in kbps
        /// </summary>
        public const int OPUS_DEFAULT_STEREO = 256;

        /// <summary>
        /// Bitrate Opus di default per surround 5.1 (6 canali) in kbps
        /// </summary>
        public const int OPUS_DEFAULT_SURROUND51 = 510;

        /// <summary>
        /// Bitrate Opus di default per surround 7.1 (8 canali) in kbps
        /// </summary>
        public const int OPUS_DEFAULT_SURROUND71 = 768;

        /// <summary>
        /// Nome della cartella di configurazione nascosta
        /// </summary>
        private const string CONFIG_FOLDER_NAME = ".mlt";

        /// <summary>
        /// Nome del file di configurazione
        /// </summary>
        private const string CONFIG_FILE_NAME = "appsettings.json";

        /// <summary>
        /// Nome della sottocartella per file temporanei di conversione
        /// </summary>
        public const string TEMP_FOLDER_NAME = "temp";

        #endregion

        #region Variabili statiche

        /// <summary>
        /// Percorso completo della cartella .mlt
        /// </summary>
        private static string s_configFolder;

        /// <summary>
        /// Percorso completo del file appsettings.json
        /// </summary>
        private static string s_configFilePath;

        #endregion

        #region Costruttore statico

        /// <summary>
        /// Costruttore statico: calcola percorsi e imposta valori di default
        /// </summary>
        static AppSettings()
        {
            string appDir = AppContext.BaseDirectory;
            s_configFolder = Path.Combine(appDir, CONFIG_FOLDER_NAME);
            s_configFilePath = Path.Combine(s_configFolder, CONFIG_FILE_NAME);
            ResetDefaults();
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Reimposta tutti i valori ai default
        /// </summary>
        public static void ResetDefaults()
        {
            FlacCompressionLevel = FLAC_COMPRESSION_DEFAULT;
            OpusBitrateMono = OPUS_DEFAULT_MONO;
            OpusBitrateStereo = OPUS_DEFAULT_STEREO;
            OpusBitrateSurround51 = OPUS_DEFAULT_SURROUND51;
            OpusBitrateSurround71 = OPUS_DEFAULT_SURROUND71;
        }

        /// <summary>
        /// Inizializza la cartella .mlt e carica le impostazioni
        /// </summary>
        /// <returns>True se le impostazioni sono state caricate o create con successo</returns>
        public static bool Initialize()
        {
            bool success = true;

            // Crea cartella .mlt se non esiste
            if (!Directory.Exists(s_configFolder))
            {
                Directory.CreateDirectory(s_configFolder);

                // Su Windows imposta attributo nascosto
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(s_configFolder);
                    dirInfo.Attributes |= FileAttributes.Hidden;
                }
            }

            // Carica o crea file impostazioni
            if (File.Exists(s_configFilePath))
            {
                success = Load();
            }
            else
            {
                // Crea con valori di default
                ResetDefaults();
                success = Save();
            }

            return success;
        }

        /// <summary>
        /// Carica le impostazioni dal file appsettings.json
        /// </summary>
        /// <returns>True se il caricamento e' riuscito</returns>
        public static bool Load()
        {
            bool success = false;
            string json = "";
            JsonDocument doc = null;
            JsonElement root;
            JsonElement flacEl;
            JsonElement opusEl;
            JsonElement bitrateEl;

            try
            {
                json = File.ReadAllText(s_configFilePath);
                doc = JsonDocument.Parse(json);
                root = doc.RootElement;

                ResetDefaults();

                // Parsing sezione Flac
                if (root.TryGetProperty("Flac", out flacEl))
                {
                    if (flacEl.TryGetProperty("CompressionLevel", out JsonElement compEl))
                    {
                        FlacCompressionLevel = compEl.GetInt32();
                    }
                }

                // Parsing sezione Opus
                if (root.TryGetProperty("Opus", out opusEl))
                {
                    if (opusEl.TryGetProperty("Bitrate", out bitrateEl))
                    {
                        if (bitrateEl.TryGetProperty("Mono", out JsonElement monoEl))
                        {
                            OpusBitrateMono = monoEl.GetInt32();
                        }
                        if (bitrateEl.TryGetProperty("Stereo", out JsonElement stereoEl))
                        {
                            OpusBitrateStereo = stereoEl.GetInt32();
                        }
                        if (bitrateEl.TryGetProperty("Surround51", out JsonElement s51El))
                        {
                            OpusBitrateSurround51 = s51El.GetInt32();
                        }
                        if (bitrateEl.TryGetProperty("Surround71", out JsonElement s71El))
                        {
                            OpusBitrateSurround71 = s71El.GetInt32();
                        }
                    }
                }

                // Valida range
                Validate();

                doc.Dispose();
                success = true;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteWarning("Errore caricamento appsettings.json: " + ex.Message);
                ResetDefaults();
            }

            return success;
        }

        /// <summary>
        /// Salva le impostazioni correnti su appsettings.json
        /// </summary>
        /// <returns>True se il salvataggio e' riuscito</returns>
        public static bool Save()
        {
            bool success = false;
            JsonSerializerOptions jsonOpts = null;
            string json = "";

            try
            {
                // Costruisce oggetto anonimo per struttura JSON leggibile
                object settingsObj = new
                {
                    Flac = new
                    {
                        CompressionLevel = FlacCompressionLevel
                    },
                    Opus = new
                    {
                        Bitrate = new
                        {
                            Mono = OpusBitrateMono,
                            Stereo = OpusBitrateStereo,
                            Surround51 = OpusBitrateSurround51,
                            Surround71 = OpusBitrateSurround71
                        }
                    }
                };

                jsonOpts = new JsonSerializerOptions();
                jsonOpts.WriteIndented = true;

                json = JsonSerializer.Serialize(settingsObj, jsonOpts);
                File.WriteAllText(s_configFilePath, json);
                success = true;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteWarning("Errore salvataggio appsettings.json: " + ex.Message);
            }

            return success;
        }

        /// <summary>
        /// Restituisce il percorso della cartella per file temporanei di conversione
        /// </summary>
        /// <returns>Percorso cartella temp, creata se non esistente</returns>
        public static string GetTempFolder()
        {
            string tempFolder = Path.Combine(s_configFolder, TEMP_FOLDER_NAME);

            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }

            return tempFolder;
        }

        /// <summary>
        /// Pulisce tutti i file temporanei dalla cartella temp
        /// </summary>
        public static void CleanupTempFiles()
        {
            string tempFolder = Path.Combine(s_configFolder, TEMP_FOLDER_NAME);
            string[] files = null;

            if (Directory.Exists(tempFolder))
            {
                files = Directory.GetFiles(tempFolder);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                    }
                    catch
                    {

                    }
                }
            }
        }

        /// <summary>
        /// Restituisce il bitrate Opus appropriato in base al numero di canali
        /// </summary>
        /// <param name="channels">Numero di canali audio</param>
        /// <returns>Bitrate in kbps</returns>
        public static int GetOpusBitrateForChannels(int channels)
        {
            int bitrate = OpusBitrateStereo;

            if (channels <= 1)
            {
                bitrate = OpusBitrateMono;
            }
            else if (channels <= 2)
            {
                bitrate = OpusBitrateStereo;
            }
            else if (channels <= 6)
            {
                bitrate = OpusBitrateSurround51;
            }
            else
            {
                bitrate = OpusBitrateSurround71;
            }

            return bitrate;
        }

        /// <summary>
        /// Valida e corregge i valori entro i range consentiti
        /// </summary>
        public static void Validate()
        {
            // Clamp FLAC compression level
            if (FlacCompressionLevel < FLAC_COMPRESSION_MIN)
            {
                FlacCompressionLevel = FLAC_COMPRESSION_MIN;
            }
            if (FlacCompressionLevel > FLAC_COMPRESSION_MAX)
            {
                FlacCompressionLevel = FLAC_COMPRESSION_MAX;
            }

            // Clamp Opus bitrate
            OpusBitrateMono = ClampBitrate(OpusBitrateMono);
            OpusBitrateStereo = ClampBitrate(OpusBitrateStereo);
            OpusBitrateSurround51 = ClampBitrate(OpusBitrateSurround51);
            OpusBitrateSurround71 = ClampBitrate(OpusBitrateSurround71);
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Limita un valore bitrate entro il range consentito
        /// </summary>
        /// <param name="value">Valore da limitare</param>
        /// <returns>Valore limitato nel range</returns>
        private static int ClampBitrate(int value)
        {
            int result = value;

            if (result < OPUS_BITRATE_MIN)
            {
                result = OPUS_BITRATE_MIN;
            }
            if (result > OPUS_BITRATE_MAX)
            {
                result = OPUS_BITRATE_MAX;
            }

            return result;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Livello compressione FLAC (0-12, default 12)
        /// </summary>
        public static int FlacCompressionLevel { get; set; }

        /// <summary>
        /// Bitrate Opus per mono (1 canale) in kbps
        /// </summary>
        public static int OpusBitrateMono { get; set; }

        /// <summary>
        /// Bitrate Opus per stereo (2 canali) in kbps
        /// </summary>
        public static int OpusBitrateStereo { get; set; }

        /// <summary>
        /// Bitrate Opus per surround 5.1 (6 canali) in kbps
        /// </summary>
        public static int OpusBitrateSurround51 { get; set; }

        /// <summary>
        /// Bitrate Opus per surround 7.1 (8 canali) in kbps
        /// </summary>
        public static int OpusBitrateSurround71 { get; set; }

        /// <summary>
        /// Percorso della cartella .mlt
        /// </summary>
        public static string ConfigFolder { get { return s_configFolder; } }

        /// <summary>
        /// Percorso del file appsettings.json
        /// </summary>
        public static string ConfigFilePath { get { return s_configFilePath; } }

        #endregion
    }
}
