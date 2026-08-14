# Research: Complete Effect Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
**Date**: 2026-08-14

This phase resolves the implementation choices needed to plan a common runtime-effect authority without broadening the feature into skill, item, wound, quest, Fate Card, or combat-system rematerialization.

## Decision 1: Materialize only active runtime instances

**Decision**: `activeEffectDefinitions[]`, existing `combatEffect`, `structuredBonuses`, Fate Card payloads, wound consequences, quest/location/hazard rules, and spiritual-art descriptions remain static source authority. A runtime effect exists only after an accepted `effectChanges[].operation=apply` resolves one exact materializable definition.

**Rationale**: Static definitions can describe instantaneous actions or passive capabilities that must not acquire an active lifetime, stacks, or terminal history. Separating the definition from the instance prevents duplicate mechanics and keeps source-owning materializers authoritative.

**Alternatives considered**:

- Promote every effect-like definition automatically: rejected because passive and instantaneous mechanics would become duplicate active statuses.
- Rewrite all source schemas into active instances: rejected as scope belonging to #1533, #1534, #1536, #1537, #1538, and existing item/Fate Card work.

## Decision 2: Use distributed owner carriers plus one identity authority

**Decision**: Canonical active instances remain in owner-appropriate carriers. `game_state/effects/effect_identity_index.json` is the single client-owned uniqueness, ownership-coordinate, stack-coordinate, lifecycle, and transition-history authority.

**Rationale**: Player, NPC, combat, afterlife profile, and spiritual-conflict state already have distinct transactional and projection contexts. A global semantic effect file would duplicate owner state, while no shared identity authority would permit replay and cross-carrier ambiguity.

**Alternatives considered**:

- One global active-effect registry: rejected as a high-contention semantic duplicate of owner state.
- Independent per-realm effect systems: rejected because identity, stack, expiry, privacy, and retry rules would diverge.

## Decision 3: Replace legacy application routes with one transient command surface

**Decision**: Add `effectChanges[]` and `effectResolutionReceipts[]` to the accepted GM response. `StateDistributor` stages them together in `game_state/effects/effect_commands.json`; the effect normalizer consumes and removes the transient command file in the same accepted transaction. `playerActiveEffectsChanges`, `NPCEffectChanges`, direct non-empty combat active arrays, and direct `combatConditions[]` mutation cease to be application routes.

**Rationale**: A common command can target any supported owner and lets one planner resolve identity, stack, lifetime, and source authority. Mapping commands directly into distributed canonical carriers would recreate independent partial writers.

**Alternatives considered**:

- Retain legacy routes as aliases: rejected by the explicit no-migration/no-compatibility decision.
- Put GM commands in `game_state/control/`: rejected because control paths are client-owned and must not become GM-authored state.

## Decision 4: Add an explicit embeddable active-effect definition

**Decision**: Source-owning canonical entities may expose a closed optional `activeEffectDefinitions[]`. Each entry has an exact `definitionKey`, allowed target/realm set, registered component templates and parameter bounds, source-authorized stack policy, lifetime modes, triggers, visibility, removal routes, and companion links. Closed adapters may also project an equivalent definition from an already-sufficient current source contract.

**Rationale**: Existing `structuredBonuses` and `combatEffect` do not consistently answer whether a live instance is intended, how it stacks, or how it ends. An explicit descriptor avoids prose inference while preserving source ownership.

**Alternatives considered**:

- Infer missing policy from names or descriptions: rejected as untestable and setting-specific.
- Build a second global definition registry: rejected because source definitions belong to the materialized source entities.

## Decision 5: Generate random effect identities once in a cached accepted plan

**Decision**: `EffectIdentityFactory` creates opaque random permanent IDs and transition IDs once. `EffectAcceptedTurnPlanCache` reuses the exact plan instance for raw validation, companion validation, normalization, and post-check under one session/snapshot/input fingerprint.

**Rationale**: Client-owned identity must not be predictable or independently regenerated between validation and commit. This follows the accepted location-materialization plan pattern while preserving random allocation.

**Alternatives considered**:

- Deterministic hashes of GM input: rejected because they make permanent IDs predictable and do not satisfy generated-once authority.
- Generate during each validation/normalization pass: rejected because IDs could differ across the accepted transition.

## Decision 6: Build exact source and target catalogs from the composed state

**Decision**: `EffectSourceAuthority` and `EffectTargetAuthority` combine the validated pre-turn snapshot with accepted same-turn identities supplied by existing item, actor, faction, location, quest, and other applicable plans. Matching is ordinal and also detects trimmed, case, Unicode-confusable, historical, ambiguous, and cross-realm selectors.

