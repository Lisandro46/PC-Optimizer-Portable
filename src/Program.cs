using System;
using System.Security.Principal;
using System.Windows.Forms;

namespace PcOptimizerPortable
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdministrator())
            {
                MessageBox.Show("La aplicación necesita permisos de administrador para ver componentes de todos los usuarios y ejecutar cambios. Windows debería solicitar esos permisos al abrirla.", "Permisos necesarios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Application.Run(new MainForm());
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
