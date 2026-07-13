# Current Milestone Close-out Audit - 2026-07-13

## Milestone

Active milestone identified from `docs/ROADMAP.md`: **Milestone 2 - Shared Data Core Foundation**.

This audit also re-checks Milestone 1 runtime behavior because Milestone 2 now feeds the prototype app through runtime package loading, parser enrichment, and development diagnostics.

## Final decision

**Complete.**

The current codebase satisfies the game-data, parser, runtime loading, item-base resolution, modifier-resolution, Advanced roll-annotation, diagnostics, provisional-record storage, tests, and artifact hygiene baseline. The final missing Milestone 2 requirement is now implemented as a minimal local provider-neutral provisional game-data record model and JSON store.

## Acceptance matrix

| Criterion | Status | Evidence |
| --- | --- | --- |
| WPF app starts | Complete | `dotnet run --project .\PoEnhance.App\PoEnhance.App.csproj --no-build` stayed alive for a 5-second smoke run and was stopped. |
| Background app behavior | Complete with documented limitation | Main window starts normally, polls PoE status on a `DispatcherTimer`, unregisters hotkeys on close, and does not require PoE to be running. No tray/minimize-to-tray production behavior is in this milestone. |
| PoE process detection | Complete | `PathOfExileProcessDetector` scans running processes and delegates matching to `PathOfExileProcessMatcher`. |
| PoE foreground detection | Complete | `PathOfExileForegroundWindowDetector` resolves the foreground window process and matches it as a PoE game process. |
| Ctrl+D activation only while PoE is foreground | Complete | Default shortcut is `Ctrl+D`; `GlobalHotkeyService` registers only after `UpdatePathOfExileForegroundState(true)` and unregisters when foreground is lost. |
| Advanced clipboard capture | Complete with documented limitation | Capture sends `Ctrl+Alt+C`, checks PoE foreground before send, during wait, and before read, then reads clipboard text. Real game/manual verification remains required. |
| Manual pasted-item input | Complete | `ManualItemInputTextBox` plus Parse/Clear buttons route text through the same parser and game-data enrichment path. |
| Parser operation | Complete | Parser handles item headers, properties, influences, Advanced modifier metadata, modifier buckets, notes, states, enchantments, flavour text, and unknown lines; covered by parser tests. |
| Parser-only fallback | Complete | Runtime game-data service can return NotConfigured/Failed; display service reports "Game data not loaded" and does not call resolvers when catalog is null. |
| Validated GameData loading | Complete | Runtime service resolves `--game-data`, `POENHANCE_GAMEDATA_PATH`, or development fallback, loads through `GameDataPackageLoader`, and constructs one session catalog only after successful package load. |
| Active source metadata | Complete | Local artifact manifest reports `sourceUri=https://github.com/repoe-fork/repoe`, `sourceVersion=c50acab2ed660a70511e7f91ee09db4e632089e4`, data version `repoe-fork-c50acab-20260713`, league `Mercenaries`, patch `3.28.0.14.3`. |
| Current package record counts | Complete | Runtime/doc baseline is 4,639 item bases, 38,800 modifiers, 22,774 stats, and 11,060 stat translations. |
| Exact/probable/unknown item-base resolution | Complete with documented limitation | Core resolver returns exact, probable, or unknown results conservatively and preserves diagnostics. Generic or ambiguous cases remain unknown by design. |
| Conservative modifier resolution pipeline | Complete with documented limitation | Pipeline is `name -> generation kind -> base/influence eligibility -> stat-text signature`; it avoids value, tier, Trade stat, and fuzzy certainty. |
| Traditional influence eligibility | Complete with documented limitation | Traditional influence labels derive dynamic eligibility tags without mutating item-base records. Eldritch influence is intentionally excluded from this traditional affix path. |
| Real Advanced roll-range support | Complete | `ModifierTextSignatureNormalizer` strips real Advanced roll annotations such as value(range) before numeric placeholder matching; real Advanced bow, jewel, armor, and Redeemer cases are covered by tests. |
| Dev diagnostics | Complete | UI displays game-data state, path, source version, record counts, item-base resolution diagnostics, modifier candidate counts, and candidate labels. |
| Tests | Complete | Final test run passed 322 tests across GameData, Core, DataImport, App, and DataTool. |
| Docs consistency | Complete | README, decisions, roadmap, and this close-out audit describe the active game-data source, conservative resolver limitations, and local provisional-record behavior. |
| Generated artifacts ignored by Git | Complete | `.gitignore` covers `artifacts/`, `local-data/`, and `data/repoe/`; `git check-ignore` confirmed representative generated/source-data paths are ignored. |
| Shared data models usable outside price checker | Complete | `PoEnhance.GameData` is an independent provider-neutral library, used by Core/App without WPF coupling. |
| Unknown/missing catalog data does not break parsing/display | Complete | Null or failed catalog state leaves parser behavior available and displays unavailable diagnostics instead of throwing. |
| Data package boundaries clear enough for importer/runtime work | Complete | Provider-specific import is in `PoEnhance.DataImport`; runtime package loading/catalogs are in `PoEnhance.GameData`; app owns path resolution and catalog lifetime. |
| Provisional records can be represented and stored locally | Complete | `PoEnhance.GameData` defines provider-neutral provisional record/snapshot models. `PoEnhance.App` stores them in deterministic local JSON at `%LOCALAPPDATA%\PoEnhance\provisional-game-data.json`, deduplicated by stable key with first/last seen timestamps and seen count. |

