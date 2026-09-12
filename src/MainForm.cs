using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace CCSwitchUpdater {
 public sealed class MainForm : Form {
  readonly string directory, exe, work, backupRoot;
  readonly Func<string,CancellationToken,Task<Release>> fetchRelease;
  readonly bool useWebFallback;
  readonly NetworkCooldown networkCooldown=new NetworkCooldown();
  NetworkSettings networkSettings=new NetworkSettings();
  readonly LinkLabel networkLink=new LinkLabel();
  string dataDirectory, architecture, targetArchitecture;
  Version current;
  Release latest;
  bool busy, installing, automaticCheckStarted;
  CancellationTokenSource cancellation;
  readonly Label currentValue=new Label(), latestValue=new Label(), status=new Label(), paths=new Label();
  readonly TextBox logBox=new TextBox(), programPath=new TextBox(), dataPath=new TextBox();
  readonly WebBrowser notesBrowser=new WebBrowser();
  readonly ToolTip pathTips=new ToolTip { AutoPopDelay=15000 };
  readonly ProgressBar progress=new ProgressBar();
  readonly Button check=new Button(), update=new Button(), launch=new Button(), cancel=new Button();
  readonly Color blue=Color.FromArgb(38,83,179), ink=Color.FromArgb(31,45,65), muted=Color.FromArgb(100,113,133);
  public MainForm(string targetDirectory, Func<string,CancellationToken,Task<Release>> releaseFetcher = null) : this(targetDirectory, releaseFetcher, true) { }
  public MainForm(string targetDirectory, Func<string,CancellationToken,Task<Release>> releaseFetcher, bool enableWebFallback) {
   useWebFallback=enableWebFallback && releaseFetcher==null;
   fetchRelease=releaseFetcher ?? FetchOfficialReleaseAsync;
   directory=Path.GetFullPath(targetDirectory); exe=Path.Combine(directory,"cc-switch.exe");
   work=Path.Combine(directory,"update-helper-data");
   backupRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CCSwitchUpdateHelper","backups");
   SuspendLayout();
   // Every dimension below is authored at 96 DPI. Set the baseline before adding controls;
   // otherwise WinForms assumes runtime DPI and never scales fixed margins / minimum sizes.
   AutoScaleDimensions=new SizeF(96F,96F); AutoScaleMode=AutoScaleMode.Dpi;
   Text="CC Switch · 便携版更新助手"; ClientSize=new Size(820,650); MinimumSize=new Size(700,590);
   StartPosition=FormStartPosition.CenterScreen; Font=new Font("Microsoft YaHei UI",9F); BackColor=Color.White; ForeColor=ink;

   try { Icon=Icon.ExtractAssociatedIcon(exe); } catch(Exception) {}
   BuildLayout();
   ResumeLayout(true);
   Shown+=async delegate { if(automaticCheckStarted || IsDisposed || !check.Enabled) return; automaticCheckStarted=true; await CheckAsync(); };
   check.Click+=async delegate { await CheckAsync(); };
   update.Click+=async delegate { await UpdateAsync(); };
   launch.Click+=delegate { try { Launch(); } catch(Exception e) { Fail(e); } };
   cancel.Click+=delegate { if(cancellation!=null) cancellation.Cancel(); };
   FormClosing+=delegate(object sender,FormClosingEventArgs e) {
    if(installing) { e.Cancel=true; MessageBox.Show(this,"正在备份或替换，请等待完成后再关闭。","正在更新"); }
    else if(busy) { e.Cancel=true; if(cancellation!=null) cancellation.Cancel(); Status("正在取消，请稍候再关闭窗口。",false); }
   };
   try { networkSettings=NetworkSettings.Load(Path.Combine(work,"network.json")); UpdateNetworkLabel(); RefreshLocal(); ShowReadyStatus(); }
   catch(Exception e) { check.Enabled=false; update.Enabled=false; launch.Enabled=File.Exists(exe); Fail(e); }
  }
  void BuildLayout() {
   var layout=new TableLayoutPanel { Name="MainLayout",Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(22,18,22,16) };
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   for(int i=0;i<8;i++) layout.RowStyles.Add(new RowStyle(i==5 ? SizeType.Percent : SizeType.AutoSize,i==5 ? 100 : 0));
   Controls.Add(layout);

   var title=AutoTable(1); title.Margin=new Padding(0,0,0,14);
   var heading=TextLabel("CC Switch 更新助手"); heading.Name="Heading"; heading.Font=new Font("Microsoft YaHei UI",18F,FontStyle.Bold); heading.Margin=new Padding(0,0,0,5);
   var subtitle=TextLabel("便携版专用 · 下载、校验、备份，一次完成"); subtitle.Name="Subtitle"; subtitle.ForeColor=muted;
   title.Controls.Add(heading,0,0); title.Controls.Add(subtitle,0,1); layout.Controls.Add(title,0,0);

   var versions=AutoTable(2); versions.Name="Versions"; versions.BackColor=Color.FromArgb(243,246,251); versions.Padding=new Padding(16,10,16,10); versions.Margin=new Padding(0,0,0,12);
   versions.Controls.Add(VersionPanel("本机版本",currentValue),0,0); versions.Controls.Add(VersionPanel("官方稳定版",latestValue),1,0); layout.Controls.Add(versions,0,1);

   var details=AutoTable(1); details.Name="InstallationDetails"; details.Margin=new Padding(0,0,0,10);
   paths.Name="Architectures"; paths.AutoSize=true; paths.Dock=DockStyle.Fill; paths.ForeColor=muted; paths.Margin=new Padding(0,0,0,5); details.Controls.Add(paths,0,0);
   var locationRows=AutoTable(2); locationRows.ColumnStyles.Clear(); locationRows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); locationRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   AddPathRow(locationRows,"程序位置",programPath,0); AddPathRow(locationRows,"数据来源",dataPath,1);
   details.Controls.Add(locationRows,0,1); layout.Controls.Add(details,0,2);

   status.Name="Status"; status.AutoSize=true; status.Dock=DockStyle.Fill; status.TextAlign=ContentAlignment.MiddleLeft; status.Padding=new Padding(12,8,12,8); status.Margin=new Padding(0); layout.Controls.Add(status,0,3);
   progress.Name="Progress"; progress.Dock=DockStyle.Fill; progress.Height=6; progress.Margin=new Padding(0,8,0,10); layout.Controls.Add(progress,0,4);

   var tabs=new TabControl { Name="InformationTabs",Dock=DockStyle.Fill,Margin=new Padding(0) };
   var releaseTab=new TabPage("更新说明") { Padding=new Padding(10,8,10,8) }; var logTab=new TabPage("操作日志") { Padding=new Padding(10,8,10,8) };
   SetupText(logBox); SetupMarkdownBrowser(notesBrowser);
   releaseTab.Controls.Add(notesBrowser); logTab.Controls.Add(logBox); tabs.TabPages.Add(releaseTab); tabs.TabPages.Add(logTab); layout.Controls.Add(tabs,0,5);

   var actions=new FlowLayoutPanel { Name="Actions",Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Margin=new Padding(0,12,0,0) };
   SetupButton(check,"检查更新",104,false); SetupButton(update,"下载并更新",148,true); SetupButton(launch,"打开 CC Switch",142,false); SetupButton(cancel,"取消",76,false);
   update.Enabled=false; cancel.Enabled=false; actions.Controls.AddRange(new Control[]{check,update,launch,cancel}); layout.Controls.Add(actions,0,6);

   var footer=new FlowLayoutPanel { Name="Footer",Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=new Padding(0,5,0,0) };
   AddLink(footer,"打开日志目录",delegate { OpenDirectory(work); }); AddLink(footer,"查看备份",delegate { OpenDirectory(backupRoot); });
   networkLink.AutoSize=true; networkLink.LinkColor=blue; networkLink.Margin=new Padding(0,2,18,2); networkLink.Text="网络设置（系统代理）"; networkLink.LinkClicked+=delegate { ConfigureNetwork(); }; footer.Controls.Add(networkLink);
   AddLink(footer,"浏览器打开官方发布页",delegate { OpenOfficialRelease(); });
   var disclaimer=TextLabel("非官方辅助工具 · 不常驻后台"); disclaimer.ForeColor=muted; disclaimer.Margin=new Padding(4,2,0,2); footer.Controls.Add(disclaimer); layout.Controls.Add(footer,0,7);
  }
  TableLayoutPanel AutoTable(int columns) {
   var table=new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=columns,Margin=new Padding(0),Padding=new Padding(0) };
   for(int i=0;i<columns;i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F/columns));
   return table;
  }
  Label TextLabel(string text) { return new Label { Text=text,AutoSize=true,Dock=DockStyle.Fill,ForeColor=ink,Margin=new Padding(0),UseMnemonic=false }; }
  Control VersionPanel(string caption,Label value) {
   var panel=AutoTable(1);
   var label=TextLabel(caption); label.ForeColor=muted; label.Margin=new Padding(0,0,0,4); panel.Controls.Add(label,0,0);
   value.Text="—"; value.Font=new Font("Consolas",24F,FontStyle.Bold); value.ForeColor=blue; value.AutoSize=true; value.Dock=DockStyle.Fill; value.Margin=new Padding(0); panel.Controls.Add(value,0,1); return panel;
  }
  void AddPathRow(TableLayoutPanel rows,string caption,TextBox box,int row) {
   var label=TextLabel(caption); label.ForeColor=muted; label.TextAlign=ContentAlignment.MiddleLeft; label.Margin=new Padding(0,1,10,3);
   box.ReadOnly=true; box.BorderStyle=BorderStyle.None; box.BackColor=Color.White; box.ForeColor=muted; box.Dock=DockStyle.Fill; box.Margin=new Padding(0,1,0,3); box.AccessibleName=caption; box.WordWrap=false;
   rows.Controls.Add(label,0,row); rows.Controls.Add(box,1,row);
  }
  void SetupMarkdownBrowser(WebBrowser browser) {
   browser.Name="MarkdownPreview"; browser.Dock=DockStyle.Fill;
   browser.AllowWebBrowserDrop=false; browser.IsWebBrowserContextMenuEnabled=false;
   browser.ScriptErrorsSuppressed=true; browser.WebBrowserShortcutsEnabled=false;
   browser.NewWindow+=delegate(object sender,System.ComponentModel.CancelEventArgs e) { e.Cancel=true; };
   browser.Navigating+=delegate(object sender,WebBrowserNavigatingEventArgs e) {
    if(e.Url!=null && e.Url.AbsoluteUri=="about:blank") return;
    e.Cancel=true;
    if(e.Url!=null && Markdown.IsSafeLink(e.Url.AbsoluteUri)) {
     // Link navigation is only permitted after an explicit click and confirmation.
     if(MessageBox.Show(this,"在默认浏览器中打开此链接？\r\n\r\n"+e.Url.AbsoluteUri,"打开更新说明链接",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK) return;
     try { Process.Start(new ProcessStartInfo(e.Url.AbsoluteUri){UseShellExecute=true}); }
     catch(Exception ex) { Log("无法打开更新说明链接："+ex.Message); }
    }
   };
   browser.DocumentText=Markdown.ToDocument("正在准备检查官方稳定版。\n\n只检查更新，不会自动下载或安装。");
  }
  async Task<Release> FetchOfficialReleaseAsync(string architecture,CancellationToken token) {
   using(var client=new GitHubClient(networkSettings,null,networkCooldown)) return await client.GetLatestAsync(architecture,token);
  }
  void SetupText(TextBox box) { box.Multiline=true; box.ReadOnly=true; box.Dock=DockStyle.Fill; box.ScrollBars=ScrollBars.Vertical; box.BorderStyle=BorderStyle.None; box.BackColor=Color.White; box.ForeColor=ink; box.Font=new Font("Microsoft YaHei UI",9.5F); }
  void SetupButton(Button b,string text,int width,bool primary) { b.Text=text; b.AutoSize=true; b.AutoSizeMode=AutoSizeMode.GrowAndShrink; b.MinimumSize=new Size(width,36); b.Padding=new Padding(12,5,12,5); b.Margin=new Padding(0,0,8,5); b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderColor=Color.FromArgb(207,216,231); b.BackColor=primary?blue:Color.White; b.ForeColor=primary?Color.White:ink; b.Cursor=Cursors.Hand; if(primary) b.EnabledChanged+=delegate { b.BackColor=b.Enabled?blue:Color.FromArgb(235,239,245); b.ForeColor=b.Enabled?Color.White:muted; }; }
  void AddLink(FlowLayoutPanel panel,string text,Action action) { var link=new LinkLabel { Text=text,AutoSize=true,LinkColor=blue,Margin=new Padding(0,2,18,2) }; link.LinkClicked+=delegate { try { action(); } catch(Exception e) { Fail(e); } }; panel.Controls.Add(link); }
  protected override void Dispose(bool disposing) { if(disposing) { if(cancellation!=null) cancellation.Cancel(); pathTips.Dispose(); } base.Dispose(disposing); }
  void RefreshLocal() {
   Core.EnsureNoReparse(exe); Core.RequirePortable(directory); current=Core.ReadVersion(exe); architecture=Core.ReadArchitecture(exe);
   targetArchitecture=Core.ReadNativeArchitecture();
   string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   string testHome=Environment.GetEnvironmentVariable("CC_SWITCH_TEST_HOME");
   if(!String.IsNullOrWhiteSpace(testHome)) { if(!Path.IsPathRooted(testHome)) throw new InvalidDataException("CC_SWITCH_TEST_HOME 必须是绝对路径。"); home=testHome; }
   string store=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"com.ccswitch.desktop","app_paths.json");
   dataDirectory=Core.ResolveDataDirectory(home,Environment.GetEnvironmentVariable("HOME"),store);
   currentValue.Text=current.ToString(3); paths.Text="系统："+targetArchitecture+"   /   已装程序："+architecture+"   /   将下载："+targetArchitecture;
   programPath.Text=exe; programPath.Select(0,0); dataPath.Text=dataDirectory; dataPath.Select(0,0); pathTips.SetToolTip(programPath,exe); pathTips.SetToolTip(dataPath,dataDirectory);
  }
  void SetBusy(bool value) {
   busy=value; check.Enabled=!value; launch.Enabled=!value; networkLink.Enabled=!value;
   update.Enabled=!value && latest!=null && current!=null && latest.Version>=current;
   update.Text=latest!=null && current!=null && latest.Version>=current
    ? (architecture!=targetArchitecture ? "修复为 "+targetArchitecture : latest.Version==current ? "重新安装 / 修复" : "下载并更新") : "下载并更新";
   cancel.Enabled=value&&!installing;
  }
  void ShowReadyStatus() {
   if(architecture!=targetArchitecture) Status("架构不一致：程序 "+architecture+" / 系统 "+targetArchitecture+"。检查更新后可安装系统原生版本。",true);
   else Status("架构匹配（"+architecture+"）。检查官方稳定版，也可同版本重新安装。",false);
  }
  void ShowReleaseStatus() {
   if(latest.Version<current) { Status("本机版本高于官方稳定版。为保护数据库，不支持降级覆盖。",architecture!=targetArchitecture); return; }
   if(architecture!=targetArchitecture) { Status("架构不一致：程序 "+architecture+" / 系统 "+targetArchitecture+"。点击修复安装 "+targetArchitecture+" 版。",true); return; }
   Status(latest.Version==current ? "已是官方稳定版（"+architecture+"）。如需覆盖修复，点击“重新安装 / 修复”。" : "有可用更新，将安装 "+targetArchitecture+" 版。点击“下载并更新”开始。",false);
  }
  void Status(string message,bool error) { status.Text=message; status.BackColor=error?Color.FromArgb(255,238,235):Color.FromArgb(233,243,249); status.ForeColor=error?Color.FromArgb(154,49,36):ink; }
  void Log(string message) {
   string line=DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine;
   logBox.AppendText(line);
   try { Core.EnsureNoReparse(work); Directory.CreateDirectory(work); string file=Path.Combine(work,"update-"+DateTime.Now.ToString("yyyyMMdd")+".log"); Core.EnsureNoReparse(file); File.AppendAllText(file,line); }
   catch(IOException) {} catch(UnauthorizedAccessException) {} catch(InvalidDataException) {}
  }
  void Fail(Exception e) { string message=e is System.Net.Http.HttpRequestException ? GitHubFailure.TransportMessage(e) : e.Message; Status(message,true); Log("失败："+message); }
  async Task<Release> FetchReleaseWithFallbackAsync(string architecture, CancellationToken token) {
   GitHubNetworkException apiError=null;
   try { return await fetchRelease(architecture,token); }
   catch(GitHubNetworkException error) { apiError=error; }
   if(!useWebFallback) throw apiError;
   networkCooldown.Record(apiError);
   Log("API 暂不可用，切换官方 Releases 网页备用路径："+apiError.Message);
   Status("API 暂不可用，正在尝试官方网页备用路径…",false);
   return await GitHubWebFallback.GetLatestAsync(networkSettings,architecture,token);
  }
  async Task CheckAsync() {
   if(busy) return;
   automaticCheckStarted=true;
   latest=null; SetBusy(true); progress.Style=ProgressBarStyle.Marquee; Status("正在检查 GitHub 官方稳定版…",false); Log("检查官方稳定版。");
   cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(45));
   try {
    RefreshLocal();
    Log("连接方式："+networkSettings.Label+"；目标为官方 GitHub API。");
    networkCooldown.Check(DateTimeOffset.UtcNow);
    latest=await FetchReleaseWithFallbackAsync(targetArchitecture,cancellation.Token);
    if(IsDisposed || Disposing) return;
    latestValue.Text=latest.Version.ToString(3); notesBrowser.DocumentText=Markdown.ToDocument(latest.Notes);
    ShowReleaseStatus();
    Log("本机 "+current.ToString(3)+"（"+architecture+"） / 官方 "+latest.Tag+" / 系统及目标 "+targetArchitecture);
   } catch(OperationCanceledException) { if(!IsDisposed && !Disposing) { Status("检查已取消或超时。可检查系统代理后重试。",false); Log("检查取消或超时。"); } }
   catch(Exception e) { latest=null; var netError=e as GitHubNetworkException; if(netError!=null) networkCooldown.Record(netError); if(!IsDisposed && !Disposing) { latestValue.Text="未获取"; Fail(e); notesBrowser.DocumentText=Markdown.ToDocument("## 检查未完成\n\n可通过下方的网络设置选择系统代理、直连或自定义 HTTP 代理。\n\n也可以点击“浏览器打开官方发布页”，使用默认浏览器（例如 Chrome）查看官方版本和下载附件。\n\n不会绕过明确的限流等待，也不会关闭证书或 SHA-256 校验。"); } }
   finally { cancellation.Dispose(); cancellation=null; if(!IsDisposed && !Disposing) { progress.Style=ProgressBarStyle.Blocks; progress.Value=0; SetBusy(false); } }
  }
  static Process[] Running() { return Process.GetProcessesByName("cc-switch"); }
  static void EnsureStopped() {
   var processes=Running(); try { if(processes.Length!=0) throw new IOException("CC Switch 仍在运行。请从系统托盘选择退出（包括其他目录的实例），然后重试。"); } finally { foreach(var p in processes) p.Dispose(); }
  }
  async Task<bool> RequestExitAsync() {
   var processes=Running();
   try { foreach(var p in processes) { try { p.CloseMainWindow(); } catch(InvalidOperationException) {} } }
   finally { foreach(var p in processes) p.Dispose(); }
   for(int i=0;i<34;i++) { await Task.Delay(300); var p=Running(); int count=p.Length; foreach(var item in p) item.Dispose(); if(count==0) return true; }
   var result=MessageBox.Show(this,"CC Switch 在 10 秒内没有正常退出。\r\n\r\n点击“强制关闭并继续”会结束所有 cc-switch.exe 实例，未保存内容可能丢失。\r\n如果你不确定，请选择“取消”，助手不会结束进程。","CC Switch 未自动退出",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button3);
   if(result!=DialogResult.Yes) return false;
   var remaining=Running();
   try { foreach(var p in remaining) { try { if(!p.HasExited) p.Kill(); } catch(InvalidOperationException) {} } }
   finally { foreach(var p in remaining) p.Dispose(); }
   for(int i=0;i<20;i++) { await Task.Delay(250); var p=Running(); int count=p.Length; foreach(var item in p) item.Dispose(); if(count==0) return true; }
   throw new IOException("已请求强制关闭，但 CC Switch 仍在运行；为保护数据，已停止更新。");
  }
  async Task UpdateAsync() {
   if(busy || latest==null) return;
   string job=null; bool committed=false;
   SetBusy(true); cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(10));
   try {
    RefreshLocal(); if(latest.Version<current) { Status("不支持降级覆盖，请重新检查更新。",false); return; }
    Version originalVersion=current; string originalArchitecture=architecture, originalHash=Core.Sha256(exe);
    Core.EnsureNoReparse(work); Directory.CreateDirectory(work);
    string lockFile=Path.Combine(work,"update.lock"); Core.EnsureNoReparse(lockFile);
    using(var gate=new FileStream(lockFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
     job=Path.Combine(work,"download-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job); string zip=Path.Combine(job,"package.zip");
     Log("下载 "+latest.AssetName+"（"+(latest.Size/1048576.0).ToString("F1")+" MB）。"); Status("正在下载，CC Switch 暂时无需退出…",false);
     var report=new Progress<int>(delegate(int n) { progress.Value=n; Status("正在下载官方便携包… "+n+"%",false); });
     using(var client=new GitHubClient(networkSettings,null,networkCooldown)) await client.DownloadAsync(latest,zip,report,cancellation.Token);
     cancellation.Token.ThrowIfCancellationRequested(); Status("正在校验 SHA-256、程序版本和架构…",false); progress.Style=ProgressBarStyle.Marquee;
     string stage=await Task.Run(delegate { Core.VerifyPackage(zip,latest); return Core.ExtractPackage(zip,Path.Combine(job,"stage"),latest,targetArchitecture); });
     cancellation.Token.ThrowIfCancellationRequested(); Log("文件大小、SHA-256、包结构、版本和架构校验通过。");
     if(MessageBox.Show(this,"下载和校验已完成，准备安装官方便携版。\r\n当前："+current.ToString(3)+"（"+architecture+"） → 目标："+latest.Version.ToString(3)+"（"+targetArchitecture+"）\r\n"+(latest.Version==current ? "这是同版本覆盖修复，仍会先备份原程序和数据。\r\n" : "")+"\r\n继续后将请求正常退出 CC Switch，然后备份数据并替换主程序。\r\n如正在使用本地代理，请等待当前请求完成后再继续。\r\n\r\n数据来源："+dataDirectory+"\r\n备份目录："+backupRoot+"\r\n\r\n确认继续吗？","准备安全更新",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK) { Status("已取消安装，现有程序未改动。",false); return; }
     cancellation.CancelAfter(Timeout.Infinite); installing=true; cancel.Enabled=false;
     if(!await RequestExitAsync()) { Status("已取消安装，现有程序未改动。",false); return; }
     EnsureStopped();
     // Re-read paths/version after process exit so changed settings cannot silently select a stale backup path.
     RefreshLocal(); if(current!=originalVersion || architecture!=originalArchitecture || Core.Sha256(exe)!=originalHash) throw new InvalidDataException("确认期间当前程序已改变，请重新检查后再安装。");
     Status("正在备份并安全替换，请勿关闭窗口或重新打开 CC Switch…",false); Log("开始备份数据并原子替换程序。");
     string backup=await Task.Run(delegate { EnsureStopped(); return Core.Install(stage,directory,dataDirectory,backupRoot,EnsureStopped); });
     committed=true; RefreshLocal(); Log("程序更新完成。备份："+backup);
     installing=false;
     try { Launch(); Status("已安装 "+current.ToString(3)+"（"+architecture+"），已发送启动请求。",false); }
     catch(Exception e) { Status("更新已完成，但启动失败。可点击“打开 CC Switch”重试。",true); Log("更新成功，启动失败："+e.Message); }
    }
   } catch(OperationCanceledException) { Status("下载已取消或超时，现有程序未改动。",false); Log("下载取消或超时。"); }
   catch(Exception e) { Fail(e); if(committed) Log("主程序已经更新，请勿仅因后续错误就降级数据库。"); else Log("更新未提交；请查看备份目录和日志。助手未主动删除原程序。"); }
   finally {
    installing=false; if(cancellation!=null) cancellation.Dispose(); cancellation=null;
    progress.Style=ProgressBarStyle.Blocks; progress.Value=committed?100:0; SetBusy(false); CleanupJob(job);
   }
  }
  void CleanupJob(string job) {
   if(job==null) return;
   try {
    Core.EnsureNoReparse(job);
    foreach(string name in new[]{"package.zip","stage\\cc-switch.exe","stage\\portable.ini"}) { string path=Path.Combine(job,name); Core.EnsureNoReparse(path); if(File.Exists(path)) File.Delete(path); }
    string stage=Path.Combine(job,"stage"); if(Directory.Exists(stage)&&!Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage);
    if(Directory.Exists(job)&&!Directory.EnumerateFileSystemEntries(job).Any()) Directory.Delete(job);
   } catch(Exception e) { Log("临时下载未清理，可稍后手动删除："+e.Message); }
  }
  void Launch() {
   Core.EnsureNoReparse(exe);
   Process.Start(new ProcessStartInfo(exe) { WorkingDirectory=directory,UseShellExecute=true }); Log("已发送 CC Switch 启动请求。");
  }
  void UpdateNetworkLabel() { networkLink.Text="网络设置（"+networkSettings.Label+"）"; }
  void ConfigureNetwork() {
   if(busy) return;
   using(var dialog=new NetworkSettingsForm(networkSettings)) {
    if(dialog.ShowDialog(this)!=DialogResult.OK) return;
    try {
     dialog.Selected.Save(Path.Combine(work,"network.json")); networkSettings=dialog.Selected; UpdateNetworkLabel();
     latest=null; latestValue.Text="未获取"; RefreshLocal(); SetBusy(false);
     Status("已保存连接方式："+networkSettings.Label+"。点击检查更新；明确限流的等待时间不会被清除。",false);
     Log("网络连接方式已更改为："+networkSettings.Label+"。未修改 Windows / Chrome 设置。");
    } catch(Exception e) { Fail(e); }
   }
  }
  void OpenOfficialRelease() {
   Process.Start(new ProcessStartInfo("https://github.com/farion1231/cc-switch/releases/latest"){UseShellExecute=true});
  }
  void OpenDirectory(string path) {
   if(!Directory.Exists(path)) { MessageBox.Show(this,"暂时没有对应记录。首次检查或更新后会自动创建。","暂无记录"); return; }
   Process.Start(new ProcessStartInfo(path) { UseShellExecute=true });
  }
 }
}








