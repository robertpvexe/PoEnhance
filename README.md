# PoEnhance

## Overview

PoEnhance is a Windows companion application and in-game overlay for Path of Exile. Development currently focuses on Path of Exile 1.

The long-term goal is to combine price checking, economy and stash value tracking, Trade search, regex tools, and searchable shared game data in one free application. The project is currently an early engineering prototype.

## Project Status

- Early development.
- Not ready for general use.
- Version 0.1 is a technical prototype.
- No public release is currently available.

## Core Principles

- GGG compliance.
- No gameplay automation.
- Local-first storage and behavior.
- Modular architecture.
- Graceful handling of unknown data.
- Shared reusable game-data catalogs.
- Explicit user control over network requests.

## Version 0.1 Goal

Confirmed version 0.1 scope:

- WPF shell.
- Background lifecycle.
- Path of Exile process detection.
- Active-window detection.
- Configurable shortcut foundation.
- Clipboard capture.
- Manual pasted-item input.
- Basic parser.
- Basic overlay.
- Parser tests where practical.
- Local development logging.

Version 0.1 does not require Trade API, OAuth, stash value, economy tracking, complete databases, or polished final UI.

## Technology

The initial stack is provisional until validated by the technical prototype:

- C#.
- .NET 10 LTS.
- WPF.
- Windows 11 primary.
- Windows 10 where practical.

## Repository Structure

Current repository contents:

- `PoEnhance.slnx` - solution file.
- `PoEnhance.App/` - WPF application project.
- `docs/` - project documentation.
- `README.md` - repository overview.

Key documentation:

- [Vision](docs/VISION.md)
- [Requirements](docs/REQUIREMENTS.md)
- [Roadmap](docs/ROADMAP.md)
- [Compliance](docs/COMPLIANCE.md)
- [Decision Log](docs/DECISIONS.md)

## Prerequisites

- Windows 10 or Windows 11.
- .NET 10 SDK.
- Git.
- An editor such as VS Code with C# support.

## Build and Run

From the repository root:

```powershell
dotnet restore
dotnet build
dotnet run --project .\PoEnhance.App\PoEnhance.App.csproj
```

## Tests

No automated test project exists yet. Automated tests are planned but not yet added.

## Development Approach

- Documentation-first.
- Small verifiable milestones.
- Working software after each milestone.
- Requirements, architecture, compliance, tests, and documentation reviewed before major expansion.

## Compliance Notice

PoEnhance is an unofficial community project. It is not affiliated with or endorsed by Grinding Gear Games.

Implementation must comply with current GGG rules and API policies. The project must not automate gameplay or use hidden integrations. This repository does not claim legal approval or formal certification from GGG.

## Contributing

The project is initially intended for the owner and a small group of friends. A detailed public contribution process has not been created yet.

## License

No open-source license has been selected yet.

## Documentation

- [docs/VISION.md](docs/VISION.md)
- [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md)
- [docs/ROADMAP.md](docs/ROADMAP.md)
- [docs/COMPLIANCE.md](docs/COMPLIANCE.md)
- [docs/DECISIONS.md](docs/DECISIONS.md)

## Current Limitations

- Path of Exile 1 focus.
- English game client first.
- Windows only.
- Early prototype.
- Incomplete parser and data catalogs.
- No production-ready Trade, OAuth, stash, or economy integration yet.

---

# PoEnhance — informacje o projekcie (wersja polska)

Wersja angielska jest kanonicznym źródłem tego README. Ta sekcja po polsku jest tłumaczeniem pomocniczym.

## Przegląd

PoEnhance jest aplikacją companion dla Windows i overlay w grze dla Path of Exile. Rozwój obecnie koncentruje się na Path of Exile 1.

Długoterminowym celem jest połączenie price checking, economy and stash value tracking, Trade search, narzędzi regex i przeszukiwalnych współdzielonych danych gry w jednej darmowej aplikacji. Projekt jest obecnie wczesnym prototypem inżynieryjnym.

## Status Projektu

