using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PcOptimizer
{
    public partial class TweaksView : UserControl
    {
        private readonly TweaksService service = new TweaksService();
        private readonly string category;
        private List<TweakItem> items = new List<TweakItem>();
        private bool busy;

        public TweaksView(string category, string title, string subtitle)
        {
            InitializeComponent();
            this.category = category;
            TitleText.Text = title;
            SubText.Text = subtitle;
            Loaded += delegate { if (items.Count == 0) Load(); };
        }

        private async void Load()
        {
            SetBusy(true, "Leyendo estado actual...");
            items = service.Build(category);
            Grid.ItemsSource = items;
            foreach (TweakItem t in items) t.PropertyChanged += delegate { UpdateApply(); };
            List<TweakItem> snapshot = items;
            await Task.Run(() => service.Refresh(snapshot));
            int applied = items.Count(i => i.Applied);
            SetBusy(false, applied + " de " + items.Count + " optimizaciones ya activas. Marcá las que quieras aplicar.");
        }

        private void Rescan_Click(object sender, RoutedEventArgs e) { if (!busy) Load(); }
        private void RowCheck_Click(object sender, RoutedEventArgs e) { UpdateApply(); }

        private void UpdateApply()
        {
            int changes = items.Count(i => i.Selected != i.Applied);
            ApplyButton.IsEnabled = !busy && changes > 0;
            ApplyButton.Content = changes > 0 ? "APLICAR " + changes + " CAMBIO" + (changes == 1 ? "" : "S") : "APLICAR CAMBIOS";
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            List<TweakItem> changed = items.Where(i => i.Selected != i.Applied).ToList();
            if (changed.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("Cambios a aplicar:");
            sb.AppendLine();
            foreach (TweakItem t in changed) sb.AppendLine((t.Selected ? "✓ Activar: " : "✗ Revertir: ") + t.Title);
            sb.AppendLine("\nTodo esto es reversible desde acá. ¿Continuar?");
            if (MessageBox.Show(sb.ToString(), "Confirmar optimizaciones", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            SetBusy(true, "Aplicando...");
            var progress = new Progress<string>(m => Status.Text = m);
            List<string> messages = await Task.Run(() => service.Apply(changed, s => ((IProgress<string>)progress).Report(s)));

            string log = "PC Optimizer — " + category + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n" + string.Join("\n", messages);
            string path = LogService.Save(category, log);
            int ok = messages.Count(m => m.StartsWith("OK"));
            int failed = messages.Count - ok;
            SetBusy(false, "Listo: " + ok + " aplicados, " + failed + " con error.");
            if (failed > 0)
                MessageBox.Show("Aplicados: " + ok + "\nCon error: " + failed + "\n\nAlgunos cambios pueden requerir cerrar sesión.\nRegistro: " + path, "Optimizaciones", MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateApply();
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
