using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: AssemblyProduct("CC Switch Portable Update Helper")]
namespace CCSwitchUpdater {
 static class Program {
  [STAThread] static void Main() {
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
   Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { MessageBox.Show("操作遇到问题：\r\n"+e.Exception.Message,"CC Switch 更新助手",MessageBoxButtons.OK,MessageBoxIcon.Error); };
   string directory=AppDomain.CurrentDomain.BaseDirectory, key;
   using(var hash=SHA256.Create()) key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant()))).Replace("-","").Substring(0,24);
   bool created;
   using(var mutex=new Mutex(true,"Local\\CCSwitchUpdateHelper-"+key,out created)) {
    if(!created) { MessageBox.Show("这个目录的更新助手已经打开，请切换到已有窗口。","CC Switch 更新助手"); return; }
    try { Application.Run(new MainForm(directory)); }
    catch(Exception e) { MessageBox.Show("无法启动更新助手：\r\n"+e.Message,"CC Switch 更新助手",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    finally { mutex.ReleaseMutex(); }
   }
  }
 }
}


