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
