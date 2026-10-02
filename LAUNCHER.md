# Lokalny launcher Mapy dysków

Z katalogu repo jednorazowo zbuduj launcher:

```powershell
.\launcher\buduj-launcher.ps1
```

Na co dzień uruchamiaj `launcher\MapaDyskow-Launcher.exe`. Możesz utworzyć skrót do tego pliku. Wymagany jest Windows x64 z .NET Framework 4.8/4.8.1 i systemowym Windows PowerShell. Ścieżki wynikają z położenia launchera; repo można przenieść.

Launcher porównuje SHA-256 nazw i zawartości plików `src\*.cs`, `*.xaml`, `*.manifest`, `*.config`, `buduj.ps1` i `konfiguracja.json`. Gdy fingerprint oraz integralność Release są poprawne, od razu uruchamia aplikację bez okna i bez builda. Przy zmianie buduje Release z migawki źródeł w `build\pending\<id>`, pokazując małe okno postępu. Wykonuje tylko krótką kontrolę `--test` na pustej bazie w katalogu próby. Pełny pipeline aplikacji nadal uruchamia się osobno przez `TEST-LOCAL.ps1`.

Po sukcesie katalog aplikacji wygląda tak:

```text
launcher/
  MapaDyskow-Launcher.exe
  MapaDyskow-Launcher.exe.config
  ustawienia.json              # po zapisaniu ustawienia
release/
  MapaDyskow.exe
  MapaDyskow.exe.config
  konfiguracja.json
  kompilacja.json
  stan-wydania.json            # version, sourceHash, builtAt, exeSha256
  dane/miniatury/              # początkowo puste
  poprzednie/<data-id>/        # kopia poprzednich plików programu
logs/launcher-<data-id>.log
```

Podmiana jest transakcyjna na poziomie plików: najpierw kopia poprzednich plików programu i dziennik, następnie atomowe podmiany pojedynczych plików, EXE jako ostatni. Po przerwaniu publikacji kolejny start odtwarza poprzednie wydanie z dziennika. Launcher nie przenosi katalogu danych, nie kopiuje indeksu ani miniaturek i zachowuje istniejącą konfigurację oraz ustawienia aplikacji. Krótka kontrola tworzy wyłącznie sztuczną bazę w `build\pending`; ta baza nie trafia do Release. Uruchomiony Release blokuje budowanie i publikację. Równoległe launchery są chronione blokadą pliku.

Przy błędzie dostępne są „Uruchom ostatnią działającą”, „Pokaż log” i „Zamknij”. Bez wcześniejszego poprawnego Release pierwszy przycisk jest nieaktywny. Ustawienie „Automatycznie buduj po zmianach źródeł” jest domyślnie włączone; zapisywane jest w `launcher\ustawienia.json`. Aby zmienić je bez oczekiwania na zmianę źródeł:

```powershell
.\launcher\MapaDyskow-Launcher.exe --ustawienia
```

Przy wyłączonej opcji launcher uruchamia ostatni poprawny Release. Jeśli go nie ma, informuje o błędzie. Po zmianie kodu samego launchera ponów `buduj-launcher.ps1`; launcher aktualizuje aplikację, a własny EXE budowany jest oddzielnie.

Testy launchera: 7 scenariuszy (A: zero builda; B: publikacja i zachowanie danych; C: błąd kompilacji bez zmian Release; ustawienia; uruchomiony EXE; odzyskiwanie transakcji; GUI okna błędu ze zrzutem PNG):

```powershell
.\launcher\testuj-launcher.ps1
```

Testy korzystają wyłącznie z kopii repo i sztucznych danych pod ignorowanym `build\testy-launchera`. Raport JSON i log pozostają w tym katalogu. Do przygotowania Release bez otwierania aplikacji służy `MapaDyskow-Launcher.exe --przygotuj` (respektuje ustawienie autobuilda).
