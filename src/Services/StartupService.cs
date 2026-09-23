using System;
using Microsoft.Win32;
namespace BdoPin.Services
{
    internal static class StartupService
    {
        private const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static bool Registered(string executable)
        {
            using(var key=Registry.CurrentUser.OpenSubKey(Key))
                return key!=null && string.Equals(key.GetValue("BdoPin") as string,"\""+executable+"\" --tray",StringComparison.OrdinalIgnoreCase);
        }
        internal static string Current()
        {
            using(var key=Registry.CurrentUser.OpenSubKey(Key))return key==null?null:key.GetValue("BdoPin") as string;
        }
        internal static void Restore(string command)
        {
            using(var key=Registry.CurrentUser.CreateSubKey(Key)) {
                if(command==null)key.DeleteValue("BdoPin",false);
                else key.SetValue("BdoPin",command,RegistryValueKind.String);
            }
        }
        internal static void Set(bool enabled,string executable)
        {
            using(var key=Registry.CurrentUser.CreateSubKey(Key)) {
                if(enabled)key.SetValue("BdoPin", "\"" + executable + "\" --tray",RegistryValueKind.String);
                else key.DeleteValue("BdoPin",false);
            }
        }
    }
}
