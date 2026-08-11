# Contract: Mortal Item Client Identity Authority v1

**Issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)
**Authority owner**: Client only

## Protected surfaces

- Permanent item identity: `itemId` and equal persisted `existedId`.
- Embedded `materializationReceipt`.
- `game_state/inventory/item_identity_index.json`.
- Receipt IDs, seals, carrier transitions, retirement states, lineage, and
  origin-materialization unions.

Raw GM creation commands must omit protected fields except `existedId: null`.
Raw existing-item commands may name the exact accepted `existedId` but cannot
resend or patch a receipt/index entry. Any direct GM mutation is rejected
against validated pre-turn authority.

## Receipt v1

Exact embedded fields:

```json
{
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
```

`instanceKind` is `root` or `split_derived`. Root receipts have no parents.
Split-derived receipts have exactly the direct source item ID. Every value is
immutable. The seal input is defined in `data-model.md`.

## Index v1

Root shape is exactly:

```json
{
  "schemaVersion": 1,
  "entries": []
}
```

Each entry has exact fields:

- `itemId`: unique permanent identity;
- `receiptId`: unique embedded receipt identity;
- `state`: `active`, `merged`, `consumed`, or `destroyed`;
- `currentCarrier`: exact carrier object for active, otherwise null;
- `originMaterializationIds`: non-empty, unique, ordinal-sorted array;
- `parentItemIds`: unique direct derivation sources;
- `mergedIntoItemId`: survivor for merged, otherwise null;
- `transitions`: non-empty append-only transition array.

Unknown and duplicate properties fail. Entries are sorted by ordinal
`itemId` for deterministic output; order is not identity.

## Transition entry

Exact fields:

- `transitionId`: globally unique client ID;
- `kind`: `create`, `transfer`, `split`, `merge`, `consume`,
  `destroy`, or `semantic_update`;
- `turn`: non-negative client turn;
- `sourceItemIds`: exact ordered unique identities;
- `sourceCarrier`: carrier or null;
- `destinationCarrier`: carrier or null;
- `quantityBefore`, `quantityAfter`: non-negative integers;
- `authorityKind`, `authorityId`: exact operation/request authority.

Transitions are append-only. A no-op transition is invalid. Quantities and
carrier change must match the actual carrier before/after images.

## Carrier

Exact fields:

```json
{
  "kind": "player_inventory",
  "ownerId": "player",
  "containerId": null,
  "containerPath": []
}
```

Supported kinds are `player_inventory`, `npc_inventory`,
`location_storage`, and `vehicle_inventory`. Owner/container requirements
are defined in `data-model.md`. `containerPath` is an exact permanent-item-ID
chain and must resolve without cycles to active containers in the same root
carrier.

## Coordinated writes

All client transitions:

1. acquire or reuse one `CanonicalWriteLease`;
2. parse and validate every touched before-image;
3. build the complete intended carrier/index after-images in memory;
4. validate identity, quantity, lineage, and companion invariants;
5. commit conditional atomic file writes;
6. restore exact before-images if any write fails;
7. return success only after the composed state revalidates.

The helper does not accept arbitrary caller-supplied index JSON. Callers supply
an operation intent; the client derives protected state.

## Exact identity rules

- Use `StringComparer.Ordinal` for every protected identity dictionary/set.
- Reject empty/whitespace IDs; never trim an accepted ID.
- Reject values that differ only by case, surrounding whitespace, or Unicode
  normalization with a dedicated ambiguity issue.
- Persist both `itemId` and `existedId` exactly equal; do not retain
  `id`/`initialId` aliases.
- Player selection may resolve a unique display name for UX, but the write
  intent is converted to exact `itemId` before authority validation.

## Continuity

- Existing receipt and envelope must equal the validated pre-turn copy.
- Existing index history is a prefix of current history.
- Transfer changes only current carrier plus one transition.
- Split adds one new child entry; source entry remains active.
- Merge preserves survivor receipt; contributor entries become `merged`.
- Full consumption/destruction retires exactly the removed item.
- Retired IDs are never reused or reactivated.
- A missing/unreadable validated pre-turn index fails closed.

## Bootstrap

Fresh empty Mortal state includes:

```json
{"schemaVersion":1,"entries":[]}
```

No placeholder item, receipt, transition, or migration marker is written.

## Protection and repair

The repair packet may tell the GM how to correct its item/envelope/route
package. It must never instruct the GM to author an item ID, receipt, seal,
index entry, or transition. Client-owned corruption triggers rollback/rebuild
only from validated before-images and accepted GM semantics; it is not repaired
by trusting GM-provided protected JSON.
