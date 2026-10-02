# Testy lokalne

Uruchom z katalogu repo, w Windows PowerShell 5.1 lub PowerShell 7:

```powershell
.\TEST-LOCAL.ps1
```

Skrypt nie wymaga parametrów ani interaktywnego wyboru ścieżek. Wymaga Windows
x64, .NET Framework 4.8 i sesji pulpitu dla istniejących testów WPF.

Kolejność: Debug build → testy Debug → Release build → testy Release.
Każda konfiguracja przechodzi trzy etapy: `--testy` (logika, operacje plikowe
i WPF), `--test` (integralność sztucznego indeksu), `--zrzuty` (renderowanie
istniejących ekranów). Łącznie: 2 buildy i 6 etapów testowych.

Kompilacje trafiają do `build\testy\Debug` i `build\testy\Release`.
Każde uruchomienie testów tworzy osobny katalog
`build\testy\uruchomienie-<czas>-<identyfikator>` z raportem `wyniki.json`,
kopią aplikacji, bazą, sztucznymi plikami, diagnostyką i zrzutami ekranów.
Artefakty pozostają do sprawdzenia i są ignorowane przez Git.

Można przetestować już zbudowany Release bez parametrów:

```powershell
.\src\testuj.ps1
```

Domyślny katalog wejściowy to `build\testy\Release`, wyliczony z położenia
repo. Opcjonalny `-KatalogDocelowy` wskazuje inną kompilację. Runner sprawdza
hash EXE i zgodność źródeł, po czym kopiuje tylko EXE i konfigurację runtime
do nowej próby. Nie kopiuje istniejącego indeksu, ustawień ani cache.
Konfiguracja testowa pochodzi z `src\konfiguracja.json`. Wszystkie skany,
hashe, hardlinki, zmiany nazw i tworzenie folderów dotyczą sztucznych danych.

Zestaw obejmuje 11 raportowanych grup: skaner, SHA-256 i duplikaty,
hardlinki, anulowanie i rollback, cache miniaturek, agregację folderów,
ochronę źródeł, sortowanie, operacje plikowe, historię Back/Forward oraz WPF.
WPF sprawdza m.in. F5, rename, ikony i Szczegóły, Ctrl+scroll, zaznaczenie,
kolumny oraz wirtualizację 100000 modeli. Runner sprawdza też obecność
27 zrzutów PNG i 2 raportów tekstowych na konfigurację.

Ręcznej oceny wymagają wygląd i czytelność zrzutów, układ przy różnych DPI,
płynność przewijania i skalowania na GPU oraz odczucia przy użyciu fizycznej
myszy i klawiatury. Sztuczne pliki multimedialne nie potwierdzają obsługi
wszystkich kodeków ani podglądów rzeczywistych filmów.
