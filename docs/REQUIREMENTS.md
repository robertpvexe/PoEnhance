# PoEnhance Requirements

## 1. Document Purpose

This document captures the initial engineering requirements for PoEnhance, a Windows desktop companion application for Path of Exile 1.

Requirements describe intended behavior, constraints, and product boundaries. They should not be read as final implementation design, final UX decisions, or a complete feature specification for every future module.

## 2. Status Definitions

- Confirmed - agreed requirement.
- Candidate - planned idea requiring validation.
- Deferred - intentionally postponed.
- Out of scope - not planned for the current stage.

## 3. Product and Platform Requirements

| ID | Status | Requirement |
| --- | --- | --- |
| PR-001 | Confirmed | PoEnhance initially supports Path of Exile 1. |
| PR-002 | Confirmed | The primary platform is Windows 11, with Windows 10 support where practical. |
| PR-003 | Confirmed | Initial users are the project owner and a small group of friends. |
| PR-004 | Confirmed | The architecture should allow possible public distribution later. |
| PR-005 | Confirmed | The English Path of Exile client is supported initially. |
| PR-006 | Confirmed | The architecture must allow additional game-client and UI languages later. |
| PR-007 | Confirmed | The application is local-first. |
| PR-008 | Confirmed | The application must remain modular and extensible. |

### 3.1 Non-Functional Requirements

| ID | Status | Requirement |
| --- | --- | --- |
| NFR-001 | Confirmed | The architecture must be modular. |
| NFR-002 | Confirmed | UI, operating-system integration, GGG integration, parsing, shared data, storage, and feature modules must be clearly separated. |
| NFR-003 | Confirmed | Shared business logic must not depend directly on a specific UI implementation. |
| NFR-004 | Confirmed | The application should be testable without requiring Path of Exile to be running for all components. |
| NFR-005 | Confirmed | Unknown data, new leagues, and incomplete catalogs must degrade gracefully. |
| NFR-006 | Confirmed | The application should remain responsive while network operations or data updates are running. |
| NFR-007 | Confirmed | Network requests should be observable through useful local logs. |
| NFR-008 | Confirmed | User-facing errors should be understandable without exposing unnecessary internal implementation details. |

### 3.2 Visual Requirements

| ID | Status | Requirement |
| --- | --- | --- |
| VIS-001 | Confirmed | Item results should support item icons. |
| VIS-002 | Confirmed | Price results should support currency icons. |
| VIS-003 | Confirmed | Relevant rarity, socket, influence, fractured, corrupted, and similar visual information should be representable. |
| VIS-004 | Candidate | Asset sources, licenses, caching, and visual styling will be decided later. |
| VIS-005 | Confirmed | The data model must include stable fields for asset identifiers or image references. |

## 4. Application Lifecycle

| ID | Status | Requirement |
| --- | --- | --- |
| LC-001 | Confirmed | PoEnhance may run while Path of Exile is not running. |
| LC-002 | Confirmed | Features such as data browsing, saved data, economy views, and manual item input may work outside the game. |
| LC-003 | Confirmed | In-game shortcuts and overlay behavior must activate only when Path of Exile is the active foreground window. |
| LC-004 | Confirmed | The application must detect whether the Path of Exile process is running. |
| LC-005 | Confirmed | The application must detect whether the Path of Exile window is currently active. |
| LC-006 | Confirmed | A failure in one module must not prevent unrelated modules from working. |

## 5. Path of Exile Integration

| ID | Status | Requirement |
| --- | --- | --- |
| POE-001 | Confirmed | Available leagues should be loaded dynamically from an official GGG source. |
| POE-002 | Confirmed | The selected league is configurable and stored locally. |
| POE-003 | Confirmed | League names must not be permanently hardcoded into the application. |
| POE-004 | Confirmed | The application obtains in-game item text through the clipboard rather than reading game memory. |
| POE-005 | Confirmed | The user places the cursor over an item before using the price-check shortcut. |
| POE-006 | Confirmed | The complete raw clipboard text must be preserved for diagnostics and fallback processing. |

## 6. Authentication and Account Access

| ID | Status | Requirement |
| --- | --- | --- |
| AUTH-001 | Confirmed | The application should use official GGG OAuth where account access is required. |
| AUTH-002 | Confirmed | The user authenticates through the official Path of Exile website in the browser. |
| AUTH-003 | Confirmed | PoEnhance must never request or store the user's GGG password. |
| AUTH-004 | Confirmed | Tokens should be refreshed automatically where possible. |
| AUTH-005 | Confirmed | When reauthentication is required, the application should display a clear prompt with an action opening the official login flow. |
| AUTH-006 | Confirmed | Features that do not require account access must continue working when authentication expires. |
| AUTH-007 | Confirmed | Account-dependent features such as stash value should clearly show that reconnection is required. |

## 7. Price Checker Requirements

### 7.1 Input

| ID | Status | Requirement |
| --- | --- | --- |
| PC-IN-001 | Confirmed | In-game item checking is initiated by a configurable keyboard shortcut while Path of Exile is active. |
| PC-IN-002 | Confirmed | The parser should support normal and advanced item descriptions. |
| PC-IN-003 | Confirmed | Advanced item descriptions should be preferred when available. |
| PC-IN-004 | Confirmed | The price checker must support manually pasted item text outside the game. |

### 7.2 Parsing

| ID | Status | Requirement |
| --- | --- | --- |
| PC-PARSE-001 | Confirmed | The parser should extract structured information independently where possible. |
| PC-PARSE-002 | Confirmed | Parsed fields should include item class, rarity, name, base type, item level, properties, requirements, sockets and links, implicits, explicits, crafted mods, fractured mods, enchantments, corruption, and other item flags where present. |
| PC-PARSE-003 | Confirmed | Missing or unknown data must not crash or invalidate the entire parsed item. |
| PC-PARSE-004 | Confirmed | Item recognition must support confidence states: Exact, Probable, Generic, and Unknown. |
| PC-PARSE-005 | Confirmed | Items with non-exact recognition may still be shown and searched, but the confidence state must be visible to the user. |

### 7.3 Recognition and Fallback

| ID | Status | Requirement |
| --- | --- | --- |
| PC-REC-001 | Confirmed | Local item and mod catalogs are the primary recognition source. |
| PC-REC-002 | Confirmed | Recognition must not depend only on an exact item-name match. |
| PC-REC-003 | Confirmed | Matching may use multiple signals, including name, base type, item class or group, rarity, properties, mod signatures, trade stat identifiers, and other stable identifiers where available. |
| PC-REC-004 | Confirmed | When an item is missing from the local catalog, the application may query configured online sources. |
| PC-REC-005 | Confirmed | Online sources are fallback providers and must not be required for normal operation. |
| PC-REC-006 | Confirmed | Data returned by fallback providers should be compared across multiple signals. |
| PC-REC-007 | Confirmed | Fallback results should create a provisional cached record rather than silently modifying the trusted main catalog. |
| PC-REC-008 | Confirmed | Provisional records should store their source, timestamp, league or patch context, and confidence level. |
| PC-REC-009 | Confirmed | New-league items must degrade gracefully instead of making the price checker unusable. |

