# RePoE Skipped Record Audit - 2026-07-12

Source: local RePoE checkout at `local-data/RePoE`, branch `master`, commit `8023a1d696dbddc836c05ac3fcedd072da1767d2`.

Smoke build inputs:

- `local-data/RePoE/RePoE/data/base_items.json`
- `local-data/RePoE/RePoE/data/mods.json`
- `local-data/RePoE/RePoE/data/stats.json`
- `local-data/RePoE/RePoE/data/stat_translations.json`

Smoke build output:

- `artifacts/poenhance-game-data.json`
- data version `repoe-8023a1d-20260712`
- source version `8023a1d696dbddc836c05ac3fcedd072da1767d2`

## Import Summary

| Source | Read | Imported | Skipped |
| --- | ---: | ---: | ---: |
| ItemBases | 4,028 | 3,608 | 420 |
| Modifiers | 30,909 | 30,480 | 429 |
| Stats | 16,756 | 16,756 | 0 |
| StatTranslations | 8,871 | 8,857 | 14 |

Diagnostics: 863 warnings, 0 errors.

## Base Item Skips

All 420 skipped base item records are `Metadata/Items/Currency/RandomFossilOutcome*`. Authentic source fields have no usable `name`, so the current `ItemBaseRecord` importer cannot create meaningful item-base records. These appear to be generated/internal fossil outcome placeholders. They are intentionally unsupported by the current package schema and are not required for the price-checker MVP.

## Modifier Skip Rule

`RePoeModifierImporter` currently requires a usable id, object value, group/type, and at least one valid stat entry before constructing `ModifierDefinition`. A statless RePoE modifier is skipped with `REPOE_MODIFIER_RECORD_MISSING_STATS`.

This confirms that `ModifierDefinition` is intentionally limited to stat-bearing modifiers. The skipped records below are not good fits for `ModifierDefinition`; they represent behavior, markers, level gates, hidden map mechanics, disabled essence rows, or non-stat metadata. If future shared GameData consumers need them, add a separate model instead of forcing them into `ModifierDefinition`.

Common source DTO fields on skipped modifier rows:

- Always present: `adds_tags`, `domain`, `generation_type`, `generation_weights`, `grants_effects`, `groups`, `implicit_tags`, `is_essence_only`, `name`, `required_level`, `spawn_weights`, `stats`, `type`.
- Non-empty across skipped rows: `required_level` 429, `name` 192, `spawn_weights` 40, `grants_effects` 9, `adds_tags` 6, `is_essence_only` 2.
- `stats` is empty for all 429 skipped modifier rows.

## Skipped Modifier Categories

