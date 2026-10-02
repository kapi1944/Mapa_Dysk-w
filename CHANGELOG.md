# Historia zmian

Historia obejmuje etap pracy z samodzielnego repo. Wersja aplikacji: **1.2.0.0**; poniższe etapy nie otrzymały osobnych numerów Assembly.

## 2026-10-02 — dokumentacja i jakość repo

- README, zasady rozwoju i mapa architektury; nowe repo jednoznacznie oznaczone jako źródło prawdy.
- Doprecyzowanie ignorowania artefaktów/runtime i reguł EOL bez masowej normalizacji.
- Usunięcie pustej linii na końcu Program.cs; bez zmiany zachowania aplikacji.

## Dotychczasowe etapy nowego repo

- `4cf8d3f`: launcher z fingerprintem źródeł, lokalnym Release, zachowaniem ostatniej działającej wersji i testami A–C.
- `82402d9`: dokończenie odczytu folderów z systemu plików i niezależnego Panelu informacji; async/cache oraz testy.
- `565a67a`: samodzielny pipeline Debug/Release, testy na fixture i istniejące testy WPF.
- `8befd67`: samowystarczalny build i smoke test świeżej instalacji.
- `f4d1c5c`: import źródeł ze starego workspace do nowego repo.

Starszy workspace pozostaje referencją do odczytu; ta historia nie próbuje odtwarzać prac sprzed importu.
