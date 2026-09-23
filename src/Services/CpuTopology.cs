using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using BdoPin.Models;
using BdoPin.Native;
namespace BdoPin.Services
{
    internal static class CpuTopology
    {
        internal static Topology Read()
        {
            var t = new Topology { Groups=NativeMethods.GetActiveProcessorGroupCount() };
            using(var key=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) {
                t.Vendor=(key == null ? "" : key.GetValue("VendorIdentifier","").ToString()).Trim();
                t.Name=(key == null ? "Unknown CPU" : key.GetValue("ProcessorNameString","Unknown CPU").ToString()).Trim();
            }
            uint size=0;
            NativeMethods.GetLogicalProcessorInformationEx(0,IntPtr.Zero,ref size);
            if(size==0 || size>16*1024*1024 || Marshal.GetLastWin32Error()!=122) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr data=Marshal.AllocHGlobal((int)size);
            try {
                if(!NativeMethods.GetLogicalProcessorInformationEx(0,data,ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
                byte[] bytes=new byte[size]; Marshal.Copy(data,bytes,0,(int)size);
                t.Cores=ParseRecords(bytes,t.Groups);
            } finally { Marshal.FreeHGlobal(data); }
            t.LogicalCount=t.Cores.Sum(c=>c.Cpus.Count);
            if(t.Cores.Count==0 || t.LogicalCount!=NativeMethods.GetActiveProcessorCount(0xffff)) throw new InvalidOperationException("Incomplete CPU topology.");
            return t;
        }
        internal static List<CpuCore> ParseRecords(byte[] bytes,int groups)
        {
            var cores=new List<CpuCore>(); var seen=new HashSet<string>();
            for(int offset=0;offset<bytes.Length;) {
                if(bytes.Length-offset<8)throw new FormatException("Truncated topology header.");
                int size=BitConverter.ToInt32(bytes,offset+4),relation=BitConverter.ToInt32(bytes,offset);
                if(size<8 || size>bytes.Length-offset)throw new FormatException("Invalid topology record.");
                if(relation==0) {
                    if(size<48 || BitConverter.ToUInt16(bytes,offset+30)!=1)throw new FormatException("Unsupported core group mapping.");
                    var core=new CpuCore { Efficiency=bytes[offset+9],Group=BitConverter.ToUInt16(bytes,offset+40) };
                    if(core.Group>=groups)throw new FormatException("Invalid processor group.");
                    ulong mask=BitConverter.ToUInt64(bytes,offset+32);
                    for(int bit=0;bit<64;bit++)if((mask&(1UL<<bit))!=0) {
                        if(!seen.Add(core.Group+":"+bit))throw new FormatException("Overlapping topology.");
                        core.Cpus.Add(bit);
                    }
                    if(core.Cpus.Count==0)throw new FormatException("Empty core.");
                    cores.Add(core);
                }
                offset+=size;
            }
            return cores;
        }
        internal static Selection Build(Topology t,Configuration cfg,bool all=false)
        {
            if(t.Groups!=1 || t.LogicalCount>64)throw new InvalidOperationException("Multiple processor groups: affinity is disabled (native BdoPin policy).");
            if(t.Cores.Count==0 || t.LogicalCount==0)throw new InvalidOperationException("CPU topology unavailable.");
            string wanted=cfg.Selected;
            if(!all && ConfigStore.Comparer.Equals(wanted,"Auto")) {
                if(!cfg.AutoDetect)throw new InvalidOperationException("Auto detection is off. Select a preset.");
                wanted=t.Vendor=="AuthenticAMD"?"AMD_Ryzen_Physical":t.Vendor=="GenuineIntel"?"Intel_PCore_Physical":"";
            }
            Preset p=all?new Preset { Name="Remove Settings",Vendor="Any",Mode=Mode.AllLogicalProcessors }:
                cfg.Presets.FirstOrDefault(x=>ConfigStore.Comparer.Equals(x.Id,wanted));
            if(p==null)throw new InvalidOperationException("Preset not found for this CPU.");
            if(p.Vendor!="Any" && p.Vendor!=t.Vendor)throw new InvalidOperationException("Preset vendor does not match CPU.");
            bool perf=p.Mode==Mode.PerformanceCoresOnly || p.Mode==Mode.PerformanceCoresPhysicalOnly;
            byte max=t.Cores.Max(x=>x.Efficiency);
            if(perf && (t.Vendor!="GenuineIntel" || t.Cores.Select(x=>x.Efficiency).Distinct().Count()<2))
                throw new InvalidOperationException("Distinct Intel P/E classes unavailable. Choose a non-P/E preset.");
            var available=new HashSet<int>(); var chosen=new SortedSet<int>();
            foreach(var c in t.Cores) {
                if(c.Group!=0 || c.Cpus.Count==0)throw new InvalidOperationException("Invalid single-group topology.");
                foreach(int n in c.Cpus) if(n<0||n>63||!available.Add(n))throw new InvalidOperationException("Invalid logical CPU mapping.");
                if(p.Mode==Mode.ExplicitCpuList || (perf&&c.Efficiency!=max))continue;
                if(p.Mode==Mode.OneThreadPerPhysicalCore||p.Mode==Mode.PerformanceCoresPhysicalOnly)chosen.Add(c.Cpus.Min());
                else chosen.UnionWith(c.Cpus);
            }
            if(available.Count!=t.LogicalCount)throw new InvalidOperationException("Incomplete CPU mapping.");
            if(p.Mode==Mode.ExplicitCpuList)foreach(int n in p.Cpus)
                if(!available.Contains(n)||!chosen.Add(n))throw new InvalidOperationException("Invalid custom CPU.");
            if(chosen.Count==0)throw new InvalidOperationException("No CPUs selected.");
            return new Selection { Name=p.Name,Cpus=chosen.ToArray(),Mask=chosen.Aggregate(0UL,(mask,n)=>mask|(1UL<<n)) };
        }
    }
}
