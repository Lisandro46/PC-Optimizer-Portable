using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace PcOptimizerPortable
{
    public sealed class ResourceMonitorService
    {
        private sealed class ProcessSample
        {
            public int Id;
            public string Name;
            public string Path;
            public long CpuTicks;
            public long WorkingSet;
            public ulong IoBytes;
        }

        private sealed class Totals
        {
            public double Cpu;
            public long Memory;
            public double DiskBytesPerSecond;
            public int Count;
        }

        public void Measure(IList<AppItem> items, int sampleMilliseconds)
        {
            if (sampleMilliseconds < 500) sampleMilliseconds = 500;
            Dictionary<int, ProcessSample> first = Capture();
            var watch = Stopwatch.StartNew();
            Thread.Sleep(sampleMilliseconds);
            Dictionary<int, ProcessSample> second = Capture();
            watch.Stop();

            foreach (AppItem item in items)
            {
                item.ResourceMeasurable = CanMeasure(item);
                item.IsRunning = false;
                item.ProcessCount = 0;
                item.CpuPercent = 0;
                item.MemoryMb = 0;
                item.DiskMbPerSecond = 0;
            }

            var totals = new Dictionary<AppItem, Totals>();
            foreach (ProcessSample current in second.Values)
            {
                AppItem owner = FindOwner(current, items);
                if (owner == null) continue;

                Totals total;
                if (!totals.TryGetValue(owner, out total))
                {
                    total = new Totals();
                    totals.Add(owner, total);
                }

                total.Count++;
                total.Memory += current.WorkingSet;
                ProcessSample previous;
                if (first.TryGetValue(current.Id, out previous))
                {
                    long cpuDelta = Math.Max(0, current.CpuTicks - previous.CpuTicks);
                    total.Cpu += (cpuDelta / (double)TimeSpan.TicksPerMillisecond) / Math.Max(1.0, watch.Elapsed.TotalMilliseconds) / Environment.ProcessorCount * 100.0;
                    ulong ioDelta = current.IoBytes >= previous.IoBytes ? current.IoBytes - previous.IoBytes : 0;
                    total.DiskBytesPerSecond += ioDelta / Math.Max(0.1, watch.Elapsed.TotalSeconds);
                }
            }

            foreach (KeyValuePair<AppItem, Totals> pair in totals)
            {
                pair.Key.ResourceMeasurable = true;
                pair.Key.IsRunning = pair.Value.Count > 0;
                pair.Key.ProcessCount = pair.Value.Count;
                pair.Key.CpuPercent = Math.Min(100.0, pair.Value.Cpu);
                pair.Key.MemoryMb = pair.Value.Memory / 1024.0 / 1024.0;
                pair.Key.DiskMbPerSecond = pair.Value.DiskBytesPerSecond / 1024.0 / 1024.0;
            }
        }

        private static Dictionary<int, ProcessSample> Capture()
        {
            var result = new Dictionary<int, ProcessSample>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    var sample = new ProcessSample();
                    sample.Id = process.Id;
                    sample.Name = process.ProcessName;
                    sample.CpuTicks = process.TotalProcessorTime.Ticks;
                    sample.WorkingSet = process.WorkingSet64;
                    try { sample.Path = process.MainModule.FileName; }
                    catch { sample.Path = ""; }
                    IoCounters counters;
                    if (GetProcessIoCounters(process.Handle, out counters))
                        sample.IoBytes = counters.ReadTransferCount + counters.WriteTransferCount;
                    result[sample.Id] = sample;
                }
                catch { }
                finally { process.Dispose(); }
            }
            return result;
        }

        private static AppItem FindOwner(ProcessSample process, IList<AppItem> items)
        {
            AppItem best = null;
            int bestLength = -1;
            if (!String.IsNullOrWhiteSpace(process.Path))
            {
                string path = NormalizePath(process.Path);
                foreach (AppItem item in items)
                {
                    if (!CanMeasure(item) || String.IsNullOrWhiteSpace(item.InstallLocation)) continue;
                    string root = NormalizePath(item.InstallLocation).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && root.Length > bestLength)
                    {
                        best = item;
                        bestLength = root.Length;
                    }
                }
            }
            if (best != null) return best;

            foreach (AppItem item in items)
            {
                if (!CanMeasure(item) || String.IsNullOrWhiteSpace(item.ExecutableHint)) continue;
                if (item.ExecutableHint.Equals(process.Name, StringComparison.OrdinalIgnoreCase)) return item;
            }
            return null;
        }

        private static bool CanMeasure(AppItem item)
        {
            if (item.Kind == AppKind.OptionalFeature || item.Kind == AppKind.WindowsCapability || item.Kind == AppKind.ProvisionedApp) return false;
            return !String.IsNullOrWhiteSpace(item.InstallLocation) || !String.IsNullOrWhiteSpace(item.ExecutableHint);
        }

        private static string NormalizePath(string value)
        {
            try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'))); }
            catch { return value ?? ""; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters lpIoCounters);
    }
}
