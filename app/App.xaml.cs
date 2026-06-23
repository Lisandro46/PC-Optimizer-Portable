using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Threading;

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
                RunSelfTest();
                return;
            }

            new MainWindow().Show();
        }

        // Recorre todas las vistas con el dispatcher corriendo y captura cualquier
        // excepción no controlada (incluye crashes async durante la carga/medición).
        private void RunSelfTest()
        {
            var log = new StringBuilder();
            var errors = new List<string>();

            DispatcherUnhandledException += delegate (object s, DispatcherUnhandledExceptionEventArgs ex)
            {
                errors.Add("UNHANDLED | " + ex.Exception.GetType().Name + ": " + ex.Exception.Message);
                ex.Handled = true;
            };

            MainWindow win;
            try { win = new MainWindow(); }
            catch (Exception ctorEx)
            {
                Finalize(log, new List<string> { "MainWindow ctor | " + ctorEx.Message });
                Shutdown();
                return;
            }

            win.ShowInTaskbar = false;
            win.WindowStartupLocation = WindowStartupLocation.Manual;
            win.Left = -4000;
            win.Top = -4000;
            win.Width = 1200;
            win.Height = 760;
            win.Show();

            string[] tags = { "dashboard", "apps", "cleanup", "startup", "services", "privacy", "performance", "health" };
            int idx = 0;
            int extra = 6; // ticks de margen para que terminen las cargas async

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += delegate
            {
                if (idx < tags.Length)
                {
                    try { win.NavigateTo(tags[idx]); log.AppendLine("NAV  | " + tags[idx]); }
                    catch (Exception navEx) { errors.Add("NAV " + tags[idx] + " | " + navEx.Message); }
                    idx++;
                    return;
                }
                if (extra-- > 0) return;

                timer.Stop();
                Finalize(log, errors);
                Shutdown();
            };
            timer.Start();
        }

        private static void Finalize(StringBuilder log, List<string> errors)
        {
            log.AppendLine();
            if (errors.Count == 0) log.AppendLine("RESULTADO: SIN ERRORES");
            else
            {
                log.AppendLine("RESULTADO: " + errors.Count + " ERROR(ES)");
                foreach (string er in errors) log.AppendLine("FAIL | " + er);
            }
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "pcopt-selftest.txt"), log.ToString(), new UTF8Encoding(false)); }
            catch { }
        }
    }
}
