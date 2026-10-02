param([Parameter(Mandatory=$true)][string]$KatalogDocelowy,[ValidateSet('Debug','Release')][string]$Tryb='Release')
$ErrorActionPreference = 'Stop'
$katalogZrodel = $PSScriptRoot
$katalogAplikacji = [IO.Path]::GetFullPath($KatalogDocelowy)
if($katalogAplikacji.TrimEnd('\') -eq (Split-Path -Parent $katalogZrodel).TrimEnd('\') -or $katalogAplikacji.TrimEnd('\') -eq $katalogZrodel.TrimEnd('\')){throw 'Wybierz osobny katalog wynikowy, np. build\manual.'}
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$kompilator = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $kompilator)) { throw 'Wymagany jest Windows x64 z .NET Framework 4.8 lub 4.8.1.' }
New-Item -ItemType Directory -Path $katalogAplikacji -Force | Out-Null
foreach($katalog in @('dane','dane\miniatury')){New-Item -ItemType Directory -Path (Join-Path $katalogAplikacji $katalog) -Force | Out-Null}
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
$czytnik=[IO.BinaryReader]::new([IO.File]::OpenRead($exe))
try{$czytnik.BaseStream.Position=0x3c;$naglowek=$czytnik.ReadInt32();$czytnik.BaseStream.Position=$naglowek;if($czytnik.ReadUInt32() -ne 0x4550 -or $czytnik.ReadUInt16() -ne 0x8664){throw 'Nie powstała aplikacja x64.'}}finally{$czytnik.Dispose()}
Copy-Item -LiteralPath (Join-Path $katalogZrodel 'MapaDyskow.exe.config') -Destination $katalogAplikacji
$konfiguracja=Join-Path $katalogAplikacji 'konfiguracja.json'
if(-not (Test-Path -LiteralPath $konfiguracja)){Copy-Item -LiteralPath (Join-Path $katalogZrodel 'konfiguracja.json') -Destination $konfiguracja}
$stan=[ordered]@{
    utc=[DateTime]::UtcNow.ToString('o')
    wersja=$metadane.Version.ToString()
    architektura='Amd64'
    sha256_exe=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    zrodla=@(Get-ChildItem -LiteralPath $katalogZrodel -File | Where-Object Extension -in @('.cs','.xaml','.manifest') | ForEach-Object {
        [ordered]@{plik=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
[IO.File]::WriteAllText((Join-Path $katalogAplikacji 'kompilacja.json'),($stan | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($true))
Write-Output ('Gotowe: ' + (Join-Path $katalogAplikacji 'MapaDyskow.exe'))
