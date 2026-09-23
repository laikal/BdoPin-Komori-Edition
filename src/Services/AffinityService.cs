using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BdoPin.Native;
using BdoPin.Models;
namespace BdoPin.Services
{
    internal static class AffinityService
    {
        internal static List<uint> Find(string target)
        {
            var result=new List<uint>();
            using(var snapshot=NativeMethods.CreateToolhelp32Snapshot(2,0)) {
                if(snapshot.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
                var e=new NativeMethods.ProcessEntry { Size=(uint)Marshal.SizeOf(typeof(NativeMethods.ProcessEntry)) };
                if(!NativeMethods.Process32First(snapshot,ref e)) {
                    if(Marshal.GetLastWin32Error()==18)return result;
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                do { if(string.Equals(e.Name,target,StringComparison.OrdinalIgnoreCase))result.Add(e.Pid); }
                while(NativeMethods.Process32Next(snapshot,ref e));
                if(Marshal.GetLastWin32Error()!=18)throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return result;
        }
        internal static bool Verify(KernelHandle handle,string target)
        {
            uint size=32768; var path=new StringBuilder((int)size);
            if(!NativeMethods.QueryFullProcessImageName(handle,0,path,ref size))throw new Win32Exception(Marshal.GetLastWin32Error());
            return string.Equals(Path.GetFileName(path.ToString()),target,StringComparison.OrdinalIgnoreCase);
        }
        private static void Require(bool ok) { if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error()); }
        internal static ApplyResult Apply(uint pid,string target,Selection selection,uint priority)
        {
            var result=new ApplyResult { Stage="Affinity" };
            try {
                if(NativeMethods.GetActiveProcessorGroupCount()!=1 || selection.Mask==0)throw new Win32Exception(50);
                using(var process=NativeMethods.OpenProcess(0x1200,false,pid)) {
                    Require(!process.IsInvalid);
                    if(!Verify(process,target))throw new Win32Exception(13);
                    uint exit; Require(NativeMethods.GetExitCodeProcess(process,out exit));
                    if(exit!=259)throw new Win32Exception(1067);
                    UIntPtr oldMask,system;
                    Require(NativeMethods.GetProcessAffinityMask(process,out oldMask,out system));
                    if((selection.Mask&system.ToUInt64())!=selection.Mask)throw new Win32Exception(87);
                    Require(NativeMethods.SetProcessAffinityMask(process,new UIntPtr(selection.Mask)));
                    Require(NativeMethods.GetProcessAffinityMask(process,out oldMask,out system));
                    if(oldMask.ToUInt64()!=selection.Mask)throw new Win32Exception(13);
                    result.AffinityApplied=true; result.Stage="Priority";
                    Require(NativeMethods.SetPriorityClass(process,priority));
                    uint actual=NativeMethods.GetPriorityClass(process); Require(actual!=0);
                    if(actual!=priority)throw new Win32Exception(13);
                    result.Success=true; result.Stage="Complete";
                }
            } catch(Win32Exception ex) { result.Error=ex.NativeErrorCode; }
            return result;
        }
        internal static bool Retry(ApplyResult result,int attempts)
        {
            return !result.Success && attempts<3 && (result.Error==87||result.Error==21||result.Error==170||result.Error==1237||result.Error==299||result.Error==31);
        }
    }
}
