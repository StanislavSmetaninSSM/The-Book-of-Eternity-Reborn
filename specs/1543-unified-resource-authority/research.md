# Research: Unified Resource Authority

**Feature**: [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543)

**Blocked consumer**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535), Effect Task 9

**Date**: 2026-08-15

This research resolves implementation choices left below the approved architecture in [2026-08-15-unified-resource-authority-design.md](../../docs/superpowers/specs/2026-08-15-unified-resource-authority-design.md). No user-facing scope decision remains open.

## Decision 1: Resource admission test

**Decision**: A mechanic enters the resource authority only when it is an exact scalar quantity owned by one stable entity, has a current value and an explicit capacity/minimum policy, and supports at least one registered `damage`, `restore`, `spend`, or `gain` transition. Numeric progression tiers, relationship axes, categorical conditions, effect lifetime counters, QTE-local progress, and accounting balances remain in their owning systems.

**Rationale**: This captures health, energy, poise, charges, ammunition, durability, action points, rerolls, and comparable reserves without turning every number in the game into a fungible resource.

**Alternatives considered**:

- Move every numeric field into the ledger: rejected because progression, clocks, prices, and relationship values have different lifecycle and transaction semantics.
- Limit the ledger to health/energy/poise: rejected because it recreates separate item, combat, afterlife, and effect arithmetic.
- Let each adapter opt in informally: rejected because it would make scope and validation inconsistent.

## Decision 2: Included and excluded existing mechanics

**Decision**: The initial cutover includes:

- Mortal player `health`, `energy`, and `poise`;
- named NPC health and any materialized bounded NPC reserve;
- vehicle health and any materialized bounded vehicle reserve, keyed by permanent `vehicleId`;
- individual combatant health/poise and stable group-member health/poise;
- item durability, generic `resourceType/resource/maximumResource`, charges, and ammunition when present;
- afterlife spiritual-conflict action points;
- bounded Guardian/Shining per-return gacha charges and numeric blessing reroll entitlements because they are spendable capped quantities;
- setting-defined mana, stamina, and equivalent resources materialized through the new definition route;
- periodic effect damage/restoration and bounded receipt deltas.

Money, Ink Feathers, Light Sparks, treasuries, faction resource ledgers, prices, trade balances, progression experience/tiers, reputation/relationship values, effect stack/lifetime counters, and QTE-local progress remain outside. Boolean entitlements remain typed entitlements even when adjacent numeric reroll counts enter the ledger.

**Rationale**: This applies the admission test to all discovered active mechanics and avoids the rejected half-measure of keeping another bounded-current/max authority merely because its subsystem is complex.

**Alternatives considered**:

- Keep gacha and rerolls in afterlife state: rejected because their spend/reset/cap lifecycle is resource behavior.
- Move currencies too: rejected because the approved scope explicitly retains accounting systems.
- Move custom states automatically: rejected because many custom states are narrative meters; an exact setting-defined resource must be materialized explicitly instead.

## Decision 3: Canonical storage split

**Decision**: Use four paths under `game_state/resources/`: sealed definitions, live state, immutable history, and transient commands. No domain file retains a persisted current/max mirror after cutover.

**Rationale**: Definitions, live values, replay history, and untrusted commands have different ownership and mutation rules. Keeping them separate makes direct-mutation and rollback checks explicit while preserving one logical authority.

**Alternatives considered**:

- One monolithic resource file: rejected because a transient command merge could overwrite protected state and because history growth would amplify every write.
- Keep state beside each owner: rejected because shared arithmetic, replay, cross-subsystem continuity, and atomic publication would still be fragmented.
- Event sourcing only: rejected because every ordinary projection would need full replay and history compaction would become a prerequisite.

## Decision 4: Exact numeric representation

**Decision**: Parse and retain resource numbers as .NET `decimal`; reject exponent/precision/range forms that cannot round-trip exactly through the canonical JSON number representation. `integer` requires an exact integral decimal. `quantum` is a positive decimal and `(value - minimum) / quantum` must be integral for state and mutation results.

**Rationale**: The game needs deterministic ordered arithmetic and exact quantum checks. Binary floating point is unsuitable, while arbitrary precision is unnecessary for bounded game resources and would expand dependencies.

