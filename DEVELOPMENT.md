# Dalsza praca

## Zakres i workflow

Pracuj wyłącznie w `C:\GitHub\Projects\Mapa_Dysków`. Stary workspace Codexa można odczytać jako referencję, lecz nie modyfikować ani usuwać. Na początku przeczytaj README i ARCHITECTURE, sprawdź `git status` oraz aktualny kod. Nie implementuj ponownie istniejących funkcji i nie włączaj do commita cudzych zmian.

1. Ustal najmniejszy zakres wynikający z zadania. Nazwy nowych funkcji, zmiennych i komentarze zapisuj po polsku; zachowuj wymagane kontrakty zewnętrzne.
2. Zmieniaj lokalny moduł, bez globalnych abstrakcji i refactorów dla porządku. Obecne części `OknoGlowne` są klasą partial, nie niezależnymi serwisami.
3. Dla zmiany zachowania dodaj odpowiedni test na sztucznych danych; dla dokumentacji nie twórz testów kopiujących jej treść.
4. Uruchom `TEST-LOCAL.ps1`; dla zmian launchera także `launcher\testuj-launcher.ps1`. Dla budowania/pierwszego startu uruchom `src\smoke-test.ps1`.
5. Przejrzyj diff, wykonaj `git diff --check` i `git status`. Dodaj jawnie pliki zadania i zrób lokalny commit. Push tylko po wyraźnym poleceniu.

## Bezpieczeństwo danych

- Testy skanera, hashów, hardlinków, rename i tworzenia folderów wykonuj wyłącznie na własnych fixture w nowych katalogach `build\testy` lub innych katalogach tymczasowych. Nie wskazuj rzeczywistych dysków ani dokumentów użytkownika.
- Nie kopiuj prawdziwego indeksu, cache miniaturek ani danych do prób. Runner tworzy sztuczną bazę i kopiuje wyłącznie niezbędne pliki programu.
- Kończ tylko procesy uruchomione przez własny test. Nie zamykaj działającej aplikacji użytkownika.
- Nie dodawaj Delete, Move, Replace ani Overwrite dla plików użytkownika. Rename ma odrzucać kolizje; tworzenie folderu nie może nadpisywać istniejącej zawartości.
- Runtime zapisuje bazę i ustawienia we własnym katalogu aplikacji. Launcher publikuje tylko pliki programu, zachowując dane i istniejącą konfigurację.
- Nie kasuj automatycznie `build`, `release` ani kopii poprzedniego wydania podczas audytu. Mogą zawierać potrzebne materiały diagnostyczne lub dane lokalnej instalacji.

## Zasady nowych funkcji

System plików pozostaje źródłem aktualnej zawartości folderu. SQLite jest warstwą analityczną. F5, zmiana widoku i zaznaczenie nie uruchamiają pełnego rescanu. Rekursję i kosztowne hashowanie uruchamiaj tylko w wyraźnie wybranym zakresie analizy, z anulowaniem i komunikatem postępu.

Droższe metadane pobieraj dla zaznaczenia, poza wątkiem UI. Ignoruj spóźnione wyniki, ogranicz równoległość i uwzględniaj ścieżkę, rozmiar oraz LastWriteTime w cache. Nie generuj wszystkich miniaturek z góry. Nie dodawaj integracji, API ani AI bez polecenia użytkownika.

## EOL, wersja i helpery

`.gitattributes` określa CRLF dla C#/PowerShell/XAML/config/manifest oraz LF dla Python/Markdown/JSON. Git przechowuje tekst z LF. Nie uruchamiaj `git add --renormalize .` przy rutynowej zmianie; nie przepisuj masowo plików ani ich kodowania. Zachowuj UTF-8 z BOM w istniejących skryptach PowerShell używanych przez 5.1.

AssemblyVersion i AssemblyFileVersion są w `src\AssemblyInfo.cs`; obecnie obie wynoszą `1.2.0.0`. Zmieniaj wersję tylko przy uzgodnionym wydaniu lub zadaniu wymagającym nowej wersji, aktualizując CHANGELOG i README. Launcher zapisuje odczytaną wersję EXE oraz osobny fingerprint źródeł.

`src\analizuj.ps1` jest dodatkową analizą tekstową/parserową, nie zamiennikiem kompilacji ani testów runtime. Generuje ignorowany `analiza-statyczna.json` w root repo; obecnie obejmuje skrypty root/src, nie katalog launchera.

`src\przygotuj-dane.py` jest historycznym helperem importu zewnętrznego audytu. Wymaga argumentu katalogu audytu, zapisuje `dane` względem root repo i nie należy do builda ani testów. Nie uruchamiaj go na danych użytkownika w ramach rutynowej weryfikacji; świeża aplikacja nie potrzebuje tego importu.
