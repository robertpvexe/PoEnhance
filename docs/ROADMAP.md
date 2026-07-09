# PoEnhance Roadmap

## 1. Roadmap Principles

- Prefer small incremental changes that can be reviewed and tested.
- Keep working software after each milestone.
- Update documentation before major implementation work.
- Review GGG compliance for major features and integration changes.
- Avoid unnecessary scope expansion.
- Keep the architecture ready for later modules without implementing them early.
- Treat priorities as adaptable after testing and user feedback.
- Treat version 0.1 as a technical prototype, not a public release.

## 2. Milestone 0 — Project Foundation

### Goal

Create a clean and maintainable engineering base.

### Main Tasks

- Complete initial documentation.
- Establish Git workflow.
- Confirm .NET 10 and WPF project setup.
- Define project and module structure.
- Establish coding conventions.
- Add basic local logging.
- Add initial test project.
- Ensure clean build and run process.

### Completion Criteria

- Project builds and runs.
- Documentation is committed.
- Basic test project runs.
- Repository has no generated build files committed.

### Explicitly Excluded Work

- Feature-complete modules.
- Public release infrastructure.
- Full game-data catalog implementation.

## 3. Milestone 1 — Version 0.1 Technical Prototype

Use requirements V01-001 through V01-013. This milestone proves the core companion model without requiring Trade API, OAuth, stash value, or a full game-data database.

### Goal

Build a technical prototype that can run in the background, detect Path of Exile state, capture item text, parse basic item data, and display it in a basic overlay.

### Main Tasks

#### 3.1 Application Shell

- Implement WPF application shell (V01-001).
- Add background and tray-ready lifecycle foundation (V01-002).
- Add basic navigation or development screen.
- Add local logging sufficient for development (V01-012).

#### 3.2 Path of Exile Detection

- Detect Path of Exile process (V01-003, LC-004).
- Detect whether the Path of Exile window is active (V01-004, LC-005).
- Expose detection state to the UI.
- Allow testing when the game is not running (NFR-004).

#### 3.3 Shortcut and Clipboard Capture

- Add configurable shortcut foundation (V01-005).
- Ensure shortcut behavior is active only when Path of Exile is foreground (LC-003).
- Capture normal or advanced item text through clipboard (V01-006, POE-004).
- Preserve raw clipboard input (POE-006).
- Avoid reading game memory (SEC-001).

#### 3.4 Basic Item Parser

- Parse item class, rarity, name, and base type (V01-008, V01-009).
- Parse basic properties and visible mods where practical.
- Tolerate missing or unknown sections (PC-PARSE-003, ERR-001).
- Add automated parser tests using saved clipboard samples where practical (V01-013).

#### 3.5 Basic Overlay

- Show a basic overlay only when requested (V01-010).
- Display parsed name, rarity, base type, and recognized mod lines (V01-009).
- Keep parsing logic independent from WPF (V01-011, NFR-003).
- Confirm overlay can be shown and closed without affecting gameplay.

#### 3.6 Manual Input Outside the Game

- Allow manually pasted item text (V01-007, PC-IN-004).
- Use the same parser as in-game clipboard capture.
- Display parse errors clearly (ERR-004).

### Completion Criteria

- The application runs in the background.
- Game process and foreground state are detected.
- A shortcut captures an item when Path of Exile is active.
- The item is parsed and displayed in a basic overlay.
- Pasted item text works outside the game.
- Parser tests exist where practical.
- No Trade API, OAuth, stash value, or full game-data database is required.

### Explicitly Excluded Work

- Complete Trade API integration (V01-NR-001).
- Complete OAuth integration (V01-NR-002).
- Stash value (V01-NR-003).
- Full economy module (V01-NR-004).
- Complete local game database (V01-NR-005).
- Support for every unusual item type (V01-NR-006).
- Polished final UI (V01-NR-007).

## 4. Milestone 2 — Shared Data Core Foundation

### Goal

Create reusable data infrastructure for future modules.

### Main Tasks

