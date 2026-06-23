using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PcOptimizer
{
    public partial class DashboardView : UserControl
    {
        private readonly SystemInfoService info = new SystemInfoService();

        public DashboardView()
        {
            InitializeComponent();
            Loaded += delegate { Refresh(); };
        }

        private async void Refresh()
        {
            RescanButton.IsEnabled = false;
            SubText.Text = "Analizando el equipo...";
            SystemSnapshot s = await Task.Run(() => info.Capture());

            RamValue.Text = s.RamTotalGb > 0
                ? s.RamUsedGb.ToString("0.0") + " / " + s.RamTotalGb.ToString("0") + " GB"
                : "N/D";
            DiskValue.Text = s.DiskTotalGb > 0 ? s.DiskFreeGb.ToString("0") + " GB" : "N/D";
            StartupValue.Text = s.StartupCount.ToString();
            UptimeValue.Text = FormatUptime(s.Uptime);

            ScoreNumber.Text = s.HealthScore.ToString();
            Color c = s.HealthScore >= 85 ? Color.FromRgb(0, 255, 136)
                : s.HealthScore >= 70 ? Color.FromRgb(255, 215, 90)
                : s.HealthScore >= 50 ? Color.FromRgb(255, 138, 75)
                : Color.FromRgb(255, 75, 85);
            var brush = new SolidColorBrush(c);
            ScoreArc.Stroke = brush;
            ScoreNumber.Foreground = brush;
            ScoreArc.Data = BuildArc(s.HealthScore);

            SubText.Text = BuildSummary(s);
            RescanButton.IsEnabled = true;
        }

        private static string BuildSummary(SystemSnapshot s)
        {
            string ram = s.RamLoadPercent >= 80 ? "RAM exigida (" + s.RamLoadPercent.ToString("0") + "%). " : "";
            string disk = "";
            if (s.DiskTotalGb > 0)
            {
                double freePct = s.DiskFreeGb / s.DiskTotalGb * 100.0;
                if (freePct < 15) disk = "Poco espacio libre (" + freePct.ToString("0") + "%). ";
            }
            string start = s.StartupCount >= 8 ? s.StartupCount + " apps arrancan con Windows. " : "";
            string head = s.HealthLabel + ". ";
            string tail = (ram + disk + start).Length == 0
                ? "No detectamos problemas evidentes; igual podés liberar espacio y revisar el arranque."
                : (ram + disk + start).Trim();
            return head + tail;
        }

        private static string FormatUptime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return ((int)t.TotalDays) + "d " + t.Hours + "h";
            if (t.TotalHours >= 1) return t.Hours + "h " + t.Minutes + "m";
            return Math.Max(1, t.Minutes) + "m";
        }

        private static Geometry BuildArc(int score)
        {
            double pct = Math.Max(0, Math.Min(100, score)) / 100.0;
            if (pct <= 0) return Geometry.Empty;
            double sweep = pct * 360.0;
            if (sweep >= 360.0) sweep = 359.9;

            const double cx = 52, cy = 52, r = 44;
            double a = sweep * Math.PI / 180.0;
            var start = new Point(cx, cy - r);
            var end = new Point(cx + r * Math.Sin(a), cy - r * Math.Cos(a));

            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, sweep > 180.0, SweepDirection.Clockwise, true));
            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            return geo;
        }

        private void Rescan_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Quick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string tag && Application.Current.MainWindow is MainWindow mw)
                mw.NavigateTo(tag);
        }
    }
}
