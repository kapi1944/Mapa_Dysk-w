<#
.SYNOPSIS
Buduje launcher i testuje go wyłącznie na kopii źródeł i sztucznych danych w build\testy-launchera.
#>
param()
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'buduj-launcher.ps1')
$proces=Start-Process -FilePath (Join-Path $PSScriptRoot 'MapaDyskow-Launcher.exe') -ArgumentList '--testy' -PassThru -WindowStyle Hidden
try {
    if(-not $proces.WaitForExit(300000)){$proces.Kill();throw 'Testy launchera przekroczyły 5 minut.'}
    $raport=Get-ChildItem -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'build\testy-launchera') -Filter 'wynik.json' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if($raport){Get-Content -LiteralPath $raport.FullName;Write-Output ('Raport: '+$raport.FullName)}
    if($proces.ExitCode -ne 0){throw ('Testy launchera zakończyły się kodem '+$proces.ExitCode)}
} finally {$proces.Dispose()}
