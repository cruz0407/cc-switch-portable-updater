using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace CCSwitchUpdater {
 public static class GitHubWebFallback {
  public const string LatestReleasePage="https://github.com/farion1231/cc-switch/releases/latest";
  public static Release Parse(string html,string tag,string architecture) {
   if(String.IsNullOrWhiteSpace(html)||architecture!="x64"&&architecture!="arm64")throw new InvalidDataException("官方发布页内容不完整。");
   string name="CC-Switch-"+tag+"-Windows"+(architecture=="arm64"?"-arm64":"")+"-Portable.zip";
   string escaped=Regex.Escape(name); Match link=Regex.Match(html,"href=\\\"([^\\\"]*/releases/download/"+Regex.Escape(tag)+"/"+escaped+"\\\"",RegexOptions.IgnoreCase); if(!link.Success)throw new InvalidDataException("官方网页中未找到匹配的 Windows 便携包。");
   Match digest=Regex.Match(html,"sha256:([a-f0-9]{64})",RegexOptions.IgnoreCase); if(!digest.Success)throw new InvalidDataException("官方网页未提供 SHA-256，已停止，避免安装来源不明的文件。");
   string url="https://github.com"+WebUtility.HtmlDecode(link.Groups[1].Value); if(!Core.IsAllowedUrl(url,true))throw new InvalidDataException("官方网页给出的下载地址不可信。");
   return new Release{Tag=tag,Version=Core.ParseVersion(tag),AssetName=name,DownloadUrl=url,Digest="sha256:"+digest.Groups[1].Value.ToLowerInvariant(),Size=0,Notes="通过官方 Releases 网页获取（API 暂不可用）。"};
  }
  public static bool CanVerifyAfterDownload(long expected,long digestSize,long actual){return expected==actual;}
  public static async Task<Release> GetLatestAsync(NetworkSettings settings,string architecture,CancellationToken token) {
   using(var client=new HttpClient(settings.CreateHandler())) {client.Timeout=TimeSpan.FromSeconds(45);client.DefaultRequestHeaders.UserAgent.ParseAdd("CCSwitch-Portable-Update-Helper/1.5");var page=await client.GetStringAsync(LatestReleasePage).ConfigureAwait(false);Match tag=Regex.Match(page,"/releases/tag/(v\\d+\\.\\d+\\.\\d+)");if(!tag.Success)throw new InvalidDataException("官方发布页未找到稳定版本标签。");using(var expanded=await client.GetAsync("https://github.com/farion1231/cc-switch/releases/expanded_assets/"+tag.Groups[1].Value,HttpCompletionOption.ResponseContentRead,token).ConfigureAwait(false)){if(!expanded.IsSuccessStatusCode)throw new IOException("官方附件列表无法访问（HTTP "+(int)expanded.StatusCode+"）。");return Parse(await expanded.Content.ReadAsStringAsync().ConfigureAwait(false),tag.Groups[1].Value,architecture);}}
  }
 }
}
