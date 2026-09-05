using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CCSwitchUpdater;
[assembly: AssemblyVersion("3.20.1.0")]
[assembly: AssemblyFileVersion("3.20.1.0")]
class StartupTests {
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(MainForm form,string name) { return (T)typeof(MainForm).GetField(name,Private).GetValue(form); }
 static void Assert(bool yes,string why) { if(!yes) throw new Exception(why); }
 static void Pump(Func<bool> done) { var timer=Stopwatch.StartNew(); while(!done()) { Application.DoEvents(); Thread.Sleep(10); if(timer.ElapsedMilliseconds>10000) throw new Exception("UI condition timed out"); } Application.DoEvents(); }
 static string Fixture() {
  string root=Path.Combine(Path.GetTempPath(),"ccswitch-startup-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  string exe=Path.Combine(root,"cc-switch.exe"); File.Copy(Assembly.GetExecutingAssembly().Location,exe);
  using(var stream=File.Open(exe,FileMode.Open,FileAccess.ReadWrite)) using(var reader=new BinaryReader(stream)) { stream.Position=0x3c; int offset=reader.ReadInt32(); stream.Position=offset+4; stream.WriteByte(0x64); stream.WriteByte(0x86); }
  File.WriteAllText(Path.Combine(root,"portable.ini"),"portable=true"); return root;
 }
 static MainForm Create(string root,Func<string,CancellationToken,Task<Release>> fetch) {
  var ctor=typeof(MainForm).GetConstructor(new[]{typeof(string),typeof(Func<string,CancellationToken,Task<Release>>)});
  if(ctor==null) throw new Exception("Missing injectable release-fetch boundary for deterministic startup tests");
  return (MainForm)ctor.Invoke(new object[]{root,fetch});
 }
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  try {
   int calls=0; var pending=new TaskCompletionSource<Release>(); string root=Fixture(); CancellationToken token=default(CancellationToken);
   using(var form=Create(root,delegate(string arch,CancellationToken ct) { calls++; token=ct; Assert(arch=="x64","wrong startup architecture"); return pending.Task; })) {
    form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-30000,-30000); form.ShowInTaskbar=false;
    form.Show(); Pump(delegate{return calls==1;}); Assert(Field<bool>(form,"busy"),"startup should be checking"); Assert(!Field<Button>(form,"check").Enabled,"manual check enabled while busy");
    Console.WriteLine("PASS startup begins exactly one async check without clicking");
    var release=new Release { Version=new Version(3,20,1,0),Tag="v3.20.1",Notes="# Release\r\n\r\n> A **bold** note.\r\n\r\n- Keep `portable.ini`\r\n- [Details](https://github.com/example/a_b_c)" };
    pending.SetResult(release); Pump(delegate{return !Field<bool>(form,"busy");});
    Assert(Field<Button>(form,"update").Enabled,"same version repair unavailable after startup");
    var browser=Field<WebBrowser>(form,"notesBrowser");
    Pump(delegate{return browser.ReadyState==WebBrowserReadyState.Complete && browser.Document!=null && browser.Document.GetElementsByTagName("h1").Count==1;});
    Assert(browser.Document.GetElementsByTagName("strong").Count==1,"bold markup not rendered"); Assert(browser.Document.GetElementsByTagName("li").Count==2,"list not rendered");
    Assert(!browser.Document.Body.InnerText.Contains("**"),"raw Markdown displayed");
    Assert(browser.Document.GetElementsByTagName("a")[0].GetAttribute("href")=="https://github.com/example/a_b_c","URL corrupted");
    Console.WriteLine("PASS auto-check renders headings / bold / lists / links in real HTML DOM");
    form.Hide(); form.Show(); Application.DoEvents(); Assert(calls==1,"showing window repeats auto-check"); Console.WriteLine("PASS repeated Show does not repeat automatic check");
    Field<Button>(form,"check").PerformClick(); Pump(delegate{return calls==2 && !Field<bool>(form,"busy");}); Console.WriteLine("PASS manual recheck still works");
    if(args.Length>0) { using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[0]); } }
   }
   int failures=0;
   using(var form=Create(root,delegate(string arch,CancellationToken ct) { failures++; var source=new TaskCompletionSource<Release>(); source.SetException(new IOException("Offline test")); return source.Task; })) {
    form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-30000,-30000); form.ShowInTaskbar=false; form.Show();
    Pump(delegate{return failures==1 && !Field<bool>(form,"busy");}); Assert(Field<Button>(form,"check").Enabled,"cannot retry failed automatic check"); Assert(Field<Label>(form,"status").Text.Contains("Offline test"),"failure not shown");
    Console.WriteLine("PASS offline startup reports error and restores retry button");
   }
   var wait=new TaskCompletionSource<Release>(); var closing=Create(root,delegate(string arch,CancellationToken ct){token=ct;return wait.Task;});
   closing.StartPosition=FormStartPosition.Manual; closing.Location=new Point(-30000,-30000); closing.ShowInTaskbar=false; closing.Show(); Pump(delegate{return Field<bool>(closing,"busy");}); closing.Dispose();
   Assert(token.IsCancellationRequested,"disposing form must cancel in-flight check"); wait.SetCanceled(); Application.DoEvents(); Console.WriteLine("PASS dispose cancels background work safely");
   Console.WriteLine("RESULT 6 startup / Markdown UI scenarios passed"); return 0;
  } catch(Exception error) { Console.Error.WriteLine("FAIL "+error.GetType().FullName+": "+error.Message); return 1; }
 }
}
