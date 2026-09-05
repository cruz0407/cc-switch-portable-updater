using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CCSwitchUpdater {
 // Deliberately bounded release-note Markdown subset. No raw HTML or remote media.
 public static class Markdown {
  static string Escape(string value) { return WebUtility.HtmlEncode(value ?? ""); }

  public static bool IsSafeLink(string value) {
   Uri uri;
   return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == "https" &&
    uri.Port == 443 && uri.UserInfo.Length == 0 && !String.IsNullOrEmpty(uri.Host);
  }

  static string Inline(string text, int depth) {
   if (depth > 8) return Escape(text);
   var result = new StringBuilder();
   for (int i = 0; i < text.Length;) {
    char ch = text[i];
    if (ch == '\\' && i + 1 < text.Length) { result.Append(Escape(text.Substring(i + 1, 1))); i += 2; continue; }
    if (ch == '`') {
     int end = text.IndexOf('`', i + 1);
     if (end > i + 1) { result.Append("<code>").Append(Escape(text.Substring(i + 1, end - i - 1))).Append("</code>"); i = end + 1; continue; }
    }
    bool image = ch == '!' && i + 1 < text.Length && text[i + 1] == '[';
    if (ch == '[' || image) {
     int labelStart = i + (image ? 2 : 1), labelEnd = text.IndexOf("](", labelStart, StringComparison.Ordinal);
     if (labelEnd >= 0) {
      int urlStart = labelEnd + 2, end = urlStart, balance = 1;
      for (; end < text.Length; end++) {
       if (text[end] == '(') balance++;
       if (text[end] == ')' && --balance == 0) break;

      }
      if (end < text.Length) {
       string label = text.Substring(labelStart, labelEnd - labelStart), url = text.Substring(urlStart, end - urlStart).Trim();
       if (!image && IsSafeLink(url)) result.Append("<a href=\"").Append(Escape(new Uri(url).AbsoluteUri)).Append("\" rel=\"noreferrer\">").Append(Inline(label, depth + 1)).Append("</a>");
       else result.Append(Inline(label, depth + 1));
       i = end + 1; continue;
      }
     }
    }
    string marker = i + 1 < text.Length ? text.Substring(i, 2) : "";
    string tag = marker == "**" || marker == "__" ? "strong" : marker == "~~" ? "del" : null;
    if (tag == null && (ch == '*' || ch == '_')) { marker = ch.ToString(); tag = "em"; }
    if (tag != null) {
     int end = text.IndexOf(marker, i + marker.Length, StringComparison.Ordinal);
     if (end > i + marker.Length) {
      result.Append('<').Append(tag).Append('>').Append(Inline(text.Substring(i + marker.Length, end - i - marker.Length), depth + 1)).Append("</").Append(tag).Append('>');
      i = end + marker.Length; continue;
     }
    }
    result.Append(Escape(ch.ToString())); i++;
   }
   return result.ToString();
  }

  static string Inline(string text) { return Inline(text, 0); }
  static bool Rule(string line) { return Regex.IsMatch(line.Trim(), @"^(-{3,}|\*{3,}|_{3,})$"); }
  static bool Heading(string line) { return Regex.IsMatch(line, @"^#{1,6}\s+"); }
  static bool ListItem(string line) { return Regex.IsMatch(line, @"^\s*(?:[-*+]|\d+[.)])\s+"); }
  static string[] Cells(string line) { return line.Trim().Trim('|').Split('|'); }
  static bool TableSeparator(string line) {
   string[] cells = Cells(line);
   if (cells.Length < 2) return false;
   foreach (string cell in cells) if (!Regex.IsMatch(cell.Trim(), @"^:?-{3,}:?$")) return false;
   return true;
  }
  static void TableRow(StringBuilder output, string line, string tag) {
   output.Append("<tr>");
   foreach (string cell in Cells(line)) output.Append('<').Append(tag).Append('>').Append(Inline(cell.Trim())).Append("</").Append(tag).Append('>');
   output.Append("</tr>");
  }

  public static string ToHtml(string markdown) {
   if (String.IsNullOrWhiteSpace(markdown)) return "<p class=\"empty\">暂无更新说明。</p>";
   if (markdown.Length > 200000) throw new InvalidOperationException("更新说明超过 200 KB，无法在预览中显示。");
   string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
   var output = new StringBuilder();
   for (int i = 0; i < lines.Length;) {
    string line = lines[i].TrimEnd();
    if (String.IsNullOrWhiteSpace(line)) { i++; continue; }
    if (line.StartsWith("```") || line.StartsWith("~~~")) {
     string fence = line.Substring(0, 3); output.Append("<pre><code>"); i++;
     while (i < lines.Length && !lines[i].StartsWith(fence)) { output.Append(Escape(lines[i])).Append('\n'); i++; }
     if (i < lines.Length) i++; output.Append("</code></pre>"); continue;
    }
    if (Rule(line)) { output.Append("<hr>"); i++; continue; }
    Match heading = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
    if (heading.Success) {
     int level = heading.Groups[1].Value.Length;
     output.Append("<h").Append(level).Append('>').Append(Inline(heading.Groups[2].Value.Trim())).Append("</h").Append(level).Append('>'); i++; continue;
    }
    if (i + 1 < lines.Length && line.Contains("|") && TableSeparator(lines[i + 1])) {
     output.Append("<table><thead>"); TableRow(output, line, "th"); output.Append("</thead><tbody>"); i += 2;
     while (i < lines.Length && lines[i].Contains("|") && !String.IsNullOrWhiteSpace(lines[i])) { TableRow(output, lines[i], "td"); i++; }
     output.Append("</tbody></table>"); continue;
    }
    if (line.TrimStart().StartsWith(">")) {
     output.Append("<blockquote>"); bool first = true;
     while (i < lines.Length && lines[i].TrimStart().StartsWith(">")) {
      if (!first) output.Append("<br>"); first = false;
      output.Append(Inline(lines[i].TrimStart().Substring(1).Trim())); i++;
     }
     output.Append("</blockquote>"); continue;
    }
    if (ListItem(line)) {
     bool ordered = Regex.IsMatch(line, @"^\s*\d+[.)]\s+"); string tag = ordered ? "ol" : "ul";
     output.Append('<').Append(tag).Append('>');
     while (i < lines.Length && ListItem(lines[i]) && Regex.IsMatch(lines[i], @"^\s*\d+[.)]\s+") == ordered) {
      string content = Regex.Replace(lines[i], @"^\s*(?:[-*+]|\d+[.)])\s+", "");
      output.Append("<li>").Append(Inline(content)).Append("</li>"); i++;
     }
     output.Append("</").Append(tag).Append('>'); continue;
    }
    output.Append("<p>").Append(Inline(line.Trim())); i++;
    while (i < lines.Length && !String.IsNullOrWhiteSpace(lines[i]) && !Heading(lines[i]) && !Rule(lines[i]) &&
     !ListItem(lines[i]) && !lines[i].TrimStart().StartsWith(">") && !lines[i].StartsWith("```") && !lines[i].StartsWith("~~~") &&
     !(i + 1 < lines.Length && lines[i].Contains("|") && TableSeparator(lines[i + 1]))) {
     output.Append("<br>").Append(Inline(lines[i].Trim())); i++;
    }
    output.Append("</p>");
   }
   return output.ToString();
  }

  public static string ToDocument(string markdown) {
   return "<!doctype html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta charset=\"utf-8\">" +
    "<style>body{font-family:'Microsoft YaHei UI','Segoe UI',sans-serif;font-size:14px;color:#1f2d41;line-height:1.65;margin:14px 16px;background:#fff;word-wrap:break-word}" +
    "h1{font-size:23px;margin:0 0 16px;color:#173b78}h2{font-size:19px;color:#173b78}h3,h4,h5,h6{font-size:16px;color:#173b78}" +
    "p{margin:0 0 12px}ul,ol{margin:4px 0 14px;padding-left:26px}li{margin:4px 0}" +
    "blockquote{border-left:4px solid #2f5fba;background:#f1f5fb;margin:10px 0 16px;padding:10px 14px;color:#42536b}" +
    "code{background:#eef2f7;padding:2px 4px;font-family:Consolas,monospace}pre{background:#f3f6fb;padding:12px;overflow:auto;white-space:pre-wrap}" +
    "a{color:#2458ad;text-decoration:underline}hr{border:0;border-top:1px solid #dce3ed;margin:18px 0}" +
    "table{border-collapse:collapse;width:100%;margin:10px 0 16px}th,td{border:1px solid #dce3ed;padding:6px 10px;text-align:left}th{background:#f1f5fb}" +
    ".empty{color:#66758a}</style></head><body>" + ToHtml(markdown) + "</body></html>";
  }
 }
}