### 7.4 Trade Channel Classification

| ID | Status | Requirement |
| --- | --- | --- |
| PC-TRADE-001 | Confirmed | The price checker must distinguish stackable items handled through Currency Exchange from individual items handled through item Trade or Manage Shop. |
| PC-TRADE-002 | Confirmed | Currency Exchange and individual-item trade paths may use different queries, result models, and user interfaces. |
| PC-TRADE-003 | Confirmed | Merchant Only or instant-buy offers should be the default for individual-item searches. |
| PC-TRADE-004 | Confirmed | The user must be able to switch to in-person trade. |
| PC-TRADE-005 | Confirmed | Exact default filters may be adjusted after testing. |

### 7.5 Rare Item Search

| ID | Status | Requirement |
| --- | --- | --- |
| PC-RARE-001 | Confirmed | The application should display recognized mods before sending a trade search. |
| PC-RARE-002 | Confirmed | By default, the user manually chooses which mods and ranges are included. |
| PC-RARE-003 | Confirmed | Opening the price checker or changing filters must not automatically send a search request. |
| PC-RARE-004 | Confirmed | A request is sent only after an explicit Search action. |
| PC-RARE-005 | Candidate | Automatic searching may be added later as an optional user setting. |
| PC-RARE-006 | Confirmed | Future automatic selection of important mods must be possible without redesigning the parser or item model. |
| PC-RARE-007 | Confirmed | Each parsed mod should support metadata needed by future selection strategies. |

### 7.6 Request Validation and Rate Limits

| ID | Status | Requirement |
| --- | --- | --- |
| PC-REQ-001 | Confirmed | Search parameters must be validated locally before any request is sent. |
| PC-REQ-002 | Confirmed | Detectable errors such as minimum value greater than maximum value must consume zero API requests. |
| PC-REQ-003 | Confirmed | Invalid numeric values, missing required fields, and locally detectable contradictory filters should be rejected before calling GGG. |
| PC-REQ-004 | Confirmed | The application must never knowingly waste a request on a query it already knows is invalid. |
| PC-REQ-005 | Confirmed | The application must read and respect GGG rate-limit information. |
| PC-REQ-006 | Confirmed | Rate limits must not be represented using permanently hardcoded values. |
| PC-REQ-007 | Confirmed | The UI should show the user the current request state where relevant. |
| PC-REQ-008 | Confirmed | Before actions such as Search, Refresh, or Load More, the user should be able to see how the action affects request limits. |
| PC-REQ-009 | Confirmed | When required, requests should be queued rather than sent in violation of current limits. |
| PC-REQ-010 | Confirmed | The application must not attempt to bypass or evade GGG rate limits. |

### 7.7 Search Results

| ID | Status | Requirement |
| --- | --- | --- |
| PC-RES-001 | Confirmed | Search results should initially load the first 10 detailed offers. |
| PC-RES-002 | Confirmed | Additional results should be loaded in batches of 10 through an explicit Load More action. |
| PC-RES-003 | Confirmed | Loading additional result batches must not repeat the original search when existing result identifiers can be reused. |
| PC-RES-004 | Confirmed | The UI should indicate whether Load More requires another fetch request. |
| PC-RES-005 | Confirmed | Results should be sorted by price ascending by default. |
| PC-RES-006 | Confirmed | The application should display the offers returned by GGG without hiding listings based on unsupported assumptions. |
| PC-RES-007 | Confirmed | The application should not automatically interpret large price differences for the user. |
| PC-RES-008 | Confirmed | One, two, or widely dispersed results should still be shown as returned. |

### 7.8 Result Errors

| ID | Status | Requirement |
| --- | --- | --- |
| PC-ERR-001 | Confirmed | The application must distinguish valid queries with no matching offers from invalid local search parameters, GGG or network failure, and rate-limit delay. |
| PC-ERR-002 | Confirmed | The application must not automatically loosen filters or send alternative searches. |
| PC-ERR-003 | Confirmed | The user remains responsible for interpreting market results. |

### 7.9 Offer Preview and Item Cards

| ID | Status | Requirement |
| --- | --- | --- |
| PC-CARD-001 | Confirmed | Hovering over a loaded result may show a temporary item preview. |
| PC-CARD-002 | Confirmed | Clicking a loaded offer should open a persistent item card showing the exact item data returned for that listing. |
| PC-CARD-003 | Confirmed | The item card should visually resemble a Path of Exile item tooltip where practical. |
| PC-CARD-004 | Confirmed | The item card should display relevant properties, sockets, implicits, explicits, crafted mods, fractured mods, enchantments, corruption, influence, and other returned data. |
| PC-CARD-005 | Confirmed | Viewing a card for an already fetched listing must not trigger another network request. |
| PC-CARD-006 | Confirmed | Local mod catalogs may enrich the card with tiers or tags. |
| PC-CARD-007 | Confirmed | If enrichment is uncertain, raw returned mod text should be shown without guessing. |

### 7.10 Trade Actions

| ID | Status | Requirement |
| --- | --- | --- |
| PC-ACT-001 | Confirmed | The application may open the official trade page for a search or offer. |
| PC-ACT-002 | Confirmed | The application must not rely on undocumented or hidden endpoints to directly teleport the user to a seller's hideout. |
| PC-ACT-003 | Candidate | Direct travel integration may only be considered if an officially supported method is identified. |
| PC-ACT-004 | Candidate | Any future whisper support must not automatically send messages or perform multiple in-game actions without explicit user input. |

### 7.11 Price Display

| ID | Status | Requirement |
| --- | --- | --- |
| PC-PRICE-001 | Confirmed | Show exact listing prices returned by GGG. |
| PC-PRICE-002 | Candidate | Optionally show a converted value in another selected in-game currency. |
| PC-PRICE-003 | Candidate | Converted prices must show the source and freshness of the exchange rate. |
| PC-PRICE-004 | Candidate | Automated estimated price ranges based on statistical interpretation are not yet confirmed. |

### 7.12 History and Saved Items

| ID | Status | Requirement |
| --- | --- | --- |
| PC-HIST-001 | Candidate | Maintain a small local history of recent searches and their result snapshots. |
| PC-HIST-002 | Candidate | Reopening a historical snapshot must not send a new request. |
| PC-HIST-003 | Candidate | Allow the user to manually refresh an old search. |
| PC-HIST-004 | Candidate | Allow the user to save selected items or searches locally. |
| PC-HIST-005 | Candidate | A saved item may show its most recent checked price and how long ago it was checked. |
| PC-HIST-006 | Candidate | A saved item may expose previous valuations only after explicit user action. |
| PC-HIST-007 | Candidate | Pinning and comparing item cards requires UI validation. |
| PC-HIST-008 | Candidate | Export and import of saved searches is a low-priority candidate. |

## 8. Shared Game-Data Core

