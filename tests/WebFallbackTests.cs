using System;
using System.IO;
using System.Threading;
using CCSwitchUpdater;
class WebFallbackTests {
 static int p,f;static void Assert(bool b,string m){if(!b)throw new Exception(m);}static void T(string n,Action a){try{a();p++;Console.WriteLine("PASS "+n);}catch(Exception e){f++;Console.WriteLine("FAIL "+n+": "+e.Message);}}
 static void Main(){string html="<a href=\"/farion1231/cc-switch/releases/download/v3.20.3/CC-Switch-v3.20.3-Windows-Portable.zip\"><span>sha256:9469d3500677c3a7a3c7cf3c5ea0ac1d260087182a890bb73b1526ff289124a0</span><span>13 MB</span></a>";
  T("official HTML x64 fallback selects exact asset and digest",delegate{var r=GitHubWebFallback.Parse(html,"v3.20.3","x64");Assert(r.AssetName=="CC-Switch-v3.20.3-Windows-Portable.zip","asset");Assert(r.DownloadUrl.EndsWith(r.AssetName),"url");Assert(r.Digest.StartsWith("sha256:"),"digest");Assert(r.Size==0,"unknown page size stays deferred");});
  T("reject HTML without exact asset or digest",delegate{try{GitHubWebFallback.Parse("<html>blocked</html>","v3.20.3","x64");throw new Exception("accepted");}catch(InvalidDataException){}});
  T("download size can be learned from Content-Length when page omits size",delegate{Assert(GitHubWebFallback.CanVerifyAfterDownload(13607261,"sha256:abc",13607261),"size");});
  Console.WriteLine("RESULT "+p+" passed, "+f+" failed");Environment.ExitCode=f==0?0:1;}
}
