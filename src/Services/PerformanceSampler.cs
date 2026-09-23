using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;
using BdoPin.Native;
using BdoPin.Logging;
namespace BdoPin.Services
{
    internal sealed class CounterSample { internal string Name; internal double Value; }
    internal sealed class GpuAdapter { internal string Key,Name; internal ulong Capacity; }
    internal sealed class GpuReading { internal string Name; internal double? Usage,Bytes; internal ulong Capacity; }
    internal sealed class PerformanceSample
    {
        internal uint Pid; internal int Targets; internal bool Denied;
        internal double? Cpu;
        internal string Drive; internal bool DriveFallback;
        internal double? DiskRead,DiskWrite;
        internal Dictionary<string,double> Logical=new Dictionary<string,double>();
        internal GpuReading Gpu=new GpuReading();
    }
    internal sealed class PerformanceSampler : IDisposable
    {
        private IntPtr query,processors,engines,memory,diskRead,diskWrite;
        private string gameDrive;
        private readonly List<GpuAdapter> adapters=new List<GpuAdapter>();
        private KernelHandle process;
        private uint pid;
        private long previousCpu,previousTime;
        private bool previous,disposed;
        internal PerformanceSampler()
        {
            try {
                if(NativeMethods.PdhOpenQuery(null,UIntPtr.Zero,out query)==0) {
                    Add(@"\Processor Information(*)\% Processor Time",out processors);
                    Add(@"\GPU Engine(*)\Utilization Percentage",out engines);
                    Add(@"\GPU Process Memory(*)\Dedicated Usage",out memory);
                    Add(@"\LogicalDisk(*)\Disk Read Bytes/sec",out diskRead);
                    Add(@"\LogicalDisk(*)\Disk Write Bytes/sec",out diskWrite);
                    NativeMethods.PdhCollectQueryData(query);
                }
                ReadAdapters();
                AppLog.Write("Performance Monitor opened");
            } catch { Dispose(); throw; }
        }
        private void Add(string path,out IntPtr counter)
        {
            uint result=NativeMethods.PdhAddEnglishCounter(query,path,UIntPtr.Zero,out counter);
            if(result!=0) { counter=IntPtr.Zero; AppLog.Write("Performance counter unavailable "+result.ToString("X8")); }
        }
        private void ReadAdapters()
        {
            IntPtr factory;
            var guid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
            if(NativeMethods.CreateDXGIFactory1(ref guid,out factory)<0)return;
            try {
                // IDXGIObject(4), IDXGIFactory(5), then IDXGIFactory1.EnumAdapters1 at slot 12.
                var enumerate=(NativeMethods.EnumAdapters1)Marshal.GetDelegateForFunctionPointer(
                    Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory),12*IntPtr.Size),typeof(NativeMethods.EnumAdapters1));
                for(uint i=0;i<64;i++) {
                    IntPtr adapter; if(enumerate(factory,i,out adapter)!=0)break;
                    try {
                        var describe=(NativeMethods.GetDesc1)Marshal.GetDelegateForFunctionPointer(
                            Marshal.ReadIntPtr(Marshal.ReadIntPtr(adapter),10*IntPtr.Size),typeof(NativeMethods.GetDesc1));
                        NativeMethods.AdapterDesc desc;
                        if(describe(adapter,out desc)==0 && (desc.Flags&2)==0)
                            adapters.Add(new GpuAdapter { Key=unchecked((uint)desc.LuidHigh).ToString("X8")+":"+desc.LuidLow.ToString("X8"),
                                Name=desc.Description,Capacity=desc.DedicatedVideo.ToUInt64() });
                    } finally { Marshal.Release(adapter); }
                }
            } finally { Marshal.Release(factory); }
        }
        private List<CounterSample> Values(IntPtr counter)
        {
            var result=new List<CounterSample>(); if(counter==IntPtr.Zero)return result;
            uint bytes=0,count=0;
            if(NativeMethods.PdhGetFormattedCounterArray(counter,0x8200,ref bytes,ref count,IntPtr.Zero)!=0x800007D2 || bytes==0 || bytes>32*1024*1024)return result;
            int allocated=(int)bytes; IntPtr buffer=Marshal.AllocHGlobal(allocated);
            try {
                if(NativeMethods.PdhGetFormattedCounterArray(counter,0x8200,ref bytes,ref count,buffer)!=0)return result;
                int size=Marshal.SizeOf(typeof(NativeMethods.CounterItem)); if(count>allocated/size)return result;
                for(int i=0;i<count;i++) {
                    var item=(NativeMethods.CounterItem)Marshal.PtrToStructure(IntPtr.Add(buffer,i*size),typeof(NativeMethods.CounterItem));
                    if(item.Value.Status<=1 && !double.IsNaN(item.Value.Value) && !double.IsInfinity(item.Value.Value))
                        result.Add(new CounterSample { Name=Marshal.PtrToStringUni(item.Name),Value=item.Value.Value });
                }
            } finally { Marshal.FreeHGlobal(buffer); }
            return result;
        }
        internal static double Percent(long cpu,long wallTicks,uint logical)
        {
            return wallTicks<=0||logical==0?0:Clamp(cpu/10000000.0/(wallTicks/(double)Stopwatch.Frequency)/logical*100);
        }
        internal static double Clamp(double n) { return Math.Max(0,Math.Min(100,n)); }
        private static bool GpuKey(string name,uint pid,out string key,out bool unsupported)
        {
            key=null; unsupported=false;
            var m=Regex.Match(name??"", @"^pid_(\d+)_luid_0x([0-9a-fA-F]{1,8})_0x([0-9a-fA-F]{1,8})_phys_(\d+)(?:_|$)");
            uint parsed,physical;
            if(!m.Success||!uint.TryParse(m.Groups[1].Value,out parsed)||parsed!=pid||!uint.TryParse(m.Groups[4].Value,out physical))return false;
            key=uint.Parse(m.Groups[2].Value,NumberStyles.HexNumber).ToString("X8")+":"+uint.Parse(m.Groups[3].Value,NumberStyles.HexNumber).ToString("X8");
            unsupported=physical!=0; return true;
        }
        internal static GpuReading Match(uint pid,IEnumerable<CounterSample> engineValues,IEnumerable<CounterSample> memoryValues,List<GpuAdapter> adapters)
        {
            var result=new GpuReading(); if(pid==0)return result;
            var usage=new Dictionary<string,double>(); var bytes=new Dictionary<string,double>();
            var active=new HashSet<string>(); var candidates=new HashSet<string>(); var unsupported=new HashSet<string>();
            foreach(var s in engineValues) {
                string key; bool bad;
                if(!GpuKey(s.Name,pid,out key,out bad)||!s.Name.Contains("_engtype_3D")||s.Value<0||double.IsNaN(s.Value)||double.IsInfinity(s.Value))continue;
                if(bad)unsupported.Add(key); candidates.Add(key);
                double prior; usage.TryGetValue(key,out prior); usage[key]=Math.Max(prior,Clamp(s.Value)); if(s.Value>0.01)active.Add(key);
            }
            foreach(var s in memoryValues) {
                string key; bool bad;
                if(!GpuKey(s.Name,pid,out key,out bad)||s.Value<0||double.IsNaN(s.Value)||double.IsInfinity(s.Value))continue;
                if(bad||bytes.ContainsKey(key))unsupported.Add(key); bytes[key]=s.Value; if(s.Value>0)candidates.Add(key);
            }
            string chosen=active.Count==1?active.First():active.Count==0&&candidates.Count==1?candidates.First():null;
            if(chosen==null||unsupported.Contains(chosen))return result;
            var matches=adapters.Where(x=>x.Key==chosen).ToList(); if(matches.Count!=1)return result;
            result.Name=matches[0].Name; result.Capacity=matches[0].Capacity;
            if(usage.ContainsKey(chosen))result.Usage=usage[chosen];
            if(bytes.ContainsKey(chosen)&&result.Capacity>0&&bytes[chosen]<=result.Capacity)result.Bytes=bytes[chosen];
            return result;
        }
        internal PerformanceSample Sample()
        {
            if(disposed)throw new ObjectDisposedException("PerformanceSampler");
            var result=new PerformanceSample(); var targets=AffinityService.Find("BlackDesert64.exe"); result.Targets=targets.Count;
            uint next=targets.Count==1?targets[0]:0;
            if(next!=pid) { if(process!=null)process.Dispose(); process=null; pid=next; previous=false; gameDrive=null; }
            result.Pid=pid;
            if(pid!=0&&process==null) {
                process=NativeMethods.OpenProcess(0x1000,false,pid);
                try {
                    if(process.IsInvalid||!AffinityService.Verify(process,"BlackDesert64.exe")) { process.Dispose(); process=null; }
                    else {
                        uint length=32768;var path=new StringBuilder((int)length);
                        if(NativeMethods.QueryFullProcessImageName(process,0,path,ref length))gameDrive=DiskMetrics.Drive(path.ToString());
                    }
                } catch(System.ComponentModel.Win32Exception) { process.Dispose(); process=null; }
            }
            result.Denied=pid!=0&&process==null;
            result.Drive=gameDrive??DiskMetrics.Drive(Environment.SystemDirectory);
            result.DriveFallback=gameDrive==null;
            if(process!=null) {
                long created,exited,kernel,user; uint code;
                if(NativeMethods.GetExitCodeProcess(process,out code)&&code==259&&NativeMethods.GetProcessTimes(process,out created,out exited,out kernel,out user)) {
                    long now=Stopwatch.GetTimestamp(),cpu=kernel+user;
                    if(previous&&cpu>=previousCpu&&now>previousTime)result.Cpu=Percent(cpu-previousCpu,now-previousTime,NativeMethods.GetActiveProcessorCount(0xffff));
                    previousCpu=cpu; previousTime=now; previous=true;
                } else { process.Dispose(); process=null; previous=false; result.Denied=true; }
            }
            if(query!=IntPtr.Zero&&NativeMethods.PdhCollectQueryData(query)==0) {
                foreach(var value in Values(processors))if(Regex.IsMatch(value.Name,@"^\d+,\d+$"))result.Logical[value.Name]=Clamp(value.Value);
                result.Gpu=Match(pid,Values(engines),Values(memory),adapters);
                result.DiskRead=DiskMetrics.Rate(Values(diskRead),result.Drive);
                result.DiskWrite=DiskMetrics.Rate(Values(diskWrite),result.Drive);
            }
            return result;
        }
        public void Dispose()
        {
            if(disposed)return; disposed=true;
            if(process!=null) { process.Dispose(); process=null; }
            if(query!=IntPtr.Zero) { NativeMethods.PdhCloseQuery(query); query=IntPtr.Zero; }
            processors=engines=memory=diskRead=diskWrite=IntPtr.Zero;
            AppLog.Write("Performance Monitor closed");
        }
    }
}
