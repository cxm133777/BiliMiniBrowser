param([string]$Output='BiliMini.exe')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if(!(Test-Path 'Microsoft.Web.WebView2.Core.dll') -or !(Test-Path 'Microsoft.Web.WebView2.Wpf.dll') -or !(Test-Path 'WebView2Loader.dll')) { & "$PSScriptRoot\RestoreWebView2.ps1" }
if(!(Test-Path 'mini.ico')) { & "$PSScriptRoot\BuildIcon.ps1" }
$framework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\PresentationFramework.dll','WPF\PresentationCore.dll','WPF\WindowsBase.dll') | ForEach-Object { '/reference:'+(Join-Path $framework $_) }
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /codepage:65001 "/out:$Output" /win32icon:mini.ico /win32manifest:App.manifest @refs /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.Wpf.dll Browser.cs
if($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built $Output"
