param([Parameter(Mandatory=$true)][string]$KatalogDocelowy,[ValidateSet('Debug','Release')][string]$Tryb='Release')
$ErrorActionPreference = 'Stop'
$katalogZrodel = $PSScriptRoot
$katalogAplikacji = [IO.Path]::GetFullPath($KatalogDocelowy)
if($katalogAplikacji.TrimEnd('\') -eq (Split-Path -Parent $katalogZrodel).TrimEnd('\')){throw 'Nie nadpisujemy starego EXE 1.0. Uruchom BUILD-LOCAL.ps1.'}
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$kompilator = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $kompilator)) { throw 'Wymagany jest Windows x64 z .NET Framework 4.8 lub 4.8.1.' }
$argumenty = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/codepage:65001',('/out:' + (Join-Path $katalogAplikacji 'MapaDyskow.exe')),('/resource:' + (Join-Path $katalogZrodel 'Interfejs.xaml') + ',Interfejs.xaml'))
foreach ($biblioteka in @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Numerics.dll','System.Windows.Forms.dll','System.Drawing.dll')) { $argumenty += '/reference:' + (Join-Path $framework $biblioteka) }
foreach ($biblioteka in @('PresentationFramework.dll','PresentationCore.dll','System.Xaml.dll','WindowsBase.dll')) { $sciezka = Join-Path $framework ('WPF\' + $biblioteka); if (-not (Test-Path -LiteralPath $sciezka)) { $sciezka = Join-Path $framework $biblioteka }; $argumenty += '/reference:' + $sciezka }
$argumenty += '/win32manifest:' + (Join-Path $katalogZrodel 'aplikacja.manifest')
if($Tryb -eq 'Debug'){$argumenty += @('/optimize-','/debug:full','/define:DEBUG;TRACE')}else{$argumenty += '/define:TRACE'}
$argumenty += Get-ChildItem -LiteralPath $katalogZrodel -Filter '*.cs' | ForEach-Object { $_.FullName }
& $kompilator @argumenty
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja nie powiodła się.' }
$exe=Join-Path $katalogAplikacji 'MapaDyskow.exe'
$metadane=[Reflection.AssemblyName]::GetAssemblyName($exe)
if($metadane.ProcessorArchitecture -ne 'Amd64'){throw 'Nie powstała aplikacja x64.'}
$stan=[ordered]@{
    utc=[DateTime]::UtcNow.ToString('o')
    wersja=$metadane.Version.ToString()
    architektura=$metadane.ProcessorArchitecture.ToString()
    sha256_exe=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    zrodla=@(Get-ChildItem -LiteralPath $katalogZrodel -File | Where-Object Extension -in @('.cs','.xaml','.manifest') | ForEach-Object {
        [ordered]@{plik=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
[IO.File]::WriteAllText((Join-Path $katalogAplikacji 'kompilacja.json'),($stan | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($true))
Write-Output ('Gotowe: ' + (Join-Path $katalogAplikacji 'MapaDyskow.exe'))
