# Game Data Source Audit - 2026-07-13

## Scope

This audit answers whether the obsolete `brather1ng/RePoE` source can be replaced by the actively maintained `repoe-fork/repoe` source without redesigning the importer.

No importer code was changed. No multi-source merging, PoB importer, PoEWiki importer, PoEDB scraping, override system, UI change, update workflow, or `Ctrl+D` behavior change was implemented.

## Baseline

Baseline was taken from a clean working tree on `main`.

| Check | Result |
| --- | --- |
| `git status --short --branch` | `## main...origin/main` |
| `dotnet build` | Passed, 0 warnings, 0 errors |
| `dotnet test` | Passed, 239 tests |

## Current Source

Current source:

- Repository URL: `https://github.com/brather1ng/RePoE.git`
- Branch: `master`
- SHA: `8023a1d696dbddc836c05ac3fcedd072da1767d2`
- Commit date: `2022-09-06T21:43:56+02:00`
- Local checkout: `local-data/RePoE`
- Consumed source files:
  - `local-data/RePoE/RePoE/data/base_items.json`
  - `local-data/RePoE/RePoE/data/mods.json`
  - `local-data/RePoE/RePoE/data/stats.json`
  - `local-data/RePoE/RePoE/data/stat_translations.json`

Current generated package:

- Package: `artifacts/poenhance-game-data.json`
- Control rebuild output: `artifacts/source-audit/old-control-package.json`
- DataTool control rebuild exit code: `0 (Success)`
- SHA256 of control rebuild: `5fede310e40dda0739618af5c0e001fe39ed2a704d49a632c987ad36143a693e`

| Section | Source read | Imported | Skipped | Final package count |
| --- | ---: | ---: | ---: | ---: |
| ItemBases | 4,028 | 3,608 | 420 | 3,608 |
| Modifiers | 30,909 | 30,480 | 429 | 30,480 |
| Stats | 16,756 | 16,756 | 0 | 16,756 |
| StatTranslations | 8,871 | 8,857 | 14 | 8,857 |

Current package duplicate IDs: none in `itemBases`, `modifiers`, `stats`, or `statTranslations`.

## Active Fork Source

Active fork:

- Repository URL: `https://github.com/repoe-fork/repoe`
- Branch: `master`
- HEAD SHA: `c50acab2ed660a70511e7f91ee09db4e632089e4`
- Latest commit date: `2026-06-30T13:59:25+07:00`
- Latest commit subject: `tolerate errors in world areas poe1`
- License: MIT for the repository code. `LICENSE.md` also states generated `data` contents belong to Grinding Gear Games and must be used or published in accordance with their terms.
- Generated-data location when generated locally: `./RePoE/data/`
- Important repo detail: the active fork does not commit `RePoE/data/`; the checkout contains schemas/docs/parser code, not generated data files.
- Hosted export index: `https://repoe-fork.github.io/poe1.html`
- Hosted export version shown by the index: `PoE version 3.28.0.14.3`

Relevant available files:

- Repository schemas: `RePoE/schema/base_items.json`, `mods.json`, `stats.json`, `stat_translations.json`, `item_classes.json`, `tags.json`
- Hosted PoE1 export files used for compatibility testing:
  - `https://repoe-fork.github.io/base_items.json`
  - `https://repoe-fork.github.io/mods.json`
  - `https://repoe-fork.github.io/stats.json`
  - `https://repoe-fork.github.io/stat_translations.json`
  - `https://repoe-fork.github.io/item_classes.json`
  - `https://repoe-fork.github.io/tags.json`

Temporary inputs were stored only under ignored `artifacts/source-audit/`.

## Source Record Comparison