| ID | Status | Requirement |
| --- | --- | --- |
| DATA-001 | Confirmed | Game-data catalogs must belong to a shared data layer, not privately to the price checker. |
| DATA-002 | Confirmed | Multiple modules should reuse the same item, mod, base-type, category, and property catalogs. |
| DATA-003 | Confirmed | Shared catalogs may include regular modifiers, implicit modifiers, synthesis modifiers, corruption implicits, enchantments, influence modifiers, fractured modifiers, item bases, item classes, mod groups, tiers, ranges, tags, and eligibility rules describing which item types can receive a modifier. |
| DATA-004 | Confirmed | The same synthesis data used by the price checker should later support questions such as which synthesis implicits may occur on rings. |
| DATA-005 | Confirmed | The same corruption data should later support searches such as which corruption implicits may occur on a given item type. |
| DATA-006 | Confirmed | Data packages should be updateable independently of the main application where practical. |
| DATA-007 | Confirmed | The architecture must permit unusual item classes and special item cases to be added incrementally without requiring version 0.1 to implement them all. |

## 9. Local Storage and Cache

| ID | Status | Requirement |
| --- | --- | --- |
| STORE-001 | Confirmed | Store application settings locally. |
| STORE-002 | Confirmed | Store selected league and user preferences locally. |
| STORE-003 | Confirmed | Store search cache locally. |
| STORE-004 | Confirmed | Store provisional item records locally. |
| STORE-005 | Confirmed | Store history and saved items locally if those candidate features are implemented. |
| STORE-006 | Confirmed | Authentication secrets must not be included in ordinary export files. |
| STORE-007 | Confirmed | Cloud synchronization is not required initially. |
| CACHE-001 | Confirmed | Identical recent searches should be served from local cache where appropriate. |
| CACHE-002 | Confirmed | Cached results must clearly show their age. |
| CACHE-003 | Confirmed | The user may explicitly request refreshed data. |
| CACHE-004 | Confirmed | Refresh must show that it uses another request before the user activates it. |
| CACHE-005 | Candidate | Cache duration should be configurable or revisited after testing. |
| CACHE-006 | Candidate | An initial working assumption is approximately two minutes for individual-item Trade results and approximately one minute for Currency Exchange results. |

## 10. External Data Sources

| ID | Status | Requirement |
| --- | --- | --- |
| EXT-001 | Confirmed | Prefer official GGG APIs and OAuth where available. |
| EXT-002 | Confirmed | Available leagues should come from an official GGG source. |
| EXT-003 | Confirmed | Online fallback sources may be used when local catalogs cannot recognize an item. |
| EXT-004 | Confirmed | Unsupported scraping or third-party sources must be reviewed for permission, reliability, and terms before implementation. |
| EXT-005 | Confirmed | External provider failure must not break unrelated modules. |
| EXT-006 | Confirmed | Network requests should be observable through useful local logs. |

## 11. Error Handling and Graceful Degradation

| ID | Status | Requirement |
| --- | --- | --- |
| ERR-001 | Confirmed | Unknown data must cause graceful degradation, not application failure. |
| ERR-002 | Confirmed | Missing catalog entries must not make the price checker unusable. |
| ERR-003 | Confirmed | External provider failure must not break unrelated modules. |
| ERR-004 | Confirmed | User-facing errors should be understandable without exposing unnecessary internal implementation details. |
| ERR-005 | Confirmed | The application should remain responsive while network operations or data updates are running. |

## 12. Security and GGG Compliance

| ID | Status | Requirement |
| --- | --- | --- |
| SEC-001 | Confirmed | No game-memory reading. |
| SEC-002 | Confirmed | No DLL injection. |
| SEC-003 | Confirmed | No packet interception. |
| SEC-004 | Confirmed | No botting or automated gameplay. |
| SEC-005 | Confirmed | No attempt to bypass API limits. |
| SEC-006 | Confirmed | No automatic multi-action gameplay macros. |
| SEC-007 | Confirmed | User actions affecting the game must remain explicit and compliant with GGG rules. |
| SEC-008 | Confirmed | GGG compliance must be reviewed for every major feature. |
| SEC-009 | Out of scope | Real-money trading workflows are not supported. |
| SEC-010 | Out of scope | Features designed to evade game or API restrictions are not supported. |

## 13. Version 0.1 Scope

Version 0.1 is a technical prototype, not the full application.

### 13.1 Required for Version 0.1

| ID | Status | Requirement |
| --- | --- | --- |
| V01-001 | Confirmed | Windows WPF application shell. |
| V01-002 | Confirmed | Background application behavior. |
| V01-003 | Confirmed | Path of Exile process detection. |
| V01-004 | Confirmed | Active Path of Exile window detection. |
| V01-005 | Confirmed | Configurable shortcut foundation. |
| V01-006 | Confirmed | Clipboard item capture. |
| V01-007 | Confirmed | Manual pasted-item input. |
| V01-008 | Confirmed | Basic item parser. |
| V01-009 | Confirmed | Display parsed item name, rarity, base type, and recognized mods. |
| V01-010 | Confirmed | Basic overlay window. |
| V01-011 | Confirmed | Clean separation between UI and parsing logic. |
| V01-012 | Confirmed | Local logging sufficient for development. |
| V01-013 | Confirmed | Initial automated tests for parser behavior where practical. |

### 13.2 Not Required for Version 0.1

| ID | Status | Requirement |
| --- | --- | --- |
| V01-NR-001 | Deferred | Complete Trade API integration. |
| V01-NR-002 | Deferred | Complete OAuth integration. |
| V01-NR-003 | Deferred | Stash value. |
| V01-NR-004 | Deferred | Full economy module. |
| V01-NR-005 | Deferred | Complete local game database. |
| V01-NR-006 | Deferred | Support for every unusual item type. |
| V01-NR-007 | Deferred | Polished final UI. |

## 14. Candidate Features

| ID | Status | Requirement |
| --- | --- | --- |
| CAND-002 | Candidate | Converted prices in another selected in-game currency with source and freshness shown. |
| CAND-003 | Candidate | Automated estimated price ranges based on statistical interpretation. |
| CAND-004 | Candidate | Small local search history with result snapshots. |
| CAND-005 | Candidate | Saved items or saved searches. |
| CAND-006 | Candidate | Previous valuations exposed only after explicit user action. |
| CAND-007 | Candidate | Pinning and comparing item cards. |
| CAND-008 | Candidate | Export and import of saved searches. |
| CAND-009 | Candidate | Optional automatic searching. |
| CAND-010 | Candidate | Future automatic important-mod selection. |
| CAND-011 | Candidate | Future whisper support that remains explicit and compliant. |

## 15. Deferred Features

| ID | Status | Requirement |
| --- | --- | --- |
| DEF-001 | Deferred | Bulk Search or Bulk Buy for multiple copies of the same non-stackable item. |
| DEF-002 | Deferred | Optimization for lowest total cost or fewest seller hideouts. |
| DEF-003 | Deferred | Full saved-item comparison workspace. |
| DEF-004 | Deferred | Public release infrastructure. |
| DEF-005 | Deferred | Cloud synchronization. |
| DEF-006 | Deferred | Path of Exile 2 support. |

