# Research: Complete Mortal Item Materialization

**Date**: 2026-08-11
**Source issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)

## Decision 1: Keep semantic items embedded and add a client-owned identity index

**Decision**: Keep the complete semantic item object in its current canonical
carrier. Embed the GM envelope and client receipt in that object, and maintain
global identity/carrier/lineage authority in
`game_state/inventory/item_identity_index.json`.

**Rationale**: Existing player, NPC, storage, vehicle, console, browser, quest,
trade, and crafting code already consumes embedded item objects. A global
reference-only registry would rewrite all of those surfaces. Embedded evidence
alone cannot prove one active carrier or retain retired split/merge identities
without repeated cross-file scans.

**Rejected alternatives**:

- Reference-only inventories: architecturally clean, but an unbounded rewrite
  for the completion roadmap.
- Embedded evidence only: insufficient for global uniqueness and lineage.
- One new item file per route: duplicates semantics and makes transfer identity
  ambiguous.

## Decision 2: Use `creationRef` as the only temporary new-item reference

**Decision**: A genuinely new item has top-level `existedId: null` and a
non-empty, turn-unique `creationRef`. Its envelope repeats the same
`creationRef`. Same-turn equipment, container, quest, journal, and placement
commands refer to that value until the client assigns the permanent `itemId`.

**Rationale**: Current examples use route-specific temporary aliases such as
`initialId`, while player `UpdateInventory` has no consistent cross-surface
temporary reference. One field makes duplicate detection and reference rewrite
deterministic.

**Rejected alternatives**:

- Reuse display name: names are not unique and comparisons are currently often
  case-insensitive.
- Let the GM invent the permanent ID: violates client-owned identity authority.
- Retain both `initialId` and `creationRef`: creates conflicting alias
  authority in the current schema.

## Decision 3: Materialize `UpdateInventory` into canonical `items[]`

**Decision**: The accepted-turn normalizer projects new/full/partial
`UpdateInventory` commands into canonical `items[]`, resolves temporary
references, removes the command surface, seals new receipts, and updates the
identity index.

**Evidence**:

- `Configuration/FileMapping.cs` maps `UpdateInventory` to
  `game_state/inventory/items.json`.
- `IO/StateDistributor.cs` performs a generic root-property merge, so the
  command remains beside existing canonical data.
- `CanonicalStateNormalizer.InventorySidecars.cs` currently strips journal
  anchors but does not apply `UpdateInventory` to `items[]`.
- `InventoryManagementService` and `StorageTransportMoveService` prefer
  `items[]` but fall back to `UpdateInventory`, confirming the current
  transitional shape.

**Rejected alternative**: Teach every reader to treat `UpdateInventory` as a
second canonical collection. That would preserve duplicate command state and
prevent reliable pre-turn/current classification.

## Decision 4: Reuse the accepted-turn snapshot/normalizer rollback contour

**Decision**: Add `item_identity_index.json` and every governed carrier input
to `CanonicalStateNormalizer.CanonicalAccumulatedFiles`,
`NormalizerBackupInputFiles`, and `NormalizerRollbackTrackedFiles` as
appropriate. Perform accepted-turn sealing through the normalizer bound to the
existing canonical write lease and validated pre-turn backups.

**Rationale**:

- `GameEngine.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` validates raw
  state, loads a hash-validated pending snapshot, normalizes canonical state,
  validates the result, and retains rollback authority through repair.
- QTE/browser transaction code already derives tracked rollback paths from the
  normalizer lists.
- A second transaction mechanism would compete with the canonical lease and
  omit existing failure/repair paths.

**Local action exception**: Drop, split, merge, storage, and vehicle moves do not
run through accepted-turn normalization. They must call one coordinated
client-owned transition writer under the already-held canonical lease. The
writer captures exact before-images for all touched carrier/index paths,
performs conditional writes, and restores them on failure.

## Decision 5: Loot/drop is a route; discard is a retirement transition

**Decision**: Do not invent a durable ground-loot file. A loot/drop acquisition
creates an item only when its final canonical carrier is player inventory, an
NPC inventory, or an already-valid location storage. The current local
`DropAsync` action removes the item and therefore records a `destroyed`
retirement. A persistent placement uses a storage carrier.

**Rationale**: Repository inspection found no canonical ground-loot carrier.
`InventoryManagementService.DropAsync` physically removes the item.
`StorageTransportMoveService` already models persistent placement by moving
the same JSON node into `locationStorages[].contents`.

**Rejected alternative**: Add `game_state/world/loot.json` in #1511. This
would invent a location/loot entity contract outside the accepted issue and
overlap #1515.

## Decision 6: Track continuity through valid vehicle carriers without materializing vehicles

**Decision**: A vehicle inventory is a supported destination/source for an
existing item transition. #1511 validates only exact item identity and the
existence of the selected already-valid vehicle carrier. Vehicle completeness,
capacity, creation, and lifecycle remain #1515.

**Rationale**: The existing player UI can move items between
`game_state/inventory/items.json` and
`game_state/misc/vehicles.json[].inventory`. Ignoring that route would make
the global item index stale after a supported local action.

## Decision 7: Exact identity is ordinal and alias conflict fails closed

