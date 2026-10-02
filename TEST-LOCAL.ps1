<#
.SYNOPSIS
Uruchamia Debug build, testy Debug/WPF, Release build i testy Release/WPF.
.DESCRIPTION
Bez parametrów i pytań. Kompilacje: build\testy\Debug i build\testy\Release.
Każdy test tworzy nowy katalog build\testy\uruchomienie-* i sztuczne dane.
Wymaga Windows x64 z .NET Framework 4.8 oraz sesji pulpitu dla WPF.
#>
param()
$ErrorActionPreference='Stop'
foreach($tryb in @('Debug','Release')){
    $katalog=Join-Path $PSScriptRoot ('build\testy\'+$tryb)
    Write-Host ('Kompilacja '+$tryb)
    & (Join-Path $PSScriptRoot 'src\buduj.ps1') -KatalogDocelowy $katalog -Tryb $tryb
    Write-Host ('Testy '+$tryb+' (logika, operacje plikowe, GUI/WPF, indeks, zrzuty)')
    & (Join-Path $PSScriptRoot 'src\testuj.ps1') -KatalogDocelowy $katalog
}
Write-Host 'PASS: Debug i Release.'
