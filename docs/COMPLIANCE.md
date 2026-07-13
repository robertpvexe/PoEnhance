# PoEnhance Compliance

## 1. Document Purpose

This document is an internal engineering checklist for reducing compliance risk while designing and implementing PoEnhance.

- It is not legal advice.
- GGG rules, API policies, and supported integrations may change.
- Current official GGG documentation must be checked before implementing or releasing major functionality.

## 2. Core Compliance Principle

PoEnhance assists the player with information and workflow organization, but it must never play the game for the user.

## 3. Confirmed Prohibited Behaviors

- Reading or modifying Path of Exile process memory.
- DLL injection.
- Packet interception or modification.
- Botting.
- Automated gameplay.
- Hidden background gameplay actions.
- Bypassing or evading API rate limits.
- Relying on hidden or undocumented GGG endpoints.
- Automatic multi-action gameplay macros.
- Real-money trading workflows.
- Storing or requesting the user's GGG password.

## 4. Allowed Design Direction

These directions are consistent with the current project documentation, but every implementation must still be reviewed against current official rules:

- Reading item text through the clipboard.
- User-triggered keyboard shortcuts.
- Shortcuts active only when Path of Exile is the active foreground window.
- Displaying information in an external overlay.
- Local parsing and local data processing.
- Browser-based official GGG OAuth.
- Official GGG APIs where available.
- Explicit user-triggered Path of Exile web Trade Search/Fetch endpoint use for price checks, when isolated behind replaceable integration code.
- Opening official Path of Exile or Trade pages.
- Local caching that respects freshness and rate limits.
- Explicit user actions for Search, Refresh, Load More, login, and future trade-related actions.

This list does not mean every design is permanently approved by GGG.

## 5. One User Action Principle

Actions affecting the game must be initiated explicitly by the user. PoEnhance must not silently chain multiple in-game actions from one shortcut.

Future trade-related actions, including any whisper support, must preserve explicit user control and must be reviewed before implementation.

## 6. API and Rate-Limit Rules

- Respect rate-limit headers returned by GGG.
- Do not permanently hardcode assumed limits.
- Validate requests locally before sending them.
- Queue requests where necessary.
- Never intentionally bypass limits.
- Do not automatically search, retry, resend, discover hidden endpoints, bypass authentication, index Public Stash data, or evade rate limits.
- Clearly communicate request cost for Search, Refresh, and Load More.
- Cache recent results where appropriate.

## 7. Authentication and Sensitive Data

- Use official browser-based OAuth when account access is implemented.
- Never collect the user's GGG password.
- Store authentication tokens using an appropriate secure Windows mechanism when OAuth is implemented.
- Do not include secrets or tokens in logs, ordinary exports, screenshots, or diagnostics.
- Keep features that do not require account access usable after authentication expires.

The final token-storage implementation is not selected yet.

## 8. Third-Party Data Sources

- Official GGG sources are preferred.
- Third-party fallback providers may be considered.
- Scraping, redistribution, caching, and asset use require review of permission, reliability, terms, and licensing.
- One external provider must not become a single point of failure.
- Fallback data must show source, timestamp, and confidence where relevant.
- Provisional data must not silently replace trusted local catalogs.

## 9. Game Files and Extracted Data

Direct interaction with game files or extracted game data requires a separate compliance review before implementation. This document does not declare such access allowed.

## 10. Assets and Intellectual Property

- Item and currency icons require source and license review.
- GGG branding and game assets must not be assumed free for unrestricted redistribution.
- Third-party icons and fonts require license verification.
- Generated or custom assets must avoid falsely representing official GGG endorsement.

## 11. Logging and Diagnostics

- Redact tokens and sensitive account data.
- Preserve enough information for debugging requests and parser failures.
- Raw clipboard item text may be stored for diagnostics only according to clear local-storage rules.
- Diagnostic export must not contain authentication secrets.

## 12. Feature Compliance Review Checklist

For every major feature, answer:

- What user action starts it?
- Does it send input to the game?
- How many game actions occur?
- Does it read memory, files, or network traffic?
- Which API or external provider is used?
- Is the API officially documented?
- What rate limits apply?
- What data is stored?
- Does it use protected assets?
- Does failure affect unrelated modules?
- Has current official GGG documentation been reviewed?
- Is the decision documented in DECISIONS.md?

## 13. Current Compliance Decisions

- PoEnhance supports Path of Exile 1 initially.
- The application assists the player but must not automate gameplay.
- In-game shortcuts and overlay behavior activate only when Path of Exile is the active foreground window.
- Item text is obtained through the clipboard and manual pasted input, not game-memory reading.
- Official GGG OAuth should be used where account access is required.
- PoEnhance must never request or store the user's GGG password.
- Official GGG APIs are preferred where available.
- Publicly reachable Path of Exile web Trade Search/Fetch endpoints may be used only for explicit user-triggered price checks, through isolated and replaceable integration code that can be disabled if upstream behavior or policy changes.
- Search, Refresh, and Load More actions must respect current rate limits.
- Hidden or undocumented GGG endpoints are outside the project scope.
- Currency Exchange integration remains deferred and separate from individual-item Trade Search/Fetch.
- Real-money trading workflows are outside the project scope.

