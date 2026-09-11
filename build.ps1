param([switch]$Test, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }
if (-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path (Get-Location).ProviderPath $OutputDirectory }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$app = Join-Path $OutputDirectory 'CCSwitch-Update-Helper.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('/r:System.dll','/r:System.Core.dll','/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Net.Http.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
New-Item -ItemType Directory -Force -Path (Join-Path $root 'build') | Out-Null
& $csc /nologo /target:library /out:"$root\build\Helper.Core.dll" @refs "$root\src\Core.cs" "$root\src\Markdown.cs" "$root\src\NetworkSupport.cs"
if ($LASTEXITCODE -ne 0) { throw 'Core build failed' }
if ($Test) {
    & $csc /nologo /target:exe /out:"$root\build\Helper.Tests.exe" @refs /r:"$root\build\Helper.Core.dll" "$root\tests\Tests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & "$root\build\Helper.Tests.exe" "$root\tests\release-sample.json"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & $csc /nologo /target:exe /out:"$root\build\MarkdownTests.exe" @refs /r:"$root\build\Helper.Core.dll" "$root\tests\MarkdownTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Markdown test build failed' }
    & "$root\build\MarkdownTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Markdown tests failed' }
    & $csc /nologo /target:exe /out:"$root\build\NetworkTests.exe" @refs /r:"$root\build\Helper.Core.dll" "$root\tests\NetworkTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Network test build failed' }
    & "$root\build\NetworkTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Network tests failed' }
    & $csc /nologo /target:exe /out:"$root\build\NetworkClientTests.exe" @refs /r:"$root\build\Helper.Core.dll" "$root\src\GitHubClient.cs" "$root\tests\NetworkClientTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Network client test build failed' }
    & "$root\build\NetworkClientTests.exe" "$root\tests\release-sample.json"
    if ($LASTEXITCODE -ne 0) { throw 'Network client tests failed' }

}
if (Test-Path -LiteralPath "$root\src\Program.cs") {
    & $csc /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:"$root\app.manifest" /out:"$app" @refs "$root\src\Core.cs" "$root\src\Markdown.cs" "$root\src\NetworkSupport.cs" "$root\src\GitHubClient.cs" "$root\src\MainForm.cs" "$root\src\NetworkSettingsForm.cs" "$root\src\Program.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
}
if ($Test -and (Test-Path -LiteralPath "$root\src\Program.cs")) {
    if ($app -ne "$root\build\CCSwitch-Update-Helper.exe") { Copy-Item -LiteralPath $app -Destination "$root\build\CCSwitch-Update-Helper.exe" -Force }
    & $csc /nologo /target:exe /out:"$root\build\UiRegression.exe" @refs /r:"$app" "$root\tests\UiRegression.cs"
    if ($LASTEXITCODE -ne 0) { throw 'UI regression build failed' }
    & "$root\build\UiRegression.exe" "$root\tests\release-sample.json"
    if ($LASTEXITCODE -ne 0) { throw 'UI regressions failed' }
    & $csc /nologo /target:exe /win32manifest:"$root\app.manifest" /out:"$root\build\StartupTests.exe" @refs /r:$app "$root\tests\StartupTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Startup tests build failed' }
    & "$root\build\StartupTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Startup tests failed' }
    & $csc /nologo /target:exe /win32manifest:"$root\app.manifest" /out:"$root\build\NetworkUiTests.exe" @refs /r:$app "$root\tests\NetworkUiTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Network UI test build failed' }
    & "$root\build\NetworkUiTests.exe" "$root\build"
    if ($LASTEXITCODE -ne 0) { throw 'Network UI tests failed' }


}

if ($Test -and (Test-Path -LiteralPath $app)) {
    # The DPI-aware test MUST use the same manifest as the shipped executable.
    & $csc /nologo /target:exe /win32manifest:"$root\app.manifest" /out:"$root\build\LayoutRegression.exe" @refs /r:$app "$root\tests\LayoutRegression.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Native DPI layout test build failed' }
    & "$root\build\LayoutRegression.exe" (Split-Path $root -Parent) "$root\build\layout-native.png"
    if ($LASTEXITCODE -ne 0) { throw 'Native DPI layout regression' }
    & $csc /nologo /target:exe /out:"$root\build\LayoutRegression-96.exe" @refs /r:$app "$root\tests\LayoutRegression.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Layout stress test build failed' }
    foreach ($factor in @('1.0','1.25','2.0')) {
        & "$root\build\LayoutRegression-96.exe" (Split-Path $root -Parent) "$root\build\layout-scale-$factor.png" $factor
        if ($LASTEXITCODE -ne 0) { throw "Layout stress regression at $factor" }
    }
}
