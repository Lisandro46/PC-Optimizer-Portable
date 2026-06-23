using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PcOptimizer
{
    public sealed class StartupItem : INotifyPropertyChanged
    {
        public enum Loc { HkcuRun, HklmRun, HklmRun32, UserFolder, CommonFolder }

        public string Name { get; set; }
        public string Command { get; set; }
        public string LocationText { get; set; }
        public Loc Location { get; set; }
        public string Risk { get; set; }
        public string RiskReason { get; set; }

        private bool enabled;
        public bool Enabled { get { return enabled; } set { enabled = value; Notify(); Notify(nameof(StateText)); } }
        public string StateText { get { return Enabled ? "Habilitado" : "Deshabilitado"; } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string n = null)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(n));
        }
    }

    public sealed class StartupService
    {
        private const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        public List<StartupItem> Scan()
        {
            var list = new List<StartupItem>();
            ReadRun(list, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupItem.Loc.HkcuRun, "Usuario · Registro Run");
            ReadRun(list, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupItem.Loc.HklmRun, "Sistema · Registro Run");
            ReadRun(list, Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", StartupItem.Loc.HklmRun32, "Sistema · Registro Run (32 bits)");
            ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupItem.Loc.UserFolder, "Usuario · Carpeta Inicio");
            ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupItem.Loc.CommonFolder, "Sistema · Carpeta Inicio");
            return list;
        }

        private void ReadRun(List<StartupItem> list, RegistryKey hive, string path, StartupItem.Loc loc, string locText)
        {
            try
            {
                using (RegistryKey key = hive.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (string name in key.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        var item = new StartupItem();
                        item.Name = name;
                        item.Command = Convert.ToString(key.GetValue(name));
                        item.Location = loc;
                        item.LocationText = locText;
                        item.Enabled = ReadApproved(loc, name);
                        AssignRisk(item);
                        list.Add(item);
                    }
                }
            }
            catch { }
        }

        private void ReadFolder(List<StartupItem> list, string folder, StartupItem.Loc loc, string locText)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
                foreach (string file in Directory.GetFiles(folder))
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    var item = new StartupItem();
                    item.Name = fileName;
                    item.Command = file;
                    item.Location = loc;
                    item.LocationText = locText;
                    item.Enabled = ReadApproved(loc, fileName);
                    AssignRisk(item);
                    list.Add(item);
                }
            }
            catch { }
        }

        private static bool ReadApproved(StartupItem.Loc loc, string name)
        {
            RegistryKey hive;
            string subkey;
            ApprovedLocation(loc, out hive, out subkey);
            try
            {
                using (RegistryKey key = hive.OpenSubKey(subkey))
                {
                    if (key == null) return true;
                    byte[] data = key.GetValue(name) as byte[];
                    if (data == null || data.Length == 0) return true;
                    return (data[0] & 1) == 0;
                }
            }
            catch { return true; }
        }

        public string SetEnabled(StartupItem item, bool enable)
        {
            RegistryKey hive;
            string subkey;
            ApprovedLocation(item.Location, out hive, out subkey);
            try
            {
                using (RegistryKey key = hive.CreateSubKey(subkey))
                {
                    byte[] data = new byte[12];
                    data[0] = (byte)(enable ? 0x02 : 0x03);
                    key.SetValue(item.Name, data, RegistryValueKind.Binary);
                }
                item.Enabled = enable;
                return "OK | " + item.Name + " | " + (enable ? "habilitado" : "deshabilitado");
            }
            catch (Exception ex)
            {
                return "ERROR | " + item.Name + " | " + ex.Message;
            }
        }

        private static void ApprovedLocation(StartupItem.Loc loc, out RegistryKey hive, out string subkey)
        {
            switch (loc)
            {
                case StartupItem.Loc.HkcuRun:
                    hive = Registry.CurrentUser; subkey = ApprovedRun; break;
                case StartupItem.Loc.UserFolder:
                    hive = Registry.CurrentUser; subkey = ApprovedFolder; break;
                case StartupItem.Loc.CommonFolder:
                    hive = Registry.LocalMachine; subkey = ApprovedFolder; break;
                default:
                    hive = Registry.LocalMachine; subkey = ApprovedRun; break;
            }
        }

        private static void AssignRisk(StartupItem item)
        {
            string n = ((item.Name ?? "") + " " + (item.Command ?? "")).ToLowerInvariant();
            if (Contains(n, "securityhealth", "defender", "rtkaudio", "realtek", "nvidia", "nvcontainer", "igfx", "synaptics", "audiodg", "waves", "soundblaster", "killer", "intel(r)"))
            {
                item.Risk = "MEDIO";
                item.RiskReason = "Parece relacionado con audio, gráficos o seguridad. Deshabilitarlo puede afectar esas funciones (es reversible).";
            }
            else
            {
                item.Risk = "BAJO";
                item.RiskReason = "App de terceros al inicio. Deshabilitarla solo evita que arranque sola; podés volver a habilitarla cuando quieras.";
            }
        }

        private static bool Contains(string value, params string[] needles)
        {
            foreach (string n in needles) if (value.Contains(n)) return true;
            return false;
        }
    }
}
