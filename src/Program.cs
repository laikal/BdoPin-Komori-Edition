using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using BdoPin.Core;
using BdoPin.Logging;
using BdoPin.UI;
namespace BdoPin
{
    internal static class Program
    {
        [STAThread] private static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                bool apply=args.Contains("--apply-now");
                bool reset=args.Contains("--remove-settings");
                if(apply&&reset)return;
                int index=Array.IndexOf(args,"--handoff-pid"),pid;
                if(index>=0) {
                    if(!Instance.IsAdmin || index+1>=args.Length || !int.TryParse(args[index+1],out pid) || pid<=0)return;
                    try { using(var parent=Process.GetProcessById(pid)) if(!parent.WaitForExit(30000))return; }
                    catch(ArgumentException) { }
                }
                if((apply||reset)&&!Instance.IsAdmin)return;
                using(var instance=new Instance()) {
                    if(!instance.First) { Instance.Restore();return; }
                    AppPaths.EnsureConfig();
                    AppLog.Write(VersionInfo.StartupMessage);
                    Application.Run(new MainForm(apply,args.Contains("--tray"),false,reset));
                    AppLog.Write("BdoPin exited");
                }
            } catch(Exception ex) {
                AppLog.Write("Startup failed "+ex.HResult.ToString("X8"));
                MessageBox.Show("BdoPin could not start.\r\n"+ex.Message,VersionInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }
    }
}
