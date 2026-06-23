using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PcOptimizer
{
    public partial class HealthView : UserControl
    {
        private readonly HealthService service = new HealthService();
        private bool busy;

        public HealthView()
        {
            InitializeComponent();
            Loaded += delegate { Analyze(); };
        }

        private async void Analyze()
        {
            SetBusy(true);
            SubText.Text = "Analizando el equipo...";
            var progress = new Progress<string>(m => SubText.Text = m);
            HealthReport report = await Task.Run(() => service.Run(s => ((IProgress<string>)progress).Report(s)));

            DiagList.ItemsSource = report.Items;
            int score = report.Snapshot.HealthScore;
            ScoreNumber.Text = score.ToString();
            Color c = score >= 85 ? Color.FromRgb(0, 255, 136)
                : score >= 70 ? Color.FromRgb(255, 215, 90)
                : score >= 50 ? Color.FromRgb(255, 138, 75)
                : Color.FromRgb(255, 75, 85);
            var brush = new SolidColorBrush(c);
            ScoreArc.Stroke = brush;
            ScoreNumber.Foreground = brush;
            ScoreArc.Data = BuildArc(score);

            int warn = report.Items.Count(i => i.Status == "warn");
            int bad = report.Items.Count(i => i.Status == "bad");
            SubText.Text = report.Snapshot.HealthLabel + ". " +
                (bad > 0 ? bad + " alertas y " : "") +
                (warn > 0 ? warn + " avisos para revisar." : "Sin problemas serios detectados.");
            SetBusy(false);
        }

        private static Geometry BuildArc(int score)
        {
            double pct = Math.Max(0, Math.Min(100, score)) / 100.0;
            if (pct <= 0) return Geometry.Empty;
            double sweep = pct * 360.0;
            if (sweep >= 360.0) sweep = 359.9;
            const double cx = 50, cy = 50, r = 42;
            double a = sweep * Math.PI / 180.0;
            var start = new Point(cx, cy - r);
            var end = new Point(cx + r * Math.Sin(a), cy - r * Math.Cos(a));
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, sweep > 180.0, SweepDirection.Clockwise, true));
            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            return geo;
        }

        private void Rescan_Click(object sender, RoutedEventArgs e) { if (!busy) Analyze(); }

        private async void Dism_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            Output.Text = "Ejecutando DISM /CheckHealth...";
            var progress = new Progress<string>(m => Output.Text = m);
            string result = await Task.Run(() => service.RunDism(s => ((IProgress<string>)progress).Report(s)));
            Output.Text = result;
            LogService.Save("dism", result);
            SetBusy(false);
        }

        private async void Sfc_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("SFC /scannow puede tardar varios minutos y usar disco y CPU. ¿Ejecutar ahora?", "Reparar archivos de sistema", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            SetBusy(true);
            Output.Text = "Ejecutando SFC /scannow... (esto puede tardar varios minutos)";
            var progress = new Progress<string>(m => Output.Text = m);
            string result = await Task.Run(() => service.RunSfc(s => ((IProgress<string>)progress).Report(s)));
            Output.Text = result;
            LogService.Save("sfc", result);
            SetBusy(false);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            Progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            RescanButton.IsEnabled = !value;
            DismButton.IsEnabled = !value;
            SfcButton.IsEnabled = !value;
        }
    }
}