### 15.1 Special Item TODOs

These are deferred and should be split into small tasks. The architecture must permit these additions, but version 0.1 must not implement them all.

| ID | Status | Requirement |
| --- | --- | --- |
| ITEM-001 | Deferred | Unidentified items. |
| ITEM-002 | Deferred | Corrupted items. |
| ITEM-003 | Deferred | Mirrored or duplicated items. |
| ITEM-004 | Deferred | Fractured items. |
| ITEM-005 | Deferred | Synthesised items. |
| ITEM-006 | Deferred | Influenced items. |
| ITEM-007 | Deferred | Enchantments. |
| ITEM-008 | Deferred | Special implicits. |
| ITEM-009 | Deferred | Gems. |
| ITEM-010 | Deferred | Maps. |
| ITEM-011 | Deferred | Cluster jewels. |
| ITEM-012 | Deferred | Timeless jewels. |
| ITEM-013 | Deferred | Other unusual item classes. |

## 16. Out of Scope

| ID | Status | Requirement |
| --- | --- | --- |
| OOS-001 | Out of scope | Gameplay automation. |
| OOS-002 | Out of scope | Botting. |
| OOS-003 | Out of scope | Real-money trading workflows. |
| OOS-004 | Out of scope | Reading or modifying Path of Exile memory. |
| OOS-005 | Out of scope | Hidden or undocumented GGG endpoints. |
| OOS-006 | Out of scope | Features designed to evade game or API restrictions. |
| OOS-007 | Out of scope | Creating a replacement Path of Exile wiki. |

## 17. Open Questions

| ID | Status | Question |
| --- | --- | --- |
| OQ-001 | Candidate | What are the final cache durations for Trade and Currency Exchange results? |
| OQ-002 | Candidate | What are the final overlay positioning and closing behaviors? |
| OQ-003 | Candidate | Should the UI framework choice be revisited after the technical prototype? |
| OQ-004 | Candidate | Which external fallback providers are acceptable? |
| OQ-005 | Candidate | What asset and icon sources can be used, and under which licenses? |
| OQ-006 | Candidate | What is the final history retention period? |
| OQ-007 | Candidate | Is price estimation beyond raw listings useful enough to include? |
| OQ-008 | Candidate | What are the final rules for future automatic important-mod selection? |

---

# PoEnhance — Wymagania (wersja polska)

Wersja angielska jest kanonicznym źródłem wymagań. Ta sekcja po polsku jest tłumaczeniem pomocniczym.

## 1. Cel Dokumentu

Ten dokument opisuje początkowe wymagania inżynieryjne dla PoEnhance, aplikacji desktopowej dla Windows wspierającej grę Path of Exile 1.

Wymagania opisują oczekiwane zachowanie, ograniczenia i granice produktu. Nie należy traktować ich jako ostatecznego projektu implementacji, ostatecznych decyzji UX ani pełnej specyfikacji każdej przyszłej funkcji.

## 2. Definicje Statusów

- Potwierdzone - uzgodnione wymaganie.
- Kandydat - planowany pomysł wymagający walidacji.
- Odłożone - świadomie przełożone.
- Poza zakresem - nieplanowane na obecny etap.

## 3. Wymagania Produktowe i Platformowe

| ID | Status | Wymaganie |
| --- | --- | --- |
| PR-001 | Potwierdzone | PoEnhance początkowo wspiera Path of Exile 1. |
| PR-002 | Potwierdzone | Główną platformą jest Windows 11, ze wsparciem Windows 10 tam, gdzie jest to praktyczne. |
| PR-003 | Potwierdzone | Początkowymi użytkownikami są właściciel projektu i mała grupa znajomych. |
| PR-004 | Potwierdzone | Architektura powinna pozwalać na ewentualną publiczną dystrybucję w przyszłości. |
| PR-005 | Potwierdzone | Początkowo wspierany jest angielski klient Path of Exile. |
| PR-006 | Potwierdzone | Architektura musi pozwalać na dodanie w przyszłości kolejnych języków klienta gry i UI. |
| PR-007 | Potwierdzone | Aplikacja działa w modelu local-first. |
| PR-008 | Potwierdzone | Aplikacja musi pozostać modularna i rozszerzalna. |

### 3.1 Wymagania Niefunkcjonalne

| ID | Status | Wymaganie |
| --- | --- | --- |
| NFR-001 | Potwierdzone | Architektura musi być modularna. |
| NFR-002 | Potwierdzone | UI, integracja z systemem operacyjnym, integracja z GGG, parsowanie, dane współdzielone, storage i moduły funkcji muszą być wyraźnie rozdzielone. |
| NFR-003 | Potwierdzone | Wspólna logika biznesowa nie może zależeć bezpośrednio od konkretnej implementacji UI. |
| NFR-004 | Potwierdzone | Aplikację powinno dać się testować bez wymogu uruchomienia Path of Exile dla wszystkich komponentów. |
| NFR-005 | Potwierdzone | Nieznane dane, nowe ligi i niekompletne katalogi muszą powodować graceful degradation. |
| NFR-006 | Potwierdzone | Aplikacja powinna pozostawać responsywna podczas operacji sieciowych lub aktualizacji danych. |
| NFR-007 | Potwierdzone | Żądania sieciowe powinny być obserwowalne przez użyteczne lokalne logi. |
| NFR-008 | Potwierdzone | Błędy widoczne dla użytkownika powinny być zrozumiałe bez ujawniania zbędnych szczegółów wewnętrznych implementacji. |

### 3.2 Wymagania Wizualne

| ID | Status | Wymaganie |
| --- | --- | --- |
| VIS-001 | Potwierdzone | Wyniki dotyczące przedmiotów powinny wspierać ikony przedmiotów. |
| VIS-002 | Potwierdzone | Wyniki cenowe powinny wspierać ikony walut. |
| VIS-003 | Potwierdzone | Istotne informacje wizualne, takie jak rarity, socket, influence, fractured, corrupted i podobne, powinny być możliwe do reprezentacji. |
| VIS-004 | Kandydat | Źródła assetów, licencje, caching i styl wizualny zostaną zdecydowane później. |
| VIS-005 | Potwierdzone | Model danych musi zawierać stabilne pola dla identyfikatorów assetów lub referencji do obrazów. |

## 4. Cykl Życia Aplikacji

| ID | Status | Wymaganie |
| --- | --- | --- |
| LC-001 | Potwierdzone | PoEnhance może działać, gdy Path of Exile nie jest uruchomione. |
| LC-002 | Potwierdzone | Funkcje takie jak przeglądanie danych, zapisane dane, widoki ekonomii i ręczne wprowadzanie przedmiotów mogą działać poza grą. |
| LC-003 | Potwierdzone | Skróty w grze i zachowanie overlay muszą aktywować się tylko wtedy, gdy Path of Exile jest aktywnym oknem foreground. |
| LC-004 | Potwierdzone | Aplikacja musi wykrywać, czy proces Path of Exile jest uruchomiony. |
| LC-005 | Potwierdzone | Aplikacja musi wykrywać, czy okno Path of Exile jest obecnie aktywne. |
| LC-006 | Potwierdzone | Awaria jednego modułu nie może uniemożliwiać działania niezależnych modułów. |

