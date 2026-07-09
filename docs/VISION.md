# PoEnhance Vision

## 1. Purpose

PoEnhance is a Windows 10/11 desktop companion application for Path of Exile 1. It runs in the background and provides a keyboard-driven in-game overlay only when Path of Exile is the active window.

The project exists to bring commonly used Path of Exile tools and information sources into one free, local-first application that assists players without automating gameplay.

## 2. Problem Statement

Path of Exile players often rely on many separate tools, websites, spreadsheets, and community resources to answer routine questions about item value, trade, economy state, stash contents, crafting inputs, regex filters, and game data.

This fragmented workflow creates friction, duplicated data handling, inconsistent results, and extra context switching during play. PoEnhance aims to reduce that friction while remaining compliant with Grinding Gear Games rules and API policies.

## 3. Target Users

The initial users are the project owner and a small group of friends who play Path of Exile 1 and want a practical desktop companion for common league and trade workflows.

The application may be made public later, so core architecture, data modeling, and module boundaries should be designed with future extensibility in mind.

## 4. Product Vision

PoEnhance should become a modular Path of Exile 1 companion that combines overlay-based tools, local data, and shared game-data catalogs into a cohesive desktop experience.

The application should feel fast, dependable, and unobtrusive: available when requested by shortcut, quiet when not needed, and resilient when leagues, items, or external data sources change.

Outside the game, PoEnhance should remain available as a standard desktop application for browsing data, managing saved searches, viewing economy information, and using tools that do not require an active Path of Exile window.

## 5. Core Principles

- Comply with Grinding Gear Games rules, terms, and API policies.
- Assist the player without playing the game or automating gameplay.
- Prefer local-first data storage for user data, cached data, and application state.
- Use a modular architecture so tools can evolve independently.
- Handle unknown items, incomplete data, new leagues, and API changes gracefully.
- Maintain shared game-data catalogs that can be reused across modules.
- Support the English game client first while keeping the architecture ready for future languages.
- Focus on Path of Exile 1 at the beginning.

## 6. Initial Scope

The first stage should establish the application foundation and prove the core companion model:

- Windows desktop application shell.
- Background process behavior.
- Path of Exile active-window detection.
- Keyboard-shortcut activation.
- In-game overlay foundation.
- Local configuration and local data storage.
- Shared game-data core for reusable Path of Exile catalogs.
- Initial module boundaries for future tools such as price checking, trade search, stash value tracking, economy views, and regex helpers.

## 7. Out of Scope for the First Stage

- Gameplay automation, input automation, botting, or any feature that plays the game for the user.
- Support for Path of Exile 2.
- Non-English game client support.
- Public distribution, installer hardening, or broad user onboarding.
- Full implementation of every planned module.
- Cloud synchronization or account-based hosted services.
- Real-money trading support or any workflow that violates game rules or policies.

## 8. Long-Term Direction

PoEnhance should grow into a free, extensible Path of Exile 1 toolkit that unifies high-value player workflows in one application.

Planned long-term modules include price checking, economy and stash value tracking, trade search, regex tools, and a shared searchable Path of Exile data core. As the application matures, modules should reuse common catalogs, storage, settings, overlay infrastructure, and compliance-aware integration patterns.

If the project becomes public, the architecture should support maintainable releases, clear module ownership, safe API usage, and graceful adaptation to new leagues and game-data changes.

---

# PoEnhance — Wizja projektu (wersja polska)

Wersja angielska jest kanonicznym źródłem wizji projektu. Ta sekcja po polsku jest tłumaczeniem pomocniczym.

## 1. Cel

PoEnhance jest aplikacją desktopową dla Windows 10/11 wspierającą Path of Exile 1. Działa w tle i udostępnia sterowany skrótami klawiaturowymi overlay w grze tylko wtedy, gdy Path of Exile jest aktywnym oknem.

Projekt istnieje po to, aby połączyć często używane narzędzia i źródła informacji dla Path of Exile w jedną darmową aplikację local-first, która pomaga graczom bez automatyzowania gameplay.

## 2. Opis problemu

Gracze Path of Exile często korzystają z wielu oddzielnych narzędzi, stron internetowych, arkuszy kalkulacyjnych i zasobów społeczności, aby odpowiadać na rutynowe pytania dotyczące wartości przedmiotów, handlu, stanu ekonomii, zawartości stash, materiałów craftingowych, filtrów regex i danych gry.

