using System.Collections.Generic;

namespace RemuxForge.Core.Models
{
    /// <summary>
    /// Candidato offset prodotto dalla pipeline frame-sync
    /// </summary>
    public class FrameSyncCandidate
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public FrameSyncCandidate()
        {
            this.OffsetMs = 0;
            this.Source = "";
            this.VoteCount = 0;
            this.VisualScore = 0.0;
            this.BlurScore = 0.0;
            this.TemporalScore = 0.0;
            this.EdgeScore = 0.0;
            this.BlockScore = 0.0;
            this.MotionScore = 0.0;
            this.HashScore = 0.0;
            this.DescriptorVotes = 0;
            this.DescriptorAgreement = 0.0;
            this.CombinedScore = 0.0;
            this.SecondBestScore = 0.0;
            this.Margin = 0.0;
            this.MatchedCuts = 0;
        }

        #endregion

        #region Costanti

        /// <summary>
        /// Candidato generato tramite voting sui tagli scena
        /// </summary>
        public const string SCENE_CUT_VOTING = "SceneCutVoting";

        /// <summary>
        /// Candidato generato tramite fingerprint temporale
        /// </summary>
        public const string TEMPORAL_FINGERPRINT = "TemporalFingerprint";

        /// <summary>
        /// Candidato generato tramite ricerca locale
        /// </summary>
        public const string LOCAL_SEARCH = "LocalSearch";

        /// <summary>
        /// Candidato generato tramite fingerprint audio globale
        /// </summary>
        public const string AUDIO_GLOBAL = "AudioGlobalFingerprint";

        #endregion

        #region Proprieta

        /// <summary>
        /// Offset candidato in millisecondi
        /// </summary>
        public int OffsetMs { get; set; }

        /// <summary>
        /// Origine del candidato
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Numero di voti grezzi ricevuti
        /// </summary>
        public int VoteCount { get; set; }

        /// <summary>
        /// Score visuale
        /// </summary>
        public double VisualScore { get; set; }

        /// <summary>
        /// Score luma blur/denoise
        /// </summary>
        public double BlurScore { get; set; }

        /// <summary>
        /// Score fingerprint temporale
        /// </summary>
        public double TemporalScore { get; set; }

        /// <summary>
        /// Score edge/gradient
        /// </summary>
        public double EdgeScore { get; set; }

        /// <summary>
        /// Score fingerprint blocchi
        /// </summary>
        public double BlockScore { get; set; }

        /// <summary>
        /// Score movimento a blocchi inter-frame
        /// </summary>
        public double MotionScore { get; set; }

        /// <summary>
        /// Score hash percettivo leggero
        /// </summary>
        public double HashScore { get; set; }

        /// <summary>
        /// Numero descriptor visuali concordanti
        /// </summary>
        public int DescriptorVotes { get; set; }

        /// <summary>
        /// Quota descriptor visuali concordanti 0..1
        /// </summary>
        public double DescriptorAgreement { get; set; }

        /// <summary>
        /// Score combinato normalizzato
        /// </summary>
        public double CombinedScore { get; set; }

        /// <summary>
        /// Score del secondo candidato migliore
        /// </summary>
        public double SecondBestScore { get; set; }

        /// <summary>
        /// Distanza tra miglior candidato e secondo candidato
        /// </summary>
        public double Margin { get; set; }

        /// <summary>
        /// Numero di tagli matchati/verificati
        /// </summary>
        public int MatchedCuts { get; set; }

        #endregion
    }

    /// <summary>
    /// Risultato iniziale della ricerca offset frame-sync
    /// </summary>
    public class FrameSyncInitialResult
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public FrameSyncInitialResult()
        {
            this.Success = false;
            this.Ambiguous = false;
            this.BestCandidate = null;
            this.Candidates = new List<FrameSyncCandidate>();
            this.FailureReason = "";
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// True se la ricerca iniziale ha prodotto un candidato accettabile
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// True se la ricerca iniziale ha prodotto candidati ambigui
        /// </summary>
        public bool Ambiguous { get; set; }

        /// <summary>
        /// Miglior candidato iniziale
        /// </summary>
        public FrameSyncCandidate BestCandidate { get; set; }

        /// <summary>
        /// Lista candidati ordinati per affidabilita'
        /// </summary>
        public List<FrameSyncCandidate> Candidates { get; set; }

        /// <summary>
        /// Motivo fallimento o ambiguita'
        /// </summary>
        public string FailureReason { get; set; }

        #endregion
    }
}
