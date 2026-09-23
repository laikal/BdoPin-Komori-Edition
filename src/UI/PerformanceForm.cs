using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BdoPin.Models;
using BdoPin.Services;
using BdoPin.Localization;
namespace BdoPin.UI
{
    internal sealed class PerformanceForm : Form
    {
        private readonly Timer timer=new Timer { Interval=1000 };
        private PerformanceSampler sampler;
        private readonly Func<int[]> selected;
        private readonly Topology topology;
        private readonly Language lang;
        private readonly Label status=new Label(),gpuName=new Label(),heading=new Label(),driveName=new Label();
        private readonly CheckBox showAll=new CheckBox();
        private readonly Panel list=new Panel { AutoScroll=true };
        private readonly List<Metric> rows=new List<Metric>();
        private readonly Metric cpu,gpu,vram,read,write;
        private double readPeak=1000000,writePeak=1000000;
        private string lastDrive;
        private string rowKey="";
        private Dictionary<string,double> lastLogical=new Dictionary<string,double>();
        private bool resourcesDisposed;
        private readonly ToolTip tip=new ToolTip();
        private sealed class Metric
        {
            private readonly int designY;
            internal Label Name=new Label(),Value=new Label(); internal ProgressBar Bar=new ProgressBar();
            internal string Key;
            internal Metric(Control parent,string text,int y) {
                designY=y;
                Name.Text=text; Name.SetBounds(8,y,60,19);
                Bar.SetBounds(72,y+4,105,9); Bar.Style=ProgressBarStyle.Continuous;
                Value.SetBounds(184,y,200,19); Value.AutoEllipsis=true;
                parent.Controls.AddRange(new Control[]{Name,Bar,Value});
            }
            internal void Set(double? value,string text=null) { Bar.Value=(int)PerformanceSampler.Clamp(value??0); Value.Text=text??(value.HasValue?value.Value.ToString("0")+"%":"N/A"); }
            internal void Layout(int width,float scale,bool wide=false) {
                Func<int,int> px=n=>(int)Math.Round(n*scale);
                int textWidth=px(wide?205:60); int barWidth=Math.Max(px(40),width-px(90)-textWidth);
                Name.SetBounds(px(8),px(designY),px(60),px(19));
                Bar.SetBounds(px(72),px(designY+4),barWidth,px(9));
                Value.SetBounds(Bar.Right+px(7),px(designY),Math.Max(px(20),width-Bar.Right-px(15)),px(19));
            }
            internal void Remove(Control parent) { parent.Controls.Remove(Name); parent.Controls.Remove(Bar); parent.Controls.Remove(Value); Name.Dispose(); Bar.Dispose(); Value.Dispose(); }
        }
        internal PerformanceForm(Language language,Topology info,Func<int[]> selection)
        {
            lang=language; topology=info; selected=selection;
            Text=lang["Monitor.Title"]; Font=SystemFonts.MessageBoxFont;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BdoPin.Icon"))
            using(var original=new Icon(stream))Icon=(Icon)original.Clone();
            AutoScaleMode=AutoScaleMode.Dpi; AutoScaleDimensions=new SizeF(96,96);
            ClientSize=new Size(410,Math.Max(300,Math.Min(430,240+20*selection().Length)));
            MinimumSize=new Size(360,330); StartPosition=FormStartPosition.CenterParent;
            status.SetBounds(8,7,390,20); status.AutoEllipsis=true; Controls.Add(status);
            cpu=new Metric(this,"CPU",32); gpu=new Metric(this,"GPU",54); vram=new Metric(this,"VRAM",76);
            read=new Metric(this,lang["Monitor.DiskRead"],98);write=new Metric(this,lang["Monitor.DiskWrite"],120);
            tip.SetToolTip(read.Name,lang["Monitor.DiskScope"]);tip.SetToolTip(write.Name,lang["Monitor.DiskScope"]);
            tip.SetToolTip(cpu.Name,lang["Monitor.ProcessCPU"]); tip.SetToolTip(gpu.Name,lang["Monitor.GPU"]); tip.SetToolTip(vram.Name,lang["Monitor.VRAM"]);
            gpuName.SetBounds(8,98,390,20); gpuName.AutoEllipsis=true; Controls.Add(gpuName);
            driveName.AutoEllipsis=true;Controls.Add(driveName);
            showAll.Text=lang["Monitor.ShowAll"]; showAll.SetBounds(8,120,390,22); Controls.Add(showAll);
            heading.Text=lang["Monitor.CompactLogical"]; heading.SetBounds(8,144,390,20); Controls.Add(heading);
            list.SetBounds(0,166,410,224); Controls.Add(list);
            showAll.CheckedChanged+=(s,e)=> { rowKey=""; RenderRows(); };
            Resize+=(s,e)=>LayoutMetrics();
            timer.Tick+=(s,e)=>UpdateSample();
            Shown+=(s,e)=> {
                try { sampler=new PerformanceSampler(); RenderRows(); UpdateSample(); timer.Start(); }
                catch(Exception) { status.Text=lang["Monitor.Unavailable"]; }
            };
            LayoutMetrics();
        }
        private void LayoutMetrics()
        {
            float scale=DeviceDpi/96f;Func<int,int> px=n=>(int)Math.Round(n*scale);
            status.SetBounds(px(8),px(7),Math.Max(1,ClientSize.Width-px(16)),px(20));
            driveName.SetBounds(px(8),px(142),status.Width,px(20));
            gpuName.SetBounds(px(8),px(164),status.Width,px(20));
            showAll.SetBounds(px(8),px(186),status.Width,px(22));
            heading.SetBounds(px(8),px(210),status.Width,px(20));
            list.SetBounds(0,px(232),ClientSize.Width,Math.Max(1,ClientSize.Height-px(232)));
            cpu.Layout(ClientSize.Width,scale);gpu.Layout(ClientSize.Width,scale);vram.Layout(ClientSize.Width,scale,true);
            read.Layout(ClientSize.Width,scale);write.Layout(ClientSize.Width,scale);
            // Throughput text needs more room than the CPU percentage column.
            foreach(var metric in new[]{read,write}) {
                metric.Name.Width=px(88);metric.Bar.Left=px(100);
                metric.Bar.Width=Math.Max(px(40),ClientSize.Width-px(220));
                metric.Value.Left=metric.Bar.Right+px(7);metric.Value.Width=ClientSize.Width-metric.Value.Left-px(8);
            }
            foreach(var row in rows)row.Layout(list.ClientSize.Width-px(20),scale);
            list.AutoScrollMinSize=new Size(0,px(rows.Count*20));
        }
        private void RenderRows()
        {
            int[] chosen=selected();
            var ids=topology.Cores.SelectMany(c=>c.Cpus.Select(n=>new { c.Group,Index=n }))
                .Where(x=>showAll.Checked||(x.Group==0&&chosen.Contains(x.Index))).OrderBy(x=>x.Group).ThenBy(x=>x.Index).ToList();
            string key=showAll.Checked+":"+string.Join(",",ids.Select(x=>x.Group+":"+x.Index))+":"+string.Join(",",chosen);
            if(key==rowKey)return; rowKey=key;
            foreach(var row in rows)row.Remove(list);rows.Clear();
            int y=0;
            foreach(var id in ids) {
                string text="CPU "+(topology.Groups>1?id.Group+":"+id.Index:id.Index.ToString());
                if(showAll.Checked&&id.Group==0&&chosen.Contains(id.Index))text+=" *";
                var row=new Metric(list,text,y) { Key=id.Group+","+id.Index };
                double previous;row.Set(lastLogical.TryGetValue(row.Key,out previous)?previous:(double?)null);
                rows.Add(row); y+=20;
            }
            list.AutoScrollPosition=Point.Empty;
            FitRows();LayoutMetrics();
        }
        internal static int DesiredHeight(int count,bool all)
        {
            int height=240+20*count;
            return all?Math.Max(300,height):Math.Max(300,Math.Min(430,height));
        }
        private void FitRows()
        {
            var work=Screen.FromControl(this).WorkingArea;
            int border=Height-ClientSize.Height;
            int desired=(int)Math.Ceiling(DesiredHeight(rows.Count,showAll.Checked)*DeviceDpi/96f)+border;
            int height=Math.Min(desired,work.Height);
            int top=Math.Max(work.Top,Math.Min(Top,work.Bottom-height));
            SuspendLayout();
            try { SetBounds(Left,top,Width,height); }
            finally { ResumeLayout(true); }
        }
        private void UpdateSample()
        {
            if(sampler==null)return;
            try {
                var s=sampler.Sample();lastLogical=s.Logical;RenderRows();
                status.Text=s.Targets==0?lang["Monitor.NotRunning"]:s.Targets>1?lang["Monitor.Multiple"]:"BlackDesert64.exe · PID "+s.Pid+(s.Denied?" · "+lang["Monitor.Denied"]:"");
                cpu.Set(s.Cpu);gpu.Set(s.Gpu.Usage);
                if(lastDrive!=s.Drive) { lastDrive=s.Drive;readPeak=writePeak=1000000; }
                readPeak=Math.Max(readPeak,s.DiskRead??0);writePeak=Math.Max(writePeak,s.DiskWrite??0);
                read.Set(s.DiskRead.HasValue?s.DiskRead/readPeak*100:null,DiskMetrics.Format(s.DiskRead));
                write.Set(s.DiskWrite.HasValue?s.DiskWrite/writePeak*100:null,DiskMetrics.Format(s.DiskWrite));
                driveName.Text=lang["Monitor.Drive"]+": "+(s.Drive??"N/A")+(s.DriveFallback?" · "+lang["Monitor.DriveFallback"]:"");
                tip.SetToolTip(driveName,lang["Monitor.DiskScope"]);
                tip.SetToolTip(read.Bar,lang["Monitor.DiskScale"]+" "+DiskMetrics.Format(readPeak));
                tip.SetToolTip(write.Bar,lang["Monitor.DiskScale"]+" "+DiskMetrics.Format(writePeak));
                gpuName.Text=s.Gpu.Name==null?lang["Monitor.UnknownGPU"]:"GPU: "+s.Gpu.Name;
                vram.Set(s.Gpu.Bytes.HasValue&&s.Gpu.Capacity>0?s.Gpu.Bytes.Value/s.Gpu.Capacity*100:(double?)null,
                    s.Gpu.Bytes.HasValue?(s.Gpu.Bytes.Value/1073741824).ToString("0.00")+" / "+(s.Gpu.Capacity/1073741824.0).ToString("0.00")+" GiB ("+(s.Gpu.Bytes.Value/s.Gpu.Capacity*100).ToString("0")+"%)":"N/A");
                foreach(var row in rows) { double value; row.Set(s.Logical.TryGetValue(row.Key,out value)?value:(double?)null); }
            } catch(Exception) { status.Text=lang["Monitor.Unavailable"]; cpu.Set(null);gpu.Set(null);vram.Set(null);read.Set(null);write.Set(null);foreach(var row in rows)row.Set(null); }
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!resourcesDisposed) {
                resourcesDisposed=true;timer.Stop();timer.Dispose();if(sampler!=null)sampler.Dispose();tip.Dispose();if(Icon!=null)Icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
