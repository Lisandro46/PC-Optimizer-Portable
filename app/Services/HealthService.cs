using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PcOptimizer
{
    public sealed class DiagItem
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
        public string Status { get; set; } // ok | warn | bad | info
    }

    public sealed class HealthReport
    {
        public SystemSnapshot Snapshot;
        public List<DiagItem> Items = new List<DiagItem>();
    }

    public sealed class HealthService
    {
        private readonly SystemInfoService sysInfo = new SystemInfoService();

        public HealthReport Run(Action<string> progress)
        {
            var report = new HealthReport();
            progress("Leyendo memoria y disco...");
            SystemSnapshot s = sysInfo.Capture();
            report.Snapshot = s;

            report.Items.Add(new DiagItem
            {
                Title = "Memoria RAM",
                Value = s.RamTotalGb > 0 ? s.RamUsedGb.ToString("0.0") + " / " + s.RamTotalGb.ToString("0") + " GB (" + s.RamLoadPercent.ToString("0") + "%)" : "N/D",
                Detail = s.RamLoadPercent >= 85 ? "Uso alto: cerrá apps o sumá RAM." : "Uso normal.",
                Status = s.RamLoadPercent >= 90 ? "bad" : s.RamLoadPercent >= 80 ? "warn" : "ok"
            });

            double freePct = s.DiskTotalGb > 0 ? s.DiskFreeGb / s.DiskTotalGb * 100.0 : 100;
            report.Items.Add(new DiagItem
            {
                Title = "Disco del sistema",
                Value = s.DiskTotalGb > 0 ? s.DiskFreeGb.ToString("0") + " GB libres de " + s.DiskTotalGb.ToString("0") + " GB" : "N/D",
                Detail = freePct < 15 ? "Poco espacio: usá Limpieza para liberar." : "Espacio suficiente.",
                Status = freePct < 8 ? "bad" : freePct < 15 ? "warn" : "ok"
            });

            progress("Consultando estado de los discos (SMART)...");
            AddSmart(report);

            report.Items.Add(new DiagItem
            {
                Title = "Apps al inicio",
                Value = s.StartupCount + " configuradas",
                Detail = s.StartupCount >= 12 ? "Bastantes: revisá la sección Arranque." : "Cantidad razonable.",
                Status = s.StartupCount >= 18 ? "bad" : s.StartupCount >= 12 ? "warn" : "ok"
            });

            progress("Verificando reinicio pendiente...");
            bool reboot = PendingReboot();
            report.Items.Add(new DiagItem
            {
                Title = "Reinicio pendiente",
                Value = reboot ? "Sí" : "No",
                Detail = reboot ? "Windows tiene cambios que se aplican al reiniciar." : "Sin reinicios pendientes.",
                Status = reboot ? "warn" : "ok"
            });

            report.Items.Add(new DiagItem
            {
                Title = "Tiempo encendido",
                Value = FormatUptime(s.Uptime),
                Detail = s.Uptime.TotalDays >= 5 ? "Reiniciar de vez en cuando ayuda al rendimiento." : "OK.",
                Status = s.Uptime.TotalDays >= 7 ? "warn" : "ok"
            });

            return report;
        }

        private void AddSmart(HealthReport report)
        {
            try
            {
                string cmd = "Get-PhysicalDisk | Select-Object FriendlyName,MediaType,HealthStatus,@{N='SizeGB';E={[math]::Round($_.Size/1GB)}} | ConvertTo-Json -Compress -Depth 2";
                PowerShellRunner.PsResult r = PowerShellRunner.Run(cmd, 30000);
                if (string.IsNullOrWhiteSpace(r.Output)) return;
                using (JsonDocument doc = JsonDocument.Parse(r.Output))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement e in root.EnumerateArray()) AddDisk(report, e);
                    else if (root.ValueKind == JsonValueKind.Object)
                        AddDisk(report, root);
                }
            }
            catch { }
        }

        private static void AddDisk(HealthReport report, JsonElement e)
        {
            string name = Str(e, "FriendlyName");
            string health = Str(e, "HealthStatus");
            string media = Str(e, "MediaType");
            string size = Str(e, "SizeGB");
            bool healthy = health.Equals("Healthy", StringComparison.OrdinalIgnoreCase);
            report.Items.Add(new DiagItem
            {
                Title = "Disco físico: " + (string.IsNullOrWhiteSpace(name) ? "desconocido" : name),
                Value = (string.IsNullOrWhiteSpace(media) ? "" : media + " · ") + (string.IsNullOrWhiteSpace(size) ? "" : size + " GB · ") + "SMART: " + (string.IsNullOrWhiteSpace(health) ? "N/D" : health),
                Detail = healthy ? "El disco reporta estado saludable." : "Atención: respaldá tus datos y revisá el disco.",
                Status = healthy ? "ok" : string.IsNullOrWhiteSpace(health) ? "info" : "bad"
            });
        }

        private static bool PendingReboot()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                    if (k != null) return true;
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                    if (k != null) return true;
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
                    if (k != null && k.GetValue("PendingFileRenameOperations") != null) return true;
            }
            catch { }
            return false;
        }

        public string RunDism(Action<string> progress)
        {
            progress("Ejecutando DISM /CheckHealth (puede tardar)...");
            PowerShellRunner.PsResult r = PowerShellRunner.Run("DISM /Online /Cleanup-Image /CheckHealth | Out-String", 240000);
            string outp = (r.Output ?? "").Trim();
            if (string.IsNullOrWhiteSpace(outp)) outp = (r.Error ?? "").Trim();
            return string.IsNullOrWhiteSpace(outp) ? "DISM no devolvió salida." : outp;
        }

        public string RunSfc(Action<string> progress)
        {
            progress("Ejecutando SFC /scannow (varios minutos)...");
            PowerShellRunner.PsResult r = PowerShellRunner.Run("sfc /scannow | Out-String", 1500000);
            string outp = (r.Output ?? "").Trim();
            if (string.IsNullOrWhiteSpace(outp)) outp = (r.Error ?? "").Trim();
            return string.IsNullOrWhiteSpace(outp) ? "SFC no devolvió salida." : outp;
        }

        private static string FormatUptime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return ((int)t.TotalDays) + "d " + t.Hours + "h";
            if (t.TotalHours >= 1) return t.Hours + "h " + t.Minutes + "m";
            return Math.Max(1, t.Minutes) + "m";
        }

        private static string Str(JsonElement e, string key)
        {
            JsonElement v;
            if (!e.TryGetProperty(key, out v)) return "";
            return v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : (v.ValueKind == JsonValueKind.Null ? "" : v.ToString());
        }
    }
}