## 5. Integracja z Path of Exile

| ID | Status | Wymaganie |
| --- | --- | --- |
| POE-001 | Potwierdzone | Dostępne ligi powinny być ładowane dynamicznie z oficjalnego źródła GGG. |
| POE-002 | Potwierdzone | Wybrana liga jest konfigurowalna i przechowywana lokalnie. |
| POE-003 | Potwierdzone | Nazwy lig nie mogą być trwale zakodowane w aplikacji. |
| POE-004 | Potwierdzone | Aplikacja pobiera tekst przedmiotu z gry przez clipboard, zamiast czytać pamięć gry. |
| POE-005 | Potwierdzone | Użytkownik umieszcza kursor nad przedmiotem przed użyciem skrótu price-check. |
| POE-006 | Potwierdzone | Pełny surowy tekst z clipboard musi być zachowany do diagnostyki i fallback processing. |

## 6. Uwierzytelnianie i Dostęp do Konta

| ID | Status | Wymaganie |
| --- | --- | --- |
| AUTH-001 | Potwierdzone | Aplikacja powinna używać oficjalnego OAuth GGG tam, gdzie wymagany jest dostęp do konta. |
| AUTH-002 | Potwierdzone | Użytkownik uwierzytelnia się przez oficjalną stronę Path of Exile w przeglądarce. |
| AUTH-003 | Potwierdzone | PoEnhance nigdy nie może prosić o hasło GGG użytkownika ani go przechowywać. |
| AUTH-004 | Potwierdzone | Tokeny powinny być odświeżane automatycznie tam, gdzie to możliwe. |
| AUTH-005 | Potwierdzone | Gdy wymagane jest ponowne uwierzytelnienie, aplikacja powinna pokazać jasny komunikat z akcją otwierającą oficjalny proces logowania. |
| AUTH-006 | Potwierdzone | Funkcje niewymagające dostępu do konta muszą nadal działać po wygaśnięciu uwierzytelnienia. |
| AUTH-007 | Potwierdzone | Funkcje zależne od konta, takie jak stash value, powinny jasno pokazywać, że wymagane jest ponowne połączenie. |

## 7. Wymagania Price Checkera

### 7.1 Dane Wejściowe

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-IN-001 | Potwierdzone | Sprawdzanie przedmiotu w grze jest inicjowane konfigurowalnym skrótem klawiaturowym, gdy Path of Exile jest aktywne. |
| PC-IN-002 | Potwierdzone | Parser powinien wspierać normalne i zaawansowane opisy przedmiotów. |
| PC-IN-003 | Potwierdzone | Zaawansowane opisy przedmiotów powinny być preferowane, gdy są dostępne. |
| PC-IN-004 | Potwierdzone | Price checker musi wspierać ręcznie wklejony tekst przedmiotu poza grą. |

### 7.2 Parsowanie

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-PARSE-001 | Potwierdzone | Parser powinien samodzielnie wyodrębniać ustrukturyzowane informacje tam, gdzie to możliwe. |
| PC-PARSE-002 | Potwierdzone | Parsowane pola powinny obejmować item class, rarity, name, base type, item level, properties, requirements, sockets and links, implicits, explicits, crafted mods, fractured mods, enchantments, corruption i inne flagi przedmiotu, gdy są obecne. |
| PC-PARSE-003 | Potwierdzone | Brakujące lub nieznane dane nie mogą powodować awarii ani unieważniać całego sparsowanego przedmiotu. |
| PC-PARSE-004 | Potwierdzone | Rozpoznawanie przedmiotów musi wspierać stany pewności: Exact, Probable, Generic i Unknown. |
| PC-PARSE-005 | Potwierdzone | Przedmioty z rozpoznaniem innym niż exact nadal mogą być pokazywane i wyszukiwane, ale stan pewności musi być widoczny dla użytkownika. |

### 7.3 Rozpoznawanie i Fallback

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-REC-001 | Potwierdzone | Lokalne katalogi przedmiotów i modów są podstawowym źródłem rozpoznawania. |
| PC-REC-002 | Potwierdzone | Rozpoznawanie nie może zależeć wyłącznie od dokładnego dopasowania nazwy przedmiotu. |
| PC-REC-003 | Potwierdzone | Dopasowanie może używać wielu sygnałów, w tym name, base type, item class or group, rarity, properties, mod signatures, trade stat identifiers i innych stabilnych identyfikatorów, gdy są dostępne. |
| PC-REC-004 | Potwierdzone | Gdy przedmiotu brakuje w lokalnym katalogu, aplikacja może zapytać skonfigurowane źródła online. |
| PC-REC-005 | Potwierdzone | Źródła online są dostawcami fallback i nie mogą być wymagane do normalnego działania. |
| PC-REC-006 | Potwierdzone | Dane zwrócone przez dostawców fallback powinny być porównywane według wielu sygnałów. |
| PC-REC-007 | Potwierdzone | Wyniki fallback powinny tworzyć prowizoryczny rekord w cache, zamiast po cichu modyfikować zaufany główny katalog. |
| PC-REC-008 | Potwierdzone | Prowizoryczne rekordy powinny przechowywać źródło, timestamp, kontekst ligi lub patcha oraz poziom pewności. |
| PC-REC-009 | Potwierdzone | Przedmioty z nowej ligi muszą powodować graceful degradation, zamiast czynić price checker bezużytecznym. |

### 7.4 Klasyfikacja Kanału Handlu

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-TRADE-001 | Potwierdzone | Price checker musi rozróżniać stackable items obsługiwane przez Currency Exchange od individual items obsługiwanych przez item Trade lub Manage Shop. |
| PC-TRADE-002 | Potwierdzone | Ścieżki Currency Exchange i handlu individual-item mogą używać różnych zapytań, modeli wyników i interfejsów użytkownika. |
| PC-TRADE-003 | Potwierdzone | Merchant Only lub instant-buy offers powinny być domyślne dla wyszukiwań individual-item. |
| PC-TRADE-004 | Potwierdzone | Użytkownik musi móc przełączyć się na in-person trade. |
| PC-TRADE-005 | Potwierdzone | Dokładne domyślne filtry mogą zostać dostosowane po testach. |

### 7.5 Wyszukiwanie Rzadkich Przedmiotów

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-RARE-001 | Potwierdzone | Aplikacja powinna wyświetlać rozpoznane mody przed wysłaniem zapytania trade search. |
| PC-RARE-002 | Potwierdzone | Domyślnie użytkownik ręcznie wybiera, które mody i zakresy są uwzględnione. |
| PC-RARE-003 | Potwierdzone | Otwarcie price checkera lub zmiana filtrów nie może automatycznie wysyłać zapytania wyszukiwania. |
| PC-RARE-004 | Potwierdzone | Żądanie jest wysyłane tylko po jawnej akcji Search. |
| PC-RARE-005 | Kandydat | Automatyczne wyszukiwanie może zostać dodane później jako opcjonalne ustawienie użytkownika. |
| PC-RARE-006 | Potwierdzone | Przyszły automatyczny wybór ważnych modów musi być możliwy bez przeprojektowania parsera lub modelu przedmiotu. |
| PC-RARE-007 | Potwierdzone | Każdy sparsowany mod powinien wspierać metadane potrzebne przyszłym strategiom wyboru. |

