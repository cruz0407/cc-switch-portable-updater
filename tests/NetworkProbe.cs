using System;
using System.IO;
using System.Threading;
using CCSwitchUpdater;
class NetworkProbe {
 static int Main(string[] args) {
  try {
   using(var client=new GitHubClient()) using(var cancel=new CancellationTokenSource(TimeSpan.FromMinutes(5))) {
    var r=client.GetLatestAsync("x64",cancel.Token).GetAwaiter().GetResult();
    Console.WriteLine("Stable release: " + r.Tag + "; asset=" + r.AssetName);
    Directory.CreateDirectory(args[0]); string zip=Path.Combine(args[0],r.AssetName);
    client.DownloadAsync(r,zip,null,cancel.Token).GetAwaiter().GetResult();
    Core.VerifyPackage(zip,r);
    string exe=Core.ExtractPackage(zip,Path.Combine(args[0],"stage-"+Guid.NewGuid().ToString("N")),r,"x64");
    Console.WriteLine("PASS official download, size, SHA256, archive, PE version and architecture: " + Core.ReadVersion(exe));
    Console.WriteLine("Binary was NOT executed or installed.");
   }
   return 0;
  } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
 }
}