## 14. Open Compliance Questions

- Exact external fallback providers.
- Permissions for PoEDB or other third-party data use.
- Asset and icon licensing.
- Final OAuth scopes.
- Secure token storage implementation.
- Future whisper support.
- Whether an officially supported direct Travel to Hideout integration exists.
- Any functionality involving game files or extracted game data.

## 15. Review Triggers

Run a new compliance review when:

- GGG policies change.
- A new API is introduced.
- A module begins sending game input.
- A new external provider is added.
- Authentication scopes change.
- The project moves toward public distribution.
- Path of Exile 2 support is considered.

---

# PoEnhance — Zgodność z zasadami (wersja polska)

Wersja angielska jest kanonicznym źródłem tego dokumentu. Ta sekcja po polsku jest tłumaczeniem pomocniczym.

## 1. Cel Dokumentu

Ten dokument jest wewnętrzną checklistą inżynieryjną służącą ograniczaniu ryzyka zgodności podczas projektowania i implementowania PoEnhance.

- To nie jest porada prawna.
- Zasady GGG, polityki API i wspierane integracje mogą się zmieniać.
- Aktualna oficjalna dokumentacja GGG musi zostać sprawdzona przed implementacją lub wydaniem ważnej funkcjonalności.

## 2. Główna Zasada Zgodności

PoEnhance pomaga graczowi informacjami i organizacją workflow, ale nigdy nie może grać za użytkownika.

## 3. Potwierdzone Zabronione Zachowania

- Czytanie lub modyfikowanie pamięci procesu Path of Exile.
- DLL injection.
- Przechwytywanie lub modyfikowanie pakietów.
- Botting.
- Automatyzacja gameplay.
- Ukryte akcje gameplay wykonywane w tle.
- Omijanie lub obchodzenie limitów API.
- Poleganie na ukrytych lub nieudokumentowanych endpointach GGG.
- Automatyczne wieloakcyjne makra gameplay.
- Workflows real-money trading.
- Przechowywanie lub proszenie o hasło GGG użytkownika.

## 4. Dozwolony Kierunek Projektowy

Te kierunki są zgodne z obecną dokumentacją projektu, ale każda implementacja nadal musi zostać sprawdzona względem aktualnych oficjalnych zasad:

- Odczytywanie tekstu przedmiotu przez clipboard.
- Skróty klawiaturowe wywoływane przez użytkownika.
- Skróty aktywne tylko wtedy, gdy Path of Exile jest aktywnym oknem foreground.
- Wyświetlanie informacji w zewnętrznym overlay.
- Lokalne parsowanie i lokalne przetwarzanie danych.
- Oficjalny OAuth GGG w przeglądarce.
- Oficjalne API GGG tam, gdzie są dostępne.
- Jawne, wywołane przez użytkownika użycie web endpointów Path of Exile Trade Search/Fetch do sprawdzania cen, gdy jest odizolowane za wymienialnym kodem integracyjnym.
- Otwieranie oficjalnych stron Path of Exile lub Trade.
- Lokalny cache respektujący świeżość i rate limit.
- Jawne akcje użytkownika dla Search, Refresh, Load More, logowania i przyszłych akcji związanych z handlem.

Ta lista nie oznacza, że każdy projekt jest trwale zatwierdzony przez GGG.

## 5. Zasada Jednej Akcji Użytkownika

Akcje wpływające na grę muszą być inicjowane jawnie przez użytkownika. PoEnhance nie może po cichu łączyć wielu akcji w grze z jednego skrótu.

Przyszłe akcje związane z handlem, w tym ewentualne wsparcie whisper, muszą zachować jawną kontrolę użytkownika i wymagają review przed implementacją.

## 6. Zasady API i Rate Limit

- Respektować nagłówki rate limit zwracane przez GGG.
- Nie kodować na stałe założonych limitów.
- Walidować żądania lokalnie przed wysłaniem.
- Kolejkować żądania tam, gdzie to konieczne.
- Nigdy celowo nie omijać limitów.
- Nie wyszukiwać automatycznie, nie ponawiać ani nie wysyłać ponownie żądań automatycznie, nie odkrywać ukrytych endpointów, nie obchodzić uwierzytelniania, nie indeksować Public Stash i nie omijać rate limitów.
- Jasno komunikować koszt żądania dla Search, Refresh i Load More.
- Używać cache dla niedawnych wyników tam, gdzie to właściwe.

## 7. Uwierzytelnianie i Dane Wrażliwe

- Używać oficjalnego OAuth w przeglądarce, gdy zostanie zaimplementowany dostęp do konta.
- Nigdy nie zbierać hasła GGG użytkownika.
- Przechowywać tokeny uwierzytelniania przy użyciu odpowiedniego bezpiecznego mechanizmu Windows, gdy OAuth zostanie zaimplementowany.
- Nie umieszczać sekretów ani tokenów w logach, zwykłych eksportach, zrzutach ekranu ani diagnostyce.
- Funkcje niewymagające dostępu do konta powinny pozostać używalne po wygaśnięciu uwierzytelnienia.

