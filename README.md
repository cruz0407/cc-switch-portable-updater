# CC Switch 便携版更新助手

一个用于更新 **CC Switch Windows 便携版** 的轻量级辅助工具。

> 支持 Windows x64 / ARM64 · .NET Framework 4.8 · 中文界面 · 不常驻后台

## 解决什么问题

CC Switch 便携版通常需要手动打开 GitHub 下载新版、解压并替换文件。本工具把检查、下载、校验、备份和替换整合到一个窗口中，减少手动操作和更新出错的风险。

## 主要功能

- 只检查官方 GitHub 稳定版。
- 按 Windows 系统原生架构选择下载包，不会跟随当前装错的 EXE 架构。
- 明确显示系统架构、已安装程序架构和将要下载的目标架构。
- 检测到架构不一致时提示，并支持修复为正确架构。
- 支持同版本重新安装 / 修复；不支持不安全的版本降级。
- 下载后校验文件大小、GitHub SHA-256、ZIP 内容、PE 架构、产品名称和版本号。
- 更新前先下载和校验，之后才请求退出 CC Switch。
- 更新前备份旧程序和数据，只原子替换 `cc-switch.exe`。
- 保留 `portable.ini`、Reasonix 文件、配置和同目录其他文件。
- 支持高 DPI、最小窗口、长路径和长错误提示，避免控件互相遮挡。

## 快速使用

打开项目的 [Releases](https://github.com/cruz0407/cc-switch-portable-updater/releases) 页面，下载：

```text
CCSwitch-Update-Helper.exe
```

把它放在以下两个文件旁边：

```text
cc-switch.exe
portable.ini
```

然后：

1. 双击 `CCSwitch-Update-Helper.exe`。
2. 点击 **检查更新**。
3. 有新版本时点击 **下载并更新**。
4. 同版本需要覆盖修复时，点击 **重新安装 / 修复**。
5. 按提示从系统托盘正常退出 CC Switch。
6. 等待备份和替换完成，助手会尝试重新启动 CC Switch。

## 安全说明

- 本工具不会强制结束 CC Switch 进程。
- 下载失败、校验失败、文件占用或备份失败时，不会先删除旧程序。
- 只访问官方 GitHub API 和官方发布附件地址，使用 Windows 系统代理设置。
- 更新前会备份旧程序和应用数据。
- 备份位置：`%LOCALAPPDATA%\CCSwitchUpdateHelper\backups`。
- 备份可能包含 API 密钥、数据库和其他敏感信息，请不要公开分享。
- 新版本可能会迁移数据库，因此工具不会自动把数据库降级回旧版本。
- 工具未进行代码签名。首次运行时请核对 Release 中的 `SHA256SUMS.txt`。
- 本项目是独立社区工具，与 CC Switch 官方项目没有隶属关系。

## Release 文件

每个 Release 通常包含：

| 文件 | 用途 |
|---|---|
| `CCSwitch-Update-Helper.exe` | Windows 直接运行的更新助手 |
| `CCSwitch-Update-Helper-v1.2.0-win-x64-arm64.zip` | 源码和构建文件压缩包 |
| `SHA256SUMS.txt` | Release 文件 SHA-256 校验值 |

## 从源码构建

需要 Windows .NET Framework 4.8 编译环境。不需要 NuGet、Node.js、Python 或其他第三方依赖。

在项目目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

脚本会执行：

- 29 项核心测试；
- 6 项架构和同版本修复回归测试；
- 150% DPI 下的布局测试；
- 最小窗口、长路径、长错误提示测试；
- 125% / 200% 字号与尺寸压力测试。

如果不希望覆盖正在运行的助手，可以输出到单独目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory .\dist
```

## 已知限制

- 只支持官方 x64 / ARM64 Windows 便携包。
- 不支持 MSI、预发布版、自动降级和符号链接 / 目录联接安装路径。
- 未签名，可能会触发 Windows 或安全软件的提醒。
- 实际更新前请关闭其他正在运行的 CC Switch 实例。
- 如果更新过程中断电，请先保留备份，不要直接覆盖数据库或盲目降级。

## 许可证

当前版本作为独立社区工具发布。使用前请同时遵守 CC Switch 及其依赖项目的相关许可条款。
