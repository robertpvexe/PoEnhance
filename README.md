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
- `PoEnhance.Core/` - shared business logic class library.
- `PoEnhance.Core.Tests/` - xUnit tests for `PoEnhance.Core`.
- `PoEnhance.GameData/` - independent shared game-data class library for versioned provider-neutral package manifests, item bases, modifier tiers, stat definitions, and stat translations.
- `PoEnhance.GameData.Tests/` - xUnit tests for `PoEnhance.GameData`.
- `PoEnhance.DataImport/` - local-file-only data import adapters; currently imports RePoE `base_items.json`, `mods.json`, `stats.json`, and `stat_translations.json` into provider-neutral game-data records without automatic downloading.
- `PoEnhance.DataImport.Tests/` - xUnit tests for `PoEnhance.DataImport`.
- `PoEnhance.DataTool/` - developer command-line tool for building a complete local PoEnhance game-data package from local RePoE files.
- `PoEnhance.DataTool.Tests/` - xUnit tests for `PoEnhance.DataTool`.
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

From the repository root:

```powershell
dotnet test
```

## Build Local Game-Data Package

`PoEnhance.DataTool` builds a complete local game-data package from local RePoE files. It does not download RePoE data or contact the network. Current local development packages use the active `repoe-fork/repoe` PoE1 export at commit `c50acab2ed660a70511e7f91ee09db4e632089e4`.

Package builds are source-guarded. The command must declare the git checkout root, exported data root, repository URI, branch, and exact source SHA. The build fails before writing output if the local git checkout does not match those values or if any input JSON file is outside the declared data root. The package manifest records the source identity plus SHA-256 fingerprints for the four input files.

The item-property semantic file is a reviewed, source-controlled PoEnhance input that supplies structural meaning RePoE does not expose. It is imported separately from the guarded RePoE exports and fingerprinted with its schema and review versions in package provenance. Any import or validation failure leaves the last valid output package unchanged.

```powershell
dotnet run --project .\PoEnhance.DataTool -- build-package `
  --base-items .\artifacts\source-audit\active-poe1\base_items.json `
  --mods .\artifacts\source-audit\active-poe1\mods.json `
  --stats .\artifacts\source-audit\active-poe1\stats.json `
  --translations .\artifacts\source-audit\active-poe1\stat_translations.json `
  --item-property-semantics .\data\semantics\item-property-semantics.json `
  --output .\artifacts\poenhance-game-data.json `
  --source-root .\artifacts\source-audit\repoe-fork `
  --source-data-root .\artifacts\source-audit\active-poe1 `
  --source-uri https://github.com/repoe-fork/repoe `
  --source-branch master `
  --source-version c50acab2ed660a70511e7f91ee09db4e632089e4 `
  --data-version dev-001
```

When a valid existing package must be bridged because its original RePoE exports are no longer available, `augment-package-semantics` can produce a separate candidate with the reviewed semantic dataset:

```powershell
dotnet run --project .\PoEnhance.DataTool\PoEnhance.DataTool.csproj -- augment-package-semantics `
  --input-package .\artifacts\poenhance-game-data.json `
  --item-property-semantics .\data\semantics\item-property-semantics.json `
  --output .\artifacts\poenhance-game-data.candidate.json `
  --data-version dev-milestone-3-semantics-weapon-dps-v1
