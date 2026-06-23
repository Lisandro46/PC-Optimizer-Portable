using System;
using System.IO;
using System.Text;

namespace PcOptimizer
{
    public static class LogService
    {
        public static string BaseDir()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "PCOptimizer-Logs");
            try { Directory.CreateDirectory(dir); return dir; }
            catch
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCOptimizer-Logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string Save(string prefix, string content)
        {
            string path = Path.Combine(BaseDir(), prefix + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(path, content, new UTF8Encoding(true));
            return path;
        }

        public static string HumanSize(long bytes)
        {
            if (bytes <= 0) return "0 MB";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
            return size.ToString(unit >= 3 ? "0.00" : unit == 2 ? "0.0" : "0") + " " + units[unit];
        }
    }
}
