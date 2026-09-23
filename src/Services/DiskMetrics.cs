using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace BdoPin.Services
{
    internal static class DiskMetrics
    {
        // LogicalDisk is volume-wide throughput, not the game's per-process I/O.
        internal static string Drive(string path)
        {
            return !string.IsNullOrEmpty(path)&&Regex.IsMatch(path,@"^[a-zA-Z]:\\")
                ?path.Substring(0,2).ToUpperInvariant():null;
        }
        internal static double? Rate(IEnumerable<CounterSample> values,string drive)
        {
            var matches=values.Where(x=>string.Equals(x.Name,drive,StringComparison.OrdinalIgnoreCase)&&
                x.Value>=0&&!double.IsNaN(x.Value)&&!double.IsInfinity(x.Value)).ToList();
            return matches.Count==1?(double?)matches[0].Value:null;
        }
        internal static string Format(double? bytes)
        {
            if(!bytes.HasValue)return "N/A";
            double n=bytes.Value;
            foreach(var unit in new[]{"B/s","KB/s","MB/s","GB/s","TB/s"}) {
                if(n<1000||unit=="TB/s")return n.ToString(n<10?"0.00":"0.0")+" "+unit;
                n/=1000;
            }
            return "N/A";
        }
    }
}
