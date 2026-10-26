using System;
using System.IO;
using System.Threading;
using CCSwitchUpdater;
class MultiRouteProbe {
 static int Main(string[] args) {
  try {
   string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
   var gate=new NetworkCooldown();gate.Record(new GitHubNetworkException("primary","simulate API quota exhausted",DateTimeOffset.UtcNow.AddHours(1)));
   Release release;
   using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(55)))release=new UpdateRoutes(new NetworkSettings(),gate,Console.WriteLine).GetLatestAsync("x64",timeout.Token).GetAwaiter().GetResult();
   Console.WriteLine("PASS API deliberately unavailable; got "+release.Tag+" via "+release.Source+" SHA256="+release.Digest);
   int passed=0,failed=0;
   foreach(string mirror in new[]{"",GitHubHttp.Mirrors[0],GitHubHttp.Mirrors[1]}) {
    string name=mirror==""?"official-direct":new Uri(mirror).Host;
    string file=Path.Combine(root,name+"-"+release.Tag+".zip");
    var r=new Release{Tag=release.Tag,Version=release.Version,DownloadUrl=release.DownloadUrl,Digest=release.Digest,Size=0,AssetName=release.AssetName};
    try {
     using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45)))using(var c=new GitHubClient(new NetworkSettings{Mode=mirror==""?"direct":"system"},null,gate))c.DownloadFromAsync(r,mirror+r.DownloadUrl,file,null,timeout.Token,mirror==""?null:mirror).GetAwaiter().GetResult();
     Core.VerifyPackage(file,r);string stage=Path.Combine(root,"stage-"+name+"-"+Guid.NewGuid().ToString("N"));string binary=Core.ExtractPackage(file,stage,r,"x64");
     if(passed==0) {
      string target=Path.Combine(root,"install-fixture");Directory.CreateDirectory(target);File.Copy(binary,Path.Combine(target,"cc-switch.exe"));File.WriteAllText(Path.Combine(target,"portable.ini"),"portable=true");File.WriteAllText(Path.Combine(target,"unrelated.txt"),"keep");
      string data=Path.Combine(root,"data-fixture");Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"settings.json"),"{}");
      string backup=Core.Install(binary,target,data,Path.Combine(root,"backups"));
      if(Core.Sha256(binary)!=Core.Sha256(Path.Combine(target,"cc-switch.exe")) || File.ReadAllText(Path.Combine(target,"unrelated.txt"))!="keep")throw new Exception("isolated install failed");
      Console.WriteLine("PASS isolated install + backup: "+backup);
     }
     passed++;Console.WriteLine("PASS live "+name+": bytes="+r.Size+" version="+Core.ReadVersion(binary)+" SHA256 verified against official page");
    } catch(Exception e){failed++;Console.WriteLine("UNAVAILABLE "+name+": "+e.GetType().Name+" "+e.Message);}
   }
   Console.WriteLine("RESULT real routes: "+passed+" passed; "+failed+" unavailable. No downloaded EXE was executed.");
   return passed==0?1:0;
  } catch(Exception e){Console.WriteLine(e.ToString());return 1;}
 }
}
