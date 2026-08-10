# Feature Specification: Complete Mortal Item Materialization

**Feature Branch**: `1511-complete-item-materialization`

**Created**: 2026-08-11

**Status**: Draft

**Input**: Complete first materialization for every durable ordinary Mortal World item across player, NPC, loot, crafting, trade, quest-reward, and existing-storage routes while preserving identity, stack lineage, bounded repair, and current-schema-only authority.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1511 — Enforce complete first materialization of Mortal World items](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)
- **Issue type**: P1 enhancement / contract-hardening task
- **Spec Kit justification**: The feature changes canonical item state, validation, normalization, rollback/repair, multiple GM creation routes, fixtures, prompts, examples, manifests, and player-facing projections across many files and sessions.
- **Contract scope**: GM-facing prompts; Mortal runtime state; validation; normalization; rollback/repair; player and NPC inventory; loot; crafting; trade; quest rewards; existing location-storage placement; console/browser read projections; docs; examples; fixtures; source guards.
- **Save compatibility**: Not required. The game is unreleased; obsolete receipt-less development state, examples, and test fixtures are migrated to the current schema rather than supported at runtime.
- **Out of scope**: Soul Relics and afterlife resources (#1512); location entity materialization (#1513/#1514); transport/storage entity materialization and capacity rules (#1515); general inventory UI redesign; network multiplayer; ordinary later item edits except identity/receipt/index continuity; runtime legacy promotion or save migration.

## Clarifications

### Session 2026-08-11

- Q: Does the contract cover every durable ordinary Mortal item regardless of owner or placement? → A: Yes; player, NPC, loot, crafting, trade, quest-reward, and existing-storage creation routes share one contract.
- Q: Does an existing physical item retain identity across owner/placement changes, and how are split stacks handled? → A: Transfers preserve identity; split stacks receive new instance identities with lineage to their source and origins.
- Q: Must simple items explicitly declare optional sections that are intentionally absent? → A: Yes; every governed section is `populated` or `empty_by_design`, with the canonical empty/null field present.
- Q: Are scalar currencies/resources item entities? → A: No, unless represented as a canonical stack with its own permanent item identity.
- Q: Which identity architecture should the feature use? → A: Hybrid embedded materialization evidence plus a client-owned global identity/carrier/lineage authority.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every New Mortal Item Is Complete (Priority: P1)

As a player, when an ordinary durable item first enters the Mortal World through any supported route, I receive one coherent, inspectable item rather than prose, a skeletal row, or disconnected companion data.

**Why this priority**: Incomplete creation leaves the player with rewards or possessions that the game cannot reliably display, equip, trade, move, craft with, or validate.

**Independent Test**: Create equivalent simple items through player inventory, NPC inventory, new-NPC inventory, loot, crafting, trade, quest reward, and existing-storage routes. Each complete creation is accepted and each route rejects the same omitted mandatory section.

**Acceptance Scenarios**:

1. **Given** a valid pre-turn state without the item, **When** the GM creates a complete ordinary item and all applicable companion data through an authorized route, **Then** the turn is accepted with one permanent item identity and sealed materialization evidence.
2. **Given** a valid pre-turn state without the item, **When** a creation route omits a required semantic section, intentional-empty reason, owner, placement, provenance, or applicable companion state, **Then** the complete turn is rejected with bounded repair instructions and no item is granted.
3. **Given** a mundane item with no mechanics, equipment use, readable text, sentience, container behavior, crafting behavior, bonds, or quest role, **When** all those absences are explicitly declared and represented by canonical empty values, **Then** the item is accepted without invented semantics.
4. **Given** a scalar money or resource counter, **When** it changes without creating a canonical item entity, **Then** item materialization is not required.

---

### User Story 2 - Physical Identity Survives Ownership and Placement Changes (Priority: P1)

As a player, I expect the sword traded by an NPC, dropped on the ground, stored, or returned to me to remain the same sword with the same history rather than a recreated duplicate.

**Why this priority**: Re-creation on transfer breaks provenance, readable content, sentient journals, bonds, uniqueness, and auditability.

**Independent Test**: Transfer one accepted item among player, NPC, loot, and existing-storage carriers and verify that its permanent identity and original receipt remain unchanged while exactly one active placement is recorded.

**Acceptance Scenarios**:

1. **Given** a completely materialized existing item, **When** an authorized transfer changes its owner or placement, **Then** the same permanent identity and original materialization evidence move atomically and the former carrier no longer owns it.
2. **Given** an existing item, **When** a transfer attempts to recreate it with a new independent identity, leave it active in two carriers, or alter its original receipt, **Then** the turn is rejected.
3. **Given** two distinct items with the same display name, **When** either is transferred, **Then** exact identity rather than name determines which item moves.

---

### User Story 3 - Stack Operations Preserve Quantity and Provenance (Priority: P1)

As a player, splitting or merging stackable items must never create or erase quantity and must retain an auditable connection to every contributing origin.

**Why this priority**: Stack duplication or provenance loss can corrupt inventory, rewards, crafting inputs, and trade outcomes.

**Independent Test**: Split one accepted stack, transfer one child, merge compatible stacks, and verify exact quantity conservation, unique active instance identities, and complete lineage for surviving and retired entries.

**Acceptance Scenarios**:

1. **Given** one accepted stack, **When** it is split, **Then** the child receives a new permanent item identity and unique instance receipt while inheriting auditable origin lineage from the source.
2. **Given** compatible stacks, **When** they merge, **Then** total quantity is conserved, one identity survives, consumed identities are retired, and all contributing origins remain auditable.
3. **Given** incompatible item semantics or a quantity mismatch, **When** a split or merge is attempted, **Then** the transition is rejected without changing any stack.

---

### User Story 4 - Repair Is Narrow, Atomic, and Player-Safe (Priority: P2)

As a GM operator and player, an invalid item creation must be repairable without duplicating a reward, replaying a transaction, leaking technical state, or damaging unrelated inventory.

**Why this priority**: A broad or ambiguous repair loop can be more destructive than the original malformed item.

**Independent Test**: Submit malformed creation packages from several routes and verify that the repair packet names the exact item, route, carrier, missing fields, and companion targets while rollback retains the pre-turn state.

**Acceptance Scenarios**:

1. **Given** a partially materialized item, **When** validation fails, **Then** one bounded repair packet describes only the necessary canonical and companion corrections and the pre-turn snapshot remains authoritative.
2. **Given** a repaired package for an already narrated reward, **When** validation succeeds, **Then** the reward is committed exactly once and player-facing output contains no validation, file, receipt, or index terminology.
3. **Given** a receipt-less canonical item in current state, **When** validation runs, **Then** it is rejected rather than promoted, tolerated, or silently completed.

---

### User Story 5 - Existing Inventory Views Remain In-World (Priority: P2)

As a player, I can inspect an accepted item in the existing console and browser inventory surfaces without seeing raw materialization envelopes, receipts, identity-index entries, file paths, or repair terminology.

**Why this priority**: Harness authority must improve correctness without turning player-facing clients into debug tools.

**Independent Test**: Open accepted items in existing console and browser detail flows and assert that normal item semantics are visible while all internal authority fields are absent.

**Acceptance Scenarios**:

1. **Given** a completely materialized item, **When** the player opens its current inventory/detail view, **Then** the existing meaningful description and mechanics appear without raw contract data.
2. **Given** a rejected item creation, **When** the turn returns for repair, **Then** no false acquisition confirmation appears in player-facing inventory or structured reward surfaces.

### Edge Cases

- A simple item intentionally has every optional capability empty; every empty section still has a non-empty in-world reason and the required canonical empty value.
- Two item identities differ only by case, Unicode normalization, or surrounding whitespace; identity comparison remains exact and malformed aliases are rejected.
- A same-turn creation reference is missing, duplicated, reused across routes, or points to two items.
- The client assigns a permanent identity but one same-turn equipment, owner, quest, journal, container, or storage reference still points to the temporary reference.
- A complete item appears in a companion file but no canonical carrier owns it, or a carrier owns an item whose required companion entry is missing.
- A new NPC carries multiple new items, including a container and a contained item that refer to each other before permanent identities exist.
- A trade, craft, quest reward, or loot outcome is cancelled or fails; no materialization receipt or active index entry may remain.
- One physical item is present in player and NPC inventories simultaneously, or an item is both active and retired.
- A split copies a root materialization identifier; this is allowed only when unique instance evidence and valid lineage prove the derivation.
- A merge combines compatible stacks from different origins; the survivor retains all origin references while consumed identities become retired.
- A merge attempts to combine readable, sentient, bonded, quest-linked, unique, equipped, contained, or mechanically different items merely because their names match.
- A crafted output uses consumed ingredients; the output is a new root materialization with ingredient provenance, not a renamed ingredient or stack split.
- A currency or resource is represented as a canonical item stack with its own identity; it is in scope even if an equivalent scalar counter exists elsewhere.
- An obsolete receipt-less fixture is encountered; repository data is migrated, while runtime validation fails closed.
- The validated pre-turn snapshot or identity authority is absent or malformed; validation fails closed instead of guessing whether an item is new or existing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST apply one first-materialization contract to every durable ordinary Mortal World item regardless of its initial supported carrier or creation route.
- **FR-002**: The supported route inventory MUST include player acquisition, existing-NPC acquisition, new-NPC initial inventory, loot/drop creation, crafting output, trade output, quest reward, and placement into an already valid location-storage carrier.
- **FR-003**: First creation MUST be determined against a validated pre-turn snapshot and current accepted identity authority, not by display name, prose, route keywords, or missing optional data.
- **FR-004**: Every first creation MUST include a complete semantic item object and a versioned GM-authored materialization envelope in the same atomic package.
- **FR-005**: The envelope MUST identify the Mortal realm, creation route, source turn, source authority, unique independent-root materialization identity, and disposition of every governed semantic section.
- **FR-006**: Every governed section MUST be `populated` or `empty_by_design`; every `empty_by_design` section MUST include a non-empty in-world reason and its mapped canonical field MUST be physically present as the correct empty or null shape.
- **FR-007**: Governed sections MUST cover presentation, physical properties, mechanical authority, equipment, container behavior, consumption, readable/sentient content, crafting/disassembly, bonds/fate cards, quest linkage, provenance, ownership, and placement.
- **FR-008**: The client MUST assign permanent item identities and MUST reject GM-authored invented permanent identities for genuinely new items.
- **FR-009**: Same-turn temporary creation references MUST be unique within the turn, resolve every dependent reference to the client-assigned identity, and cease to act as identity after acceptance.
- **FR-010**: Successful first creation MUST produce immutable client-sealed receipt evidence binding the permanent item identity to the accepted envelope and turn.
- **FR-011**: A client-owned identity authority MUST track every active or retired item instance, its current carrier when active, its independent origin materialization identities, and its transition lineage.
- **FR-012**: GM-authored state MUST NOT create, alter, remove, or counterfeit client-owned identity-authority entries or sealed receipt fields.
- **FR-013**: Identity comparisons MUST be exact and MUST reject duplicate, ambiguous, blank, case-variant alias, or conflicting independent-root identities.
- **FR-014**: An active item MUST have exactly one active owner or placement across all governed carriers.
- **FR-015**: An authorized transfer of an existing item MUST preserve its permanent identity, original envelope, and original sealed receipt while moving all applicable companion authority atomically.
- **FR-016**: A transfer MUST fail if it recreates an existing item, leaves multiple active carriers, drops required companion state, changes immutable evidence, or refers only by display name.
- **FR-017**: Splitting a stack MUST create a unique child item identity and unique instance evidence, conserve total quantity, and retain auditable lineage to the source and all origin materializations.
- **FR-018**: Merging compatible stacks MUST conserve total quantity, retain one active identity, retire every consumed identity, and preserve the union of contributing origins.
- **FR-019**: Stack compatibility MUST be based on complete governed semantics and state; matching name, type, or quality alone MUST NOT authorize a merge.
- **FR-020**: A crafted output MUST be a new root materialization with its own accepted envelope and explicit provenance to consumed inputs.
- **FR-021**: Scalar currencies and resources without canonical item identity MUST remain outside item materialization; any canonical stack with its own item identity MUST be governed as an item.
- **FR-022**: Every applicable companion surface—mechanical authority, equipment, readable text, sentient journal, bonds/fate cards, recipes, quest links, ownership, and placement—MUST be validated and committed atomically with first creation.
- **FR-023**: Companion entries MUST resolve to one exact canonical item and MUST NOT exist as orphaned, duplicate, or cross-realm authority.
- **FR-024**: Every mechanical player-facing summary MUST have matching structured authority in the same accepted item package; prose and display text alone MUST NOT grant mechanics.
- **FR-025**: Route-specific requests, receipts, rewards, purchases, crafting outcomes, and transfers MUST bind to the exact accepted new or existing item transition they claim.
- **FR-026**: A structured possession or reward outcome without a matching accepted item transition MUST reject the turn.
- **FR-027**: Free-form narrative MUST NOT constitute ownership authority; inventory and structured player-facing acquisition confirmation MUST derive only from accepted canonical transitions.
- **FR-028**: A receipt-less item in current canonical state MUST be invalid and MUST NOT enter a runtime promotion, compatibility, fallback, or semantic auto-completion path.
- **FR-029**: Normalization MAY assign identities, resolve temporary references, seal client receipts, and update client-owned identity authority, but MUST NOT invent descriptions, mechanics, provenance, section dispositions, empty reasons, ownership intent, or route evidence.
- **FR-030**: Invalid materialization MUST retain the validated pre-turn snapshot and rollback authority until a corrected package passes validation.
- **FR-031**: Repair output MUST identify the exact item or creation reference, creation route, carrier, missing or conflicting fields, required companion targets, expected authority, and actual invalid evidence.
- **FR-032**: Repair MUST be idempotent with respect to item grant, transaction settlement, crafting consumption, quest reward, and stack quantity.
- **FR-033**: After accepted first materialization, ordinary later item changes MUST use existing narrow lifecycle commands and MUST NOT rewrite the original envelope or sealed receipt.
- **FR-034**: Existing console and browser inventory/detail projections MUST expose accepted item semantics while withholding raw envelope, receipt, identity-authority, file-path, and repair data.
- **FR-035**: Bootstrap state with no items MUST remain valid without synthetic placeholder materialization records.
- **FR-036**: Repository bootstrap state, examples, manifests, and active test fixtures containing durable items MUST be migrated to the current receipt-bearing schema or explicitly retained as malformed negative inputs.
- **FR-037**: GM-facing prompts, rules, command documentation, worked examples, manifests, and source guards MUST describe and enforce the same current materialization workflow.
- **FR-038**: Documentation MUST include at least two valid worked creation routes, one complete simple item with explicit empty sections, and one receipt-less current-schema rejection example.
- **FR-039**: The contract MUST remain setting-agnostic and MUST NOT infer capabilities, mechanics, ownership, uniqueness, or route authority from genre terms, display names, item types, or descriptive prose.
- **FR-040**: Validation across carriers and identity authority MUST avoid duplicate-sensitive repeated full scans that grow quadratically with item count.
- **FR-041**: Soul Relics, afterlife counters, and afterlife item routes MUST remain excluded and realm-separated for follow-up #1512.
- **FR-042**: Transport and storage entity completeness or capacity MUST remain owned by #1515; this feature MUST only require an exact reference to an already valid carrier and maintain item identity continuity.
- **FR-043**: The final candidate MUST pass repository artifact hygiene, focused contract tests, broad control tests, documentation/source guards, and a clean-checkout pre-merge control within configured limits.

### Key Entities

- **Mortal Item**: A durable ordinary item instance or stack with current semantics, permanent identity, current owner/placement, and complete materialization evidence.
- **Materialization Envelope**: GM-authored, versioned declaration of one independent root creation, its route/source, and the populated or intentionally empty state of every governed section.
- **Sealed Materialization Receipt**: Immutable client evidence binding an accepted item instance to an envelope, permanent identity, and accepted turn.
- **Identity Authority Entry**: Client-owned record of an item instance's active/retired state, current carrier, origin materializations, and transition lineage.
- **Carrier**: The exact canonical player, NPC, loot/drop, or already valid location-storage owner/placement that contains one active item.
- **Companion Authority**: Canonical state outside the core item that provides item text, sentient journals, equipment, mechanics, bonds, recipes, quest linkage, or placement evidence.
- **Item Transition**: A creation, transfer, split, merge, consumption, destruction, crafting, or retirement operation that changes identity authority without rewriting prior evidence.
- **Stack Lineage**: Auditable relationship among source, child, survivor, and retired item identities with conserved quantities and retained origin materializations.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All eight enumerated first-creation route classes accept a complete representative item and reject the same missing required section with no partial grant.
- **SC-002**: One hundred percent of current-schema durable items in active repository bootstrap state, examples, and positive fixtures carry valid materialization evidence; explicitly malformed negative fixtures remain isolated and labelled.
- **SC-003**: Every tested transfer among player, NPC, loot, and existing-storage carriers preserves the exact permanent identity and original receipt with exactly one active carrier.
- **SC-004**: Every tested split and merge conserves quantity exactly and leaves complete origin lineage for all active and retired identities.
- **SC-005**: Every tested structured reward, purchase, crafting, and quest outcome without matching item authority is rejected before the player can inspect or use the claimed item.
- **SC-006**: Every malformed creation scenario produces one bounded repair packet that names all required item-specific targets and no unrelated canonical target.
- **SC-007**: Console and browser item projections expose zero raw materialization, receipt, identity-index, path, or repair fields in player-facing output.
- **SC-008**: Documentation contains at least two valid route examples, one complete simple item with explicit empty sections, and one rejected receipt-less item; all referenced examples pass documentation validation.
- **SC-009**: Doubling a representative multi-carrier item population does not cause more than 2.5 times the validation work in the repository performance control, preventing quadratic identity scans.
- **SC-010**: Focused contract tests, Fast control, required documentation/source-guard controls, and clean-checkout PreMerge complete with zero failures, zero duplicate tests, no timeout, and successful owned-process cleanup.

## Verification Plan *(mandatory)*

- **C# verification**: Use `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused` with filters for new item-materialization contract/integration tests and existing inventory, NPC inventory, craft, trade, quest-reward, storage-transition, normalizer, repair, and projection tests; run one `Fast` checkpoint and one clean-candidate `PreMerge` before integration.
- **Documentation/contract verification**: Run focused documentation/source-guard tests including `ExampleDocumentationValidationTests`; run `FullValidation` if shared example manifests or validation-documentation boundaries require it.
- **Frontend verification**: No frontend source change is expected. Existing browser projection integration tests MUST prove accepted items remain visible and internal fields remain hidden. Run frontend verification only if implementation changes frontend source or browser contracts.
- **Manual/player-facing verification**: Inspect one accepted simple item and one mechanic-bearing item through existing console and browser detail flows; confirm in-world Russian output and absence of internal contract vocabulary.

## Assumptions

- The game remains unreleased and no public save population requires compatibility.
- Existing player, NPC, loot, crafting, trade, quest, and storage command surfaces remain the route entry points; this feature hardens their shared first-creation boundary rather than redesigning every workflow.
- Existing item detail UI can consume the semantic item object while ignoring new internal authority fields; any necessary projection filtering is in scope, but visual redesign is not.
- Scalar resources and currencies remain governed by their existing resource contracts unless represented as canonical item stacks.
- The identity authority is client-owned and may be maintained automatically after accepted narrow lifecycle operations without granting the client permission to invent GM semantics.
- A split-derived item may inherit an origin materialization identifier only when unique instance evidence and valid lineage prove the derivation; independent root creations must never reuse a materialization identifier.
- Compatible stack merges may combine multiple origins; the active survivor and retired contributors retain an auditable union of those origins.
- A raw sentence can describe an in-world event but cannot grant mechanical ownership. Machine-readable acquisition and player-facing inventory confirmation remain authoritative.
- #1515 may later add stronger storage/transport carrier authority without changing the item identity and receipt guarantees established here.
