using System.Collections.Generic;
using System.Globalization;

namespace RemuxForge.Web.Services
{
    /// <summary>
    /// Validazione e parsing dei campi testuali del dialog impostazioni avanzate
    /// </summary>
    public static class AdvancedSettingsFieldValidator
    {
        #region Metodi pubblici

        /// <summary>
        /// Valida un intero in range
        /// </summary>
        /// <param name="errors">Lista errori da aggiornare</param>
        /// <param name="fieldName">Nome campo</param>
        /// <param name="text">Valore testuale</param>
        /// <param name="min">Valore minimo</param>
        /// <param name="max">Valore massimo</param>
        public static void ValidateIntRange(List<string> errors, string fieldName, string text, int min, int max)
        {
            int value;
            if (!int.TryParse(text, out value))
            {
                errors.Add(fieldName + ": valore intero non valido");
            }
            else if (value < min || value > max)
            {
                errors.Add(fieldName + ": deve essere tra " + min + " e " + max);
            }
        }

        /// <summary>
        /// Valida un double in range
        /// </summary>
        /// <param name="errors">Lista errori da aggiornare</param>
        /// <param name="fieldName">Nome campo</param>
        /// <param name="text">Valore testuale</param>
        /// <param name="min">Valore minimo</param>
        /// <param name="max">Valore massimo</param>
        public static void ValidateDoubleRange(List<string> errors, string fieldName, string text, double min, double max)
        {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                errors.Add(fieldName + ": valore numerico non valido");
            }
            else if (value < min || value > max)
            {
                errors.Add(fieldName + ": deve essere tra " + min.ToString(CultureInfo.InvariantCulture) + " e " + max.ToString(CultureInfo.InvariantCulture));
            }
        }

        #endregion
    }
}