Ten rozproszony workflow powoduje tarcie, duplikację obsługi danych, niespójne wyniki i dodatkowe przełączanie kontekstu podczas gry. PoEnhance ma zmniejszyć to tarcie, pozostając zgodnym z zasadami Grinding Gear Games i politykami API.

## 3. Użytkownicy docelowi

Początkowymi użytkownikami są właściciel projektu i mała grupa znajomych grających w Path of Exile 1, którzy chcą praktycznej aplikacji desktopowej wspierającej typowe league i trade workflows.

Aplikacja może zostać później udostępniona publicznie, dlatego podstawowa architektura, modelowanie danych i granice modułów powinny być projektowane z myślą o przyszłej rozszerzalności.

## 4. Wizja produktu

PoEnhance powinien stać się modularnym companionem dla Path of Exile 1, który łączy narzędzia oparte na overlay, dane lokalne i współdzielone katalogi game-data w spójne doświadczenie desktopowe.

Aplikacja powinna być szybka, niezawodna i nienachalna: dostępna po użyciu skrótu, cicha, gdy nie jest potrzebna, oraz odporna na zmiany lig, przedmiotów lub zewnętrznych źródeł danych.

Poza grą PoEnhance powinien pozostać dostępny jako standardowa aplikacja desktopowa do przeglądania danych, zarządzania zapisanymi wyszukiwaniami, przeglądania informacji ekonomicznych i używania narzędzi, które nie wymagają aktywnego okna Path of Exile.

## 5. Główne zasady

- Przestrzegać zasad, warunków i polityk API Grinding Gear Games.
- Pomagać graczowi bez grania za niego lub automatyzowania gameplay.
- Preferować local-first storage dla danych użytkownika, danych w cache i stanu aplikacji.
- Używać modularnej architektury, aby narzędzia mogły rozwijać się niezależnie.
- Gracefully handle nieznane przedmioty, niekompletne dane, nowe ligi i zmiany API.
- Utrzymywać współdzielone katalogi game-data, które mogą być ponownie używane przez wiele modułów.
- Najpierw wspierać angielski klient gry, zachowując architekturę gotową na przyszłe języki.
- Na początku koncentrować się na Path of Exile 1.

## 6. Początkowy zakres

Pierwszy etap powinien ustanowić fundament aplikacji i potwierdzić podstawowy model companion:

- Powłoka aplikacji desktopowej Windows.
- Zachowanie procesu działającego w tle.
- Wykrywanie aktywnego okna Path of Exile.
- Aktywacja skrótem klawiaturowym.
- Fundament overlay w grze.
- Lokalna konfiguracja i lokalny storage danych.
- Wspólny rdzeń game-data dla wielokrotnego użycia katalogów Path of Exile.
- Początkowe granice modułów dla przyszłych narzędzi, takich jak price checking, trade search, stash value tracking, widoki ekonomii i pomocniki regex.

## 7. Poza zakresem pierwszego etapu

- Automatyzacja gameplay, input automation, botting lub dowolna funkcja, która gra za użytkownika.
- Wsparcie dla Path of Exile 2.
- Wsparcie klienta gry innego niż angielski.
- Publiczna dystrybucja, dopracowanie instalatora lub szeroki onboarding użytkowników.
- Pełna implementacja każdego planowanego modułu.
- Cloud synchronization lub usługi hostowane oparte na kontach.
- Wsparcie real-money trading lub jakikolwiek workflow naruszający zasady gry albo polityki.

## 8. Kierunek długoterminowy

PoEnhance powinien rozwinąć się w darmowy, rozszerzalny toolkit dla Path of Exile 1, który łączy wartościowe workflows gracza w jednej aplikacji.

Planowane moduły długoterminowe obejmują price checking, economy and stash value tracking, trade search, narzędzia regex oraz współdzielony, przeszukiwalny rdzeń danych Path of Exile. Wraz z dojrzewaniem aplikacji moduły powinny ponownie używać wspólnych katalogów, storage, ustawień, infrastruktury overlay i wzorców integracji uwzględniających zgodność.

Jeśli projekt stanie się publiczny, architektura powinna wspierać utrzymywalne wydania, jasną własność modułów, bezpieczne użycie API i graceful adaptation do nowych lig oraz zmian game-data.