| Category | Count | Representative IDs | GenerationType | Domain | Source-field signal |
| --- | ---: | --- | --- | --- | --- |
| Daemon/effect-only records | 72 | `AvatarConvocationDaemon`, `ChestBarrelArchnemesisGreedDebuff`, `ChestBarrelExplodeVolatile`, `ShaperDaemon1` | `talisman`, `unique` | `chest`, `eldritch_altar`, `monster` | groups/types describe daemons, barrels, explosions, beacons, clouds, or display/effect rows; some have `spawn_weights default:0`; some have display `name` such as `Shaped`. |
| Tag or marker records | 6 | `BlightDoesntEngageNearbyEnemies`, `MonsterTagAncientVaal`, `MonsterTagExpedition`, `MonsterTagMirrored`, `MonsterTagSynthesised`, `PinnacleAtlasBoss` | `unique` | `monster` | non-empty `adds_tags`; no stats, spawn weights, or granted effects. Tags include `blight_doesnt_engage`, `corrupted_vaal`, `expedition_monster`, `mirrored_monster`, `synthesised_monster`, `pinnacle_boss`. |
| Level-requirement records | 23 | `FlaskLevelRequirement7`, `FlaskLevelRequirement10`, `FlaskLevelRequirement14Real_`, `FlaskLevelRequirement70`, `FlaskLevelRequirementUtilityResists` | `unique` | `flask` | `groups/type = FlaskLevelRequirement`, non-empty `required_level`, no stats or other useful payload. |
| Map or league mechanic behavior records | 188 | `EssenceBallLightningDaemon2`, `EssenceDaemonDelirium1`, `HellscapeDownsideMapArmourWhileInHellscape`, `HellscapeUpsideMapHellscapeRareMonstersBasicCurrencyItemDropChance`, `MapTierDifficulty1` | `essence`, `prefix`, `scourge_benefit`, `scourge_detriment`, `scourge_gimmick`, `torment`, `unique` | `area`, `item`, `monster`, `watchstone` | source fields point to map/scourge/essence/torment/watchstone behavior, map spawn weights, or area mechanics. Hellscape rows use `spawn_weights map:*|default:0`; `MapTierDifficulty*` rows use `domain=area`, `groups/type=MapTierDifficulty`. |
| Item-granted support or trigger behavior | 2 | `ItemActsAsAddedLightningDamageSupportUniqueBow18`, `ItemActsAsElementalFocusSupportUniqueBow18` | `unique` | `item` | non-empty `grants_effects`: `SupportAddedLightningDamage` level 15 and `SupportElementalFocus` level 15. No stats. |
| Legacy or disabled records | 2 | `LocalAddedPhysicalDamageEssence7`, `LocalAddedPhysicalDamageTwoHandEssence7` | `unique` | `item` | `groups/type = Deprecated`, `spawn_weights default:0`, `name=Essences`, `required_level=82`, `is_essence_only=true`. |
| Genuinely empty records | 57 | `ArenaChampionZombieDesecrateDaemon`, `BarrelofSpidersBossDaemon`, `BestiaryModRavenStormBoss`, `BetrayalUpgradeMonsterCorpseAcid` | `unique` | `chest`, `item`, `monster` | no stats, no name, no tags, no spawn weights, no grants; mostly only id, group/type, domain, generation type, and `required_level=1`. |
| Records containing useful non-stat metadata | 79 | `CleansingAltarDownsideMonsterSearingMonsters`, `GrantsFireChasmUnique__1`, `MonsterArchnemesisChargeGenerator__`, `MonsterArchnemesisConsecration_` | `bloodlines`, `monster_affliction`, `nemesis`, `prefix`, `suffix`, `talisman`, `unique` | `eldritch_altar`, `item`, `monster` | meaningful non-stat fields such as display `name`, `grants_effects`, or non-zero spawn weights like `default:2500`; still no stat payload. |

## Specifically Requested Records

- `LocalAddedPhysicalDamageEssence7` and `LocalAddedPhysicalDamageTwoHandEssence7`: legacy/disabled. Both have `groups/type=Deprecated`, `generation_type=unique`, `domain=item`, `spawn_weights default:0`, `required_level=82`, `name=Essences`, and `is_essence_only=true`. Not useful for current item parsing or price checking; possibly useful only to future legacy-data or browser tooling.
- `ItemActsAsAddedLightningDamageSupportUniqueBow18`: item-granted support behavior. `groups/type=DisplaySocketedGemsGetAddedLightningDamage`, `generation_type=unique`, `domain=item`, `required_level=1`, `grants_effects SupportAddedLightningDamage level 15`. Potentially relevant to future item-browser or build-analysis consumers, not current price checking.
- `ItemActsAsElementalFocusSupportUniqueBow18`: item-granted support behavior. `groups/type=DisplaySocketedGemsGetElementalFocus`, `generation_type=unique`, `domain=item`, `required_level=1`, `grants_effects SupportElementalFocus level 15`. Same future relevance as above.
- `FlaskLevelRequirement*`: 23 rows. `groups/type=FlaskLevelRequirement`, `generation_type=unique`, `domain=flask`, no stats, no tags, no spawn weights, populated `required_level`. This could matter to future item browser validation, but current item-base records already carry base requirements where needed.
- `MapTierDifficulty*`: 14 rows. `groups/type=MapTierDifficulty`, `generation_type=unique`, `domain=area`, `required_level=1`, no stats, tags, spawn weights, or grants. Potential future map-tool metadata, not current price checking.
- Hellscape map upside/downside rows: scourge map behavior records. Upsides use `generation_type=scourge_benefit`; downsides use `generation_type=scourge_detriment`; gimmicks use `generation_type=scourge_gimmick`. Domain is `item` for map modifiers or `area` for gimmicks. Many have `spawn_weights map:*|default:0`. They are relevant to future map tools or a mechanic-browser model, not to the current stat-bearing modifier package.