**Alternatives considered**:

- `double`: rejected due to non-exact decimal and ordering divergence.
- Scaled `long` only: rejected because setting-defined decimal quantum would require one global scale or hidden rounding.
- Arbitrary-precision decimal package: rejected as unnecessary dependency and state complexity for the accepted bounded catalog.

## Decision 5: Built-in definition and capacity policy

**Decision**: Active templates ship built-in version-1 definitions. Health, energy, poise, action points, charges, ammunition, durability, and rerolls use integer quantum `1` unless a source-specific registered definition states otherwise. Capacity is one of `definition_fixed`, `instance_fixed`, or a closed `registered_formula`; formulas receive a validated owner snapshot and return an exact capacity without arbitrary expressions.

**Rationale**: Built-ins must work before the GM can act, while settings need bounded extensibility. Registered formulas cover existing derived maxima such as spirit-focus action points and Guardian reputation gacha capacity without exposing code selection to the GM.

**Alternatives considered**:

- Store formula expressions in JSON: rejected as executable/prose authority.
- Copy every derived maximum into owner state: rejected because it becomes another persisted authority.
- Only fixed capacities: rejected because existing action-economy and per-return charge mechanics have legitimate derived capacity.

## Decision 6: Stable owner identity and combat generalization

**Decision**: Introduce common mechanics owner authority and rename/generalize the effect-only combatant identity allocator so resources and effects consume the same combatant IDs. New groups receive stable client-owned `memberId` values, and named NPC combat rows bind to the permanent NPC owner rather than receiving a second resource owner.

**Rationale**: Effect Task 6 already proved the need for client-owned combatant identity. Resource cutover must not create a second allocator or preserve positional group identity.

**Alternatives considered**:

- Reuse `EffectCombatantIdentityState` without refactoring: rejected because resource identity would incorrectly depend on an effect-specific type and no-effect combat turns would remain fragile.
- Allocate resource-only combatant IDs: rejected because one combatant would have divergent effect and resource identities.
- Derive group member identity from index/name: rejected because reordering/rename would change identity.

## Decision 7: One mutation reducer and cross-domain orchestrator

**Decision**: `ResourceMutationReducer` performs only exact arithmetic and history production for one authorized mutation against an immutable working ledger. `AcceptedMechanicsPlanner` owns ordered domain adapters, resource events, effect trigger graph traversal, effect lifecycle, pending work, and the complete after-state.

**Rationale**: Arithmetic must be independently testable and source-neutral, but effects require feedback from applied resource results. One orchestrator avoids circular normalizer dependencies and effect-only adapters.

**Alternatives considered**:

- Put trigger traversal inside the reducer: rejected because the reducer would depend on effects and file contours.
- Let each subsystem invoke the reducer and write immediately: rejected because ordering and cross-domain atomicity would be lost.
- Keep resource and effect planners independent then reconcile writes: rejected because resource events and effect-triggered mutations form one ordered graph.

## Decision 8: Mutation order and graph bounds

**Decision**: Use closed phases `direct_cost`, `direct_outcome`, `registered_system_outcome`, and `effect_trigger`. Within a phase, sort by registered priority, exact `originId`, then client-owned `operationId`. Apply sequentially. Trigger expansion uses a DAG keyed by event/trigger/operation identity, rejects cycles, and enforces registered per-turn node/depth limits recorded in the technical contract.

**Rationale**: Clamps and depletion events make order observable. Stable ordering and hard graph limits are required for replay and denial-of-service safety.

**Alternatives considered**:

- Sum all deltas: rejected because clamp/fill/deplete outcomes differ.
- Use GM array order: rejected because it grants phase/priority authority and makes composition fragile.
- Recursive unbounded trigger calls: rejected because cycles and exponential expansion could hang a turn.

## Decision 9: Capacity changes are not ordinary deltas

**Decision**: Capacity changes require their own source-authorized transition and one of `preserve`, `clamp_to_new_maximum`, or `scale_ratio_exact`. They run before ordinary mutations for newly created/reconfigured coordinates and otherwise at the registered system phase. `scale_ratio_exact` rejects inexact quantum results.

**Rationale**: Maximum changes alter the meaning of the current value and must not be smuggled in as gain/restore or raw state replacement.

