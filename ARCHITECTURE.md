# Architektura Mapy Dysków

## Runtime i źródła danych

Jedna aplikacja WPF x64 na .NET Framework, kompilowana systemowym kompilatorem C#. XAML jest zasobem EXE; .NET/WPF, `winsqlite3.dll` i handlery Windows są zależnościami systemowymi. Nie ma backendu, NuGet ani wymaganego środowiska Python.

`Program.Katalog` wskazuje katalog EXE. `konfiguracja.json`, indeks i ustawienia są lokalne dla instalacji. Przy braku indeksu tworzony jest pusty schemat. Konfiguracja wskazuje bazę względnie, domyślnie `dane\indeks.sqlite`; wyjście poza katalog aplikacji jest odrzucane.

Przepływ zwykłego folderu: **Nawigacja → EksploratorFolderow → system plików**, z dołączeniem dostępnych danych Baza. Przepływ analizy: **jawny zakres skanu → Skaner → Baza → widoki analityczne**. Dane analityczne nie zastępują bieżącego odczytu folderu. F5 nie wywołuje skanera.

## Moduły

| Plik w `src` | Odpowiedzialność i zależności |
| --- | --- |
| `Program.cs` | Punkt wejścia, konfiguracja, inicjalizacja indeksu, model `Wiersz`, wspólne elementy UI, główne okno i widoki analizy/porównania. Korzysta z Baza oraz części `OknoGlowne`. Obsługuje też przełączniki testowe. |
| `Baza.cs` | SQLite przez P/Invoke do systemowego `winsqlite3.dll`; parametryzowane zapytania, połączenia do odczytu/zapisu, pusty schemat plików, folderów, duplikatów, dysków i zdarzeń. |
| `Skaner.cs` | Rekursywna analiza jawnie wskazanego zakresu, agregaty, typy, tożsamości plików/hardlinki, hashe i duplikaty. Używa Baza, modelu/konfiguracji Program i Windows API; obsługuje anulowanie i transakcje. |
| `EksploratorFolderow.cs` | Enumeruje wyłącznie bezpośrednie elementy systemu plików. Baza dostarcza dodatkowe informacje; nowe lub zmienione pliki są oznaczane jako nieprzeanalizowane. Używa `Wiersz` i konwersji Program. |
| `Nawigacja.cs` | Historia Back/Forward, lokalizacje, skróty klawiatury/myszy, F5 oraz spójność ścieżek po rename. Część `OknoGlowne`; korzysta z OperacjePlikowe i odczytu folderów. |
| `Widoki.cs` | Przełączanie/pamięć widoków i skali, obsługa list ikon oraz ich wirtualizacja. Część `OknoGlowne`; współpracuje z Kafelki i Szczegoly. |
| `Szczegoly.cs` | Kolumny, naturalne sortowanie, układy folderów, zaznaczenie i prezentacja tabelaryczna. Definiuje modele układu oraz część `OknoGlowne`. |
| `Kafelki.cs` | Modele wyglądu, profile kafelków, miniatury/metadane, cache i zapis `wyglad.json`. Korzysta z MetadaneWindows, WPF i modeli Program. Klasa danych `Wyglad` jest zdefiniowana tutaj. |
| `PanelInformacji.cs` | Niezależny panel pliku/folderu/dysku/multi-select; szerokość i sekcje. Async, ograniczona równoległość, anulowanie i cache metadanych. Korzysta z Baza, Kafelki, MetadaneWindows i OperacjePlikowe; nie skanuje rekursywnie ani nie hashuje przy zaznaczeniu. |
| `OperacjePlikowe.cs` | Walidacja nazw, tworzenie folderu i rename bez nadpisywania celu; aktualizacja ścieżek indeksu i zdarzenia dla UI. F5 jest oddzielony od skanu analitycznego. |
| `MetadaneWindows.cs` | COM/property store i Windows shell: lekkie właściwości, metadane multimediów i miniatury. Dostępność pól zależy od pliku i handlerów systemowych. |
| `Wyglad.cs` | Okno personalizacji `OknoWygladu` z podglądem; edytuje profile Kafelki i zapisuje wygląd. |
| `Interfejs.xaml` | Zasoby, style i kontrolki głównego interfejsu, osadzane przez builder w EXE. |

`Program.cs`, Nawigacja, Widoki, Szczegoly i PanelInformacji współdzielą klasę partial `OknoGlowne`. Nie są odrębnymi procesami ani izolowanymi modułami. Przy zmianach kontroluj powiązane zdarzenia i stan zaznaczenia; nie wprowadzaj nowej globalnej warstwy tylko dla rozdzielenia plików.

## Build, testy i launcher

`src\buduj.ps1` kompiluje wszystkie `src\*.cs`, osadza XAML i manifest, kopiuje konfigurację runtime/wzorcową oraz zapisuje metadane kompilacji. Wzorcowa konfiguracja nie zastępuje istniejącej w katalogu wynikowym. `MapaDyskow.exe.config` wskazuje .NET Framework 4.8; `aplikacja.manifest` określa uprawnienia i DPI.

Testy `Testy.cs`, `TestyOperacji.cs`, `TestyWidokow.cs` i `TestyEksploratoraPanelu.cs` są częścią EXE. Runner `src\testuj.ps1` tworzy osobną instalację testową, sprawdza jej zgodność z buildem i uruchamia logikę/WPF, integralność indeksu oraz zrzuty. `TEST-LOCAL.ps1` wykonuje ten cykl w Debug i Release.

Launcher ma osobny EXE i namespace, bez zależności kompilacyjnej od aplikacji. `ProgramLaunchera.cs` odpowiada za WPF i ustawienia; `WydanieLokalne.cs` za fingerprint, blokadę, staging, kontrolę, kopię poprzednich plików i publikację z dziennikiem odzyskiwania. `TestyLaunchera.cs` sprawdza ten proces na kopii repo. Launcher uruchamia istniejący builder aplikacji i nie wykonuje pełnego zestawu testów przy każdym starcie.

Stan wydania jest w `release\stan-wydania.json`, poprzednie pliki programu w `release\poprzednie`, a próby w `build\pending`. Dane instalacji pozostają w miejscu. Szczegóły: [LAUNCHER.md](LAUNCHER.md).
