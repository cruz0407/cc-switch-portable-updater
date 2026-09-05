using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using CCSwitchUpdater;
class LayoutRegression {
 static int failures,checks;
 static IEnumerable<Control> Tree(Control c) { yield return c; foreach(Control child in c.Controls) foreach(Control item in Tree(child)) yield return item; }
 static void SimulateScale(MainForm form,float factor) {
  // Stress-test authored geometry and font sizes without changing the user's display settings.
  var fonts=Tree(form).Select(c=>new {Control=c,Font=c.Font}).ToArray();
  Size client=form.ClientSize, minimum=form.MinimumSize;
  form.AutoScaleMode=AutoScaleMode.None; form.SuspendLayout(); form.Scale(new SizeF(factor,factor));
  foreach(var item in fonts) item.Control.Font=new Font(item.Font.FontFamily,item.Font.SizeInPoints*factor,item.Font.Style);
  form.MinimumSize=new Size((int)(minimum.Width*factor),(int)(minimum.Height*factor));
  form.ClientSize=new Size((int)(client.Width*factor),(int)(client.Height*factor));
  form.ResumeLayout(true); Application.DoEvents();
 }
 static void Save(MainForm form,string path) { using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(path); } }
 static void Check(bool ok,string text) { checks++; if(!ok) { failures++; Console.WriteLine("FAIL "+text); } }
 static void Inspect(Control parent) {
  foreach(Control c in parent.Controls) {
   if(!c.Visible) continue;
   if(!(c is TabPage)) Check(c.Left>=-2 && c.Top>=-2 && c.Right<=parent.ClientSize.Width+2 && c.Bottom<=parent.ClientSize.Height+2,"control outside parent: "+c.Name+" "+c.GetType().Name+" bounds="+c.Bounds+" parent="+parent.ClientSize);
   if(c is Label || c is Button || c is LinkLabel) {
    string text=c.Text.Replace("\r\n"," / "); if(text.Length>65) text=text.Substring(0,65);
    Size needed=c.GetPreferredSize(new Size(c.Width,0));
    Check(c.Height+2>=needed.Height,"text clipped: "+text+" actual="+c.Size+" needed="+needed);
    if(c is Label && !(c is LinkLabel)) Check(c.Top>=0 && c.Bottom<=parent.ClientSize.Height+2,"label clipped by parent: "+text+" bounds="+c.Bounds+" parent="+parent.ClientSize);
   }
   var peers=parent.Controls.Cast<Control>().Where(x=>x.Visible && x!=c && !(x is TabPage)).ToArray();
   foreach(var peer in peers) {
    if(c.TabIndex>=peer.TabIndex) continue;
    Rectangle overlap=Rectangle.Intersect(c.Bounds,peer.Bounds);
    Check(overlap.Width<=1 || overlap.Height<=1,"sibling overlap: "+c.GetType().Name+" "+c.Text+" / "+peer.GetType().Name+" "+peer.Text);
   }
   Inspect(c);
  }
 }
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new MainForm(args[0])) {
   form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-30000,-30000); form.ShowInTaskbar=false; form.Show(); Application.DoEvents();
   if(args.Length>2) { float factor=float.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture); Console.WriteLine("Synthetic font + geometry scaling factor="+factor); SimulateScale(form,factor); }
   using(var g=form.CreateGraphics()) Console.WriteLine("DPI="+g.DpiX+" baseline="+form.AutoScaleDimensions+" current="+form.CurrentAutoScaleDimensions+" client="+form.ClientSize+" min="+form.MinimumSize);
   Console.WriteLine("CASE default"); Inspect(form); Save(form,Path.Combine(Path.GetDirectoryName(args[1]),Path.GetFileNameWithoutExtension(args[1])+"-default.png"));
   form.Size=form.MinimumSize; Application.DoEvents(); Console.WriteLine("CASE minimum window"); Inspect(form);
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   var status=(Label)typeof(MainForm).GetField("status",flags).GetValue(form);
   status.Text="无法连接 GitHub，请检查系统代理设置后重试。详细信息：连接在服务器返回下载内容之前中断，请确认网络可用、系统时间正确，或者稍后重新检查。现有程序和数据没有被修改，您可以安全关闭助手。";
   ((Button)typeof(MainForm).GetField("update",flags).GetValue(form)).Text="重新安装 / 修复";
   var pathField=typeof(MainForm).GetField("programPath",flags);
   if(pathField!=null) ((TextBox)pathField.GetValue(form)).Text=@"D:\这是一个很长的程序文件夹名称用于检查路径显示\这是一个很长的程序文件夹名称用于检查路径显示\这是一个很长的程序文件夹名称用于检查路径显示\cc-switch.exe";
   Application.DoEvents(); Console.WriteLine("CASE minimum window with long status / paths"); Inspect(form);
   using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[1]); }
  }
  Console.WriteLine("RESULT "+checks+" geometry checks, "+failures+" failures"); return failures==0?0:1;
 }
}