**Alternatives considered**:

- Treat maximum as another resource: rejected because it creates recursive capacity authority.
- Always clamp: rejected because it changes current state without source policy.
- Always scale: rejected because ratios can be inexact and often contradict gameplay intent.

## Decision 10: Command and event authority

**Decision**: Add three response fields: `resourceDefinitionCreations`, `resourceCapacityChanges`, and `resourceChanges`, staged into the transient command root. Same-turn owners use exact temporary refs from their owning materializer. The client derives phase/priority, allocates operation/transition IDs, and validates accepted event refs by ordinal; commands never contain after-state or paths.

**Rationale**: This is the narrow GM surface needed for ordinary authored outcomes without exposing canonical state.

**Alternatives considered**:

- Reuse legacy delta fields: rejected because they encode only player-specific resources and no source/owner/capacity authority.
- Let each domain keep its command field: rejected because shared validation and ordering would remain incomplete.
- Let the GM submit `resourceOwnerId`: rejected for same-turn and client-owned identities.

## Decision 11: Bounded story resolution

**Decision**: Deterministic resource changes never pend. Story judgment uses a client-owned pending request with only two initial result kinds: `narrated_no_state_change` and bounded typed `resource_delta`. The receipt selects only an allowed result and bounded operand; the client restores coordinate, operation, source, and event authority from the request.

**Rationale**: Arbitrary JSON-path receipts would reintroduce direct state writes. A minimal closed catalog supports narrative judgment while preserving one reducer.

**Alternatives considered**:

- Let the GM return a state patch: rejected as protected-authority bypass.
- Resolve all story effects deterministically: rejected because some fiction legitimately requires GM judgment.
- Add a broad result-kind catalog immediately: rejected under YAGNI; later kinds require tracked contracts.

## Decision 12: Publication and cache binding

**Decision**: Cache one random-ID `AcceptedMechanicsPlan` under session/request/snapshot plus canonical fingerprints for definitions, owner catalog, resource state/history, effect sources/targets/carriers/index, pending authority, accepted events, commands, and internal adapter inputs. Under the write lease, compare exact before-images/fingerprints, write all after-images, delete consumed commands, post-validate, and restore bytes/prior absence on failure.

**Rationale**: Effect Task 6 exposed how partial fingerprints and regenerated plans create TOCTOU and data-loss holes. Resource/effect integration must start with a complete handoff.

**Alternatives considered**:

- Rebuild the plan at publication: rejected because random identities and source inputs can diverge.
- Bind only touched carriers: rejected because coordinated mutation of an untouched authority can remain self-consistent.
- Depend only on later global validation: rejected because unauthorized bytes may be published before rollback and direct normalizer calls would be unsafe.

## Decision 13: Projection-only compatibility for UI models

**Decision**: `ResourceProjectionService` derives typed in-memory summaries. Existing UI DTO property names such as health/energy/poise percentages may remain temporarily as projection fields, but they are never persisted or read as mechanics. All ordinary consumers must receive the projection, and raw fallback is forbidden.

**Rationale**: Removing persisted mirrors does not require a simultaneous visual redesign. It does require proving that the familiar UI values originate only from accepted resource state.

**Alternatives considered**:

- Rename every UI DTO in the same change: rejected as unrelated visual/API churn with no authority benefit.
- Keep legacy files synchronized for UI: rejected as dual-write authority.
- Let consumers fall back to raw fields: rejected by no-migration and privacy requirements.

## Decision 14: Cutover sequencing and merge boundary

**Decision**: Implement contract/reducer/owner/planner slices behind unmerged branch-local code, then cut every included writer/reader and active fixture before the feature is merge-ready. No intermediate PR may claim #1543 complete while a second persisted authority remains. Effect Task 9 resumes only after ordinary operations and owner lifecycles use the common planner.

**Rationale**: The user explicitly rejected fragile adapters and requested a complete model without losing the later refactor.

**Alternatives considered**:

- Merge the ledger first with mirrors: rejected because it normalizes a dual-authority architecture.
- Implement effects first and retrofit ordinary actions later: rejected as the original brittle proposal.
- One enormous untested cutover commit: rejected; branch-local TDD slices and reviewer gates remain necessary even though final integration is one authority.

