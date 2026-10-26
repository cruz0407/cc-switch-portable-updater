using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace CCSwitchUpdater {
 public sealed class GitHubClient:IDisposable {
  readonly HttpClient client;readonly NetworkCooldown cooldown;
  public GitHubClient(NetworkSettings settings=null,HttpMessageHandler handler=null,NetworkCooldown gate=null) {
   settings=settings??new NetworkSettings();settings.Validate();cooldown=gate??new NetworkCooldown();client=GitHubHttp.CreateClient(settings,handler);
  }
  public async Task<Release> GetLatestAsync(string architecture,CancellationToken token) {
   using(var response=await GitHubHttp.GetAsync(client,Core.LatestUrl,"api",cooldown,token).ConfigureAwait(false)) {
    var release=Core.SelectRelease(await GitHubHttp.ReadTextAsync(response,4*1024*1024,token).ConfigureAwait(false),architecture);
    release.Source="官方 GitHub API";return release;
   }
  }
  public Task DownloadAsync(Release release,string destination,IProgress<int> progress,CancellationToken token) {return DownloadFromAsync(release,release.DownloadUrl,destination,progress,token);}
  public async Task DownloadFromAsync(Release release,string url,string destination,IProgress<int> progress,CancellationToken token,string mirror=null) {
   Core.EnsureNoReparse(destination);
   if(release.Size<0 || release.Size>Core.MaxPackageSize || !System.Text.RegularExpressions.Regex.IsMatch(release.Digest??"",@"^sha256:[a-fA-F0-9]{64}$"))throw new InvalidDataException("缺少有效的官方大小/校验摘要。");
   bool created=false,complete=false;long expected=release.Size;
   try {
    using(var response=await GitHubHttp.GetAsync(client,url,"asset",null,token,mirror).ConfigureAwait(false)) {
     long? header=response.Content.Headers.ContentLength;
     if(header.HasValue && (header.Value<=0 || header.Value>Core.MaxPackageSize || (expected>0&&header.Value!=expected)))throw new InvalidDataException("附件响应大小与预期不一致。");
     long length=expected>0?expected:header??0;
     using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
     using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)) {
      created=true;byte[] buffer=new byte[65536];long total=0;int n,last=-1;
      while((n=await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0) {
       total+=n;if(total>Core.MaxPackageSize || (length>0&&total>length))throw new InvalidDataException("附件超过允许大小。");
       await output.WriteAsync(buffer,0,n,token).ConfigureAwait(false);
       int value=length>0?(int)(total*100/length):0;if(value!=last&&progress!=null){last=value;progress.Report(value);}
      }
      if(total<=0 || (length>0&&total!=length))throw new InvalidDataException("附件下载被截断或为空。");
      await output.FlushAsync(token).ConfigureAwait(false);expected=total;
     }
    }
    if(!String.Equals("sha256:"+Core.Sha256(destination),release.Digest,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("附件 SHA-256 与官方摘要不符，已丢弃，将尝试下一条下载线路。");
    release.Size=expected;complete=true;if(progress!=null)progress.Report(100);
   } finally {if(created&&!complete&&File.Exists(destination))File.Delete(destination);}
  }
  public void Dispose(){client.Dispose();}
 }
}