- Define shared item and modifier models (DATA-001, DATA-002).
- Define local catalog interfaces for items, modifiers, bases, categories, and properties.
- Support versioned data packages where practical (DATA-006).
- Add provisional record model (PC-REC-007, STORE-004).
- Support confidence states: Exact, Probable, Generic, Unknown (PC-PARSE-004).
- Store source, timestamp, league or patch context, and confidence metadata (PC-REC-008).
- Define interfaces for future external fallback providers (PC-REC-004, EXT-003).
- Ensure unknown items degrade gracefully (PC-REC-009, ERR-001).

### Completion Criteria

- Shared data models can be used outside the price checker.
- Unknown or missing catalog data does not break parsing or display.
- Provisional records can be represented and stored locally.
- Data package boundaries are clear enough to evolve independently.

### Explicitly Excluded Work

- Complete Path of Exile database.
- Full support for all unusual item classes.
- Production fallback-provider integration.

## 5. Milestone 3 — Basic Trade Price Checker

### Goal

Connect parsed individual items to official Trade search behavior.

### Main Tasks

- Add local request validation (PC-REQ-001 through PC-REQ-004).
- Define item Trade / Manage Shop query model (PC-TRADE-001, PC-TRADE-002).
- Use Merchant Only as the initial default for individual-item searches (PC-TRADE-003).
- Allow switching to in-person trade (PC-TRADE-004).
- Require a manual Search action (PC-RARE-003, PC-RARE-004).
- Read and respect rate-limit information (PC-REQ-005 through PC-REQ-010).
- Load the first 10 detailed offers (PC-RES-001).
- Load more results in batches of 10 (PC-RES-002, PC-RES-003).
- Show exact listing prices (PC-PRICE-001).
- Add search cache with visible age (CACHE-001 through CACHE-004).
- Separate no-results, invalid-parameters, network, and rate-limit states (PC-ERR-001).
- Add hover preview and persistent item card if practical (PC-CARD-001 through PC-CARD-007).

### Completion Criteria

- A parsed individual item can be turned into a validated search.
- No locally invalid request is knowingly sent.
- Initial results and Load More behavior work without repeating the original search unnecessarily.
- Rate-limit state is visible where relevant.
- Cached data clearly shows its age.
- Result errors are distinguishable.

### Explicitly Excluded Work

- Advanced price estimation (PC-PRICE-004).
- Bulk Search or Bulk Buy (DEF-001).
- Complete support for unusual item types (ITEM-001 through ITEM-013).
- Automatic loosening of filters or alternative searches (PC-ERR-002).

## 6. Milestone 4 — Currency Exchange Price Path

### Goal

Support stackable items through a separate Currency Exchange flow.

### Main Tasks

- Classify stackable versus individual items (PC-TRADE-001).
- Define separate Currency Exchange query and result model (PC-TRADE-002).
- Show exchange rates where available.
- Apply appropriate cache behavior (CACHE-005, CACHE-006).
- Support currency icons (VIS-002).
- Evaluate optional currency conversion if validated (PC-PRICE-002, PC-PRICE-003).

### Completion Criteria

- Stackable items follow the Currency Exchange path.
- Individual items continue to follow item Trade / Manage Shop behavior.
- Currency Exchange results have clear pricing and cache freshness.
- Optional conversion remains disabled or clearly marked until validated.

### Explicitly Excluded Work

- Statistical price estimation.
- Bulk-buy optimization.
- Any rate-limit bypass behavior.

## 7. Milestone 5 — Authentication and Account Data

### Goal

Add official GGG OAuth and account-dependent feature foundations.

### Main Tasks

- Implement browser-based OAuth (AUTH-001, AUTH-002).
- Ensure PoEnhance never requests or stores the user's GGG password (AUTH-003).
- Store tokens securely.
- Refresh tokens automatically where possible (AUTH-004).
- Show reconnection prompt when reauthentication is required (AUTH-005).
- Keep non-account features usable when authentication expires (AUTH-006).
- Prepare foundation for stash access (AUTH-007).

### Completion Criteria

- User can authenticate through the official Path of Exile website.
- Account-dependent state can detect expired authentication.
- Non-account features continue working after authentication expiry.
- Secrets are not included in ordinary exports (STORE-006).

### Explicitly Excluded Work

- Stash valuation as a finished feature.
- Cloud synchronization.
- Password-based authentication.

## 8. Milestone 6 — Stash Value and Economy Foundation

### Goal

