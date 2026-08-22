# Feature Specification: Unified Resource Authority

**Feature Branch**: `1535-effect-materialization`

**Created**: 2026-08-15

**Status**: Approved for planning

**Input**: User description: "Replace the fragile collection of player, NPC, combat, item, afterlife, and effect-specific health, energy, poise, charge, ammunition, durability, action-economy, and spiritual-reserve fields with one complete resource materialization model. Do not migrate technical Pre-Alpha saves. Do not reject existing mechanics merely because they are difficult; model them now so Effect Task 9 can use the same authority."

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1543 — Materialize one canonical resource authority](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543); blocked consumer [#1535 — Enforce complete effect materialization across Mortal World and afterlife](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
- **Issue type**: P1 cross-domain canonical-state epic and blocking feature
- **Spec Kit justification**: The feature replaces canonical resource authority across Mortal World, afterlife, combat, items, effects, validation, normalization, rollback, player projections, GM contracts, examples, and multiple implementation sessions.
- **Contract scope**: Player-facing console and browser; GM-facing Mortal and afterlife prompts and examples; runtime state; validation; normalization; owner lifecycle; accepted-turn planning; pending resolution; repair and rollback; documentation; manifests; active templates and fixtures.
- **Save compatibility**: Not required. The game is technical Pre-Alpha; active templates, examples, and fixtures switch directly to the new authority, and old saves are rejected rather than migrated, promoted, or dual-read.
- **Out of scope**: Money, currencies, treasuries, faction accounting ledgers, market balances, and general economic transactions; changing the fiction or balance of existing mechanics; lossy history compaction; in-place semantic revision of sealed resource definitions; general combat or UI redesign unrelated to the authority cutover.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One Trustworthy Resource Value (Priority: P1)

As a player, I want health, energy, poise, charges, durability, action economy, and similar bounded quantities to have one accepted value so that different actions and screens cannot disagree about my current state.

**Why this priority**: Every ordinary action, effect, item operation, and afterlife operation depends on the same value, bounds, owner, and replay evidence.

**Independent Test**: Materialize built-in and setting-defined resources, initialize one value for each supported owner kind, and prove duplicate, ambiguous, direct, malformed, or out-of-policy state is rejected before any accepted value changes.

**Acceptance Scenarios**:

1. **Given** a complete accepted definition and an exact resource owner, **When** the resource is initialized, **Then** one bounded current value and capacity exist at one exact coordinate.
2. **Given** duplicate, case-variant, Unicode-confusable, unknown, cross-realm, or display-name-only identity, **When** materialization is attempted, **Then** no definition or state is accepted.
3. **Given** an already accepted value, **When** a GM response attempts to write the value, capacity, chronology, or replay identity directly, **Then** the complete turn fails without changing accepted state.
4. **Given** a setting-defined resource, **When** its complete policy is materialized through the dedicated route, **Then** later ordinary turns may use it but may not rewrite its sealed definition.

---

### User Story 2 - Deterministic Ordinary Resource Changes (Priority: P1)

As a player, I want damage, healing, recovery, costs, spending, restoration, and gains to be ordered, bounded, and applied exactly once so that retries or simultaneous mechanics cannot create different outcomes.

**Why this priority**: A shared value is not trustworthy unless every source uses the same arithmetic, ordering, capacity, and replay rules.

**Independent Test**: Apply representative ordinary operations at minimum, maximum, quantum, overflow, replay, and non-commutative ordering boundaries and verify one deterministic final value and immutable transition evidence.

**Acceptance Scenarios**:

1. **Given** several authorized mutations in one turn, **When** they are accepted, **Then** they execute in one stable client-owned order and each observes the prior applied result.
2. **Given** the same accepted event and operation is retried, **When** it is semantically identical, **Then** it produces no duplicate transition; when its semantics differ, the complete turn fails.
3. **Given** an operation that crosses a bound, **When** its governing policy authorizes clamping, **Then** the exact clamped result is recorded; otherwise the complete turn fails.
4. **Given** an integer or decimal definition with an exact quantum, **When** a mutation cannot be represented exactly, **Then** it is rejected without rounding.
5. **Given** one invalid mutation among valid siblings, **When** the turn is evaluated, **Then** none of the sibling changes are published.

---

### User Story 3 - Complete Owner Lifecycle Across the Game (Priority: P1)

As a player, I want the same resource to follow the same logical actor or item through combat, movement, ownership changes, and afterlife scopes so that entering another subsystem does not copy, reset, or orphan it.

**Why this priority**: Existing mechanics use different owner shapes and are the main source of duplicated or positional authority.

**Independent Test**: Exercise player, named NPC, named NPC in combat, anonymous combatant, combat group member, item, persistent afterlife actor, and scoped afterlife participant creation and retirement while proving exact continuity and cleanup.

**Acceptance Scenarios**:

1. **Given** a named NPC with accepted health, **When** that NPC enters and leaves combat, **Then** combat references the same resource owner and no combat-local health copy becomes authoritative.
2. **Given** an anonymous combatant or group member, **When** it is materialized, **Then** the client creates a stable owner identity before any resource mutation and never uses list position as identity.
3. **Given** a vehicle with accepted health, **When** it is updated, activated, parked, moved, or destroyed, **Then** health remains attached to the permanent vehicle identity and cannot be replaced through `UpdateVehicles`.
4. **Given** an item with charges, ammunition, durability, or another bounded reserve, **When** the item moves between valid carriers, **Then** the resource remains attached to the permanent item identity without duplication.
5. **Given** a persistent afterlife actor and a conflict-scoped participant, **When** the conflict closes, **Then** only the scoped resource owner is terminally retired while persistent actor resources remain.
6. **Given** owner destruction or terminal consumption, **When** cleanup is authorized, **Then** terminal history is prepared and no live orphan state remains.

---

### User Story 4 - Effects Use the Same Resource Model (Priority: P1)

As a player, I want periodic damage, restoration, resource-event triggers, and bounded story resolutions to use the same resource rules as ordinary actions so that effects cannot bypass costs, bounds, owners, or replay protection.

**Why this priority**: Issue #1543 exists specifically to remove the fragile effect-only adapter approach and unblock Effect Task 9 with a complete shared model.

**Independent Test**: Compare an ordinary mutation and an equivalent effect-generated mutation, then resolve a bounded story receipt, and verify identical arithmetic, authority, ordering, replay, and atomicity.

**Acceptance Scenarios**:

1. **Given** an accepted periodic damage or restore component, **When** its trigger becomes due, **Then** it produces the same authorized mutation and result rules as an ordinary operation.
2. **Given** a resource result emits a registered event, **When** an effect trigger depends on that event, **Then** the finite dependency graph executes in stable order against the same working state.
3. **Given** a trigger cycle or expansion beyond the registered limit, **When** the graph is evaluated, **Then** the complete transition fails before publication.
4. **Given** a story-facing effect whose deterministic prerequisites pass, **When** GM judgment is required, **Then** canonical mechanics remain unchanged until one bounded client-owned request is resolved.
5. **Given** a valid bounded receipt, **When** the full turn is resubmitted, **Then** the client resolves its protected target and operation, converts it into an ordinary mutation, and consumes it exactly once.

---

### User Story 5 - Safe and Consistent Player/GM Projections (Priority: P2)

As a player, I want console and browser views to show the same understandable resource state and recent visible changes without exposing internal IDs, paths, receipts, or hidden mechanics.

**Why this priority**: Removing legacy persisted mirrors must not make status, combat, item, or afterlife views unusable or turn them into debug surfaces.

**Independent Test**: Render representative visible, owner-visible, GM-only, hidden, missing, and malformed resource sets through console, browser, and GM context projections and compare safe semantic output.

**Acceptance Scenarios**:

1. **Given** a player-visible resource, **When** status is rendered, **Then** console and browser show equivalent localized name, current value, maximum, percentage when meaningful, and safe recent delta.
2. **Given** an item or actor resource, **When** the corresponding detail view is rendered, **Then** it uses the accepted resource projection rather than a persisted legacy mirror.
3. **Given** a hidden or GM-only resource, **When** an ordinary player surface is serialized, **Then** its existence, value, delta, and internal coordinate remain absent.
4. **Given** malformed or unaccepted resource state, **When** a player surface is built, **Then** it fails closed to a generic in-world unavailable state rather than exposing raw fallback data.
5. **Given** GM context, **When** resources are described, **Then** it includes only the bounded authoring information needed for legal commands and never grants authority to write current values or protected evidence.

---

### User Story 6 - Atomic Failure, Repair, and Breaking Cutover (Priority: P2)

As a player, I want a failed resource/effect transition to restore the complete pre-turn state and suppress stale output so that partial mechanics never survive.

**Why this priority**: The feature changes several owners and companions in one accepted turn and deliberately removes all compatibility authorities.

**Independent Test**: Inject failures before, during, and after publication across resource, effect, pending, owner, and output paths; verify byte-and-existence-exact rollback, no stale narration, and bounded repair behavior.

**Acceptance Scenarios**:

1. **Given** a write, validation, stale-input, or post-validation failure, **When** publication aborts, **Then** every touched path and prior absence is restored exactly.
2. **Given** a protected identity, replay, ambiguity, arithmetic, cycle, owner, or direct-mutation failure, **When** repair handling runs, **Then** no actionable GM repair authority is granted.
3. **Given** one unambiguous GM-owned omission, **When** repair is appropriate, **Then** only that bounded semantic omission is requested and a coherent full-turn resubmission is required.
4. **Given** an old technical save containing any removed legacy resource authority, **When** it is loaded or validated, **Then** it is rejected as incompatible rather than migrated or partially interpreted.
5. **Given** the completed feature, **When** active templates, examples, and fixtures are inspected, **Then** no included mechanic retains a second persisted resource value or write route.

### Edge Cases

- A decimal operation is finite but exceeds supported numeric range, has excessive precision, or does not align to the exact quantum.
- A capacity reduction falls below current value with no disposition, an invalid disposition, or an inexact ratio result.
- Two definitions or owners differ only by case, normalization, or a Unicode confusable.
- The same event, operation, transition, or receipt identity is reused with identical versus different semantics.
- A named NPC appears both as an NPC and combatant, or two combatants claim the same named NPC binding.
- A vehicle is created with a temporary identity, partially updated beside a health mutation, activated/parked, or removed while nonterminal health remains.
- A group changes order, loses one member, adds a member, or submits a stale positional health array.
- An item moves, stacks, splits, merges, is destroyed, or is consumed while a resource mutation is also pending.
- A source or target is created in the same accepted turn and referenced through its exact temporary authority.
- A resource owner changes realm or a scoped owner reaches terminal lifecycle.
- Ordered clamp behavior produces a different result than summing mutations first.
- A registered resource event causes nested effect triggers, a cycle, or excessive graph expansion.
- A pending receipt is missing, partial, stale, repeated, cross-target, wrong-operation, over-bound, or accompanied by extra operands.
- A protected file changes after validation but before publication.
- A rollback target existed before the turn versus was newly created during the turn.
- A player projection receives hidden, GM-only, malformed, stale, or unavailable resource authority.
- Mortal and afterlife GM examples must demonstrate complete definition creation, ordinary mutation, owner lifecycle, bounded receipt, and no direct canonical writes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST maintain exactly one accepted definition for each exact, case-sensitive, confusable-unique resource key.
- **FR-002**: Each definition MUST state its numeric kind, unit, quantum, minimum policy, capacity policy, allowed owner kinds, allowed operations, default floor/cap policies, visibility, and client-owned materialization evidence.
- **FR-003**: Definition catalogs, policy catalogs, operation catalogs, owner-kind catalogs, visibility catalogs, and registered formulas MUST be closed.
- **FR-004**: A registered formula MUST refer only to a client-known formula and MUST NOT contain executable text, expressions, file paths, or GM-selected method names.
- **FR-005**: Built-in definitions MUST exist in every active new-game state that can use them.
- **FR-006**: A setting-defined resource MUST be created only through a dedicated bounded materialization route and MUST be sealed by the client.
- **FR-007**: An existing sealed definition MUST NOT be rewritten by an ordinary turn; semantic revision requires a separately specified version and reconciliation feature.
- **FR-008**: The system MUST maintain exactly one state entry per realm, owner kind, stable owner identity, and resource key.
- **FR-009**: Current value, maximum, capacity binding, lifecycle state, and chronology MUST satisfy the exact accepted definition and owner authority.
- **FR-010**: Resource arithmetic MUST use exact decimal semantics, reject non-finite or unsupported values, and never use binary floating-point rounding as authority.
- **FR-011**: Integer resources MUST remain integral and every resource value and mutation MUST align exactly to its quantum.
- **FR-012**: Each live state MUST be `active` or `suspended`; terminal cleanup MUST leave immutable history and no orphan live state.
- **FR-013**: The current Mortal player MUST use the stable owner `player_current`.
- **FR-014**: A named Mortal NPC MUST use its permanent NPC identity inside and outside combat.
- **FR-015**: An anonymous individual combatant MUST use a client-owned stable combatant identity.
- **FR-016**: Every combat-group member MUST use a client-owned stable member identity across nested-group and detached top-level combat carriers; array position, carrier shape, or display name MUST NOT be authority.
- **FR-017**: An item resource MUST use the permanent item identity and remain unchanged in identity across valid carrier movement.
- **FR-018**: Persistent afterlife actors MUST use persistent profile/actor identity, while conflict-scoped participants MUST use client-owned scoped identity retired with the conflict. A same-turn afterlife actor MUST reuse the exact canonical `actorId` sealed by Actor Materialization; the resource layer MUST NOT allocate a second actor identity or accept `actorRef`.
- **FR-019**: A raw resource command MUST NOT invent a permanent owner and MUST resolve exact existing or validated same-turn owner authority. For a newly materialized afterlife actor, the immutable Actor Materialization `materializationId` is the one-turn resource `ownerRef`, while actor/effect selectors continue to use canonical `actorId`.
- **FR-020**: Accepted history MUST retain immutable transition identity, event, origin, operation, coordinate, before/after, source evidence, receipt binding when present, and turn.
- **FR-021**: Transition, operation, and receipt identities MUST be client-owned and exact/confusable-unique.
- **FR-022**: Exact semantic replay MUST return the previous accepted result or no-op without duplicate history; conflicting reuse MUST fail the entire transition.
- **FR-023**: The accepted command surface MUST be closed to definition creation, capacity change, and ordinary resource change proposals.
- **FR-024**: Raw commands MUST NOT contain sealed definitions, current or maximum after-state, chronology, client IDs, phase, priority, floor/cap selection, final value, file paths, or arbitrary selectors.
- **FR-025**: Ordinary resource operations MUST be limited to `damage`, `restore`, `spend`, and `gain` and MUST carry exact target, resource, positive amount, accepted source evidence, event reference, and reason.
- **FR-026**: Every ordinary action, item operation, system rule, afterlife operation, and effect operation MUST become the same source-neutral authorized mutation before arithmetic.
- **FR-027**: Capacity transitions MUST be separately authorized and MUST use exactly one disposition: preserve, clamp to new maximum, or exact ratio scaling.
- **FR-028**: Ratio scaling MUST fail when its result is not exactly representable by the target quantum.
- **FR-029**: The system MUST apply direct costs, direct outcomes, registered system outcomes, and effect-triggered outcomes in one stable client-owned phase and priority order.
- **FR-030**: Mutations MUST be applied sequentially to one working state and MUST NOT be summed before floor/cap behavior.
- **FR-031**: Clamping MUST occur only when the accepted definition or source policy explicitly authorizes it; otherwise an out-of-range result MUST fail.
- **FR-032**: Every applied result MUST emit only registered resource events with exact applied before/after evidence.
- **FR-033**: Resource-event and scheduled effect triggers MUST execute through one finite dependency graph with stable ordering, exact replay protection, cycle rejection, and bounded expansion.
- **FR-034**: The complete accepted mechanics result MUST be planned without writes and MUST contain every resource, effect, pending, owner-companion, history, consumed-command after-image, plus the immutable projection input required to derive non-persisted player and GM views after publication.
- **FR-035**: One validated plan instance MUST supply validation and publication; identities and arithmetic MUST NOT be regenerated independently.
- **FR-036**: Any change to definitions, owners, state, history, sources, targets, events, commands, carriers, or pending authority after validation MUST invalidate publication.
- **FR-037**: Publication MUST hold one canonical write authority from final before-image comparison through writes, validation, rollback, and decision.
- **FR-038**: One invalid mutation, owner, definition, capacity, replay, source, target, quantum, arithmetic, cycle, expansion, pending, or before-image MUST block every after-image.
- **FR-039**: A deterministic resource operation MUST NOT create pending GM work.
- **FR-040**: A story-facing effect MAY create pending work only after deterministic prerequisites pass and MUST leave canonical resource/effect state unchanged until resolution.
- **FR-041**: A pending request MUST bind the exact session, turn, event, source, target, trigger, allowed result kinds, numeric bounds, companions, and full-turn resubmission requirement.
- **FR-042**: The initial pending result catalog MUST contain only narrated no-state-change and bounded resource-delta results.
- **FR-043**: A receipt MUST contain only its request identity, allowed result, allowed operands, and narrative reason; protected coordinate and operation authority MUST come from the pending request.
- **FR-044**: Missing, stale, partial, extra, repeated, cross-target, wrong-operation, or out-of-bound receipts MUST fail closed and MUST NOT be actionable semantic repair.
- **FR-045**: Mortal player health, energy, poise, and setting-defined resources MUST use the accepted resource authority and MUST NOT retain persisted mechanical mirrors.
- **FR-046**: Named NPC, individual combatant, and group-member health/poise or equivalent bounded state MUST use the accepted resource authority and MUST NOT retain positional or duplicated mechanical mirrors.
- **FR-047**: Item charges, ammunition, durability, and other bounded reserves MUST use item-owned accepted resource state and MUST NOT remain writable item fields.
- **FR-048**: Afterlife spendable/restorable action economy and spiritual reserves MUST use accepted resource state; specialized non-quantity axes and currencies MUST remain outside this feature.
- **FR-049**: Effect periodic damage, restoration, resource-event triggers, and bounded receipts MUST use the same accepted mutation and publication flow as ordinary operations.
- **FR-050**: Console, browser, and GM context MUST consume derived projections rather than persisted domain mirrors.
- **FR-051**: Player projections MUST preserve semantic console/browser parity while hiding internal owner identities, transition IDs, event refs, fingerprints, paths, pending/receipt DTOs, validation codes, repair guidance, and hidden or GM-only resources.
- **FR-052**: Malformed or unavailable resource authority MUST fail closed to generic in-world player copy and MUST NOT fall back to legacy raw fields.
- **FR-053**: Protected identity, ambiguity, replay, arithmetic, cycle, owner, capacity, and direct-mutation failures MUST NOT grant actionable GM repair authority.
- **FR-054**: A repair request MAY address only one unambiguous GM-owned semantic omission and MUST require coherent complete resubmission.
- **FR-055**: Any publication or post-validation failure MUST restore every touched path byte-for-byte and by prior existence and MUST suppress stale player narration/interface output.
- **FR-056**: No compatibility reader, dual-write bridge, legacy promotion, or automatic save conversion MAY be added.
- **FR-057**: Active templates, examples, fixtures, and new-game state MUST use only the accepted resource authority before the feature is considered complete.
- **FR-058**: Mortal and afterlife GM prompts, documentation, worked examples, manifests, and source-guard tests MUST be updated with the executable resource contract in the same feature.
- **FR-059**: The feature MUST include at least one worked Mortal example and one worked afterlife example covering legal resource authoring without direct canonical writes.
- **FR-060**: Currency, treasury, faction-accounting, and market-balance systems MUST remain separate and MUST NOT be silently routed through this resource authority.
- **FR-061**: Vehicle health and any accepted bounded vehicle reserve MUST use the permanent vehicle identity and accepted resource authority; `UpdateVehicles`, vehicle movement/activation, and removal MUST NOT retain or create a second mechanical value.
- **FR-062**: Each raw ordinary `action_cost`, `combat_outcome`, or `narrative_outcome` source MUST contain only its registered kind; the client MUST derive its source identity from the exact accepted command event and bind it to the resolved target so a raw rename cannot evade replay. Item-local sources MUST remain bound to the exact permanent item ID.
- **FR-063**: A detached combat-group member MUST retain its exact `combat_group_member` resource owner, effect target, carrier ownership, and history and MUST NOT be re-materialized as an anonymous combatant.
- **FR-064**: FullParty resource changes for another player MUST be validated as a closed recipient-scoped outbound packet, MUST share exact accepted event ordering with the originating response, and MUST NOT mutate the originating client's resource/effect state; remote application requires the recipient's own accepted authority.

### Key Entities *(include if feature involves data)*

- **Resource Definition**: The sealed policy for one semantic resource key, including value representation, bounds, allowed owners and operations, visibility, and materialization evidence.
- **Resource Coordinate**: The exact realm, owner kind, stable resource owner identity, and resource key that identifies one logical value.
- **Resource State**: The current and maximum value, capacity binding, lifecycle state, and chronology for one coordinate.
- **Resource Mutation**: One already-authorized ordered damage, restore, spend, or gain operation against one coordinate.
- **Registered System Outcome Draft**: A client-owned no-I/O adapter result that contributes exact sources and mutations to the common plan, then derives companion after-images only from that plan's accepted resource result.
- **Resource Transition Evidence**: Immutable accepted proof of one mutation's identity, event, origin, before/after values, source, receipt, and turn.
- **Resource Owner**: A validated player, named NPC, anonymous combatant, group member, item, persistent afterlife actor, or conflict-scoped participant that may hold resources.
- **Capacity Transition**: A separately authorized maximum-value change and explicit current-value disposition.
- **Resource Event**: A closed event derived from an exact applied result and available to downstream effect triggers.
- **Accepted Mechanics Plan**: The complete no-write after-state for resources, effects, pending work, histories, owner companions, consumed commands, and projections.
- **Pending Resource Resolution**: A client-owned bounded request for story judgment that has not yet changed mechanics.
- **Resource Receipt**: A bounded GM result that resolves one exact pending request and becomes an ordinary mutation at most once.
- **Resource Projection**: A safe, non-authoritative player or GM view derived from accepted definitions and state.
- **FullParty Resource Packet**: A closed recipient-keyed outbound ordinary-command packet validated by the origin client but admitted into mechanics only through the recipient client's accepted-turn authority.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every included built-in mechanic and every accepted setting-defined resource has exactly one accepted current value and zero persisted legacy mechanical mirrors.
- **SC-002**: Representative ordinary and effect-generated equivalent operations produce byte-equivalent semantic results and history across 100 repeated deterministic runs.
- **SC-003**: All supported owner families, including vehicles, pass creation, continuity, movement/entry, and terminal cleanup scenarios without duplicate or orphan state.
- **SC-004**: Exact replay creates zero duplicate transitions, while every conflicting replay case is rejected with zero published changes.
- **SC-005**: Every defined numeric, quantum, capacity, clamp, overflow, and ordering boundary has an automated positive and negative scenario.
- **SC-006**: Every injected publication and post-validation failure restores 100% of touched paths by bytes and prior existence and leaves no stale player output.
- **SC-007**: Console and browser projections expose equivalent facts and actions for all visible resources, and recursive privacy scans find zero protected/internal terms or hidden resource values.
- **SC-008**: Active Mortal and afterlife examples and templates pass production validation with zero legacy resource-authority fields.
- **SC-009**: The complete local verification gate passes within the repository's bounded lanes with no failed tests, duplicate discoveries, timeout, or leaked test processes.
- **SC-010**: Effect Task 9 periodic operations and bounded receipts execute through the common resource plan without any effect-only arithmetic or field adapter.

## Verification Plan *(mandatory)*

- **C# verification**: Focused unit and integration filters for resource contracts, definitions, state/history, owner authority, reducer, accepted mechanics plan, player/NPC/vehicle/combat/item/afterlife owner cutovers, effect integration, rollback, and projections; one meaningful `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast`; one final `PreMerge` only after #1543 and resumed #1535 Task 9 are merge-ready.
- **Documentation/contract verification**: Focused `AfterlifeDocumentationCoverageTests`, prompt/source guards, and `ExampleDocumentationValidationTests`; conditional `FullValidation` because Mortal and afterlife prompts/examples/manifests change.
- **Frontend verification**: Existing C# browser DTO/service tests; run `npm run verify` only if the React/Vite client contract or rendering code changes rather than consuming the existing typed DTO shape.
- **Manual/player-facing verification**: Compare console and browser status, combatant, item, and afterlife resource views; inspect hidden/GM-only privacy; exercise one Mortal and one afterlife GM-authored resource turn without technical terminology leaking to the player.

## Assumptions

- The game remains technical Pre-Alpha and no old save population requires support.
- Existing permanent NPC, item, effect, profile, and combatant identity authorities remain the source of exact owner identity rather than being redesigned by this feature.
- Built-in health, energy, and poise use integer percent-like values with exact quantum 1 unless the approved data model records a different built-in policy.
- Setting-defined resources are uncommon bootstrap/setting-expansion materialization operations, not arbitrary per-turn schema edits.
- Current accepted-turn snapshot, write-lease, rollback, repair, and bounded local test infrastructure are reused and hardened rather than replaced wholesale.
- Specialized afterlife combat axes, currencies, trade balances, and faction accounting remain authoritative in their existing dedicated systems unless a separate tracked issue explicitly moves them.
- The resource feature and blocked effect Task 9 are developed in the existing isolated `1535-effect-materialization` worktree so the final effect integration can consume the foundation without an interim compatibility layer.
