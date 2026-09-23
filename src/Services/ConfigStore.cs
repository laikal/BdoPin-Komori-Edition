using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BdoPin.Models;
namespace BdoPin.Services
{
    internal static class ConfigStore
    {
        internal static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
        private static string Get(Dictionary<string,string> data, string key, string fallback = "") { string value; return data.TryGetValue(key, out value) ? value : fallback; }
        private static bool Flag(Dictionary<string,string> data, string key, bool fallback)
        {
            string value = Get(data, key, fallback ? "1" : "0");
            if (value != "1" && value != "0") throw new FormatException("INI: " + key + " must be 0 or 1.");
            return value == "1";
        }
        internal static string Read(string path)
        {
            if (new FileInfo(path).Length > 1048576) throw new FormatException("INI exceeds 1 MiB.");
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }
        internal static Configuration Load(string path) { return Parse(Read(path)); }
        internal static int[] CpuList(string text)
        {
            var list = text.Split(',').Select(x => x.Trim()).ToArray();
            if (list.Any(x => !Regex.IsMatch(x, @"^[0-9]{1,2}$"))) throw new FormatException("CPU list must contain numbers 0 through 63.");
            int[] cpus = list.Select(int.Parse).ToArray();
            if (cpus.Any(x => x > 63) || cpus.Distinct().Count() != cpus.Length) throw new FormatException("Invalid or duplicate CPU number.");
            return cpus;
        }
        internal static Configuration Parse(string text)
        {
            var sections = new Dictionary<string,Dictionary<string,string>>(Comparer);
            var order = new List<string>(); Dictionary<string,string> current = null;
            foreach (string raw in text.TrimStart('\uFEFF').Split('\n')) {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[")) {
                    if (!line.EndsWith("]") || line.IndexOf(']') != line.Length-1) throw new FormatException("Invalid INI section.");
                    var section = line.Substring(1,line.Length-2).Trim();
                    if (section.Length == 0 || sections.ContainsKey(section)) throw new FormatException("Empty/duplicate INI section.");
                    current = new Dictionary<string,string>(Comparer); sections.Add(section,current); order.Add(section);
                } else {
                    int eq = line.IndexOf('=');
                    if (current == null || eq <= 0) throw new FormatException("Invalid INI entry.");
                    string key = line.Substring(0,eq).Trim();
                    if (key.Length == 0 || current.ContainsKey(key)) throw new FormatException("Empty/duplicate INI key.");
                    current.Add(key,line.Substring(eq+1).Trim());
                }
            }
            if (!sections.ContainsKey("General")) throw new FormatException("Missing [General].");
            var g = sections["General"]; var c = new Configuration();
            string[] allowed = { "TargetProcess","SelectedPreset","Language","ProcessPriority","Priority","AutoDetectVendor","AutoApply","MinimizeToTray","StartWithWindows","EnableLog" };
            if (g.Keys.Any(x => !allowed.Contains(x,Comparer))) throw new FormatException("Unknown General setting.");
            c.Target = Get(g,"TargetProcess",c.Target); c.Selected = Get(g,"SelectedPreset","Auto");
            if (c.Target.Length < 5 || c.Target.Length > 240 || !c.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) ||
                c.Target.IndexOfAny("\\/:*?\"<>|'\r\n".ToCharArray()) >= 0) throw new FormatException("TargetProcess must be a plain .exe filename.");
            c.Language = Get(g,"Language","auto").ToLowerInvariant(); if (c.Language != "ko" && c.Language != "en") c.Language = "auto";
            c.AutoDetect=Flag(g,"AutoDetectVendor",true); c.AutoApply=Flag(g,"AutoApply",true); c.Tray=Flag(g,"MinimizeToTray",true);
            c.Startup=Flag(g,"StartWithWindows",false); c.LegacyLog=Flag(g,"EnableLog",false);
            c.Priority=Get(g,"ProcessPriority",Get(g,"Priority","AboveNormal"));
            if (!g.ContainsKey("ProcessPriority") && new[]{"Unchanged","Idle","BelowNormal"}.Contains(c.Priority,Comparer)) c.Priority="AboveNormal";
            c.Priority = new[]{"Normal","AboveNormal","High"}.FirstOrDefault(x => Comparer.Equals(x,c.Priority));
            if (c.Priority == null) throw new FormatException("Invalid ProcessPriority.");
            foreach (var id in order.Where(x => !Comparer.Equals(x,"General"))) {
                if (Comparer.Equals(id,"Auto")) throw new FormatException("Auto is reserved.");
                var s = sections[id]; Mode mode;
                if (s.Keys.Any(x => !new[]{"Name","Vendor","Mode","CPUs"}.Contains(x,Comparer)) ||
                    !Enum.TryParse(Get(s,"Mode"),true,out mode) || !Enum.IsDefined(typeof(Mode),mode) ||
                    !new[]{"AuthenticAMD","GenuineIntel","Any"}.Contains(Get(s,"Vendor")) || Get(s,"Name") == "")
                    throw new FormatException("Invalid preset: " + id);
                c.Presets.Add(new Preset { Id=id, Name=Get(s,"Name"), Vendor=Get(s,"Vendor"), Mode=mode,
                    Cpus=mode==Mode.ExplicitCpuList ? CpuList(Get(s,"CPUs")) : new int[0] });
            }
            if (c.Presets.Count==0 || (!Comparer.Equals(c.Selected,"Auto") && !c.Presets.Any(x=>Comparer.Equals(x.Id,c.Selected))))
                throw new FormatException("SelectedPreset not found.");
            return c;
        }
        internal static void Save(string path, Configuration c)
        {
            string original = Read(path); Parse(original);
            var updates = new Dictionary<string,string>(Comparer) {
                {"TargetProcess",c.Target},{"SelectedPreset",c.Selected},{"Language",c.Language},{"ProcessPriority",c.Priority},
                {"AutoDetectVendor",c.AutoDetect?"1":"0"},{"AutoApply",c.AutoApply?"1":"0"},{"MinimizeToTray",c.Tray?"1":"0"},
                {"StartWithWindows",c.Startup?"1":"0"},{"EnableLog",c.LegacyLog?"1":"0"} };
            var output = new StringBuilder(); bool general=false;
            Action flush = () => { foreach(var kv in updates) output.AppendLine(kv.Key+"="+kv.Value); updates.Clear(); };
            foreach(var raw in original.Split('\n')) {
                string line=raw.TrimEnd('\r'), t=line.Trim();
                if(t.StartsWith("[")) { if(general)flush(); general=Comparer.Equals(t,"[General]"); }
                else if(general && !t.StartsWith(";") && !t.StartsWith("#")) {
                    int eq=t.IndexOf('=');
                    if(eq>0) { string key=t.Substring(0,eq).Trim();
                        if(Comparer.Equals(key,"Priority"))continue;
                        if(updates.ContainsKey(key)) { line=key+"="+updates[key]; updates.Remove(key); }
                    }
                }
                output.AppendLine(line);
            }
            if(general)flush(); Parse(output.ToString());
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { File.WriteAllText(temp,output.ToString(),new UTF8Encoding(false)); File.Replace(temp,path,null); }
            finally { if(File.Exists(temp))File.Delete(temp); }
        }
    }
}
