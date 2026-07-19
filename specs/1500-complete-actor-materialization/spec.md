# Feature Specification: Complete Actor Materialization

**Feature Branch**: `1500-complete-actor-materialization`

**Created**: 2026-07-18

**Status**: Approved

**Input**: Prevent technically valid but hollow Mortal and afterlife actors by enforcing a setting-agnostic materialization contract.

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500); related startup findings [#1446](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1446) and [#1461](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1461).
- **Issue type**: Harness/RLM enhancement, validation hardening, canonical-state contract.
- **Spec Kit justification**: The change spans Mortal NPC state, Chaos Sea and Shining Abode actors, validation, cross-file authority, repair packets, client-owned fresh-game seeds, GM prompts, documentation, examples, manifests, and tests.
- **Contract scope**: GM-facing prompts; runtime state; validation; normalization preservation; repair loop; Mortal World; Chaos Sea; Shining Abode; documentation; worked examples; source guards. Console and browser remain readers of canonical state and must not expose contract metadata as player-facing prose.
- **Out of scope**: Inventing actor content in the client; deriving professions, skills, items, roles, or capabilities from prose; forcing every actor to own an item or Fate Card when fiction explicitly says otherwise; redesigning NPC/afterlife player-facing screens; adding Mortal inventory to afterlife entities; retroactively fabricating content for untouched legacy actors.

## User Scenarios & Testing

### User Story 1 - Complete Mortal NPCs (Priority: P1)

As a player, when a significant Mortal NPC first appears, I can later inspect a coherent person rather than an identity stub: the NPC has personality, characteristics, goals, memory ownership, and explicit skill, possession, Fate Card, quest, and relationship state.

**Why this priority**: Hollow Mortal NPCs directly break dialogue, combat, training, trade, progression, and Actor Brain behavior.

**Independent Test**: Submit first-materialization NPC objects through `NPCsInScene` and `UpdateNPCs`. Confirm that populated profiles pass, accidental empty sections fail, and deliberately empty sections pass only with structured reasons.

**Acceptance Scenarios**:

1. **Given** a new Mortal NPC with non-empty characteristics and personality but empty skills, inventory, Fate Cards, personal quests, and relationships, **when** no materialization section dispositions are supplied, **then** validation blocks the turn and requests only the missing dispositions or content.
2. **Given** an itemless non-combatant NPC whose empty skill and possession sections are explicitly declared `empty_by_design` with non-empty in-world reasons, **when** the remaining required profile sections are complete, **then** validation accepts the materialization.
3. **Given** a new NPC explicitly declared able to teach, trade, or fight, **when** the corresponding canonical teacher, merchant, or skill authority is absent, **then** validation blocks the materialization without parsing the NPC name, occupation, history, tags, or narrative.
4. **Given** an existing NPC, **when** the GM changes inventory, skills, relationships, activity, or memory, **then** existing dedicated delta commands remain authoritative and the full materialization envelope is not resent.
5. **Given** a first-materialization Mortal NPC with `characteristics={}`, **when** its complete object is validated, **then** validation reports `npc_characteristics_empty`; at least one setting-defined numeric property is required without prescribing characteristic names.
6. **Given** a legacy Mortal NPC receives its first complete envelope during a structured promotion, **when** the schema-required `UpdateNPCs.inventory` differs from validated pre-turn inventory, **then** validation rejects the resend; the same promotion passes when the inventory snapshot is semantically unchanged and all mutations use dedicated atomic commands.

---

### User Story 2 - Complete Afterlife Actors (Priority: P1)

As a player, when a Guardian, resident, radiant actor, Shining faction head, Saref agent, or other significant afterlife entity appears, its identity resolves to a complete afterlife profile with spiritual progression, agency, relationships, Fate Cards, custom states, and actor-owned memory.

**Why this priority**: Afterlife actor data is currently fragmented across strong type-specific files and a partially optional common profile, allowing important entities to exist without inspectable or actionable authority.

**Independent Test**: Materialize each supported afterlife actor type and verify cross-file profile binding, section completeness, Actor Brain inputs, memory initialization, and bounded repair behavior in Chaos Sea and Shining Abode.

**Acceptance Scenarios**:

1. **Given** a newly significant afterlife entity, **when** no matching `afterlife_entity_profiles.json` profile is created, **then** the accepted turn is blocked and repair identifies the exact missing actor/profile link.
2. **Given** a new afterlife profile with currencies, progression, and arts but absent Fate Cards, relationships, agency, custom states, or progression history, **when** no explicit section dispositions exist, **then** validation blocks the profile as incomplete.
3. **Given** a profile section that is legitimately empty, **when** its materialization disposition is `empty_by_design` with a non-empty reason, **then** the profile can be accepted without client-authored narrative invention.
4. **Given** non-vacant Shining leadership, **when** its Guardian, resident, or radiant head lacks matching complete actor-profile authority, **then** cross-file validation rejects the leadership state or requests profile materialization.
5. **Given** a client-owned system Guardian created during New Game, **when** its canonical state is seeded, **then** the client deterministically writes a complete materialization envelope and does not ask the GM to replace it.
6. **Given** a new afterlife profile whose agency disposition is `populated`, **when** it contains only a mask or progression strategy without a goal, personal quest, current activity, or completed activity, **then** validation rejects the agency disposition.
7. **Given** an afterlife actor declares `canTrade=true`, **when** it is neither the exact active Guardian in the current Chaos Sea abode nor an operational secure/contested Shining faction head at trade tier 1 or higher, **then** validation fails closed without reading names, roles, or prose.

---

### User Story 3 - Safe Legacy Adoption and Repair (Priority: P2)

As a returning player, I can load an older save without the client silently inventing biographies, skills, inventory, or spiritual powers, while newly created or newly promoted significant actors must use the current contract.

**Why this priority**: Strict unconditional validation of every historical actor would make valid existing saves unusable; silently filling them would violate GM authority.

**Independent Test**: Load legacy files without materialization metadata, verify unchanged actors remain readable, then touch or promote one actor and confirm the repair loop requests only the missing current-contract sections.

**Acceptance Scenarios**:

1. **Given** an untouched legacy actor without a materialization envelope, **when** a save is loaded and the actor is not newly created or promoted in the current turn, **then** validation does not fabricate data or destroy the save.
2. **Given** that legacy actor becomes newly relevant through a first canonical profile promotion, teacher/merchant enablement, or Shining leadership appointment, **when** the turn is validated, **then** current-contract materialization is required.
3. **Given** a repair packet for an incomplete actor, **when** some sections are already valid, **then** repair preserves them and names only missing or contradictory sections.
4. **Given** a worker proposes a bounded actor repair, **when** the proposal also changes personality, another section, another actor, or unrelated root state, **then** the apply gate rejects the proposal before canonical files are written.
5. **Given** the canonical Guardian thought journal does not exist and one exact `guardian:<id>` memory-missing issue is routed to it, **when** a completed proposal uses `changeKind=add`, `beforeSha256=missing`, exact proposal-bound content, and one fresh owned entry under `{ "entries": [...] }`, **then** the apply gate accepts creation; wrong-owner or extra-root data remains rejected.

---

### User Story 4 - GM and Client Contract Clarity (Priority: P2)

As the GM agent, I receive one bounded, setting-agnostic authoring contract and worked examples for Mortal and afterlife actors; as a player, I never see its implementation metadata.

**Why this priority**: Validator-only behavior would create repair loops unless prompts and examples teach the same contract, while exposing raw metadata would degrade both clients.

**Independent Test**: Run documentation/source guards, inspect generated repair packets, and render representative actor details in console/browser tests to confirm metadata stays internal.

**Acceptance Scenarios**:

1. **Given** a first actor materialization request, **when** the GM reads the context packet, **then** it receives the correct type-specific template and no genre-specific keyword guidance.
2. **Given** a validation failure, **when** a repair packet is generated, **then** it contains actor identity, missing section names, allowed dispositions, and canonical target surfaces without asking for unrelated rewrites.
3. **Given** a complete materialization envelope in canonical JSON, **when** console and browser detail views render the actor, **then** they display gameplay data but not schema versions, receipt IDs, section-state tokens, or empty-state implementation labels.

### Edge Cases

- An actor can legitimately be itemless, non-combatant, unable to teach, unable to trade, without current quests, or without discovered Fate Cards. Each governed empty section requires an explicit in-world reason; absence or an empty string is not equivalent.
- A `populated` section must contain structurally valid data. A disposition cannot launder an empty or malformed section.
- `empty_by_design` conflicts with non-empty content and is rejected rather than normalized away.
- Capabilities are explicit booleans and must agree with canonical fields. `canTeach=true` requires teacher authority; `canTrade=true` requires merchant authority; Mortal `canFight=true` requires an active or passive skill; afterlife `canFight=true` requires at least one usable standard or special art.
- Arbitrary settings are supported. Validation never searches for fantasy, science-fiction, historical, modern, or post-apocalyptic words in names, occupations, descriptions, item labels, skill labels, or IDs.
- A background mention is not automatically a significant actor. Significance begins when the actor is canonically created, placed in relevant Actor Brain scope, receives a persistent update, becomes a teacher/merchant/combat participant, or occupies a supported political/leadership role.
- Vacant Shining leadership requires no head profile. A player-soul head resolves to the existing client-owned player profile.
- Guardians and residents retain their type-specific canonical files; the common profile complements rather than replaces those files.
- Afterlife possessions are not Mortal inventory. Trade stock, offerings, relic links, and mentor showcases remain their existing explicit capability surfaces.
- Legacy files are accepted only as unchanged baseline authority. A current-turn new actor cannot omit metadata by presenting a permanent-looking ID.
- A legacy Mortal promotion may repeat only an inventory snapshot semantically equal to validated pre-turn authority; changed, added, or removed inventory entries remain atomic-command-only.
- A missing Guardian thought journal is treated as `{ "entries": [] }` only inside the exact issue-bound Add path. Other missing actor files, owners, issue codes, or proposed roots remain fail-closed.
- Generated worker audit event IDs remain readable but must be unique even when multiple helper calls occur in the same millisecond.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST define a versioned `materialization` envelope shared conceptually by new Mortal NPC objects and new afterlife entity profiles.
- **FR-002**: The envelope MUST contain exact `actorType` and `actorId` bindings, a stable `materializationId`, `materializedAtTurn`, `schemaVersion`, `state=complete`, explicit capability booleans, and per-section dispositions.
- **FR-003**: A governed section disposition MUST be either `populated` or `empty_by_design`; the latter MUST include a non-empty in-world reason.
- **FR-004**: Validation MUST reject a missing disposition, a `populated` disposition with empty content, an `empty_by_design` disposition with content, unknown section keys, duplicate authority, and malformed envelope fields.
- **FR-005**: Mortal first materialization MUST govern combined skills, inventory/equipment, Fate Cards, personal quests, and NPC relationships through dispositions while continuing to require existing core identity, personality, characteristics, goals, progression, location, and memory fields. `characteristics` MUST contain at least one numeric property, but the validator MUST NOT prescribe setting-specific characteristic names.
- **FR-006**: A Mortal NPC with `canFight=true` MUST have at least one valid active or passive skill.
- **FR-007**: A Mortal NPC with `canTeach=true` MUST have canonical `teacherProfile.canTeach=true` and non-empty teacher skill authority.
- **FR-008**: A Mortal NPC with `canTrade=true` MUST have canonical merchant/trade authority; initial stock may remain pending only through the existing explicit trade request contract.
- **FR-009**: A Mortal NPC with `ownsItems=true` MUST have non-empty canonical inventory, and equipped item references MUST resolve to that inventory.
- **FR-010**: Existing Mortal NPCs MUST continue to mutate through dedicated delta commands; validators MUST forbid using the first-materialization envelope to bypass existing update contracts. A legacy promotion MAY include the schema-required inventory snapshot only when it is semantically identical to validated pre-turn inventory; any add, removal, or change through `UpdateNPCs.inventory` MUST be rejected.
- **FR-011**: A newly significant non-player afterlife actor MUST have a matching common afterlife entity profile unless its actor type is explicitly client-owned and documented as equivalent authority.
- **FR-012**: New afterlife profiles MUST govern standard arts, special arts, custom states, Fate Cards, relationships, actor agency, and progression history through dispositions and MUST initialize actor-owned memory. A populated agency section MUST contain meaningful goals, personal quests, a current activity, or completed activity history; masks, disposition, or progression strategy alone MUST NOT satisfy agency.
- **FR-013**: An afterlife actor with `canFight=true` MUST have at least one usable standard or special spiritual art.
- **FR-014**: An afterlife actor with `canTeach=true` MUST have canonical mentor authority and at least one teachable art or explicit supported teaching surface.
- **FR-015**: An afterlife actor with `canTrade=true` MUST resolve to exact current realm authority: the one active Guardian in the current Chaos Sea abode, or a non-player secure/contested head of an operational Shining faction at trade tier 1 or higher. Mortal NPC trade files MUST remain forbidden in afterlife realms, and names, roles, descriptions, or genre vocabulary MUST NOT create authority.
- **FR-016**: Cross-file validation MUST bind Guardians, residents, Shining political actors, and non-vacant Shining faction heads to exact actor type and stable ID profile authority.
- **FR-017**: Actor Brain inputs and actor-owned memory MUST be initialized on first materialization; later significant decisions remain append-only under existing memory rules.
- **FR-018**: The client MUST determine new/current-turn materialization from validated pre-turn authority and current structured commands, not from display names, ID prefixes, prose, or current pathname state alone.
- **FR-019**: Untouched legacy actors MUST remain loadable without invented data. Current-turn creation, promotion, or newly significant role assignment MUST trigger the current contract.
- **FR-020**: Repair packets MUST preserve valid actor data and report only missing, empty, contradictory, or unbound sections for the exact stable actor identity.
- **FR-020a**: The worker apply gate MUST compare a proposed actor repair with canonical JSON and MUST reject protected actor data changes outside the exact actor and named repair section. The only missing-baseline actor-memory creation exception is an exact Guardian thought journal Add routed solely by `afterlife_actor_materialization_memory_missing` for one exact `guardian:<id>`; preservation treats that baseline as `{ "entries": [] }` and accepts exactly one fresh meaningful owned entry.
- **FR-020b**: Validation-repair proposals MUST pin exact canonical bytes with 64-character SHA-256 context/before hashes, bind every non-delete result to its own proposal-scoped content and exact after hash, and apply or roll back through one cross-process compare/exchange write protocol so a concurrent canonical writer cannot be overwritten.
- **FR-020c**: Worker dispatch MUST happen before exposing the legacy main-GM repair request. Once the apply gate accepts a worker repair, the worker remains the sole repair owner even if ready-signal publication fails; the client MUST revalidate directly and MUST NOT start a second legacy repair. Player-facing output freshness MUST use an explicit repair boundary rather than depend on a legacy request file.
- **FR-020d**: Only a worker proposal with `status=completed` MAY enter the apply gate. `failed`, `timed-out`, and `rejected` proposals MUST carry an empty `changedFiles` collection, remain diagnostic evidence only, and MUST NOT mutate canonical state.
- **FR-020e**: Worker audit appends MUST serialize their read-and-append operation under the shared cross-process canonical-write lock. Generated audit event IDs MUST remain unique under concurrent or same-millisecond helper calls. Failure to publish audit telemetry after a canonical apply is accepted MUST NOT roll back or revoke the accepted canonical bytes.
- **FR-021**: Normalization MUST preserve valid materialization metadata but MUST NOT generate narrative reasons, capabilities, skills, inventory, Fate Cards, goals, relationships, or profile content.
- **FR-022**: System Guardian fresh-game builders MUST emit deterministic valid envelopes for their client-owned Guardian/profile seeds, and every capability boolean, including `canTrade`, MUST match the exact current seeded authority.
- **FR-023**: GM prompts, Mortal NPC documentation, afterlife contract documentation, worked examples, manifests, and source/documentation guards MUST be updated in the same change.
- **FR-024**: Player-facing console and browser projections MUST ignore materialization metadata unless an explicit advanced/debug mode requests it.
- **FR-025**: Tests and source guards MUST prove that no materialization decision uses genre-specific keyword dictionaries or prose inference.

### Key Entities

- **Actor Materialization Envelope**: Versioned proof that a first canonical actor creation addressed every governed content section and declared gameplay capabilities explicitly.
- **Section Disposition**: `populated` or `empty_by_design` state for one governed actor section, with a required reason for deliberate emptiness.
- **Actor Capabilities**: Explicit setting-neutral booleans for combat, teaching, trade, and possessions; these constrain but never generate canonical content.
- **Mortal NPC Materialization**: Existing complete NPC object plus semantic section dispositions and capability consistency.
- **Afterlife Entity Profile Materialization**: Common profile for Guardian, resident, Shining resident/head, radiant actor, Saref/system actor, player soul, or custom afterlife actor plus type-specific cross-file links.
- **Materialization Repair Packet**: Bounded GM task describing exact actor identity and only the missing or contradictory sections.

## Success Criteria

### Measurable Outcomes

- **SC-001**: All focused tests reject 100% of new Mortal NPC fixtures that omit governed content without explicit empty-state reasons.
- **SC-002**: All supported valid minimal Mortal NPC fixtures pass without requiring a genre-specific skill, item, profession, or Fate Card.
- **SC-003**: New Guardian, resident, radiant actor, and Shining faction-head fixtures cannot pass without exact common-profile binding or an explicitly documented client-owned equivalent.
- **SC-004**: Existing unchanged legacy fixtures remain valid and no test observes client-authored narrative content added during normalization or repair preparation.
- **SC-005**: Repair tests show that already valid actor sections remain semantically equivalent, only the exact named repair subtree may change, stale-byte proposals and concurrent overwrite/rollback attempts are rejected, non-completed proposals cannot enter apply, audit append races lose no events, audit publication failure cannot revoke an accepted apply, and proposals that alter protected actor data are rejected before apply.
- **SC-006**: Mortal World, Chaos Sea, and Shining Abode worked examples pass documentation validation and demonstrate at least one populated and one deliberately empty section.
- **SC-007**: Source guards find no actor materialization branch based on genre-specific names, descriptions, occupations, tags, labels, IDs, or keyword tables.
- **SC-008**: Existing console/browser actor detail tests continue to pass without rendering materialization metadata in player mode.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ActorMaterialization|NpcFullObject|AfterlifeEntityProfile|GuardianAbodeResident|ShiningLeadership|SystemGuardianLibrary|GmWorker|ValidationRepair"`
- **Documentation/contract verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|ValidationSourceGuardTests|GameEngineSourceGuardTests"`
- **Frontend verification**: Existing browser projection tests affected by actor DTO changes; run `npm run verify` only if frontend source changes.
- **Manual/player-facing verification**: Fresh Mortal opening with populated actor; intentionally minimal itemless actor; Chaos Sea Guardian/resident creation; Shining faction-head appointment; inspect `/нпс` and `/профили_загробья` to confirm no metadata leakage.

## Assumptions

- “Complete” means every governed section is intentionally addressed, not that every section must contain an entry.
- The GM remains semantic authority for all narrative actor content and reasons.
- Existing explicit teacher, merchant, combat, inventory, Guardian, resident, and Shining leadership contracts remain authoritative and are cross-validated rather than replaced.
- Actor materialization metadata is private canonical/harness authority and is not inherently player-facing.
- Player soul remains client-owned for currencies, progression, and ordinary spiritual arts; its existing profile is not forced through non-player requirements.
