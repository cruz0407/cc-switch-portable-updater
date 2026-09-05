using System;
using System.Text.RegularExpressions;
using CCSwitchUpdater;
class MarkdownTests {
 static int p,f; static void Test(string n,Action a){try{a();p++;Console.WriteLine("PASS "+n);}catch(Exception e){f++;Console.WriteLine("FAIL "+n+": "+e.Message);}}
 static void Assert(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(){
  Test("renders headings bold bullets blockquotes and links",delegate{string h=Markdown.ToHtml("# CC Switch v3.20.1\n\n> Important **security** note.\n\n- keep `portable.ini`\n- [release notes](https://github.com/farion1231/cc-switch/releases)"); Assert(h.Contains("<h1>CC Switch v3.20.1</h1>"),"heading"); Assert(h.Contains("<strong>security</strong>"),"bold"); Assert(h.Contains("<blockquote>Important <strong>security</strong> note.</blockquote>"),"quote"); Assert(h.Contains("<li>keep <code>portable.ini</code></li>"),"list"); Assert(h.Contains("rel=\"noreferrer\""),"link safety"); });
  Test("escapes HTML and rejects unsafe links",delegate{string h=Markdown.ToHtml("<script>alert(1)</script>\n\n[x](javascript:alert(1))"); Assert(!h.Contains("<script>"),"raw html"); Assert(h.Contains("&lt;script&gt;"),"escaped html"); Assert(!h.Contains("javascript:"),"unsafe url"); });
  Test("handles empty and multiline paragraphs",delegate{string h=Markdown.ToHtml("first line\nsecond line\n\nthird"); Assert(h.Contains("<p>first line<br>second line</p>"),"line break"); Assert(h.Contains("<p>third</p>"),"paragraph"); Assert(Markdown.ToDocument("# x").Contains("<style>"),"document"); });
  Test("Windows newlines do not split one paragraph",delegate { Assert(Markdown.ToHtml("first\r\nsecond")=="<p>first<br>second</p>","CRLF became a blank line"); });
  Test("formatting never modifies URLs containing underscores",delegate { string h=Markdown.ToHtml("[notes](https://example.com/a_b_c?q=x_y_z)"); Assert(h.Contains("href=\"https://example.com/a_b_c?q=x_y_z\""),"underscore changed URL or attributes"); });
  Test("inline code contents are literal",delegate { string h=Markdown.ToHtml("`<script>**hello**</script>`"); Assert(h.Contains("<code>&lt;script&gt;**hello**&lt;/script&gt;</code>"),"inline code interpreted"); });
  Test("fenced code preserves blank lines and quotes",delegate { string h=Markdown.ToHtml("```csharp\nvar a=\"<tag>\";\n\n# literal\n```"); Assert(h.Contains("# literal"),"missing code"); Assert(!h.Contains("<h1>"),"code heading rendered"); Assert(!h.Contains("<tag>"),"raw code injected"); });
  Test("numbered list and horizontal rule render",delegate { string h=Markdown.ToHtml("1. first\n2. second\n\n---"); Assert(h.Contains("<ol>"),"ordered list"); Assert(h.Contains("<hr>"),"rule"); });
  Test("images cannot fetch remote resources",delegate { string h=Markdown.ToDocument("![image](https://example.com/tracker.png)"); Assert(!h.Contains("<img"),"remote image"); });
  Test("release notes link in bold stays clickable without raw syntax",delegate { string h=Markdown.ToHtml("**[English →](https://github.com/example/notes-en.md) | [日本語版 →](https://github.com/example/notes-ja.md)**"); Assert(h.Contains("<strong><a "),"bold link"); Assert(!h.Contains("**"),"unrendered markers"); });
  Test("table header and cells render",delegate { string h=Markdown.ToHtml("| File | Action |\n| --- | --- |\n| `portable.ini` | keep |"); Assert(h.Contains("<table>"),"table"); Assert(h.Contains("<th>File</th>"),"header"); Assert(h.Contains("<td><code>portable.ini</code></td>"),"cell"); });
  Console.WriteLine("RESULT "+p+" passed, "+f+" failed"); Environment.ExitCode=f==0?0:1;
 }
}
