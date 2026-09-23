using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BdoPin.Core;
using BdoPin.Models;
using BdoPin.Services;
using BdoPin.Native;
using BdoPin.Logging;
using BdoPin.Localization;
namespace BdoPin.UI
{
    internal sealed class MainForm : Form
    {
        private Configuration config;
        private Topology topology;
        private readonly Language lang;
        private readonly ComboBox presets=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
        private readonly ComboBox priority=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
        private readonly CheckBox autoDetect=new CheckBox(),autoApply=new CheckBox(),trayOption=new CheckBox(),startup=new CheckBox();
        private readonly TextBox selected=new TextBox { ReadOnly=true },last=new TextBox { ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical };
        private readonly TextBox status=new TextBox { ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical };
        private readonly Label cpu=new Label(),watcherState=new Label();
        private readonly ListView cores=new ListView { View=View.Details,FullRowSelect=true,GridLines=true };
        private readonly Button apply=new Button(),remove=new Button();
        private readonly ProcessWatcher watcher=new ProcessWatcher();
        private readonly NotifyIcon tray=new NotifyIcon();
        private readonly HashSet<uint> pending=new HashSet<uint>();
        private CancellationTokenSource cancellation=new CancellationTokenSource();
        private PerformanceForm monitor;
        private bool loading=true,exiting,resourcesDisposed;
        private string configFailure;
        internal MainForm(bool applyOnStart=false,bool startInTray=false,bool testing=false,bool resetOnStart=false)
        {
            try { config=ConfigStore.Load(AppPaths.Ini); } catch(Exception ex) { config=new Configuration();configFailure=ex.Message; }
            lang=new Language(config.Language,AppPaths.Root);
            Text=VersionInfo.Product; Font=SystemFonts.MessageBoxFont; StartPosition=FormStartPosition.CenterScreen;
            AutoScaleMode=AutoScaleMode.Dpi; AutoScaleDimensions=new SizeF(96,96);
            ClientSize=new Size(560,730);MinimumSize=new Size(560,650);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BdoPin.Icon"))
            using(var original=new Icon(stream))Icon=(Icon)original.Clone();
            BuildMenu();
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=13,Padding=new Padding(12),AutoScroll=true };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int h in new[]{72,32,32,62,26,115,25,29,39,39,78,27})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            cpu.Dock=DockStyle.Fill; root.Controls.Add(cpu,0,0);
            root.Controls.Add(Field(lang["Main.Preset"],presets),0,1);
            priority.Items.AddRange(new object[]{lang["Priority.Normal"],lang["Priority.AboveNormal"],lang["Priority.High"]});
            root.Controls.Add(Field(lang["Main.ProcessPriority"],priority),0,2);
            var options=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2 };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            autoDetect.Text=lang["Main.AutoDetect"];autoApply.Text=lang["Main.AutoApply"];trayOption.Text=lang["Main.MinimizeToTray"];startup.Text=lang["Main.StartWithWindows"];
            foreach(var box in new[]{autoDetect,autoApply,trayOption,startup})box.Dock=DockStyle.Fill;
            options.Controls.Add(autoDetect,0,0);options.Controls.Add(autoApply,1,0);options.Controls.Add(trayOption,0,1);options.Controls.Add(startup,1,1);
            root.Controls.Add(options,0,3);
            root.Controls.Add(new Label { Text=lang["Main.CpuTopology"],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,4);
            cores.Columns.Add("Core",65);cores.Columns.Add("Group",60);cores.Columns.Add(lang["Main.LogicalCPUs"],210);cores.Columns.Add("Class",90);
            cores.Dock=DockStyle.Fill;root.Controls.Add(cores,0,5);
            root.Controls.Add(new Label { Text=lang["Main.SelectedCPUs"],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,6);
            selected.Dock=DockStyle.Fill;root.Controls.Add(selected,0,7);
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            apply.Text=lang["Main.ApplyNow"];remove.Text=lang["Main.UseAllCPUs"];
            foreach(var b in new[]{apply,remove}) { b.AutoSize=true;b.MinimumSize=new Size(125,29);buttons.Controls.Add(b); }
            root.Controls.Add(buttons,0,8);
            var secondary=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            var monitorButton=new Button { Text=lang["Monitor.Title"],AutoSize=true,MinimumSize=new Size(125,29) };
            monitorButton.Click+=(s,e)=>ShowMonitor();
            secondary.Controls.Add(monitorButton);root.Controls.Add(secondary,0,9);
            var lastBox=new GroupBox { Text=lang["LastApply.Title"],Dock=DockStyle.Fill };last.Dock=DockStyle.Fill;last.Text=lang["LastApply.None"];lastBox.Controls.Add(last);root.Controls.Add(lastBox,0,10);
            watcherState.Dock=DockStyle.Fill;watcherState.Text=lang["Watcher.Stopped"];root.Controls.Add(watcherState,0,11);
            status.Dock=DockStyle.Fill;root.Controls.Add(status,0,12);
            Controls.Add(root);root.BringToFront();
            tray.Icon=Icon;tray.Text=VersionInfo.Product;
            var trayMenu=new ContextMenuStrip();
            trayMenu.Items.Add(lang["Tray.Open"],null,(s,e)=>RestoreWindow());
            trayMenu.Items.Add(lang["Tray.ReloadPresets"],null,(s,e)=>Reload());
            trayMenu.Items.Add(lang["Tray.About"],null,(s,e)=>ShowAbout());
            trayMenu.Items.Add(lang["Tray.Exit"],null,(s,e)=>Exit());
            tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=(s,e)=>RestoreWindow();
            presets.SelectedIndexChanged+=(s,e)=>SaveOptions();priority.SelectedIndexChanged+=(s,e)=>SaveOptions();
            autoDetect.CheckedChanged+=(s,e)=>SaveOptions();autoApply.CheckedChanged+=(s,e)=>SaveOptions();trayOption.CheckedChanged+=(s,e)=>SaveOptions();
            startup.CheckedChanged+=(s,e)=>ChangeStartup();
            apply.Click+=async(s,e)=> { if(SaveOptions())await Scan(true,false); };
            remove.Click+=async(s,e)=>await Scan(true,true);
            Resize+=(s,e)=> { if(WindowState==FormWindowState.Minimized&&config.Tray)MinimizeTray(); };
            FormClosing+=OnClosingRequest;
            RefreshControls();
            if(!testing)Shown+=async(s,e)=> {
                NativeMethods.ChangeWindowMessageFilterEx(Handle,Instance.RestoreMessage,1,IntPtr.Zero);
                if(configFailure==null) { StartWatcher();await Scan(applyOnStart||resetOnStart,resetOnStart); }
                if(startInTray)MinimizeTray();
            };
        }
        private Control Field(string label,Control control)
        {
            var row=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            row.Controls.Add(new Label { Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,0);
            control.Dock=DockStyle.Fill;row.Controls.Add(control,1,0);return row;
        }
        private void BuildMenu()
        {
            var menu=new MenuStrip();
            var file=new ToolStripMenuItem(lang["Menu.File"]);
            file.DropDownItems.Add(lang["Menu.OpenIni"],null,(s,e)=> {
                try { using(var process=Process.Start(new ProcessStartInfo(AppPaths.Ini) { UseShellExecute=true })) {} }
                catch(Exception) { Status(lang["Error.OpenExternal"]); }
            });
            file.DropDownItems.Add(lang["Main.ReloadPresets"],null,(s,e)=>Reload());
            file.DropDownItems.Add(lang["Menu.Exit"],null,(s,e)=>Exit());
            var language=new ToolStripMenuItem(lang["Menu.Language"]);
            foreach(var code in new[]{"auto","ko","en"}) {
                string choice=code;
                var item=new ToolStripMenuItem(code=="auto"?lang["Menu.Auto"]:code=="ko"?"한국어":"English") { Checked=config.Language==code };
                item.Click+=(s,e)=> { config.Language=choice; if(SaveOptions())MessageBox.Show(this,lang["Status.LanguageRestart"],Text); };
                language.DropDownItems.Add(item);
            }
            var help=new ToolStripMenuItem(lang["Menu.Help"]);
            help.DropDownItems.Add(lang["Menu.HowTo"],null,(s,e)=>ShowHelp());
            help.DropDownItems.Add(lang["Menu.About"],null,(s,e)=>ShowAbout());
            menu.Items.AddRange(new ToolStripItem[]{file,language,help});MainMenuStrip=menu;Controls.Add(menu);
        }
        private void RefreshControls()
        {
            loading=true;
            presets.Items.Clear();presets.Items.Add(new Preset { Id="Auto",Name=lang["Menu.Auto"] });
            foreach(var p in config.Presets)presets.Items.Add(p);
            presets.SelectedItem=presets.Items.Cast<Preset>().FirstOrDefault(p=>ConfigStore.Comparer.Equals(p.Id,config.Selected));
            priority.SelectedIndex=Array.IndexOf(new[]{"Normal","AboveNormal","High"},config.Priority);
            autoDetect.Checked=config.AutoDetect;autoApply.Checked=config.AutoApply;trayOption.Checked=config.Tray;startup.Checked=StartupService.Registered(Application.ExecutablePath);
            cores.Items.Clear();topology=null;
            try {
                topology=CpuTopology.Read();
                string vendor=topology.Vendor=="AuthenticAMD"?"AMD":topology.Vendor=="GenuineIntel"?"Intel":lang["Main.Unknown"];
                cpu.Text=topology.Name+"\r\n"+lang["Main.Vendor"]+": "+vendor+
                    "\r\n"+lang["Main.Privilege"]+": "+lang[Instance.IsAdmin?"Main.Administrator":"Main.StandardUser"]+
                    "\r\n"+lang["Main.PhysicalCores"]+": "+topology.Cores.Count+" · "+lang["Main.LogicalCPUs"]+": "+topology.LogicalCount+
                    " · "+lang["Main.ProcessorGroups"]+": "+topology.Groups;
                for(int i=0;i<topology.Cores.Count;i++) { var c=topology.Cores[i];cores.Items.Add(new ListViewItem(new[]{i.ToString(),c.Group.ToString(),string.Join(", ",c.Cpus),c.Efficiency.ToString()})); }
            } catch(Exception ex) { Status(lang["Status.TopologyFailed"]+" "+ex.Message); }
            loading=false;UpdateSelection();
            if(configFailure!=null)Status(configFailure);
        }
        private void UpdateSelection()
        {
            apply.Enabled=remove.Enabled=false;
            try {
                if(configFailure!=null)throw new InvalidOperationException(configFailure);
                if(topology==null)throw new InvalidOperationException(lang["Status.TopologyFailed"]);
                remove.Enabled=topology.Groups==1;
                Selection s=CpuTopology.Build(topology,config);selected.Text=string.Join(", ",s.Cpus);apply.Enabled=true;
            } catch(Exception ex) { selected.Text=ex.Message; }
        }
        private bool SaveOptions()
        {
            if(loading)return true;
            if(configFailure!=null)return false;
            var preset=presets.SelectedItem as Preset;if(preset!=null)config.Selected=preset.Id;
            if(priority.SelectedIndex>=0)config.Priority=new[]{"Normal","AboveNormal","High"}[priority.SelectedIndex];
            config.AutoDetect=autoDetect.Checked;config.AutoApply=autoApply.Checked;config.Tray=trayOption.Checked;
            try { ConfigStore.Save(AppPaths.Ini,config);UpdateSelection();return true; }
            catch(Exception ex) { Status(ex.Message);AppLog.Write("Config save failed "+ex.HResult.ToString("X8"));return false; }
        }
        private void ChangeStartup()
        {
            if(loading)return;
            bool old=config.Startup;bool requested=startup.Checked;string priorCommand=null;bool captured=false;
            try {
                if(configFailure!=null)throw new InvalidOperationException(configFailure);
                priorCommand=StartupService.Current();captured=true;
                StartupService.Set(requested,Application.ExecutablePath);config.Startup=requested;
                if(!SaveOptions())throw new IOException("Could not save startup setting.");
                Status(lang[requested?"Status.StartupEnabled":"Status.StartupDisabled"]);
            } catch(Exception ex) {
                config.Startup=old;try { if(captured)StartupService.Restore(priorCommand); } catch(Exception) { Status(lang["Error.StartupRollback"]); }
                loading=true;startup.Checked=captured?string.Equals(priorCommand,"\""+Application.ExecutablePath+"\" --tray",StringComparison.OrdinalIgnoreCase):old;
                loading=false;Status(lang["Error.Startup"]+" "+ex.Message);
            }
        }
        private async void Reload()
        {
            cancellation.Cancel();cancellation.Dispose();cancellation=new CancellationTokenSource();pending.Clear();watcher.Dispose();
            try { config=ConfigStore.Load(AppPaths.Ini);configFailure=null; }
            catch(Exception ex) { configFailure=ex.Message; }
            RefreshControls();
            if(configFailure==null) { StartWatcher();await Scan(false,false); }
        }
        private void Post(Action action)
        {
            if(IsDisposed||!IsHandleCreated)return;
            try { BeginInvoke(new Action(()=> { if(!IsDisposed&&!exiting)action(); })); } catch(InvalidOperationException) { }
        }
        private void StartWatcher()
        {
            watcher.Start(config.Target,pid=>Post(()=>QueueApply(pid)),state=>Post(()=> {
                string[] parts=state.Split(' ');watcherState.Text=lang["Watcher."+parts[0]]+(parts.Length>1?" "+parts[1]:"");
                AppLog.Write("Watcher "+state);
            }));
        }
        private async void QueueApply(uint pid)
        {
            if(!config.AutoApply) { Status(lang["Status.AutoOff"]);return; }
            if(!pending.Add(pid))return;
            var token=cancellation.Token;
            try {
                await Task.Delay(250,token);
                if(!config.AutoApply||configFailure!=null||topology==null)return;
                var selection=CpuTopology.Build(topology,config);
                string target=config.Target;uint requestedPriority=config.PriorityValue;
                for(int attempt=1;attempt<=3;attempt++) {
                    token.ThrowIfCancellationRequested();
                    var result=AffinityService.Apply(pid,target,selection,requestedPriority);Report(pid,selection,requestedPriority,result,false);
                    if(!AffinityService.Retry(result,attempt))break;
                    Status(lang["Status.RetryScheduled"]);await Task.Delay(1000,token);
                    if(!config.AutoApply)break;
                }
            } catch(OperationCanceledException) {} catch(Exception ex) { Status(lang["Status.InvalidSelection"]+" "+ex.Message); }
            finally { if(!token.IsCancellationRequested)pending.Remove(pid); }
        }
        private Task Scan(bool force,bool reset)
        {
            try {
                if(force) { cancellation.Cancel();cancellation.Dispose();cancellation=new CancellationTokenSource();pending.Clear(); }
                if(configFailure!=null)throw new InvalidOperationException(configFailure);
                var pids=AffinityService.Find(config.Target);
                if(pids.Count==0) { Status(lang["Status.WaitingTarget"].Replace("{target}",config.Target));return Task.CompletedTask; }
                if(!force) { foreach(var pid in pids)QueueApply(pid);return Task.CompletedTask; }
                if(topology==null)throw new InvalidOperationException(lang["Status.TopologyFailed"]);
                var selection=CpuTopology.Build(topology,config,reset);
                bool denied=false;
                foreach(var pid in pids) {
                    var result=AffinityService.Apply(pid,config.Target,selection,reset?0x20u:config.PriorityValue);
                    Report(pid,selection,reset?0x20u:config.PriorityValue,result,reset);
                    denied|=result.Error==5;
                }
                // Only a manual action may request UAC, and only after an access-denied result.
                if(denied&&!Instance.IsAdmin)Elevate(reset);
            } catch(Exception ex) { Status(ex.Message); }
            return Task.CompletedTask;
        }
        private void Report(uint pid,Selection s,uint requestedPriority,ApplyResult result,bool reset)
        {
            string message=lang[result.Success?(reset?"Status.ResetApplied":"Status.AppliedPriority"):result.AffinityApplied?"Status.PriorityFailed":"Status.ApplyFailed"];
            if(result.Error==5)message=lang[Instance.IsAdmin?"Status.AdminDenied":"Status.AutoApplyAdminRequired"];
            last.Text=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" · PID "+pid+"\r\n"+s.Name+" · "+(requestedPriority==0x20?"Normal":requestedPriority==0x80?"High":"AboveNormal")+"\r\n"+message+
                (result.Error!=0?" · "+lang["LastApply.Error"]+" "+result.Error:"");
            Status(message);AppLog.Write("Apply PID="+pid+" mask="+s.Mask.ToString("X")+" priority="+requestedPriority+" stage="+result.Stage+" error="+result.Error);
        }
        private void Elevate(bool reset)
        {
            if(Instance.IsAdmin)return;
            if(!SaveOptions())return;
            try {
                var info=new ProcessStartInfo(Application.ExecutablePath,(reset?"--remove-settings ":"--apply-now ")+"--handoff-pid "+Process.GetCurrentProcess().Id) { UseShellExecute=true,Verb="runas" };
                using(var child=Process.Start(info)) { if(child==null)throw new InvalidOperationException(); }
                Exit();
            } catch(Win32Exception ex) { Status(lang[ex.NativeErrorCode==1223?"Status.ElevationCancelled":"Status.ElevationFailed"]); }
            catch(Exception) { Status(lang["Status.ElevationFailed"]); }
        }
        private void Status(string message)
        {
            if(status.TextLength>30000)status.Clear();
            status.AppendText((status.TextLength==0?"":Environment.NewLine)+message);
        }
        private int[] Selected()
        {
            try { return topology==null?new int[0]:CpuTopology.Build(topology,config).Cpus; } catch(Exception) { return new int[0]; }
        }
        private void ShowMonitor()
        {
            if(topology==null)return;
            if(monitor!=null&&!monitor.IsDisposed) { monitor.Show();monitor.Activate();return; }
            monitor=new PerformanceForm(lang,topology,Selected);monitor.Show(this);
        }
        private void ShowAbout() { using(var about=new AboutForm(lang.Code))about.ShowDialog(this); }
        private void ShowHelp()
        {
            using(var help=new Form { Text=lang["Help.Title"],StartPosition=FormStartPosition.CenterParent,Size=new Size(590,570),Icon=Icon }) {
                var text=new TextBox { Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Font=Font,
                    Text=string.Join(Environment.NewLine+Environment.NewLine,new[]{"What","Why","QuickStart","Permissions","Safety"}.Select(key=>lang["Help."+key])) };
                var close=new Button { Text=lang["Help.Close"],Dock=DockStyle.Bottom,Height=32,DialogResult=DialogResult.OK };
                help.Controls.Add(text);help.Controls.Add(close);help.AcceptButton=help.CancelButton=close;help.ShowDialog(this);
            }
        }
        private void MinimizeTray()
        {
            try { tray.Visible=true;Hide(); } catch(Exception) { Show();WindowState=FormWindowState.Normal;Status(lang["Error.Tray"]); }
        }
        internal void RestoreWindow() { Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false; }
        private void Exit() { exiting=true;Close(); }
        private void OnClosingRequest(object sender,FormClosingEventArgs e)
        {
            if(exiting||e.CloseReason!=CloseReason.UserClosing)return;
            using(var dialog=new Form { Text=Text,ClientSize=new Size(360,112),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false }) {
                dialog.Controls.Add(new Label { Text=lang["Close.Question"],Dock=DockStyle.Top,Height=45,Padding=new Padding(10) });
                var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=46,FlowDirection=FlowDirection.RightToLeft };
                var cancel=new Button { Text=lang["Close.Cancel"],DialogResult=DialogResult.Cancel,AutoSize=true };
                var exit=new Button { Text=lang["Close.Exit"],DialogResult=DialogResult.No,AutoSize=true };
                var hide=new Button { Text=lang["Close.Tray"],DialogResult=DialogResult.Yes,AutoSize=true };
                buttons.Controls.AddRange(new Control[]{cancel,exit,hide});dialog.Controls.Add(buttons);dialog.CancelButton=cancel;
                var answer=dialog.ShowDialog(this);
                e.Cancel=answer!=DialogResult.No;if(answer==DialogResult.Yes)MinimizeTray();else if(answer==DialogResult.No)exiting=true;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if((uint)m.Msg==Instance.RestoreMessage) { RestoreWindow();return; }
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!resourcesDisposed) {
                resourcesDisposed=true;exiting=true;
                cancellation.Cancel();watcher.Dispose();
                if(monitor!=null) { monitor.Close();monitor.Dispose(); }
                tray.Visible=false;tray.Dispose();cancellation.Dispose();
                if(Icon!=null)Icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
