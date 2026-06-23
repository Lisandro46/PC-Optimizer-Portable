using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PcOptimizer
{
    public sealed class SystemSnapshot
    {
        public double RamUsedGb;
        public double RamTotalGb;
        public double RamLoadPercent;
        public double DiskFreeGb;
        public double DiskTotalGb;
        public double DiskUsedPercent;
        public int StartupCount;
        public TimeSpan Uptime;
        public int HealthScore;
        public string HealthLabel;
    }

    public sealed class SystemInfoService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MemoryStatusEx() { dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx)); }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

        public SystemSnapshot Capture()
        {
            var s = new SystemSnapshot();

            try
            {
                var mem = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(mem))
                {
                    s.RamTotalGb = mem.ullTotalPhys / 1073741824.0;
                    s.RamUsedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / 1073741824.0;
                    s.RamLoadPercent = mem.dwMemoryLoad;
                }
            }
            catch { }

            try
            {
                string sysDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                var drive = new DriveInfo(sysDrive);
                if (drive.IsReady)
                {
                    s.DiskTotalGb = drive.TotalSize / 1073741824.0;
                    s.DiskFreeGb = drive.AvailableFreeSpace / 1073741824.0;
                    s.DiskUsedPercent = s.DiskTotalGb > 0 ? (1.0 - s.DiskFreeGb / s.DiskTotalGb) * 100.0 : 0;
                }
            }
            catch { }

            try { s.StartupCount = CountStartup(); } catch { }
            try { s.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64); } catch { }

            ComputeScore(s);
            return s;
        }

        private static int CountStartup()
        {
            int count = 0;
            count += CountRunKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            count += CountRunKey(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            count += CountRunKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run");

            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(startupFolder))
                    foreach (string f in Directory.GetFiles(startupFolder))
                        if (!f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)) count++;
            }
            catch { }
            return count;
        }

        private static int CountRunKey(RegistryKey hive, string path)
        {
            try
            {
                using (RegistryKey key = hive.OpenSubKey(path))
                {
                    if (key == null) return 0;
                    int n = 0;
                    foreach (string name in key.GetValueNames())
                        if (!String.IsNullOrEmpty(name)) n++;
                    return n;
                }
            }
            catch { return 0; }
        }

        private static void ComputeScore(SystemSnapshot s)
        {
            int score = 100;
            if (s.RamLoadPercent >= 90) score -= 20;
            else if (s.RamLoadPercent >= 80) score -= 12;
            else if (s.RamLoadPercent >= 70) score -= 6;

            if (s.DiskTotalGb > 0)
            {
                double freePct = s.DiskFreeGb / s.DiskTotalGb * 100.0;
                if (freePct < 8) score -= 22;
                else if (freePct < 15) score -= 12;
                else if (freePct < 25) score -= 5;
            }

            if (s.StartupCount >= 18) score -= 14;
            else if (s.StartupCount >= 12) score -= 8;
            else if (s.StartupCount >= 8) score -= 4;

            if (score < 0) score = 0;
            if (score > 100) score = 100;
            s.HealthScore = score;
            s.HealthLabel = score >= 85 ? "Muy bien" : score >= 70 ? "Bien, mejorable" : score >= 50 ? "Mejorable" : "Necesita atención";
        }
    }
}
