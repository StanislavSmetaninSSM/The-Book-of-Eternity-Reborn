# Contract: Mortal Item Routes and Transitions v1

**Issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)

## Shared rule

Every durable ordinary Mortal item first appears through exactly one route
adapter and ends in exactly one real canonical carrier. Adapters add
route-specific authority checks; they never relax the complete envelope,
semantic, companion, identity, receipt, or single-carrier contract.

## Creation routes

### `player_acquisition`

- Input: one complete new `UpdateInventory` item.
- Authority: active accepted turn plus exact structured acquisition outcome.
- Destination: player inventory, optionally an exact player container path.
- Failure: no canonical item/index/equipment/quest confirmation.

### `npc_acquisition`

- Input: one `NPCInventoryAdds` entry for an existing permanent NPC.
- Authority: exact NPC ID and inventory-add command.
- Destination: that NPC inventory, optionally an exact NPC container path.
- Existing physical transfers must use a transfer authority, not a null-ID add.

### `new_npc_inventory`

- Input: complete items embedded in one genuinely new NPC's initial
  `inventory[]`.
- Authority: exact new-NPC creation reference and complete NPC materialization.
- Equipment references use each item's `creationRef` and are rewritten after
  both actor and item identities exist.
- NPC rejection rejects all its initial items atomically.

### `loot_acquisition`

- Input: one exact `lootForCurrentTurn` template plus a complete new item.
- Authority: the derived ordinal key
  `loot_template:<turn>:<ordinal>:<baseName>` from the validated turn context.
- Destination: player inventory, NPC inventory, or existing location storage.
- There is no durable ground-loot carrier in v1.

### `craft_output`

- Input: complete new output item plus ingredient changes/removals.
- Authority: exact validated `pending_craft_request.json` request ID.
- Provenance: exact consumed input item IDs and quantities.
- Output is a new independent root, never a renamed/split ingredient.
- All ingredient and output/index writes commit or roll back together.

### `trade_output`

- Input: source removal plus destination item transition.
- Authority: exact NPC trade request/receipt and offer/item identity.
- Buying/selling/buyback of an existing physical item is a transfer preserving
  identity/receipt. A merchant-generated never-before-existing stock item uses
  independent creation exactly once.
- Sold-out, price, currency, source/destination, and item transition agree.

### `quest_reward`

- Input: complete new item plus structured `itemsReceived[]` reference.
- Authority: exact quest/reward identity in canonical quest history.
- Reward detail resolves to the permanent item after sealing.
- A reward summary without a matching accepted item fails.

### `storage_placement`

- Input: complete new item with exact initial destination.
- Authority: already-valid current location ID and storage ID.
- This route validates item placement only. Storage creation, completeness,
  capacity model, and lifecycle remain #1515.

## Existing-item transfer

An authorized transfer has:

- exact permanent item ID;
- exactly one pre-turn source carrier;
- no post-turn copy in the source;
- exactly one post-turn destination carrier;
- unchanged envelope, receipt, and unmodified semantics except separately
  authorized narrow changes;
- preserved applicable companions;
- one client index `transfer` transition.

Supported roots are player inventory, NPC inventory, existing location storage,
and already-valid vehicle inventory. Vehicle materialization/capacity remains
#1515. Name-only transfer is invalid.

## Container placement

`contentsPath` is null at a root or an ordered array of permanent parent item
IDs. Every parent:

- is active in the same root carrier;
- has `isContainer: true`;
- appears only once in the path;
- satisfies existing capacity/weight rules;
- is not the child itself.

Same-turn parent/child creation may use `creationRef` in raw commands; the
normalizer rewrites the complete chain or rejects the package.

## Split

- Source is one active stack with count greater than one.
- Requested child quantity is positive and less than source count.
- Child semantics exactly match source after excluding identity, count,
  receipt, and client lineage.
- Source remains the user-selected permanent identity.
- Child gets a new permanent ID and split-derived receipt.
- Before count equals the sum of both after counts.
- Both active index entries share origin materialization IDs.
- Failure changes no carrier/index data.

## Merge

- At least two active stacks are in the same root carrier and container path.
- The selected item is the deterministic survivor.
- Compatibility compares every governed semantic field after excluding only
  permanent identity, receipt, count, and client index evidence.
- Readable, sentient, bonded, quest-linked, unique, equipped, contained, or
  differently mechanical items fail compatibility unless their complete
  canonical state is exactly stack-compatible.
- Survivor receipt remains unchanged.
- Contributor IDs become `merged` into the survivor.
- Survivor origin set becomes the exact sorted union.
- Total before and after quantity is equal.

## Consume, remove, discard, and destroy

- A partial count reduction keeps the identity active and records a semantic
  update/consume transition with conserved quantity.
- A full authorized consumable use retires as `consumed`.
- Current local `DropAsync` is destructive discard and retires as
  `destroyed`; it does not create ground loot.
- A persistent handoff or placement uses transfer, not removal plus recreation.
- Equipment is cleared atomically when the sole item leaves the equipping
  carrier.

## Structured outcomes and narrative

Machine-authoritative acquisition, trade, craft, loot, or quest outcomes must
resolve to an accepted transition. Narrative prose is untrusted display text
and cannot grant ownership or mechanics. Player-facing acquisition
confirmation is derived after successful canonical validation, so repair
cannot display or duplicate an uncommitted item.

## Realm boundary

All adapters require current and envelope realm `Mortal`. Soul Relics,
afterlife counters/resources, Guardian/Shining trade artifacts, and afterlife
route authority are rejected here and remain #1512.