Finalna implementacja przechowywania tokenów nie została jeszcze wybrana.

## 8. Zewnętrzne Źródła Danych

- Oficjalne źródła GGG są preferowane.
- Zewnętrzni dostawcy fallback mogą być rozważeni.
- Scraping, redystrybucja, cache i użycie assetów wymagają review pod kątem zgody, niezawodności, warunków i licencji.
- Jeden zewnętrzny dostawca nie może stać się pojedynczym punktem awarii.
- Dane fallback muszą pokazywać źródło, timestamp i confidence tam, gdzie jest to istotne.
- Dane prowizoryczne nie mogą po cichu zastępować zaufanych lokalnych katalogów.

## 9. Pliki Gry i Wyodrębnione Dane

Bezpośrednia interakcja z plikami gry lub wyodrębnionymi danymi gry wymaga osobnego review zgodności przed implementacją. Ten dokument nie uznaje takiego dostępu za dozwolony.

## 10. Assety i Własność Intelektualna

- Ikony przedmiotów i walut wymagają review źródła i licencji.
- Branding GGG i assety gry nie mogą być uznawane za swobodnie dostępne do nieograniczonej redystrybucji.
- Ikony i fonty third-party wymagają weryfikacji licencji.
- Assety generowane lub niestandardowe muszą unikać fałszywego sugerowania oficjalnego poparcia GGG.

## 11. Logowanie i Diagnostyka

- Redagować tokeny i wrażliwe dane konta.
- Zachować wystarczająco dużo informacji do debugowania żądań i błędów parsera.
- Surowy tekst przedmiotu z clipboard może być przechowywany diagnostycznie tylko zgodnie z jasnymi zasadami lokalnego storage.
- Eksport diagnostyczny nie może zawierać sekretów uwierzytelniania.

## 12. Checklista Review Zgodności Funkcji

Dla każdej dużej funkcji odpowiedz:

- Jaka akcja użytkownika ją uruchamia?
- Czy wysyła input do gry?
- Ile akcji w grze występuje?
- Czy czyta pamięć, pliki lub ruch sieciowy?
- Jakie API lub zewnętrzny dostawca jest używany?
- Czy API jest oficjalnie udokumentowane?
- Jakie rate limits obowiązują?
- Jakie dane są przechowywane?
- Czy używa chronionych assetów?
- Czy awaria wpływa na niezależne moduły?
- Czy sprawdzono aktualną oficjalną dokumentację GGG?
- Czy decyzja została udokumentowana w DECISIONS.md?

## 13. Obecne Decyzje Zgodności

- PoEnhance początkowo wspiera Path of Exile 1.
- Aplikacja pomaga graczowi, ale nie może automatyzować gameplay.
- Skróty w grze i zachowanie overlay aktywują się tylko wtedy, gdy Path of Exile jest aktywnym oknem foreground.
- Tekst przedmiotu jest pobierany przez clipboard i ręcznie wklejony input, a nie przez czytanie pamięci gry.
- Oficjalny OAuth GGG powinien być używany tam, gdzie wymagany jest dostęp do konta.
- PoEnhance nigdy nie może prosić o hasło GGG użytkownika ani go przechowywać.
- Oficjalne API GGG są preferowane tam, gdzie są dostępne.
- Publicznie osiągalne web endpointy Path of Exile Trade Search/Fetch mogą być używane tylko dla jawnych price checków wywołanych przez użytkownika, przez odizolowany i wymienialny kod integracyjny, który można wyłączyć, jeśli zmieni się zachowanie upstream lub polityka.
- Akcje Search, Refresh i Load More muszą respektować aktualne rate limits.
- Ukryte lub nieudokumentowane endpointy GGG są poza zakresem projektu.
- Integracja Currency Exchange pozostaje odłożona i oddzielna od individual-item Trade Search/Fetch.
- Workflows real-money trading są poza zakresem projektu.

## 14. Otwarte Pytania Zgodności

- Dokładni zewnętrzni dostawcy fallback.
- Uprawnienia do użycia PoEDB lub innych danych third-party.
- Licencjonowanie assetów i ikon.
- Finalne scopes OAuth.
- Implementacja bezpiecznego przechowywania tokenów.
- Przyszłe wsparcie whisper.
- Czy istnieje oficjalnie wspierana integracja bezpośredniego Travel to Hideout.
- Jakakolwiek funkcjonalność obejmująca pliki gry lub wyodrębnione dane gry.

## 15. Wyzwalacze Review

Uruchom nowe review zgodności, gdy:

- Zmienią się polityki GGG.
- Zostanie wprowadzone nowe API.
- Moduł zacznie wysyłać input do gry.
- Zostanie dodany nowy zewnętrzny dostawca.
- Zmienią się scopes uwierzytelniania.
- Projekt zacznie zmierzać w stronę publicznej dystrybucji.
- Będzie rozważane wsparcie Path of Exile 2.
