using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CCSwitchUpdater;
class NetworkClientTests {
 sealed class Handler:HttpMessageHandler {
  public int Calls; public Func<HttpRequestMessage,HttpResponseMessage> Respond;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();Calls++;return Task.FromResult(Respond(request));}
 }
 static int passed,failed; static string fixture;
 static void Assert(bool value,string why){if(!value)throw new Exception(why);}
 static void Test(string name,Action work){try{work();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static GitHubClient Client(Handler handler,NetworkCooldown gate){var ctor=typeof(GitHubClient).GetConstructor(new[]{typeof(NetworkSettings),typeof(HttpMessageHandler),typeof(NetworkCooldown)});if(ctor==null)throw new Exception("missing configurable transport constructor");return (GitHubClient)ctor.Invoke(new object[]{new NetworkSettings(),handler,gate});}
 static HttpResponseMessage Response(int status,string body){return new HttpResponseMessage((HttpStatusCode)status){Content=new StringContent(body)};}
 static GitHubNetworkException Failure(GitHubClient client){try{client.GetLatestAsync("x64",CancellationToken.None).GetAwaiter().GetResult();}catch(GitHubNetworkException e){return e;}throw new Exception("expected classified error");}
 static int Main(string[] args){fixture=File.ReadAllText(args[0]);
  Test("client preserves GitHub quota headers before disposing response",delegate {var h=new Handler{Respond=delegate {var r=Response(403,"{\"message\":\"API rate limit exceeded\"}");r.Headers.Add("X-RateLimit-Remaining","0");r.Headers.Add("X-RateLimit-Limit","60");r.Headers.Add("X-RateLimit-Reset",DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString());return r;}};using(var c=Client(h,new NetworkCooldown()))Assert(Failure(c).Kind=="primary-rate-limit","headers lost");});
  Test("new clients sharing cooldown cannot issue repeated rate-limited requests",delegate {var gate=new NetworkCooldown();var first=new Handler{Respond=delegate {var r=Response(429,"{}");r.Headers.Add("Retry-After","120");return r;}};using(var c=Client(first,gate))Failure(c);var next=new Handler{Respond=delegate {return Response(200,fixture);}};using(var c=Client(next,gate))Assert(Failure(c).Kind=="cooldown","cooldown missing");Assert(first.Calls==1 && next.Calls==0,"sent extra retry");});
  Test("permission denial not retried automatically",delegate {var h=new Handler{Respond=delegate{return Response(403,"{\"message\":\"forbidden\"}");}};using(var c=Client(h,new NetworkCooldown()))Assert(Failure(c).Kind=="forbidden","misclassified");Assert(h.Calls==1,"automatic retry");});
  Test("oversized error HTML truncated and never exposed",delegate {var h=new Handler{Respond=delegate{return Response(403,"<html>"+new string('x',100000)+"secret</html>");}};using(var c=Client(h,new NetworkCooldown())){var e=Failure(c);Assert(e.Kind=="gateway-denied","wrong html error");Assert(!e.Message.Contains("secret"),"body exposed");}});
  Test("successful official metadata and headers unaffected",delegate {var h=new Handler{Respond=delegate(HttpRequestMessage req){Assert(req.RequestUri.AbsoluteUri==Core.LatestUrl,"changed origin");Assert(req.Headers.Authorization==null,"token sent unexpectedly");return Response(200,fixture);}};using(var c=Client(h,new NetworkCooldown()))Assert(c.GetLatestAsync("x64",CancellationToken.None).GetAwaiter().GetResult().Tag=="v3.20.1","success broken");});
  Test("untrusted redirect still rejected",delegate {var h=new Handler{Respond=delegate{var r=Response(302,"");r.Headers.Location=new Uri("https://untrusted.example/");return r;}};using(var c=Client(h,new NetworkCooldown())){try{c.GetLatestAsync("x64",CancellationToken.None).GetAwaiter().GetResult();throw new Exception("redirect allowed");}catch(InvalidDataException){}}Assert(h.Calls==1,"contacted untrusted destination");});
  Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
 }
}
