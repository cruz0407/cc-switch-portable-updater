using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace CCSwitchUpdater {
 public static class GitHubWebFallback {
  public const string LatestReleasePage=GitHubHttp.WebRoot+"latest";
  public static Release Parse(string html,string tag,string architecture) {
   Core.ParseVersion(tag);
   if(!Regex.IsMatch(tag??"",@"^v\d+\.\d+\.\d+$") || (architecture!="x64"&&architecture!="arm64"))throw new InvalidDataException("发布标签或程序架构无效。");
   if(String.IsNullOrWhiteSpace(html)||html.Length>6*1024*1024)throw new InvalidDataException("官方附件列表为空或过大。");
   string name="CC-Switch-"+tag+"-Windows"+(architecture=="arm64"?"-arm64":"")+"-Portable.zip";
   string path="/farion1231/cc-switch/releases/download/"+tag+"/"+name, expected="https://github.com"+path;
   string matched=null;int count=0;
   foreach(Match row in Regex.Matches(html,@"<li\b[^>]*>[\s\S]*?</li>",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2))) {
    bool found=false;
    foreach(Match link in Regex.Matches(row.Value,"href\\s*=\\s*[\"']([^\"']+)[\"']",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1))) {
     string href=WebUtility.HtmlDecode(link.Groups[1].Value);
     if(href==path || href==expected)found=true;
    }
    if(found){count++;matched=row.Value;}
   }
   if(count!=1)throw new InvalidDataException("官方网页未找到唯一匹配的便携包；请勿使用其他架构或邻近附件的校验值。");
   var digests=Regex.Matches(matched,@"sha256:([a-fA-F0-9]{64})(?![a-fA-F0-9])").Cast<Match>().Select(x=>x.Groups[1].Value.ToLowerInvariant()).Distinct().ToArray();
   if(digests.Length!=1)throw new InvalidDataException("目标附件没有唯一的官方 SHA-256，无法安全安装。");
   return new Release{Tag=tag,Version=Core.ParseVersion(tag),AssetName=name,DownloadUrl=expected,Digest="sha256:"+digests[0],Size=0,Source="官方 Releases 网页",Notes="# CC Switch "+tag+"\n\n已通过官方发布页获取版本、下载地址及目标附件 SHA-256。\n\n[查看完整更新说明](https://github.com/farion1231/cc-switch/releases/tag/"+tag+")"};
  }
  public static async Task<Release> GetLatestAsync(NetworkSettings settings,string architecture,CancellationToken token,HttpMessageHandler handler=null) {
   using(var client=GitHubHttp.CreateClient(settings,handler)) {
    string tag;
    using(var response=await GitHubHttp.GetAsync(client,LatestReleasePage,"web",null,token).ConfigureAwait(false)) {
     string final=response.RequestMessage==null?"":response.RequestMessage.RequestUri.AbsoluteUri;
     if(!GitHubHttp.IsWebUrl(final))throw new InvalidDataException("官方最新发布页未返回有效目标地址。");
     var match=Regex.Match(final,@"/releases/tag/(v\d+\.\d+\.\d+)$");
     if(!match.Success)throw new InvalidDataException("官方最新发布页未重定向到稳定版标签。");
     tag=match.Groups[1].Value;
    }
    using(var response=await GitHubHttp.GetAsync(client,GitHubHttp.WebRoot+"expanded_assets/"+tag,"web",null,token).ConfigureAwait(false)) {
     return Parse(await GitHubHttp.ReadTextAsync(response,6*1024*1024,token).ConfigureAwait(false),tag,architecture);
    }
   }
  }
 }
}
