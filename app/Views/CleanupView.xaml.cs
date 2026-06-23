using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PcOptimizer
{
    public partial class CleanupView : UserControl
    {
        private readonly CleanupService service = new CleanupService();
        private List<CleanupTarget> targets = new List<CleanupTarget>();
        private bool busy;

        public CleanupView()
        {
            InitializeComponent();
            Loaded += delegate { if (targets.Count == 0) Scan(); };
        }

        private async void Scan()
        {
            SetBusy(true, "Calculando espacio recuperable...");
            targets = service.BuildTargets();
            Grid.ItemsSource = targets;
            foreach (CleanupTarget t in targets) t.PropertyChanged += delegate { UpdateTotal(); };

            var progress = new Progress<string>(m => Status.Text = m);
            List<CleanupTarget> snapshot = targets;
            await Task.Run(() => service.Measure(snapshot, s => ((IProgress<string>)progress).Report(s)));

            long total = targets.Sum(t => t.SizeBytes);
            SetBusy(false, "Espacio recuperable detectado: " + LogService.HumanSize(total) + " en total.");
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            long sel = targets.Where(t => t.Selected).Sum(t => t.SizeBytes);
            TotalText.Text = sel > 0 ? "Seleccionado: " + LogService.HumanSize(sel) : "";
            CleanButton.IsEnabled = !busy && targets.Any(t => t.Selected);
        }

        private void RowCheck_Click(object sender, RoutedEventArgs e) { UpdateTotal(); }
        private void Rescan_Click(object sender, RoutedEventArgs e) { if (!busy) Scan(); }

        private async void Clean_Click(object sender, RoutedEventArgs e)
        {
            List<CleanupTarget> selected = targets.Where(t => t.Selected).ToList();
            if (selected.Count == 0) return;

            long expected = selected.Sum(t => t.SizeBytes);
            var sb = new StringBuilder();
            sb.AppendLine("Se va a limpiar:");
            sb.AppendLine();
            foreach (CleanupTarget t in selected) sb.AppendLine("• " + t.Name + "  (" + t.SizeText + ")");
            sb.AppendLine();
            sb.AppendLine("Total estimado a liberar: " + LogService.HumanSize(expected));
            sb.AppendLine();
            sb.AppendLine("Los archivos en uso se omiten. ¿Continuar?");
            if (MessageBox.Show(sb.ToString(), "Confirmar limpieza", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            SetBusy(true, "Limpiando...");
            var progress = new Progress<string>(m => Status.Text = m);
            CleanupResult result = await Task.Run(() => service.Clean(selected, s => ((IProgress<string>)progress).Report(s)));

            var log = new StringBuilder();
            log.AppendLine("PC Optimizer — Limpieza de disco");
            log.AppendLine("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.AppendLine("Total liberado: " + LogService.HumanSize(result.FreedBytes));
            log.AppendLine();
            foreach (string m in result.Messages) log.AppendLine(m);
            string path = LogService.Save("limpieza", log.ToString());

            SetBusy(false, "Limpieza finalizada: liberaste " + LogService.HumanSize(result.FreedBytes) + ".");
            MessageBox.Show("Espacio liberado: " + LogService.HumanSize(result.FreedBytes) + "\n\nRegistro: " + path, "Limpieza finalizada", MessageBoxButton.OK, MessageBoxImage.Information);
            Scan();
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            Progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            RescanButton.IsEnabled = !value;
            Grid.IsEnabled = !value;
            CleanButton.IsEnabled = !value && targets.Any(t => t.Selected);
            Status.Text = message;
        }
    }
}
