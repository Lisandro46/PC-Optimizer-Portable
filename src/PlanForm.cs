using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PcOptimizerPortable
{
    public sealed class PlanForm : Form
    {
        private readonly TextBox confirmation;
        private readonly Button execute;
        public bool CreateRestorePoint { get { return restore.Checked; } }
        private readonly CheckBox restore;

        public PlanForm(List<OperationPlanItem> plan)
        {
            Text = "Plan antes de ejecutar";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(10, 10, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            var title = new Label();
            title.Text = "Revisá el plan completo";
            title.Font = new Font("Segoe UI Semibold", 18F);
            title.ForeColor = Color.FromArgb(0, 255, 136);
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            var summary = new Label();
            int unsupported = plan.Count(p => !p.Supported);
            int critical = plan.Count(p => p.Item.Risk == RiskLevel.Critico);
            summary.Text = plan.Count + " operaciones · " + critical + " críticas · " + unsupported + " no compatibles (se omitirán)";
            summary.AutoSize = true;
            summary.Location = new Point(22, 58);
            Controls.Add(summary);

            var details = new RichTextBox();
            details.ReadOnly = true;
            details.BackColor = Color.FromArgb(22, 25, 30);
            details.ForeColor = Color.White;
            details.BorderStyle = BorderStyle.FixedSingle;
            details.Font = new Font("Consolas", 9.5F);
            details.Location = new Point(22, 88);
            details.Size = new Size(840, 440);
            details.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            details.Text = BuildText(plan);
            Controls.Add(details);

            restore = new CheckBox();
            restore.Text = "Intentar crear un punto de restauración antes de cambiar nada";
            restore.Checked = true;
            restore.AutoSize = true;
            restore.Location = new Point(22, 542);
            restore.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(restore);

            var prompt = new Label();
            prompt.Text = "Para habilitar la ejecución escribí: EJECUTAR";
            prompt.AutoSize = true;
            prompt.Location = new Point(22, 579);
            prompt.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(prompt);

            confirmation = new TextBox();
            confirmation.Location = new Point(335, 575);
            confirmation.Width = 190;
            confirmation.BackColor = Color.FromArgb(35, 38, 44);
            confirmation.ForeColor = Color.White;
            confirmation.BorderStyle = BorderStyle.FixedSingle;
            confirmation.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            confirmation.TextChanged += delegate { execute.Enabled = confirmation.Text.Trim().Equals("EJECUTAR", StringComparison.OrdinalIgnoreCase) && plan.Any(p => p.Supported); };
            Controls.Add(confirmation);

            var cancel = new Button();
            cancel.Text = "Cancelar";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Size = new Size(120, 38);
            cancel.Location = new Point(610, 612);
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Controls.Add(cancel);

            execute = new Button();
            execute.Text = "Ejecutar plan";
            execute.DialogResult = DialogResult.OK;
            execute.Enabled = false;
            execute.Size = new Size(132, 38);
            execute.Location = new Point(735, 612);
            execute.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            execute.BackColor = Color.FromArgb(0, 255, 136);
            execute.ForeColor = Color.Black;
            execute.FlatStyle = FlatStyle.Flat;
            Controls.Add(execute);

            AcceptButton = execute;
            CancelButton = cancel;
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
    }
}
