using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PcOptimizerPortable
{
    public sealed class MainForm : Form
    {
        private readonly InventoryService inventoryService = new InventoryService();
        private readonly ActionService actionService = new ActionService();
        private readonly List<AppItem> inventory = new List<AppItem>();
        private readonly BindingSource binding = new BindingSource();
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox search = new TextBox();
        private readonly ComboBox sourceFilter = new ComboBox();
        private readonly ComboBox riskFilter = new ComboBox();
        private readonly Label status = new Label();
        private readonly Label selectedCount = new Label();
        private readonly Button scanButton = new Button();
        private readonly Button resourceButton = new Button();
        private readonly Button planButton = new Button();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly ResourceMonitorService resourceMonitor = new ResourceMonitorService();
        private readonly Timer resourceTimer = new Timer();
        private bool resourceBusy;
        private string sortProperty = "Name";
        private bool sortAscending = true;

        public MainForm()
        {
            Text = "PC Optimizer Portable — Modo experto";
            Width = 1280;
            Height = 780;
            MinimumSize = new Size(980, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Shield;
            BackColor = Color.FromArgb(10, 10, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            BuildHeader();
            BuildFilters();
            BuildGrid();
            BuildFooter();

            resourceTimer.Interval = 10000;
            resourceTimer.Tick += delegate { StartResourceMeasurement(); };

            Shown += delegate { StartScan(); };
        }

        private void BuildHeader()
        {
            var title = new Label();
            title.Text = "PC OPTIMIZER";
            title.Font = new Font("Segoe UI Semibold", 22F);
            title.ForeColor = Color.White;
            title.AutoSize = true;
            title.Location = new Point(20, 14);
            Controls.Add(title);

            var mode = new Label();
            mode.Text = "MODO EXPERTO";
            mode.Font = new Font("Segoe UI Semibold", 9F);
            mode.ForeColor = Color.Black;
            mode.BackColor = Color.FromArgb(0, 255, 136);
            mode.Padding = new Padding(8, 4, 8, 4);
            mode.AutoSize = true;
            mode.Location = new Point(225, 24);
            Controls.Add(mode);

            var warning = new Label();
            warning.Text = "Muestra también componentes ocultos y protegidos. Nada se selecciona ni se ejecuta automáticamente.";
            warning.ForeColor = Color.FromArgb(255, 205, 80);
            warning.AutoSize = true;
            warning.Location = new Point(23, 57);
            Controls.Add(warning);

            scanButton.Text = "Volver a escanear";
            scanButton.Width = 145;
            scanButton.Height = 34;
            scanButton.Location = new Point(ClientSize.Width - 165, 20);
            scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            scanButton.FlatStyle = FlatStyle.Flat;
            scanButton.FlatAppearance.BorderColor = Color.FromArgb(0, 212, 255);
            scanButton.ForeColor = Color.White;
            scanButton.Click += delegate { StartScan(); };
            Controls.Add(scanButton);

            resourceButton.Text = "Actualizar consumo";
            resourceButton.Width = 145;
            resourceButton.Height = 34;
            resourceButton.Location = new Point(ClientSize.Width - 320, 20);
            resourceButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            resourceButton.FlatStyle = FlatStyle.Flat;
            resourceButton.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 136);
            resourceButton.ForeColor = Color.White;
            resourceButton.Enabled = false;
            resourceButton.Click += delegate { StartResourceMeasurement(); };
            Controls.Add(resourceButton);
        }

        private void BuildFilters()
        {
            var bar = new Panel();
            bar.Location = new Point(20, 86);
            bar.Size = new Size(ClientSize.Width - 40, 58);
            bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            bar.BackColor = Color.FromArgb(22, 25, 30);
            Controls.Add(bar);

            search.PlaceholderTextCompat("Buscar por nombre, editor o identificador...");
            search.Location = new Point(12, 16);
            search.Width = 350;
            search.BackColor = Color.FromArgb(35, 38, 44);
            search.ForeColor = Color.White;
            search.BorderStyle = BorderStyle.FixedSingle;
            search.TextChanged += delegate { ApplyFilters(); };
            bar.Controls.Add(search);

            sourceFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            sourceFilter.Items.Add("Todos los orígenes");
            sourceFilter.SelectedIndex = 0;
            sourceFilter.Location = new Point(375, 15);
            sourceFilter.Width = 215;
            sourceFilter.SelectedIndexChanged += delegate { ApplyFilters(); };
            bar.Controls.Add(sourceFilter);

            riskFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            riskFilter.Items.AddRange(new object[] { "Todos los riesgos", "BAJO", "MEDIO", "ALTO", "CRÍTICO" });
            riskFilter.SelectedIndex = 0;
            riskFilter.Location = new Point(603, 15);
            riskFilter.Width = 155;
            riskFilter.SelectedIndexChanged += delegate { ApplyFilters(); };
            bar.Controls.Add(riskFilter);

            var clear = new Button();
            clear.Text = "Desmarcar todo";
            clear.Width = 125;
            clear.Height = 30;
            clear.Location = new Point(772, 13);
            clear.FlatStyle = FlatStyle.Flat;
            clear.ForeColor = Color.White;
            clear.Click += delegate
            {
                foreach (AppItem item in inventory) item.Selected = false;
                grid.Refresh();
                UpdateSelectedCount();
            };
            bar.Controls.Add(clear);

            selectedCount.Text = "0 seleccionados";
            selectedCount.AutoSize = true;
            selectedCount.ForeColor = Color.FromArgb(0, 255, 136);
            selectedCount.Location = new Point(920, 19);
            bar.Controls.Add(selectedCount);
        }

        private void BuildGrid()
        {
            grid.Location = new Point(20, 156);
            grid.Size = new Size(ClientSize.Width - 40, ClientSize.Height - 246);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.BackgroundColor = Color.FromArgb(15, 17, 20);
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Color.FromArgb(45, 48, 54);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 33, 38);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
            grid.ColumnHeadersHeight = 38;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(20, 22, 26);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 70, 65);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.RowTemplate.Height = 34;

            var selected = new DataGridViewCheckBoxColumn();
            selected.DataPropertyName = "Selected";
            selected.HeaderText = "";
            selected.Width = 38;
            grid.Columns.Add(selected);

            var name = new DataGridViewTextBoxColumn();
            name.DataPropertyName = "Name";
            name.HeaderText = "Aplicación o componente";
            name.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            name.FillWeight = 32;
            name.ReadOnly = true;
            grid.Columns.Add(name);

            var source = new DataGridViewTextBoxColumn();
            source.DataPropertyName = "Source";
            source.HeaderText = "Origen";
            source.Width = 180;
            source.ReadOnly = true;
            grid.Columns.Add(source);

            var publisher = new DataGridViewTextBoxColumn();
            publisher.DataPropertyName = "Publisher";
            publisher.HeaderText = "Editor";
            publisher.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            publisher.FillWeight = 20;
            publisher.ReadOnly = true;
            grid.Columns.Add(publisher);

            var state = new DataGridViewTextBoxColumn();
            state.DataPropertyName = "State";
            state.HeaderText = "Estado";
            state.Width = 95;
            state.ReadOnly = true;
            grid.Columns.Add(state);

            var risk = new DataGridViewTextBoxColumn();
            risk.DataPropertyName = "RiskText";
            risk.HeaderText = "Riesgo";
            risk.Width = 82;
            risk.ReadOnly = true;
            grid.Columns.Add(risk);

            var cpu = new DataGridViewTextBoxColumn();
            cpu.DataPropertyName = "CpuText";
            cpu.HeaderText = "CPU ahora";
            cpu.Width = 82;
            cpu.ReadOnly = true;
            cpu.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(cpu);

            var memory = new DataGridViewTextBoxColumn();
            memory.DataPropertyName = "MemoryText";
            memory.HeaderText = "RAM";
            memory.Width = 82;
            memory.ReadOnly = true;
            memory.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(memory);

            var disk = new DataGridViewTextBoxColumn();
            disk.DataPropertyName = "DiskText";
            disk.HeaderText = "Disco";
            disk.Width = 92;
            disk.ReadOnly = true;
            disk.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(disk);

            var processes = new DataGridViewTextBoxColumn();
            processes.DataPropertyName = "ProcessText";
            processes.HeaderText = "Procesos";
            processes.Width = 72;
            processes.ReadOnly = true;
            processes.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns.Add(processes);

            var action = new DataGridViewComboBoxColumn();
            action.DataPropertyName = "Action";
            action.HeaderText = "Acción";
            action.Items.AddRange("Desinstalar", "Desactivar");
            action.Width = 115;
            action.FlatStyle = FlatStyle.Flat;
            grid.Columns.Add(action);

            var help = new DataGridViewButtonColumn();
            help.HeaderText = "Info";
            help.Text = "?";
            help.UseColumnTextForButtonValue = true;
            help.Width = 52;
            help.FlatStyle = FlatStyle.Flat;
            grid.Columns.Add(help);

            grid.DataSource = binding;
            grid.CellContentClick += GridCellContentClick;
            grid.ColumnHeaderMouseClick += GridColumnHeaderMouseClick;
            grid.CellFormatting += GridCellFormatting;
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += delegate { UpdateSelectedCount(); };
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            foreach (DataGridViewColumn column in grid.Columns)
                column.SortMode = column is DataGridViewButtonColumn ? DataGridViewColumnSortMode.NotSortable : DataGridViewColumnSortMode.Programmatic;
            Controls.Add(grid);
        }

        private void BuildFooter()
        {
            progress.Location = new Point(20, ClientSize.Height - 78);
            progress.Size = new Size(260, 10);
            progress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            progress.Style = ProgressBarStyle.Marquee;
            progress.Visible = false;
            Controls.Add(progress);

            status.Text = "Esperando escaneo...";
            status.AutoEllipsis = true;
            status.Location = new Point(20, ClientSize.Height - 59);
            status.Size = new Size(ClientSize.Width - 265, 35);
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            status.ForeColor = Color.FromArgb(190, 195, 202);
            Controls.Add(status);

            planButton.Text = "REVISAR PLAN";
            planButton.Font = new Font("Segoe UI Semibold", 10F);
            planButton.BackColor = Color.FromArgb(0, 255, 136);
            planButton.ForeColor = Color.Black;
            planButton.FlatStyle = FlatStyle.Flat;
            planButton.FlatAppearance.BorderSize = 0;
            planButton.Size = new Size(200, 44);
            planButton.Location = new Point(ClientSize.Width - 220, ClientSize.Height - 72);
            planButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            planButton.Enabled = false;
            planButton.Click += delegate { ReviewPlan(); };
            Controls.Add(planButton);
        }

        private void StartScan()
        {
            resourceTimer.Stop();
            SetBusy(true, "Iniciando inventario completo...");
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = inventoryService.Scan(delegate(string message)
                {
                    BeginInvoke((MethodInvoker)delegate { status.Text = message; });
                });
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    SetBusy(false, "Error durante el escaneo: " + e.Error.Message);
                    return;
                }
                inventory.Clear();
                inventory.AddRange((List<AppItem>)e.Result);
                RebuildSourceFilter();
                ApplyFilters();
                string message = "Inventario listo: " + inventory.Count + " elementos detectados.";
                if (inventoryService.Errors.Count > 0) message += " " + inventoryService.Errors.Count + " consultas devolvieron advertencias.";
                SetBusy(false, message);
                resourceButton.Enabled = true;
                resourceTimer.Start();
                StartResourceMeasurement();
            };
            worker.RunWorkerAsync();
        }

        private void StartResourceMeasurement()
        {
            if (resourceBusy || inventory.Count == 0 || progress.Visible) return;
            resourceBusy = true;
            resourceButton.Enabled = false;
            status.Text = "Midiendo CPU, RAM y disco durante un segundo...";
            List<AppItem> snapshot = inventory.ToList();
            var worker = new BackgroundWorker();
            worker.DoWork += delegate { resourceMonitor.Measure(snapshot, 1000); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                resourceBusy = false;
                resourceButton.Enabled = !progress.Visible;
                if (e.Error != null)
                {
                    status.Text = "No se pudo actualizar el consumo: " + e.Error.Message;
                    return;
                }
                if (IsNumericSort(sortProperty)) ApplyFilters();
                else grid.Refresh();
                int running = inventory.Count(i => i.IsRunning);
                status.Text = "Consumo actualizado: " + running + " aplicaciones detectadas en ejecución. Próxima medición automática en 10 segundos.";
            };
            worker.RunWorkerAsync();
        }

        private void ApplyFilters()
        {
            if (inventory == null) return;
            string q = search.Text.Trim();
            string src = sourceFilter.SelectedItem == null ? "Todos los orígenes" : sourceFilter.SelectedItem.ToString();
            string risk = riskFilter.SelectedItem == null ? "Todos los riesgos" : riskFilter.SelectedItem.ToString();
            IEnumerable<AppItem> rows = inventory;
            if (!String.IsNullOrWhiteSpace(q))
            {
                rows = rows.Where(i => (i.Name + " " + i.Publisher + " " + i.Id).IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }
            if (src != "Todos los orígenes") rows = rows.Where(i => i.Source == src);
            if (risk != "Todos los riesgos") rows = rows.Where(i => i.RiskText == risk);
            List<AppItem> sorted = rows.ToList();
            sorted.Sort(new AppItemComparer(sortProperty, sortAscending));
            binding.DataSource = new BindingList<AppItem>(sorted);
            UpdateSortGlyph();
            UpdateSelectedCount();
        }

        private void GridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            DataGridViewColumn column = grid.Columns[e.ColumnIndex];
            if (column.SortMode == DataGridViewColumnSortMode.NotSortable || String.IsNullOrWhiteSpace(column.DataPropertyName)) return;

            if (sortProperty == column.DataPropertyName)
                sortAscending = !sortAscending;
            else
            {
                sortProperty = column.DataPropertyName;
                sortAscending = !IsNumericSort(sortProperty);
            }
            ApplyFilters();
        }

        private void UpdateSortGlyph()
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.SortMode == DataGridViewColumnSortMode.NotSortable) continue;
                column.HeaderCell.SortGlyphDirection = column.DataPropertyName == sortProperty
                    ? (sortAscending ? SortOrder.Ascending : SortOrder.Descending)
                    : SortOrder.None;
            }
        }

        private static bool IsNumericSort(string property)
        {
            return property == "RiskText" || property == "CpuText" || property == "MemoryText" || property == "DiskText" || property == "ProcessText";
        }

        private void RebuildSourceFilter()
        {
            string current = sourceFilter.SelectedItem == null ? "Todos los orígenes" : sourceFilter.SelectedItem.ToString();
            sourceFilter.Items.Clear();
            sourceFilter.Items.Add("Todos los orígenes");
            foreach (string source in inventory.Select(i => i.Source).Distinct().OrderBy(s => s)) sourceFilter.Items.Add(source);
            int index = sourceFilter.Items.IndexOf(current);
            sourceFilter.SelectedIndex = index >= 0 ? index : 0;
        }

        private void GridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == grid.Columns.Count - 1)
            {
                var item = grid.Rows[e.RowIndex].DataBoundItem as AppItem;
                if (item != null) ShowInfo(item);
            }
        }

        private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var item = grid.Rows[e.RowIndex].DataBoundItem as AppItem;
            if (item == null) return;
            if (grid.Columns[e.ColumnIndex].DataPropertyName == "RiskText")
            {
                e.CellStyle.Font = new Font(grid.Font, FontStyle.Bold);
                if (item.Risk == RiskLevel.Critico) e.CellStyle.ForeColor = Color.FromArgb(255, 75, 85);
                else if (item.Risk == RiskLevel.Alto) e.CellStyle.ForeColor = Color.FromArgb(255, 145, 70);
                else if (item.Risk == RiskLevel.Medio) e.CellStyle.ForeColor = Color.FromArgb(255, 215, 90);
                else e.CellStyle.ForeColor = Color.FromArgb(0, 255, 136);
            }
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
            sb.AppendLine("Puede desinstalar: " + YesNo(item.CanUninstall));
            sb.AppendLine("Puede desactivar: " + YesNo(item.CanDisable));
            sb.AppendLine();
            sb.AppendLine("CONSUMO ACTUAL");
            sb.AppendLine("CPU: " + item.CpuText);
            sb.AppendLine("Memoria RAM: " + item.MemoryText);
            sb.AppendLine("Actividad de disco: " + item.DiskText);
            sb.AppendLine("Procesos relacionados: " + item.ProcessText);
            if (!item.ResourceMeasurable) sb.AppendLine("N/D significa que Windows comparte este componente o no publica una relación confiable con un proceso.");
            if (item.NonRemovable) sb.AppendLine("Windows lo marca como NO REMOVIBLE. El modo experto permitirá intentarlo, sin garantizar que Windows lo acepte.");
            MessageBox.Show(sb.ToString(), item.Name, MessageBoxButtons.OK, item.Risk >= RiskLevel.Alto ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void ReviewPlan()
        {
            grid.EndEdit();
            binding.EndEdit();
            List<AppItem> selected = inventory.Where(i => i.Selected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Marcá al menos un elemento para preparar el plan.", "Nada seleccionado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<OperationPlanItem> plan = actionService.BuildPlan(selected);
            using (var dialog = new PlanForm(plan))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string logFolder;
                try { logFolder = actionService.SaveInventoryAndPlan(inventory, plan); }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo guardar el inventario de seguridad: " + ex.Message + "\n\nNo se ejecutará ningún cambio.", "Protección interrumpida", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (dialog.CreateRestorePoint)
                {
                    SetBusy(true, "Creando punto de restauración...");
                    string restoreResult = actionService.CreateRestorePoint();
                    SetBusy(false, restoreResult);
                    if (restoreResult.StartsWith("No se pudo", StringComparison.OrdinalIgnoreCase))
                    {
                        DialogResult choice = MessageBox.Show(restoreResult + "\n\n¿Querés continuar igualmente con el plan ya aprobado?", "Sin punto de restauración", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                        if (choice != DialogResult.Yes) return;
                    }
                }

                ExecutePlan(plan, logFolder);
            }
        }

        private void ExecutePlan(List<OperationPlanItem> plan, string logFolder)
        {
            resourceTimer.Stop();
            SetBusy(true, "Ejecutando plan...");
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = actionService.Execute(plan, delegate(string message)
                {
                    BeginInvoke((MethodInvoker)delegate { status.Text = message; });
                });
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    SetBusy(false, "La ejecución se interrumpió: " + e.Error.Message);
                    return;
                }
                var results = (List<OperationResult>)e.Result;
                string path = actionService.SaveResults(results, logFolder);
                int ok = results.Count(r => r.Success);
                int failed = results.Count - ok;
                SetBusy(false, "Plan finalizado: " + ok + " correctas, " + failed + " con error u omitidas.");
                MessageBox.Show("Operaciones correctas: " + ok + "\nCon error u omitidas: " + failed + "\n\nRegistro: " + path + "\n\nSe volverá a escanear el equipo.", "Plan finalizado", MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                StartScan();
            };
            worker.RunWorkerAsync();
        }

        private void UpdateSelectedCount()
        {
            int count = inventory.Count(i => i.Selected);
            selectedCount.Text = count + (count == 1 ? " seleccionado" : " seleccionados");
            planButton.Enabled = count > 0 && !progress.Visible;
        }

        private void SetBusy(bool busy, string message)
        {
            progress.Visible = busy;
            scanButton.Enabled = !busy;
            resourceButton.Enabled = !busy && !resourceBusy && inventory.Count > 0;
            grid.Enabled = !busy;
            search.Enabled = !busy;
            sourceFilter.Enabled = !busy;
            riskFilter.Enabled = !busy;
            planButton.Enabled = !busy && inventory.Any(i => i.Selected);
            status.Text = message;
        }

        private static string Empty(string value) { return String.IsNullOrWhiteSpace(value) ? "No informado" : value; }
        private static string YesNo(bool value) { return value ? "Sí" : "No"; }

        private sealed class AppItemComparer : IComparer<AppItem>
        {
            private readonly string property;
            private readonly bool ascending;

            public AppItemComparer(string property, bool ascending)
            {
                this.property = property;
                this.ascending = ascending;
            }

            public int Compare(AppItem x, AppItem y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return 1;
                if (y == null) return -1;

                if (IsResourceProperty(property) && x.ResourceMeasurable != y.ResourceMeasurable)
                    return x.ResourceMeasurable ? -1 : 1;

                int result;
                if (property == "Selected") result = x.Selected.CompareTo(y.Selected);
                else if (property == "RiskText") result = x.Risk.CompareTo(y.Risk);
                else if (property == "CpuText") result = x.CpuPercent.CompareTo(y.CpuPercent);
                else if (property == "MemoryText") result = x.MemoryMb.CompareTo(y.MemoryMb);
                else if (property == "DiskText") result = x.DiskMbPerSecond.CompareTo(y.DiskMbPerSecond);
                else if (property == "ProcessText") result = x.ProcessCount.CompareTo(y.ProcessCount);
                else if (property == "Source") result = Text(x.Source, y.Source);
                else if (property == "Publisher") result = Text(x.Publisher, y.Publisher);
                else if (property == "State") result = Text(x.State, y.State);
                else if (property == "Action") result = Text(x.Action, y.Action);
                else result = Text(x.Name, y.Name);

                if (result == 0 && property != "Name") result = Text(x.Name, y.Name);
                return ascending ? result : -result;
            }

            private static bool IsResourceProperty(string value)
            {
                return value == "CpuText" || value == "MemoryText" || value == "DiskText" || value == "ProcessText";
            }

            private static int Text(string a, string b)
            {
                return StringComparer.CurrentCultureIgnoreCase.Compare(a ?? "", b ?? "");
            }
        }
    }

    internal static class TextBoxExtensions
    {
        // .NET Framework no ofrece PlaceholderText; el texto de ayuda se mantiene como tooltip accesible.
        public static void PlaceholderTextCompat(this TextBox box, string text)
        {
            var tip = new ToolTip();
            tip.SetToolTip(box, text);
        }
    }
}