```

The augmentation command never overwrites its input, and a failure preserves any previous candidate. Normal future league updates should continue to use the full source-guarded `build-package` pipeline.

Generated packages are local artifacts and are not committed. Use local paths such as `artifacts/`, `local-data/`, or `data/repoe/` for generated packages and local source snapshots.

The current active-fork package build imports 4,639 item bases, 38,800 modifiers, 22,774 stats, and 11,060 stat translations with zero validation errors. `Inscribed Ultimatum` and `Card Belt` are still not exact imported item-base names; `Engraved Ultimatum` remains the catalog base for the related Ultimatum map record.

## Load Local Game-Data Packages

`PoEnhance.GameData` can load complete local package files or streams asynchronously, deserialize them with the shared package JSON options, and validate them before use. Runtime lookup catalogs are provider-neutral and are built in memory only from validated packages.

`PoEnhance.Core` can enrich an already parsed item with provider-neutral item-base resolution when a caller supplies a validated `GameDataCatalog`. Exact parsed base-type matches are catalog-backed only when one item-class-compatible record remains; identified magic item names use a conservative token-boundary suffix match and are reported as probable. It can also discover modifier candidates from Advanced Item Description modifier names plus parsed generation kind, then narrow those candidates with verified item-base eligibility when exactly one catalog item base has been resolved. Eligibility uses provider-neutral base domain, static base tags, dynamic traditional-influence item-context tags, modifier domain, and ordered modifier spawn weights. After eligibility, modifier resolution can compare conservative stat-text signatures built from ordered modifier stats and imported stat translations: numeric rolls are placeholders, definitive text-shape mismatches are excluded, and unsupported or ambiguous translation shapes remain Unknown instead of being guessed. Traditional influence context is derived from parsed item influence lines and the resolved item-base tags; it is not stored back into the item-base record, and eldritch influences do not unlock traditional influence affixes. If the base is unresolved or ambiguous, modifier resolution preserves the earlier name-and-kind candidates instead of guessing.

The development application can load one local generated package for a session and show item-base resolution plus modifier candidate diagnostics in the developer parser UI. Modifier debug output shows the parsed modifier name, final status, name/kind/eligibility/text narrowing counts, a limited candidate preview, and the diagnostic code for supported modifier kinds; unsupported placeholder kinds remain parsed but do not append repetitive candidate debug blocks in the regular panel. Description remains stored on `ParsedItem` and visible in Raw Input, but it is hidden from the regular development result panel. Configure the package with `--game-data <path>`, `POENHANCE_GAMEDATA_PATH`, or the development fallback `artifacts/poenhance-game-data.json` discovered from the current directory or parent repository directories. Missing or invalid game data does not prevent parser operation; the UI degrades to parser-only output.

The development application also keeps a minimal local provisional game-data store at `%LOCALAPPDATA%\PoEnhance\provisional-game-data.json`. It records only high-confidence unresolved identities found during normal parsing and catalog resolution: missing parsed item bases with a `BASE_NOT_FOUND` result, and missing authentic Advanced modifier names with zero catalog name candidates and a supported generation kind. Records are provider-neutral, local-only, deduplicated by stable key, and store source, timestamps, league or patch context, confidence, and concise discovery context. They do not store full copied item text, seller notes, account names, prices, or clipboard history, and they do not automatically become canonical game data. Review, enrichment, promotion, and management UI remain deferred.

Production package activation, installation, rollback, automatic updates, and update UI are not implemented yet. Parser enrichment does not load package files or create global catalog state. Modifier tier inference, value-range matching, full rendered-stat/value matching, Trade stat mapping, fuzzy matching, and statless RePoE concepts audited in `docs/development/repoe-skipped-record-audit-2026-07-12.md` remain deferred.

## Development Logs

PoEnhance.App writes local development logs to `%LOCALAPPDATA%\PoEnhance\Logs`.

PoEnhance.App writes provisional game-data records to `%LOCALAPPDATA%\PoEnhance\provisional-game-data.json`.

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
- RePoE importing is local-file-only for supported files; there is no automatic data downloading or update workflow yet.
- Complete local game-data packages can be loaded and validated. The development App can load one local package for the current session through `--game-data <path>`, `POENHANCE_GAMEDATA_PATH`, or the `artifacts/poenhance-game-data.json` fallback.
- Parsed items can be enriched with item-base resolution and Advanced metadata modifier candidate discovery in `PoEnhance.Core` when a caller supplies a validated `GameDataCatalog`; modifier candidates can be narrowed by verified base eligibility, dynamic traditional-influence item-context tags, and conservative stat-text signature matching. Unresolved or ambiguous bases preserve earlier candidates, and unsupported translation shapes remain Unknown. Tier inference, modifier value-range matching, full rendered-stat/value matching, and fuzzy matching are not implemented yet.
- Provisional game-data records capture only high-confidence unresolved identities in a local provider-neutral JSON store. They are not a second canonical data package and are not automatically promoted into trusted game data.
- Production package installation, activation, rollback, automatic updates, and update UI are not implemented yet.
- Runtime lookup indexes are provider-neutral and intentionally exclude statless RePoE behavior records until a separate future model is designed.
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
- `PoEnhance.Core/` - biblioteka klas dla współdzielonej logiki biznesowej.
- `PoEnhance.Core.Tests/` - testy xUnit dla `PoEnhance.Core`.
- `PoEnhance.GameData/` - niezależna biblioteka klas dla wersjonowanych, provider-neutral manifestów pakietów, baz przedmiotów, tierów modifierów, definicji statystyk i tłumaczeń statystyk.
- `PoEnhance.GameData.Tests/` - testy xUnit dla `PoEnhance.GameData`.
- `PoEnhance.DataImport/` - adaptery importu danych tylko z lokalnych plików; obecnie importuje RePoE `base_items.json`, `mods.json`, `stats.json` i `stat_translations.json` do provider-neutral rekordów game-data bez automatycznego pobierania.
- `PoEnhance.DataImport.Tests/` - testy xUnit dla `PoEnhance.DataImport`.
- `PoEnhance.DataTool/` - narzędzie developerskie command-line do budowania kompletnego lokalnego pakietu game-data PoEnhance z lokalnych plików RePoE.
- `PoEnhance.DataTool.Tests/` - testy xUnit dla `PoEnhance.DataTool`.
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

Z katalogu głównego repozytorium:

```powershell
dotnet test
```

## Budowanie Lokalnego Pakietu Game-Data

Obecne lokalne pakiety developerskie uzywaja aktywnego eksportu PoE1 `repoe-fork/repoe` z commita `c50acab2ed660a70511e7f91ee09db4e632089e4`. Budowanie pakietu ma source guard: komenda musi podac root checkoutu git, root wyeksportowanych danych, URI repozytorium, branch i dokladny SHA zrodla. Build konczy sie przed zapisem outputu, jesli lokalny checkout git nie pasuje do tych wartosci albo jesli ktorykolwiek input JSON jest poza zadeklarowanym data root. Manifest pakietu zapisuje tozsamosc zrodla oraz SHA-256 fingerprints czterech plikow input.

`PoEnhance.DataTool` buduje kompletny lokalny pakiet game-data z lokalnych plików RePoE. Narzędzie nie pobiera danych RePoE i nie kontaktuje się z siecią.

```powershell
dotnet run --project .\PoEnhance.DataTool -- build-package `
  --base-items .\artifacts\source-audit\active-poe1\base_items.json `
  --mods .\artifacts\source-audit\active-poe1\mods.json `
  --stats .\artifacts\source-audit\active-poe1\stats.json `
  --translations .\artifacts\source-audit\active-poe1\stat_translations.json `
  --item-property-semantics .\data\semantics\item-property-semantics.json `
  --output .\artifacts\poenhance-game-data.json `
  --source-root .\artifacts\source-audit\repoe-fork `
  --source-data-root .\artifacts\source-audit\active-poe1 `
  --source-uri https://github.com/repoe-fork/repoe `
  --source-branch master `
  --source-version c50acab2ed660a70511e7f91ee09db4e632089e4 `
  --data-version dev-001
