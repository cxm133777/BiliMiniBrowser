$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$version='1.0.4258.31'
$cache=Join-Path $PSScriptRoot 'sdk'
$package=Join-Path $cache 'webview2'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if(!(Test-Path (Join-Path $package 'lib\net462\Microsoft.Web.WebView2.Core.dll'))){
 $archive=Join-Path $cache 'webview2.zip'
 Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $archive
 Expand-Archive -LiteralPath $archive -DestinationPath $package -Force
}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll')){
 Copy-Item -LiteralPath (Join-Path $package "lib\net462\$name") -Destination (Join-Path $PSScriptRoot $name) -Force
}
Copy-Item -LiteralPath (Join-Path $package 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $PSScriptRoot -Force
Write-Host "Restored Microsoft.Web.WebView2 $version"