### 7.6 Walidacja Żądań i Rate Limits

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-REQ-001 | Potwierdzone | Parametry wyszukiwania muszą być walidowane lokalnie przed wysłaniem jakiegokolwiek żądania. |
| PC-REQ-002 | Potwierdzone | Wykrywalne błędy, takie jak wartość minimalna większa od maksymalnej, muszą zużywać zero żądań API. |
| PC-REQ-003 | Potwierdzone | Nieprawidłowe wartości liczbowe, brakujące wymagane pola i lokalnie wykrywalne sprzeczne filtry powinny być odrzucane przed wywołaniem GGG. |
| PC-REQ-004 | Potwierdzone | Aplikacja nigdy nie może świadomie marnować żądania na zapytanie, o którym już wie, że jest nieprawidłowe. |
| PC-REQ-005 | Potwierdzone | Aplikacja musi odczytywać i respektować informacje GGG o rate limit. |
| PC-REQ-006 | Potwierdzone | Rate limits nie mogą być reprezentowane przez trwale zakodowane wartości. |
| PC-REQ-007 | Potwierdzone | UI powinno pokazywać użytkownikowi bieżący stan żądań tam, gdzie jest to istotne. |
| PC-REQ-008 | Potwierdzone | Przed akcjami takimi jak Search, Refresh lub Load More użytkownik powinien móc zobaczyć, jak akcja wpływa na request limits. |
| PC-REQ-009 | Potwierdzone | Gdy jest to wymagane, żądania powinny być kolejkowane zamiast wysyłane z naruszeniem obecnych limitów. |
| PC-REQ-010 | Potwierdzone | Aplikacja nie może próbować omijać ani obchodzić rate limits GGG. |

### 7.7 Wyniki Wyszukiwania

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-RES-001 | Potwierdzone | Wyniki wyszukiwania powinny początkowo ładować pierwszych 10 szczegółowych ofert. |
| PC-RES-002 | Potwierdzone | Dodatkowe wyniki powinny być ładowane w partiach po 10 przez jawną akcję Load More. |
| PC-RES-003 | Potwierdzone | Ładowanie dodatkowych partii wyników nie może powtarzać pierwotnego wyszukiwania, gdy można ponownie użyć istniejących identyfikatorów wyników. |
| PC-RES-004 | Potwierdzone | UI powinno wskazywać, czy Load More wymaga kolejnego żądania fetch. |
| PC-RES-005 | Potwierdzone | Wyniki powinny być domyślnie sortowane rosnąco według ceny. |
| PC-RES-006 | Potwierdzone | Aplikacja powinna wyświetlać oferty zwrócone przez GGG bez ukrywania listingów na podstawie niewspieranych założeń. |
| PC-RES-007 | Potwierdzone | Aplikacja nie powinna automatycznie interpretować dużych różnic cenowych za użytkownika. |
| PC-RES-008 | Potwierdzone | Jeden, dwa lub szeroko rozproszone wyniki nadal powinny być pokazane tak, jak zostały zwrócone. |

### 7.8 Błędy Wyników

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-ERR-001 | Potwierdzone | Aplikacja musi rozróżniać poprawne zapytania bez pasujących ofert od nieprawidłowych lokalnych parametrów wyszukiwania, awarii GGG lub sieci oraz opóźnienia przez rate-limit. |
| PC-ERR-002 | Potwierdzone | Aplikacja nie może automatycznie luzować filtrów ani wysyłać alternatywnych wyszukiwań. |
| PC-ERR-003 | Potwierdzone | Użytkownik pozostaje odpowiedzialny za interpretację wyników rynkowych. |

### 7.9 Podgląd Oferty i Karty Przedmiotów

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-CARD-001 | Potwierdzone | Najechanie na załadowany wynik może pokazać tymczasowy podgląd przedmiotu. |
| PC-CARD-002 | Potwierdzone | Kliknięcie załadowanej oferty powinno otworzyć trwałą kartę przedmiotu pokazującą dokładne dane przedmiotu zwrócone dla tego listingu. |
| PC-CARD-003 | Potwierdzone | Karta przedmiotu powinna wizualnie przypominać tooltip przedmiotu z Path of Exile tam, gdzie jest to praktyczne. |
| PC-CARD-004 | Potwierdzone | Karta przedmiotu powinna wyświetlać odpowiednie properties, sockets, implicits, explicits, crafted mods, fractured mods, enchantments, corruption, influence i inne zwrócone dane. |
| PC-CARD-005 | Potwierdzone | Wyświetlenie karty dla już pobranego listingu nie może wywoływać kolejnego żądania sieciowego. |
| PC-CARD-006 | Potwierdzone | Lokalne katalogi modów mogą wzbogacać kartę o tiers lub tags. |
| PC-CARD-007 | Potwierdzone | Jeśli wzbogacenie jest niepewne, surowy zwrócony tekst moda powinien być pokazany bez zgadywania. |

### 7.10 Akcje Handlowe

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-ACT-001 | Potwierdzone | Aplikacja może otworzyć oficjalną stronę trade dla wyszukiwania lub oferty. |
| PC-ACT-002 | Potwierdzone | Aplikacja nie może polegać na nieudokumentowanych lub ukrytych endpointach, aby bezpośrednio teleportować użytkownika do kryjówki sprzedawcy. |
| PC-ACT-003 | Kandydat | Bezpośrednia integracja podróży może być rozważona tylko wtedy, gdy zostanie zidentyfikowana oficjalnie wspierana metoda. |
| PC-ACT-004 | Kandydat | Ewentualne przyszłe wsparcie whisper nie może automatycznie wysyłać wiadomości ani wykonywać wielu akcji w grze bez jawnego działania użytkownika. |

### 7.11 Wyświetlanie Cen

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-PRICE-001 | Potwierdzone | Pokazywać dokładne ceny listingów zwrócone przez GGG. |
| PC-PRICE-002 | Kandydat | Opcjonalnie pokazywać wartość przeliczoną na inną wybraną walutę w grze. |
| PC-PRICE-003 | Kandydat | Przeliczone ceny muszą pokazywać źródło i świeżość kursu wymiany. |
| PC-PRICE-004 | Kandydat | Automatyczne szacowane zakresy cen oparte na interpretacji statystycznej nie są jeszcze potwierdzone. |

### 7.12 Historia i Zapisane Przedmioty

