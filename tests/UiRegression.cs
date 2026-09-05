using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CCSwitchUpdater;
[assembly: AssemblyVersion("3.20.1.0")]
[assembly: AssemblyFileVersion("3.20.1.0")]
class UiRegression {
 static int passed,failed; static string fixture, root;
 static FieldInfo Field(string name) { return typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic); }
 static T Value<T>(MainForm f,string name) { var field=Field(name); if(field==null) throw new Exception("Missing field "+name); return (T)field.GetValue(f); }
 static void Invoke(MainForm f,string name,params object[] args) { typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args); }
 static void Assert(bool yes,string why) { if(!yes) throw new Exception(why); }
 static void Test(string name,Action action) { try { action(); passed++; Console.WriteLine("PASS "+name); } catch(Exception e) { failed++; Console.WriteLine("FAIL "+name+": "+e.Message); } }
 static MainForm Form(ushort machine) {
  string dir=Path.Combine(root,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
  string exe=Path.Combine(dir,"cc-switch.exe"); File.Copy(Assembly.GetExecutingAssembly().Location,exe);
  using(var s=File.Open(exe,FileMode.Open,FileAccess.ReadWrite)) using(var reader=new BinaryReader(s)) { s.Position=0x3c; int offset=reader.ReadInt32(); s.Position=offset+4; s.WriteByte((byte)(machine&255)); s.WriteByte((byte)(machine>>8)); }
  File.WriteAllText(Path.Combine(dir,"portable.ini"),"portable=true");
  return new MainForm(dir);
 }
 static void Latest(MainForm f,Version version) { Release r=Core.SelectRelease(fixture,"x64"); r.Version=version; Field("latest").SetValue(f,r); Invoke(f,"SetBusy",false); }
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  fixture=File.ReadAllText(args[0]); root=Path.Combine(Path.GetTempPath(),"ccswitch-ui-regression-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  Test("same-version x64 exposes reinstall instead of disabled button",delegate { using(var f=Form(0x8664)) { Latest(f,new Version(3,20,1,0)); var b=Value<Button>(f,"update"); Assert(b.Enabled,"Same-version reinstall remains disabled"); Assert(b.Text.Contains("重新安装"),"Missing explicit reinstall label"); } });
  Test("ARM64 binary on x64 shows startup mismatch warning and selects system x64",delegate { using(var f=Form(0xaa64)) { Assert(Value<Label>(f,"status").Text.Contains("不一致"),"Architecture mismatch not shown: "+Value<Label>(f,"status").Text); Assert(Value<string>(f,"targetArchitecture")=="x64","Target copied from installed ARM64 instead of system"); Latest(f,new Version(3,20,1,0)); Assert(Value<Button>(f,"update").Enabled,"Same-version architecture repair disabled"); Assert(Value<Button>(f,"update").Text.Contains("修复"),"Repair action missing"); } });
  Test("x86 binary on x64 remains checkable and repairable",delegate { using(var f=Form(0x014c)) { Assert(Value<Button>(f,"check").Enabled,"x86 target incorrectly blocks all checks: "+Value<Label>(f,"status").Text); Latest(f,new Version(3,20,1,0)); Assert(Value<Button>(f,"update").Enabled,"x86 repair disabled"); Assert(Value<string>(f,"targetArchitecture")=="x64","Wrong target"); } });
  Test("older release stays blocked even for architecture repair",delegate { using(var f=Form(0xaa64)) { Latest(f,new Version(3,19,0,0)); Assert(!Value<Button>(f,"update").Enabled,"Repair incorrectly allows downgrade"); } });
  Test("busy state disables reinstall; idle restores it",delegate { using(var f=Form(0x8664)) { Latest(f,new Version(3,20,1,0)); Invoke(f,"SetBusy",true); Assert(!Value<Button>(f,"update").Enabled,"Busy enabled"); Invoke(f,"SetBusy",false); Assert(Value<Button>(f,"update").Enabled,"Idle repair disabled"); } });
  Test("architecture repair validates and stages SYSTEM architecture binary",delegate { using(var f=Form(0xaa64)) { var target=Value<string>(f,"targetArchitecture"); Assert(Core.SelectRelease(fixture,target).AssetName=="CC-Switch-v3.20.1-Windows-Portable.zip","ARM64 asset selected for x64 system"); } });
  Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed. "+root); return failed==0?0:1;
 }
}

