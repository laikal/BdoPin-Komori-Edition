using System;
using System.Collections.Generic;
namespace BdoPin.Models
{
    internal enum Mode { OneThreadPerPhysicalCore, AllLogicalProcessors, PerformanceCoresPhysicalOnly, PerformanceCoresOnly, ExplicitCpuList }
    internal sealed class Preset
    {
        internal string Id, Name, Vendor; internal Mode Mode; internal int[] Cpus = new int[0];
        public override string ToString() { return Name; }
    }
    internal sealed class Configuration
    {
        internal string Target = "BlackDesert64.exe", Selected = "Auto", Language = "auto", Priority = "AboveNormal";
        internal bool AutoDetect = true, AutoApply = true, Tray = true, Startup, LegacyLog;
        internal List<Preset> Presets = new List<Preset>();
        internal uint PriorityValue { get { return Priority == "High" ? 0x80u : Priority == "Normal" ? 0x20u : 0x8000u; } }
    }
    internal sealed class CpuCore
    {
        internal ushort Group; internal byte Efficiency; internal List<int> Cpus = new List<int>();
    }
    internal sealed class Topology
    {
        internal string Vendor = "", Name = "";
        internal int Groups, LogicalCount;
        internal List<CpuCore> Cores = new List<CpuCore>();
    }
    internal sealed class Selection
    {
        internal string Name; internal int[] Cpus; internal ulong Mask;
    }
    internal sealed class ApplyResult
    {
        internal bool Success, AffinityApplied; internal int Error; internal string Stage;
    }
}