## Evidence

- Initial baseline: `git status --short --branch` returned `## main...origin/main`.
- Initial baseline: `dotnet test` passed 296 tests.
- Initial baseline: `dotnet build` succeeded. The first build/test were accidentally run concurrently and produced transient file-copy retry warnings from locked testhost outputs; they still succeeded. Final verification should be read from the verification section below.
- App smoke: `dotnet run --project .\PoEnhance.App\PoEnhance.App.csproj --no-build` stayed alive for 5 seconds.
- Artifact hygiene: `git check-ignore -v` confirmed ignored generated paths under `artifacts/`, `local-data/RePoE/RePoE/data/`, and `data/repoe/`.
- Source metadata: `artifacts/poenhance-game-data.json` manifest identifies the active `repoe-fork/repoe` source and SHA listed above.
- Provisional model: records preserve schema version, stable key, record kind, normalized/original unresolved identity, item class, modifier kind/generation kind when relevant, source, first/last seen UTC timestamps, seen count, league or patch context, confidence, and concise discovery context.
- Recording policy: item bases are recorded only for parsed base types with `BASE_NOT_FOUND`; modifiers are recorded only for authentic Advanced modifier names with supported generation kind and zero catalog name candidates. Ambiguous, exact, probable, unsupported, non-Advanced, unique-placeholder, parser-failure, value/tier, and translation-unknown cases are not recorded.
- Store behavior: local JSON writes use a temporary file followed by replacement, one writer at a time, deterministic stable-key ordering, read-only snapshots, and non-throwing failure results. Malformed existing JSON is not overwritten.
- Development visibility: the App shows provisional store record count, file path, and last diagnostic in the Game Data panel.
- Final verification: `dotnet test` passed 322 tests.

## Known limitations

- Provisional records are intentionally minimal and only capture high-confidence unresolved identities; they are not a second canonical package and are not automatically promoted into trusted game data.
- Provisional record review, enrichment, conflict resolution, deletion/editing UI, and promotion workflow remain deferred.
- Runtime package activation, rollback UI, automatic updates, and admin update UI are deferred.
- Modifier resolution is intentionally conservative: no value/range matching, tier inference, fuzzy matching, localized text, Trade stat mapping, or statless behavior model.
- Item-base resolution intentionally returns unknown for ambiguous/generic cases instead of guessing.
- Advanced capture still needs a real-client manual pass because automated tests cannot validate the live game foreground and clipboard behavior.
- Tray/background polish is not production complete.

## Manual checklist

- Start app with no game-data package and confirm parser-only manual input still works.
- Start app with `artifacts/poenhance-game-data.json` or `--game-data <path>` and confirm loaded state, source version, and record counts are visible.
- With Path of Exile foreground, press `Ctrl+D` on an item and confirm the app sends Advanced copy, captures text, parses item fields, and displays item-base/modifier diagnostics.
- With another app foreground, press `Ctrl+D` and confirm no PoE capture path runs.
- Test a regular non-Advanced copy and confirm modifier identity remains conservative/unknown where Advanced metadata is unavailable.
- Test at least one influenced rare item and confirm traditional influence candidate diagnostics are visible.
- Confirm generated artifacts remain untracked after package rebuilds.

## Recommended next action

Milestone 2 is closed. The next roadmap milestone is **Milestone 3 - Basic Trade Price Checker**. The first task should be defining the local Trade query/request model and validation boundary so price-checker work can consume parser/game-data output without adding network behavior prematurely.
