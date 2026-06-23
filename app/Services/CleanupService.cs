using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PcOptimizer
{
    public sealed class CleanupTarget : INotifyPropertyChanged
    {
        public enum CleanKind { FolderContents, RecycleBin, Glob }

        private bool selected;
        public bool Selected { get { return selected; } set { selected = value; Notify(); } }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Risk { get; set; }
        public CleanKind Kind { get; set; }
        public string[] Paths { get; set; }
        public string GlobDir { get; set; }
        public string GlobPattern { get; set; }

        private long sizeBytes;
        public long SizeBytes { get { return sizeBytes; } set { sizeBytes = value; Notify(); Notify(nameof(SizeText)); } }
        public string SizeText { get { return measured ? LogService.HumanSize(SizeBytes) : "..."; } }

        private bool measured;
        public bool Measured { get { return measured; } set { measured = value; Notify(nameof(SizeText)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string name = null)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class CleanupResult
    {
        public long FreedBytes;
        public List<string> Messages = new List<string>();
    }

    public sealed class CleanupService
    {
        public List<CleanupTarget> BuildTargets()
        {
            string temp = Path.GetTempPath();
            string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            string softwareDist = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
            string prefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string crashDumps = Path.Combine(local, "CrashDumps");
            string werQueue = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportQueue");
            string werArchive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportArchive");
            string explorerCache = Path.Combine(local, "Microsoft", "Windows", "Explorer");

            return new List<CleanupTarget>
            {
                new CleanupTarget { Name = "Archivos temporales del usuario", Description = "Carpeta TEMP de tu cuenta. Lo que esté en uso se omite.", Risk = "BAJO", Kind = CleanupTarget.CleanKind.FolderContents, Paths = new[] { temp }, Selected = true },
                new CleanupTarget { Name = "Archivos temporales de Windows", Description = "Carpeta Temp del sistema (C:\\Windows\\Temp).", Risk = "BAJO", Kind = CleanupTarget.CleanKind.FolderContents, Paths = new[] { winTemp }, Selected = true },
                new CleanupTarget { Name = "Caché de Windows Update", Description = "Descargas de actualizaciones ya aplicadas. Windows puede re-descargar si hace falta.", Risk = "MEDIO", Kind = CleanupTarget.CleanKind.FolderContents, Paths = new[] { softwareDist } },
                new CleanupTarget { Name = "Papelera de reciclaje", Description = "Vacía la papelera de todas las unidades. Borrado definitivo.", Risk = "MEDIO", Kind = CleanupTarget.CleanKind.RecycleBin },
                new CleanupTarget { Name = "Caché de miniaturas e iconos", Description = "Se regenera sola. Útil si ves miniaturas rotas.", Risk = "BAJO", Kind = CleanupTarget.CleanKind.Glob, GlobDir = explorerCache, GlobPattern = "thumbcache_*.db", Selected = true },
                new CleanupTarget { Name = "Volcados de error y reportes (WER)", Description = "Crash dumps y reportes de errores de Windows.", Risk = "BAJO", Kind = CleanupTarget.CleanKind.FolderContents, Paths = new[] { crashDumps, werQueue, werArchive }, Selected = true },
                new CleanupTarget { Name = "Prefetch", Description = "Datos de prearranque. Se regeneran; los primeros arranques pueden ser algo más lentos.", Risk = "MEDIO", Kind = CleanupTarget.CleanKind.FolderContents, Paths = new[] { prefetch } }
            };
        }

        public void Measure(IEnumerable<CleanupTarget> targets, Action<string> progress)
        {
            foreach (CleanupTarget t in targets)
            {
                progress("Calculando: " + t.Name);
                long size = 0;
                try
                {
                    switch (t.Kind)
                    {
                        case CleanupTarget.CleanKind.FolderContents:
                            foreach (string p in t.Paths) size += FolderSize(p);
                            break;
                        case CleanupTarget.CleanKind.RecycleBin:
                            size = RecycleBinSize();
                            break;
                        case CleanupTarget.CleanKind.Glob:
                            size = GlobSize(t.GlobDir, t.GlobPattern);
                            break;
                    }
                }
                catch { }
                t.SizeBytes = size;
                t.Measured = true;
            }
        }

        public CleanupResult Clean(IEnumerable<CleanupTarget> targets, Action<string> progress)
        {
            var result = new CleanupResult();
            foreach (CleanupTarget t in targets)
            {
                progress("Limpiando: " + t.Name);
                long freed = 0;
                try
                {
                    switch (t.Kind)
                    {
                        case CleanupTarget.CleanKind.FolderContents:
                            foreach (string p in t.Paths) freed += DeleteFolderContents(p);
                            break;
                        case CleanupTarget.CleanKind.RecycleBin:
                            long before = RecycleBinSize();
                            SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
                            freed += before;
                            break;
                        case CleanupTarget.CleanKind.Glob:
                            freed += DeleteGlob(t.GlobDir, t.GlobPattern);
                            break;
                    }
                    result.Messages.Add("OK | " + t.Name + " | liberado " + LogService.HumanSize(freed));
                }
                catch (Exception ex)
                {
                    result.Messages.Add("PARCIAL | " + t.Name + " | " + ex.Message);
                }
                result.FreedBytes += freed;
            }
            return result;
        }

        private static long FolderSize(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            long total = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        private static long GlobSize(string dir, string pattern)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
            long total = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        private static long DeleteFolderContents(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            long freed = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { long len = new FileInfo(file).Length; File.Delete(file); freed += len; } catch { }
                }
            }
            catch { }
            try
            {
                foreach (string dir in Directory.EnumerateDirectories(path))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
            catch { }
            return freed;
        }

        private static long DeleteGlob(string dir, string pattern)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
            long freed = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                {
                    try { long len = new FileInfo(file).Length; File.Delete(file); freed += len; } catch { }
                }
            }
            catch { }
            return freed;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private static long RecycleBinSize()
        {
            var info = new SHQUERYRBINFO();
            info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
            try { if (SHQueryRecycleBin(null, ref info) == 0) return info.i64Size; }
            catch { }
            return 0;
        }
    }
}
