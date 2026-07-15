# Price Checker modifier-bound audit (2026-07-15)

## Scope and evidence

The audit used the checked-in production snapshots:

- `artifacts/poenhance-game-data.json` for modifier identities, stat groups, tags, roll ranges, and translation branches;
- `artifacts/source-audit/active-poe1/stat_translations.json` for English translation forms and their provider stat mappings.

Only English translation forms with at least one non-null Trade stat mapping are included in the mutually exclusive provider-searchable totals below. The broader handler inventory also includes production GameData forms without a Trade mapping so an unknown transform cannot become a raw-number fallback later.

## Official added-damage projection

Official endpoints inspected:

- `GET https://www.pathofexile.com/api/trade/data/stats`
- `POST https://www.pathofexile.com/api/trade/search/Mirage`

The current official stat catalog represents local added Cold Damage with a two-placeholder template (`Adds # to # Cold Damage (Local)`) but accepts one scalar `min`/`max` object in the query filter. A live official listing with `Adds 42 to 64 Cold Damage` was used as a boundary probe:

- `min: 52.99` included the listing;
- `min: 53` included the listing;
- `min: 53.01` excluded the listing.

This proves the official scalar is the arithmetic mean, `(low + high) / 2`, because `(42 + 64) / 2 = 53`. The same projection is enabled generically only when all of these are true:

1. the selected GameData branch has exactly two numeric values;
2. its stat identities form a generic minimum/maximum pair with the same stem;
3. the resolved modifier is tagged as damage;
4. both translation handler sequences are identity;
5. the exact resolved provider stat independently exposes two numeric placeholders.

No element, modifier, base, item, or provider stat ID participates in that decision. For `Adds 14(11-15) to 25(23-26) Cold Damage`, the observed tuple is `(14, 25)` and the default provider minimum is `19.5`. The parenthesized tier ranges are removed before numeric arity is determined.

## Provider-searchable classification

| Classification | Forms | Bound behavior |
|---|---:|---|
| Scalar | 7,357 | One rendered numeric value; supported when every handler has proven ordering semantics. |
| Projectable numeric tuple | 174 | Two-value damage minimum/maximum family; arithmetic-mean projection after exact provider arity confirmation. |
| Presence/option-only | 2,563 | Selectable provider form, but no copied numeric value from which to invent a bound. |
| Ambiguous/unsupported | 33 | Unknown semantic handler or multi-value projection not proven by the provider mapping. |
| **Total** | **10,127** | |

Independent logical components are counted as their child scalar/range/presence forms above, rather than double-counted as another translation form. A separate source-definition pass found 4,257 production modifier definitions containing two or more distinct tradable translation groups (3,730 with two groups, 336 with three, 157 with four, 29 with five, and 5 with six). The existing mapper retains these as independently searchable components when the copied modifier supplies separate rendered lines.

## Numeric arity

| Rendered numeric arity | Forms |
|---:|---:|
| 0 | 2,563 |
| 1 | 7,367 |
| 2 | 195 |
| 7 | 2 |
| **Total** | **10,127** |

The 195 two-value forms divide into 174 proven damage means and 21 unsupported tuples. Ten one-value forms use semantic lookup handlers rather than decimal transforms. Both seven-value forms remain unsupported.

## Translation handler audit

Scalar clipboard numbers have already been rendered through the selected GameData handler sequence. Applying the magnitude transform again would double-transform them. The pipeline therefore retains the exact branch and handler sequence, uses the displayed decimal as the provider magnitude, and uses the handler sequence to prove ordering and choose the default direction.

Supported order-preserving handler sequences (identity is the empty sequence):

