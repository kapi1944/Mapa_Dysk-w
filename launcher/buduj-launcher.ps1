<#
.SYNOPSIS
Jednorazowo buduje launcher w launcher\MapaDyskow-Launcher.exe.
#>
param()
$ErrorActionPreference='Stop'
$katalogFramework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$argumenty=@('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/codepage:65001',('/out:'+(Join-Path $PSScriptRoot 'MapaDyskow-Launcher.exe')),('/win32manifest:'+(Join-Path (Split-Path -Parent $PSScriptRoot) 'src\aplikacja.manifest')))
foreach($biblioteka in @('System.dll','System.Core.dll','System.Web.Extensions.dll')){$argumenty+='/reference:'+(Join-Path $katalogFramework $biblioteka)}
foreach($biblioteka in @('PresentationFramework.dll','PresentationCore.dll','System.Xaml.dll','WindowsBase.dll')){$sciezka=Join-Path $katalogFramework ('WPF\'+$biblioteka);if(-not (Test-Path -LiteralPath $sciezka)){$sciezka=Join-Path $katalogFramework $biblioteka};$argumenty+='/reference:'+$sciezka}
$argumenty+=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName
& (Join-Path $katalogFramework 'csc.exe') @argumenty
if($LASTEXITCODE -ne 0){throw 'Kompilacja launchera nie powiodła się.'}
Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\MapaDyskow.exe.config') -Destination (Join-Path $PSScriptRoot 'MapaDyskow-Launcher.exe.config')
Write-Output (Join-Path $PSScriptRoot 'MapaDyskow-Launcher.exe')
