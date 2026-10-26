using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace CCSwitchUpdater {
 public static class GitHubHttp {
  public const string WebRoot="https://github.com/farion1231/cc-switch/releases/";
  public static readonly string[] Mirrors={"https://gh-proxy.com/","https://ghfast.top/"};
  public static bool IsWebUrl(string value) {
   Uri u;if(!Uri.TryCreate(value,UriKind.Absolute,out u)||u.Scheme!="https"||u.Host!="github.com"||u.Port!=443||u.UserInfo!=""||u.Query!=""||u.Fragment!="")return false;
   return Regex.IsMatch(u.AbsolutePath,@"^/farion1231/cc-switch/releases/(latest|tag/v\d+\.\d+\.\d+|expanded_assets/v\d+\.\d+\.\d+)$");
  }
  public static bool IsMirrorUrl(string value,string root) {
   if(Array.IndexOf(Mirrors,root)<0 || !value.StartsWith(root,StringComparison.Ordinal))return false;
   return Core.IsAllowedUrl(value.Substring(root.Length),true);
  }
  static bool Allowed(string url,string scope,string mirror,bool initial) {
   if(scope=="api")return Core.IsAllowedUrl(url,false);
   if(scope=="web")return IsWebUrl(url);
   if(scope!="asset")return false;
   if(Core.IsAllowedUrl(url,true))return true;
   if(mirror!=null && IsMirrorUrl(url,mirror))return true;
   Uri u;if(initial || !Uri.TryCreate(url,UriKind.Absolute,out u))return false;
   return u.Scheme=="https" && u.Port==443 && u.UserInfo=="" && u.Fragment=="" && (u.Host=="release-assets.githubusercontent.com" || u.Host=="objects.githubusercontent.com");
  }
  public static async Task<string> ReadTextAsync(HttpResponseMessage response,int limit,CancellationToken token,bool truncate=false) {
   if(response.Content==null)return "";
   using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var memory=new MemoryStream()) {
    byte[] b=new byte[16384];int n;
    while((n=await input.ReadAsync(b,0,b.Length,token).ConfigureAwait(false))>0) {
     if(memory.Length+n>limit){if(!truncate)throw new InvalidDataException("服务器返回内容过大。");memory.Write(b,0,(int)(limit-memory.Length));break;}
     memory.Write(b,0,n);
    }
    return Encoding.UTF8.GetString(memory.ToArray());
   }
  }
  public static async Task<HttpResponseMessage> GetAsync(HttpClient client,string url,string scope,NetworkCooldown gate,CancellationToken token,string mirror=null) {
   if(!Allowed(url,scope,mirror,true))throw new InvalidDataException("请求地址未通过来源校验。");
   if(scope=="api" && gate!=null)gate.Check(DateTimeOffset.UtcNow);
   string current=url;
   for(int i=0;i<6;i++) {
    token.ThrowIfCancellationRequested();
    using(var request=new HttpRequestMessage(HttpMethod.Get,current)) {
     request.Headers.Accept.ParseAdd(scope=="api"?"application/vnd.github+json":scope=="web"?"text/html":"application/octet-stream");
     HttpResponseMessage response;
     using(var headersTimeout=CancellationTokenSource.CreateLinkedTokenSource(token)) {
      headersTimeout.CancelAfter(TimeSpan.FromSeconds(12));
      response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,headersTimeout.Token).ConfigureAwait(false);
     }
     int code=(int)response.StatusCode;
     if(code>=300 && code<=399) {
      Uri location=response.Headers.Location; response.Dispose();
      if(location==null)throw new InvalidDataException("重定向缺少目标地址。");
      var next=location.IsAbsoluteUri?location:new Uri(new Uri(current),location);
      if(!Allowed(next.AbsoluteUri,scope,mirror,false))throw new InvalidDataException("已阻止非预期域名的重定向。");
      current=next.AbsoluteUri;continue;
     }
     if(!response.IsSuccessStatusCode) {
      try {
       var text=await ReadTextAsync(response,32768,token,true).ConfigureAwait(false);
       var error=GitHubFailure.Describe(response,text,DateTimeOffset.UtcNow);
       if(scope=="api" && gate!=null)gate.Record(error);
       throw error;
      } finally {response.Dispose();}
     }
     return response;
    }
   }
   throw new IOException("重定向次数超过限制。");
  }
  public static HttpClient CreateClient(NetworkSettings settings,HttpMessageHandler handler=null) {
   System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
   var client=new HttpClient(handler??settings.CreateHandler());client.Timeout=Timeout.InfiniteTimeSpan;
   client.DefaultRequestHeaders.UserAgent.ParseAdd("CCSwitch-Portable-Update-Helper/1.7");return client;
  }
 }
}
