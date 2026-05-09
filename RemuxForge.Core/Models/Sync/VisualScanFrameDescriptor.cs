namespace RemuxForge.Core.Models
{
    /// <summary>
    /// Descriptor visuale precomputato per fallback visual scan e local checkpoint
    /// </summary>
    public class VisualScanFrameDescriptor
    {
        /// <summary>
        /// Frame grayscale originale
        /// </summary>
        public byte[] Frame { get; set; }

        /// <summary>
        /// Campioni blur precomputati
        /// </summary>
        public ushort[] BlurSamples { get; set; }

        /// <summary>
        /// Campioni edge precomputati
        /// </summary>
        public ushort[] EdgeSamples { get; set; }

        /// <summary>
        /// Blocchi luminanza
        /// </summary>
        public ushort[] Blocks { get; set; }

        /// <summary>
        /// Blocchi edge
        /// </summary>
        public ushort[] EdgeBlocks { get; set; }

        /// <summary>
        /// Blocchi movimento
        /// </summary>
        public short[] MotionBlocks { get; set; }

        /// <summary>
        /// Average hash del frame
        /// </summary>
        public ulong AverageHash { get; set; }

        /// <summary>
        /// Difference hash del frame
        /// </summary>
        public ulong DifferenceHash { get; set; }

        /// <summary>
        /// Media luminanza
        /// </summary>
        public double Mean { get; set; }

        /// <summary>
        /// Varianza luminanza
        /// </summary>
        public double Variance { get; set; }
    }
}