- `30%_of_value`, `60%_of_value`
- `deciseconds_to_seconds`
- `divide_by_fifteen_0dp`, `divide_by_five`, `divide_by_four`
- `divide_by_one_hundred`, `divide_by_one_hundred_2dp`, `divide_by_one_hundred_2dp_if_required`
- `divide_by_one_thousand`, `divide_by_six`
- `divide_by_ten_0dp`, `divide_by_ten_1dp`, `divide_by_ten_1dp_if_required`
- `divide_by_three`, `divide_by_twelve`, `divide_by_twenty`, `divide_by_twenty_then_double_0dp`, `divide_by_two_0dp`
- `double`, `locations_to_metres`
- `milliseconds_to_seconds`, `milliseconds_to_seconds_0dp`, `milliseconds_to_seconds_1dp`, `milliseconds_to_seconds_2dp`, `milliseconds_to_seconds_2dp_if_required`
- `multiplicative_damage_modifier`
- `old_leech_percent`, `old_leech_permyriad`
- `per_minute_to_per_second`, `per_minute_to_per_second_0dp`, `per_minute_to_per_second_1dp`, `per_minute_to_per_second_2dp`, `per_minute_to_per_second_2dp_if_required`
- `permyriad_per_minute_to_%_per_second`
- `plus_two_hundred`, `times_one_point_five`, `times_twenty`

Supported order-reversing handlers:

- `negate`
- `negate_and_double`
- `divide_by_one_hundred_and_negate`

Compound sequences are evaluated for ordering in sequence; every reversing handler flips direction. An odd number of reversals uses the negative displayed magnitude as the default maximum. An even number uses the displayed magnitude as the default minimum. Decimal values remain `decimal` throughout.

Production handlers intentionally not treated as decimal projections:

- `affliction_reward_type`
- `canonical_stat`
- `display_indexable_skill`
- `display_indexable_support`
- `mod_value_to_item_class`
- `passive_hash`
- `tree_expansion_jewel_passive`
- `weapon_tree_unique_base_type_name`

These map numeric source storage to semantic options or identities, not a faithful provider-searchable decimal.

## Remaining unsupported provider forms

The 33 unsupported provider forms consist of:

- 10 arity-one semantic lookup forms (six passive-hash forms and four canonical-stat combinations);
- 21 arity-two forms without one proven scalar projection;
- 2 arity-seven forms without one proven scalar projection.

Representative cases:

| Source identity | Rendered form | Provider mismatch/reason |
|---|---|---|
| `memory_line_minimum_possessions_of_rare_unique_monsters` + maximum counterpart | Rare/Unique monsters possessed by X to Y spirits | Two independent bounds; no official single-scalar formula established. |
| `cast_socketed_spells_on_X_life_spent` + trigger chance | Chance to trigger after spending an amount | Chance and spend threshold have different meanings; averaging or choosing either value would be false. |
| `spell_minimum_added_cold_damage_per_power_charge` + maximum counterpart | Adds X to Y Cold Damage per Power Charge | The mapped provider stat exposes the minimum-only branch, not a two-placeholder range; provider arity confirmation fails. |
| `local_weapon_passive_tree_granted_passive_hash` | Allocates a passive | `passive_hash` is a semantic identity lookup, not a decimal bound. |
| seven-stat unique jewel forms | Effects selected by passive-tree start | Several conditionally selected effects cannot be reduced to one numeric bound. |

Representative presence-only form: `local_item_allow_modification_while_corrupted` / `Can be modified while Corrupted`.

## Representative supported paths

- Scalar integer: crafted/local Attack Speed retains its displayed integer minimum.
- Scalar decimal: Mana Leech with `divide_by_one_hundred` retains displayed `2.83`, not raw `283` and not `0.0283`.
- Negative/inverse: `negate` selects a negative maximum.
- Added elemental and physical damage: local/global Fire, Cold, Lightning, and Physical min/max stat pairs use the same generic arithmetic mean after provider confirmation.
- Crafted/fractured/explicit provider variants: provider kind does not alter the projection.
- Hybrid source modifier: each separately rendered effect keeps its own resolved component and provider stat.

## Current fixture JSON

The Cold fixture produces a JSON number, not a string:

```json
{
  "stats": [
    {
      "type": "and",
      "filters": [
        {
          "id": "explicit.stat_1037193709",
          "value": {
            "min": 19.5
          }
        }
      ]
    }
  ]
}
```

The Trade button opens the successful official query identity created from that same request; it does not rebuild or reinterpret the bound in the UI.
