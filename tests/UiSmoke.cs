using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CCSwitchUpdater;
class UiSmoke {
 [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(MainForm form,string name) { return (T)typeof(MainForm).GetField(name,Flags).GetValue(form); }
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  try {
   using(var f=new MainForm(args[0])) {
    f.StartPosition=FormStartPosition.Manual; f.Location=new Point(-30000,-30000); f.ShowInTaskbar=false; f.Show();
    var timer=Stopwatch.StartNew();
    // No PerformClick: only the production Shown handler starts the request.
    while(Field<Release>(f,"latest")==null || Field<bool>(f,"busy")) {
     Application.DoEvents(); Thread.Sleep(20);
     if(!Field<bool>(f,"busy") && Field<Release>(f,"latest")==null) throw new Exception("Automatic check failed: "+Field<Label>(f,"status").Text);
     if(timer.ElapsedMilliseconds>60000) throw new Exception("Automatic check timed out");
    }
    var browser=Field<WebBrowser>(f,"notesBrowser");
    while(browser.ReadyState!=WebBrowserReadyState.Complete || browser.Document==null || browser.Document.GetElementsByTagName("h1").Count==0) {
     Application.DoEvents(); Thread.Sleep(20); if(timer.ElapsedMilliseconds>65000) throw new Exception("Markdown DOM did not load");
    }
    var latest=Field<Release>(f,"latest"); var current=Field<Version>(f,"current");
    if(latest.Version>=current && !Field<Button>(f,"update").Enabled) throw new Exception("Repair/update action disabled");
    if(browser.Document.Body.InnerText.Contains("**")) throw new Exception("Raw bold markers displayed");
    Console.WriteLine("PASS real startup auto-check (no click): "+latest.Tag);
    Console.WriteLine("PASS real Markdown DOM: headings="+browser.Document.GetElementsByTagName("h1").Count+", bold="+browser.Document.GetElementsByTagName("strong").Count+", links="+browser.Document.GetElementsByTagName("a").Count);
    // Remove machine-specific paths from the documentation screenshot only.
    Field<TextBox>(f,"programPath").Text=@"D:\Apps\CC-Switch\cc-switch.exe";
    Field<TextBox>(f,"dataPath").Text=@"%USERPROFILE%\.cc-switch";
    f.Refresh(); Application.DoEvents();
    using(var bitmap=new Bitmap(f.Width,f.Height)) {
     using(var g=Graphics.FromImage(bitmap)) { IntPtr dc=g.GetHdc(); try { if(!PrintWindow(f.Handle,dc,0)) throw new Exception("PrintWindow failed"); } finally { g.ReleaseHdc(dc); } }
     bitmap.Save(args[1]);
    }
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[1]),"release-preview.html"),Markdown.ToDocument(latest.Notes));
    Console.WriteLine("PASS screenshot captured: "+args[1]);
   }
   return 0;
  } catch(Exception e) { Console.Error.WriteLine("FAIL "+e.GetType().FullName+": "+e.Message); return 1; }
 }
}
