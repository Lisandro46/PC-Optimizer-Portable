using System;
using System.Diagnostics;
using System.Text;

namespace PcOptimizer
{
    public static class PowerShellRunner
    {
        public sealed class PsResult
        {
            public int Exit;
            public string Output = "";
            public string Error = "";
            public bool Ok { get { return Exit == 0; } }
        }

        public static PsResult Run(string command, int timeoutMs)
        {
            var result = new PsResult();
            try
            {
                string full = "[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false);$ProgressPreference='SilentlyContinue';" + command;
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
                var psi = new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                var p = Process.Start(psi);
                result.Output = p.StandardOutput.ReadToEnd();
                result.Error = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    result.Exit = -1;
                    result.Error = "La operación superó el tiempo máximo.";
                    return result;
                }
                result.Exit = p.ExitCode;
            }
            catch (Exception ex)
            {
                result.Exit = -1;
                result.Error = ex.Message;
            }
            return result;
        }
    }
}