| File | Old records | Active records | Added | Removed |
| --- | ---: | ---: | ---: | ---: |
| `base_items.json` | 4,028 | 5,059 | 1,034 by metadata ID | 3 by metadata ID |
| `mods.json` | 30,909 | 39,292 | 8,464 by mod ID | 81 by mod ID |
| `stats.json` | 16,756 | 22,774 | 6,229 by stat ID | 211 by stat ID |
| `stat_translations.json` | 8,871 | 11,076 | 2,336 by ordered `ids` tuple | 131 by ordered `ids` tuple |
| `item_classes.json` | 83 | 99 | 16 by item-class ID | 0 |
| `tags.json` | 1,014 | 1,355 | 346 tag values | 5 tag values |

Active export duplicate IDs/values: none found in top-level `base_items`, `mods`, `stats`, `item_classes`, `tags`, or ordered `stat_translations.ids` tuples.

Representative new active-fork bases only by metadata ID:

| Name | ID | Item class | Domain |
| --- | --- | --- | --- |
| A Chilling Wind | `Metadata/Items/DivinationCards/DivinationCardAChillingWind` | DivinationCard | undefined |
| A Dusty Memory | `Metadata/Items/DivinationCards/DivinationCardADustyMemory` | DivinationCard | undefined |
| Abomination Map | `Metadata/Items/Maps/MapWorldsAbomination` | Map | area |
| Abyss Scarab | `Metadata/Items/Scarabs/ScarabAbyssNew1` | MapFragment | undefined |
| Abyss Scarab of Descending | `Metadata/Items/Scarabs/ScarabAbyssNew3` | MapFragment | undefined |
| Abyss Scarab of Edifice | `Metadata/Items/Scarabs/ScarabAbyssNew4` | MapFragment | undefined |
| Abyss Scarab of Multitudes | `Metadata/Items/Scarabs/ScarabAbyssNew2` | MapFragment | undefined |
| Abyss Scarab of Profound Depth | `Metadata/Items/Scarabs/ScarabAbyssNew5` | MapFragment | undefined |
| Adherent of Zarokh | `Metadata/Items/ItemisedCorpses/FaridunAstralAcolyteMid` | ItemisedCorpse | undefined |
| Aegis Tulgraft | `Metadata/Items/Chayula/TulGraft4` | RemovedItem | brequel_graft |
| Agility Contract | `Metadata/Items/TradeProxy/AgilityHeistContract` | InstanceLocalItem | undefined |
| Al-Hezmin's Citadel Map | `Metadata/Items/TradeProxy/HunterCitadelMap` | InstanceLocalItem | undefined |
| Alivia's Grace | `Metadata/Items/DivinationCards/DivinationCardAliviasGrace` | DivinationCard | undefined |
| Allflame Ember of Abyss | `Metadata/Items/NecropolisPacks/AbyssNecropolisPack` | NecropolisPack | undefined |
| Allflame Ember of Anarchy | `Metadata/Items/NecropolisPacks/RogueExiles` | NecropolisPack | undefined |
| Allflame Ember of Arohongui | `Metadata/Items/NecropolisPacks/ArohonguiNecropolisPack` | NecropolisPack | undefined |
| Allflame Ember of Artillery Gemlings | `Metadata/Items/NecropolisPacks/GemlingCold2NecropolisPack` | NecropolisPack | undefined |
| Allflame Ember of Beidat | `Metadata/Items/NecropolisPacks/ScourgePaleNecropolisPack` | NecropolisPack | undefined |
| Allflame Ember of Berserking Gemlings | `Metadata/Items/NecropolisPacks/GemlingFire2NecropolisPack` | NecropolisPack | undefined |
| Allflame Ember of Brawling Gemlings | `Metadata/Items/NecropolisPacks/GemlingFireNecropolisPack` | NecropolisPack | undefined |

## Schema Comparison

### `base_items.json`

Root shape remains an object keyed by metadata ID.