Introduce account stash valuation and economy data at a foundation level.

Detailed requirements for this milestone are not yet fully designed, so work should remain high-level and validated before implementation.

### Main Tasks

- Retrieve stash data where officially supported.
- Reuse pricing sources and shared data models.
- Add current economy view foundation.
- Prepare foundation for historical economy data.
- Isolate failures from other modules (LC-006, EXT-005).

### Completion Criteria

- Account stash data can be loaded where officially supported.
- Stash-related failures do not break unrelated modules.
- Economy data has clear source and freshness information.
- Additional requirements are documented before broad implementation.

### Explicitly Excluded Work

- Full economy module before detailed requirements exist.
- Public release infrastructure.
- Cloud synchronization.

## 9. Candidate Milestones

These are not committed milestones. They may be promoted after validation.

- Recent-search history (PC-HIST-001).
- Saved items and valuations (PC-HIST-004 through PC-HIST-006).
- Pinned item comparisons (PC-HIST-007).
- Saved-search import/export (PC-HIST-008).
- Automatic important-mod selection (PC-RARE-006, CAND-010).
- Price estimation (PC-PRICE-004, CAND-003).
- Regex tools.
- Searchable Path of Exile data browser.
- Crafting-oriented data views.
- Public distribution and updater.

## 10. Deferred Work

- Bulk Search / Bulk Buy (DEF-001).
- Lowest-cost versus fewest-hideouts optimization (DEF-002).
- Cloud synchronization (DEF-005).
- Path of Exile 2 support (DEF-006).
- Full public release infrastructure (DEF-004).
- Special item TODOs from REQUIREMENTS.md (ITEM-001 through ITEM-013).

## 11. Review Gates

Before moving between major milestones, require:

- Requirements review.
- Architecture impact review.
- GGG compliance review where relevant.
- Test results.
- Manual usability check.
- Documentation update.

---

# PoEnhance — Mapa rozwoju (wersja polska)

Wersja angielska jest kanonicznym źródłem mapy rozwoju. Ta sekcja po polsku jest tłumaczeniem pomocniczym.

## 1. Zasady Mapy Rozwoju

- Preferować małe, inkrementalne zmiany, które można przejrzeć i przetestować.
- Zachowywać działające oprogramowanie po każdym kamieniu milowym.
- Aktualizować dokumentację przed dużymi pracami implementacyjnymi.
- Przeprowadzać review zgodności z GGG dla dużych funkcji i zmian integracyjnych.
- Unikać niepotrzebnego rozszerzania zakresu.
- Utrzymywać architekturę gotową na późniejsze moduły bez implementowania ich zbyt wcześnie.
- Traktować priorytety jako adaptowalne po testach i feedbacku użytkowników.
- Traktować wersję 0.1 jako prototyp techniczny, a nie publiczne wydanie.

## 2. Milestone 0 — Fundament Projektu

### Cel

Stworzyć czystą i utrzymywalną bazę inżynieryjną.

### Główne Zadania

- Ukończyć początkową dokumentację.
- Ustanowić workflow Git.
- Potwierdzić konfigurację projektu .NET 10 i WPF.
- Zdefiniować strukturę projektu i modułów.
- Ustanowić konwencje kodowania.
- Dodać podstawowe lokalne logowanie.
- Dodać początkowy projekt testowy.
- Zapewnić czysty proces build i run.

### Kryteria Ukończenia

- Projekt buduje się i uruchamia.
- Dokumentacja jest zacommitowana.
- Podstawowy projekt testowy działa.
- Repozytorium nie zawiera zacommitowanych wygenerowanych plików build.

### Jawnie Wykluczone Prace

- Moduły kompletne funkcjonalnie.
- Infrastruktura publicznego wydania.
- Pełna implementacja katalogu game-data.

## 3. Milestone 1 — Prototyp Techniczny Wersji 0.1

Używa wymagań V01-001 do V01-013. Ten milestone potwierdza podstawowy model companion bez wymagania Trade API, OAuth, stash value lub pełnej bazy game-data.

### Cel

Zbudować prototyp techniczny, który może działać w tle, wykrywać stan Path of Exile, przechwytywać tekst przedmiotu, parsować podstawowe dane przedmiotu i wyświetlać je w podstawowym overlay.

