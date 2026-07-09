# PoEnhance Decision Log

This lightweight log records engineering decisions that shape PoEnhance. It should be updated when major requirements, architecture, compliance, or product choices change.

## Decision Statuses

- Accepted: agreed and currently active.
- Provisional: selected for now, but requiring validation.
- Deferred: intentionally postponed.
- Rejected: considered and not selected.
- Superseded: replaced by a newer decision.

## Decisions

| ID | Status | Decision | Reason | Consequences | Revisit trigger |
| --- | --- | --- | --- | --- | --- |
| DEC-001 | Accepted | Path of Exile 1 is the initial supported game. Path of Exile 2 is deferred. | The project scope starts with Path of Exile 1. | Early design can focus on one game. | Revisit when Path of Exile 2 support is considered. |
| DEC-002 | Accepted | Windows 11 is the primary platform. Windows 10 is supported where practical. Other operating systems are not initial targets. | PoEnhance is a Windows desktop companion. | Platform work can focus on Windows APIs and WPF. | Revisit if public distribution or user demand expands platform needs. |
| DEC-003 | Accepted | Initial users are the project owner and a small group of friends. Architecture should not prevent possible public distribution later. | Early users are limited, but extensibility matters. | UX and release process can stay lightweight while architecture remains maintainable. | Revisit before public release planning. |
| DEC-004 | Accepted | PoEnhance is local-first. Cloud synchronization is deferred. | Local-first behavior is a core principle. | Settings, cache, history, and provisional data should be local by default. | Revisit when cloud synchronization is reconsidered. |
| DEC-005 | Accepted | The application may work outside the game. Overlay shortcuts work only while Path of Exile is the active foreground window. | Some workflows are useful outside gameplay, while in-game behavior must be constrained. | Data browsing and manual input can work outside the game; shortcuts are gated by active-window detection. | Revisit if shortcut behavior changes. |
| DEC-006 | Accepted | Item input comes from clipboard data and manual pasted text. Game-memory reading is prohibited. | Compliance and safety require avoiding game-memory access. | Parser work must consume clipboard or pasted text. | Revisit only if official guidance changes and compliance review approves. |
| DEC-007 | Accepted | The English Path of Exile client is supported first. Architecture should allow additional languages later. | Initial scope is English, but future languages should not require redesign. | Parsing and catalogs should avoid hard assumptions that block localization. | Revisit when adding another game-client or UI language. |
| DEC-008 | Accepted | The application uses a modular architecture. Shared business logic must not depend directly on WPF. | Modules and shared logic need to remain reusable and testable. | UI, parsing, storage, integrations, and shared data should stay separated. | Revisit during architecture review after the technical prototype. |
| DEC-009 | Provisional | C#, .NET 10 LTS, and WPF are the initial technical stack. The choice must be validated by the version 0.1 technical prototype. | The stack fits the Windows desktop direction. | The prototype should prove build, UI, overlay, and lifecycle feasibility. | Revisit after version 0.1 technical prototype validation. |
| DEC-010 | Accepted | Shared game-data catalogs belong to a common data layer. Price Checker is only one consumer of those catalogs. | Multiple future modules need the same game data. | Catalogs should not be private to the price checker. | Revisit if module boundaries change. |
| DEC-011 | Accepted | Unknown items and new-league data must degrade gracefully. Recognition uses Exact, Probable, Generic, and Unknown confidence states. | Path of Exile data changes frequently. | Unknown data should not crash parsing or make the app unusable. | Revisit when recognition model changes. |
| DEC-012 | Accepted | Local catalogs are the primary recognition source. Online providers are fallback sources. Fallback results create provisional cached records. | Normal operation should not depend on online fallback providers. | Fallback data must be traceable and should not silently replace trusted catalogs. | Revisit when selecting fallback providers. |
| DEC-013 | Accepted | Individual-item searches default to Merchant Only. In-person trade remains selectable. | This is the current planned default for individual-item searches. | UI must allow switching trade mode. | Revisit after trade-search testing. |
| DEC-014 | Accepted | Rare-item searches are manual by default. Search occurs only after explicit user action. Automatic searching may become an optional setting later. | User control and rate-limit safety are required. | Opening the price checker or changing filters must not automatically search. | Revisit if optional automatic searching is designed. |
| DEC-015 | Accepted | Requests must be validated locally before contacting GGG. | Known-invalid requests should consume zero API requests. | Query builders need local validation before network calls. | Revisit when request models change. |
| DEC-016 | Accepted | GGG rate limits are dynamic and must be respected. Search, Refresh, and Load More should communicate their request impact. | Rate limits must not be bypassed or hardcoded permanently. | UI and integration logic must expose request state where relevant. | Revisit when GGG rate-limit behavior or APIs change. |
| DEC-017 | Accepted | Initial Trade results load 10 detailed offers. Additional offers load in explicit batches of 10. | This limits request cost and keeps loading explicit. | Load More must be user-triggered and reuse result identifiers where possible. | Revisit after trade-search usability testing. |
| DEC-018 | Accepted | Loaded offers may be previewed without another network request. Clicking an offer may open a persistent Path of Exile-style item card. | Already fetched listing data should be reused. | Item cards should not trigger extra requests for already loaded offers. | Revisit when item-card UX is validated. |
| DEC-019 | Accepted | Undocumented endpoints will not be used for direct Travel to Hideout. The idea may be reconsidered only if an officially supported method exists. | Hidden or undocumented integrations are outside scope. | Direct travel is not implemented unless official support is found. | Revisit if official documentation introduces a supported method. |
| DEC-020 | Provisional | Recent identical Trade queries may use approximately a two-minute cache. Currency Exchange may use approximately a one-minute cache. Final values require testing. | Initial cache assumptions reduce repeated requests. | Cache freshness must be visible and values may change. | Revisit after rate-limit and user testing. |
| DEC-021 | Deferred | Search history, saved items, saved valuations, pinned comparisons, and saved-search export/import require later UX validation. | These features are candidates, not core prototype scope. | They should not block version 0.1. | Revisit during candidate feature planning. |
| DEC-022 | Deferred | Bulk Search and Bulk Buy are not included in version 0.1. | Bulk workflows are deferred. | Version 0.1 remains focused on prototype fundamentals. | Revisit after basic trade search matures. |
| DEC-023 | Accepted | Version 0.1 is a technical prototype. It does not require Trade API, OAuth, stash value, a full economy module, a complete game-data database, or polished final UI. | The first version should prove foundations. | Scope is limited to shell, detection, shortcut, clipboard, parser, overlay, logging, and initial tests. | Revisit when version 0.1 is complete. |
| DEC-024 | Accepted | Official browser-based GGG OAuth will be used when account access is implemented. Expired authentication must not disable unrelated application features. | Account access must avoid password handling and isolate failures. | OAuth-dependent modules need reconnection handling. | Revisit when OAuth scopes and token storage are designed. |
| DEC-025 | Accepted | Hidden or undocumented GGG endpoints, gameplay automation, botting, memory reading, packet interception, and API-limit evasion are outside the project scope. | Compliance and safety are core project principles. | These behaviors must not be implemented. | Revisit only if official rules change and compliance review approves. |
| DEC-026 | Accepted | PoEnhance is intended to run primarily in the background. A dedicated configurable shortcut opens the main multitool menu, with Backslash (`\`) as the current default. | The application should stay unobtrusive while keeping core tools quickly accessible. | The main multitool menu becomes the entry point for modules such as regex tools, economy views, game-data browsing, and other future modules. | Revisit if the application-launch or main-menu model changes. |
| DEC-027 | Accepted | Price Checker has a separate dedicated configurable shortcut, with `Ctrl + D` as the confirmed default. The prototype `X` shortcut is temporary technical scaffolding and is not the final default. | Price checking is frequent enough to deserve a direct binding, while the current implementation still uses a temporary prototype shortcut. | Documentation and future implementation should not describe `X` as final; the app must support distinct menu and Price Checker bindings. | Revisit if shortcut defaults change after testing. |
| DEC-028 | Accepted | Shortcut bindings will later be editable in application Settings through user-facing input controls. | Users need control over key bindings without editing files manually. | Settings work must include shortcut editing, but persistence and Settings UI do not need to be completed in the current prototype step. | Revisit when Settings persistence and UI are designed. |
| DEC-029 | Accepted | The reference screenshot discussed by the project owner is inspiration only. PoEnhance will not copy another tool's interface, and final overlay and menu layout, artwork, and assets remain open decisions. | Inspiration should not become an unreviewed UI requirement or asset commitment. | Do not add the screenshot to the repository or treat its layout, artwork, or assets as confirmed requirements. | Revisit when final UI design requirements are created. |
| DEC-030 | Provisional | Shared game data is placed in the independent `PoEnhance.GameData` library. Data packages use a versioned manifest, and the project boundary and manifest schema will be validated while Milestone 2 develops. | Milestone 2 needs reusable game-data infrastructure without coupling it to the WPF app, parser UI, or external providers. | Game-data package metadata can evolve independently and be tested before real Path of Exile data importers are added. | Revisit when item-base, modifier, catalog, or package activation schemas are designed. |
| DEC-031 | Provisional | Shared game-data packages use provider-neutral internal records for item bases and concrete modifier tiers. Records retain source references, and RePoE or PoEDB formats will be converted into the internal schema rather than becoming application-domain models directly. | Milestone 2 needs stable boundaries before real provider importers are evaluated. | The schema can be tested with fixtures first, while provider-specific formats remain outside the application domain. | Revisit after the schema is tested against real RePoE and PoEDB data. |
| DEC-032 | Provisional | Provider-specific imports live outside `PoEnhance.GameData` in `PoEnhance.DataImport`. RePoE is converted into the provider-neutral PoEnhance schema, runtime network updates are not part of this stage, and source schema changes must fail visibly rather than silently corrupt data. | Shared game-data models should stay provider-neutral while import adapters handle unstable source formats. | Data import can advance without coupling the runtime app to RePoE, HTTP downloads, or provider-specific DTOs. | Revisit when runtime update workflows, additional providers, or real package activation are designed. |

## Open Decisions

- Final UI and overlay behavior.
- Final main multitool menu layout and interaction behavior.
- Final cache durations.
- External fallback providers.
- Asset and icon sources.
- History retention.
- Statistical price estimation.
- Future automatic important-mod selection.
- Secure OAuth token storage.
- Final module and project structure after the technical prototype.

## How to Add a Decision

```markdown
## DEC-000 - Short title

- ID:
- Date:
- Status:
- Context:
- Decision:
- Alternatives considered:
- Consequences:
- Revisit trigger:
```

---

# PoEnhance — Dziennik decyzji (wersja polska)

Wersja angielska jest kanonicznym źródłem tego dokumentu. Ta sekcja po polsku jest tłumaczeniem pomocniczym.

Ten lekki dziennik zapisuje decyzje inżynieryjne kształtujące PoEnhance. Powinien być aktualizowany, gdy zmieniają się ważne wymagania, architektura, zgodność lub wybory produktowe.

## Statusy Decyzji

- Accepted: uzgodniona i obecnie aktywna.
- Provisional: wybrana na teraz, ale wymagająca walidacji.
- Deferred: świadomie odłożona.
- Rejected: rozważona i niewybrana.
- Superseded: zastąpiona nowszą decyzją.

## Decyzje

| ID | Status | Decyzja | Powód | Konsekwencje | Wyzwalacz ponownego rozważenia |
| --- | --- | --- | --- | --- | --- |
| DEC-001 | Accepted | Path of Exile 1 jest początkowo wspieraną grą. Path of Exile 2 jest odłożone. | Zakres projektu zaczyna się od Path of Exile 1. | Wczesny projekt może skupić się na jednej grze. | Rozważyć ponownie, gdy będzie brane pod uwagę wsparcie Path of Exile 2. |
| DEC-002 | Accepted | Windows 11 jest główną platformą. Windows 10 jest wspierany tam, gdzie jest to praktyczne. Inne systemy operacyjne nie są początkowymi celami. | PoEnhance jest aplikacją desktopową dla Windows. | Prace platformowe mogą skupić się na API Windows i WPF. | Rozważyć ponownie, jeśli publiczna dystrybucja lub potrzeby użytkowników rozszerzą wymagania platformowe. |
| DEC-003 | Accepted | Początkowymi użytkownikami są właściciel projektu i mała grupa znajomych. Architektura nie powinna blokować możliwej publicznej dystrybucji później. | Wczesna grupa użytkowników jest ograniczona, ale rozszerzalność ma znaczenie. | UX i proces wydawania mogą pozostać lekkie, a architektura utrzymywalna. | Rozważyć ponownie przed planowaniem publicznego wydania. |
| DEC-004 | Accepted | PoEnhance jest local-first. Cloud synchronization jest odłożone. | Local-first jest główną zasadą. | Ustawienia, cache, historia i dane prowizoryczne powinny być domyślnie lokalne. | Rozważyć ponownie, gdy cloud synchronization wróci do planów. |
| DEC-005 | Accepted | Aplikacja może działać poza grą. Skróty overlay działają tylko wtedy, gdy Path of Exile jest aktywnym oknem foreground. | Część workflows jest użyteczna poza gameplay, a zachowanie w grze musi być ograniczone. | Przeglądanie danych i ręczny input mogą działać poza grą; skróty są ograniczone wykrywaniem aktywnego okna. | Rozważyć ponownie, jeśli zmieni się zachowanie skrótów. |
| DEC-006 | Accepted | Input przedmiotów pochodzi z danych clipboard i ręcznie wklejonego tekstu. Czytanie pamięci gry jest zabronione. | Zgodność i bezpieczeństwo wymagają unikania dostępu do pamięci gry. | Parser musi używać clipboard lub wklejonego tekstu. | Rozważyć ponownie tylko jeśli zmieni się oficjalne guidance i review zgodności to zatwierdzi. |
| DEC-007 | Accepted | Angielski klient Path of Exile jest wspierany jako pierwszy. Architektura powinna pozwalać na dodatkowe języki później. | Początkowy zakres to angielski, ale przyszłe języki nie powinny wymagać redesignu. | Parsowanie i katalogi powinny unikać założeń blokujących lokalizację. | Rozważyć ponownie przy dodawaniu kolejnego języka klienta gry lub UI. |
| DEC-008 | Accepted | Aplikacja używa modularnej architektury. Wspólna logika biznesowa nie może zależeć bezpośrednio od WPF. | Moduły i wspólna logika muszą pozostać wielokrotnego użytku i testowalne. | UI, parsowanie, storage, integracje i współdzielone dane powinny pozostać rozdzielone. | Rozważyć ponownie podczas review architektury po prototypie technicznym. |
| DEC-009 | Provisional | C#, .NET 10 LTS i WPF są początkowym stackiem technicznym. Wybór musi zostać zwalidowany przez prototyp techniczny wersji 0.1. | Stack pasuje do kierunku Windows desktop. | Prototyp powinien potwierdzić build, UI, overlay i lifecycle. | Rozważyć ponownie po walidacji prototypu technicznego wersji 0.1. |
| DEC-010 | Accepted | Współdzielone katalogi game-data należą do wspólnej warstwy danych. Price Checker jest tylko jednym konsumentem tych katalogów. | Wiele przyszłych modułów potrzebuje tych samych danych gry. | Katalogi nie powinny być prywatne dla price checkera. | Rozważyć ponownie, jeśli zmienią się granice modułów. |
| DEC-011 | Accepted | Nieznane przedmioty i dane z nowych lig muszą powodować graceful degradation. Rozpoznawanie używa stanów confidence: Exact, Probable, Generic i Unknown. | Dane Path of Exile często się zmieniają. | Nieznane dane nie powinny psuć parsowania ani czynić aplikacji bezużyteczną. | Rozważyć ponownie, gdy zmieni się model rozpoznawania. |
| DEC-012 | Accepted | Lokalne katalogi są podstawowym źródłem rozpoznawania. Dostawcy online są źródłami fallback. Wyniki fallback tworzą prowizoryczne rekordy w cache. | Normalne działanie nie powinno zależeć od dostawców fallback online. | Dane fallback muszą być identyfikowalne i nie powinny po cichu zastępować zaufanych katalogów. | Rozważyć ponownie przy wyborze dostawców fallback. |
| DEC-013 | Accepted | Wyszukiwania individual-item domyślnie używają Merchant Only. In-person trade pozostaje możliwy do wyboru. | To obecnie planowana domyślna opcja dla wyszukiwań individual-item. | UI musi pozwalać na zmianę trybu handlu. | Rozważyć ponownie po testach trade search. |
| DEC-014 | Accepted | Wyszukiwania rare-item są domyślnie ręczne. Search następuje tylko po jawnej akcji użytkownika. Automatyczne wyszukiwanie może później stać się opcjonalnym ustawieniem. | Wymagane są kontrola użytkownika i bezpieczeństwo rate-limit. | Otwarcie price checkera lub zmiana filtrów nie może automatycznie wyszukiwać. | Rozważyć ponownie, jeśli zostanie zaprojektowane opcjonalne automatyczne wyszukiwanie. |
| DEC-015 | Accepted | Żądania muszą być walidowane lokalnie przed kontaktem z GGG. | Znane nieprawidłowe żądania powinny zużywać zero żądań API. | Query builders potrzebują lokalnej walidacji przed wywołaniami sieciowymi. | Rozważyć ponownie, gdy zmienią się modele żądań. |
| DEC-016 | Accepted | Rate limits GGG są dynamiczne i muszą być respektowane. Search, Refresh i Load More powinny komunikować swój wpływ na żądania. | Rate limits nie mogą być obchodzone ani trwale hardcoded. | UI i logika integracji muszą pokazywać stan żądań tam, gdzie jest to istotne. | Rozważyć ponownie, gdy zmieni się zachowanie rate-limit GGG lub API. |
| DEC-017 | Accepted | Początkowe wyniki Trade ładują 10 szczegółowych ofert. Dodatkowe oferty ładują się w jawnych partiach po 10. | To ogranicza koszt żądań i utrzymuje ładowanie jako jawne. | Load More musi być wywołane przez użytkownika i używać identyfikatorów wyników tam, gdzie to możliwe. | Rozważyć ponownie po testach użyteczności trade search. |
| DEC-018 | Accepted | Załadowane oferty mogą być podglądane bez kolejnego żądania sieciowego. Kliknięcie oferty może otworzyć trwałą kartę przedmiotu w stylu Path of Exile. | Już pobrane dane listingu powinny być użyte ponownie. | Karty przedmiotów nie powinny wywoływać dodatkowych żądań dla już załadowanych ofert. | Rozważyć ponownie, gdy UX kart przedmiotów zostanie zwalidowany. |
| DEC-019 | Accepted | Nieudokumentowane endpointy nie będą używane do bezpośredniego Travel to Hideout. Pomysł można rozważyć ponownie tylko, jeśli istnieje oficjalnie wspierana metoda. | Ukryte lub nieudokumentowane integracje są poza zakresem. | Bezpośrednia podróż nie jest implementowana bez oficjalnego wsparcia. | Rozważyć ponownie, jeśli oficjalna dokumentacja wprowadzi wspieraną metodę. |
| DEC-020 | Provisional | Niedawne identyczne zapytania Trade mogą używać około dwuminutowego cache. Currency Exchange może używać około jednominutowego cache. Finalne wartości wymagają testów. | Początkowe założenia cache ograniczają powtarzane żądania. | Świeżość cache musi być widoczna, a wartości mogą się zmienić. | Rozważyć ponownie po testach rate-limit i użytkownika. |
| DEC-021 | Deferred | Historia wyszukiwań, zapisane przedmioty, zapisane wyceny, przypięte porównania i import/export zapisanych wyszukiwań wymagają późniejszej walidacji UX. | Te funkcje są kandydatami, a nie zakresem podstawowego prototypu. | Nie powinny blokować wersji 0.1. | Rozważyć ponownie podczas planowania funkcji kandydujących. |
| DEC-022 | Deferred | Bulk Search i Bulk Buy nie są częścią wersji 0.1. | Workflows bulk są odłożone. | Wersja 0.1 pozostaje skupiona na fundamentach prototypu. | Rozważyć ponownie po dojrzewaniu podstawowego trade search. |
| DEC-023 | Accepted | Wersja 0.1 jest prototypem technicznym. Nie wymaga Trade API, OAuth, stash value, pełnego modułu ekonomii, kompletnej bazy game-data ani dopracowanego finalnego UI. | Pierwsza wersja powinna potwierdzić fundamenty. | Zakres jest ograniczony do shell, detection, shortcut, clipboard, parser, overlay, logging i początkowych testów. | Rozważyć ponownie po ukończeniu wersji 0.1. |
| DEC-024 | Accepted | Oficjalny OAuth GGG w przeglądarce będzie używany, gdy zostanie zaimplementowany dostęp do konta. Wygasłe uwierzytelnienie nie może wyłączać niezależnych funkcji aplikacji. | Dostęp do konta musi unikać obsługi haseł i izolować awarie. | Moduły zależne od OAuth potrzebują obsługi ponownego połączenia. | Rozważyć ponownie przy projektowaniu scopes OAuth i token storage. |
| DEC-025 | Accepted | Ukryte lub nieudokumentowane endpointy GGG, automatyzacja gameplay, botting, czytanie pamięci, packet interception i obchodzenie limitów API są poza zakresem projektu. | Zgodność i bezpieczeństwo są głównymi zasadami projektu. | Te zachowania nie mogą zostać zaimplementowane. | Rozważyć ponownie tylko jeśli zmienią się oficjalne zasady i review zgodności to zatwierdzi. |
| DEC-026 | Accepted | PoEnhance ma działać przede wszystkim w tle. Dedykowany konfigurowalny skrót otwiera główne menu multitool, z klawiszem Backslash (`\`) jako obecnym ustawieniem domyślnym. | Aplikacja powinna być nienachalna, a jednocześnie zapewniać szybki dostęp do głównych narzędzi. | Główne menu multitool staje się punktem wejścia do modułów takich jak narzędzia regex, widoki ekonomii, przeglądanie game-data i inne przyszłe moduły. | Rozważyć ponownie, jeśli zmieni się model uruchamiania aplikacji lub głównego menu. |
| DEC-027 | Accepted | Price Checker ma osobny dedykowany konfigurowalny skrót, z `Ctrl + D` jako potwierdzonym ustawieniem domyślnym. Prototypowy skrót `X` jest tymczasowym technicznym rusztowaniem i nie jest finalnym ustawieniem domyślnym. | Price checking jest na tyle częste, że zasługuje na bezpośrednie powiązanie, a obecna implementacja nadal używa tymczasowego skrótu prototypowego. | Dokumentacja i przyszła implementacja nie powinny opisywać `X` jako finalnego; aplikacja musi wspierać osobne powiązania dla menu i Price Checkera. | Rozważyć ponownie, jeśli domyślne skróty zmienią się po testach. |
| DEC-028 | Accepted | Powiązania skrótów będą później edytowalne w Settings aplikacji przez kontrolki widoczne dla użytkownika. | Użytkownicy potrzebują kontroli nad skrótami bez ręcznej edycji plików. | Prace nad Settings muszą obejmować edycję skrótów, ale persystencja i UI Settings nie muszą być ukończone w obecnym kroku prototypu. | Rozważyć ponownie przy projektowaniu persystencji i UI Settings. |
| DEC-029 | Accepted | Screenshot referencyjny omówiony przez właściciela projektu jest wyłącznie inspiracją. PoEnhance nie będzie kopiować interfejsu innego narzędzia, a finalny layout overlay i menu, artwork oraz assety pozostają otwartymi decyzjami. | Inspiracja nie powinna stać się niezweryfikowanym wymaganiem UI ani zobowiązaniem dotyczącym assetów. | Nie dodawać screenshota do repozytorium ani nie traktować jego layoutu, artworku lub assetów jako potwierdzonych wymagań. | Rozważyć ponownie przy tworzeniu finalnych wymagań projektu UI. |
| DEC-030 | Provisional | Współdzielone dane gry są umieszczone w niezależnej bibliotece `PoEnhance.GameData`. Pakiety danych używają wersjonowanego manifestu, a granica projektu i schemat manifestu będą walidowane podczas rozwoju Milestone 2. | Milestone 2 potrzebuje infrastruktury danych wielokrotnego użytku bez powiązania z aplikacją WPF, UI parsera lub zewnętrznymi dostawcami. | Metadane pakietów game-data mogą ewoluować niezależnie i być testowane przed dodaniem importerów realnych danych Path of Exile. | Rozważyć ponownie przy projektowaniu schematów item-base, modifier, catalog lub package activation. |
| DEC-031 | Provisional | Pakiety współdzielonych danych gry używają provider-neutral rekordów wewnętrznych dla baz przedmiotów i konkretnych tierów modifierów. Rekordy zachowują referencje źródłowe, a formaty RePoE lub PoEDB będą konwertowane do schematu wewnętrznego zamiast stawać się bezpośrednio modelami domenowymi aplikacji. | Milestone 2 potrzebuje stabilnych granic przed oceną prawdziwych importerów providerów. | Schemat może być najpierw testowany fixture'ami, a formaty specyficzne dla providerów pozostają poza domeną aplikacji. | Rozważyć ponownie po przetestowaniu schematu z realnymi danymi RePoE i PoEDB. |
| DEC-032 | Provisional | Importy specyficzne dla providerów są poza `PoEnhance.GameData`, w `PoEnhance.DataImport`. RePoE jest konwertowane do provider-neutral schematu PoEnhance, runtime network updates nie są częścią tego etapu, a zmiany schematu źródłowego muszą kończyć się widocznym błędem zamiast po cichu psuć dane. | Współdzielone modele game-data powinny pozostać provider-neutral, a adaptery importu obsługują niestabilne formaty źródłowe. | Import danych może rozwijać się bez wiązania runtime aplikacji z RePoE, pobieraniem HTTP lub DTO specyficznymi dla providera. | Rozważyć ponownie przy projektowaniu workflow aktualizacji runtime, kolejnych providerów lub realnej aktywacji pakietów. |

## Otwarte Decyzje

- Finalne zachowanie UI i overlay.
- Finalny layout i zachowanie interakcji głównego menu multitool.
- Finalne czasy cache.
- Zewnętrzni dostawcy fallback.
- Źródła assetów i ikon.
- Retencja historii.
- Statystyczna estymacja ceny.
- Przyszły automatyczny wybór ważnych modów.
- Bezpieczne przechowywanie tokenów OAuth.
- Finalna struktura modułów i projektu po prototypie technicznym.

## Jak Dodać Decyzję

```markdown
## DEC-000 - Krótki tytuł

- ID:
- Date:
- Status:
- Context:
- Decision:
- Alternatives considered:
- Consequences:
- Revisit trigger:
```
