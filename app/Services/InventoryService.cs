using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PcOptimizer
{
    public sealed class InventoryService
    {
        public List<string> Errors { get; private set; }

        public InventoryService()
        {
            Errors = new List<string>();
        }

        public List<AppItem> Scan(Action<string> progress)
        {
            Errors.Clear();
            var all = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);

            progress("Leyendo programas tradicionales...");
            ScanRegistry(all, RegistryHive.LocalMachine, RegistryView.Registry64, "Todos los usuarios · 64 bits");
            ScanRegistry(all, RegistryHive.LocalMachine, RegistryView.Registry32, "Todos los usuarios · 32 bits");
            ScanRegistry(all, RegistryHive.CurrentUser, RegistryView.Registry64, "Usuario actual · 64 bits");
            ScanRegistry(all, RegistryHive.CurrentUser, RegistryView.Registry32, "Usuario actual · 32 bits");

            progress("Buscando componentes especiales de Windows...");
            AddEdgeComponent(all);

            progress("Leyendo aplicaciones de Microsoft Store...");
            ScanStoreApps(all);

            progress("Leyendo aplicaciones preinstaladas para usuarios futuros...");
            ScanProvisionedApps(all);

            progress("Leyendo características opcionales de Windows...");
            ScanOptionalFeatures(all);

            progress("Leyendo capacidades de Windows...");
            ScanCapabilities(all);

            var result = all.Values.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            progress("Inventario listo: " + result.Count + " elementos.");
            return result;
        }

        private void ScanRegistry(Dictionary<string, AppItem> all, RegistryHive hive, RegistryView view, string scope)
        {
            const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var root = baseKey.OpenSubKey(path))
                {
                    if (root == null) return;
                    foreach (string subName in root.GetSubKeyNames())
                    {
                        try
                        {
                            using (var key = root.OpenSubKey(subName))
                            {
                                if (key == null) continue;
                                string name = Value(key, "DisplayName");
                                if (String.IsNullOrWhiteSpace(name)) continue;

                                string uninstall = Value(key, "UninstallString");
                                string quiet = Value(key, "QuietUninstallString");
                                var item = new AppItem();
                                item.Id = subName;
                                item.Name = name.Trim();
                                item.Version = Value(key, "DisplayVersion");
                                item.Publisher = Value(key, "Publisher");
                                item.Source = "Programa tradicional";
                                item.Scope = scope;
                                item.State = "Instalado";
                                item.InstallLocation = Value(key, "InstallLocation");
                                item.ExecutableHint = ExecutableNameFromDisplayIcon(Value(key, "DisplayIcon"));
                                item.UninstallCommand = uninstall;
                                item.QuietUninstallCommand = quiet;
                                item.Kind = AppKind.Classic;
                                item.CanUninstall = !String.IsNullOrWhiteSpace(uninstall) || !String.IsNullOrWhiteSpace(quiet);
                                item.CanDisable = IsEdge(item.Name);
                                item.Action = "Desinstalar";
                                Enrich(item, Convert.ToInt32(key.GetValue("SystemComponent", 0)) == 1);

                                string dedupe = "Classic|" + item.Name + "|" + item.Version + "|" + item.UninstallCommand + "|" + scope.Split('·')[0];
                                if (!all.ContainsKey(dedupe)) all.Add(dedupe, item);
                            }
                        }
                        catch (Exception ex)
                        {
                            Errors.Add("Registro " + subName + ": " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Errors.Add("No se pudo leer " + hive + " " + view + ": " + ex.Message);
            }
        }

        private void AddEdgeComponent(Dictionary<string, AppItem> all)
        {
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application");
            if (!Directory.Exists(basePath)) return;

            try
            {
                string setup = Directory.GetDirectories(basePath)
                    .Select(d => new { Dir = d, Setup = Path.Combine(d, "Installer", "setup.exe"), Name = Path.GetFileName(d) })
                    .Where(x => File.Exists(x.Setup))
                    .OrderByDescending(x => ParseVersion(x.Name))
                    .Select(x => x.Setup)
                    .FirstOrDefault();

                if (String.IsNullOrWhiteSpace(setup)) return;
                string version = Directory.GetParent(Directory.GetParent(setup).FullName).Name;
                var item = new AppItem();
                item.Id = "MicrosoftEdgeChromium";
                item.Name = "Microsoft Edge (componente de Windows)";
                item.Version = version;
                item.Publisher = "Microsoft Corporation";
                item.Source = "Componente de Windows";
                item.Scope = "Todos los usuarios";
                item.State = "Instalado";
                item.InstallLocation = basePath;
                item.ExecutableHint = "msedge";
                item.UninstallCommand = Quote(setup) + " --uninstall --system-level --verbose-logging --force-uninstall";
                item.Kind = AppKind.WindowsComponent;
                item.CanUninstall = true;
                item.CanDisable = true;
                item.Action = "Desinstalar";
                Enrich(item, true);
                all["WindowsComponent|MicrosoftEdgeChromium"] = item;
            }
            catch (Exception ex)
            {
                Errors.Add("Edge: " + ex.Message);
            }
        }

        private void ScanStoreApps(Dictionary<string, AppItem> all)
        {
            string command = "$ProgressPreference='SilentlyContinue'; Get-AppxPackage -AllUsers | Select-Object Name,PackageFullName,Publisher,Version,InstallLocation,NonRemovable,IsFramework | ConvertTo-Json -Compress -Depth 3";
            foreach (var row in JsonRows(RunPowerShell(command, 180000)))
            {
                string full = S(row, "PackageFullName");
                string name = S(row, "Name");
                if (String.IsNullOrWhiteSpace(full) || String.IsNullOrWhiteSpace(name)) continue;
                var item = new AppItem();
                item.Id = full;
                item.Name = FriendlyPackageName(name);
                item.Version = S(row, "Version");
                item.Publisher = FriendlyPublisher(S(row, "Publisher"));
                item.Source = "Microsoft Store / AppX";
                item.Scope = "Uno o más usuarios";
                item.State = "Instalado";
                item.InstallLocation = S(row, "InstallLocation");
                item.PackageFullName = full;
                item.NonRemovable = B(row, "NonRemovable");
                item.IsFramework = B(row, "IsFramework");
                item.Kind = AppKind.StoreApp;
                item.CanUninstall = true;
                item.CanDisable = IsEdge(item.Name);
                item.Action = "Desinstalar";
                Enrich(item, false);
                all[item.UniqueKey] = item;
            }
        }

        private void ScanProvisionedApps(Dictionary<string, AppItem> all)
        {
            string command = "$ProgressPreference='SilentlyContinue'; Get-AppxProvisionedPackage -Online | Select-Object DisplayName,PackageName,PublisherId,Version,InstallLocation | ConvertTo-Json -Compress -Depth 3";
            foreach (var row in JsonRows(RunPowerShell(command, 180000)))
            {
                string package = S(row, "PackageName");
                string name = S(row, "DisplayName");
                if (String.IsNullOrWhiteSpace(package) || String.IsNullOrWhiteSpace(name)) continue;
                var item = new AppItem();
                item.Id = package;
                item.Name = FriendlyPackageName(name) + " (preinstalada)";
                item.Version = S(row, "Version");
                item.Publisher = FriendlyPublisher(S(row, "PublisherId"));
                item.Source = "App preinstalada";
                item.Scope = "Usuarios futuros";
                item.State = "Aprovisionada";
                item.InstallLocation = S(row, "InstallLocation");
                item.ProvisionedPackageName = package;
                item.Kind = AppKind.ProvisionedApp;
                item.CanUninstall = true;
                item.CanDisable = false;
                item.Action = "Desinstalar";
                Enrich(item, false);
                all[item.UniqueKey] = item;
            }
        }

        private void ScanOptionalFeatures(Dictionary<string, AppItem> all)
        {
            string command = "$ProgressPreference='SilentlyContinue'; Get-WindowsOptionalFeature -Online | Select-Object FeatureName,State | ConvertTo-Json -Compress -Depth 2";
            foreach (var row in JsonRows(RunPowerShell(command, 240000)))
            {
                string name = S(row, "FeatureName");
                if (String.IsNullOrWhiteSpace(name)) continue;
                var item = new AppItem();
                item.Id = name;
                item.Name = name;
                item.Publisher = "Microsoft Windows";
                item.Source = "Característica opcional";
                item.Scope = "Sistema";
                item.State = S(row, "State");
                item.FeatureName = name;
                item.Kind = AppKind.OptionalFeature;
                item.CanUninstall = false;
                item.CanDisable = item.State.IndexOf("Enabled", StringComparison.OrdinalIgnoreCase) >= 0 || item.State.IndexOf("Enable Pending", StringComparison.OrdinalIgnoreCase) >= 0;
                item.Action = "Desactivar";
                Enrich(item, false);
                all[item.UniqueKey] = item;
            }
        }

        private void ScanCapabilities(Dictionary<string, AppItem> all)
        {
            string command = "$ProgressPreference='SilentlyContinue'; Get-WindowsCapability -Online | Select-Object Name,State | ConvertTo-Json -Compress -Depth 2";
            foreach (var row in JsonRows(RunPowerShell(command, 240000)))
            {
                string name = S(row, "Name");
                if (String.IsNullOrWhiteSpace(name)) continue;
                var item = new AppItem();
                item.Id = name;
                item.Name = name;
                item.Publisher = "Microsoft Windows";
                item.Source = "Capacidad de Windows";
                item.Scope = "Sistema";
                item.State = S(row, "State");
                item.CapabilityName = name;
                item.Kind = AppKind.WindowsCapability;
                item.CanUninstall = item.State.Equals("Installed", StringComparison.OrdinalIgnoreCase);
                item.CanDisable = false;
                item.Action = "Desinstalar";
                Enrich(item, false);
                all[item.UniqueKey] = item;
            }
        }

        private static void Enrich(AppItem item, bool hiddenSystemComponent)
        {
            string n = (item.Name + " " + item.Id).ToLowerInvariant();
            item.Description = Describe(n, item);
            item.Risk = RiskFor(n, item, hiddenSystemComponent);

            if (item.NonRemovable)
                item.RiskReason = "Windows marca este paquete como no removible. El intento experto puede fallar o afectar el inicio de sesión.";
            else if (item.IsFramework || ContainsAny(n, "vclibs", ".net.native", "uixaml", "framework", "webview2"))
                item.RiskReason = "Es una biblioteca compartida: otras aplicaciones pueden dejar de abrir.";
            else if (item.Risk == RiskLevel.Critico)
                item.RiskReason = "Participa en funciones centrales de Windows. Quitarla puede romper la interfaz, la seguridad o el inicio de sesión.";
            else if (item.Risk == RiskLevel.Alto)
                item.RiskReason = "Tiene dependencias del sistema o de otras aplicaciones. Se recomienda desactivar antes que eliminar.";
            else if (item.Risk == RiskLevel.Medio)
                item.RiskReason = "Puede afectar hardware, compatibilidad o funciones que quizá uses.";
            else
                item.RiskReason = "No se detectaron dependencias críticas conocidas, pero toda desinstalación puede eliminar configuración propia.";
        }

        private static RiskLevel RiskFor(string n, AppItem item, bool hidden)
        {
            if (item.NonRemovable || ContainsAny(n,
                "shellexperience", "startmenuexperience", "windows.search", "sechealth", "defender",
                "aad.broker", "cloudexperience", "immersivecontrolpanel", "client.cbs", "lockapp",
                "apprep", "textinput", "windowslogon", "credential", "oobenetwork", "contentdeliverymanager"))
                return RiskLevel.Critico;

            if (item.IsFramework || ContainsAny(n,
                "vclibs", ".net.native", "uixaml", "framework", "webview2", "visual c++", "redistributable",
                "microsoft edge", "hyper-v", "netfx", "subsystem-linux", "wsl", "powershell", "appinstaller",
                "desktopappinstaller", "storepurchase", "windowsstore"))
                return RiskLevel.Alto;

            if (hidden || ContainsAny(n,
                "driver", "intel", "nvidia", "amd", "realtek", "bluetooth", "wireless", "codec", "mediafeature",
                "directplay", "iis", "openssh", "sandbox", "containers", "printing", "xps", "internet-explorer"))
                return RiskLevel.Medio;

            if (item.Kind == AppKind.OptionalFeature || item.Kind == AppKind.WindowsCapability)
                return RiskLevel.Medio;
            return RiskLevel.Bajo;
        }

        private static string Describe(string n, AppItem item)
        {
            if (ContainsAny(n, "microsoft edge", "microsoftedge")) return "Navegador de Microsoft y componente usado por algunas funciones web de Windows.";
            if (n.Contains("webview2")) return "Motor que permite a otras aplicaciones mostrar contenido web dentro de sus ventanas.";
            if (ContainsAny(n, "defender", "sechealth")) return "Protección antivirus y panel de Seguridad de Windows.";
            if (n.Contains("windowsstore")) return "Tienda oficial para instalar y actualizar aplicaciones de Windows.";
            if (ContainsAny(n, "desktopappinstaller", "appinstaller")) return "Instala paquetes MSIX y proporciona el administrador de paquetes WinGet.";
            if (n.Contains("calculator")) return "Calculadora de Windows.";
            if (ContainsAny(n, "windows.photos", "microsoft.photos")) return "Visor, organizador y editor básico de fotos y videos.";
            if (n.Contains("windowscamera")) return "Aplicación para usar cámaras y webcams.";
            if (ContainsAny(n, "xbox", "gamingservices")) return "Servicios y aplicaciones para juegos de Xbox y títulos de Microsoft Store.";
            if (n.Contains("onedrive")) return "Sincroniza archivos con la nube de Microsoft OneDrive.";
            if (n.Contains("teams")) return "Aplicación de chat, llamadas y reuniones de Microsoft.";
            if (n.Contains("clipchamp")) return "Editor de video de Microsoft.";
            if (n.Contains("copilot")) return "Asistente de inteligencia artificial de Microsoft.";
            if (n.Contains("cortana")) return "Asistente de voz heredado de Microsoft.";
            if (n.Contains("windowsterminal")) return "Terminal moderna para PowerShell, Símbolo del sistema y otras consolas.";
            if (n.Contains("windowsnotepad")) return "Editor de texto Bloc de notas.";
            if (ContainsAny(n, "mspaint", "paint")) return "Editor básico de imágenes de Windows.";
            if (n.Contains("stickynotes")) return "Notas rápidas sincronizables.";
            if (n.Contains("people")) return "Gestión e integración de contactos de Windows.";
            if (n.Contains("gethelp")) return "Aplicación de ayuda y soporte de Microsoft.";
            if (n.Contains("feedbackhub")) return "Envía comentarios y diagnósticos a Microsoft.";
            if (n.Contains("windowsmaps")) return "Mapas y navegación de Windows.";
            if (ContainsAny(n, "bingweather", "weather")) return "Aplicación y contenido meteorológico.";
            if (ContainsAny(n, "bingnews", "news")) return "Aplicación y contenido de noticias de Microsoft.";
            if (n.Contains("solitaire")) return "Colección de juegos de cartas de Microsoft.";
            if (ContainsAny(n, "yourphone", "phonelink")) return "Vincula Windows con un teléfono para notificaciones, archivos y llamadas.";
            if (n.Contains("mixedreality")) return "Componentes para cascos y experiencias de realidad mixta.";
            if (n.Contains("shellexperience")) return "Parte central de la interfaz: escritorio, paneles y experiencias visuales de Windows.";
            if (n.Contains("startmenuexperience")) return "Componente que dibuja y controla el menú Inicio.";
            if (n.Contains("windows.search")) return "Búsqueda del menú Inicio y del sistema.";
            if (n.Contains("immersivecontrolpanel")) return "Aplicación Configuración de Windows.";
            if (ContainsAny(n, "vclibs", ".net.native", "uixaml", "framework")) return "Biblioteca compartida necesaria para que otras aplicaciones puedan ejecutarse.";
            if (item.Kind == AppKind.OptionalFeature) return "Característica opcional de Windows. Su función exacta se identifica por el nombre técnico mostrado.";
            if (item.Kind == AppKind.WindowsCapability) return "Capacidad instalable de Windows, como idioma, administración, red o herramientas del sistema.";
            if (item.Kind == AppKind.ProvisionedApp) return "Copia base que Windows instala automáticamente a las cuentas de usuario nuevas.";
            if (item.Kind == AppKind.StoreApp) return "Aplicación o componente empaquetado de Microsoft Store. Puede pertenecer a Windows o a un tercero.";
            return "Programa instalado por " + (String.IsNullOrWhiteSpace(item.Publisher) ? "un editor no identificado" : item.Publisher) + ".";
        }

        private string RunPowerShell(string command, int timeoutMs)
        {
            try
            {
                string fullCommand = "[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false);" + command;
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(fullCommand));
                var psi = new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    Errors.Add("Una consulta de Windows superó el tiempo máximo.");
                    return "";
                }
                if (p.ExitCode != 0 && !String.IsNullOrWhiteSpace(error)) Errors.Add(error.Trim());
                return output.Trim();
            }
            catch (Exception ex)
            {
                Errors.Add(ex.Message);
                return "";
            }
        }

        private List<JsonElement> JsonRows(string json)
        {
            var rows = new List<JsonElement>();
            if (String.IsNullOrWhiteSpace(json)) return rows;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement element in root.EnumerateArray())
                            if (element.ValueKind == JsonValueKind.Object) rows.Add(element.Clone());
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        rows.Add(root.Clone());
                    }
                }
            }
            catch (Exception ex)
            {
                Errors.Add("Respuesta de Windows no reconocida: " + ex.Message);
            }
            return rows;
        }

        private static string Value(RegistryKey key, string name)
        {
            object value = key.GetValue(name, "");
            return value == null ? "" : Convert.ToString(value);
        }

        private static string S(JsonElement row, string key)
        {
            JsonElement value;
            if (!row.TryGetProperty(key, out value)) return "";
            switch (value.ValueKind)
            {
                case JsonValueKind.String: return value.GetString() ?? "";
                case JsonValueKind.Number: return value.GetRawText();
                case JsonValueKind.True: return "True";
                case JsonValueKind.False: return "False";
                case JsonValueKind.Null:
                case JsonValueKind.Undefined: return "";
                default: return value.GetRawText();
            }
        }

        private static bool B(JsonElement row, string key)
        {
            JsonElement value;
            if (!row.TryGetProperty(key, out value)) return false;
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
            bool parsed;
            return Boolean.TryParse(value.ToString(), out parsed) && parsed;
        }

        private static string FriendlyPackageName(string name)
        {
            return name.Replace("Microsoft.", "Microsoft · ").Replace("MicrosoftCorporationII.", "Microsoft · ");
        }

        private static string ExecutableNameFromDisplayIcon(string displayIcon)
        {
            if (String.IsNullOrWhiteSpace(displayIcon)) return "";
            string value = Environment.ExpandEnvironmentVariables(displayIcon.Trim());
            int comma = value.LastIndexOf(',');
            if (comma > 0) value = value.Substring(0, comma);
            value = value.Trim().Trim('"');
            try { return Path.GetFileNameWithoutExtension(value); }
            catch { return ""; }
        }

        private static string FriendlyPublisher(string publisher)
        {
            if (publisher.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0 || publisher.IndexOf("CN=Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Microsoft Corporation";
            return publisher;
        }

        private static bool IsEdge(string value)
        {
            return value.IndexOf("Microsoft Edge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            foreach (string needle in needles) if (value.Contains(needle)) return true;
            return false;
        }

        private static string Quote(string path)
        {
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        private static Version ParseVersion(string value)
        {
            Version version;
            return Version.TryParse(value, out version) ? version : new Version(0, 0);
        }
    }
}