## Decision 15: Repair, privacy, and player failure behavior

**Decision**: Identity, replay, direct mutation, stale input, arithmetic, cycle, owner, capacity, and receipt binding failures are protected and non-actionable. Only one exact GM-owned semantic omission can yield a bounded repair request. Player copy is fixed in-world Russian and projections suppress all internal coordinates, IDs, paths, codes, history, pending DTOs, and hidden resources.

**Rationale**: A resource coordinate is sensitive mechanical authority and must not be delegated through broad repair or exposed through ordinary UI.

**Alternatives considered**:

- Ask the GM to rewrite canonical state: rejected because it grants protected authority.
- Show detailed validation issues to the player: rejected by console/browser integrity and privacy contracts.
- Hide only known field names: rejected because recursive wrappers and future fields can leak complete internal DTOs.

## Decision 16: Verification and performance contour

**Decision**: Unit-test closed contracts, exact arithmetic, replay, ordering, and graph behavior; integration-test owner composition, publication, rollback, legacy rejection, projections, prompts, and examples. Index definition/state/history/owner/source/target catalogs once per plan. Doubling a representative population must remain at or below 2.5x planner/validation work. Use only bounded local test lanes; GitHub Actions remain disabled.

**Rationale**: The repository already has a bounded local runner and effect performance precedent. Linear or near-linear catalogs are sufficient for local save scale without premature storage optimization.

**Alternatives considered**:

- No performance guard: rejected because history and cross-domain catalog scans can accidentally become quadratic.
- Introduce a database/index service: rejected as unnecessary architectural expansion for a local file-backed game.
- Use GitHub Actions: rejected by explicit user instruction and repository policy.

## Decision 17: Phase-1 executable inventory correction — vehicles

**Decision**: Add `vehicle` as a ninth closed resource owner kind. Existing `game_state/misc/vehicles.json` objects have a stable `vehicleId` and mechanically authoritative `currentHealth`/`maxHealth`, so vehicle health satisfies the same admission test as NPC/combat health. The cutover removes those writable vehicle fields, initializes health through accepted vehicle materialization, preserves the coordinate through activation/parking/movement, and retires it on destruction.

**Rationale**: The first executable inventory found that the approved eight-owner draft omitted an active bounded mechanic. Leaving it in `UpdateVehicles` would violate the single-authority goal and the user's explicit requirement not to discard difficult existing mechanics. The fix is made in specification artifacts before runtime implementation rather than hidden behind a later adapter.

**Alternatives considered**:

- Treat vehicle health as narrative metadata: rejected because production validates and renders it as current/max mechanical health and permits partial updates.
- Reuse `item` ownership because vehicles can carry inventory: rejected because vehicles have their own permanent identity and lifecycle.
- Defer vehicles to a follow-up: rejected because the final no-mirror boundary would be false at merge.

## Implementation preflight conflicts and resolutions (2026-08-15)

- Issues #1543 and #1535 are open; #1535 records #1543 as the blocker for Effect Task 9. The active branch/root are `1535-effect-materialization` and `E:/Games/worktrees/boe-1535-effect-materialization`.
- Repository policy and `docs/testing.md` agree on bounded PowerShell lanes, a five-minute Fast limit, a twenty-minute PreMerge limit, no duplicate Fast immediately before PreMerge, and no GitHub Actions. No implementation-plan conflict remains.
- The initial design said eight owner kinds, but the executable scan found vehicle health. Decision 17 resolves the mismatch by adding `vehicle` everywhere before production behavior is written.
- Broad token collisions were reviewed and remain outside the ledger by explicit policy: owner-bond/reputation/mastery/experience are relationships/progression; Ink Feathers, Light Sparks, treasuries, faction ledgers, prices, and trade balances are accounting; effect stacks/uses/turns are effect lifetime; QTE counters and lock-pin durability are QTE-local state; spiritual power/shield values are specialized conflict axes or immutable audit; item stack count remains item lifecycle. These exclusions are not compatibility fallbacks and may enter only through a separately tracked contract change.
- No runtime migration, compatibility reader, dual write, raw projection fallback, Actions workflow, or cloud dependency is authorized.