**Decision**: Permanent item IDs, creation refs, materialization IDs, receipt
IDs, owner IDs, and carrier IDs use `StringComparer.Ordinal`. Leading/trailing
whitespace, Unicode-normalization alternatives, case variants, multiple
different identity aliases on one item, and duplicates are invalid.

**Rationale**: Existing inventory readers often use
`OrdinalIgnoreCase` and name fallbacks. Those remain presentation lookup
helpers only; authority matching must not collapse distinct identities.

**Rejected alternative**: Normalize or case-fold IDs. That makes two authored
identities silently become one and undermines duplicate detection.

## Decision 8: Derive validation work from one-pass catalogs

**Decision**: `MortalItemCarrierCatalog.Build` enumerates each governed
carrier once and returns exact dictionaries by item ID, creation ref, receipt
ID, materialization ID, companion reference, and route authority. Validators
consume those dictionaries instead of scanning all carriers per item.

**Verification**: An internal deterministic scan counter records visited item,
companion, and route nodes. A focused test doubles the representative
population and requires total work to remain at most 2.5x.

**Rejected alternative**: A stopwatch-only assertion. Wall-clock tests are
unstable and cannot prove the absence of hidden nested scans.

## Decision 9: Separate pre-seal and post-seal validation

**Decision**:

1. **Raw/pre-seal** validation accepts new items only with
   `existedId: null`, complete semantics, `creationRef`, and GM envelope;
   rejects any GM-authored receipt/index fields; validates route/companion
   authority against the validated pre-turn snapshot.
2. **Normalization** assigns IDs, rewrites references, seals receipts, projects
   commands, and updates the client index without inventing semantics.
3. **Canonical/post-seal** validation requires every durable item to have one
   valid receipt and matching index entry; verifies immutable continuity,
   one active carrier, transition lineage, route outcomes, and companions.

**Rationale**: Requiring a client receipt in the raw GM package is impossible;
allowing receipt-less items after normalization violates the current-schema-only
policy.

## Decision 10: Keep route authority in existing surfaces

**Decision**: Do not create a universal GM transaction DTO. Route adapters bind
the shared materialization package to the current surfaces:

| Route | Existing authority used |
| --- | --- |
| `player_acquisition` | new `UpdateInventory` item plus accepted turn context |
| `npc_acquisition` | `NPCInventoryAdds` for an existing NPC |
| `new_npc_inventory` | new NPC initial `inventory[]` |
| `loot_acquisition` | structured loot outcome plus one real destination carrier |
| `craft_output` | validated `pending_craft_request.json` and consumed-input transition |
| `trade_output` | validated NPC trade request/receipt and source/destination transition |
| `quest_reward` | structured quest reward detail bound to the created item |
| `storage_placement` | new item plus exact already-valid location/storage destination |

**Rationale**: Craft, trade, and quest code already has route-specific
anti-duplication authority. The shared item contract should bind it, not replace
it.

## Decision 11: Preserve current schema only

**Decision**: Bootstrap empty state gains an empty identity index. Every
positive repository item fixture/example gains a complete envelope, receipt,
and matching index entry. Receipt-less objects survive only inside named
negative tests. No promotion classifier, fallback reader, migration command, or
same-turn legacy overlay is implemented.

**Rationale**: The game has not shipped and there is no player save population.
The constitution explicitly rejects hypothetical pre-release compatibility
complexity.

## Decision 12: Documentation and projection boundaries

**Decision**:

- Update Mortal item rules, NPC inventory transfer guidance, CLI docs, task
  guides, daemon prompt entrypoint, worked examples, and validation manifest.
- Add at least a mundane empty-section example, a mechanic-bearing second-route
  example, and a receipt-less rejection example.
- Prove existing console/browser projections do not expose the envelope,
  receipt, index, file paths, or repair language.
- Do not edit afterlife contract docs because no Chaos Sea/Shining/Saref
  contract changes; record that no-update rationale in the PR.
- Do not edit frontend source unless the red privacy/parity tests show an
  actual leak or inability to display semantic item state.

## Current repository surfaces to account for

- Full player/new-NPC item validation:
  `ValidationService.PlayerAndInventory.cs`.
- NPC inventory delta validation:
  `ValidationService.NpcWorldAndMeta.cs`.
- Cross-references and sidecars:
  `ValidationService.InventoryNpcWorldCrossRefs.cs` and
  `CanonicalStateNormalizer.InventorySidecars.cs`.
- Accepted-turn validation and repair:
  `GameEngine.ValidationAndRepair.cs`.
- Snapshot/rollback:
  `GameEngine.SessionAndSnapshots.cs`,
  `LiveTurnPreparationService.cs`, and normalizer tracked-file lists.
- Local item transitions:
  `InventoryManagementService.cs`,
  `StorageTransportMoveService.cs`, console Explorer, and browser command
  services.
- Route authority:
  `CraftRequestState.cs`, `NpcTradeRequestState.cs`, and
  `QuestRewardAuthority.cs`.
- Active example item:
  `FileSystemExample/game_session/game_state/inventory/items.json`.

## Resolved unknowns

No `NEEDS CLARIFICATION` item remains. The only conditional implementation
branch is frontend source editing, determined mechanically by red projection
tests rather than a product decision.
