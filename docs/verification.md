# Verification — 2026-09-06 (local Asia/Shanghai)

- Compiled with Windows .NET Framework csc.exe; no downloaded compiler or dependencies.
- 29 core behavior tests passed, including real file locks, isolated atomic replacement,
  neighbor preservation, database backup and protected backup ACL.
- Initial RED run: 29 tests failed with NotImplementedException; then implementation made them pass.
- ACL tests require running outside the Codex restricted sandbox; they pass in the ordinary host context.
- Official GitHub API and real portable ZIP download passed; metadata size, SHA256,
  archive layout, executable product name, PE architecture and file version all verified.
- WinForms constructor, local path/version detection and off-screen form render passed;
  rendered image visually inspected after showing the form off-screen to create child handles.
- Existing executable remains 3.20.1 and matches the independently downloaded official executable:
  077B9C9EC89D748A008A3E6E5735A576ADA977FD5FE747EDD9F02DDE82923616.
- Current CC Switch was not stopped, replaced or restarted. Downloaded executable was never executed.
- Not verified: actual in-place user upgrade (already at latest), ARM64 hardware, all DPI settings,
  GUI click-through of every cancellation/error branch, power-loss recovery.
- Reviewed locally: official host allowlist, strict two-file ZIP whitelist, guarded same-volume
  File.Replace without pre-deletion, process rechecks, data backup ACL, no forced process kill.

## v1.1 architecture repair and same-version reinstall
- Reproduced the original UI regressions before the patch: same-version install disabled,
  missing native-architecture target, missing mismatch warning, x86 blocking all checks.
- Fixed target selection to Windows native architecture via IsWow64Process2, with legacy
  GetNativeSystemInfo fallback. Installed PE architecture is displayed separately.
- Allows same-version reinstall and architecture repair; versions older than the installed
  version remain blocked. Rechecks version, architecture and original executable SHA256
  after exit confirmation to catch files changed while the user was deciding.
- Fresh build: 29 core tests + 6 UI regression scenarios pass. Regressions run via build.ps1 -Test.
- Real off-screen UI check against GitHub completed; local and remote 3.20.1,
  button text is “重新安装 / 修复”, Enabled=True. Screenshot visually inspected.
- Existing user cc-switch.exe remains x64 (PE 0x8664), version 3.20.1, SHA256 unchanged.
- No installation or process termination was performed. ARM64 OS hardware remains untested.

## v1.2 DPI/layout correction
- Reproduced the attached screenshot under the shipped DPI-aware manifest at actual 144 DPI:
  title/subtitle overlap, version caption/value overlap, and clipped path rows (5 detected violations).
- Root cause: missing 96-DPI authoring baseline plus fixed pixel rows and absolute child positions.
  Earlier UiSmoke renders were DPI-unaware and did not exercise the shipped manifest behavior.
- Set the 96-DPI baseline before building the form, with SuspendLayout / ResumeLayout.
- Replaced fixed header, version, details, status, button and footer rows with content-sized layout.
  Long paths now have single-line selectable read-only fields with horizontal scrolling and tooltips.
  Status messages wrap; buttons size to text; action/footer rows can wrap; disabled primary button
  uses a muted background. The tab region takes remaining height rather than overlapping actions.
- Caught and fixed a second regression at minimum window size: TabControl.MinimumSize was forcing
  it over the footer. Removed that conflicting minimum, retaining scrollable notes/log content.
- Fresh verification: 29 core + 6 architecture/reinstall regressions pass. Four layout runs each
  pass 321 geometry assertions across default, minimum and minimum-with-long-content cases.
- Actual execution contexts: 144 DPI with the production manifest; virtualized 96 DPI without it.
  125% and 200% are synthetic geometry/font stress tests, not changes to the user's desktop DPI.
- Screenshots reviewed: native-DPI default and minimum/long-content states.
- build.ps1 -Test now includes the manifest-matched layout regression and synthetic stress checks.
  Optional -OutputDirectory allows validating a candidate without overwriting a running helper.
- Deployed v1.2.0.0 to the original helper path using atomic replacement after normal helper exit;
  old helper is backed up. Deployed SHA256 matches tested candidate.
- CC Switch was not closed or modified; its SHA256 remains
  077B9C9EC89D748A008A3E6E5735A576ADA977FD5FE747EDD9F02DDE82923616.