## Relevance Assessment

| Category | Item parsing | Price checking | Crafting/browser tools | Map tools | Future shared GameData |
| --- | --- | --- | --- | --- | --- |
| Daemon/effect-only | Low | Low | Medium for effect browsers | Low to medium | Separate behavior/effect model if needed |
| Tag or marker | Low to medium for monster context | Low | Medium | Medium | Separate tag/marker model |
| Level requirement | Medium for flask/item browser validation | Low | Medium | Low | Separate requirement/gate model |
| Map or league mechanic behavior | Low | Low | Medium | High | Separate map/mechanic behavior model |
| Item-granted support/trigger | Medium | Low for MVP, medium if pricing pseudo-effects later | High | Low | Separate granted-effect model |
| Legacy or disabled | Low | Low | Low, except historical browser modes | Low | Probably excluded or explicit legacy model |
| Genuinely empty | Low | Low | Low | Low | No current model needed |
| Useful non-stat metadata | Low to medium | Low | Medium | Medium | Separate metadata/behavior model |

## Translation Skips

All 13 unknown stat ids are present in `stat_translations.json`, hidden, absent from `stats.json`, and not referenced by the inspected alias fields in `stats.json`. No placeholder stats or translations should be created.

| StatId | Status |
| --- | --- |
| `buff_time_passed_-%` | Hidden translation, absent from `stats.json`, no alias. Fuzzy related ids exist for `buff_time_passed_+%`, so this looks like a legacy/inverse stat name. |
| `desecrate_on_block_%_life_regen_per_minute` | Hidden translation, absent from `stats.json`, no alias. |
| `is_cursed` | Hidden `ignore` translation, absent from `stats.json`, no alias. |
| `map_display_two_bosses` | Hidden `ignore` translation, absent from `stats.json`, no alias. Potentially map-display legacy metadata. |
| `map_ignore_extra_monster_rarity_bias` | Hidden `ignore` translation, absent from `stats.json`, no alias. Potentially map mechanic metadata. |
| `map_override_extra_monster_min_level` | Hidden translation, absent from `stats.json`, no alias. Potentially map mechanic metadata. |
| `max_corrupted_blood_rain_stacks` | Hidden translation, absent from `stats.json`, no alias. |
| `max_corrupted_blood_stacks` | Hidden translation, absent from `stats.json`, no alias. |
| `maximum_dodge_chance_%` | Hidden translation, absent from `stats.json`, no alias. Likely legacy after dodge removal. |
| `maximum_spell_dodge_chance_%` | Hidden translation, absent from `stats.json`, no alias. Likely legacy after spell dodge removal. |
| `monster_drop_additional_rare_items` | Hidden translation, absent from `stats.json`, no alias. |
| `monster_ground_poison_on_death_base_area_of_effect_radius` | Hidden translation, absent from `stats.json`, no alias. |
| `movement_velocity_+%_per_ten_levels` | Hidden translation, absent from `stats.json`, no alias. |

`dummy_stat_display_nothing` is the single invalid-format translation skip. The source entry is hidden, has `ids=["dummy_stat_display_nothing"]`, `format=["ignore"]`, an empty `string`, and an empty index handler. It intentionally displays nothing and should stay unsupported.

## Decision

The generated package remains suitable for the current price-checker MVP because the MVP needs item bases, stat-bearing modifiers, stat definitions, and valid stat translations. The skipped records do not remove stat-bearing modifier data or stat definitions used by the package.

The generated package is not lossless enough for all future shared GameData use. Future consumers that need map mechanics, monster markers, granted effects, flask requirement gates, hidden behavior rows, or legacy browsing should get separate provider-neutral models. Do not expand `ModifierDefinition` with statless rows; preserve it as the stat-bearing modifier model.

Future architectural decisions:

- Whether to model statless behavior rows as `MechanicDefinition`, `GrantedEffectDefinition`, `MapMechanicDefinition`, or another separate concept.
- Whether hidden translation rows for absent stats should be retained as archival metadata in a future lossless source package.
- Whether generated/internal base item placeholders such as `RandomFossilOutcome*` should remain excluded or move to a future raw-source mirror.
