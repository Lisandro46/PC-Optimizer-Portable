using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PcOptimizer
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, UserControl> cache = new Dictionary<string, UserControl>();

        public MainWindow()
        {
            InitializeComponent();
            AdminBadge.Text = App.IsAdministrator
                ? "Permisos de administrador: OK"
                : "Sin admin: algunas acciones fallarán. Reabrí como administrador.";
            AdminBadge.Foreground = (System.Windows.Media.Brush)FindResource(App.IsAdministrator ? "AccentBrush" : "AmberBrush");
            Nav.SelectedIndex = 0;
        }

        private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Nav.SelectedItem is ListBoxItem item && item.Tag is string tag)
                Navigate(tag);
        }

        public void NavigateTo(string tag)
        {
            foreach (object obj in Nav.Items)
            {
                if (obj is ListBoxItem li && (li.Tag as string) == tag)
                {
                    li.IsSelected = true;
                    return;
                }
            }
        }

        private void Navigate(string tag)
        {
            if (Host == null) return;
            if (!cache.TryGetValue(tag, out UserControl view))
            {
                view = Create(tag);
                cache[tag] = view;
            }
            Host.Content = view;
        }

        private UserControl Create(string tag)
        {
            switch (tag)
            {
                case "dashboard": return new DashboardView();
                case "apps": return new AppsView();
                case "startup": return new StartupView();
                case "cleanup": return new CleanupView();
                case "services": return new ServicesView();
                case "privacy": return new TweaksView("privacy", "Privacidad", "Tweaks reversibles para reducir telemetría y rastreo. Marcá y aplicá.");
                case "performance": return new TweaksView("performance", "Rendimiento", "Ajustes para una PC más ágil. Reversibles desde acá mismo.");
                case "health": return new HealthView();
                default: return new PlaceholderView(tag, "");
            }
        }
    }
}
