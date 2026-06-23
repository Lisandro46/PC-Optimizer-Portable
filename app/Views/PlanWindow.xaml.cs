using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace PcOptimizer
{
    public partial class PlanWindow : Window
    {
        private readonly List<OperationPlanItem> plan;
        public bool CreateRestorePoint { get { return Restore.IsChecked == true; } }

        public PlanWindow(List<OperationPlanItem> plan)
        {
            InitializeComponent();
            this.plan = plan;

            int unsupported = plan.Count(p => !p.Supported);
            int critical = plan.Count(p => p.Item.Risk == RiskLevel.Critico);
            Summary.Text = plan.Count + " operaciones · " + critical + " críticas · " + unsupported + " no compatibles (se omitirán)";
            Details.Text = BuildText(plan);
        }

        private static string BuildText(IEnumerable<OperationPlanItem> plan)
        {
            var sb = new StringBuilder();
            int index = 1;
            foreach (OperationPlanItem op in plan)
            {
                sb.AppendLine(index + ". [" + op.Item.RiskText + "] " + op.Action.ToUpperInvariant() + " — " + op.Item.Name);
                sb.AppendLine("   Origen: " + op.Item.Source + " | Alcance: " + op.Item.Scope);
                sb.AppendLine("   Método: " + op.Method);
                sb.AppendLine("   Acción exacta: " + op.ExactCommand);
                sb.AppendLine("   Riesgo: " + op.Warning);
                if (!op.Supported) sb.AppendLine("   RESULTADO: SE OMITIRÁ");
                sb.AppendLine();
                index++;
            }
            return sb.ToString();
        }

        private void Confirm_Changed(object sender, RoutedEventArgs e)
        {
            ExecuteButton.IsEnabled = Confirm.Text.Trim().Equals("EJECUTAR", StringComparison.OrdinalIgnoreCase)
                && plan.Any(p => p.Supported);
        }

        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
