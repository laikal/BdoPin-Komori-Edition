using System;
using System.IO;
using System.Text;
using BdoPin.Core;
namespace BdoPin.Logging
{
    internal static class AppLog
    {
        private static readonly object gate = new object();
        internal static void Write(string message)
        {
            // Callers use event codes / CPU / PID only, never full paths or exception messages.
            lock (gate) try {
                var path = Path.Combine(AppPaths.Root, "BdoPin.log");
                var line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine;
                if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(line) > 1048576) File.Delete(path);
                File.AppendAllText(path, line, new UTF8Encoding(false));
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
