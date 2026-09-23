using System;
using System.Diagnostics;
using System.Windows.Forms;
namespace BdoPin.Core
{
    internal static class AboutLinks
    {
        internal const string GitHub = "https://github.com/laikal/BdoPin-Komori-Edition";
        internal const string Chzzk = "https://chzzk.naver.com/688b22118a21cd70b53ad8b1d024b5d2";
        internal static ProcessStartInfo StartInfo(string url)
        {
            if (url != GitHub && url != Chzzk) throw new ArgumentException("Unsupported About link.");
            return new ProcessStartInfo(url) { UseShellExecute = true };
        }
        internal static void Open(IWin32Window owner, string url, bool korean)
        {
            try { using (var process = Process.Start(StartInfo(url))) { } }
            catch (Exception) {
                MessageBox.Show(owner, korean ? "기본 브라우저를 열 수 없습니다." : "Could not open the default browser.",
                    VersionInfo.Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
