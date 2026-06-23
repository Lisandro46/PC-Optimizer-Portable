using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PcOptimizer
{
    public sealed class ActionService
    {
        public List<OperationPlanItem> BuildPlan(IEnumerable<AppItem> selected)
        {
            var plan = new List<OperationPlanItem>();
            foreach (AppItem item in selected)
            {
                var op = new OperationPlanItem();
                op.Item = item;
                op.Action = item.Action;
                op.Warning = item.RiskReason;
                op.Supported = true;

                if (item.Action == "Desactivar")
                {
                    if (item.Kind == AppKind.OptionalFeature && item.CanDisable)
                    {
                        op.Method = "API oficial de características opcionales (reversible)";
                        op.ExactCommand = "Disable-WindowsOptionalFeature -Online -FeatureName '" + Ps(item.FeatureName) + "' -NoRestart";
                    }
                    else if (IsEdge(item) && item.CanDisable)
                    {
                        op.Method = "Políticas de Windows para impedir precarga y ejecución en segundo plano";
                        op.ExactCommand = "Stop-Process msedge; HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge: StartupBoostEnabled=0, BackgroundModeEnabled=0; HKLM\\SOFTWARE\\Policies\\Microsoft\\MicrosoftEdge\\Main: AllowPrelaunch=0";
                        op.Warning += " Esta desactivación evita la precarga y el funcionamiento en segundo plano, pero Windows puede seguir invocando Edge para ciertas tareas internas.";
                    }
                    else
                    {
                        op.Supported = false;
                        op.Method = "No existe una desactivación reversible conocida";
                        op.ExactCommand = "No se ejecutará";
                    }
                }
                else
                {
                    if (!item.CanUninstall)
                    {
                        op.Supported = false;
                        op.Method = "Windows o el instalador no publican un método de desinstalación";
                        op.ExactCommand = "No se ejecutará";
                    }
                    else if (item.Kind == AppKind.Classic || item.Kind == AppKind.WindowsComponent)
                    {
                        string cmd = !String.IsNullOrWhiteSpace(item.UninstallCommand) ? item.UninstallCommand : item.QuietUninstallCommand;
                        cmd = NormalizeMsiUninstall(cmd);
                        op.Method = item.Kind == AppKind.WindowsComponent ? "Desinstalador experto del componente" : "Desinstalador publicado por la aplicación";
                        op.ExactCommand = cmd;
                    }
                    else if (item.Kind == AppKind.StoreApp)
                    {
                        op.Method = item.NonRemovable ? "Intento experto con Remove-AppxPackage; Windows puede rechazarlo" : "API oficial AppX para todos los usuarios";
                        op.ExactCommand = StoreRemovalCommand(item);
                    }
                    else if (item.Kind == AppKind.ProvisionedApp)
                    {
                        op.Method = "Quitar la copia base para usuarios futuros";
                        op.ExactCommand = "Remove-AppxProvisionedPackage -Online -PackageName '" + item.ProvisionedPackageName + "'";
                    }
                    else if (item.Kind == AppKind.WindowsCapability)
                    {
                        op.Method = "API oficial de capacidades de Windows";
                        op.ExactCommand = "Remove-WindowsCapability -Online -Name '" + item.CapabilityName + "'";
                    }
                    else
                    {
                        op.Supported = false;
                        op.Method = "La acción seleccionada no corresponde a este elemento";
                        op.ExactCommand = "No se ejecutará";
                    }
                }
                plan.Add(op);
            }
            return plan;
        }

        public string CreateRestorePoint()
        {
            string command = "Checkpoint-Computer -Description 'Antes de PC Optimizer' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop";
            string output;
            string error;
            int exit = RunPowerShell(command, out output, out error, 180000);
            if (exit == 0) return "Punto de restauración creado.";
            return "No se pudo crear el punto de restauración: " + Clean(error);
        }

        public List<OperationResult> Execute(List<OperationPlanItem> plan, Action<string> progress)
        {
            var results = new List<OperationResult>();
            foreach (OperationPlanItem op in plan)
            {
                var result = new OperationResult();
                result.Item = op.Item;
                if (!op.Supported)
                {
                    result.Success = false;
                    result.Message = "Omitido: acción no compatible.";
                    results.Add(result);
                    continue;
                }

                progress(op.Action + ": " + op.Item.Name);
                try
                {
                    string output;
                    string error;
                    int exit;

                    if (op.Action == "Desactivar" && IsEdge(op.Item))
                    {
                        DisableEdge();
                        result.Success = true;
                        result.Message = "Precarga y ejecución en segundo plano desactivadas mediante políticas.";
                    }
                    else if (op.Action == "Desactivar" && op.Item.Kind == AppKind.OptionalFeature)
                    {
                        exit = RunPowerShell("Disable-WindowsOptionalFeature -Online -FeatureName '" + Ps(op.Item.FeatureName) + "' -NoRestart -ErrorAction Stop | Out-String", out output, out error, 900000);
                        result.Success = exit == 0;
                        result.Message = exit == 0 ? "Característica desactivada. Puede requerir reinicio." : Clean(error);
                    }
                    else if (op.Item.Kind == AppKind.Classic || op.Item.Kind == AppKind.WindowsComponent)
                    {
                        exit = RunCommand(op.ExactCommand);
                        result.Success = exit == 0 || exit == 1641 || exit == 3010;
                        result.Message = result.Success ? (exit == 3010 || exit == 1641 ? "Finalizado; Windows solicita reinicio." : "El desinstalador finalizó correctamente.") : "El desinstalador devolvió el código " + exit + ".";
                    }
                    else if (op.Item.Kind == AppKind.StoreApp)
                    {
                        string command = StoreRemovalCommand(op.Item);
                        exit = RunPowerShell(command, out output, out error, 600000);
                        result.Success = exit == 0;
                        result.Message = exit == 0 ? "Paquete AppX eliminado." : Clean(error);
                    }
                    else if (op.Item.Kind == AppKind.ProvisionedApp)
                    {
                        string command = "Remove-AppxProvisionedPackage -Online -PackageName '" + Ps(op.Item.ProvisionedPackageName) + "' -ErrorAction Stop | Out-String";
                        exit = RunPowerShell(command, out output, out error, 600000);
                        result.Success = exit == 0;
                        result.Message = exit == 0 ? "Copia para usuarios futuros eliminada." : Clean(error);
                    }
                    else if (op.Item.Kind == AppKind.WindowsCapability)
                    {
                        string command = "Remove-WindowsCapability -Online -Name '" + Ps(op.Item.CapabilityName) + "' -ErrorAction Stop | Out-String";
                        exit = RunPowerShell(command, out output, out error, 900000);
                        result.Success = exit == 0;
                        result.Message = exit == 0 ? "Capacidad eliminada. Puede requerir reinicio." : Clean(error);
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Tipo de elemento no compatible.";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = ex.Message;
                }
                results.Add(result);
            }
            return results;
        }

        public string SaveInventoryAndPlan(IEnumerable<AppItem> inventory, IEnumerable<OperationPlanItem> plan)
        {
            string baseDir = Path.Combine(AppContext.BaseDirectory, "PCOptimizer-Logs");
            try { Directory.CreateDirectory(baseDir); }
            catch
            {
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCOptimizer-Logs");
                Directory.CreateDirectory(baseDir);
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string inventoryPath = Path.Combine(baseDir, "inventario-" + stamp + ".csv");
            var sb = new StringBuilder();
            sb.AppendLine("Nombre;Version;Editor;Origen;Alcance;Estado;Riesgo;Id");
            foreach (AppItem item in inventory)
            {
                sb.AppendLine(Csv(item.Name) + ";" + Csv(item.Version) + ";" + Csv(item.Publisher) + ";" + Csv(item.Source) + ";" + Csv(item.Scope) + ";" + Csv(item.State) + ";" + Csv(item.RiskText) + ";" + Csv(item.Id));
            }
            File.WriteAllText(inventoryPath, sb.ToString(), new UTF8Encoding(true));

            string planPath = Path.Combine(baseDir, "plan-" + stamp + ".txt");
            var psb = new StringBuilder();
            psb.AppendLine("PC Optimizer — Plan aprobado");
            psb.AppendLine("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            psb.AppendLine();
            foreach (OperationPlanItem op in plan)
            {
                psb.AppendLine("[" + op.Item.RiskText + "] " + op.Action + " — " + op.Item.Name);
                psb.AppendLine("Método: " + op.Method);
                psb.AppendLine("Acción exacta: " + op.ExactCommand);
                psb.AppendLine("Advertencia: " + op.Warning);
                psb.AppendLine();
            }
            File.WriteAllText(planPath, psb.ToString(), new UTF8Encoding(true));
            return baseDir;
        }

        public string SaveResults(IEnumerable<OperationResult> results, string folder)
        {
            string path = Path.Combine(folder, "resultado-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var sb = new StringBuilder();
            foreach (OperationResult result in results)
                sb.AppendLine((result.Success ? "OK" : "ERROR") + " | " + result.Item.Name + " | " + result.Message);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }

        private static void DisableEdge()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("msedge"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }

            using (RegistryKey edge = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge"))
            {
                edge.SetValue("StartupBoostEnabled", 0, RegistryValueKind.DWord);
                edge.SetValue("BackgroundModeEnabled", 0, RegistryValueKind.DWord);
            }
            using (RegistryKey legacy = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\MicrosoftEdge\Main"))
            {
                legacy.SetValue("AllowPrelaunch", 0, RegistryValueKind.DWord);
            }
        }

        private static int RunCommand(string command)
        {
            string[] args = SplitCommandLine(Environment.ExpandEnvironmentVariables(command));
            if (args.Length == 0) throw new InvalidOperationException("El comando de desinstalación está vacío.");
            var psi = new ProcessStartInfo();
            psi.FileName = args[0];
            psi.Arguments = String.Join(" ", args.Skip(1).Select(QuoteArgument).ToArray());
            psi.UseShellExecute = true;
            psi.WorkingDirectory = Path.GetDirectoryName(args[0]);
            var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException("Windows no pudo iniciar el desinstalador.");
            process.WaitForExit();
            return process.ExitCode;
        }

        private static int RunPowerShell(string command, out string output, out string error, int timeoutMs)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ProgressPreference='SilentlyContinue';" + command));
            var psi = new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            var p = Process.Start(psi);
            output = p.StandardOutput.ReadToEnd();
            error = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                error = "La operación superó el tiempo máximo.";
                return -1;
            }
            return p.ExitCode;
        }

        private static string NormalizeMsiUninstall(string command)
        {
            if (String.IsNullOrWhiteSpace(command)) return command;
            string lower = command.ToLowerInvariant();
            if (lower.Contains("msiexec") && lower.Contains("/i"))
            {
                int pos = lower.IndexOf("/i", StringComparison.Ordinal);
                return command.Substring(0, pos) + "/X" + command.Substring(pos + 2);
            }
            return command;
        }

        private static string StoreRemovalCommand(AppItem item)
        {
            return "$p=Get-AppxPackage -AllUsers | Where-Object {$_.PackageFullName -eq '" + Ps(item.PackageFullName) + "'}; if($null -eq $p){throw 'Paquete no encontrado'}; $p | Remove-AppxPackage -AllUsers -Confirm:$false -ErrorAction Stop";
        }

        private static string[] SplitCommandLine(string commandLine)
        {
            int argc;
            IntPtr argv = CommandLineToArgvW(commandLine, out argc);
            if (argv == IntPtr.Zero) return new string[0];
            try
            {
                var args = new string[argc];
                for (int i = 0; i < argc; i++)
                {
                    IntPtr ptr = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(ptr);
                }
                return args;
            }
            finally { LocalFree(argv); }
        }

        private static string QuoteArgument(string arg)
        {
            if (String.IsNullOrEmpty(arg)) return "\"\"";
            if (arg.IndexOfAny(new[] { ' ', '\t', '\"' }) < 0) return arg;
            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool IsEdge(AppItem item)
        {
            return item.Name.IndexOf("Microsoft Edge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Ps(string value) { return (value ?? "").Replace("'", "''"); }
        private static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }
        private static string Clean(string value) { return String.IsNullOrWhiteSpace(value) ? "Windows no informó el motivo." : value.Trim(); }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}