**Rationale**: Effects often arise from a source or target created in the same response. Reading only on-disk pre-turn files would reject valid composition; trusting raw sibling JSON would bypass its owning materializer.

**Alternatives considered**:

- Name-based resolution: rejected because names are display semantics, not authority.
- Read raw source files directly: rejected because raw candidates have not yet earned canonical identity.

## Decision 7: Give anonymous Mortal combatants a stable combat-local target anchor

**Decision**: Accepted combatants must expose an exact stable client-owned `combatantId`. A new raw combatant carries an exact same-turn `combatantRef`; the cached accepted plan allocates and exports the permanent combat-local identity. Existing named NPC combatants also bind that anchor to exact NPC authority. Same-turn effect targets use `targetRef`, later targets use `targetId`, and exactly one is allowed. Non-empty raw `activeBuffs`/`activeDebuffs` are rejected and same-turn effects use `effectChanges[]` against the accepted temporary anchor.

**Rationale**: Array index and display name cannot safely identify an effect target across retries, sorting, or combat updates.

**Alternatives considered**:

- Let the GM author a permanent `combatantId`: rejected because retry-safe target identity is client-owned.
- Use array index: rejected as unstable.
- Use name: rejected as ambiguous and user-visible.
- Exclude anonymous combatants: rejected because the approved design covers Mortal combatants and combat-derived effects.

## Decision 8: Use a closed component registry

**Decision**: The first registry supports `characteristic_modifier`, `roll_modifier`, `resistance_modifier`, `periodic_damage`, `periodic_restore`, `action_control`, `event_reaction`, `wound_consequence`, and `afterlife_combat_condition`. Each profile owns a closed payload validator, deterministic executor or bounded-resolution descriptor, merge behavior, and player projection.

**Rationale**: A discriminated registry permits setting-specific mechanics without allowing arbitrary prose or unknown JSON to change game behavior.

**Alternatives considered**:

- One arbitrary `payload` interpreted by consumers: rejected because validation and privacy would be incomplete.
- Hard-code one monolithic effect object: rejected because unrelated component requirements would become nullable and ambiguous.

## Decision 9: Close stacking and lifetime semantics

**Decision**: Stack policy is exactly `independent`, `stack`, `refresh`, `replace`, or `merge`. Lifetime mode is exactly `turns`, `uses`, `until_time`, `scene`, `source_bound`, `condition_bound`, explicitly authorized `permanent`, or registered `manual`. The source definition supplies all policy, maximum, refresh/merge, persistence, and removal bounds.

**Rationale**: The GM should request application, not calculate post-stack state or invent persistence. Closed modes eliminate `999` sentinels and prose-only expiry.

**Alternatives considered**:

- Let the GM send `currentStacks` and `remaining`: rejected as derived-state authoring.
- Preserve numeric or text sentinels: rejected by the approved no-compatibility decision.

## Decision 10: Schedule lifecycle in one deterministic composed order

**Decision**: The scheduler processes: bound continuation; accepted remove/dispel; application/stack resolution; due-trigger selection; deterministic execution or receipt validation; use/time/turn/scene advancement; terminal cleanup; derived snapshot recomputation. Within a phase, sort by declared priority, ordinal `effectId`, then component identity.

**Rationale**: A fixed order resolves same-turn apply/expire/remove conflicts and makes retries and clients converge.

**Alternatives considered**:

- Let each carrier tick itself: rejected because cross-owner triggers and stack conflicts would be order-dependent.
- Ask the GM to decrement duration and uses: rejected because retry would duplicate or skip lifecycle work.

## Decision 11: Use bounded pending resolution only for non-deterministic story output

**Decision**: Deterministic components execute client-side. A component requiring constrained narration or a permitted story choice creates `game_state/control/pending_effect_resolutions.json`. The GM returns an exact `effectResolutionReceipts[]` entry; the receipt cannot broaden result kind, target, numeric bounds, companions, or identity.

**Rationale**: This keeps the GM in the narrative loop without asking it to implement mechanics or granting it a general file-edit capability.

**Alternatives considered**:

- Resolve every component through the GM: rejected as slow, ambiguous, and retry-unsafe.
- Ban all story-facing effect components: rejected because quests, hazards, Fate Cards, and afterlife conditions may require bounded narration.

## Decision 12: Keep afterlife conditions specialized

**Decision**: Existing `combatConditions[]` retain their five condition kinds, legal spiritual-combat axes, counterplay, affected operations, payoff, visibility, and exchange/scene semantics. The adapter supplies common effect identity, exact source/target, stack coordinate, event evidence, consumption/expiry, and terminal history. Persistent afterlife actor effects live in accepted `afterlife_entity_profiles.json` profiles rather than duplicating Guardian/resident state.

