using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using BdoPin.Native;
namespace BdoPin.Core
{
    internal sealed class Instance : IDisposable
    {
        internal static readonly uint RestoreMessage=NativeMethods.RegisterWindowMessage("Eltax.BdoPin.Restore");
        private KernelHandle handle;
        internal bool First { get; private set; }
        internal Instance()
        {
            // Synchronize-only access permits checking an elevated instance without requesting mutation rights.
            handle=NativeMethods.CreateMutexEx(IntPtr.Zero,@"Local\Eltax.BdoPin.KomoriEdition",0,0x100000);
            int error=Marshal.GetLastWin32Error();
            if(handle.IsInvalid) { if(error==5) { First=false; return; } throw new Win32Exception(error); }
            First=error!=183;
        }
        internal static bool IsAdmin {
            get { using(var id=WindowsIdentity.GetCurrent())return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); }
        }
        internal static void Restore()
        {
            IntPtr hwnd=NativeMethods.FindWindow(null,VersionInfo.Product);
            if(hwnd!=IntPtr.Zero) { NativeMethods.PostMessage(hwnd,RestoreMessage,IntPtr.Zero,IntPtr.Zero); NativeMethods.ShowWindow(hwnd,9); NativeMethods.SetForegroundWindow(hwnd); }
        }
        public void Dispose() { if(handle!=null)handle.Dispose(); }
    }
}
