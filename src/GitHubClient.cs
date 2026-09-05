using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace CCSwitchUpdater {
 public sealed class GitHubClient : IDisposable {
  readonly HttpClient client;
  public GitHubClient() {
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
   client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
   client.Timeout = Timeout.InfiniteTimeSpan;
   client.DefaultRequestHeaders.UserAgent.ParseAdd("CCSwitch-Portable-Update-Helper/1.3");
   client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
  }
  async Task<HttpResponseMessage> GetAsync(string url, bool asset, CancellationToken token) {
   if (!Core.IsAllowedUrl(url, asset)) throw new InvalidDataException("不可信的请求地址。");
   Uri current = new Uri(url);
   for (int i=0; i<6; i++) {
    var response = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
    int status = (int)response.StatusCode;
    if (status >= 300 && status <= 399) {
     Uri location = response.Headers.Location;
     response.Dispose();
     if (location == null) throw new IOException("GitHub 返回了没有目标地址的重定向。");
     Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);
     bool allowed = next.Scheme == "https" && next.Port == 443 && next.UserInfo.Length == 0 && next.Fragment.Length == 0 &&
      (Core.IsAllowedUrl(next.AbsoluteUri, asset) || (asset && (next.Host == "release-assets.githubusercontent.com" || next.Host == "objects.githubusercontent.com")));
     if (!allowed) throw new InvalidDataException("下载重定向到了非官方地址，已停止。");
     current = next; continue;
    }
    if (!response.IsSuccessStatusCode) {
     response.Dispose();
     if (status == 403 || status == 429) throw new IOException("GitHub 拒绝请求或触发访问限流（HTTP " + status + "）。请稍后重试，或检查系统代理。");
     throw new IOException("GitHub 请求失败（HTTP " + status + "），请稍后重试。");
    }
    return response;
   }
   throw new IOException("GitHub 重定向次数过多。");
  }
  public async Task<Release> GetLatestAsync(string architecture, CancellationToken token) {
   using(var response = await GetAsync(Core.LatestUrl, false, token).ConfigureAwait(false))
   using(var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
   using(var memory = new MemoryStream()) {
    byte[] buffer = new byte[16384]; int n;
    while((n=await stream.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0) {
     if(memory.Length+n>4*1024*1024) throw new InvalidDataException("发布信息过大。");
     memory.Write(buffer,0,n);
    }
    return Core.SelectRelease(System.Text.Encoding.UTF8.GetString(memory.ToArray()),architecture);
   }
  }
  public async Task DownloadAsync(Release release, string destination, IProgress<int> progress, CancellationToken token) {
   Core.EnsureNoReparse(destination);
   using(var response=await GetAsync(release.DownloadUrl,true,token).ConfigureAwait(false)) {
    long? length=response.Content.Headers.ContentLength;
    if(length.HasValue && length.Value!=release.Size) throw new InvalidDataException("服务器返回的文件大小与发布信息不一致。");
    using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
    using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)) {
     byte[] buffer=new byte[65536]; long total=0; int n,last=-1;
     while((n=await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0) {
      total+=n;
      if(total>release.Size || total>Core.MaxPackageSize) throw new InvalidDataException("下载内容超出预期大小。");
      await output.WriteAsync(buffer,0,n,token).ConfigureAwait(false);
      int value=(int)(total*100/release.Size); if(value!=last && progress!=null) { last=value; progress.Report(value); }
     }
     if(total!=release.Size) throw new InvalidDataException("下载未完成，请重试。");
     await output.FlushAsync(token).ConfigureAwait(false);
    }
   }
  }
  public void Dispose() { client.Dispose(); }
 }
}
