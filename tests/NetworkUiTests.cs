using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CCSwitchUpdater;
[assembly:AssemblyVersion("3.20.1.0")]
[assembly:AssemblyFileVersion("3.20.1.0")]
class NetworkUiTests {
 [DllImport("user32.dll")]static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Assert(bool yes,string why){if(!yes)throw new Exception(why);}
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,Flags).GetValue(value);}
 static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();}
 static void Snapshot(Form form,string path){using(var bitmap=new Bitmap(form.Width,form.Height)){using(var g=Graphics.FromImage(bitmap)){var dc=g.GetHdc();try{if(!PrintWindow(form.Handle,dc,0))throw new Exception("snapshot failed");}finally{g.ReleaseHdc(dc);}}bitmap.Save(path);}}
 static void Geometry(Control parent){foreach(Control child in parent.Controls){if(!child.Visible)continue;if(!(child is TabPage)){Assert(child.Left>=-2 && child.Top>=-2 && child.Right<=parent.ClientSize.Width+2 && child.Bottom<=parent.ClientSize.Height+2,"outside: "+child.GetType().Name+" "+child.Text);if(child is Label)Assert(child.Height+2>=child.GetPreferredSize(new Size(child.Width,0)).Height,"text clipped: "+child.Text);}Geometry(child);}}
 [STAThread]static int Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{
  using(var f=new NetworkSettingsForm(new NetworkSettings())){Show(f);Geometry(f);var mode=Field<ComboBox>(f,"mode");var addr=Field<TextBox>(f,"address");Assert(!addr.Enabled,"system shows editable proxy");mode.SelectedIndex=1;Assert(!addr.Enabled,"direct shows editable proxy");mode.SelectedIndex=2;Assert(addr.Enabled,"custom hides proxy field");addr.Text="http://127.0.0.1:7890";f.Size=f.MinimumSize;Application.DoEvents();Geometry(f);Snapshot(f,Path.Combine(args[0],"network-dialog.png"));((Button)f.AcceptButton).PerformClick();Assert(f.Selected.Mode=="custom" && f.Selected.ProxyAddress=="http://127.0.0.1:7890","save lost proxy choice");Console.WriteLine("PASS system/direct/custom controls, minimum dialog and save selection");}
  string dir=Path.Combine(Path.GetTempPath(),"ccswitch-network-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string exe=Path.Combine(dir,"cc-switch.exe");File.Copy(Assembly.GetExecutingAssembly().Location,exe);using(var s=File.Open(exe,FileMode.Open,FileAccess.ReadWrite))using(var r=new BinaryReader(s)){s.Position=0x3c;int offset=r.ReadInt32();s.Position=offset+4;s.WriteByte(0x64);s.WriteByte(0x86);}File.WriteAllText(Path.Combine(dir,"portable.ini"),"portable=true");
  int calls=0;var retry=DateTimeOffset.UtcNow.AddMinutes(10);
  using(var f=new MainForm(dir,delegate(string arch,CancellationToken token){calls++;var t=new TaskCompletionSource<Release>();t.SetException(new GitHubNetworkException("primary-rate-limit","GitHub API 配额已耗尽（剩余 0 / 60），HTTP 403。"+GitHubFailure.WaitMessage(retry),retry));return t.Task;})){Show(f);Assert(!Field<bool>(f,"busy"),"check did not finish");Assert(Field<Label>(f,"status").Text.Contains("0 / 60"),"error detail hidden");Assert(Field<LinkLabel>(f,"networkLink").Enabled,"settings inaccessible after 403");Field<Button>(f,"check").PerformClick();Application.DoEvents();Assert(calls==1,"UI retried during cooldown");Assert(Field<Label>(f,"status").Text.Contains("未发送新请求"),"wait not explained");f.Size=f.MinimumSize;Application.DoEvents();Geometry(f);Snapshot(f,Path.Combine(args[0],"network-error.png"));Console.WriteLine("PASS 403 details, retry suppression, settings accessible and long-message geometry");}
  Console.WriteLine("RESULT 2 network UI scenarios passed");return 0;
 }catch(Exception e){Console.Error.WriteLine("FAIL "+e.GetType().Name+": "+e.Message);return 1;}}
}
