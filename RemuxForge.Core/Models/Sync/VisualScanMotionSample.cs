namespace RemuxForge.Core.Models
{
    /// <summary>
    /// Campione ordinabile per movimento locale
    /// </summary>
    public class VisualScanMotionSample
    {
        /// <summary>
        /// Indice frame nel segmento analizzato
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Timestamp frame in millisecondi
        /// </summary>
        public double TimestampMs { get; set; }

        /// <summary>
        /// Valore movimento locale
        /// </summary>
        public double Motion { get; set; }
    }
}