### Główne Zadania

#### 3.1 Powłoka Aplikacji

- Zaimplementować powłokę aplikacji WPF (V01-001).
- Dodać fundament lifecycle w tle i gotowy pod tray (V01-002).
- Dodać podstawową nawigację lub ekran developerski.
- Dodać lokalne logowanie wystarczające do developmentu (V01-012).

#### 3.2 Wykrywanie Path of Exile

- Wykrywać proces Path of Exile (V01-003, LC-004).
- Wykrywać, czy okno Path of Exile jest aktywne (V01-004, LC-005).
- Udostępnić stan wykrywania w UI.
- Umożliwić testowanie, gdy gra nie jest uruchomiona (NFR-004).

#### 3.3 Skrót i Przechwytywanie Clipboard

- Dodać fundament konfigurowalnego skrótu (V01-005).
- Zapewnić, że zachowanie skrótu jest aktywne tylko wtedy, gdy Path of Exile jest foreground (LC-003).
- Przechwytywać normalny lub zaawansowany tekst przedmiotu przez clipboard (V01-006, POE-004).
- Zachować surowy input z clipboard (POE-006).
- Unikać czytania pamięci gry (SEC-001).

#### 3.4 Podstawowy Parser Przedmiotów

- Parsować item class, rarity, name i base type (V01-008, V01-009).
- Parsować podstawowe properties i widoczne mody tam, gdzie jest to praktyczne.
- Tolerować brakujące lub nieznane sekcje (PC-PARSE-003, ERR-001).
- Dodać automatyczne testy parsera z użyciem zapisanych próbek clipboard tam, gdzie jest to praktyczne (V01-013).

#### 3.5 Podstawowy Overlay

- Pokazywać podstawowy overlay tylko na żądanie (V01-010).
- Wyświetlać sparsowane name, rarity, base type i rozpoznane linie modów (V01-009).
- Utrzymać logikę parsowania niezależną od WPF (V01-011, NFR-003).
- Potwierdzić, że overlay może być pokazany i zamknięty bez wpływu na gameplay.

#### 3.6 Ręczny Input Poza Grą

- Umożliwić ręczne wklejenie tekstu przedmiotu (V01-007, PC-IN-004).
- Używać tego samego parsera co przechwytywanie clipboard w grze.
- Jasno wyświetlać błędy parsowania (ERR-004).

### Kryteria Ukończenia

- Aplikacja działa w tle.
- Wykrywany jest proces gry i stan foreground.
- Skrót przechwytuje przedmiot, gdy Path of Exile jest aktywne.
- Przedmiot jest parsowany i wyświetlany w podstawowym overlay.
- Wklejony tekst przedmiotu działa poza grą.
- Testy parsera istnieją tam, gdzie jest to praktyczne.
- Trade API, OAuth, stash value ani pełna baza game-data nie są wymagane.

### Jawnie Wykluczone Prace

- Pełna integracja Trade API (V01-NR-001).
- Pełna integracja OAuth (V01-NR-002).
- Stash value (V01-NR-003).
- Pełny moduł ekonomii (V01-NR-004).
- Kompletna lokalna baza game-data (V01-NR-005).
- Wsparcie dla każdego nietypowego typu przedmiotu (V01-NR-006).
- Dopracowane finalne UI (V01-NR-007).

## 4. Milestone 2 — Fundament Wspólnego Rdzenia Danych

### Cel

Stworzyć wielokrotnego użytku infrastrukturę danych dla przyszłych modułów.

### Główne Zadania

- Zdefiniować współdzielone modele przedmiotów i modifierów (DATA-001, DATA-002).
- Zdefiniować interfejsy lokalnych katalogów dla przedmiotów, modifierów, baz, kategorii i properties.
- Wspierać wersjonowane pakiety danych tam, gdzie jest to praktyczne (DATA-006).
- Dodać model prowizorycznego rekordu (PC-REC-007, STORE-004).
- Wspierać stany pewności: Exact, Probable, Generic, Unknown (PC-PARSE-004).
- Przechowywać źródło, timestamp, kontekst ligi lub patcha oraz metadane pewności (PC-REC-008).
- Zdefiniować interfejsy dla przyszłych zewnętrznych dostawców fallback (PC-REC-004, EXT-003).
- Zapewnić graceful handling nieznanych przedmiotów (PC-REC-009, ERR-001).

