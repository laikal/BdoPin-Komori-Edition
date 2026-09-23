using System;
using System.IO;
using System.Reflection;
namespace BdoPin.Core
{
    internal static class AppPaths
    {
        internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        internal static string Ini { get { return Path.Combine(Root, "BdoPin.ini"); } }
        internal static void EnsureConfig()
        {
            if (File.Exists(Ini)) return;
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("BdoPin.Default.ini"))
            using (var output = new FileStream(Ini, FileMode.CreateNew, FileAccess.Write)) source.CopyTo(output);
        }
    }
}
