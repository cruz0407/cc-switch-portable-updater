using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CCSwitchUpdater;
[assembly:AssemblyVersion("4.0.20.0")]
[assembly:AssemblyFileVersion("4.0.20.0")]
class AutoRoutesUiTests {
 static int api,web;static BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 class Fake:HttpMessageHandler {
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
   await Task.Yield();
   token.ThrowIfCancellationRequested();var r=new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request};
   if(request.RequestUri.Host=="api.github.com"){api++;r.StatusCode=HttpStatusCode.Forbidden;r.Content=new StringContent("{}");r.Headers.Add("X-RateLimit-Remaining","0");r.Headers.Add("Retry-After","3600");}
   else{web++;if(request.RequestUri.AbsolutePath.EndsWith("latest")){r.StatusCode=HttpStatusCode.Redirect;r.Headers.Location=new Uri("/farion1231/cc-switch/releases/tag/v4.0.21",UriKind.Relative);r.Content=new StringContent("");}else{r.Content=new StringContent(request.RequestUri.AbsolutePath.Contains("expanded_assets")?"<li><a href=\"/farion1231/cc-switch/releases/download/v4.0.21/CC-Switch-v4.0.21-Windows-Portable.zip\">zip</a>sha256:6291de97f030e965fabd361418c349f5c0b390e46d55b5fa806cf967f5201a38</li>":"release");}}
   return r;
  }
 }
 static T Value<T>(MainForm f,string field){return (T)typeof(MainForm).GetField(field,Flags).GetValue(f);}
 static void Assert(bool b,string why){if(!b)throw new Exception(why);}
 static void Wait(MainForm f){var time=Stopwatch.StartNew();while(Value<bool>(f,"busy")){Application.DoEvents();Thread.Sleep(10);if(time.ElapsedMilliseconds>5000)throw new Exception("UI timeout");}Application.DoEvents();}
 [STAThread]static int Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{
  string root=Path.Combine(Path.GetTempPath(),"ccswitch-auto-routes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string exe=Path.Combine(root,"cc-switch.exe");File.Copy(Assembly.GetExecutingAssembly().Location,exe);using(var stream=File.Open(exe,FileMode.Open,FileAccess.ReadWrite))using(var reader=new BinaryReader(stream)){stream.Position=0x3c;int offset=reader.ReadInt32();stream.Position=offset+4;stream.WriteByte(0x64);stream.WriteByte(0x86);}File.WriteAllText(Path.Combine(root,"portable.ini"),"portable=true");
  using(var form=new MainForm(root,null,true,settings=>new Fake())){
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;
   int stage=0;Exception failure=null;var clock=Stopwatch.StartNew();
   using(var timer=new System.Windows.Forms.Timer{Interval=40}){
    timer.Tick+=delegate{
     try {
      if(clock.ElapsedMilliseconds>10000)throw new Exception("UI timeout: "+Value<TextBox>(form,"logBox").Text);
      if(Value<bool>(form,"busy") || Value<Release>(form,"latest")==null)return;
      if(stage==0){
       Assert(api==1 && web==3,"wrong initial route counts: api="+api+" web="+web);
       Assert(Value<Label>(form,"latestValue").Text=="4.0.21","latest version not shown");
       Assert(Value<Button>(form,"update").Enabled,"update remains disabled after API failure");
       Assert(Value<Release>(form,"latest").Source.Contains("网页"),"wrong metadata source");
       Console.WriteLine("PASS real MainForm auto-check: API 403 -> official web -> update enabled");
       stage=1;Value<Button>(form,"check").PerformClick();return;
      }
      Assert(api==1 && web==6,"manual retry hit cooling-down API or skipped webpage");
      Assert(Value<Button>(form,"update").Enabled,"update disabled during API cooldown");
      var preview=Value<WebBrowser>(form,"notesBrowser");if(preview.ReadyState!=WebBrowserReadyState.Complete)return;
      Console.WriteLine("PASS real MainForm manual recheck during API cooldown still enables update");timer.Stop();form.BeginInvoke(new Action(form.Close));
     }catch(Exception error){failure=error;timer.Stop();form.BeginInvoke(new Action(form.Close));}
    };
    timer.Start();Application.Run(form);if(failure!=null)throw failure;Assert(stage==1,"no completed automatic check");
   }
  }
  return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e.ToString());return 1;}}
}
