using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace BdoPin.Native
{
    internal sealed class KernelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public KernelHandle() : base(true) { }
        protected override bool ReleaseHandle() { return NativeMethods.CloseHandle(handle); }
    }
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern KernelHandle OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern bool QueryFullProcessImageName(KernelHandle handle, uint flags, StringBuilder path, ref uint size);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool GetProcessAffinityMask(KernelHandle process, out UIntPtr processMask, out UIntPtr systemMask);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool SetProcessAffinityMask(KernelHandle process, UIntPtr mask);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool SetPriorityClass(KernelHandle process, uint priority);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern uint GetPriorityClass(KernelHandle process);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool GetExitCodeProcess(KernelHandle process, out uint code);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool GetProcessTimes(KernelHandle process, out long created, out long exited, out long kernel, out long user);
        [DllImport("kernel32.dll")] internal static extern ushort GetActiveProcessorGroupCount();
        [DllImport("kernel32.dll")] internal static extern uint GetActiveProcessorCount(ushort group);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool GetLogicalProcessorInformationEx(int relation, IntPtr buffer, ref uint length);
        [DllImport("kernel32.dll", SetLastError=true)] internal static extern KernelHandle CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern bool Process32First(KernelHandle snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern bool Process32Next(KernelHandle snapshot, ref ProcessEntry entry);
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        internal struct ProcessEntry {
            internal uint Size, Usage, Pid; internal UIntPtr Heap; internal uint Module, Threads, Parent;
            internal int Priority; internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] internal string Name;
        }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern KernelHandle CreateMutexEx(IntPtr attributes, string name, uint flags, uint access);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool ChangeWindowMessageFilterEx(IntPtr window, uint message, uint action, IntPtr change);
        [DllImport("pdh.dll", CharSet=CharSet.Unicode)] internal static extern uint PdhOpenQuery(string source, UIntPtr data, out IntPtr query);
        [DllImport("pdh.dll", CharSet=CharSet.Unicode)] internal static extern uint PdhAddEnglishCounter(IntPtr query, string path, UIntPtr data, out IntPtr counter);
        [DllImport("pdh.dll")] internal static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll", CharSet=CharSet.Unicode)] internal static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bytes, ref uint count, IntPtr buffer);
        [DllImport("pdh.dll")] internal static extern uint PdhCloseQuery(IntPtr query);
        [StructLayout(LayoutKind.Sequential)] internal struct CounterValue { internal uint Status; internal double Value; }
        [StructLayout(LayoutKind.Sequential)] internal struct CounterItem { internal IntPtr Name; internal CounterValue Value; }
        [DllImport("dxgi.dll", ExactSpelling=true)] internal static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);
        // COM vtable delegates are declared here with the documented IDXGIFactory1 / IDXGIAdapter1 ABI.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int EnumAdapters1(IntPtr self, uint index, out IntPtr adapter);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetDesc1(IntPtr self, out AdapterDesc description);
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        internal struct AdapterDesc {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] internal string Description;
            internal uint Vendor, Device, Subsys, Revision;
            internal UIntPtr DedicatedVideo, DedicatedSystem, Shared;
            internal uint LuidLow; internal int LuidHigh; internal uint Flags;
        }
    }
}
