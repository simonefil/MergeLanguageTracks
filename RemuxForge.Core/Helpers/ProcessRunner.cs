using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace RemuxForge.Core
{
    /// <summary>
    /// Risultato di un'esecuzione processo
    /// </summary>
    public class ProcessResult
    {
        /// <summary>
        /// Codice di uscita del processo (-1 se errore o timeout)
        /// </summary>
        public int ExitCode;

        /// <summary>
        /// Output standard del processo
        /// </summary>
        public string Stdout;

        /// <summary>
        /// Output errore del processo
        /// </summary>
        public string Stderr;

        /// <summary>
        /// Costruttore
        /// </summary>
        public ProcessResult()
        {
            this.ExitCode = -1;
            this.Stdout = "";
            this.Stderr = "";
        }
    }

    /// <summary>
    /// Esecuzione centralizzata di processi esterni con lettura parallela stdout/stderr
    /// </summary>
    public static class ProcessRunner
    {
        #region Metodi pubblici

        /// <summary>
        /// Esegue un processo e cattura stdout e stderr come testo
        /// </summary>
        /// <param name="fileName">Percorso dell'eseguibile</param>
        /// <param name="arguments">Argomenti del processo</param>
        /// <param name="timeoutMs">Timeout in millisecondi, 0 = nessun timeout</param>
        /// <returns>Risultato con exit code, stdout e stderr</returns>
        public static ProcessResult Run(string fileName, string[] arguments, int timeoutMs = 0)
        {
            ProcessResult result = new ProcessResult();
            Process proc = null;
            string stdout = "";
            string stderr = "";

            try
            {
                proc = new Process();
                SetupStartInfo(proc, fileName);

                // Argomenti via ArgumentList per encoding corretto su Linux (UTF-8)
                for (int i = 0; i < arguments.Length; i++)
                {
                    proc.StartInfo.ArgumentList.Add(arguments[i]);
                }

                proc.Start();

                // Legge stdout e stderr in parallelo per prevenire deadlock
                Thread stdoutThread = new Thread(() => { stdout = proc.StandardOutput.ReadToEnd(); });
                stdoutThread.Start();
                stderr = proc.StandardError.ReadToEnd();
                stdoutThread.Join();

                // Attendi terminazione con timeout opzionale
                if (timeoutMs > 0)
                {
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        // Kill best-effort: il processo potrebbe essere gia' terminato
                        try { proc.Kill(); } catch { }
                        result.ExitCode = -1;
                        result.Stdout = stdout;
                        result.Stderr = stderr;
                        return result;
                    }
                }
                else
                {
                    proc.WaitForExit();
                }

                result.ExitCode = proc.ExitCode;
                result.Stdout = stdout;
                result.Stderr = stderr;
            }
            catch (Exception ex)
            {
                result.Stderr = "Eccezione durante l'esecuzione di " + fileName + ": " + ex.Message;
            }
            finally
            {
                if (proc != null) { proc.Dispose(); proc = null; }
            }

            return result;
        }

        /// <summary>
        /// Esegue un processo leggendo stderr riga per riga per il progresso in tempo reale
        /// </summary>
        /// <param name="fileName">Percorso dell'eseguibile</param>
        /// <param name="arguments">Lista argomenti</param>
        /// <param name="onStderrLine">Callback invocato per ogni riga di stderr</param>
        /// <returns>Codice di uscita del processo</returns>
        public static int RunWithProgress(string fileName, List<string> arguments, Action<string> onStderrLine)
        {
            int exitCode = -1;
            Process proc = null;

            try
            {
                proc = new Process();
                SetupStartInfo(proc, fileName);

                // Argomenti via ArgumentList
                for (int i = 0; i < arguments.Count; i++)
                {
                    proc.StartInfo.ArgumentList.Add(arguments[i]);
                }

                proc.Start();

                // Leggi stdout in thread separato per evitare deadlock
                // Catch silenzioso intenzionale: pipe puo' chiudersi se il processo termina
                Thread stdoutThread = new Thread(() =>
                {
                    try { proc.StandardOutput.ReadToEnd(); }
                    catch { }
                });
                stdoutThread.Start();

                // Leggi stderr riga per riga per il progresso
                StreamReader stderrReader = proc.StandardError;
                string line = "";
                while ((line = stderrReader.ReadLine()) != null)
                {
                    if (onStderrLine != null && line.Length > 0)
                    {
                        onStderrLine(line);
                    }
                }

                stdoutThread.Join();
                proc.WaitForExit();
                exitCode = proc.ExitCode;
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write(LogSection.General, LogLevel.Warning, "Errore esecuzione " + fileName + ": " + ex.Message);
            }
            finally
            {
                if (proc != null) { proc.Dispose(); proc = null; }
            }

            return exitCode;
        }

        /// <summary>
        /// Esegue un processo scartando l'output, con supporto timeout e kill
        /// </summary>
        /// <param name="fileName">Percorso dell'eseguibile</param>
        /// <param name="arguments">Argomenti del processo</param>
        /// <param name="timeoutMs">Timeout in millisecondi, 0 = nessun timeout</param>
        /// <returns>Codice di uscita del processo, -1 se timeout o errore</returns>
        public static int RunDiscardOutput(string fileName, string[] arguments, int timeoutMs = 0)
        {
            int exitCode = -1;
            Process proc = null;

            try
            {
                proc = new Process();
                SetupStartInfo(proc, fileName);

                // Argomenti via ArgumentList
                for (int i = 0; i < arguments.Length; i++)
                {
                    proc.StartInfo.ArgumentList.Add(arguments[i]);
                }

                proc.Start();

                // Svuota stdout e stderr in thread separati per prevenire deadlock
                // Catch silenzioso intenzionale: pipe puo' chiudersi se il processo termina
                Thread stdoutThread = new Thread(() =>
                {
                    try { proc.StandardOutput.ReadToEnd(); } catch { }
                });
                Thread stderrThread = new Thread(() =>
                {
                    try { proc.StandardError.ReadToEnd(); } catch { }
                });
                stdoutThread.Start();
                stderrThread.Start();

                // Attendi terminazione con timeout opzionale
                if (timeoutMs > 0)
                {
                    if (proc.WaitForExit(timeoutMs))
                    {
                        exitCode = proc.ExitCode;
                    }
                    else
                    {
                        // Kill best-effort: il processo potrebbe essere gia' terminato
                        try { proc.Kill(); } catch { }
                    }
                }
                else
                {
                    proc.WaitForExit();
                    exitCode = proc.ExitCode;
                }

                // Attendi thread con timeout per evitare hang
                stdoutThread.Join(5000);
                stderrThread.Join(5000);
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write(LogSection.General, LogLevel.Warning, "Errore esecuzione " + fileName + ": " + ex.Message);
            }
            finally
            {
                if (proc != null) { proc.Dispose(); proc = null; }
            }

            return exitCode;
        }

        /// <summary>
        /// Splitta argomenti composti contenenti spazi in token singoli,
        /// preservando path con separatori directory (/ o \)
        /// </summary>
        /// <param name="args">Array di argomenti potenzialmente composti</param>
        /// <returns>Array con argomenti individuali</returns>
        public static string[] SplitCompoundArgs(string[] args)
        {
            List<string> result = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains(" ") && !args[i].Contains("/") && !args[i].Contains("\\"))
                {
                    // Argomento composto - splitta in token singoli
                    string[] subArgs = args[i].Split(' ');
                    for (int j = 0; j < subArgs.Length; j++)
                    {
                        string trimmed = subArgs[j].Trim();
                        if (trimmed.Length > 0)
                        {
                            result.Add(trimmed);
                        }
                    }
                }
                else
                {
                    result.Add(args[i]);
                }
            }

            return result.ToArray();
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Configura le impostazioni comuni di avvio processo
        /// </summary>
        /// <param name="proc">Processo da configurare</param>
        /// <param name="fileName">Percorso dell'eseguibile</param>
        private static void SetupStartInfo(Process proc, string fileName)
        {
            proc.StartInfo.FileName = fileName;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            proc.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        }

        #endregion
    }
}