| Category | Result |
| --- | --- |
| Identical fields relevant to current importer | `name`, `item_class`, `requirements.level`, `domain`, `tags` |
| Added fields | `inherits_from`, `skills_granted` |
| Removed fields | None observed |
| Changed types | No importer-consumed type changes observed |
| Changed nullability | Active export explicitly emits many optional nested properties as `null`, especially inside `properties`, `grants_buff`, and `skills_granted`; current importer ignores those fields |
| Changed string values | Domains added: `affliction_charm`, `brequel_graft`, `map_relic`, `memory_lines`, `sanctum_relic`, `templar_relic`, `tincture`; domain `memory` removed. Item classes added include `AnimalCharm`, `AtlasRelic`, `Breachstone`, `BrequelFruit`, `Gold`, `InstanceLocalItem`, `ItemisedCorpse`, `MapKey`, `NecropolisPack`, `Relic`, `RemovedItem`, `SanctumSpecialRelic`, `Tincture`, `VaultKey` |
| Ordering semantics | No meaningful root ordering; importer sorts imported records by ID. `tags` order is not semantically used by PoEnhance and importer sorts tags |

Compatibility: base item import itself still works. Active run read 5,059 records, imported 4,639, and skipped the same 420 unnamed `RandomFossilOutcome*` pattern.

### `mods.json`

Root shape remains an object keyed by modifier ID.

| Category | Result |
| --- | --- |
| Identical fields relevant to current importer | `groups`, `type`, `stats[].id`, `stats[].min`, `stats[].max`, `name`, `generation_type`, `required_level`, `domain`, `adds_tags`, `implicit_tags`, `spawn_weights[].tag`, `spawn_weights[].weight` |
| Added fields | `gold_value`, `text` |
| Removed fields | None observed |
| Changed types | No importer-consumed type changes observed; `stats[].min/max` remain numeric integers in the active export |
| Changed nullability | Optional new fields may be absent or null; current importer ignores them |
| Changed string values | Domains added: `affliction_charm`, `brequel_graft`, `crucible_remnant`, `map_relic`, `memory_lines`, `necropolis_monster`, `primordial_altar`, `sanctum_relic`, `templar_relic`, `tincture`; domains removed: `eldritch_altar`, `memory`. Generation types added include `azmeri_empowered_monster`, `crucible_tree`, `crucible_unique_tree`, `eater_of_worlds_implicit`, `memory_altar`, `necropolis_devoted_monster`, `necropolis_monster`, `searing_exarch_implicit`; removed values include `archnemesis`, `eater_implicit`, `exarch_implicit` |
| Ordering semantics | `spawn_weights` remains order-sensitive for eligibility. Current importer preserves spawn-weight order. Root records are sorted by ID in the package |

Compatibility: modifier import itself still mostly works. Active run read 39,292 records, imported 38,800, and skipped 492 statless modifier records.

### `stats.json`

Root shape remains an object keyed by stat ID, but nullable alias representation changed.

| Category | Result |
| --- | --- |
| Identical top-level fields | `alias`, `is_aliased`, `is_local` |
| Added fields | None |
| Removed fields | None |
| Changed types | `alias.when_in_main_hand` and `alias.when_in_off_hand` are now present with `null` for non-aliased stats; old data used an empty `alias` object for those records |
| Changed nullability | Active export has 45,214 null alias values and 334 string alias values. Current importer treats null alias entries as unsupported and skips the whole stat |
| Changed string values | Stat ID set expanded substantially: 6,229 added and 211 removed |
| Ordering semantics | No meaningful root ordering; importer sorts imported stats by ID |

First precise incompatibility: record `%_chance_to_blind_on_critical_strike` has:

```json
"alias": {
  "when_in_off_hand": null,
  "when_in_main_hand": null
}
```

`RePoeStatsImporter.TryReadAliases` currently accepts only `when_in_main_hand` / `when_in_off_hand` entries whose values are non-empty strings. It rejects the null values and skips the stat. This produced `REPOE_STAT_RECORD_INVALID_ALIAS count=22617`.

### `stat_translations.json`

Root shape remains an array of translation objects.