### Kryteria Ukończenia

- Współdzielone modele danych mogą być używane poza price checkerem.
- Nieznane lub brakujące dane katalogowe nie psują parsowania ani wyświetlania.
- Prowizoryczne rekordy mogą być reprezentowane i przechowywane lokalnie.
- Granice pakietów danych są wystarczająco jasne, aby ewoluować niezależnie.

### Jawnie Wykluczone Prace

- Kompletna baza Path of Exile.
- Pełne wsparcie wszystkich nietypowych klas przedmiotów.
- Produkcyjna integracja dostawców fallback.

## 5. Milestone 3 — Podstawowy Trade Price Checker

### Cel

Połączyć sparsowane individual items z oficjalnym zachowaniem wyszukiwania Trade.

### Główne Zadania

- Dodać lokalną walidację żądań (PC-REQ-001 do PC-REQ-004).
- Zdefiniować model zapytań item Trade / Manage Shop (PC-TRADE-001, PC-TRADE-002).
- Użyć Merchant Only jako początkowego domyślnego ustawienia dla wyszukiwań individual-item (PC-TRADE-003).
- Pozwolić na przełączenie na in-person trade (PC-TRADE-004).
- Wymagać ręcznej akcji Search (PC-RARE-003, PC-RARE-004).
- Odczytywać i respektować informacje rate-limit (PC-REQ-005 do PC-REQ-010).
- Ładować pierwszych 10 szczegółowych ofert (PC-RES-001).
- Ładować więcej wyników w partiach po 10 (PC-RES-002, PC-RES-003).
- Pokazywać dokładne ceny listingów (PC-PRICE-001).
- Dodać search cache z widocznym wiekiem (CACHE-001 do CACHE-004).
- Rozdzielić stany: brak wyników, nieprawidłowe parametry, sieć i rate-limit (PC-ERR-001).
- Dodać hover preview i trwałą kartę przedmiotu, jeśli praktyczne (PC-CARD-001 do PC-CARD-007).

### Kryteria Ukończenia

- Sparsowany individual item może zostać zamieniony w zwalidowane wyszukiwanie.
- Żadne lokalnie nieprawidłowe żądanie nie jest świadomie wysyłane.
- Początkowe wyniki i Load More działają bez niepotrzebnego powtarzania pierwotnego wyszukiwania.
- Stan rate-limit jest widoczny tam, gdzie jest to istotne.
- Dane z cache jasno pokazują swój wiek.
- Błędy wyników są rozróżnialne.

### Jawnie Wykluczone Prace

- Zaawansowana estymacja ceny (PC-PRICE-004).
- Bulk Search lub Bulk Buy (DEF-001).
- Pełne wsparcie nietypowych typów przedmiotów (ITEM-001 do ITEM-013).
- Automatyczne luzowanie filtrów lub alternatywne wyszukiwania (PC-ERR-002).

## 6. Milestone 4 — Ścieżka Cen Currency Exchange

### Cel

Wspierać stackable items przez oddzielny flow Currency Exchange.

### Główne Zadania

- Klasyfikować stackable i individual items (PC-TRADE-001).
- Zdefiniować oddzielny model zapytań i wyników Currency Exchange (PC-TRADE-002).
- Pokazywać exchange rates, gdy są dostępne.
- Zastosować odpowiednie zachowanie cache (CACHE-005, CACHE-006).
- Wspierać ikony walut (VIS-002).
- Ocenić opcjonalne currency conversion, jeśli zostanie zwalidowane (PC-PRICE-002, PC-PRICE-003).

### Kryteria Ukończenia

- Stackable items używają ścieżki Currency Exchange.
- Individual items nadal używają zachowania item Trade / Manage Shop.
- Wyniki Currency Exchange mają jasne ceny i świeżość cache.
- Opcjonalna konwersja pozostaje wyłączona albo jasno oznaczona do czasu walidacji.

### Jawnie Wykluczone Prace

- Statystyczna estymacja ceny.
- Optymalizacja bulk-buy.
- Jakiekolwiek zachowanie omijające rate limit.