**Rationale**: The current afterlife condition contract is stronger and domain-specific. Flattening it would lose legal-axis rules; leaving it independent would duplicate lifecycle authority.

**Alternatives considered**:

- Replace spiritual conditions with Mortal stat effects: rejected as a gameplay regression.
- Leave direct `combatConditions[]` authoring: rejected because identity and retry would remain outside common authority.

## Decision 13: Derive mechanics from one accepted snapshot

**Decision**: `CharacteristicsService`, combat calculations, action eligibility, periodic changes, and afterlife axes consume an immutable accepted `EffectMechanicsSnapshot`. If any active carrier or identity agreement is invalid, none of that set contributes mechanics.

**Rationale**: Current readers apply recognized siblings independently and can hide malformed neighbors. The issue requires all-or-nothing composed validity.

**Alternatives considered**:

- Skip malformed entries: rejected because partial authority is indistinguishable from tampering or drift.
- Keep consumer-specific legacy fallbacks: rejected because console, browser, and mechanics would disagree.

## Decision 14: Extend the existing accepted transaction and repair loop

**Decision**: Effect carriers, command staging, identity index, pending resolutions, companions, and any derived canonical state join the existing canonical write lease, before-image set, post-validation, rollback, diagnostic, repair-request, coherent-resubmission, and stale-output boundaries. Effect repair packets allow only bounded unambiguous GM-owned semantic omissions; authority failures never dispatch.

**Rationale**: A second transaction would not roll back the rest of the GM response. Existing accepted-turn infrastructure already owns the required atomic boundary.

**Alternatives considered**:

- Per-carrier rollback: rejected because cross-carrier stack/trigger transitions would partially publish.
- Prompt-only retry guidance: rejected because identity and target errors must be mechanically unrepairable by retargeting.

## Decision 15: Share one active-effect projection policy

**Decision**: `EffectPlayerProjection` accepts only a fully validated effect set and produces recursive player-safe view models for Mortal and afterlife callers. It preserves visible mechanics and action eligibility while suppressing hidden effects and complete internal identity/index/receipt/transition/pending/repair DTO shapes.

**Rationale**: Field-name denylisting alone leaves residual generic fields such as `kind`, `title`, or `steps`; independent console/browser parsing creates parity drift.

**Alternatives considered**:

- Let each UI parse raw carrier JSON: rejected by existing privacy and parity failures.
- Globally hide generic semantic keys: rejected because legitimate world, quest, item, and interaction semantics use them.

## Decision 16: Do not implement runtime migration

**Decision**: Missing carriers in a pristine session are treated as empty and initialized on first accepted use. Present non-empty carrier/index shapes must satisfy the new schema. Active bootstrap files, templates, examples, manifests, and tests are updated in-repository; no legacy readers, promotion, or migration command is added.

**Rationale**: The user explicitly rejected migration, and the project constitution declares no pre-release compatibility requirement.

**Alternatives considered**:

- One-time save upgrader: rejected as unnecessary technical debt for unsupported saves.
- Indefinite compatibility aliases: rejected because they preserve the ambiguity this feature removes.

## Decision 17: Verification remains fully local

**Decision**: Verification uses bounded local `scripts/test-csharp.ps1` lanes. GitHub Actions are neither required nor enabled. Focused RED/GREEN drives implementation; one meaningful Fast checkpoint, conditional FullValidation/LifecycleIntegration, and one final PreMerge provide controls.

**Rationale**: This follows repository policy and the user's explicit hard limit on GitHub Actions while preserving evidence quality.

**Alternatives considered**:

- CI-only evidence: rejected because Actions are disabled and local lanes are authoritative for this repository.

## Implementation Inventory Result

The implementation preflight classified the exact repository surfaces in
[effect-surface-inventory.md](effect-surface-inventory.md). The targeted scan
found 94 non-generated code/rule/guide/example/fixture files and added ten
indirect executable or contract paths whose implementations do not contain the
searched property names themselves.

The inventory confirms three categories that implementation must not collapse:

1. `playerActiveEffectsChanges` and `NPCEffectChanges` are incomplete legacy
   application routes and are removed rather than aliased.
2. `structuredBonuses`, `combatEffect`, Fate Card effects, special-art combat
   descriptions, and Shining blessing entitlement remain static/source or
   separate-entitlement authority.
3. Active runtime state is confined to the planned player, NPC, combatant,
   persistent afterlife profile, and spiritual-condition carriers plus the
   identity, transient command, and pending-resolution roots.

This inventory is the path-ownership source for T028–T110. Any newly discovered
effect-shaped path must be classified there before its reader or writer changes.
