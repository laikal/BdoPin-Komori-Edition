using System;
using System.Management;
namespace BdoPin.Services
{
    internal sealed class ProcessWatcher : IDisposable
    {
        private ManagementEventWatcher watcher;
        private bool stopping;
        internal void Start(string target,Action<uint> started,Action<string> state)
        {
            Dispose(); stopping=false;
            try {
                watcher=new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = '"+target+"'"));
                watcher.EventArrived+=(s,e)=> { if(!stopping)started(Convert.ToUInt32(e.NewEvent["ProcessID"])); };
                watcher.Stopped+=(s,e)=> { if(!stopping)state("Disconnected "+((int)e.Status).ToString("X8")); };
                watcher.Start(); state("Active");
            } catch(Exception ex) {
                Dispose(); var management=ex as ManagementException;
                state("Failed "+(management==null?ex.HResult:(int)management.ErrorCode).ToString("X8"));
            }
        }
        public void Dispose()
        {
            stopping=true; var old=watcher; watcher=null;
            if(old==null)return;
            try { old.Stop(); } catch(ManagementException) {} catch(System.Runtime.InteropServices.COMException) {}
            finally { old.Dispose(); }
        }
    }
}
