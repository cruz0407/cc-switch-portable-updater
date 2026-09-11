using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Script.Serialization;

namespace CCSwitchUpdater {
 public sealed class NetworkSettings {
  public string Mode {get;set;}
  public string ProxyAddress {get;set;}
  public NetworkSettings() {Mode="system";ProxyAddress="";}
  public string Label {get {return Mode=="direct"?"直连":Mode=="custom"?"自定义 HTTP 代理":"系统代理";}}
  public void Validate() {
   if(Mode!="system" && Mode!="direct" && Mode!="custom") throw new InvalidDataException("网络连接方式无效，请重新设置。");
   if(Mode!="custom") return;
   Uri uri;
   if(!Uri.TryCreate(ProxyAddress,UriKind.Absolute,out uri) || uri.Scheme!="http" || String.IsNullOrWhiteSpace(uri.Host) ||
    uri.UserInfo.Length!=0 || uri.AbsolutePath!="/" || uri.Query.Length!=0 || uri.Fragment.Length!=0)
    throw new InvalidDataException("请输入 HTTP 正向代理地址，例如 http://127.0.0.1:7890。不能包含账号、密码、路径、查询参数或反代前缀；不支持 SOCKS。");
  }
  public HttpClientHandler CreateHandler() {
   Validate();
   var handler=new HttpClientHandler {AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate,UseProxy=Mode!="direct"};
   if(Mode=="custom") handler.Proxy=new WebProxy(new Uri(ProxyAddress)) {BypassProxyOnLocal=false,Credentials=null};
   return handler;
  }
  public static NetworkSettings Load(string file) {
   Core.EnsureNoReparse(file);
   if(!File.Exists(file)) return new NetworkSettings();
   if(new FileInfo(file).Length>16384) throw new InvalidDataException("网络设置文件过大，请通过“网络设置”重新保存。");
   try {
    var result=new JavaScriptSerializer().Deserialize<NetworkSettings>(File.ReadAllText(file));
    if(result==null) throw new InvalidDataException("网络设置为空。");
    result.Validate(); return result;
   } catch(InvalidDataException){throw;} catch(Exception e){throw new InvalidDataException("网络设置文件损坏，请通过“网络设置”重新保存；未自动改变连接方式。",e);}
  }
  public void Save(string file) {
   Validate(); Core.EnsureNoReparse(file);
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
   string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {
    File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(new NetworkSettings{Mode=Mode,ProxyAddress=Mode=="custom"?ProxyAddress:""}));
    if(File.Exists(file)) File.Replace(temporary,file,null); else File.Move(temporary,file);
   } finally {if(File.Exists(temporary)) File.Delete(temporary);}
  }
 }
 public sealed class GitHubNetworkException : IOException {
  public string Kind {get;private set;}
  public DateTimeOffset? RetryAt {get;private set;}
  public GitHubNetworkException(string kind,string message,DateTimeOffset? retryAt):base(message){Kind=kind;RetryAt=retryAt;}
 }
 public static class GitHubFailure {
  static string Header(HttpResponseMessage response,string name) {
   IEnumerable<string> values; return response.Headers.TryGetValues(name,out values)?values.FirstOrDefault():null;
  }
  static DateTimeOffset? RetryAfter(HttpResponseMessage response,DateTimeOffset now) {
   string raw=Header(response,"Retry-After"); long seconds; DateTimeOffset date;
   if(Int64.TryParse(raw,out seconds) && seconds>=0 && seconds<=604800) return now.AddSeconds(Math.Max(1,seconds));
   if(DateTimeOffset.TryParse(raw,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out date) && date>now && date<now.AddDays(7)) return date;
   return null;
  }
  public static string WaitMessage(DateTimeOffset date) {return "请在 "+date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture)+" 后重试；等待期间不会自动重试或切换线路。";}
  public static GitHubNetworkException Describe(HttpResponseMessage response,string body,DateTimeOffset now) {
   int status=(int)response.StatusCode; string message=""; bool json=false;
   try {var value=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(body ?? ""); object raw; json=value!=null; if(json && value.TryGetValue("message",out raw)) message=raw as string ?? "";}catch(ArgumentException){}catch(InvalidOperationException){}
   DateTimeOffset? retry=RetryAfter(response,now);
   bool primary=(status==403||status==429) && Header(response,"X-RateLimit-Remaining")=="0";
   if(primary) {
    long epoch;
    if(Int64.TryParse(Header(response,"X-RateLimit-Reset"),out epoch) && epoch>=0 && epoch<=253402300799) {
     var reset=DateTimeOffset.FromUnixTimeSeconds(epoch);
     if(reset>now && reset<now.AddDays(7) && (!retry.HasValue||reset>retry.Value)) retry=reset;
    }
    if(!retry.HasValue) retry=now.AddMinutes(1);
    long limit; string quota=Int64.TryParse(Header(response,"X-RateLimit-Limit"),out limit)&&limit>=0?"（剩余 0 / "+limit+"）":"（剩余 0）";
    return new GitHubNetworkException("primary-rate-limit","GitHub API 配额已耗尽"+quota+"，HTTP "+status+"。"+WaitMessage(retry.Value),retry);
   }
   if(status==429 || ((status==403) && (retry.HasValue || message.IndexOf("secondary rate",StringComparison.OrdinalIgnoreCase)>=0 || message.IndexOf("abuse",StringComparison.OrdinalIgnoreCase)>=0))) {
    if(!retry.HasValue) retry=now.AddMinutes(1);
    return new GitHubNetworkException("secondary-rate-limit","GitHub 触发请求频率限制（HTTP "+status+"）。"+WaitMessage(retry.Value),retry);
   }
   if(status==403) {
    if(!json) return new GitHubNetworkException("gateway-denied","HTTP 403：返回内容不是 GitHub JSON，可能由代理、网关或访问策略拦截。请检查“网络设置”，也可用浏览器打开官方发布页。",null);
    return new GitHubNetworkException("forbidden","GitHub 拒绝此次请求（HTTP 403），未取得配额耗尽证据，不能仅凭 403 确认是限流。可检查网络设置或在浏览器打开官方发布页。",null);
   }
   if(status==401) return new GitHubNetworkException("unauthorized","HTTP 401：认证被拒绝。助手未发送 Token，请检查代理是否修改请求；不要提供 Chrome Cookie。",null);
   if(status==407) return new GitHubNetworkException("proxy-auth","HTTP 407：代理要求身份认证。当前不支持代理账号密码，请选择无需认证的可信代理或恢复系统代理。",null);
   if(status==404) return new GitHubNetworkException("not-found","HTTP 404：官方发布接口或附件不存在。请重新检查版本，或在浏览器打开官方发布页核对。",null);
   if(status>=500) return new GitHubNetworkException("upstream","HTTP "+status+"：GitHub 或中间网关服务异常，并非已确认限流。可稍后重试或检查网络设置。",null);
   return new GitHubNetworkException("http-error","GitHub 请求失败（HTTP "+status+"）。请查看网络设置，或用浏览器打开官方发布页。",null);
  }
  public static string TransportMessage(Exception error) {
   for(Exception current=error;current!=null;current=current.InnerException) {
    var web=current as WebException;
    if(web==null) continue;
    if(web.Status==WebExceptionStatus.NameResolutionFailure || web.Status==WebExceptionStatus.ProxyNameResolutionFailure) return "域名解析失败：请检查 DNS 或代理地址。可在“网络设置”选择其他连接方式。";
    if(web.Status==WebExceptionStatus.TrustFailure || web.Status==WebExceptionStatus.SecureChannelFailure) return "HTTPS 证书或 TLS 验证失败。请检查系统时间、证书和代理；助手不会关闭证书校验。";
    if(web.Status==WebExceptionStatus.ConnectFailure) return "无法连接服务器或代理端口。请确认代理已运行、端口正确，或选择系统代理 / 直连。";
   }
   return "无法完成 GitHub 网络请求。请检查网络连接或“网络设置”；Chrome 能访问网页不代表 API 使用相同连接方式。";
  }
 }
 public sealed class NetworkCooldown {
  public DateTimeOffset? Until {get;private set;}
  public void Record(GitHubNetworkException error) {if(error.RetryAt.HasValue && (!Until.HasValue||error.RetryAt.Value>Until.Value)) Until=error.RetryAt;}
  public void Check(DateTimeOffset now) {
   if(Until.HasValue && now<Until.Value) throw new GitHubNetworkException("cooldown","GitHub 限流等待中，未发送新请求。"+GitHubFailure.WaitMessage(Until.Value),Until);
  }
 }
}
