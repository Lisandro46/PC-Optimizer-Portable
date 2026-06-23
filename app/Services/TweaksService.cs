using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PcOptimizer
{
    public sealed class TweakItem : INotifyPropertyChanged
    {
        public enum TweakKind { RegistryDword, PowerPlan }

        public string Title { get; set; }
        public string Description { get; set; }
        public string Risk { get; set; }
        public string Category { get; set; }
        public TweakKind Kind { get; set; }

        public bool Hklm { get; set; }
        public string Path { get; set; }
        public string ValueName { get; set; }
        public int Optimized { get; set; }
        public int Default { get; set; }

        private bool applied;
        public bool Applied { get { return applied; } set { applied = value; Notify(); } }

        private bool selected;
        public bool Selected { get { return selected; } set { selected = value; Notify(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string n = null)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(n));
        }
    }

    public sealed class TweaksService
    {
        private const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

        public List<TweakItem> Build(string category)
        {
            return category == "performance" ? BuildPerformance() : BuildPrivacy();
        }

        private static List<TweakItem> BuildPrivacy()
        {
            return new List<TweakItem>
            {
                Reg("privacy", "Telemetría al mínimo", "Reduce los datos de diagnóstico que Windows envía a Microsoft.", "BAJO",
                    true, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, 1),
                Reg("privacy", "Desactivar ID de publicidad", "Evita que las apps usen un identificador para anuncios personalizados.", "BAJO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1),
                Reg("privacy", "Desactivar historial de actividad", "Windows deja de publicar tu actividad (Timeline).", "BAJO",
                    true, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, 1),
                Reg("privacy", "Quitar contenido y consejos sugeridos", "Desactiva sugerencias del menú Inicio y Configuración.", "BAJO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, 1),
                Reg("privacy", "Quitar Bing del menú Inicio", "La búsqueda del menú Inicio deja de consultar la web.", "BAJO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, 1),
                Reg("privacy", "Sin sugerencias en pantalla de bloqueo", "Desactiva contenido y datos curiosos en la pantalla de bloqueo.", "BAJO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled", 0, 1),
            };
        }

        private static List<TweakItem> BuildPerformance()
        {
            return new List<TweakItem>
            {
                new TweakItem { Category = "performance", Kind = TweakItem.TweakKind.PowerPlan,
                    Title = "Plan de energía: Alto rendimiento", Risk = "BAJO",
                    Description = "Prioriza rendimiento sobre ahorro. En notebooks consume más batería." },
                Reg("performance", "Efectos visuales para rendimiento", "Reduce animaciones y sombras para una interfaz más ágil.", "BAJO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, 0),
                Reg("performance", "Desactivar apps en segundo plano", "Apps de la Store dejan de ejecutarse en segundo plano.", "MEDIO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, 0),
                Reg("performance", "Sin retraso de apps al inicio", "Quita la demora artificial de las apps de inicio.", "BAJO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, 1),
                Reg("performance", "Desactivar transparencias", "Quita el efecto de transparencia (ahorra algo de GPU).", "BAJO",
                    false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1),
            };
        }

        public void Refresh(IEnumerable<TweakItem> items)
        {
            foreach (TweakItem t in items)
            {
                bool applied = false;
                try
                {
                    if (t.Kind == TweakItem.TweakKind.PowerPlan)
                    {
                        PowerShellRunner.PsResult r = PowerShellRunner.Run("powercfg /getactivescheme", 15000);
                        applied = r.Output != null && r.Output.IndexOf(HighPerf, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    else
                    {
                        using (RegistryKey key = (t.Hklm ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(t.Path))
                        {
                            object v = key == null ? null : key.GetValue(t.ValueName);
                            applied = v != null && Convert.ToInt32(v) == t.Optimized;
                        }
                    }
                }
                catch { }
                t.Applied = applied;
                t.Selected = applied;
            }
        }

        public List<string> Apply(IEnumerable<TweakItem> items, Action<string> progress)
        {
            var messages = new List<string>();
            foreach (TweakItem t in items)
            {
                if (t.Selected == t.Applied) continue;
                progress((t.Selected ? "Aplicando: " : "Revirtiendo: ") + t.Title);
                try
                {
                    if (t.Kind == TweakItem.TweakKind.PowerPlan)
                    {
                        PowerShellRunner.PsResult r = PowerShellRunner.Run("powercfg /setactive " + (t.Selected ? HighPerf : Balanced), 15000);
                        if (!r.Ok) throw new Exception(string.IsNullOrWhiteSpace(r.Error) ? "powercfg falló" : r.Error.Trim());
                    }
                    else
                    {
                        using (RegistryKey key = (t.Hklm ? Registry.LocalMachine : Registry.CurrentUser).CreateSubKey(t.Path))
                        {
                            key.SetValue(t.ValueName, t.Selected ? t.Optimized : t.Default, RegistryValueKind.DWord);
                        }
                    }
                    t.Applied = t.Selected;
                    messages.Add("OK | " + t.Title + " | " + (t.Selected ? "aplicado" : "revertido"));
                }
                catch (Exception ex)
                {
                    t.Selected = t.Applied;
                    messages.Add("ERROR | " + t.Title + " | " + ex.Message);
                }
            }
            return messages;
        }

        private static TweakItem Reg(string cat, string title, string desc, string risk, bool hklm, string path, string name, int optimized, int def)
        {
            return new TweakItem
            {
                Category = cat,
                Kind = TweakItem.TweakKind.RegistryDword,
                Title = title,
                Description = desc,
                Risk = risk,
                Hklm = hklm,
                Path = path,
                ValueName = name,
                Optimized = optimized,
                Default = def
            };
        }
    }
}
