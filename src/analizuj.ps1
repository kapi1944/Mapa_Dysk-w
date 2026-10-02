$ErrorActionPreference='Stop'
$katalogAplikacji=Split-Path -Parent $PSScriptRoot
$wyniki=[Collections.Generic.List[object]]::new()
foreach($plik in @((Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1'),(Get-ChildItem -LiteralPath $katalogAplikacji -Filter '*.ps1')) | ForEach-Object {$_}){
    $tokeny=$null;$bledy=$null
    [Management.Automation.Language.Parser]::ParseFile($plik.FullName,[ref]$tokeny,[ref]$bledy) | Out-Null
    if($bledy.Count -gt 0){throw ($plik.Name+': '+($bledy.Message -join '; '))}
    $wyniki.Add([ordered]@{plik=$plik.Name;parser_powershell='OK'})
}
$xml=[xml]([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Interfejs.xaml')))
$nazwy=@($xml.SelectNodes('//*[@*[local-name()="Name"]]') | ForEach-Object {$_.GetAttribute('Name','http://schemas.microsoft.com/winfx/2006/xaml')})
if(@($nazwy | Group-Object | Where-Object Count -gt 1).Count -gt 0){throw 'Powtórzone nazwy kontrolek XAML.'}
foreach($plik in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'){
    $kod=[IO.File]::ReadAllText($plik.FullName)
    $czysty=[regex]::Replace($kod,'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|''(?:\\.|[^''\\])*''|//[^\r\n]*|/\*[\s\S]*?\*/','')
    foreach($para in @(@('{','}'),@('(',')'),@('[',']'))){
        $otwarte=[regex]::Matches($czysty,[regex]::Escape($para[0])).Count
        $zamkniete=[regex]::Matches($czysty,[regex]::Escape($para[1])).Count
        if($otwarte -ne $zamkniete){throw ('Niezgodne nawiasy '+$plik.Name+' '+$para[0])}
    }
    if($czysty -match '\b(?:File|Directory)\s*\.\s*(?:Delete|Move|Replace)\s*\('){throw ('Niedozwolona operacja plikowa w '+$plik.Name)}
    foreach($dopasowanie in [regex]::Matches($kod,'Kontrolka<[^>]+>\("([^"]+)"\)')){
        if($nazwy -notcontains $dopasowanie.Groups[1].Value){throw ('Brak kontrolki XAML: '+$dopasowanie.Groups[1].Value)}
    }
    $wyniki.Add([ordered]@{plik=$plik.Name;nawiasy='OK';usuwanie_lub_przenoszenie='Nie znaleziono API plikowego';sha256=(Get-FileHash -LiteralPath $plik.FullName -Algorithm SHA256).Hash})
}
$stan=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');wyniki=$wyniki;xml='Poprawny XML; nie jest to test runtime WPF';kompilacja_wykonana=$false;uwaga='Kontrole tekstowe i parser PowerShell nie potwierdzają kompilacji C#, poprawności COM, wykonania zapytań SQL ani działania interakcji GUI.'}
[IO.File]::WriteAllText((Join-Path $katalogAplikacji 'analiza-statyczna.json'),($stan | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($true))
Write-Output 'Analiza statyczna zakończona; kompilacja i testy runtime pozostają oddzielnym etapem.'
