# Feature Specification: Complete Faction Materialization

**Feature Branch**: `1510-faction-materialization-design`

**Created**: 2026-08-03

**Status**: Approved

**Input**: Enforce complete first creation and legacy promotion for Mortal
World and Shining Abode factions using the approved Faction Materialization
design.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  [#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510).
  Related contracts and boundaries:
  [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500),
  [#1222](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1222),
  [#1368](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1368),
  and
  [#1462](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1462).
- **Issue type**: P1 Harness/RLM enhancement, canonical-state hardening, and
  validation epic.
- **Spec Kit justification**: The work spans two realms, multiple canonical
  files, creation and migration state transitions, validators, normalization,
  rollback and repair, GM authoring contracts, examples, documentation guards,
  and bounded verification across multiple sessions.
- **Contract scope**: GM-facing prompts, runtime state, validation,
  normalization, repair packets, Mortal World faction authority, Shining Abode
  faction authority, documentation, examples, manifests, console and browser
  metadata privacy, and automated verification.
- **Out of scope**: The faction-content worker from #1222; autonomous
  living-world scheduling; Mortal living-world behavior from #1462; Chaos Sea
  Guardian politics except for an explicit non-faction boundary; player-facing
  UI redesign; a universal genre-specific faction model; eager migration of
  every untouched historical faction; wider test timeouts or concurrency.
- **Follow-up issue policy**: Any missing requirement, Critical or Important
  review finding, authority bypass, data-loss risk, or failed required
  verification remains blocking in #1510. Only unrelated or explicitly
  deferred non-blocking work may move to a follow-up, and only after a linked
  GitHub issue is created and recorded in `tasks.md` and the final PR summary.

## User Scenarios & Testing

### User Story 1 - Complete Mortal Factions (Priority: P1)

As a player, when a Mortal faction first becomes part of the world, it has a
coherent identity, purpose, agenda, principles, power, governance, leadership,
memory, and explicit state for every optional faction system instead of being a
technically valid but hollow shell.

**Why this priority**: Hollow Mortal factions break reputation, projects,
resources, ranks, territorial control, NPC affiliation, world progression, and
later player inspection.

**Independent Test**: Create Mortal factions through the full first-creation
carrier. Verify that a populated faction and a deliberately minimal faction
both pass, while missing semantic core, omitted sidecars, accidental emptiness,
identity divergence, and capability contradictions are rejected.

**Acceptance Scenarios**:

1. **Given** no faction with the proposed identity exists, **when** the GM
   creates a faction with a complete materialization receipt and a consistent
   cross-file bundle, **then** the faction is accepted atomically.
2. **Given** a new faction legitimately has no relations, projects, territory,
   player membership, or custom mechanics, **when** each exact empty surface is
   present with a meaningful `empty_by_design` reason, **then** the faction is
   accepted without invented content.
3. **Given** a new faction omits purpose, agenda, principles, leadership,
   memory, a governed section, or a required sidecar record, **when** the turn
   is validated, **then** the entire creation is rejected with focused issues.
4. **Given** a populated section, **when** its capability is false or its
   disposition says `empty_by_design`, **then** validation rejects the
   contradiction.
5. **Given** same-turn location control or NPC affiliation refers to the new
   faction, **when** its identity does not bind to the exact new faction
   identity, **then** the complete bundle is rejected.

---

### User Story 2 - Complete Shining Factions (Priority: P1)

As a player, when a Shining faction is discovered, founded, or introduced by a
supported story route, it has a real hall, charter, leadership, lifecycle,
memory, chronicle, visibility, political state, and complete actor links rather
than defaults synthesized by the client.

**Why this priority**: Shining factions drive residents, halls, projects,
politics, trade, influence, leadership transitions, and story progression.
Missing semantics can be hidden by normalization and surface much later as
broken gameplay.

**Independent Test**: Materialize one faction through each supported creation
route and verify route-specific counts, receipts, costs, Actor Materialization
links, hall and resident bindings, raw-state failure behavior, and derived-only
projection behavior.

**Acceptance Scenarios**:

1. **Given** an accepted `discover_native_faction` request, **when** the GM
   closes it, **then** exactly one new hall, one native faction, two through
   four new ascended residents, and exactly two seeded completed projects are
   created with complete authority and matching receipt evidence.
2. **Given** an accepted player-founding request, **when** the faction is
   founded, **then** the exact request, charter, supporters, player-soul
   leadership, reserved costs, receipt, hall, affiliations, history, and
   materialization state agree.
3. **Given** exact documented story authority for a hidden faction, **when**
   the faction is created, **then** its visibility is hidden but its hall,
   charter, lifecycle, leadership, memory, chronicle, and materialization
   remain complete.
4. **Given** a new Shining faction lacks authored origin, charter, leadership,
   visibility, agenda, or strategic memory, **when** raw state is validated,
   **then** the turn fails before normalization can add a default.
5. **Given** non-vacant non-player leadership or newly significant residents,
   **when** an exact actor lacks complete Actor Materialization authority,
   **then** the faction creation is rejected.
6. **Given** an untouched historical Shining faction, **when** only
   client-owned strength, tier, or service projections are recalculated,
   **then** the faction is not forced through promotion.

---

### User Story 3 - Safe Legacy Promotion and Existing Updates (Priority: P1)

As a returning player, I can load an older save without fabricated faction
content, while the first real GM-authored change upgrades only the touched
faction and later changes use narrow commands instead of full-object rewrites.

**Why this priority**: Unconditional migration would break old saves, while an
unrestricted full carrier would let ordinary updates bypass ownership and
overwrite unrelated faction history.

**Independent Test**: Load untouched legacy Mortal and Shining factions, mutate
one legacy faction, update one already materialized Mortal faction through the
closed core command and dedicated commands, and attempt forbidden full-object
resends.

**Acceptance Scenarios**:

1. **Given** a legacy faction without materialization authority, **when** the
   save is loaded and the faction is untouched, **then** the faction remains
   readable and unchanged.
2. **Given** that legacy faction, **when** any GM-authored core, sidecar,
   affiliation, memory, leadership, influence, resource, or project mutation
   is accepted, **then** the same turn must provide a complete promotion.
3. **Given** a promoted or newly materialized faction, **when** its historical
   materialization receipt is changed or removed, **then** validation rejects
   the turn.
4. **Given** an already materialized Mortal faction, **when** an allowed core
   group changes through one exact `FactionCoreChanges` entry, **then** only
   that group changes and the command is consumed.
5. **Given** an already materialized faction, **when** the GM resends a full
   faction object or places a dedicated-command field inside
   `FactionCoreChanges`, **then** validation rejects the bypass.
6. **Given** a legacy faction promoted in the same turn as a supported gameplay
   mutation, **when** unrelated historical state also changes, **then** the
   promotion fails and repair preserves the validated history.

---

### User Story 4 - Bounded Repair and Clear GM Contract (Priority: P2)

As the GM agent, I receive exact authoring guidance and focused repair packets
for the one incomplete faction, while players see gameplay data without
technical materialization metadata.

**Why this priority**: A strict validator without precise prompts, examples,
and repair coordinates would create repeated repair loops and tempt broad
rewrites.

**Independent Test**: Trigger representative Mortal and Shining
materialization failures, inspect repair packets and worked examples, and
render faction views in both clients to verify bounded targeting and metadata
privacy.

**Acceptance Scenarios**:

1. **Given** a faction with one missing or contradictory section, **when** a
   repair packet is generated, **then** it names one stable faction coordinate,
   the exact target file or state subtree, the exact defect, and all valid
   sections that must be preserved.
2. **Given** a bounded repair, **when** it changes another faction, unrelated
   state, or valid authored content, **then** the repair/apply boundary rejects
   it.
3. **Given** a missing narrative faction field, **when** normalization or repair
   runs, **then** neither derives content from a name, ID, description, tag, or
   genre vocabulary.
4. **Given** a complete envelope, **when** console and browser faction views
   render, **then** schema versions, receipt IDs, disposition tokens, and
   private empty-state reasons remain hidden in ordinary player-facing output.
5. **Given** a GM authoring a supported creation, promotion, update, or repair,
   **when** the relevant guidance is followed, **then** a worked example exists
   for that route and agrees with validation.

### Edge Cases

- A same-turn Mortal creation uses one effective temporary identity before
  canonical binding; collisions with existing permanent identities fail
  closed.
- Duplicate JSON members anywhere in materialization or the validated pre-turn
  authority do not acquire a winner by property order.
- `populated` cannot legalize an empty or malformed section, and
  `empty_by_design` cannot coexist with content.
- A reason containing only whitespace is absent.
- An omitted sidecar entry is not equivalent to an exact empty sidecar.
- A structured vacant or distributed Mortal leadership state remains mandatory
  when no individual leader exists.
- Vacant Shining leadership does not require an actor profile; player-soul
  leadership uses the existing client-owned profile.
- Resident membership remains resident-owned and is not duplicated inside a
  faction merely to satisfy the materialization contract.
- A broken or dissolved Shining faction may retain historical trade receipts
  while its current trade capability is false.
- A story-hidden faction remains semantically complete; hidden visibility is
  not permission to omit authority.
- One accepted turn may combine a Shining creation route with independent
  scheduler or pending work only when every contract and receipt remains
  complete and mutually consistent.
- Loading, rendering, save/archive handling, and client-derived recomputation
  do not trigger legacy promotion.
- Chaos Sea Guardian relations, projects, influence, and chronicles never
  become Mortal or Shining faction records.

## Requirements

### Functional Requirements

- **FR-001**: Every newly created or promoted faction MUST contain exactly one
  complete, versioned, faction-bound materialization receipt with a stable
  unique materialization identity, exact faction type and identity, accepted
  turn, capability snapshot, and governed section dispositions.
- **FR-002**: The contract MUST support exactly the `mortal_faction` and
  `shining_faction` profiles and reject unknown types, members, capabilities,
  sections, duplicate members, partial receipts, and identity mismatches.
- **FR-003**: A `populated` disposition MUST require production-valid canonical
  content and forbid a reason; `empty_by_design` MUST require a meaningful
  reason and the exact canonical empty surface.
- **FR-004**: Historical materialization receipts MUST remain semantically
  unchanged after first acceptance.
- **FR-005**: Newness, promotion, and current-turn touches MUST be classified
  against exact duplicate-sensitive validated pre-turn authority rather than
  names, prose, tags, or inferred identity.
- **FR-006**: An untouched legacy faction without a receipt MUST remain loadable
  and unchanged.
- **FR-007**: Any accepted GM-authored mutation of a legacy faction MUST require
  full same-turn promotion of that exact faction.
- **FR-008**: Loading, rendering, and explicitly client-owned derived
  recomputation MUST NOT trigger promotion.
- **FR-009**: New and promoted candidate state MUST be semantically validated
  before normalization, and normalization MUST NOT create missing authored
  semantics, receipts, dispositions, reasons, or authority links.
- **FR-010**: A materialization bundle MUST be accepted atomically or rejected
  without partial persistence.
- **FR-011**: A Mortal materialization MUST always populate identity and visual
  profile, purpose, current agenda, principles, power/progression, governance,
  leadership, memory, and an initial chronicle entry.
- **FR-012**: A Mortal receipt MUST govern hierarchy, resources, relations,
  projects, territory/influence, player membership, and custom states through
  exact dispositions and corresponding capability evidence.
- **FR-013**: A Mortal bundle MUST bind one exact faction identity across core,
  structure, resources, projects, custom state, chronicles, location control,
  and NPC affiliation authority, including exact empty records where required.
- **FR-014**: The full Mortal faction carrier MUST be accepted only for genuine
  first creation or exact legacy promotion.
- **FR-015**: Ordinary existing Mortal core updates MUST use a closed
  `FactionCoreChanges` command with exact permanent identity, non-empty reason,
  absolute resulting values, reviewed core groups, protected identity and
  receipt fields, recursive unknown-member rejection, and consume-on-success
  behavior.
- **FR-016**: Existing dedicated commands MUST remain authoritative for ranks,
  bonuses, resources, projects, custom states, chronicles, location control,
  and NPC affiliations. Because the current protocol has no standalone Mortal
  relation-change command, ordinary relations MUST use the closed absolute
  `FactionCoreChanges.relations` group rather than a full-object resend or a
  second new response surface.
- **FR-017**: A new same-turn Mortal faction MUST bind its receipt and all
  same-turn references through one exact effective identity and preserve that
  binding after canonicalization.
- **FR-018**: A Shining materialization MUST always populate exact identity and
  provenance, one hall binding, charter/purpose, lifecycle, explicit
  leadership, strategic memory, an initial chronicle entry, and supported
  visibility/story authority.
- **FR-019**: A Shining receipt MUST govern projects, territorial influence,
  resource ledger, resident affiliations, trade state, leadership history, and
  story state through exact dispositions and capability evidence.
- **FR-020**: Native discovery MUST create exactly one hall, one native faction,
  two through four new ascended residents, exactly two seeded completed
  projects, matching receipt/cost evidence, and complete required actor
  authority without rewriting pre-existing unrelated state.
- **FR-021**: Player founding MUST bind the exact pending request, unique request
  identity, charter, supporters, player-soul leadership, reserved costs,
  founding receipt, hall, affiliations, history, and complete faction state.
- **FR-022**: Story-owned or hidden creation MUST require exact documented story
  authority and supported visibility while retaining every other mandatory
  semantic field.
- **FR-023**: Every applicable non-player Shining head, political actor, and
  newly significant resident MUST resolve by exact type and identity to
  complete Actor Materialization authority; vacancy and player-soul leadership
  retain their explicit exceptions.
- **FR-024**: Shining resident membership MUST remain resident-owned, and every
  faction MUST bind by exact identity to one existing or same-turn hall.
- **FR-025**: Faction strength, derived tier, and service multiplier MUST remain
  client-owned projections and MUST NOT grant authored semantic authority.
- **FR-026**: Trade capability MUST use exact existing operational lifecycle,
  leadership, derived-tier, and realm-local evidence rather than prose.
- **FR-027**: Validation and repair MUST use stable
  `mortal_faction:<id>` and `shining_faction:<id>` coordinates plus stable issue
  families for missing/invalid receipts, identity, sections, dispositions,
  capabilities, bundle links, required promotion, and forbidden full resends.
- **FR-028**: Repair packets MUST identify exact target files or state subtrees,
  exact defects, allowed correction shapes, required links, preserved valid
  sections, and prohibited unrelated roots.
- **FR-029**: Repair and normalization MUST NOT invent narrative or mechanical
  faction content from names, descriptions, IDs, tags, or genre vocabulary.
- **FR-030**: Ordinary console and browser views MUST hide materialization
  schema, IDs, disposition tokens, and private reasons while preserving legacy
  faction readability.
- **FR-031**: GM prompts, Mortal faction rules, Shining contract guidance,
  worked examples, example manifests, documentation tests, and source guards
  MUST remain synchronized with the runtime contract.
- **FR-032**: Worked guidance MUST cover populated and deliberately minimal
  Mortal creation, Mortal promotion, `FactionCoreChanges`, all three Shining
  creation routes, and bounded repair in both domains.
- **FR-033**: Chaos Sea Guardian politics MUST remain actor/living-world
  authority under #1500 and #1368 and MUST NOT be represented as a Mortal or
  Shining faction.
- **FR-034**: This feature MUST NOT implement the #1222 faction-content worker,
  autonomous scheduling, #1462 living-world behavior, or player-facing UI
  redesign.
- **FR-035**: Verification MUST use bounded focused development checks, one Fast
  checkpoint, conditional afterlife contract validation, and one clean-checkout
  PreMerge without increasing current timeouts or concurrency.

### Key Entities

- **Faction Materialization Receipt**: Immutable proof of one faction's first
  complete accepted state, including exact identity, domain profile,
  capabilities, and section dispositions.
- **Section Disposition**: Declares one governed section populated or
  deliberately empty and binds that declaration to canonical content.
- **Mortal Faction Bundle**: The exact-ID union of faction core, sidecars,
  memory, location-control, and NPC-affiliation authority.
- **Shining Faction**: Hall-bound afterlife political entity with provenance,
  charter, lifecycle, leadership, memory, visibility, projects, affiliations,
  political state, trade, and receipts.
- **Faction Touch Classification**: New, legacy promotion, already materialized,
  client-derived-only, or untouched legacy state determined from validated
  pre-turn authority.
- **Faction Core Change**: Narrow ordinary-existing Mortal command for reviewed
  core groups that cannot mutate identity, receipt, or dedicated-command
  domains.
- **Faction Repair Packet**: Stable-coordinate correction request constrained
  to the exact missing or contradictory faction surfaces.

## Success Criteria

### Measurable Outcomes

- **SC-001**: All supported new Mortal and Shining creation scenarios reject
  every missing mandatory semantic group and accept every reviewed complete
  example.
- **SC-002**: A minimal Mortal faction with seven governed empty sections is
  accepted only when all seven exact empty surfaces and seven meaningful
  reasons are present.
- **SC-003**: All three Shining creation routes pass independently, including
  native discovery with exactly one hall, one faction, two through four
  residents, and two completed seed projects.
- **SC-004**: In legacy compatibility scenarios, 100% of untouched legacy
  factions load unchanged, while 100% of GM-mutated legacy factions without a
  complete promotion are rejected.
- **SC-005**: In ordinary-update scenarios, 100% of full-object resends for
  already materialized factions are rejected and all supported narrow updates
  preserve unrelated faction data and the historical receipt.
- **SC-006**: Every representative materialization failure produces one stable
  faction coordinate and a repair scope containing no unrelated faction or
  state root.
- **SC-007**: Eight reviewed worked-example families cover both Mortal creation
  forms, Mortal promotion, Mortal core update, all three Shining routes, and
  bounded repair in both domains.
- **SC-008**: Ordinary console and browser faction views expose zero private
  materialization field names or reason text.
- **SC-009**: No accepted test scenario derives faction semantics or capability
  from prose, names, IDs, tags, or genre vocabulary.
- **SC-010**: The standard development and merge controls complete within their
  existing bounded lane limits, with zero failed tests, duplicate test IDs,
  timeouts, or owned-process cleanup failures.

## Verification Plan

- **C# verification**: Use
  `pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionMaterializationContractTests|FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~FactionCoreChangesContractTests|FullyQualifiedName~FactionCoreChangesTests|FullyQualifiedName~CanonicalStateNormalizerTests"`
  during RED/GREEN work, then one
  `pwsh .\scripts\test-csharp.ps1 -Lane Fast` checkpoint and one clean-checkout
  `pwsh .\scripts\test-csharp.ps1 -Lane PreMerge` before integration.
- **Documentation/contract verification**: Run
  `pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"`
  and the relevant Mortal/example source guards. Because afterlife guidance and
  examples change, run one bounded
  `pwsh .\scripts\test-csharp.ps1 -Lane FullValidation` before final
  integration.
- **Frontend verification**: No frontend behavior is planned. Existing focused
  console/browser metadata-privacy tests prove readers remain compatible; run
  frontend verification only if implementation discovers an actual frontend
  source change, and update the plan first.
- **Manual/player-facing verification**: Inspect representative Mortal and
  Shining faction detail output in console and browser fixtures and confirm
  private receipt fields and reasons are absent.

## Assumptions

- Actor Materialization from #1500 is canonical and available for all Shining
  actor cross-links required by this feature.
- Existing Mortal faction sidecars and Shining state remain the canonical
  storage locations; no global faction-materialization registry is added.
- Existing dedicated Mortal and Shining mutation commands remain authoritative
  unless this spec explicitly introduces `FactionCoreChanges`.
- The materialization receipt records the accepted first-complete state and is
  immutable during ordinary later gameplay updates.
- Exact client-owned derived values remain recalculable without GM authorship
  and without legacy promotion.
- Existing repair and rollback infrastructure is reused; #1510 extends its
  faction issue and packet vocabulary but does not implement the #1222 worker.
- The approved design at
  `docs/superpowers/specs/2026-08-03-faction-materialization-design.md` is the
  product-decision source for this feature.
