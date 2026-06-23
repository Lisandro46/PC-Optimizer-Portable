using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace PcOptimizer
{
    public sealed class ServiceItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string StartMode { get; set; }
        public string State { get; set; }
        public string Description { get; set; }
        public string Risk { get; set; }
        public string RiskReason { get; set; }

        private string newMode = "Sin cambio";
        public string NewMode { get { return newMode; } set { newMode = value; Notify(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string n = null)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(n));
        }
    }

    public sealed class ServicesService
    {
        // Servicios que conviene NO tocar: núcleo del sistema, red, seguridad.
        private static readonly string[] Critical =
        {
            "rpcss", "dcomlaunch", "lsm", "plugplay", "power", "brokerinfrastructure", "systemeventsbroker",
            "winlogon", "profsvc", "themes", "audiosrv", "audioendpointbuilder", "schedule", "eventlog",
            "wsearch", "samss", "wscsvc", "wuauserv", "bits", "cryptsvc", "dnscache", "nsi", "nlasvc",
            "wlansvc", "netman", "dhcp", "mpssvc", "windefend", "wdnissvc", "securityhealthservice",
            "trustedinstaller", "msiserver", "gpsvc", "usermanager", "coremessagingregistrar"
        };

        public List<ServiceItem> Scan(Action<string> progress)
        {
            progress("Leyendo servicios en Automático...");
            var list = new List<ServiceItem>();
            string cmd = "Get-CimInstance Win32_Service | Where-Object { $_.StartMode -eq 'Auto' } | Select-Object Name,DisplayName,StartMode,State,Description | ConvertTo-Json -Compress -Depth 2";
            PowerShellRunner.PsResult res = PowerShellRunner.Run(cmd, 120000);
            if (string.IsNullOrWhiteSpace(res.Output)) return list;

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(res.Output))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement e in root.EnumerateArray()) Add(list, e);
                    else if (root.ValueKind == JsonValueKind.Object)
                        Add(list, root);
                }
            }
            catch { }
            list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        private static void Add(List<ServiceItem> list, JsonElement e)
        {
            string name = Str(e, "Name");
            if (string.IsNullOrWhiteSpace(name)) return;
            var item = new ServiceItem();
            item.Name = name;
            item.DisplayName = Str(e, "DisplayName");
            item.StartMode = "Automático";
            item.State = TranslateState(Str(e, "State"));
            item.Description = Str(e, "Description");
            AssignRisk(item);
            list.Add(item);
        }

        public List<string> Apply(IEnumerable<ServiceItem> items, Action<string> progress)
        {
            var messages = new List<string>();
            foreach (ServiceItem item in items)
            {
                if (item.NewMode == "Sin cambio") continue;
                string type = item.NewMode == "Manual" ? "Manual" : "Automatic";
                progress((item.NewMode == "Manual" ? "Pasando a Manual: " : "Volviendo a Automático: ") + item.DisplayName);
                string cmd = "Set-Service -Name '" + item.Name.Replace("'", "''") + "' -StartupType " + type + " -ErrorAction Stop";
                PowerShellRunner.PsResult res = PowerShellRunner.Run(cmd, 30000);
                if (res.Ok)
                {
                    item.StartMode = item.NewMode;
                    item.NewMode = "Sin cambio";
                    messages.Add("OK | " + item.DisplayName + " | " + type);
                }
                else
                {
                    messages.Add("ERROR | " + item.DisplayName + " | " + (string.IsNullOrWhiteSpace(res.Error) ? "no se pudo cambiar" : res.Error.Trim()));
                }
            }
            return messages;
        }

        private static void AssignRisk(ServiceItem item)
        {
            string key = (item.Name ?? "").ToLowerInvariant();
            foreach (string c in Critical)
            {
                if (key == c)
                {
                    item.Risk = "ALTO";
                    item.RiskReason = "Servicio del núcleo de Windows, red o seguridad. No se recomienda tocarlo.";
                    return;
                }
            }
            if (key.Contains("diagtrack") || key.Contains("dmwappushservice"))
            {
                item.Risk = "BAJO";
                item.RiskReason = "Telemetría/diagnóstico. Pasarlo a Manual reduce envío de datos sin romper el sistema.";
                return;
            }
            item.Risk = "MEDIO";
            item.RiskReason = "Servicio de terceros o no esencial. Pasarlo a Manual lo arranca solo cuando se necesita (reversible).";
        }

        private static string TranslateState(string s)
        {
            if (string.Equals(s, "Running", StringComparison.OrdinalIgnoreCase)) return "En ejecución";
            if (string.Equals(s, "Stopped", StringComparison.OrdinalIgnoreCase)) return "Detenido";
            return s;
        }

        private static string Str(JsonElement e, string key)
        {
            JsonElement v;
            if (!e.TryGetProperty(key, out v)) return "";
            return v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : (v.ValueKind == JsonValueKind.Null ? "" : v.ToString());
        }
    }
}
