using System;
using System.IO;
using System.Net;
using System.Net.Http;
using CCSwitchUpdater;
class NetworkTests {
 static int passed,failed;
 static DateTimeOffset Now=new DateTimeOffset(2026,9,11,12,0,0,TimeSpan.Zero);
 static void Assert(bool yes,string why){if(!yes)throw new Exception(why);}
 static void Test(string name,Action work){try{work();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Invalid(Action work){try{work();}catch(InvalidDataException){return;}throw new Exception("invalid settings accepted");}
 static HttpResponseMessage Response(int code){return new HttpResponseMessage((HttpStatusCode)code);}
 static void Main(){
  Test("primary quota displays exact reset and remaining without leaking IP",delegate {using(var r=Response(403)){r.Headers.Add("X-RateLimit-Remaining","0");r.Headers.Add("X-RateLimit-Limit","60");r.Headers.Add("X-RateLimit-Reset",Now.AddMinutes(20).ToUnixTimeSeconds().ToString());var e=GitHubFailure.Describe(r,"{\"message\":\"API rate limit exceeded for 1.2.3.4\"}",Now);Assert(e.Kind=="primary-rate-limit","wrong kind");Assert(e.RetryAt==Now.AddMinutes(20),"reset lost");Assert(e.Message.Contains("0 / 60"),"quota not shown");Assert(!e.Message.Contains("1.2.3.4"),"IP leak");}});
  Test("secondary Retry-After seconds respected",delegate {using(var r=Response(403)){r.Headers.TryAddWithoutValidation("Retry-After","120");var e=GitHubFailure.Describe(r,"{\"message\":\"secondary rate limit\"}",Now);Assert(e.Kind=="secondary-rate-limit","wrong secondary kind");Assert(e.RetryAt==Now.AddSeconds(120),"wrong wait");}});
  Test("429 date Retry-After supported",delegate {using(var r=Response(429)){r.Headers.TryAddWithoutValidation("Retry-After",Now.AddMinutes(3).ToString("r"));Assert(GitHubFailure.Describe(r,"{}",Now).RetryAt==Now.AddMinutes(3),"date ignored");}});
  Test("403 permission denial is not falsely called quota exhaustion",delegate {using(var r=Response(403)){r.Headers.Add("X-RateLimit-Remaining","42");var e=GitHubFailure.Describe(r,"{\"message\":\"Resource not accessible\"}",Now);Assert(e.Kind=="forbidden","misdiagnosis");Assert(e.RetryAt==null,"invented reset");Assert(e.Message.Contains("不能仅凭 403"),"missing uncertainty");}});
  Test("HTML 403 described as possible gateway denial without rendering server HTML",delegate {using(var r=Response(403)){var e=GitHubFailure.Describe(r,"<html><script>secret</script>blocked</html>",Now);Assert(e.Kind=="gateway-denied","wrong gateway class");Assert(!e.Message.Contains("secret"),"raw body leak");}});
  Test("401 and 407 have distinct guidance",delegate {using(var a=Response(401))using(var b=Response(407)){Assert(GitHubFailure.Describe(a,"{}",Now).Kind=="unauthorized","401 wrong");Assert(GitHubFailure.Describe(b,"{}",Now).Kind=="proxy-auth","407 wrong");}});
  Test("502 is upstream failure not rate limit",delegate {using(var r=Response(502)){var e=GitHubFailure.Describe(r,"bad gateway",Now);Assert(e.Kind=="upstream","502 wrong");Assert(e.RetryAt==null,"unexpected wait");}});
  Test("malformed headers cannot crash error classification",delegate {using(var r=Response(429)){r.Headers.TryAddWithoutValidation("Retry-After","bad");r.Headers.TryAddWithoutValidation("X-RateLimit-Reset","999999999999999999999");var e=GitHubFailure.Describe(r,"not json",Now);Assert(e.RetryAt==Now.AddMinutes(1),"must default to one minute");}});
  Test("cooldown blocks attempts until the allowed time",delegate {var gate=new NetworkCooldown();gate.Record(new GitHubNetworkException("primary-rate-limit","wait",Now.AddMinutes(2)));try{gate.Check(Now);throw new Exception("request allowed too soon");}catch(GitHubNetworkException){}gate.Check(Now.AddMinutes(2));});
  Test("later errors cannot shorten active cooldown",delegate {var gate=new NetworkCooldown();gate.Record(new GitHubNetworkException("primary","wait",Now.AddMinutes(5)));gate.Record(new GitHubNetworkException("secondary","wait",Now.AddMinutes(1)));Assert(gate.Until==Now.AddMinutes(5),"shortened wait");});
  Test("default uses system proxy",delegate {using(var h=new NetworkSettings().CreateHandler()){Assert(h.UseProxy,"system proxy disabled");Assert(h.Proxy==null,"custom proxy leaked into system mode");Assert(!h.AllowAutoRedirect,"redirect validation bypassed");}});
  Test("direct explicitly disables system proxy",delegate {using(var h=new NetworkSettings{Mode="direct"}.CreateHandler()){Assert(!h.UseProxy,"direct still uses proxy");}});
  Test("custom HTTP proxy is configured and GitHub URL stays HTTPS",delegate {var s=new NetworkSettings{Mode="custom",ProxyAddress="http://127.0.0.1:7890"};using(var h=s.CreateHandler()){Assert(h.UseProxy,"proxy disabled");Assert(h.Proxy.GetProxy(new Uri(Core.LatestUrl)).AbsoluteUri=="http://127.0.0.1:7890/","wrong proxy target");}});
  Test("reject credentials, SOCKS, query, path and reverse-proxy URLs",delegate {foreach(var url in new[]{"http://user:pass@127.0.0.1:7890","socks5://127.0.0.1:7890","http://localhost:7890/?token=secret","https://relay.example/https://api.github.com","file:///tmp/proxy"})Invalid(delegate {new NetworkSettings{Mode="custom",ProxyAddress=url}.Validate();});});
  Test("unknown mode rejected",delegate {Invalid(delegate {new NetworkSettings{Mode="magic"}.Validate();});});
  Test("settings round trip and default missing file",delegate {string root=Path.Combine(Path.GetTempPath(),"ccswitch-network-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string path=Path.Combine(root,"network.json");Assert(NetworkSettings.Load(path).Mode=="system","bad default");new NetworkSettings{Mode="custom",ProxyAddress="http://127.0.0.1:7890"}.Save(path);Assert(NetworkSettings.Load(path).ProxyAddress=="http://127.0.0.1:7890","lost config");new NetworkSettings{Mode="direct"}.Save(path);Assert(NetworkSettings.Load(path).Mode=="direct","second save failed");});
  Test("corrupt settings fail visibly not silently change route",delegate {string path=Path.GetTempFileName();File.WriteAllText(path,"broken");Invalid(delegate {NetworkSettings.Load(path);});});
  Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");Environment.ExitCode=failed==0?0:1;
 }
}
