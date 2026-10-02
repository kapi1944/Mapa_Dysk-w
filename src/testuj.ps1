param([Parameter(Mandatory=$true)][string]$KatalogDocelowy)
$ErrorActionPreference='Stop'
$katalogAplikacji=[IO.Path]::GetFullPath($KatalogDocelowy)
$exe=Join-Path $katalogAplikacji 'MapaDyskow.exe'
if(-not (Test-Path -LiteralPath $exe)){throw 'Najpierw zbuduj aplikację skryptem buduj.ps1.'}
$metadaneKompilacji=Join-Path $katalogAplikacji 'kompilacja.json'
if(-not (Test-Path -LiteralPath $metadaneKompilacji)){throw 'Brakuje potwierdzenia aktualnej kompilacji. Uruchom buduj.ps1 przed testami.'}
$kompilacja=Get-Content -LiteralPath $metadaneKompilacji -Raw | ConvertFrom-Json
if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $kompilacja.sha256_exe){throw 'EXE różni się od potwierdzonej kompilacji.'}
foreach($plik in $kompilacja.zrodla){$sciezka=Join-Path $PSScriptRoot $plik.plik;if(-not (Test-Path -LiteralPath $sciezka)){throw ('Brakuje źródła '+$plik.plik)};if((Get-FileHash -LiteralPath $sciezka -Algorithm SHA256).Hash -ne $plik.sha256){throw ('Źródło '+$plik.plik+' zmieniło się po kompilacji. Zbuduj aplikację ponownie.')}}
$znacznik=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N')
$katalogWynikow=Join-Path $katalogAplikacji ('testy\uruchomienie-'+$znacznik)
New-Item -ItemType Directory -Path $katalogWynikow | Out-Null
function Uruchom-Test([string[]]$Argumenty){
    $proces=Start-Process -FilePath $exe -ArgumentList $Argumenty -PassThru -WindowStyle Hidden
    if(-not $proces.WaitForExit(300000)){throw 'Test nadal działa po 5 minutach. Proces nie został przerwany; sprawdź go przed ponownym testem.'}
    $proces.Refresh()
    if($proces.ExitCode -ne 0){throw ('Test zakończył się kodem '+$proces.ExitCode+'. Sprawdź blad-uruchomienia.log i bledy.log w katalogu aplikacji.')}
}
Uruchom-Test @('--testy')
$fixture=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'ostatni-test.json') -Raw | ConvertFrom-Json
if($fixture.sukces -ne $true){throw 'Testy na danych izolowanych nie potwierdziły sukcesu.'}
Uruchom-Test @('--test')
$indeks=Get-Content -LiteralPath (Join-Path $katalogAplikacji 'test-indeks.json') -Raw | ConvertFrom-Json
if($indeks.integralnosc -ne 'ok'){throw 'Niepoprawna integralność indeksu.'}
$katalogZrzutow=Join-Path $katalogWynikow 'zrzuty'
Uruchom-Test @('--zrzuty',('"'+$katalogZrzutow+'"'))
foreach($nazwa in @('01-foldery.png','02-duplikaty.png','03-porownanie.png','04-kompaktowe.png','05-lista.png','06-personalizacja.png','07-skala-75.png','08-skala-125.png','09-zdjecia.png','10-filmy.png','test-ui.txt')){
    $sciezka=Join-Path $katalogZrzutow $nazwa
    if(-not (Test-Path -LiteralPath $sciezka)){throw ('Brakuje materiału UI: '+$nazwa)}
}
$wynik=[ordered]@{sukces=$true;utc=[DateTime]::UtcNow.ToString('o');testy=$fixture;indeks=$indeks;katalog_zrzutow=$katalogZrzutow;sha256_exe=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;ograniczenia='Renderowanie nie potwierdza manualnych interakcji, wszystkich kodeków ani wydajności GPU.'}
[IO.File]::WriteAllText((Join-Path $katalogWynikow 'wyniki.json'),($wynik|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($true))
Write-Output ('Testy zakończone. Wyniki: '+(Join-Path $katalogWynikow 'wyniki.json'))
