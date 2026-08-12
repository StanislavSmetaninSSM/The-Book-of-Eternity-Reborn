# Complete Mortal Item Materialization Design

**Date**: 2026-08-11

**Source issue**: [GitHub #1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)

**Spec Kit feature**: `specs/1511-complete-item-materialization/`

**Status**: Design and written specification approved in conversation

## Purpose

The Mortal World currently validates many fields on item commands, but it does not provide one continuity contract that proves every durable item was completely created, has exactly one identity and carrier, preserves that identity through transfers, and leaves bounded repair evidence when creation fails.

This design adds that shared first-materialization boundary without replacing the existing inventory, NPC, loot, crafting, trade, quest, storage, console, or browser systems.

## Accepted Product Decisions

1. Every durable ordinary Mortal World item is covered regardless of whether it first appears with the player, an NPC, a new NPC, as loot, from crafting, from trade, as a quest reward, or in an existing storage carrier.
2. A physical item keeps the same canonical identity when it changes owner or placement.
3. Split stacks receive new instance identities and auditable lineage. Merges conserve quantity and retire consumed identities.
4. Every governed optional section is explicit: `populated` or `empty_by_design`, with canonical empty/null state present.
5. Scalar resources and currencies are excluded unless represented as canonical item stacks with their own identity.
6. The selected architecture is hybrid: evidence travels with the item, while a client-owned global authority tracks identity, carrier, state, and lineage.
7. The game is unreleased. Receipt-less development state is invalid and repository fixtures are migrated; runtime legacy promotion is not implemented.

## Current-State Observations

- Player item creation enters through `UpdateInventory` and persists in `game_state/inventory/items.json`.
- Existing NPC item creation enters through `NPCInventoryAdds`; a newly created NPC may carry complete items inside its initial inventory.
- Craft, trade, quest reward, equipment, item text, sentient journals, bonds, recipes, movement, and storage already have separate command or companion surfaces.
- Current validation checks many full-item fields and mechanical-summary authority, but creation completeness is route-local and does not seal one cross-carrier identity receipt.
- Existing rules sometimes describe transfer as creating a new receiver instance. That behavior must change for a physical existing item because it breaks history, bonds, journals, and uniqueness.
- The current normalizer can shape or merge several inventory sidecars. It must not become a semantic author for missing item content.

## Approaches Considered

### A. Hybrid embedded evidence plus client identity authority — selected

The existing carrier keeps the full item. The item carries its authored materialization envelope and sealed receipt. A client-owned identity authority records global placement and lineage.

Advantages:

- Preserves existing readers and command routes.
- Gives cross-carrier duplicate detection and exact transfer continuity.
- Supports split/merge lineage without turning every inventory into a reference-only view.
- Keeps implementation bounded enough for the completion roadmap.

Costs:

- Requires coordinated validation across several existing carrier and companion surfaces.
- Requires client bookkeeping for every accepted identity transition.

### B. One global item registry with reference-only inventories — rejected

This gives the cleanest normalized model but would rewrite inventory, NPC state, trade, crafting, storage, UI projections, fixtures, and large parts of the command contract. The migration and regression surface is too large for the project's current completion goal.

### C. Embedded evidence only — rejected

This is smaller initially but forces repeated cross-file scans and leaves transfers, duplicates, and stack lineage vulnerable to drift. It cannot reliably distinguish one moved item from two active copies without global authority.

## Architecture

```text
GM route command + complete item + authored envelope
                         |
                         v
       pre-turn/current route and semantic validation
                         |
                         v
 client assigns item ID, resolves creationRef, seals receipt
                         |
              +----------+-----------+
              |                      |
              v                      v
 canonical carrier + companions   client identity authority
              |                      |
              +----------+-----------+
                         v
           console/browser semantic projection
```

The canonical carrier remains the source of current item semantics. The client identity authority is the source of global instance identity, active/retired state, current carrier, and lineage. Neither replaces route-specific request or receipt authority.

## Component Boundaries

### 1. GM-authored materialization envelope

Every independently created root item supplies a versioned envelope alongside the complete item. Conceptually it contains:

- schema version;
- independent-root `materializationId`;
- Mortal realm;
- supported creation route;
- current turn and route/source authority;
- same-turn `creationRef`;
- one disposition for every governed semantic section.

The envelope is authored before a permanent item identity exists. `creationRef` is temporary routing evidence, not a permanent identity. After acceptance it may be retained only as audit evidence inside the sealed receipt.

An `empty_by_design` disposition always carries a non-empty in-world reason. The corresponding canonical item or companion field must still be present in its real empty shape. A reason in the envelope cannot substitute for missing canonical state.

### 2. Client-sealed materialization receipt

After the package passes pre-seal validation, the client assigns the permanent item identity and creates immutable receipt evidence that binds:

- a unique instance receipt identity;
- the permanent `itemId`;
- the accepted root envelope;
- the accepted turn;
- the consumed `creationRef`;
- root or derived lineage status.

The GM cannot author or patch sealed fields. Ordinary later updates never replace the original receipt.

### 3. Client-owned item identity authority

The proposed canonical location is `game_state/inventory/item_identity_index.json`. Its exact contract is finalized during planning, but each entry must represent:

- exact `itemId`;
- unique instance receipt identity;
- one or more origin materialization identities;
- `active`, `consumed`, `destroyed`, or `merged` state;
- exact current carrier for active items;
- parent/source item identities for derivations;
- operation and turn evidence for transitions.

The index is not GM-authored state. Validation derives the expected transition from the validated pre-turn authority plus accepted route commands, then verifies the client result. Direct GM edits are rejected.

The implementation must build exact-key dictionaries once per validation pass. It must not perform repeated full-carrier scans per item.

### 4. Carrier adapters

One shared materialization validator receives normalized item candidates from route-specific carrier adapters:

- player inventory;
- existing NPC inventory;
- new-NPC initial inventory;
- loot/drop acquisition adapter whose accepted destination is a real player,
  NPC, or existing-storage carrier;
- craft output;
- trade output;
- quest reward;
- existing location storage.

Adapters describe route and carrier authority but do not weaken the shared semantic contract. They also expose the applicable companion targets and same-turn references for atomic validation.

### 5. Companion authority adapters

The shared contract reconciles the item with applicable existing state for:

- equipment;
- mechanical bonuses/effects;
- item resources;
- readable text;
- sentient journals;
- bonds and fate cards;
- recipes and disassembly;
- quest linkage;
- ownership and placement.

An absent companion entry is valid only when the corresponding section is explicitly empty and no canonical field or route claim requires it. Orphan companion entries always fail.

## Governed Sections

| Section | Required meaning |
| --- | --- |
| Presentation | Stable player-facing name, description, type/group semantics, quality/rarity, and image guidance |
| Physical | Count, weight, volume, value, durability, and other applicable physical values |
| Mechanics | Structured authority for every mechanical summary; explicit narrative-only disposition when appropriate |
| Equipment | Compatible slots, two-hand behavior, accessory rules, and same-turn equipment links |
| Container | Container capability, capacity/weight behavior, parent path, and contained-item links |
| Consumption | Consumable capability and complete effect authority or explicit non-consumable state |
| Readable/sentient | Readable content and sentient journal authority, or explicit absence |
| Crafting/disassembly | Recipe/disassembly relationships and material outputs, or explicit absence |
| Bonds/fate cards | Bond state and fate-card definitions/links, or explicit absence |
| Quest role | Quest linkage, uniqueness/turn-in semantics, or explicit absence |
| Provenance | Creation route, source authority, realm, and route-specific request/receipt evidence |
| Ownership/placement | Exactly one initial canonical carrier and exact owner/location references |

The detailed field-to-section map belongs in the feature's contract and data-model artifacts during planning. It must reuse current canonical field names where they are coherent and remove redundant aliases only when tests prove readers remain correct.

## Lifecycle Flows

### Independent first creation

1. Load and validate the pre-turn snapshot and current route authority.
2. Identify a genuinely new candidate by absence from every governed pre-turn carrier and identity entry.
3. Validate the complete item, envelope, section dispositions, route evidence, and all companion state.
4. Assign a permanent item identity only after semantic validation succeeds.
5. Resolve every same-turn reference from `creationRef` to the permanent identity.
6. Seal the immutable receipt and create the active identity entry.
7. Revalidate the composed canonical state under the same bound
   `CanonicalWriteLease`, then release the lease only after success.

The accepted-turn refresh captures exact before-images for every touched
carrier, companion, and index path before the first write. If normalization or
post-seal validation fails, it restores those paths byte-for-byte, so no item,
reward settlement, consumed ingredient, equipment change, or active index entry
commits.

### Existing-item transfer

1. Resolve the exact existing identity from pre-turn authority.
2. Require the source carrier to remove it and exactly one destination carrier to accept it in the same transition.
3. Preserve the semantic object, root envelope, and original sealed receipt except for narrow, separately authorized changes.
4. Move applicable companion ownership/placement atomically.
5. Update only the current carrier and transition evidence in the client authority.

Display name never identifies the transferred object. A merchant sale, NPC handoff, drop, retrieval, or storage operation that moves an existing physical item follows this flow rather than independent creation.

The existing local `DropAsync` action is destructive discard, not persistent
ground placement: it retires the removed identity as destroyed. Persistent
placement uses an already-valid storage carrier. Existing vehicle-inventory
moves also preserve item identity and update its carrier coordinate, while
vehicle materialization and capacity remain #1515.

### Stack split

1. Require a positive child count strictly smaller than the source count.
2. Require complete semantic compatibility between source and child other than identity, count, carrier, and client lineage evidence.
3. Assign a new child item identity and unique derived instance receipt.
4. Copy the accepted semantic envelope without claiming a new GM-authored root creation.
5. Inherit the source origin materialization set and record source/child/operation evidence.
6. Commit both new counts and identity entries atomically.

The root `materializationId` is unique among independent GM-authored roots. A split-derived item may reference the same root only when the client-owned unique instance receipt and lineage prove that relationship.

### Stack merge

1. Require full stack compatibility; name/type/quality alone are insufficient.
2. Choose one existing identity as survivor through a documented deterministic rule.
3. Conserve total count exactly.
4. Keep the survivor's immutable materialization receipt unchanged.
5. Retire every consumed identity as `merged` and record the survivor.
6. Preserve the union of all origin materialization identities in the client authority.

Readable, sentient, bonded, quest-linked, unique, equipped, contained, or mechanically different items do not merge unless an existing explicit contract proves they are semantically stack-compatible.

### Crafting output

A crafted output is a new independent root item, not a split or rename of an ingredient. Its provenance identifies the authorized craft request and consumed inputs. Ingredient consumption and output creation commit atomically.

### Failed or cancelled route

A failed craft, declined trade, failed quest reward, or abandoned loot decision creates no materialization envelope receipt, active identity entry, or false player-facing acquisition confirmation.

## Validation Order

1. Validate snapshot and route authority availability.
2. Build exact global identity, carrier, companion, request, and receipt indexes.
3. Classify each transition as independent creation, existing transfer, split, merge, or other existing-item lifecycle operation.
4. Validate GM-owned semantics and envelope completeness.
5. Validate route/source authority and realm separation.
6. Validate companion completeness and same-turn cross-references.
7. Validate client-owned receipt/index transition and immutability.
8. Validate single active carrier and stack quantity/lineage invariants.
9. Validate player-facing structured outcome consistency.
10. Commit only after the full composed state is valid.

No classifier has a receipt-less legacy-promotion branch.

## Narrative and Player-Facing Authority

Arbitrary natural-language interpretation is not a safe validation boundary. Therefore:

- prose never grants ownership or mechanics;
- pending route requests, item commands, canonical state, and structured outcome receipts are authoritative;
- player inventory and acquisition confirmations derive from accepted transitions;
- a structured reward/purchase/craft/transfer claim without matching accepted item authority fails;
- prompts and examples forbid the GM from narrating a granted durable item without authoring the structured transition;
- console/browser projections strip envelope, receipt, index, file, and repair internals.

This avoids pretending that keyword matching can reliably understand Russian narrative while still preventing every machine-authoritative prose-only grant.

## Repair and Rollback

An item repair packet must include:

- exact item identity or temporary creation reference;
- transition class and route;
- source and destination carrier targets;
- missing/invalid envelope fields and section dispositions;
- required companion targets;
- expected route/source authority and actual evidence;
- receipt/index conflict when client-owned continuity fails;
- explicit instruction not to replay the transaction or duplicate the reward.

The validated pre-turn snapshot remains rollback authority until the corrected composed state passes. Repair may patch missing GM semantics but may not manually invent permanent IDs, sealed receipts, or identity-index records. Player-facing narrative repair is separated from canonical repair when the state is already correct.

## Documentation and Examples

The same change reviews and updates at least:

- `Rules/Block_2.txt`, `Rules/Block_5.txt`, `Rules/Block_9.txt`,
  `Rules/Block_10.txt`, `Rules/Block_11.txt`, `Rules/Block_19.A.txt`, and
  `Rules/Block_20.txt`;
- CLI operation guidance and GM prompt entrypoints;
- item, NPC inventory, crafting, trade, quest-reward, and storage worked examples;
- example validation manifest;
- documentation/source-guard tests;
- repository bootstrap and positive fixtures.

Required worked evidence:

1. A complete simple mundane item with all optional sections explicitly empty.
2. A mechanic-bearing item through a different creation route with matching structured authority.
3. A receipt-less current-schema item rejected as malformed, not promoted.
4. Transfer and stack-lineage examples if no existing focused fixture expresses them clearly.

## Test Strategy

Implementation follows red-green-refactor:

- focused contract tests for envelope, receipt, section mapping, exact identity, realm, and client-owned field protection;
- focused integration tests for every enumerated creation route;
- continuity tests for transfers and direct canonical-write bypasses;
- split/merge conservation and lineage tests;
- companion atomicity and orphan tests;
- bounded repair and rollback/idempotence matrices covering craft, trade,
  quest rewards, loot, transfer, split, and merge;
- existing console/browser projection tests for accepted visibility and internal-field privacy;
- fixture, documentation, manifest, and source-guard tests;
- a representative scaling control that detects quadratic multi-carrier scans;
- one Fast checkpoint and one clean-checkout PreMerge at the exact final commit.

Frontend source is changed only if existing projections leak internal fields or cannot display accepted semantic state. Otherwise the final report records the no-update rationale.

## Explicit Non-Goals

- Afterlife Soul Relics or resources.
- Location, transport, or storage entity materialization.
- New inventory UX or visual redesign.
- Network multiplayer.
- General rewrite to a reference-only global item registry.
- Compatibility readers, migration commands, or promotion workflows for obsolete development saves.
- Broad refactoring unrelated to item first-materialization and identity continuity.

## Design Self-Review

- No placeholders, TODOs, or unresolved questions remain.
- Independent creation is distinguished from transfer, split, merge, and craft output.
- Root materialization uniqueness is consistent with split-derived instance lineage.
- The design does not claim reliable semantic parsing of arbitrary narrative.
- #1512 and #1515 boundaries remain explicit.
- The hybrid authority avoids a full inventory architecture rewrite and avoids embedded-only cross-carrier drift.
