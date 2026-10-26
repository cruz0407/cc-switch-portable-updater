using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CCSwitchUpdater;
class WebFallbackTests {
 static int passed,failed; static string A=new string('a',64),B=new string('b',64);
 static void Assert(bool b,string why){if(!b)throw new Exception(why);}
 static void Test(string name,Action run){try{run();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static string Item(string suffix,string digest){string name="CC-Switch-v3.20.3-Windows"+suffix+"-Portable.zip";return "<li><a href=\"/farion1231/cc-switch/releases/download/v3.20.3/"+name+"\">"+name+"</a><span>sha256:"+digest+"</span></li>";}
 static void Reject(Action run){try{run();}catch(InvalidDataException){return;}throw new Exception("invalid page accepted");}
 static int Main(){
  Test("x64 digest comes from matching asset row not first digest",delegate{var r=GitHubWebFallback.Parse(Item("-arm64",A)+Item("",B),"v3.20.3","x64");Assert(r.Digest=="sha256:"+B,"wrong file digest");});
  Test("ARM64 chooses its own row",delegate{Assert(GitHubWebFallback.Parse(Item("",B)+Item("-arm64",A),"v3.20.3","arm64").Digest=="sha256:"+A,"wrong ARM64 hash");});
  Test("reject malformed or prerelease tag",delegate{Reject(delegate{GitHubWebFallback.Parse(Item("",B),"v3.20.3-rc1","x64");});});
  Test("reject digest only on neighboring asset",delegate{Reject(delegate{GitHubWebFallback.Parse(Item("-arm64",A)+Item("","missing"),"v3.20.3","x64");});});
  Test("reject duplicate asset rows",delegate{Reject(delegate{GitHubWebFallback.Parse(Item("",B)+Item("",B),"v3.20.3","x64");});});
  Test("reject untrusted href",delegate{Reject(delegate{GitHubWebFallback.Parse(Item("",B).Replace("href=\"/farion1231","href=\"https://evil.example/farion1231"),"v3.20.3","x64");});});
  Test("unknown precision page size stays unknown until verified download",delegate{var r=GitHubWebFallback.Parse(Item("",B)+"13 MB","v3.20.3","x64");Assert(r.Size==0,"rounded MB trusted as exact size");});
  Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
 }
}
