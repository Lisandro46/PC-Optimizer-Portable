using System;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PcOptimizer
{
    public partial class App : Application
    {
        public static bool IsAdministrator { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                IsAdministrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            base.OnStartup(e);

            if (Array.IndexOf(e.Args, "--selftest") >= 0)
            {
                SelfTest();
                Shutdown();
                return;
            }

            new MainWindow().Show();
        }

        private static void SelfTest()
        {
            var sb = new StringBuilder();
            Check(sb, "DashboardView", () => new DashboardView());
            Check(sb, "AppsView", () => new AppsView());
            Check(sb, "CleanupView", () => new CleanupView());
            Check(sb, "StartupView", () => new StartupView());
            Check(sb, "ServicesView", () => new ServicesView());
            Check(sb, "TweaksView(privacy)", () => new TweaksView("privacy", "Privacidad", "x"));
            Check(sb, "TweaksView(performance)", () => new TweaksView("performance", "Rendimiento", "x"));
            Check(sb, "HealthView", () => new HealthView());
            Check(sb, "PlaceholderView", () => new PlaceholderView("x", "y"));
            Check(sb, "MainWindow", () => new MainWindow());

            string path = Path.Combine(Path.GetTempPath(), "pcopt-selftest.txt");
            try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); } catch { }
        }

        private static void Check(StringBuilder sb, string name, Func<object> create)
        {
            try { object o = create(); sb.AppendLine("OK  | " + name); }
            catch (Exception ex) { sb.AppendLine("FAIL| " + name + " | " + ex.GetType().Name + ": " + ex.Message); }
        }
    }
}
