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

Zestaw obejmuje 12 raportowanych grup: skaner, SHA-256 i duplikaty,
hardlinki, anulowanie i rollback, cache miniaturek, agregację folderów,
ochronę źródeł, sortowanie, operacje plikowe, historię Back/Forward oraz WPF.
WPF sprawdza m.in. F5, rename, ikony i Szczegóły, Ctrl+scroll, zaznaczenie,
kolumny oraz wirtualizację 100000 modeli. Runner sprawdza też obecność
33 zrzutów PNG i 2 raportów tekstowych na konfigurację.

## Eksplorator i Panel informacji

Audyt przed dokończeniem: odczyt folderu z systemu plików, F5 bez rekursji,
analityka SQLite, ON/OFF, X, separator, zapis szerokości i sekcji oraz
metadane Windows/EXIF były już zaimplementowane. Async/cache i synchronizacja
zaznaczenia wymagały dokończenia; brakowało testów panelu i danych spoza indeksu.

Normalny folder pokazuje bezpośrednie elementy systemu plików, z istniejącym
filtrem i stronicowaniem. F5 ponownie odczytuje bieżący folder, nie zmienia
historycznej bazy i nie uruchamia skanera ani hashowania. Rozmiary rekursywne,
tagi i duplikaty pozostają danymi analitycznymi z indeksu.

Panel działa w Szczegółach i Ikonach. Menu Wyświetl przełącza ON/OFF, X zamyka,
separator zmienia szerokość; szerokość i stan sekcji są zapisane w wyglad.json.
Przycisk Informacje o folderze pokazuje dane bieżącego folderu, a w katalogu
głównym dysku także pojemność, wolne/zajęte, system plików i etykietę.
Informacje o zawartości folderu są odczytywane bez rekursji; największe pliki
obejmują także elementy spoza indeksu.

Panel odczytuje droższe metadane tylko dla zaznaczonego pliku. Dwa zadania
mogą pracować równolegle. Zmiana zaznaczenia anuluje oczekiwanie i kolejkę;
spóźnione wyniki nie aktualizują panelu. Trwające wywołanie natywnego handlera
Windows może dokończyć się w tle. Cache uwzględnia ścieżkę, rozmiar i dokładną
datę modyfikacji. EXIF jest pobierany po zaznaczeniu, dokumenty używają lekkich
właściwości Windows, bez dodawania parserów Office. Dostępność FPS, bitrate,
kodeków, profili i tagów zależy od pliku i zainstalowanych handlerów Windows.

Testy używają własnych PNG/JPEG z EXIF, poprawnego AVI RGB24 i WAV PCM.
Sprawdzają F5 i pliki/foldery spoza indeksu, brak skanu i zmian bazy,
Szczegóły/Ikony, prawdziwe zdarzenia GridSplitter, pamięć szerokości/sekcji,
multi-select, metadane zdjęcia/filmu/audio, cache i jego unieważnianie,
anulowanie oraz brak dodatkowego hashowania. Zrzuty panelu mają numery 30–35.

Ręcznej oceny wymagają wygląd i czytelność zrzutów, układ przy różnych DPI,
płynność przewijania i skalowania na GPU oraz odczucia przy użyciu fizycznej
myszy i klawiatury. Sztuczne pliki multimedialne nie potwierdzają obsługi
wszystkich kodeków ani podglądów rzeczywistych filmów.

## Launcher i smoke test

`launcher\testuj-launcher.ps1` buduje launcher i wykonuje 7 scenariuszy na
kopii źródeł w `build\testy-launchera`: brak zmian/zero builda, poprawną
publikację, błąd kompilacji bez zmian Release, ustawienia, ochronę działającego
EXE, odzyskiwanie transakcji oraz GUI okna błędu. Szczegóły: [LAUNCHER.md](LAUNCHER.md).

`src\smoke-test.ps1` sprawdza świeży build, puste katalogi i indeks, otwarcie
okna, ponowny start, pierwszy skan sztucznego folderu i zachowanie danych po
rebuildzie. Wynik zapisuje w `build\smoke-<id>\wynik.json`.

Testy GUI najlepiej uruchamiać kolejno w tej samej sesji pulpitu. Podczas audytu
2026-10-02 smoke uruchomiony równolegle z testami launchera raz przekroczył
5 sekund na zamknięcie własnego okna. Ponowne samodzielne uruchomienie przeszło;
przyczyna timeoutu nie została potwierdzona. Jeśli błąd wróci, zachowaj katalog
próby i sprawdź zamykanie okna ręcznie. Nie traktuj wymuszonego zakończenia jako
poprawnego zamknięcia aplikacji.
