param()
$ErrorActionPreference='Stop'
$katalogRepo=Split-Path -Parent $PSScriptRoot
$katalogProby=Join-Path $katalogRepo ('build\smoke-'+[Guid]::NewGuid().ToString('N'))
$katalogAplikacji=Join-Path $katalogProby 'aplikacja'
$exe=Join-Path $katalogAplikacji 'MapaDyskow.exe'
$proces=$null
function Sprawdz-Uruchomienie {
    $proces=Start-Process -FilePath $exe -WorkingDirectory $katalogRepo -WindowStyle Hidden -PassThru
    try{
        $zegar=[Diagnostics.Stopwatch]::StartNew()
        do{
            Start-Sleep -Milliseconds 200
            $proces.Refresh()
            if($proces.HasExited){throw ('Aplikacja zakończyła się przed otwarciem okna: '+$proces.ExitCode)}
        }while($proces.MainWindowHandle -eq 0 -and $zegar.ElapsedMilliseconds -lt 15000)
        if($proces.MainWindowHandle -eq 0){throw 'Nie powstało główne okno aplikacji.'}
        Start-Sleep -Seconds 3
        $proces.Refresh()
        if($proces.HasExited){throw 'Aplikacja zakończyła się zaraz po starcie.'}
        foreach($nazwa in @('blad-uruchomienia.log','blad-krytyczny.log','bledy.log')){
            if(Test-Path -LiteralPath (Join-Path $katalogAplikacji $nazwa)){throw ('Błąd aplikacji: '+$nazwa)}
        }
    }finally{
        if(-not $proces.HasExited){$null=$proces.CloseMainWindow();if(-not $proces.WaitForExit(5000)){$proces.Kill();throw 'Aplikacja nie zamknęła się poprawnie.'}}
        $proces.Dispose()
    }
}
function Uruchom-Kontrole([string[]]$Argumenty){
    $proces=Start-Process -FilePath $exe -ArgumentList $Argumenty -WorkingDirectory $katalogRepo -WindowStyle Hidden -PassThru
    try{
        if(-not $proces.WaitForExit(30000)){$proces.Kill();throw 'Przekroczono czas kontroli indeksu/skanu.'}
        if($proces.ExitCode -ne 0){throw ('Kontrola zakończyła się kodem '+$proces.ExitCode)}
    }finally{$proces.Dispose()}
}
& (Join-Path $PSScriptRoot 'buduj.ps1') -KatalogDocelowy $katalogAplikacji
foreach($nazwa in @('MapaDyskow.exe','MapaDyskow.exe.config','konfiguracja.json','kompilacja.json','dane','dane\miniatury')){
    if(-not (Test-Path -LiteralPath (Join-Path $katalogAplikacji $nazwa))){throw ('Brakuje: '+$nazwa)}
}
if(@(Get-ChildItem -LiteralPath (Join-Path $katalogAplikacji 'dane') -Recurse -File).Count -ne 0){throw 'Build skopiował dane do pustej instalacji.'}
$konfiguracja=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'konfiguracja.json') -Raw | ConvertFrom-Json
if([IO.Path]::IsPathRooted($konfiguracja.plikBazy)){throw 'Ścieżka bazy nie jest względna.'}
[xml]$konfiguracjaRuntime=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'MapaDyskow.exe.config') -Raw
if($konfiguracjaRuntime.configuration.startup.supportedRuntime.sku -ne '.NETFramework,Version=v4.8'){throw 'Niepoprawny runtime.'}
Sprawdz-Uruchomienie
Uruchom-Kontrole @('--test')
$indeks=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'test-indeks.json') -Raw | ConvertFrom-Json
if($indeks.pliki -ne 0 -or $indeks.foldery -ne 0 -or $indeks.integralnosc -ne 'ok'){throw 'Nowy indeks nie jest pusty lub poprawny.'}
Sprawdz-Uruchomienie
$katalogDanych=Join-Path $katalogProby 'dane-sztuczne'
New-Item -ItemType Directory -Path $katalogDanych | Out-Null
[IO.File]::WriteAllText((Join-Path $katalogDanych 'proba.txt'),'Dane wyłącznie smoke testu.')
Uruchom-Kontrole @('--test-skan',('"'+$katalogDanych+'"'))
Uruchom-Kontrole @('--test')
$indeks=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'test-indeks.json') -Raw | ConvertFrom-Json
if($indeks.pliki -ne 1 -or $indeks.foldery -ne 1 -or $indeks.integralnosc -ne 'ok'){throw 'Pierwszy skan nie działa na nowym indeksie.'}
$skrotBazy=(Get-FileHash -LiteralPath (Join-Path $katalogAplikacji $konfiguracja.plikBazy)).Hash
$skrotKonfiguracji=(Get-FileHash -LiteralPath (Join-Path $katalogAplikacji 'konfiguracja.json')).Hash
& (Join-Path $PSScriptRoot 'buduj.ps1') -KatalogDocelowy $katalogAplikacji
if((Get-FileHash -LiteralPath (Join-Path $katalogAplikacji $konfiguracja.plikBazy)).Hash -ne $skrotBazy -or (Get-FileHash -LiteralPath (Join-Path $katalogAplikacji 'konfiguracja.json')).Hash -ne $skrotKonfiguracji){throw 'Ponowny build zmienił indeks lub konfigurację.'}
Sprawdz-Uruchomienie
$wynik=[ordered]@{sukces=$true;katalog=$katalogAplikacji;pusty_start=$true;ponowny_start=$true;pierwszy_skan=$true;build_zachowuje_dane=$true}
$wynik | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $katalogProby 'wynik.json') -Encoding UTF8
Write-Output ($wynik | ConvertTo-Json)
