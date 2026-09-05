# CC Switch Portable Updater

A small, dependency-free Windows helper for updating the **portable** build of [CC Switch](https://github.com/farion1231/cc-switch).

> Windows x64 / ARM64 · .NET Framework 4.8 · Chinese UI · no background service

## What it does

- Checks the official stable GitHub release only.
- Chooses the package from Windows' native architecture, independently of the currently installed executable.
- Verifies package size, GitHub SHA-256, ZIP contents, PE architecture, product name, and version.
- Downloads and stages before asking CC Switch to exit.
- Backs up the old executable and application data, then atomically replaces only `cc-switch.exe`.
- Preserves `portable.ini`, Reasonix files, settings, and unrelated files.
- Supports same-version reinstall/repair; refuses downgrades.
- Handles high-DPI layouts and long paths without overlapping controls.

## Quick start

Download `CCSwitch-Update-Helper.exe` from the latest release and put it beside:

```text
cc-switch.exe
portable.ini
```

Double-click the helper, choose **检查更新**, then follow the confirmation steps. The helper never force-kills CC Switch and never installs unattended.

## Safety notes

The helper is unsigned. Review the release checksums before running it. It uses only official GitHub API/release URLs and the Windows system proxy. API keys and databases are not uploaded; local backups may contain sensitive credentials.

Backups are stored under `%LOCALAPPDATA%\CCSwitchUpdateHelper\backups`. Do not share them publicly. A database is not automatically rolled back after a newer version may have migrated it.

The helper does not modify the CC Switch executable during build or test. See [`README.zh-CN.md`](README.zh-CN.md) for the full Chinese usage guide and limitations.

## Build and test

Requires the Windows .NET Framework 4.8 compiler available on the build machine. No NuGet packages are required:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

To build into another directory without overwriting a running helper:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory .\dist
```

## Release assets

Each release includes:

- `CCSwitch-Update-Helper.exe` — ready-to-run helper
- `CCSwitch-Update-Helper-v1.2.0-win-x64-arm64.zip` — source and reproducible build files
- `SHA256SUMS.txt` — checksums for release assets

This project is an independent community utility and is not affiliated with the CC Switch project.
