using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using CCSwitchUpdater;
class UiSmoke {
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  using(var f=new MainForm(args[0])) {
   f.StartPosition=FormStartPosition.Manual; f.Location=new Point(-30000,-30000); f.ShowInTaskbar=false; f.Show(); Application.DoEvents(); f.PerformLayout();
   if(f.Text.IndexOf("更新助手")<0) throw new Exception("Missing title");
   if(args.Length>2 && args[2]=="--check") {
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    var check=(Button)typeof(MainForm).GetField("check",flags).GetValue(f);
    if(!check.Enabled) throw new Exception("Check disabled on startup");
    check.PerformClick(); DateTime deadline=DateTime.UtcNow.AddSeconds(60);
    while((bool)typeof(MainForm).GetField("busy",flags).GetValue(f)) { Application.DoEvents(); Thread.Sleep(20); if(DateTime.UtcNow>deadline) throw new Exception("UI check did not finish"); }
    var latest=(Release)typeof(MainForm).GetField("latest",flags).GetValue(f);
    if(latest==null) throw new Exception("UI check failed: "+((Label)typeof(MainForm).GetField("status",flags).GetValue(f)).Text);
    var current=(Version)typeof(MainForm).GetField("current",flags).GetValue(f);
    var update=(Button)typeof(MainForm).GetField("update",flags).GetValue(f);
    if(latest.Version>=current && !update.Enabled) throw new Exception("Install disabled after real check");
    if(latest.Version==current && !update.Text.Contains("重新安装")) throw new Exception("Same version did not expose reinstall");
    Console.WriteLine("PASS real UI check: "+latest.Tag+" / "+update.Text+" / enabled="+update.Enabled);
   }
   using(var image=new Bitmap(f.Width,f.Height)) { f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height)); image.Save(args[1]); }
   Console.WriteLine("PASS form constructed and rendered without starting updates: " + args[1]);
  }
  return 0;
 }
}