| Category | Result |
| --- | --- |
| Identical fields relevant to current importer | `ids`, `English`, `English[].condition`, `English[].format`, `English[].index_handlers`, `English[].string` |
| Added fields | `trade_stats`, `French`, `German`, `Japanese`, `Korean`, `Portuguese`, `Russian`, `Spanish`, `Thai`, `Traditional Chinese`; active `English[]` variants also include `reminder_text`, `is_markup` |
| Removed fields | None observed |
| Changed types | No required current-importer field moved to another container |
| Changed nullability | Active export explicitly emits `condition.min`, `condition.max`, `condition.negated`, `hidden`, language blocks, `trade_stats`, `reminder_text`, and `is_markup` as null when absent. Old export mostly omitted absent condition fields |
| Changed enum/string values | `format` values remain `#`, `+#`, and `ignore`; active export uses more rows and more explicit nullable metadata |
| Ordering semantics | Translation variant ordering remains meaningful because first matching condition wins. Current importer preserves variant order within a translation record |

Discovered incompatibility: `RePoeStatTranslationsImporter.TryReadDecimal` accepts absent `min`/`max`, but rejects explicit `null`. Active translations commonly represent open condition bounds as `null`, for example `max: null` and `negated: null`. This produced `REPOE_STAT_TRANSLATION_INVALID_CONDITION count=125` in the unchanged run. Most translation loss was cascading from skipped stats: `REPOE_STAT_TRANSLATION_UNKNOWN_STAT_ID count=10948`.

### `item_classes.json`

Root shape remains an object keyed by item-class ID.

| Category | Result |
| --- | --- |
| Identical fields | `name` |
| Added fields | `category`, `category_id`, `influence_tags` |
| Removed fields | None observed |
| Changed types/nullability | `influence_tags` is explicitly null for many records; not consumed by PoEnhance today |
| Changed string values | 16 item classes added, none removed |
| Ordering semantics | No meaningful root ordering |

Compatibility: not consumed by the current DataTool package build.

### `tags.json`

Root shape remains an array of strings.

| Category | Result |
| --- | --- |
| Identical fields | Not applicable; root is an array |
| Added values | 346 tags |
| Removed values | 5 tags |
| Changed types/nullability | No type change observed; values are strings |
| Ordering semantics | No PoEnhance importer dependency today |

Compatibility: not consumed by the current DataTool package build.

## Existing Importer Compatibility

Command:

```powershell
dotnet run --project .\PoEnhance.DataTool -- build-package `
  --base-items .\artifacts\source-audit\active-poe1\base_items.json `
  --mods .\artifacts\source-audit\active-poe1\mods.json `
  --stats .\artifacts\source-audit\active-poe1\stats.json `
  --translations .\artifacts\source-audit\active-poe1\stat_translations.json `
  --output .\artifacts\source-audit\active-poe1-package.json `
  --data-version repoe-fork-c50acab-20260713 `
  --source-version c50acab2ed660a70511e7f91ee09db4e632089e4
