# Data Model: Complete Mortal Item Materialization

**Date**: 2026-08-11
**Source issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)

## Authority boundaries

| Surface | Authority owner | Mutable after acceptance |
| --- | --- | --- |
| Item semantic fields | GM through existing narrow commands | Yes, only through an authorized lifecycle command |
| `materialization` | GM at independent root creation | No |
| `materializationReceipt` | Client during sealing | No |
| Permanent `itemId` / `existedId` | Client | No |
| Identity-index entry state/carrier/lineage | Client | Yes, through validated client transitions |
| Root origin materialization set | Client-derived from accepted receipts | Append-only on merge; never remove |
| Route request/receipt | Existing route owner | Per its existing contract |
| Player-facing projection | Client-derived | Contains semantic fields only |

The item carrier owns current semantic data. The identity index owns global
instance location and lineage. Neither is allowed to reconstruct missing
GM-authored semantics.

## Canonical item identity fields

A persisted item uses one exact permanent identity:

```json
{
  "existedId": "itm_...",
  "itemId": "itm_..."
}
```

`existedId` and `itemId` MUST both be present and exactly equal in current
canonical state. The generic `id`, `initialId`, name-based identity, blank
strings, trimmed variants, case variants, and Unicode-normalized alternatives
are not identity aliases in the new schema. Existing readers may continue to
use names for display selection, but validation and writes resolve authority by
exact `itemId`.

A raw genuinely new item instead has:

```json
{
  "existedId": null,
  "creationRef": "new_item_..."
}
```

`itemId`, `id`, `initialId`, and `materializationReceipt` are forbidden
on a raw new item. The client removes top-level `creationRef` after resolving
all same-turn references; the accepted audit copy remains inside the receipt.

## GM-authored materialization envelope

Every independently created root item contains exactly:

```json
{
  "materialization": {
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
      "mechanics": { "state": "empty_by_design", "reason": "У предмета нет механического эффекта." },
      "equipment": { "state": "empty_by_design", "reason": "Предмет нельзя экипировать." },
      "container": { "state": "empty_by_design", "reason": "Предмет не является вместилищем." },
      "consumption": { "state": "empty_by_design", "reason": "Предмет не расходуется при использовании." },
      "readableOrSentient": { "state": "empty_by_design", "reason": "У предмета нет текста или собственного голоса." },
      "craftingAndDisassembly": { "state": "empty_by_design", "reason": "Предмет не задаёт рецептов или разборки." },
      "bondsAndFateCards": { "state": "empty_by_design", "reason": "Предмет не образует связь и не имеет Карт Судьбы." },
      "questRole": { "state": "empty_by_design", "reason": "Предмет не связан с заданием." },
      "provenance": { "state": "populated", "reason": null },
      "ownershipAndPlacement": { "state": "populated", "reason": null }
    }
  }
}
```

Envelope objects use exact field sets. `sourceAuthority.kind` is one of the
route-specific kinds in the routes contract; `authorityId` is the exact
existing request/receipt/turn/reward identifier. A populated section has
`reason: null`; an `empty_by_design` section has a non-empty in-world reason.

## Governed section-to-state map

| Section | Required canonical evidence |
| --- | --- |
| `presentation` | `name`, `description`, `image_prompt`, `type`, `group`, `quality`, `rarity` |
| `physical` | `price`, `count`, `weight`, `volume`, `durability`, `maxDurability` |
| `mechanics` | `bonuses`, `effects`, `structuredBonuses`, `combatEffect`, `customProperties`, mechanical-summary authority/reason fields |
| `equipment` | `equipmentSlot`, `accessoryForSlot`, `requiresTwoHands`, and same-turn equipment references |
| `container` | `isContainer`, `capacity`, `containerWeight`, `weightReduction`, `contentsPath` |
| `consumption` | `isConsumption` and any complete structured consumption effect |
| `readableOrSentient` | `textContent`, `journalEntries`, `isSentient`, unreadable/sealed/locked reason fields, item text/journal companion entries |
| `craftingAndDisassembly` | `disassembleTo`, recipe links, and consumed-input provenance when crafted |
| `bondsAndFateCards` | bond fields, `fateCards`, item-bond companion entries |
| `questRole` | quest identity/turn-in/uniqueness fields and quest reward/history link |
| `provenance` | route, source authority, Mortal realm, materialization ID, route request/receipt |
| `ownershipAndPlacement` | exact destination carrier, `contentsPath`, and applicable equipment/owner references |

Every governed optional canonical field is physically present. The canonical
empty shape is field-specific: `null`, `false`, `[]`, or `{}`. A section
reason never replaces the field.

## Client-sealed embedded receipt

After raw validation succeeds, the client writes:

```json
{
  "materializationReceipt": {
    "schemaVersion": 1,
    "receiptId": "mirec_...",
    "itemId": "itm_...",
    "materializationId": "mat_item_...",
    "acceptedAtTurn": 42,
    "creationRef": "new_item_...",
    "instanceKind": "root",
    "parentItemIds": [],
    "seal": "sha256:..."
  }
}
```

For a split-derived child, `instanceKind` is `split_derived`,
`parentItemIds` contains the exact source item ID, and
`materializationId` remains the accepted independent root origin. The child
has a new `receiptId` and `seal`.

The seal is SHA-256 over a versioned canonical serialization of:
`receiptId`, `itemId`, `materializationId`, `acceptedAtTurn`,
`creationRef`, `instanceKind`, exact `parentItemIds`, and the immutable
`materialization` object. The seal is integrity evidence, not GM authority;
ownership protection comes from raw rejection plus pre-turn receipt/index
continuity.