- Wczesny rozwój.
- Nie jest gotowy do ogólnego użycia.
- Wersja 0.1 jest prototypem technicznym.
- Obecnie nie ma publicznego wydania.

## Główne Zasady

- Zgodność z GGG.
- Brak automatyzacji gameplay.
- Local-first storage i zachowanie.
- Modularna architektura.
- Graceful handling nieznanych danych.
- Współdzielone katalogi game-data wielokrotnego użytku.
- Jawna kontrola użytkownika nad żądaniami sieciowymi.

## Cel Wersji 0.1

Potwierdzony zakres wersji 0.1:

- WPF shell.
- Lifecycle w tle.
- Wykrywanie procesu Path of Exile.
- Wykrywanie aktywnego okna.
- Fundament konfigurowalnego skrótu.
- Przechwytywanie clipboard.
- Ręcznie wklejony input przedmiotu.
- Podstawowy parser.
- Podstawowy overlay.
- Testy parsera tam, gdzie jest to praktyczne.
- Lokalne logowanie developerskie.

Wersja 0.1 nie wymaga Trade API, OAuth, stash value, economy tracking, kompletnych baz danych ani dopracowanego finalnego UI.

## Technologia

Początkowy stack jest provisional do czasu walidacji przez prototyp techniczny:

- C#.
- .NET 10 LTS.
- WPF.
- Windows 11 jako główny system.
- Windows 10 tam, gdzie jest to praktyczne.

## Struktura Repozytorium

Obecna zawartość repozytorium:

- `PoEnhance.slnx` - plik solution.
- `PoEnhance.App/` - projekt aplikacji WPF.
- `docs/` - dokumentacja projektu.
- `README.md` - przegląd repozytorium.

Kluczowa dokumentacja:

- [Wizja](docs/VISION.md)
- [Wymagania](docs/REQUIREMENTS.md)
- [Mapa rozwoju](docs/ROADMAP.md)
- [Zgodność](docs/COMPLIANCE.md)
- [Dziennik decyzji](docs/DECISIONS.md)

## Wymagania Wstępne

- Windows 10 lub Windows 11.
- .NET 10 SDK.
- Git.
- Edytor taki jak VS Code ze wsparciem C#.

## Build i Uruchomienie

Z katalogu głównego repozytorium:

```powershell
dotnet restore
dotnet build
dotnet run --project .\PoEnhance.App\PoEnhance.App.csproj
```

## Testy

Projekt testów automatycznych jeszcze nie istnieje. Testy automatyczne są planowane, ale nie zostały jeszcze dodane.

## Podejście Developerskie

- Documentation-first.
- Małe, weryfikowalne milestones.
- Działające oprogramowanie po każdym milestone.
- Review wymagań, architektury, zgodności, testów i dokumentacji przed dużym rozszerzeniem zakresu.

## Informacja o Zgodności

PoEnhance jest nieoficjalnym projektem społecznościowym. Nie jest powiązany z Grinding Gear Games ani przez nie wspierany.

Implementacja musi być zgodna z aktualnymi zasadami GGG i politykami API. Projekt nie może automatyzować gameplay ani używać ukrytych integracji. To repozytorium nie twierdzi, że posiada zgodę prawną lub formalną certyfikację od GGG.

## Współpraca

Projekt jest początkowo przeznaczony dla właściciela i małej grupy znajomych. Szczegółowy publiczny proces współpracy nie został jeszcze utworzony.

## Licencja

Nie wybrano jeszcze licencji open-source.

## Dokumentacja

- [docs/VISION.md](docs/VISION.md)
- [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md)
- [docs/ROADMAP.md](docs/ROADMAP.md)
- [docs/COMPLIANCE.md](docs/COMPLIANCE.md)
- [docs/DECISIONS.md](docs/DECISIONS.md)

## Obecne Ograniczenia

- Skupienie na Path of Exile 1.
- Najpierw angielski klient gry.
- Tylko Windows.
- Wczesny prototyp.
- Niekompletny parser i katalogi danych.
- Brak production-ready integracji Trade, OAuth, stash lub economy.
