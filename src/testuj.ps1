<#
.SYNOPSIS
Testuje kompilację na izolowanej kopii aplikacji pod build\testy.
.PARAMETER KatalogDocelowy
Katalog kompilacji do odczytu. Domyślnie build\testy\Release względem repo.
#>
param([string]$KatalogDocelowy=(Join-Path (Split-Path -Parent $PSScriptRoot) 'build\testy\Release'))
$ErrorActionPreference='Stop'
$katalogAplikacji=[IO.Path]::GetFullPath($KatalogDocelowy)
$exe=Join-Path $katalogAplikacji 'MapaDyskow.exe'
if(-not (Test-Path -LiteralPath $exe)){throw 'Najpierw uruchom TEST-LOCAL.ps1 lub buduj.ps1.'}
$metadaneKompilacji=Join-Path $katalogAplikacji 'kompilacja.json'
if(-not (Test-Path -LiteralPath $metadaneKompilacji)){throw 'Brakuje potwierdzenia kompilacji. Uruchom buduj.ps1.'}
$kompilacja=Get-Content -LiteralPath $metadaneKompilacji -Raw -Encoding UTF8 | ConvertFrom-Json
if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $kompilacja.sha256_exe){throw 'EXE różni się od potwierdzonej kompilacji.'}
foreach($plik in $kompilacja.zrodla){$sciezka=Join-Path $PSScriptRoot $plik.plik;if(-not (Test-Path -LiteralPath $sciezka)){throw ('Brakuje źródła '+$plik.plik)};if((Get-FileHash -LiteralPath $sciezka -Algorithm SHA256).Hash -ne $plik.sha256){throw ('Źródło '+$plik.plik+' zmieniło się po kompilacji. Zbuduj aplikację ponownie.')}}
$znacznik=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N')
$katalogWynikow=Join-Path (Split-Path -Parent $PSScriptRoot) ('build\testy\uruchomienie-'+$znacznik)
$katalogProby=Join-Path $katalogWynikow 'aplikacja'
New-Item -ItemType Directory -Path $katalogProby | Out-Null
foreach($nazwa in @('MapaDyskow.exe','MapaDyskow.exe.config')){Copy-Item -LiteralPath (Join-Path $katalogAplikacji $nazwa) -Destination $katalogProby}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'konfiguracja.json') -Destination $katalogProby
$katalogAplikacji=$katalogProby
$exe=Join-Path $katalogAplikacji 'MapaDyskow.exe'
function Uruchom-Test([string[]]$Argumenty){
    Write-Host ('Test: '+($Argumenty -join ' '))
    $proces=Start-Process -FilePath $exe -ArgumentList $Argumenty -WorkingDirectory $katalogAplikacji -PassThru -WindowStyle Hidden
    try{
        $zegar=[Diagnostics.Stopwatch]::StartNew()
        while(-not $proces.WaitForExit(1000)){if($zegar.Elapsed.TotalSeconds -ge 300){$proces.Kill();$proces.WaitForExit();throw 'Test przekroczył 5 minut. Zatrzymano wyłącznie własny proces testowy.'}}
        $proces.Refresh()
        if($proces.ExitCode -ne 0){throw ('Test zakończył się kodem '+$proces.ExitCode+'. Diagnostyka: '+(Join-Path $katalogAplikacji 'blad-uruchomienia.log'))}
        foreach($nazwa in @('blad-uruchomienia.log','blad-krytyczny.log','bledy.log')){if(Test-Path -LiteralPath (Join-Path $katalogAplikacji $nazwa)){throw ('Błąd testu: '+(Join-Path $katalogAplikacji $nazwa))}}
    }finally{$proces.Dispose()}
}
Uruchom-Test @('--testy')
$fixture=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'ostatni-test.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($fixture.sukces -ne $true){throw 'Testy na danych izolowanych nie potwierdziły sukcesu.'}
$bazaTestowa=[IO.Path]::GetFullPath($fixture.baza)
if(-not $bazaTestowa.StartsWith($katalogProby.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Baza testowa znajduje się poza izolowanym katalogiem.'}
Copy-Item -LiteralPath $bazaTestowa -Destination (Join-Path $katalogAplikacji 'dane\indeks.sqlite')
$katalogZrzutowOperacji=Join-Path (Split-Path -Parent $bazaTestowa) '..\zrzuty-operacji'
foreach($nazwa in @('11-nawigacja.png','12-nowy-folder-inline.png','13-rename-inline.png','14-scroll-przed.png','14-scroll-po.png','14-scroll-back.png','15-ostrzezenie-rozszerzenie.png','16-rename-kafelek.png','20-ikony-100.png','21-ikony-105.png','22-ikony-70.png','22-ikony-75.png','22-ikony-90.png','22-ikony-110.png','22-ikony-140.png','23-szczegoly.png','24-szczegoly-sortowanie.png','30-panel-zdjecie-szczegoly.png','31-panel-zdjecie-ikony.png','32-panel-film.png','33-panel-multi-select.png','34-panel-resize.png','35-panel-folder.png','test-widokow.txt')){
    if(-not (Test-Path -LiteralPath (Join-Path $katalogZrzutowOperacji $nazwa))){throw ('Brakuje materiału WPF: '+$nazwa)}
}
Uruchom-Test @('--test')
$indeks=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'test-indeks.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($indeks.integralnosc -ne 'ok'){throw 'Niepoprawna integralność indeksu.'}
$katalogZrzutow=Join-Path $katalogWynikow 'zrzuty'
Uruchom-Test @('--zrzuty',('"'+$katalogZrzutow+'"'))
foreach($nazwa in @('01-foldery.png','02-duplikaty.png','03-porownanie.png','04-kompaktowe.png','05-lista.png','06-personalizacja.png','07-skala-75.png','08-skala-125.png','09-zdjecia.png','10-filmy.png','test-ui.txt')){
    if(-not (Test-Path -LiteralPath (Join-Path $katalogZrzutow $nazwa))){throw ('Brakuje materiału UI: '+$nazwa)}
}
$wynik=[ordered]@{sukces=$true;utc=[DateTime]::UtcNow.ToString('o');liczba_grup=@($fixture.testy).Count;testy=$fixture;indeks=$indeks;katalog_zrzutow=$katalogZrzutow;katalog_zrzutow_operacji=[IO.Path]::GetFullPath($katalogZrzutowOperacji);sha256_exe=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;ograniczenia='Renderowanie nie potwierdza manualnych interakcji, wszystkich kodeków ani wydajności GPU.'}
[IO.File]::WriteAllText((Join-Path $katalogWynikow 'wyniki.json'),($wynik|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($true))
Write-Output ('Testy zakończone. Wyniki: '+(Join-Path $katalogWynikow 'wyniki.json'))
Write-Host ('PASS: '+$wynik.liczba_grup+' grup testów oraz kontrola indeksu i zrzutów GUI/WPF.')
