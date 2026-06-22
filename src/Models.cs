using System;

namespace PcOptimizerPortable
{
    public enum AppKind
    {
        Classic,
        StoreApp,
        ProvisionedApp,
        OptionalFeature,
        WindowsCapability,
        WindowsComponent
    }

    public enum RiskLevel
    {
        Bajo = 0,
        Medio = 1,
        Alto = 2,
        Critico = 3
    }

    public sealed class AppItem
    {
        public bool Selected { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Publisher { get; set; }
        public string Source { get; set; }
        public string Scope { get; set; }
        public string State { get; set; }
        public string InstallLocation { get; set; }
        public string UninstallCommand { get; set; }
        public string QuietUninstallCommand { get; set; }
        public string PackageFullName { get; set; }
        public string ProvisionedPackageName { get; set; }
        public string FeatureName { get; set; }
        public string CapabilityName { get; set; }
        public string ExecutableHint { get; set; }
        public bool NonRemovable { get; set; }
        public bool IsFramework { get; set; }
        public AppKind Kind { get; set; }
        public RiskLevel Risk { get; set; }
        public string RiskReason { get; set; }
        public string Description { get; set; }
        public string Action { get; set; }
        public bool CanUninstall { get; set; }
        public bool CanDisable { get; set; }
        public bool ResourceMeasurable { get; set; }
        public bool IsRunning { get; set; }
        public int ProcessCount { get; set; }
        public double CpuPercent { get; set; }
        public double MemoryMb { get; set; }
        public double DiskMbPerSecond { get; set; }

        public AppItem()
        {
            Id = "";
            Name = "";
            Version = "";
            Publisher = "";
            Source = "";
            Scope = "";
            State = "";
            InstallLocation = "";
            UninstallCommand = "";
            QuietUninstallCommand = "";
            PackageFullName = "";
            ProvisionedPackageName = "";
            FeatureName = "";
            CapabilityName = "";
            ExecutableHint = "";
            RiskReason = "";
            Description = "";
            Action = "Desinstalar";
        }

        public string CpuText { get { return ResourceMeasurable ? CpuPercent.ToString("0.0") + " %" : "N/D"; } }
        public string MemoryText { get { return ResourceMeasurable ? MemoryMb.ToString("0") + " MB" : "N/D"; } }
        public string DiskText { get { return ResourceMeasurable ? DiskMbPerSecond.ToString("0.00") + " MB/s" : "N/D"; } }
        public string ProcessText { get { return ResourceMeasurable ? ProcessCount.ToString() : "N/D"; } }

        public string RiskText
        {
            get
            {
                if (Risk == RiskLevel.Critico) return "CRÍTICO";
                if (Risk == RiskLevel.Alto) return "ALTO";
                if (Risk == RiskLevel.Medio) return "MEDIO";
                return "BAJO";
            }
        }

        public string UniqueKey
        {
            get
            {
                return Kind.ToString() + "|" + Id + "|" + Scope;
            }
        }
    }

    public sealed class OperationPlanItem
    {
        public AppItem Item { get; set; }
        public string Action { get; set; }
        public string Method { get; set; }
        public string ExactCommand { get; set; }
        public bool Supported { get; set; }
        public string Warning { get; set; }
    }

    public sealed class OperationResult
    {
        public AppItem Item { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
