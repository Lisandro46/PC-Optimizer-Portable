using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PcOptimizer
{
    public partial class ServicesView : UserControl
    {
        private readonly ServicesService service = new ServicesService();
        private List<ServiceItem> items = new List<ServiceItem>();
        private bool busy;

        public ServicesView()
        {
            InitializeComponent();
            Loaded += delegate { if (items.Count == 0) Scan(); };
        }

        private async void Scan()
        {
            SetBusy(true, "Leyendo servicios...");
            var progress = new Progress<string>(m => Status.Text = m);
            items = await Task.Run(() => service.Scan(s => ((IProgress<string>)progress).Report(s)));
            Grid.ItemsSource = items;
            SetBusy(false, items.Count + " servicios en Automático. Elegí 'Manual' en los que quieras diferir.");
        }

        private void Rescan_Click(object sender, RoutedEventArgs e) { if (!busy) Scan(); }
        private void Mode_Changed(object sender, SelectionChangedEventArgs e) { UpdateApply(); }

        private void UpdateApply()
        {
            int changes = items.Count(i => i.NewMode != "Sin cambio");
            ApplyButton.IsEnabled = !busy && changes > 0;
            ApplyButton.Content = changes > 0 ? "APLICAR " + changes + " CAMBIO" + (changes == 1 ? "" : "S") : "APLICAR CAMBIOS";
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            List<ServiceItem> changed = items.Where(i => i.NewMode != "Sin cambio").ToList();
            if (changed.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("Se van a cambiar estos servicios:");
            sb.AppendLine();
            foreach (ServiceItem i in changed) sb.AppendLine("• " + i.DisplayName + "  →  " + i.NewMode + (i.Risk == "ALTO" ? "   [ALTO]" : ""));
            if (changed.Any(i => i.Risk == "ALTO")) sb.AppendLine("\n⚠ Hay servicios marcados ALTO (núcleo/seguridad). Revisá bien antes de continuar.");
            sb.AppendLine("\nEl cambio es reversible (podés volver a Automático). ¿Continuar?");
            if (MessageBox.Show(sb.ToString(), "Confirmar cambios de servicios", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            SetBusy(true, "Aplicando cambios...");
            var progress = new Progress<string>(m => Status.Text = m);
            List<string> messages = await Task.Run(() => service.Apply(changed, s => ((IProgress<string>)progress).Report(s)));

            string log = "PC Optimizer — Servicios\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n" + string.Join("\n", messages);
            string path = LogService.Save("servicios", log);
            int ok = messages.Count(m => m.StartsWith("OK"));
            int failed = messages.Count - ok;
            SetBusy(false, "Cambios aplicados: " + ok + " correctos, " + failed + " con error.");
            MessageBox.Show("Correctos: " + ok + "\nCon error: " + failed + "\n\nRegistro: " + path, "Servicios actualizados", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            Scan();
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            Progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            RescanButton.IsEnabled = !value;
            Grid.IsEnabled = !value;
            Status.Text = message;
            UpdateApply();
        }
    }
}
