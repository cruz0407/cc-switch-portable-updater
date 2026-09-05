using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CCSwitchUpdater {
 public sealed class Release {
  public string Tag, AssetName, DownloadUrl, Notes, Digest;
  public long Size;
  public Version Version;
 }
 public sealed class GitHubAsset {
  public string name { get; set; } public string browser_download_url { get; set; }
  public string digest { get; set; } public long size { get; set; }
 }
 public sealed class GitHubRelease {
  public string tag_name { get; set; } public bool prerelease { get; set; }
  public bool draft { get; set; } public string body { get; set; }
  public GitHubAsset[] assets { get; set; }
 }
 public static class Core {
  public const string LatestUrl = "https://api.github.com/repos/farion1231/cc-switch/releases/latest";
  public const long MaxPackageSize = 200L * 1024 * 1024;
  [DllImport("kernel32.dll", SetLastError = true)]
  static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);
  [DllImport("kernel32.dll")]
  static extern void GetNativeSystemInfo(out SystemInfo info);
  [StructLayout(LayoutKind.Sequential)]
  struct SystemInfo {
   public ushort Architecture, Reserved; public uint PageSize;
   public IntPtr MinimumAddress, MaximumAddress, ActiveMask;
   public uint ProcessorCount, ProcessorType, AllocationGranularity;
   public ushort ProcessorLevel, ProcessorRevision;
  }
  public static string ReadNativeArchitecture() {
   // Read the OS native machine, not this AnyCPU helper's bitness or user-editable environment variables.
   try {
    ushort processMachine, nativeMachine;
    if (!IsWow64Process2(new IntPtr(-1), out processMachine, out nativeMachine)) throw new IOException("无法读取系统原生架构，Windows 错误：" + Marshal.GetLastWin32Error());
    if (nativeMachine == 0x8664) return "x64";
    if (nativeMachine == 0xaa64) return "arm64";
    throw new InvalidDataException("此系统不是受支持的 x64 / ARM64 Windows。");
   } catch (EntryPointNotFoundException) {
    SystemInfo info; GetNativeSystemInfo(out info);
    if (info.Architecture == 9) return "x64";
    if (info.Architecture == 12) return "arm64";
    throw new InvalidDataException("此系统不是受支持的 x64 / ARM64 Windows。");
   }
  }
  public static Version ParseVersion(string text) {
   string s = (text ?? "").Trim();
   if (!Regex.IsMatch(s, @"^v?\d+\.\d+\.\d+(\.\d+)?$")) throw new InvalidDataException("无法识别稳定版本号：" + s);
   Version v;
   if (!Version.TryParse(s.TrimStart('v'), out v)) throw new InvalidDataException("版本号超出允许范围。");
   return new Version(v.Major, v.Minor, v.Build, v.Revision < 0 ? 0 : v.Revision);
  }
  public static Version ReadVersion(string exe) {
   return ParseVersion(FileVersionInfo.GetVersionInfo(exe).FileVersion);
  }
  public static string ReadArchitecture(string exe) {
   using (var s = File.OpenRead(exe)) using (var r = new BinaryReader(s)) {
    if (s.Length < 64 || r.ReadUInt16() != 0x5a4d) throw new InvalidDataException("程序不是有效的 Windows PE 文件。");
    s.Position = 0x3c; int offset = r.ReadInt32();
    if (offset < 64 || offset > s.Length - 24) throw new InvalidDataException("PE 头位置无效。");
    s.Position = offset;
    if (r.ReadUInt32() != 0x4550) throw new InvalidDataException("PE 签名无效。");
    ushort machine = r.ReadUInt16();
    if (machine == 0x8664) return "x64";
    if (machine == 0xaa64) return "arm64";
    if (machine == 0x014c) return "x86";
    throw new InvalidDataException("无法识别程序架构。");
   }
  }
  public static bool IsAllowedUrl(string url, bool asset) {
   Uri u;
   if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Scheme != "https" || u.Port != 443 || u.UserInfo.Length != 0 || u.Fragment.Length != 0) return false;
   if (!asset) return u.Host == "api.github.com" && u.AbsolutePath == "/repos/farion1231/cc-switch/releases/latest" && u.Query.Length == 0;
   return u.Host == "github.com" && u.AbsolutePath.StartsWith("/farion1231/cc-switch/releases/download/", StringComparison.Ordinal) && u.Query.Length == 0;
  }
  public static Release SelectRelease(string json, string architecture) {
   if (architecture != "x64" && architecture != "arm64") throw new InvalidDataException("第一版仅支持官方 x64 / ARM64 便携包。");
   GitHubRelease r;
   try { r = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<GitHubRelease>(json); }
   catch (Exception e) { throw new InvalidDataException("GitHub 发布信息格式异常。", e); }
   if (r == null || r.prerelease || r.draft || r.assets == null) throw new InvalidDataException("返回的不是可用的正式发布版本。");
   Version v = ParseVersion(r.tag_name);
   string name = "CC-Switch-" + r.tag_name + "-Windows" + (architecture == "arm64" ? "-arm64" : "") + "-Portable.zip";
   var assets = r.assets.Where(a => a != null && a.name == name).ToArray();
   if (assets.Length != 1) throw new InvalidDataException("未找到唯一匹配的官方便携包，已停止，避免下载错误版本。");
   GitHubAsset a0 = assets[0];
   string expectedUrl = "https://github.com/farion1231/cc-switch/releases/download/" + r.tag_name + "/" + name;
   if (!IsAllowedUrl(a0.browser_download_url, true) || a0.browser_download_url != expectedUrl) throw new InvalidDataException("下载地址不属于预期的官方发布包。");
   if (a0.size <= 0 || a0.size > MaxPackageSize) throw new InvalidDataException("发布包大小异常。");
   if (!Regex.IsMatch(a0.digest ?? "", "^sha256:[a-fA-F0-9]{64}$")) throw new InvalidDataException("官方未提供可用的 SHA-256 摘要；为安全起见，暂不更新。");
   return new Release { Tag = r.tag_name, Version = v, AssetName = name, Size = a0.size, Digest = a0.digest.ToLowerInvariant(), DownloadUrl = a0.browser_download_url, Notes = r.body ?? "此版本没有更新说明。" };
  }
  public static string Sha256(string file) {
   using (var s = File.OpenRead(file)) using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
  }
  public static void VerifyPackage(string path, Release release) {
   if (new FileInfo(path).Length != release.Size) throw new InvalidDataException("下载文件大小不匹配，请重新下载。");
   if (!Regex.IsMatch(release.Digest ?? "", "^sha256:[a-fA-F0-9]{64}$") || !String.Equals("sha256:" + Sha256(path), release.Digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 校验失败，文件未被安装，请重新下载。");
  }
  public static string ExtractPackage(string zip, string destination, Release release, string architecture) {
   EnsureNoReparse(destination);
   if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) throw new InvalidDataException("解压目录必须为空。");
   using (var z = ZipFile.OpenRead(zip)) {
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var e in z.Entries) {
     // Current upstream ZIP is exactly two root files. Fail closed if upstream changes layout.
     if ((e.FullName != "cc-switch.exe" && e.FullName != "portable.ini") || !names.Add(e.FullName)) throw new InvalidDataException("便携包含有未知、重复或不安全的文件路径，已停止更新。");
     long limit = e.FullName == "portable.ini" ? 4096 : 256L * 1024 * 1024;
     if (e.Length <= 0 || e.Length > limit) throw new InvalidDataException("解压后的文件大小异常。");
     int unixType = (e.ExternalAttributes >> 16) & 0xf000;
     if (unixType == 0xa000 || (e.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("不允许压缩包包含符号链接。");
    }
    if (names.Count != 2) throw new InvalidDataException("便携包缺少主程序或 portable.ini。");
    Directory.CreateDirectory(destination);
    foreach (var e in z.Entries) {
     string path = Path.Combine(destination, e.FullName);
     using (var source = e.Open()) using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
      byte[] buffer = new byte[65536]; long total = 0; int n;
      while ((n = source.Read(buffer, 0, buffer.Length)) > 0) { total += n; if (total > e.Length) throw new InvalidDataException("压缩包长度不一致。"); target.Write(buffer, 0, n); }
      if (total != e.Length) throw new InvalidDataException("压缩包被截断。");
     }
    }
   }
   string exe = Path.Combine(destination, "cc-switch.exe");
   RequirePortable(destination);
   if (ReadVersion(exe) != release.Version) throw new InvalidDataException("包内程序版本与发布版本不一致。");
   if (ReadArchitecture(exe) != architecture) throw new InvalidDataException("包内程序架构与当前程序不一致。");
   if (!String.Equals(FileVersionInfo.GetVersionInfo(exe).ProductName, "CC Switch", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("包内程序产品名称不是 CC Switch。");
   return exe;
  }
  public static string ResolveDataDirectory(string home, string legacyHome, string storeFile) {
   if (File.Exists(storeFile)) {
    Dictionary<string, object> store;
    try { store = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(storeFile)); }
    catch (Exception e) { throw new InvalidDataException("无法读取 CC Switch 的数据目录设置，请先检查 app_paths.json。", e); }
    if (store == null) throw new InvalidDataException("数据目录设置为空或损坏。");
    object raw;
    if (store.TryGetValue("app_config_dir_override", out raw) && raw != null) {
     string path = raw as string;
     if (path == null) throw new InvalidDataException("自定义数据目录不是文本路径。");
     path = path.Trim();
     if (path == "~") path = home;
     else if (path.StartsWith("~/") || path.StartsWith("~\\")) path = Path.Combine(home, path.Substring(2));
     if (path.Length > 0) {
      if (!Path.IsPathRooted(path)) throw new InvalidDataException("自定义数据目录是相对路径，无法可靠定位，请在 CC Switch 中改为绝对路径。");
      if (Directory.Exists(path)) return Path.GetFullPath(path);
     }
    }
   }
   string normal = Path.Combine(home, ".cc-switch");
   if (!File.Exists(Path.Combine(normal, "cc-switch.db")) && !String.IsNullOrWhiteSpace(legacyHome)) {
    string legacy = Path.Combine(legacyHome.Trim(), ".cc-switch");
    if (Path.IsPathRooted(legacy) && File.Exists(Path.Combine(legacy, "cc-switch.db"))) return Path.GetFullPath(legacy);
   }
   return Path.GetFullPath(normal);
  }
  public static void EnsureNoReparse(string path) {
   string full = Path.GetFullPath(path);
   while (!String.IsNullOrEmpty(full)) {
    if ((Directory.Exists(full) || File.Exists(full)) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("为避免更新到意外位置，不支持符号链接或目录联接：" + full);
    string parent = Path.GetDirectoryName(full);
    if (parent == full) break;
    full = parent;
   }
  }
  public static void RequirePortable(string directory) {
   string p = Path.Combine(directory, "portable.ini");
   EnsureNoReparse(p);
   if (!File.Exists(p) || new FileInfo(p).Length > 4096 || !Regex.IsMatch(File.ReadAllText(p), @"(?im)^\s*portable\s*=\s*true\s*$")) throw new InvalidDataException("目标目录没有有效的 portable.ini，拒绝修改非便携安装。");
  }
  public static void CreatePrivateDirectory(string path) {
   EnsureNoReparse(path);
   var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
   var user = WindowsIdentity.GetCurrent().User;
   acl.SetOwner(user);
   foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
    acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
   if (!Directory.Exists(path)) Directory.CreateDirectory(path, acl);
   else new DirectoryInfo(path).SetAccessControl(acl);
  }
  static bool Inside(string path, string parent) {
   string p = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
   string q = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
   return q.StartsWith(p, StringComparison.OrdinalIgnoreCase);
  }
  static void CopyData(string source, string destination, ref long remaining, bool top) {
   EnsureNoReparse(source); Directory.CreateDirectory(destination);
   foreach (string f in Directory.GetFiles(source)) {
    EnsureNoReparse(f);
    using (var input = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read)) using (var output = new FileStream(Path.Combine(destination, Path.GetFileName(f)), FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
     remaining -= input.Length;
     if (remaining < 0) throw new InvalidDataException("数据备份超过 2 GB，已停止更新，请先整理数据目录。");
     input.CopyTo(output); output.Flush(true);
    }
   }
   foreach (string d in Directory.GetDirectories(source)) {
    string name = Path.GetFileName(d);
    if (top && (name.Equals("logs", StringComparison.OrdinalIgnoreCase) || name.Equals("backups", StringComparison.OrdinalIgnoreCase) || name.Equals("skill-backups", StringComparison.OrdinalIgnoreCase))) continue;
    CopyData(d, Path.Combine(destination, name), ref remaining, false);
   }
  }
  public static string Install(string stagedExe, string targetDirectory, string dataDirectory, string backupRoot, Action ensureStopped = null) {
   targetDirectory = Path.GetFullPath(targetDirectory); dataDirectory = Path.GetFullPath(dataDirectory);
   EnsureNoReparse(targetDirectory); EnsureNoReparse(stagedExe); EnsureNoReparse(dataDirectory); EnsureNoReparse(backupRoot);
   RequirePortable(targetDirectory);
   if (!Directory.Exists(dataDirectory)) throw new InvalidDataException("未找到数据目录，无法确认备份范围，已停止更新。");
   if (Inside(backupRoot, dataDirectory) || Inside(dataDirectory, backupRoot)) throw new InvalidDataException("备份目录不能与数据目录重叠。");
   string target = Path.Combine(targetDirectory, "cc-switch.exe"); EnsureNoReparse(target);
   string incoming = Path.Combine(targetDirectory, ".cc-switch-update-" + Guid.NewGuid().ToString("N") + ".tmp");
   string backup = Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0,8));
   CreatePrivateDirectory(backupRoot); CreatePrivateDirectory(backup);
   try {
    // All work until File.Replace is staging/backup only. The target is never deleted first.
    File.Copy(target, Path.Combine(backup, "cc-switch.exe"), false);
    File.Copy(Path.Combine(targetDirectory, "portable.ini"), Path.Combine(backup, "portable.ini"), false);
    long remaining = 2L * 1024 * 1024 * 1024;
    if (ensureStopped != null) ensureStopped();
    CopyData(dataDirectory, Path.Combine(backup, "data"), ref remaining, true);
    string oldHash = Sha256(Path.Combine(backup, "cc-switch.exe")); string newHash = Sha256(stagedExe);
    File.Copy(stagedExe, incoming, false);
    using (var f = new FileStream(incoming, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) f.Flush(true);
    if (Sha256(incoming) != newHash || Sha256(target) != oldHash) throw new InvalidDataException("替换前文件发生变化，已停止更新。");
    string record = "CC Switch 更新备份\r\n目标：" + target + "\r\n数据来源：" + dataDirectory + "\r\n旧程序 SHA256：" + oldHash + "\r\n新程序 SHA256：" + newHash + "\r\n" +
     "备份不包括顶层 logs / backups / skill-backups。data 中包含数据库、设置及其他当前数据。\r\n" +
     "如需手动恢复：先退出所有 CC Switch，再用这里的 cc-switch.exe 替换目标程序。\r\n" +
     "如果新版已经启动，可能迁移了数据库；不要盲目恢复旧程序或单独覆盖数据库。请保留现有数据，再由熟悉数据库恢复的人处理整套 data（含 WAL/SHM）。\r\n" +
     "状态记录用于排查；停电或强制退出时请核对目标文件 SHA256，不能仅凭状态判断。\r\n";
    File.WriteAllText(Path.Combine(backup, "恢复说明.txt"), record, Encoding.UTF8);
    File.WriteAllText(Path.Combine(backup, "state.txt"), "prepared", Encoding.UTF8);
    if (ensureStopped != null) ensureStopped();
    File.Replace(incoming, target, null, false);
    // A bookkeeping failure must not misreport an already committed executable replacement as failure.
    try { File.WriteAllText(Path.Combine(backup, "state.txt"), "installed", Encoding.UTF8); } catch (IOException) {} catch (UnauthorizedAccessException) {}
    return backup;
   } finally {
    if (File.Exists(incoming)) { try { File.Delete(incoming); } catch (IOException) {} catch (UnauthorizedAccessException) {} }
   }
  }
 }
}