```

Outcome: B. Existing importer fails unchanged.

No active-fork candidate package was produced.

| Section | Source read | Imported | Skipped |
| --- | ---: | ---: | ---: |
| ItemBases | 5,059 | 4,639 | 420 |
| Modifiers | 39,292 | 38,800 | 492 |
| Stats | 22,774 | 157 | 22,617 |
| StatTranslations | 11,076 | 3 | 11,073 |

Grouped diagnostics from the unchanged run:

| Severity | Code | Count | Meaning |
| --- | --- | ---: | --- |
| Warning | `REPOE_RECORD_MISSING_NAME` | 420 | Same unnamed `RandomFossilOutcome*` base-item pattern as old source |
| Warning | `REPOE_MODIFIER_RECORD_MISSING_STATS` | 492 | Statless modifiers skipped |
| Warning | `REPOE_STAT_RECORD_INVALID_ALIAS` | 22,617 | Active stats use nullable alias entries; importer expects only non-empty strings when alias entries exist |
| Warning | `REPOE_STAT_TRANSLATION_INVALID_CONDITION` | 125 | Active translations use explicit null bounds/flags in conditions |
| Warning | `REPOE_STAT_TRANSLATION_UNKNOWN_STAT_ID` | 10,948 | Cascades mainly from skipped stats |
| Error | `PACKAGE_STAT_ALIAS_REFERENCE_MISSING` | 314 | Cascades from skipped alias target stats |
| Error | `PACKAGE_MODIFIER_STAT_REFERENCE_MISSING` | 55,810 | Cascades from skipped stats |
| Error | `PACKAGE_VALIDATION_FAILED` | 56,124 | Package validator rejects modifier stat references not present in imported stats |

The first precise incompatibility is the active `stats.alias` null representation. The second discovered narrow incompatibility is explicit null condition bounds in `stat_translations.json`.

## Known Base Coverage

Because the active DataTool run failed validation, active coverage below is source-level, not candidate-package-level. "Resolver would resolve" means whether the record shape is enough for the existing exact base resolver after a narrowly adapted importer produces a validated package; it does not imply a package exists today.

| Name | Old source exact presence | Old package presence | Active source exact presence | Active active-fork ID | Item class | Domain | Tags | Resolver would resolve |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Organic Ring | No | No | Yes | `Metadata/Items/Rings/RingB4` | Ring | item | `not_for_sale`, `ring`, `default` | Yes for parsed Ring base text once imported |
| Necrotic Armour | No | No | Yes | `Metadata/Items/Armours/BodyArmours/BodyDexInt20` | Body Armour | item | `dex_int_armour`, `top_tier_base_item_type`, `not_for_sale`, `body_armour`, `armour`, `default` | Yes for parsed Body Armour base text once imported |
| Tattoo of the Tasalio Shaman | No | No | Yes | `Metadata/Items/Currency/AncestralTattooTasalio3` | StackableCurrency | undefined | `int_tattoo`, `currency`, `default` | Yes for generic/currency-style display-name lookup once imported |
| Manifold Ring | No | No | Yes | `Metadata/Items/Rings/RingE4` | Ring | item | `not_for_sale`, `experimental_base`, `ring`, `default` | Yes for parsed Ring base text once imported |
| Coin of Restoration | No | No | Yes | `Metadata/Items/Currency/CurrencyWishConvertUnique` | StackableCurrency | undefined | `currency`, `default` | Yes for generic/currency-style display-name lookup once imported |
| Inscribed Ultimatum | No exact match; `Engraved Ultimatum` exists as `Metadata/Items/Maps/MapWorldsTrialmaster` | No | No exact match; `Engraved Ultimatum` still exists | None for exact display name | Not applicable | Not applicable | Not applicable | No for exact `Inscribed Ultimatum`; would still require explicit alias/enrichment policy |
| Titan Plate | No | No | Yes | `Metadata/Items/Armours/BodyArmours/BodyStr18` | Body Armour | item | `str_armour`, `top_tier_base_item_type`, `not_for_sale`, `body_armour`, `armour`, `default` | Yes for parsed Body Armour base text once imported |
| Card Belt | No | No | No | None | Not applicable | Not applicable | Not applicable | No |

The active fork substantially improves base coverage for the audited live/suspicious names, but does not fully close the list.

## Decision

The active fork cannot replace the obsolete RePoE source without code changes, because the current importer fails unchanged on narrow schema/nullability drift.

The failure does not indicate that active fork coverage is insufficient or that a multi-source architecture is needed now. Base item import succeeds, active source coverage is materially better, and the incompatibilities are localized to current parsing assumptions:

- allow null `stats.alias.when_in_main_hand` / `when_in_off_hand` as equivalent to absent alias;
- allow null `stat_translations.condition.min` / `max` / `negated` as equivalent to absent open bounds / false negation;
- then rerun package validation before making the active fork primary.

## Verification

Final verification for this audit:

- `git diff --check`: Passed.
- `dotnet build`: Passed, 0 warnings, 0 errors.
- `dotnet test`: Passed, 239 tests.
- `git status --short --branch`: `## main...origin/main` plus this untracked report file.

Recommendation: Adapt importer narrowly, then replace old RePoE.
