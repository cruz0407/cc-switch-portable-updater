using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CCSwitchUpdater;
class DownloadTests {
 class Handler:HttpMessageHandler {public int Calls; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t){Calls++;var x=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[]{97,98,99})};return Task.FromResult(x);}}
 static Release R(){return new Release{Size=0,Digest="sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",DownloadUrl="https://github.com/farion1231/cc-switch/releases/download/v3.20.3/a.zip"};}
 static int Main(){int failed=0;foreach(bool waiting in new[]{false,true}){try{var h=new Handler();var gate=new NetworkCooldown();if(waiting)gate.Record(new GitHubNetworkException("primary-rate-limit","wait",DateTimeOffset.UtcNow.AddHours(1)));var r=R();string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".zip");using(var c=new GitHubClient(new NetworkSettings(),h,gate))c.DownloadAsync(r,path,null,CancellationToken.None).GetAwaiter().GetResult();Core.VerifyPackage(path,r);if(r.Size!=3||h.Calls!=1)throw new Exception("wrong size/requests");Console.WriteLine("PASS unknown-size official download; API cooldown="+waiting);}catch(Exception e){failed++;Console.WriteLine("FAIL API cooldown="+waiting+": "+e.Message);}}return failed==0?0:1;}
}
