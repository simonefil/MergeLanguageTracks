using RemuxForge.Core.Models;
using System;
using System.IO;

namespace RemuxForge.Core.Subtitles
{
    /// <summary>
    /// Riscrive sottotitoli PGS/SUP applicando cut e insert ai timestamp packet
    /// </summary>
    internal class PgsSubtitleTimelineRewriter
    {
        #region Metodi pubblici

        /// <summary>
        /// Riscrive un file PGS/SUP applicando le operazioni dell'edit map
        /// </summary>
        /// <param name="inputFile">File PGS/SUP originale</param>
        /// <param name="outputFile">File PGS/SUP riscritto</param>
        /// <param name="editMap">Edit map da applicare</param>
        /// <returns>True se il file riscritto contiene packet validi</returns>
        public bool Rewrite(string inputFile, string outputFile, EditMap editMap)
        {
            byte[] data = File.ReadAllBytes(inputFile);
            MemoryStream output = new MemoryStream();
            int pos = 0;
            int setStart;
            int setEnd;

            // Il formato SUP/PGS e' una sequenza di display-set terminati da segment type 0x80
            while (pos + 13 <= data.Length)
            {
                setStart = pos;
                if (!this.TryFindDisplaySetEnd(data, setStart, out setEnd))
                {
                    return false;
                }

                if (!this.WriteMappedDisplaySet(data, setStart, setEnd, editMap, output))
                {
                    return false;
                }

                pos = setEnd;
            }

            File.WriteAllBytes(outputFile, output.ToArray());
            return output.Length > 0;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Trova la fine del display-set PGS corrente
        /// </summary>
        /// <param name="data">Buffer SUP</param>
        /// <param name="start">Offset iniziale display-set</param>
        /// <param name="end">Offset subito dopo il display-set</param>
        /// <returns>True se il display-set e' completo</returns>
        private bool TryFindDisplaySetEnd(byte[] data, int start, out int end)
        {
            int pos = start;
            int packetLength;
            int segmentType;
            end = start;

            while (pos + 13 <= data.Length)
            {
                if (!this.TryGetPacketLength(data, pos, out packetLength))
                {
                    return false;
                }

                segmentType = data[pos + 10];
                pos += packetLength;
                if (segmentType == 0x80)
                {
                    end = pos;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Scrive un display-set completo applicando lo stesso delta temporale a tutti i packet
        /// </summary>
        /// <param name="data">Buffer SUP</param>
        /// <param name="start">Offset iniziale display-set</param>
        /// <param name="end">Offset finale display-set</param>
        /// <param name="editMap">Edit map da applicare</param>
        /// <param name="output">Stream output</param>
        /// <returns>True se il display-set e' valido</returns>
        private bool WriteMappedDisplaySet(byte[] data, int start, int end, EditMap editMap, MemoryStream output)
        {
            long firstPtsMs = (long)Math.Round(this.ReadUInt32BigEndian(data, start + 2) / 90.0);
            long mappedFirstPtsMs = SubtitleTimelineMapper.MapPacketTimestamp(firstPtsMs, editMap);
            long deltaMs;
            int pos = start;
            int packetLength;
            byte[] packet;

            // Un display-set che cade dentro un cut va scartato interamente per non lasciare PCS/ODS/END incompleti
            if (mappedFirstPtsMs < 0)
            {
                return true;
            }

            deltaMs = mappedFirstPtsMs - firstPtsMs;
            while (pos < end)
            {
                if (!this.TryGetPacketLength(data, pos, out packetLength) || pos + packetLength > end)
                {
                    return false;
                }

                packet = new byte[packetLength];
                Array.Copy(data, pos, packet, 0, packetLength);
                this.OffsetPacketTimestamp(packet, 2, deltaMs);
                this.OffsetPacketTimestamp(packet, 6, deltaMs);
                output.Write(packet, 0, packet.Length);
                pos += packetLength;
            }

            return true;
        }

        /// <summary>
        /// Legge e valida la lunghezza di un packet SUP
        /// </summary>
        /// <param name="data">Buffer SUP</param>
        /// <param name="pos">Offset packet</param>
        /// <param name="packetLength">Lunghezza packet completa</param>
        /// <returns>True se il packet e' valido</returns>
        private bool TryGetPacketLength(byte[] data, int pos, out int packetLength)
        {
            int size;
            packetLength = 0;
            if (pos + 13 > data.Length || data[pos] != (byte)'P' || data[pos + 1] != (byte)'G')
            {
                return false;
            }

            size = (data[pos + 11] << 8) | data[pos + 12];
            packetLength = 13 + size;
            return pos + packetLength <= data.Length;
        }

        /// <summary>
        /// Applica un delta in millisecondi a un timestamp packet
        /// </summary>
        /// <param name="packet">Packet SUP</param>
        /// <param name="offset">Offset timestamp</param>
        /// <param name="deltaMs">Delta in millisecondi</param>
        private void OffsetPacketTimestamp(byte[] packet, int offset, long deltaMs)
        {
            long timestampMs = (long)Math.Round(this.ReadUInt32BigEndian(packet, offset) / 90.0);
            long mappedMs = timestampMs + deltaMs;
            if (mappedMs < 0)
            {
                mappedMs = 0;
            }

            this.WriteUInt32BigEndian(packet, offset, (uint)Math.Round(mappedMs * 90.0));
        }

        /// <summary>
        /// Legge un intero unsigned 32 bit big-endian
        /// </summary>
        /// <param name="data">Buffer dati</param>
        /// <param name="offset">Offset lettura</param>
        /// <returns>Valore letto</returns>
        private uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        /// <summary>
        /// Scrive un intero unsigned 32 bit big-endian
        /// </summary>
        /// <param name="data">Buffer dati</param>
        /// <param name="offset">Offset scrittura</param>
        /// <param name="value">Valore da scrivere</param>
        private void WriteUInt32BigEndian(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)((value >> 24) & 0xff);
            data[offset + 1] = (byte)((value >> 16) & 0xff);
            data[offset + 2] = (byte)((value >> 8) & 0xff);
            data[offset + 3] = (byte)(value & 0xff);
        }

        #endregion
    }
}