| ID | Status | Wymaganie |
| --- | --- | --- |
| PC-HIST-001 | Kandydat | Utrzymywać małą lokalną historię ostatnich wyszukiwań i ich snapshotów wyników. |
| PC-HIST-002 | Kandydat | Ponowne otwarcie historycznego snapshota nie może wysyłać nowego żądania. |
| PC-HIST-003 | Kandydat | Pozwolić użytkownikowi ręcznie odświeżyć stare wyszukiwanie. |
| PC-HIST-004 | Kandydat | Pozwolić użytkownikowi zapisywać wybrane przedmioty lub wyszukiwania lokalnie. |
| PC-HIST-005 | Kandydat | Zapisany przedmiot może pokazywać swoją ostatnio sprawdzoną cenę oraz kiedy została sprawdzona. |
| PC-HIST-006 | Kandydat | Zapisany przedmiot może udostępniać poprzednie wyceny tylko po jawnej akcji użytkownika. |
| PC-HIST-007 | Kandydat | Przypinanie i porównywanie kart przedmiotów wymaga walidacji UI. |
| PC-HIST-008 | Kandydat | Eksport i import zapisanych wyszukiwań jest kandydatem o niskim priorytecie. |

## 8. Wspólny Rdzeń Danych Gry

| ID | Status | Wymaganie |
| --- | --- | --- |
| DATA-001 | Potwierdzone | Katalogi danych gry muszą należeć do współdzielonej warstwy danych, a nie prywatnie do price checkera. |
| DATA-002 | Potwierdzone | Wiele modułów powinno ponownie używać tych samych katalogów item, mod, base-type, category i property. |
| DATA-003 | Potwierdzone | Wspólne katalogi mogą obejmować regular modifiers, implicit modifiers, synthesis modifiers, corruption implicits, enchantments, influence modifiers, fractured modifiers, item bases, item classes, mod groups, tiers, ranges, tags i eligibility rules opisujące, które typy przedmiotów mogą otrzymać dany modifier. |
| DATA-004 | Potwierdzone | Te same dane synthesis używane przez price checker powinny później wspierać pytania takie jak to, które synthesis implicits mogą wystąpić na rings. |
| DATA-005 | Potwierdzone | Te same dane corruption powinny później wspierać wyszukiwania takie jak to, które corruption implicits mogą wystąpić na danym typie przedmiotu. |
| DATA-006 | Potwierdzone | Pakiety danych powinny być aktualizowalne niezależnie od głównej aplikacji tam, gdzie jest to praktyczne. |
| DATA-007 | Potwierdzone | Architektura musi pozwalać na stopniowe dodawanie nietypowych klas przedmiotów i specjalnych przypadków bez wymagania, aby wersja 0.1 implementowała je wszystkie. |

## 9. Lokalny Storage i Cache

| ID | Status | Wymaganie |
| --- | --- | --- |
| STORE-001 | Potwierdzone | Przechowywać ustawienia aplikacji lokalnie. |
| STORE-002 | Potwierdzone | Przechowywać wybraną ligę i preferencje użytkownika lokalnie. |
| STORE-003 | Potwierdzone | Przechowywać search cache lokalnie. |
| STORE-004 | Potwierdzone | Przechowywać prowizoryczne rekordy przedmiotów lokalnie. |
| STORE-005 | Potwierdzone | Przechowywać historię i zapisane przedmioty lokalnie, jeśli te funkcje kandydujące zostaną zaimplementowane. |
| STORE-006 | Potwierdzone | Sekrety uwierzytelniania nie mogą być uwzględniane w zwykłych plikach eksportu. |
| STORE-007 | Potwierdzone | Cloud synchronization nie jest początkowo wymagane. |
| CACHE-001 | Potwierdzone | Identyczne niedawne wyszukiwania powinny być obsługiwane z lokalnego cache tam, gdzie jest to właściwe. |
| CACHE-002 | Potwierdzone | Wyniki z cache muszą jasno pokazywać swój wiek. |
| CACHE-003 | Potwierdzone | Użytkownik może jawnie zażądać odświeżonych danych. |
| CACHE-004 | Potwierdzone | Refresh musi pokazywać, że używa kolejnego żądania, zanim użytkownik go aktywuje. |
| CACHE-005 | Kandydat | Czas trwania cache powinien być konfigurowalny lub ponownie oceniony po testach. |
| CACHE-006 | Kandydat | Początkowe założenie robocze to około dwie minuty dla wyników individual-item Trade i około jedna minuta dla wyników Currency Exchange. |

## 10. Zewnętrzne Źródła Danych

| ID | Status | Wymaganie |
| --- | --- | --- |
| EXT-001 | Potwierdzone | Preferować oficjalne API GGG i OAuth tam, gdzie są dostępne. |
| EXT-002 | Potwierdzone | Dostępne ligi powinny pochodzić z oficjalnego źródła GGG. |
| EXT-003 | Potwierdzone | Źródła online fallback mogą być używane, gdy lokalne katalogi nie mogą rozpoznać przedmiotu. |
| EXT-004 | Potwierdzone | Niewspierany scraping lub źródła third-party muszą zostać sprawdzone pod kątem zgody, niezawodności i warunków użycia przed implementacją. |
| EXT-005 | Potwierdzone | Awaria zewnętrznego dostawcy nie może psuć niezależnych modułów. |
| EXT-006 | Potwierdzone | Żądania sieciowe powinny być obserwowalne przez użyteczne lokalne logi. |

## 11. Obsługa Błędów i Graceful Degradation

| ID | Status | Wymaganie |
| --- | --- | --- |
| ERR-001 | Potwierdzone | Nieznane dane muszą powodować graceful degradation, a nie awarię aplikacji. |
| ERR-002 | Potwierdzone | Brakujące wpisy katalogu nie mogą czynić price checkera bezużytecznym. |
| ERR-003 | Potwierdzone | Awaria zewnętrznego dostawcy nie może psuć niezależnych modułów. |
| ERR-004 | Potwierdzone | Błędy widoczne dla użytkownika powinny być zrozumiałe bez ujawniania zbędnych szczegółów wewnętrznych implementacji. |
| ERR-005 | Potwierdzone | Aplikacja powinna pozostawać responsywna podczas operacji sieciowych lub aktualizacji danych. |

## 12. Bezpieczeństwo i Zgodność z GGG

| ID | Status | Wymaganie |
| --- | --- | --- |
| SEC-001 | Potwierdzone | Brak czytania pamięci gry. |
| SEC-002 | Potwierdzone | Brak DLL injection. |
| SEC-003 | Potwierdzone | Brak packet interception. |
| SEC-004 | Potwierdzone | Brak bottingu lub automatyzacji gameplay. |
| SEC-005 | Potwierdzone | Brak prób omijania limitów API. |
| SEC-006 | Potwierdzone | Brak automatycznych gameplay macros wykonujących wiele akcji. |
| SEC-007 | Potwierdzone | Akcje użytkownika wpływające na grę muszą pozostać jawne i zgodne z zasadami GGG. |
| SEC-008 | Potwierdzone | Zgodność z GGG musi być sprawdzana dla każdej dużej funkcji. |
| SEC-009 | Poza zakresem | Workflows związane z real-money trading nie są wspierane. |
| SEC-010 | Poza zakresem | Funkcje zaprojektowane do omijania ograniczeń gry lub API nie są wspierane. |

