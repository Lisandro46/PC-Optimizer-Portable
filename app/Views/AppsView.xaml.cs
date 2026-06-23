using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace PcOptimizer
{
    public partial class AppsView : UserControl
    {
        private readonly InventoryService inventoryService = new InventoryService();
        private readonly ActionService actionService = new ActionService();
        private readonly ResourceMonitorService resourceMonitor = new ResourceMonitorService();
        private readonly List<AppItem> inventory = new List<AppItem>();
        private readonly DispatcherTimer measureTimer = new DispatcherTimer();
        private ICollectionView view;
        private bool busy;
        private bool measuring;

        public AppsView()
        {
            InitializeComponent();
            RiskFilter.ItemsSource = new[] { "Todos los riesgos", "BAJO", "MEDIO", "ALTO", "CRÍTICO" };
            RiskFilter.SelectedIndex = 0;
            SourceFilter.ItemsSource = new[] { "Todos los orígenes" };
            SourceFilter.SelectedIndex = 0;
            measureTimer.Interval = TimeSpan.FromSeconds(10);
            measureTimer.Tick += delegate { if (!busy && !measuring) Measure(); };
            Loaded += delegate { if (inventory.Count == 0) StartScan(); };
        }

        private async void StartScan()
        {
            measureTimer.Stop();
            SetBusy(true, "Iniciando inventario completo...");
            var progress = new Progress<string>(m => Status.Text = m);
            List<AppItem> result = null;
            try
            {
                result = await Task.Run(() => inventoryService.Scan(s => ((IProgress<string>)progress).Report(s)));
            }
            catch (Exception ex)
            {
                SetBusy(false, "Error durante el escaneo: " + ex.Message);
                return;
            }

            inventory.Clear();
            inventory.AddRange(result);
            BuildView();
            RebuildSourceFilter();

            string message = "Inventario listo: " + inventory.Count + " elementos detectados.";
            if (inventoryService.Errors.Count > 0) message += " " + inventoryService.Errors.Count + " consultas con advertencias.";
            SetBusy(false, message);
            MeasureButton.IsEnabled = true;
            UpdateSelectedCount();
            measureTimer.Start();
            Measure();
        }

        private void BuildView()
        {
            view = CollectionViewSource.GetDefaultView(inventory);
            view.Filter = FilterItem;
            AppGrid.ItemsSource = view;
            view.Refresh();
        }

        private bool FilterItem(object o)
        {
            var i = o as AppItem;
            if (i == null) return false;
            string q = SearchBox.Text == null ? "" : SearchBox.Text.Trim();
            if (q.Length > 0 && (i.Name + " " + i.Publisher + " " + i.Id).IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0)
                return false;
            string src = SourceFilter.SelectedItem as string;
            if (!string.IsNullOrEmpty(src) && src != "Todos los orígenes" && i.Source != src) return false;
            string risk = RiskFilter.SelectedItem as string;
            if (!string.IsNullOrEmpty(risk) && risk != "Todos los riesgos" && i.RiskText != risk) return false;
            return true;
        }

        private void RebuildSourceFilter()
        {
            var sources = new List<string> { "Todos los orígenes" };
            sources.AddRange(inventory.Select(i => i.Source).Distinct().OrderBy(s => s));
            SourceFilter.ItemsSource = sources;
            SourceFilter.SelectedIndex = 0;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (view != null) view.Refresh();
            UpdateSelectedCount();
        }

        private async void Measure()
        {
            if (measuring || inventory.Count == 0 || busy) return;
            measuring = true;
            MeasureButton.IsEnabled = false;
            if (!busy) Status.Text = "Midiendo CPU, RAM y disco durante un segundo...";
            List<AppItem> snapshot = inventory.ToList();
            try
            {
                await Task.Run(() => resourceMonitor.Measure(snapshot, 1000));
            }
            catch (Exception ex)
            {
                measuring = false;
                MeasureButton.IsEnabled = !busy;
                if (!busy) Status.Text = "No se pudo actualizar el consumo: " + ex.Message;
                return;
            }

            foreach (AppItem item in inventory) item.RaiseResourceChanged();
            int running = inventory.Count(i => i.IsRunning);
            measuring = false;
            MeasureButton.IsEnabled = !busy;
            if (!busy) Status.Text = "Consumo actualizado: " + running + " aplicaciones en ejecución. Próxima medición en 10 s.";
        }

        private void Measure_Click(object sender, RoutedEventArgs e) { Measure(); }
        private void Scan_Click(object sender, RoutedEventArgs e) { StartScan(); }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            foreach (AppItem i in inventory) i.Selected = false;
            UpdateSelectedCount();
        }

        private void RowCheck_Click(object sender, RoutedEventArgs e) { UpdateSelectedCount(); }
        private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) { UpdateSelectedCount(); }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AppItem item) ShowInfo(item);
        }

        private void ShowInfo(AppItem item)
        {
            var sb = new StringBuilder();
            sb.AppendLine(item.Description);
            sb.AppendLine();
            sb.AppendLine("RIESGO " + item.RiskText);
            sb.AppendLine(item.RiskReason);
            sb.AppendLine();
            sb.AppendLine("Origen: " + item.Source);
            sb.AppendLine("Editor: " + Empty(item.Publisher));
            sb.AppendLine("Versión: " + Empty(item.Version));
            sb.AppendLine("Alcance: " + item.Scope);
            sb.AppendLine("Estado: " + item.State);
            sb.AppendLine("Ubicación: " + Empty(item.InstallLocation));
            sb.AppendLine("Identificador: " + item.Id);
            sb.AppendLine();
            sb.AppendLine("Puede desinstalar: " + (item.CanUninstall ? "Sí" : "No"));
            sb.AppendLine("Puede desactivar: " + (item.CanDisable ? "Sí" : "No"));
            sb.AppendLine();
            sb.AppendLine("CONSUMO ACTUAL");
            sb.AppendLine("CPU: " + item.CpuText + "   RAM: " + item.MemoryText + "   Disco: " + item.DiskText + "   Procesos: " + item.ProcessText);
            if (!item.ResourceMeasurable) sb.AppendLine("N/D: Windows comparte este componente o no publica una relación confiable con un proceso.");
            if (item.NonRemovable) sb.AppendLine("Windows lo marca como NO REMOVIBLE. El modo experto permitirá intentarlo sin garantías.");
            MessageBox.Show(sb.ToString(), item.Name, MessageBoxButton.OK,
                item.Risk >= RiskLevel.Alto ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void Plan_Click(object sender, RoutedEventArgs e)
        {
            AppGrid.CommitEdit(DataGridEditingUnit.Row, true);
            List<AppItem> selected = inventory.Where(i => i.Selected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Marcá al menos un elemento para preparar el plan.", "Nada seleccionado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<OperationPlanItem> plan = actionService.BuildPlan(selected);
            var dialog = new PlanWindow(plan) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;

            string logFolder;
            try { logFolder = actionService.SaveInventoryAndPlan(inventory, plan); }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo guardar el inventario de seguridad: " + ex.Message + "\n\nNo se ejecutará ningún cambio.", "Protección interrumpida", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ExecutePlan(plan, logFolder, dialog.CreateRestorePoint);
        }

        private async void ExecutePlan(List<OperationPlanItem> plan, string logFolder, bool restorePoint)
        {
            measureTimer.Stop();

            if (restorePoint)
            {
                SetBusy(true, "Creando punto de restauración...");
                string restoreResult = await Task.Run(() => actionService.CreateRestorePoint());
                if (restoreResult.StartsWith("No se pudo", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBoxResult choice = MessageBox.Show(restoreResult + "\n\n¿Continuar igualmente con el plan aprobado?", "Sin punto de restauración", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    if (choice != MessageBoxResult.Yes) { SetBusy(false, restoreResult); measureTimer.Start(); return; }
                }
            }

            SetBusy(true, "Ejecutando plan...");
            var progress = new Progress<string>(m => Status.Text = m);
            List<OperationResult> results;
            try
            {
                results = await Task.Run(() => actionService.Execute(plan, s => ((IProgress<string>)progress).Report(s)));
            }
            catch (Exception ex)
            {
                SetBusy(false, "La ejecución se interrumpió: " + ex.Message);
                return;
            }

            string path = actionService.SaveResults(results, logFolder);
            int ok = results.Count(r => r.Success);
            int failed = results.Count - ok;
            SetBusy(false, "Plan finalizado: " + ok + " correctas, " + failed + " con error u omitidas.");
            MessageBox.Show("Operaciones correctas: " + ok + "\nCon error u omitidas: " + failed + "\n\nRegistro: " + path + "\n\nSe volverá a escanear el equipo.", "Plan finalizado", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            StartScan();
        }

        private void UpdateSelectedCount()
        {
            int count = inventory.Count(i => i.Selected);
            SelectedCount.Text = count + (count == 1 ? " seleccionado" : " seleccionados");
            PlanButton.IsEnabled = count > 0 && !busy;
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            Progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            ScanButton.IsEnabled = !value;
            MeasureButton.IsEnabled = !value && !measuring && inventory.Count > 0;
            AppGrid.IsEnabled = !value;
            SearchBox.IsEnabled = !value;
            SourceFilter.IsEnabled = !value;
            RiskFilter.IsEnabled = !value;
            PlanButton.IsEnabled = !value && inventory.Any(i => i.Selected);
            Status.Text = message;
        }

        private static string Empty(string value) { return string.IsNullOrWhiteSpace(value) ? "No informado" : value; }
    }
}
