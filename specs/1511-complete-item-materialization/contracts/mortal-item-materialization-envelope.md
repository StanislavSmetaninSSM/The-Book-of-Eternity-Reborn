# Contract: Mortal Item Materialization Envelope v1

**Issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)
**Authority owner**: GM for a genuinely new ordinary Mortal item
**Persistence**: Embedded unchanged in the accepted item

## Raw creation shape

A raw new item MUST contain:

- `existedId: null`;
- no `itemId`, `id`, or `initialId`;
- a non-empty turn-unique `creationRef`;
- every complete semantic field required below;
- exactly one `materialization` envelope;
- no `materializationReceipt` and no identity-index command.

The envelope has the exact fields:

```json
{
  "schemaVersion": 1,
  "materializationId": "mat_item_...",
  "realm": "Mortal",
  "route": "player_acquisition",
  "sourceTurn": 42,
  "sourceAuthority": {
    "kind": "turn_outcome",
    "authorityId": "turn_42"
  },
  "creationRef": "new_item_...",
  "state": "complete",
  "sections": {
    "presentation": { "state": "populated", "reason": null },
    "physical": { "state": "populated", "reason": null },
    "mechanics": { "state": "empty_by_design", "reason": "..." },
    "equipment": { "state": "empty_by_design", "reason": "..." },
    "container": { "state": "empty_by_design", "reason": "..." },
    "consumption": { "state": "empty_by_design", "reason": "..." },
    "readableOrSentient": { "state": "empty_by_design", "reason": "..." },
    "craftingAndDisassembly": { "state": "empty_by_design", "reason": "..." },
    "bondsAndFateCards": { "state": "empty_by_design", "reason": "..." },
    "questRole": { "state": "empty_by_design", "reason": "..." },
    "provenance": { "state": "populated", "reason": null },
    "ownershipAndPlacement": { "state": "populated", "reason": null }
  }
}
```

No unknown envelope, source-authority, section, or disposition property is
allowed. Duplicate JSON properties fail.

## Scalar rules

- `schemaVersion`: integer exactly `1`.
- `materializationId`: non-empty and globally unique among independent roots.
- `realm`: exact ordinal string `Mortal`.
- `route`: one value from the route table below.
- `sourceTurn`: integer equal to the active accepted turn and at least 1.
- `sourceAuthority.kind`: exact route-authority kind.
- `sourceAuthority.authorityId`: exact non-empty current request, receipt,
  reward, carrier, or turn authority ID.
- `creationRef`: exactly equals the top-level item `creationRef`.
- `state`: exact `complete`.
- Disposition `state`: exact `populated` or `empty_by_design`.
- A populated disposition has `reason: null`.
- An empty disposition has a non-empty in-world reason after trimming.

Identity-like values are ordinal. The validator rejects leading/trailing
whitespace, case variants, Unicode-normalization variants, and duplicates
rather than normalizing them.

## Route and authority values

| Route | `sourceAuthority.kind` |
| --- | --- |
| `player_acquisition` | `turn_outcome` |
| `npc_acquisition` | `npc_inventory_add` |
| `new_npc_inventory` | `new_npc` |
| `loot_acquisition` | `loot_template` |
| `craft_output` | `craft_request` |
| `trade_output` | `npc_trade_receipt` |
| `quest_reward` | `quest_reward` |
| `storage_placement` | `location_storage` |

The route adapter validates the authority ID against the hash-validated
pre-turn/current route surface. For standard loot it derives the exact key
`loot_template:<turn>:<ordinal>:<baseName>` from
`Context.lootForCurrentTurn`; plot-mandated acquisition uses the ordinary
`player_acquisition` turn authority. Text labels and narrative do not satisfy
route authority.

## Complete semantic fields

All raw independent roots physically include the following fields even when
their governed section is empty:

| Section | Fields |
| --- | --- |
| Presentation | `name`, `description`, `image_prompt`, `type`, `group`, `quality`, `rarity` |
| Physical | `price`, `count`, `weight`, `volume`, `durability`, `maxDurability` |
| Mechanics | `bonuses`, `effects`, `structuredBonuses`, `combatEffect`, `customProperties`, `mechanicalSummaryAuthority`, `mechanicalSummaryUnresolvedReason` |
| Equipment | `equipmentSlot`, `accessoryForSlot`, `requiresTwoHands` |
| Container | `isContainer`, `capacity`, `containerWeight`, `weightReduction` |
| Consumption | `isConsumption` and any applicable structured effect authority |
| Readable/sentient | `textContent`, `journalEntries`, `isSentient`, `unreadableReason`, `sealedReason`, `lockedReason` |
| Craft/disassembly | `disassembleTo` plus applicable recipe companion references |
| Bonds/Fate Cards | `ownerBondLevelCurrent`, `ownerBondLevelMax`, `fateCards` |
| Quest role | `questLinks` |
| Ownership/placement | `contentsPath` plus the exact route destination |

`count` is a positive integer. Numeric physical values are non-negative.
`durability` and `maxDurability` use the existing percentage-string
contract and durability does not exceed maximum. Presentation strings are
non-empty. `quality` and `rarity` use canonical values and agree.

## Canonical empty shapes

| Section | Required empty evidence |
| --- | --- |
| Mechanics | `bonuses: []`, `effects: []`, `structuredBonuses: []`, `combatEffect: []`, `customProperties: []`, both mechanical-summary fields `null` |
| Equipment | `equipmentSlot: null`, `accessoryForSlot: null`, `requiresTwoHands: false`; no same-turn equipment reference |
| Container capability | `isContainer: false`, `capacity: null`, `containerWeight: null`, `weightReduction: null` |
| Consumption | `isConsumption: false`; no consumption-only structured effects |
| Readable/sentient | `textContent: null`, `journalEntries: []`, `isSentient: false`, unreadable/sealed/locked reasons `null`, no companion text/journal entry |
| Craft/disassembly | `disassembleTo: null`, no recipe companion link |
| Bonds/Fate Cards | both bond levels `null`, `fateCards: []`, no bond companion entry |
| Quest role | `questLinks: []`, no structured quest linkage |

Presentation, physical, provenance, and ownership/placement are always
`populated` for a durable item. A non-container item can still be located
inside another container: `contentsPath` belongs to placement, not to the
item's own container capability.

## Populated consistency

`populated` requires actual matching structured evidence. Examples:

- mechanical display text has matching structured target/value authority;
- equipment slots satisfy two-hand/accessory rules;
- container capacity is present and positive;
- consumable effect authority is complete;
- readable content or the exact text sidecar exists;
- a sentient item has exact journal authority;
- disassembly outputs are complete material objects;
- bond/Fate Card schemas are complete;
- quest links resolve to exact quest/reward authority;
- `contentsPath` contains permanent parent item IDs after sealing.

The envelope does not legalize missing, malformed, orphaned, or contradictory
canonical data.

## Immutability

After acceptance, the whole envelope is immutable. Ordinary updates use the
permanent `itemId` and may change only fields authorized by the existing
narrow command. Transfer changes placement/index state, not the envelope.
Split children inherit the root envelope but receive separate derived receipt
evidence. No later GM response resends or regenerates the envelope.

## Rejection codes

Codes use the `mortal_item_materialization_` prefix. Required families:

- `missing_envelope`, `invalid_envelope`, `unknown_field`;
- `identity_conflict`, `duplicate_materialization_id`,
  `duplicate_creation_ref`;
- `route_authority_missing`, `route_authority_mismatch`;
- `section_missing`, `section_empty_reason_missing`,
  `section_state_mismatch`;
- `canonical_empty_surface_missing`, `companion_missing`,
  `orphan_companion`;
- `gm_authored_client_field`, `wrong_realm`;
- `receiptless_current_item`, `immutable_envelope_rewrite`.

There is no legacy-promotion or compatibility code family.