## 7. Milestone 5 — Uwierzytelnianie i Dane Konta

### Cel

Dodać oficjalne OAuth GGG i fundamenty funkcji zależnych od konta.

### Główne Zadania

- Zaimplementować OAuth w przeglądarce (AUTH-001, AUTH-002).
- Zapewnić, że PoEnhance nigdy nie prosi o hasło GGG użytkownika ani go nie przechowuje (AUTH-003).
- Bezpiecznie przechowywać tokeny.
- Odświeżać tokeny automatycznie tam, gdzie jest to możliwe (AUTH-004).
- Pokazywać prompt ponownego połączenia, gdy wymagane jest ponowne uwierzytelnienie (AUTH-005).
- Utrzymać działanie funkcji niezależnych od konta po wygaśnięciu uwierzytelnienia (AUTH-006).
- Przygotować fundament pod dostęp do stash (AUTH-007).

### Kryteria Ukończenia

- Użytkownik może uwierzytelnić się przez oficjalną stronę Path of Exile.
- Stan zależny od konta może wykryć wygasłe uwierzytelnienie.
- Funkcje niezależne od konta nadal działają po wygaśnięciu uwierzytelnienia.
- Sekrety nie są uwzględniane w zwykłych eksportach (STORE-006).

### Jawnie Wykluczone Prace

- Stash valuation jako ukończona funkcja.
- Cloud synchronization.
- Uwierzytelnianie oparte na haśle.

## 8. Milestone 6 — Fundament Stash Value i Ekonomii

### Cel

Wprowadzić wycenę stash konta i dane ekonomii na poziomie fundamentu.

Szczegółowe wymagania dla tego milestone nie są jeszcze w pełni zaprojektowane, więc prace powinny pozostać wysokopoziomowe i zwalidowane przed implementacją.

### Główne Zadania

- Pobierać dane stash tam, gdzie jest to oficjalnie wspierane.
- Ponownie używać źródeł cen i współdzielonych modeli danych.
- Dodać fundament bieżącego widoku ekonomii.
- Przygotować fundament pod historyczne dane ekonomii.
- Izolować awarie od innych modułów (LC-006, EXT-005).

### Kryteria Ukończenia

- Dane stash konta mogą być ładowane tam, gdzie jest to oficjalnie wspierane.
- Awarie związane ze stash nie psują niezależnych modułów.
- Dane ekonomii mają jasne źródło i informację o świeżości.
- Dodatkowe wymagania są udokumentowane przed szeroką implementacją.

### Jawnie Wykluczone Prace

- Pełny moduł ekonomii przed powstaniem szczegółowych wymagań.
- Infrastruktura publicznego wydania.
- Cloud synchronization.

## 9. Milestone'y Kandydujące

To nie są zatwierdzone milestone'y. Mogą zostać awansowane po walidacji.

- Historia ostatnich wyszukiwań (PC-HIST-001).
- Zapisane przedmioty i wyceny (PC-HIST-004 do PC-HIST-006).
- Przypięte porównania przedmiotów (PC-HIST-007).
- Import/export zapisanych wyszukiwań (PC-HIST-008).
- Automatyczny wybór ważnych modów (PC-RARE-006, CAND-010).
- Estymacja ceny (PC-PRICE-004, CAND-003).
- Narzędzia regex.
- Przeszukiwalna przeglądarka danych Path of Exile.
- Widoki danych zorientowane na crafting.
- Publiczna dystrybucja i updater.

## 10. Prace Odłożone

- Bulk Search / Bulk Buy (DEF-001).
- Optymalizacja lowest-cost versus fewest-hideouts (DEF-002).
- Cloud synchronization (DEF-005).
- Wsparcie Path of Exile 2 (DEF-006).
- Pełna infrastruktura publicznego wydania (DEF-004).
- TODO dla specjalnych przedmiotów z REQUIREMENTS.md (ITEM-001 do ITEM-013).

## 11. Bramki Review

Przed przejściem między dużymi milestone'ami wymagane są:

- Review wymagań.
- Review wpływu na architekturę.
- Review zgodności z GGG tam, gdzie jest to istotne.
- Wyniki testów.
- Ręczny usability check.
- Aktualizacja dokumentacji.