## 13. Zakres Wersji 0.1

Wersja 0.1 jest prototypem technicznym, nie pełną aplikacją.

### 13.1 Wymagane dla Wersji 0.1

| ID | Status | Wymaganie |
| --- | --- | --- |
| V01-001 | Potwierdzone | Powłoka aplikacji Windows WPF. |
| V01-002 | Potwierdzone | Zachowanie aplikacji w tle. |
| V01-003 | Potwierdzone | Wykrywanie procesu Path of Exile. |
| V01-004 | Potwierdzone | Wykrywanie aktywnego okna Path of Exile. |
| V01-005 | Potwierdzone | Fundament konfigurowalnych skrótów. |
| V01-006 | Potwierdzone | Przechwytywanie przedmiotu przez clipboard. |
| V01-007 | Potwierdzone | Ręczne wklejanie danych przedmiotu. |
| V01-008 | Potwierdzone | Podstawowy parser przedmiotów. |
| V01-009 | Potwierdzone | Wyświetlanie sparsowanej nazwy przedmiotu, rarity, base type i rozpoznanych modów. |
| V01-010 | Potwierdzone | Podstawowe okno overlay. |
| V01-011 | Potwierdzone | Czyste rozdzielenie UI i logiki parsowania. |
| V01-012 | Potwierdzone | Lokalne logowanie wystarczające do developmentu. |
| V01-013 | Potwierdzone | Początkowe testy automatyczne zachowania parsera tam, gdzie jest to praktyczne. |

### 13.2 Niewymagane dla Wersji 0.1

| ID | Status | Wymaganie |
| --- | --- | --- |
| V01-NR-001 | Odłożone | Pełna integracja Trade API. |
| V01-NR-002 | Odłożone | Pełna integracja OAuth. |
| V01-NR-003 | Odłożone | Stash value. |
| V01-NR-004 | Odłożone | Pełny moduł ekonomii. |
| V01-NR-005 | Odłożone | Kompletny lokalny game database. |
| V01-NR-006 | Odłożone | Wsparcie dla każdego nietypowego typu przedmiotu. |
| V01-NR-007 | Odłożone | Dopracowane finalne UI. |

## 14. Funkcje Kandydujące

| ID | Status | Wymaganie |
| --- | --- | --- |
| CAND-002 | Kandydat | Przeliczone ceny w innej wybranej walucie w grze z pokazanym źródłem i świeżością. |
| CAND-003 | Kandydat | Automatyczne szacowane zakresy cen oparte na interpretacji statystycznej. |
| CAND-004 | Kandydat | Mała lokalna historia wyszukiwań ze snapshotami wyników. |
| CAND-005 | Kandydat | Zapisane przedmioty lub zapisane wyszukiwania. |
| CAND-006 | Kandydat | Poprzednie wyceny udostępniane tylko po jawnej akcji użytkownika. |
| CAND-007 | Kandydat | Przypinanie i porównywanie kart przedmiotów. |
| CAND-008 | Kandydat | Eksport i import zapisanych wyszukiwań. |
| CAND-009 | Kandydat | Opcjonalne automatyczne wyszukiwanie. |
| CAND-010 | Kandydat | Przyszły automatyczny wybór ważnych modów. |
| CAND-011 | Kandydat | Przyszłe wsparcie whisper, które pozostaje jawne i zgodne z zasadami. |

## 15. Funkcje Odłożone

| ID | Status | Wymaganie |
| --- | --- | --- |
| DEF-001 | Odłożone | Bulk Search lub Bulk Buy dla wielu kopii tego samego non-stackable item. |
| DEF-002 | Odłożone | Optymalizacja pod najniższy całkowity koszt lub najmniejszą liczbę kryjówek sprzedawców. |
| DEF-003 | Odłożone | Pełna przestrzeń robocza porównywania zapisanych przedmiotów. |
| DEF-004 | Odłożone | Infrastruktura publicznych wydań. |
| DEF-005 | Odłożone | Cloud synchronization. |
| DEF-006 | Odłożone | Wsparcie Path of Exile 2. |

### 15.1 TODO dla Specjalnych Przedmiotów

Te elementy są odłożone i powinny zostać rozbite na małe zadania. Architektura musi pozwalać na te dodatki, ale wersja 0.1 nie może implementować ich wszystkich.

| ID | Status | Wymaganie |
| --- | --- | --- |
| ITEM-001 | Odłożone | Unidentified items. |
| ITEM-002 | Odłożone | Corrupted items. |
| ITEM-003 | Odłożone | Mirrored or duplicated items. |
| ITEM-004 | Odłożone | Fractured items. |
| ITEM-005 | Odłożone | Synthesised items. |
| ITEM-006 | Odłożone | Influenced items. |
| ITEM-007 | Odłożone | Enchantments. |
| ITEM-008 | Odłożone | Special implicits. |
| ITEM-009 | Odłożone | Gems. |
| ITEM-010 | Odłożone | Maps. |
| ITEM-011 | Odłożone | Cluster jewels. |
| ITEM-012 | Odłożone | Timeless jewels. |
| ITEM-013 | Odłożone | Inne nietypowe klasy przedmiotów. |

## 16. Poza Zakresem

| ID | Status | Wymaganie |
| --- | --- | --- |
| OOS-001 | Poza zakresem | Automatyzacja gameplay. |
| OOS-002 | Poza zakresem | Botting. |
| OOS-003 | Poza zakresem | Workflows związane z real-money trading. |
| OOS-004 | Poza zakresem | Czytanie lub modyfikowanie pamięci Path of Exile. |
| OOS-005 | Poza zakresem | Ukryte lub nieudokumentowane endpointy GGG. |
| OOS-006 | Poza zakresem | Funkcje zaprojektowane do omijania ograniczeń gry lub API. |
| OOS-007 | Poza zakresem | Tworzenie zamiennika wiki Path of Exile. |

## 17. Otwarte Pytania

| ID | Status | Pytanie |
| --- | --- | --- |
| OQ-001 | Kandydat | Jakie są finalne czasy trwania cache dla wyników Trade i Currency Exchange? |
| OQ-002 | Kandydat | Jakie są finalne zachowania pozycjonowania i zamykania overlay? |
| OQ-003 | Kandydat | Czy wybór frameworka UI powinien zostać ponownie oceniony po prototypie technicznym? |
| OQ-004 | Kandydat | Którzy zewnętrzni dostawcy fallback są akceptowalni? |
| OQ-005 | Kandydat | Jakie źródła assetów i ikon mogą być użyte oraz na jakich licencjach? |
| OQ-006 | Kandydat | Jaki jest finalny okres przechowywania historii? |
| OQ-007 | Kandydat | Czy estymacja ceny wykraczająca poza surowe listingi jest wystarczająco użyteczna, aby ją uwzględnić? |
| OQ-008 | Kandydat | Jakie są finalne zasady przyszłego automatycznego wyboru ważnych modów? |
