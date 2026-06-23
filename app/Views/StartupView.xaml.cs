using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PcOptimizer
{
    public partial class StartupView : UserControl
    {
        private readonly StartupService service = new StartupService();
        private List<StartupItem> items = new List<StartupItem>();

        public StartupView()
        {
            InitializeComponent();
            Loaded += delegate { if (items.Count == 0) Scan(); };
        }

        private async void Scan()
        {
            Status.Text = "Leyendo apps de inicio...";
            RescanButton.IsEnabled = false;
            items = await Task.Run(() => service.Scan());
            Grid.ItemsSource = items;
            int enabled = items.Count(i => i.Enabled);
            Status.Text = items.Count + " apps al inicio · " + enabled + " habilitadas. Tocá la casilla para habilitar o deshabilitar.";
            RescanButton.IsEnabled = true;
        }

        private void Rescan_Click(object sender, RoutedEventArgs e) { Scan(); }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb) || !(cb.DataContext is StartupItem item)) return;
            bool desired = cb.IsChecked == true;
            string result = service.SetEnabled(item, desired);
            if (result.StartsWith("ERROR", StringComparison.Ordinal))
            {
                item.Enabled = !desired;
                MessageBox.Show("No se pudo cambiar el estado de " + item.Name + ".\n\n" + result, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LogService.Save("arranque", "PC Optimizer — cambio de arranque\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + result + "\n");
            Status.Text = (desired ? "Habilitado: " : "Deshabilitado: ") + item.Name + ". Cambio reversible.";
        }
    }
}