```

Wygenerowane pakiety są lokalnymi artefaktami i nie są commitowane. Do wygenerowanych pakietów i lokalnych snapshotów źródłowych używaj ścieżek takich jak `artifacts/`, `local-data/` lub `data/repoe/`.

## Ladowanie Lokalnych Pakietow Game-Data

`PoEnhance.GameData` moze ladowac kompletne lokalne pliki lub streamy pakietow asynchronicznie, deserializowac je wspolnymi opcjami JSON i walidowac przed uzyciem. Runtime lookup catalogs sa provider-neutral i powstaja w pamieci tylko z poprawnie zwalidowanych pakietow.

`PoEnhance.Core` moze wzbogacic juz sparsowany przedmiot o provider-neutral rozpoznanie item-base, gdy caller przekaze zwalidowany `GameDataCatalog`. Exact match z parsowanego base type jest uznawany tylko wtedy, gdy zostaje jeden rekord zgodny z item class; rozpoznanie nazw zidentyfikowanych magic itemow uzywa konserwatywnego suffix match na granicach tokenow i ma status probable. Core moze tez odkrywac pierwszy etap kandydatow modifierow z nazw modifierow w Advanced Item Description oraz sparsowanego generation kind. Candidate discovery jest exact i konserwatywne: ambiguous nazwy pozostaja nierozwiazanymi zbiorami kandydatow, a normalny opis przedmiotu nie jest reverse-engineered do tozsamosci modifierow.

Development App moze zaladowac jeden lokalnie wygenerowany pakiet na sesje i pokazac item-base resolution oraz diagnostyke kandydatow modifierow w developerskim UI parsera. Sciezke mozna ustawic przez `--game-data <path>`, `POENHANCE_GAMEDATA_PATH` albo development fallback `artifacts/poenhance-game-data.json` znaleziony z current directory lub parent repository directories. Brak lub niepoprawne game data nie blokuje parsera; UI degraduje sie do parser-only output.

Production package activation, instalacja, rollback, automatyczne aktualizacje i update UI nie sa jeszcze zaimplementowane. Parser enrichment nie laduje plikow pakietu i nie tworzy globalnego stanu katalogu. Modifier value matching, tier resolution, rendered stat matching, Trade stat mapping, fuzzy matching i statless koncepty RePoE opisane w `docs/development/repoe-skipped-record-audit-2026-07-12.md` pozostaja odlozone.

## Logi Developerskie

PoEnhance.App zapisuje lokalne logi developerskie w `%LOCALAPPDATA%\PoEnhance\Logs`.

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
- Import RePoE działa tylko z obsługiwanych lokalnych plików; nie ma jeszcze automatycznego pobierania danych ani workflow aktualizacji.
- Kompletne lokalne pakiety game-data moga byc ladowane i walidowane. Development App moze zaladowac jeden lokalny pakiet na aktualna sesje przez `--game-data <path>`, `POENHANCE_GAMEDATA_PATH` albo fallback `artifacts/poenhance-game-data.json`.
- Sparsowane przedmioty moga byc wzbogacane o item-base resolution i pierwszy etap Advanced metadata modifier candidate discovery w `PoEnhance.Core`, gdy caller przekaze zwalidowany `GameDataCatalog`; modifier value/tier matching i fuzzy matching nie sa jeszcze zaimplementowane.
- Production package installation, activation, rollback, automatyczne aktualizacje i update UI nie sa jeszcze zaimplementowane.
- Runtime lookup indexes sa provider-neutral i celowo wykluczaja statless rekordy RePoE do czasu zaprojektowania osobnego przyszlego modelu.
- Brak production-ready integracji Trade, OAuth, stash lub economy.