Every receipt field and the embedded envelope are byte-semantically immutable
after acceptance. Transfer, ordinary update, and merge do not replace them.

## Client-owned identity index

Path: `game_state/inventory/item_identity_index.json`

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "itemId": "itm_...",
      "receiptId": "mirec_...",
      "state": "active",
      "currentCarrier": {
        "kind": "player_inventory",
        "ownerId": "player",
        "containerId": null,
        "containerPath": []
      },
      "originMaterializationIds": ["mat_item_..."],
      "parentItemIds": [],
      "mergedIntoItemId": null,
      "transitions": [
        {
          "transitionId": "mitrn_...",
          "kind": "create",
          "turn": 42,
          "sourceItemIds": [],
          "sourceCarrier": null,
          "destinationCarrier": {
            "kind": "player_inventory",
            "ownerId": "player",
            "containerId": null,
            "containerPath": []
          },
          "quantityBefore": 0,
          "quantityAfter": 1,
          "authorityKind": "turn_outcome",
          "authorityId": "turn_42"
        }
      ]
    }
  ]
}
```

### Entry states

- `active`: exactly one current carrier and one embedded item/receipt.
- `merged`: no current carrier; `mergedIntoItemId` names one active or later
  retired survivor; the original entry and transitions remain.
- `consumed`: no current carrier; consumption/crafting transition records the
  authorizing operation.
- `destroyed`: no current carrier; discard/destruction transition records the
  authorizing operation.

An entry is never deleted. A retired item cannot become active again by
reusing its ID. A buyback/reacquisition of the same still-existing physical
item is a transfer and therefore must not first retire it as destroyed.

### Carrier coordinate

`currentCarrier` has exact fields:

| `kind` | `ownerId` | `containerId` |
| --- | --- | --- |
| `player_inventory` | literal `player` | null |
| `npc_inventory` | permanent NPC ID | null |
| `location_storage` | permanent location ID | permanent storage ID |
| `vehicle_inventory` | permanent vehicle ID | null |

`containerPath` is an ordered array of permanent item IDs from outermost to
immediate parent. It is empty for a root item in the carrier.
`contentsPath` in the canonical semantic item is the same array or `null`
for root placement; the current name-based path is removed from positive
current-schema fixtures and documentation.

## Transition model

### Independent creation

- Pre-state: no item, receipt, materialization ID, or creation ref in accepted
  authority.
- Raw state: complete item, `existedId: null`, unique `creationRef`, complete
  envelope, no client fields.
- Post-state: one permanent identity, one receipt, one active index entry, one
  carrier, all dependent references rewritten.

### Transfer

- Same `itemId`, envelope, receipt, count, and applicable semantics.
- Source carrier count goes from one item instance to zero; destination from
  zero to one.
- Index appends one `transfer` transition and replaces only
  `currentCarrier`.
- Optional independent narrow semantic updates must carry their own authority
  and are validated separately; they do not become implicit transfer changes.

### Split

- `0 < childQuantity < sourceQuantity`.
- Parent keeps its identity/receipt and decremented count.
- Child gets a new item ID and split-derived receipt, identical governed
  semantics except identity/count/client lineage.
- Parent and child share the same origin set.
- Exactly one `split` transition records before/after quantities.

### Merge

- At least two fully compatible active stacks in one operation.
- The user-selected stack is the deterministic survivor; ties do not depend on
  array order or display name.
- Survivor count becomes the exact sum.
- Survivor receipt is unchanged.
- Consumed entries become `merged`; survivor index origin set becomes the
  sorted exact union; one transition records every source ID and quantity.

### Consume/destroy

- The item disappears from its sole carrier.
- Partial stack consumption changes count without retirement.
- Full consumption becomes `consumed`; local discard/destruction becomes
  `destroyed`.
- Equipment and companion ownership references are cleared or retained as
  historical evidence according to their existing contract.

### Craft

- Ingredient partial/full consumption and output independent creation form one
  coordinated transition.
- Output envelope uses route `craft_output`; index creation transition records
  every consumed input ID in provenance.
- Failed/cancelled craft changes neither ingredients nor output/index state.

## Validation invariants

1. Every durable current-schema Mortal item has exactly one exact permanent ID,
   envelope, receipt, active index entry, and carrier coordinate.
2. Every active entry resolves to exactly one carrier item with the matching
   receipt; every retired entry resolves to none.
3. Independent root `materializationId`, raw `creationRef`, permanent
   `itemId`, and `receiptId` values are globally unique under ordinal
   comparison.
4. A split may reuse an origin materialization ID only with a unique
   split-derived receipt and valid parent lineage.
5. Envelope and receipt equal their validated pre-turn values for every
   pre-existing item.
6. The GM cannot add, remove, or modify index entries or embedded client fields.
7. Every companion reference resolves to one exact active item and agrees with
   its section disposition.
8. One-pass catalog counters grow linearly with input nodes.
9. Realm must be exactly `Mortal`; ordinary items never appear in afterlife
   relic/resource authority.
10. Empty bootstrap state is `{"schemaVersion":1,"entries":[]}` and requires
    no placeholder item.

## State transitions

```text
                         split
                  +----------------> active child
                  |
new raw package -> active root ----+---- transfer ----> active root
                  |                |
                  |                +---- merge -------> active survivor
                  |                                      +
                  |                                      |
                  +---- consume/destroy ----------> retired entry
                                                         ^
consumed merge contributor -------------------------------+
```

Retirement is terminal for an identity. Historical entries remain readable by
the validator/index but are not exposed as player inventory.
