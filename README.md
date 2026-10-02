# Mapa Dysków

Lokalna aplikacja Windows/WPF do przeglądania folderów i analizy miejsca na dysku, typów plików oraz duplikatów. Działa bez serwera. Pozwala tworzyć foldery i zmieniać nazwy; celowo nie oferuje **Delete / Move / Replace / Overwrite** dla danych użytkownika.

**Źródłem prawdy projektu jest `C:\GitHub\Projects\Mapa_Dysków`.** Stary workspace `C:\Users\Kacper\Documents\Codex\2026-10-01\przeanalizuj-lokalne-dyski-i-foldery-na` pozostaje wyłącznie materiałem referencyjnym do odczytu. Nie budujemy z niego, nie zapisujemy tam zmian i nie usuwamy go.

## Explorer i Analysis

- **Explorer:** system plików określa aktualną bezpośrednią zawartość folderu, również pliki spoza indeksu. Filtry i stronicowanie sterują prezentacją. F5 odczytuje tylko bieżący folder, bez rekursywnego skanu i hashowania.
- **Analysis:** SQLite przechowuje wyniki jawnego skanowania: rozmiary rekursywne, oznaczenia analityczne, hashe i duplikaty. Historyczny indeks może być nieaktualny; nie rozstrzyga o istnieniu plików w zwykłym folderze.
- **Panel informacji:** niezależny od Ikon/Szczegółów. Metadane zaznaczonego elementu są pobierane asynchronicznie, z anulowaniem i cache. Panel nie uruchamia pełnych skanów ani hashowania.

Szczegóły modułów i zależności: [ARCHITECTURE.md](ARCHITECTURE.md).

## Wymagania i układ repo

Windows 10/11 x64, .NET Framework 4.8/4.8.1, systemowa biblioteka `winsqlite3.dll`, Windows PowerShell 5.1 lub PowerShell 7. Testy WPF wymagają sesji pulpitu. Kompilacja używa systemowego `csc.exe`; Python nie jest wymagany do builda ani testów.

```text
src/               aplikacja, XAML, konfiguracja wzorcowa, build i testy
launcher/          kod i skrypty osobnego launchera
TEST-LOCAL.ps1     pełny pipeline Debug i Release aplikacji
TESTY.md           zakres i ograniczenia testów
LAUNCHER.md        działanie i testy lokalnego updatera
DEVELOPMENT.md     zasady dalszej pracy
ARCHITECTURE.md    mapa modułów
CHANGELOG.md       historia obecnego etapu
build/             buildy, pending i izolowane próby (ignorowane)
release/           aplikacja publikowana przez launcher (ignorowana)
logs/              logi launchera (ignorowane)
```

Testy C# pozostają w `src`, ponieważ obecny prosty builder kompiluje je do tego samego EXE. Nie ma osobnego projektu MSBuild ani zależności NuGet.

## Build i uruchamianie

Komendy wykonuj z katalogu nowego repo. Ręczny, samowystarczalny build:

```powershell
.\src\buduj.ps1 -KatalogDocelowy .\build\manual -Tryb Release
.\build\manual\MapaDyskow.exe
```

Builder sam tworzy katalog docelowy, EXE, konfigurację runtime, `konfiguracja.json`, `kompilacja.json` i puste `dane\miniatury`. Nie kopiuje indeksu ani cache. Pierwszy start tworzy pusty indeks. Domyślna baza to `dane\indeks.sqlite` względem katalogu aplikacji; konfiguracja nie może wskazywać bazy poza tym katalogiem.

Codzienne uruchamianie z automatycznym lokalnym Release:

```powershell
.\launcher\buduj-launcher.ps1
.\launcher\MapaDyskow-Launcher.exe
```

Launcher publikuje do `release\MapaDyskow.exe`, a stan zapisuje w `release\stan-wydania.json`. Bez zmian źródeł nie buduje ponownie. Przy zmianach buduje w `build\pending`, zachowuje poprzednią wersję i publikuje dopiero po sukcesie. Nie aktualizuje uruchomionego Release. Zachowuje jego dane, cache i konfigurację. Podmiany plików programu przez updater są oddzielne od operacji na danych użytkownika. Więcej: [LAUNCHER.md](LAUNCHER.md).

## Weryfikacja

```powershell
.\TEST-LOCAL.ps1
.\launcher\testuj-launcher.ps1
.\src\smoke-test.ps1
git diff --check
```

Pierwszy skrypt wykonuje Debug build/testy i Release build/testy, w tym WPF, indeks i zrzuty. Drugi sprawdza launcher na kopii źródeł i sztucznych danych. Smoke test buduje świeżą instalację i sprawdza jej uruchomienie. Artefakty pozostają w `build`. Zakres oraz rzeczy wymagające oceny ręcznej opisuje [TESTY.md](TESTY.md).

Aktualna wersja Assembly i FileVersion: **1.2.0.0** (`src\AssemblyInfo.cs`). Zasady bezpiecznych zmian: [DEVELOPMENT.md](DEVELOPMENT.md).
