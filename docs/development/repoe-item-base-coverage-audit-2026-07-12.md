# RePoE Item Base Coverage Audit - 2026-07-12

Source: local RePoE checkout at `local-data/RePoE`, commit `8023a1d696dbddc836c05ac3fcedd072da1767d2`.

Generated package: `artifacts/poenhance-game-data.json`, data version `repoe-8023a1d-20260712`.

Files inspected:

- `local-data/RePoE/RePoE/data/base_items.json`
- `local-data/RePoE/RePoE/data/item_classes.json`
- `local-data/RePoE/RePoE/data/mods.json`
- `local-data/RePoE/RePoE/data/stats.json`
- `local-data/RePoE/RePoE/data/stat_translations.json`
- `local-data/RePoE/RePoE/data/flavour.json`
- `local-data/full-data-smoke-build-report.txt`
- `artifacts/poenhance-game-data.json`

## Summary

The remaining live-game item-base misses below are not caused by `GameDataCatalog`, `ParsedItemBaseResolver`, or `ItemBaseClassCompatibility` losing imported data. The current RePoE source snapshot does not contain exact base item records for these live display names. The base-item importer skips only malformed base item records; in this snapshot all 420 skipped base item rows are unnamed `Metadata/Items/Currency/RandomFossilOutcome*` records and none match the audited names or search terms.

Placeholder records must not be silently generated. These examples need either updated source coverage in RePoE or a future explicit enrichment source, such as an official GGG data feed or a separately designed PoEDB enrichment importer.

## Findings

| Item | Raw `base_items.json` exact name | Equivalent or nearby RePoE record | Internal metadata ID without usable name | Other RePoE dataset presence | Generated package exact name | Resolver input/result | Root cause |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Organic Ring | No | None found by `Organic` or `Organic Ring` in base items | No | None found in inspected non-minified datasets | No | `BaseType=Organic Ring`; `Unknown` / `BASE_NOT_FOUND` | Missing source data |
| Necrotic Armour | No | None found by `Necrotic Armour` in base items | No | `Necrotic Footprints` mod/stat/translation records only; not an armour base | No | `BaseType=Necrotic Armour`; `Unknown` / `BASE_NOT_FOUND` | Missing source data |
| Tattoo of the Tasalio Shaman | No | None found by `Tattoo`, `Tasalio`, or `Shaman` in base items | No | `Tasalio` appears only in flavour text for an unrelated unique ring | No | Generic currency path attempted `DisplayName=Tattoo of the Tasalio Shaman`; `Unknown` / `BASE_NOT_FOUND` | Missing source data |
| Inscribed Ultimatum | No | `Metadata/Items/Maps/MapWorldsTrialmaster` exists as `Engraved Ultimatum`, `item_class=Map`; this is not an exact display-name match and not `MiscMapItem` | No | `Inscribed` appears as a ward modifier prefix; Ultimatum stats/mods exist for map or encounter mechanics | No | Generic currency path attempted `DisplayName=Inscribed Ultimatum`; `Unknown` / `BASE_NOT_FOUND` | Exact live display name absent; related `Engraved Ultimatum` would need an explicit alias/enrichment decision |
| Manifold Ring | No | None found by `Manifold` or `Manifold Ring` in base items | No | None found in inspected non-minified datasets | No | `BaseType=Manifold Ring`; `Unknown` / `BASE_NOT_FOUND` | Missing source data |
| Coin of Restoration | No | None found by `Coin of Restoration` in base items | No | `Restoration` appears in unrelated imprint text, ward translations, and modifier names | No | Generic currency path attempted `DisplayName=Coin of Restoration`; `Unknown` / `BASE_NOT_FOUND` | Missing source data |

## Price-Checking Impact

These are important for price checking because a copied item's exact base identity is one of the safest keys for trade lookup. When the generated package lacks a live base, the current resolver correctly refuses to invent an identity. The UI can still show parser-derived text, but catalog-backed base IDs remain unavailable.

Organic Ring, Necrotic Armour, Tattoo of the Tasalio Shaman, Manifold Ring, and Coin of Restoration are confirmed source-coverage gaps in the audited RePoE snapshot. Inscribed Ultimatum is a source/policy gap: RePoE contains `Engraved Ultimatum` as a map base, but not the live copied `Inscribed Ultimatum` display name under `Misc Map Items`.

## Future Decision

Do not weaken exact matching, add fuzzy matching, or create placeholder item bases. A future architecture decision should choose whether to:

- wait for RePoE to gain the missing live item bases;
- add an official-GGG source if one is available and locally importable;
- add a PoEDB enrichment source with explicit provenance and conflict rules;
- add a narrow alias/enrichment model for verified renamed item bases such as the `Inscribed Ultimatum` / `Engraved Ultimatum` case.

Any enrichment should remain explicit and provenance-backed so `ItemBaseRecord` does not silently mix guessed records with imported source records.
