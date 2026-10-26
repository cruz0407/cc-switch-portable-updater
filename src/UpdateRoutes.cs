using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace CCSwitchUpdater {
 public sealed class UpdateRoutes {
  readonly NetworkSettings settings;readonly NetworkCooldown apiCooldown;
  readonly Func<NetworkSettings,HttpMessageHandler> transport;readonly Action<string> report;
  public UpdateRoutes(NetworkSettings selected,NetworkCooldown gate,Action<string> progress=null,Func<NetworkSettings,HttpMessageHandler> handlers=null) {
   settings=selected??new NetworkSettings();settings.Validate();apiCooldown=gate??new NetworkCooldown();report=progress??delegate{};transport=handlers;
  }
  List<NetworkSettings> Connections() {
   var list=new List<NetworkSettings>{settings};
   if(settings.Mode!="direct")list.Add(new NetworkSettings{Mode="direct"});
   if(settings.Mode!="system")list.Add(new NetworkSettings());
   return list;
  }
  HttpMessageHandler Handler(NetworkSettings connection){return transport==null?null:transport(connection);}
  static bool IsRetryable(Exception error){return error is IOException || error is InvalidDataException || error is HttpRequestException || error is OperationCanceledException;}
  void Failed(string label,Exception error,List<string> failures) {
   string text=error is HttpRequestException?GitHubFailure.TransportMessage(error):error is OperationCanceledException?"该线路超时":error.Message;
   failures.Add(label+"："+text);report(label+"失败，继续尝试下一方案。"+text);
  }
  public async Task<Release> GetLatestAsync(string architecture,CancellationToken token) {
   var failures=new List<string>();
   foreach(var connection in Connections()) {
    if(!apiCooldown.Until.HasValue || apiCooldown.Until.Value<=DateTimeOffset.UtcNow) {
     string label="官方 API / "+connection.Label;report("正在尝试："+label);
     using(var attempt=CancellationTokenSource.CreateLinkedTokenSource(token)) {
      attempt.CancelAfter(TimeSpan.FromSeconds(8));
      try {using(var client=new GitHubClient(connection,Handler(connection),apiCooldown)){var release=await client.GetLatestAsync(architecture,attempt.Token).ConfigureAwait(false);release.Source=label;report("已取得官方版本："+label);return release;}}
      catch(Exception e){token.ThrowIfCancellationRequested();if(!IsRetryable(e))throw;Failed(label,e,failures);}
     }
    } else report("官方 API 等待恢复；跳过 API，继续官方网页方案。");
    string webLabel="官方网页 / "+connection.Label;report("正在尝试："+webLabel);
    using(var attempt=CancellationTokenSource.CreateLinkedTokenSource(token)) {
     attempt.CancelAfter(TimeSpan.FromSeconds(10));
     try {var release=await GitHubWebFallback.GetLatestAsync(connection,architecture,attempt.Token,Handler(connection)).ConfigureAwait(false);release.Source=webLabel;report("已取得官方版本及 SHA-256："+webLabel);return release;}
     catch(Exception e){token.ThrowIfCancellationRequested();if(!IsRetryable(e))throw;Failed(webLabel,e,failures);}
    }
   }
   throw new IOException("官方 API、官方网页和直连检查均未完成。可修改网络设置后重试。\r\n"+String.Join("\r\n",failures));
  }
  public async Task DownloadAsync(Release release,string destination,IProgress<int> progress,CancellationToken token) {
   if(File.Exists(destination))throw new IOException("下载目标已存在，请选择新的临时文件。");
   if(!Core.IsAllowedUrl(release.DownloadUrl,true))throw new InvalidDataException("附件不是官方发布地址。");
   var failures=new List<string>();var connections=Connections();
   foreach(string mirror in settings.AllowDownloadMirrors?new[]{"",GitHubHttp.Mirrors[0],GitHubHttp.Mirrors[1]}:new[]{""}) {
    foreach(var connection in connections) {
     string label=(mirror==""?"官方附件":new Uri(mirror).Host+" 下载代理")+" / "+connection.Label;
     report("正在下载："+label);if(progress!=null)progress.Report(0);
     using(var attempt=CancellationTokenSource.CreateLinkedTokenSource(token)) {
      attempt.CancelAfter(TimeSpan.FromSeconds(90));
      try {
       using(var client=new GitHubClient(connection,Handler(connection))) {
        await client.DownloadFromAsync(release,mirror+release.DownloadUrl,destination,progress,attempt.Token,mirror==""?null:mirror).ConfigureAwait(false);
       }
       report("下载并核对官方 SHA-256 通过："+label);return;
      } catch(Exception e) {
       token.ThrowIfCancellationRequested();if(!IsRetryable(e))throw;Failed(label,e,failures);
       var rate=e as GitHubNetworkException;if(rate!=null && rate.RetryAt.HasValue)break;
      }
     }
    }
   }
   throw new IOException("所有下载线路均失败；未替换现有程序。\r\n"+String.Join("\r\n",failures));
  }
 }
}
