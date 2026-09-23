using System;
using System.Globalization;
using System.IO;
namespace BdoPin.Localization
{
    // Same auto/ko/en selection and English fallback policy as native BdoPin.
    // Developer, product, license and link definitions stay owned by BdoPin.
    internal sealed class AboutText
    {
        internal readonly bool Korean;
        internal AboutText(string language)
        {
            Korean = string.Equals(language, "ko", StringComparison.OrdinalIgnoreCase) ||
                (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) &&
                 CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko");
        }
        internal string Pick(string ko, string en) { return Korean ? ko : en; }
        internal static string ReadPreference(string ini)
        {
            try {
                if (!File.Exists(ini) || new FileInfo(ini).Length > 1048576) return "auto";
                bool general = false;
                foreach (var line in File.ReadAllLines(ini)) {
                    var t = line.Trim();
                    if (t.StartsWith("[") && t.EndsWith("]")) general = t.Equals("[General]", StringComparison.OrdinalIgnoreCase);
                    else if (general && !t.StartsWith(";") && !t.StartsWith("#")) {
                        int eq = t.IndexOf('=');
                        if (eq > 0 && t.Substring(0, eq).Trim().Equals("Language", StringComparison.OrdinalIgnoreCase))
                            return t.Substring(eq + 1).Trim();
                    }
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return "auto";
        }
    }
}
