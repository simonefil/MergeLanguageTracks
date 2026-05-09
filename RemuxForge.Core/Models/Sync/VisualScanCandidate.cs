namespace RemuxForge.Core.Models
{
    /// <summary>
    /// Risultato di scoring di un offset nel fallback visuale
    /// </summary>
    public class VisualScanCandidate
    {
        /// <summary>
        /// Offset candidato in millisecondi
        /// </summary>
        public int OffsetMs { get; set; }

        /// <summary>
        /// Numero campioni usati per lo score
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        /// Score aggregato
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Score blur
        /// </summary>
        public double BlurScore { get; set; }

        /// <summary>
        /// Score edge
        /// </summary>
        public double EdgeScore { get; set; }

        /// <summary>
        /// Score blocchi
        /// </summary>
        public double BlockScore { get; set; }

        /// <summary>
        /// Score movimento
        /// </summary>
        public double MotionScore { get; set; }

        /// <summary>
        /// Score hash
        /// </summary>
        public double HashScore { get; set; }

        /// <summary>
        /// Numero descriptor concordi
        /// </summary>
        public int DescriptorVotes { get; set; }
    }
}
