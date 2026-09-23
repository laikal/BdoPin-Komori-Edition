using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
namespace BdoPin.Localization
{
    internal sealed class Language
    {
        private readonly Dictionary<string,string> words=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        internal readonly string Code;
        internal Language(string preference,string root)
        {
            Code=preference=="ko"||preference=="en"?preference:CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="ko"?"ko":"en";
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BdoPin.English"))
            using(var reader=new StreamReader(stream))Merge(reader.ReadToEnd());
            Load(Path.Combine(root,"lang","en.lng")); if(Code!="en")Load(Path.Combine(root,"lang",Code+".lng"));
        }
        private void Load(string path) { try { if(File.Exists(path)&&new FileInfo(path).Length<=1048576)Merge(File.ReadAllText(path)); } catch(IOException) {} catch(UnauthorizedAccessException) {} }
        private void Merge(string text)
        {
            string section="";
            foreach(var raw in text.TrimStart('\uFEFF').Split('\n')) {
                string s=raw.Trim();
                if(s.StartsWith(";")||s.StartsWith("#"))continue;
                if(s.StartsWith("[")&&s.EndsWith("]"))section=s.Substring(1,s.Length-2);
                else { int eq=s.IndexOf('='); if(eq>0)words[section+"."+s.Substring(0,eq).Trim()]=s.Substring(eq+1).Trim().Replace("\\n",Environment.NewLine); }
            }
        }
        internal string this[string key] { get { string value;return words.TryGetValue(key,out value)?value:key; } }
    }
}
