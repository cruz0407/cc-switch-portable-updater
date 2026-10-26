param([switch]$Test,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'dist'}
if(-not [IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path (Get-Location).ProviderPath $OutputDirectory}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory,"$root\build"|Out-Null
$app=Join-Path $OutputDirectory 'CCSwitch-Update-Helper.exe'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('/r:System.dll','/r:System.Core.dll','/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Net.Http.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
$all=@(Get-ChildItem -LiteralPath "$root\src" -Filter *.cs|ForEach-Object FullName)
$core=@($all|Where-Object {[IO.Path]::GetFileName($_) -notin @('Program.cs','MainForm.cs','NetworkSettingsForm.cs')})
& $csc /nologo /target:library /out:"$root\build\Helper.Core.dll" @refs @core
if($LASTEXITCODE -ne 0){throw 'Core build failed'}
if($Test){
 foreach($name in @('Tests','MarkdownTests','NetworkTests','NetworkClientTests','WebFallbackTests','DownloadTests','RouteTests')){
  & $csc /nologo /target:exe /out:"$root\build\$name.exe" @refs /r:"$root\build\Helper.Core.dll" "$root\tests\$name.cs"
  if($LASTEXITCODE -ne 0){throw "$name build failed"}
  & "$root\build\$name.exe" "$root\tests\release-sample.json"
  if($LASTEXITCODE -ne 0){throw "$name failed"}
 }
}
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:"$root\app.manifest" /out:$app @refs @all
if($LASTEXITCODE -ne 0){throw 'Application build failed'}
if($Test){
 if($app -ne "$root\build\CCSwitch-Update-Helper.exe"){Copy-Item -LiteralPath $app -Destination "$root\build\CCSwitch-Update-Helper.exe" -Force}
 foreach($name in @('UiRegression','StartupTests','NetworkUiTests','AutoRoutesUiTests')){
  & $csc /nologo /target:exe /win32manifest:"$root\app.manifest" /out:"$root\build\$name.exe" @refs /r:$app "$root\tests\$name.cs"
  if($LASTEXITCODE -ne 0){throw "$name build failed"}
  if($name -eq 'NetworkUiTests'){& "$root\build\$name.exe" "$root\build"} elseif($name -eq 'StartupTests'){& "$root\build\$name.exe"} else {& "$root\build\$name.exe" "$root\tests\release-sample.json"}
  if($LASTEXITCODE -ne 0){throw "$name failed"}
 }
 & $csc /nologo /target:exe /win32manifest:"$root\app.manifest" /out:"$root\build\LayoutRegression.exe" @refs /r:$app "$root\tests\LayoutRegression.cs"
 if($LASTEXITCODE -ne 0){throw 'Native layout test build failed'}
 & "$root\build\LayoutRegression.exe" (Split-Path $root -Parent) "$root\build\layout-native.png"
 if($LASTEXITCODE -ne 0){throw 'Native layout test failed'}
 & $csc /nologo /target:exe /out:"$root\build\LayoutRegression-96.exe" @refs /r:$app "$root\tests\LayoutRegression.cs"
 if($LASTEXITCODE -ne 0){throw 'Layout stress test build failed'}
 foreach($factor in @('1.0','1.25','2.0')){
  & "$root\build\LayoutRegression-96.exe" (Split-Path $root -Parent) "$root\build\layout-scale-$factor.png" $factor
  if($LASTEXITCODE -ne 0){throw "Layout stress test failed: $factor"}
 }
}
