using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RemuxForge.Core
{
    public static class CodecMapping
    {
        #region Variabili di classe

        /// <summary>
        /// Codec lossless riconosciuti (stringhe mkvmerge)
        /// </summary>
        private static readonly string[] s_losslessCodecs = new string[]
        {
            "DTS-HD Master Audio",
            "DTS-HD High Resolution",
            "TrueHD",
            "PCM",
            "ALAC",
            "MLP",
            "FLAC"
        };

        /// <summary>
        /// Mappa nomi codec utente a stringhe codec esatte mkvmerge
        /// </summary>
        private static readonly Dictionary<string, string[]> s_codecMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Dolby
            { "AC3",       new[] { "AC-3" } },
            { "AC-3",      new[] { "AC-3" } },
            { "DD",        new[] { "AC-3" } },
            { "EAC3",      new[] { "E-AC-3" } },
            { "E-AC-3",    new[] { "E-AC-3" } },
            { "DD+",       new[] { "E-AC-3" } },
            { "DDP",       new[] { "E-AC-3" } },
            { "TRUEHD",    new[] { "TrueHD" } },
            { "ATMOS",     new[] { "TrueHD", "E-AC-3" } },
            { "MLP",       new[] { "MLP" } },

            // DTS - matching esatto per distinguere DTS core da DTS-HD
            { "DTS",       new[] { "DTS" } },
            { "DTS-HD",    new[] { "DTS-HD Master Audio", "DTS-HD High Resolution" } },
            { "DTS-HD MA", new[] { "DTS-HD Master Audio" } },
            { "DTS-HDMA",  new[] { "DTS-HD Master Audio" } },
            { "DTS-HD HR", new[] { "DTS-HD High Resolution" } },
            { "DTS-HDHR",  new[] { "DTS-HD High Resolution" } },
            { "DTS-ES",    new[] { "DTS-ES" } },
            { "DTS:X",     new[] { "DTS:X" } },
            { "DTSX",      new[] { "DTS:X" } },

            // Lossless
            { "FLAC",      new[] { "FLAC" } },
            { "PCM",       new[] { "PCM" } },
            { "LPCM",      new[] { "PCM" } },
            { "WAV",       new[] { "PCM" } },
            { "ALAC",      new[] { "ALAC" } },

            // Lossy
            { "AAC",       new[] { "AAC" } },
            { "HE-AAC",    new[] { "AAC" } },
            { "MP3",       new[] { "MPEG Audio", "MP3" } },
            { "MP2",       new[] { "MP2", "MPEG Audio Layer 2" } },
            { "OPUS",      new[] { "Opus" } },
            { "VORBIS",    new[] { "Vorbis" } }
        };

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Restituisce i pattern codec esatti mkvmerge per una stringa codec fornita dall'utente.
        /// </summary>
        /// <param name="userCodec">La stringa codec fornita dall'utente.</param>
        /// <returns>Un array di pattern codec esatti, o null se non riconosciuto.</returns>
        public static string[] GetCodecPatterns(string userCodec)
        {
            string[] result = null;
            string normalized = userCodec.Trim().ToUpper();

            // Lookup diretto
            if (s_codecMap.ContainsKey(normalized))
            {
                result = s_codecMap[normalized];
            }
            else
            {
                // Fallback: rimuovi trattini, spazi, due punti per match fuzzy
                string strippedInput = Regex.Replace(normalized, @"[\s\-:]", "");

                foreach (KeyValuePair<string, string[]> entry in s_codecMap)
                {
                    string strippedKey = Regex.Replace(entry.Key.ToUpper(), @"[\s\-:]", "");
                    if (strippedKey == strippedInput)
                    {
                        result = entry.Value;
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Restituisce una stringa separata da virgole di tutte le chiavi alias codec riconosciute.
        /// </summary>
        /// <returns>Una stringa che elenca tutti gli alias codec.</returns>
        public static string GetAllCodecNames()
        {
            return string.Join(", ", s_codecMap.Keys);
        }

        /// <summary>
        /// Verifica se un codec traccia corrisponde a uno dei pattern specificati.
        /// </summary>
        /// <param name="trackCodec">La stringa codec dalla traccia MKV.</param>
        /// <param name="patterns">L'array di pattern codec esatti con cui confrontare.</param>
        /// <returns>True se il codec traccia corrisponde a qualche pattern, false altrimenti.</returns>
        public static bool MatchesCodec(string trackCodec, string[] patterns)
        {
            bool matched = false;

            for (int i = 0; i < patterns.Length; i++)
            {
                if (string.Equals(trackCodec, patterns[i], StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                    break;
                }
            }

            return matched;
        }

        /// <summary>
        /// Verifica se un codec e' lossless (candidato a conversione)
        /// </summary>
        /// <param name="trackCodec">Stringa codec dalla traccia MKV</param>
        /// <returns>True se il codec e' lossless</returns>
        public static bool IsLosslessCodec(string trackCodec)
        {
            bool result = false;

            for (int i = 0; i < s_losslessCodecs.Length; i++)
            {
                if (string.Equals(trackCodec, s_losslessCodecs[i], StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Verifica se una traccia e' Atmos o DTS:X (codec con metadati spaziali, non convertibile)
        /// </summary>
        /// <param name="track">Traccia da verificare</param>
        /// <returns>True se la traccia e' TrueHD Atmos o DTS:X</returns>
        public static bool IsSpatialCodec(TrackInfo track)
        {
            bool result = false;

            // DTS:X: codec gia' distinto in mkvmerge
            if (string.Equals(track.Codec, "DTS:X", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
            }
            // TrueHD Atmos: codec TrueHD + nome traccia contiene "Atmos"
            else if (string.Equals(track.Codec, "TrueHD", StringComparison.OrdinalIgnoreCase) && track.Name.IndexOf("Atmos", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Verifica se una traccia e' lossless e convertibile (lossless ma non spaziale)
        /// </summary>
        /// <param name="track">Traccia da verificare</param>
        /// <param name="targetFormat">Formato target (flac/opus). Se flac, FLAC sorgente non viene convertito</param>
        /// <returns>True se la traccia puo' essere convertita</returns>
        public static bool IsConvertibleLossless(TrackInfo track, string targetFormat)
        {
            bool result = false;

            // Deve essere lossless
            if (!IsLosslessCodec(track.Codec))
            {
                result = false;
            }
            // Non convertire codec spaziali
            else if (IsSpatialCodec(track))
            {
                result = false;
            }
            // Non convertire FLAC -> FLAC
            else if (string.Equals(track.Codec, "FLAC", StringComparison.OrdinalIgnoreCase) && string.Equals(targetFormat, "flac", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
            }
            else
            {
                result = true;
            }

            return result;
        }

        #endregion
    }
}
