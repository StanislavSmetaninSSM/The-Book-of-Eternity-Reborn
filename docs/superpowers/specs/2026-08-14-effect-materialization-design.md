# Complete Effect Materialization Design

**Date**: 2026-08-14

**Source issue**: [GitHub #1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

**Spec Kit feature**: `specs/1535-complete-effect-materialization/`

**Status**: Design approved in conversation; written specification awaiting final review

## Purpose

Effects are currently represented by several unrelated shapes. Mortal player effect
commands are written into `game_state/player/effects.json`; named-NPC commands are
written into `game_state/npcs/npc_effects.json`; combatants carry
`activeBuffs`/`activeDebuffs`; afterlife spiritual conflicts carry
`combatConditions[]`; and skills, items, special arts, Fate Cards, wounds, quests,
locations, factions, hazards, and events can all describe effect-like mechanics.

The runtime validates some individual fields, but it has no single boundary proving
that an applied effect has a valid target, exact source authority, complete
mechanical payload, deterministic stacking policy, finite or explicitly authorized
persistence, and an idempotent lifecycle. Command wrappers can become durable state,
characteristic readers can consume partially valid arrays, and the GM is expected to
remember ticking, refreshing, expiry, and removal conventions.

This design introduces a setting-agnostic effect materialization boundary for active
runtime instances across Mortal World and afterlife. Static definitions remain source
contracts; applying one of them creates or changes a separately governed effect
instance.

## Accepted Product Decisions

1. Materialization governs active runtime effect instances, not every static effect
   definition.
2. Existing `combatEffect`, `structuredBonuses`, Fate Card effects, item mechanics,
   skill mechanics, and special-art combat descriptions remain source templates.
   Source adapters prove what active instance, if any, a template may create.
3. Existing afterlife `combatConditions[]` retain their spiritual-conflict-specific
   mechanics, but participate in the common effect identity and lifecycle authority
   through a specialized profile adapter.
4. Active effects remain in distributed owner-appropriate carriers. One common
   contract, identity index, accepted-turn plan, lifecycle scheduler, and projection
   policy govern every carrier.
5. The client assigns every permanent `effectId`. The GM cannot invent, select, or
   rewrite a new effect identity.
6. A source-authorized `stackKey` plus a closed policy determines whether a repeated
   application creates an independent instance, stacks, refreshes, replaces, or
   merges an existing instance.
7. Duration uses closed lifecycle modes. Numeric sentinels such as `duration: 999`
   and narrative-only duration strings are not mechanical authority.
8. Runtime migration is explicitly excluded. Non-empty legacy effect state is
   unsupported after the feature lands; the technical Pre-Alpha does not promise
   save compatibility. Repository fixtures and new-game templates are updated in the
   implementation change.
9. A missing effect carrier in a pristine/new session means no active effects and may
   be created by the first accepted application. A present non-empty carrier must
   satisfy the new schema exactly.
10. Removing a wound-derived effect never heals, deletes, or mutates the source wound.
    A separately accepted wound transition may end its source-bound effects.

## Current-State Observations

- `ValidationService.ValidateEffectObject` currently requires only one readable
  effect-like string and validates a few optional fields. It does not validate source
  identity, target, realm, stacking, triggers, expiry, or removal authority.
- `NPCEffectChanges[]` validation currently proves NPC identity and the presence of an
  `effectsApplied` property, but not the effect payload.
- `StateDistributor` groups mapped response fields by file and merges the latest
  command property into the existing object. There is no player/NPC effect
  normalizer that consumes commands into canonical active state.
- `CharacteristicsService` reads several legacy array aliases and applies recognized
  bonuses independently. A malformed sibling effect does not establish an
  all-or-nothing accepted effect set.
- Console and browser effect readers understand multiple array names and loosely
  shaped descriptions. They do not share a canonical acceptance authority.
- Mortal combatants use `activeBuffs` and `activeDebuffs` with the old Block 6 effect
  object. Wound references use a duration sentinel and defer mechanics to a wound.
- Afterlife spiritual `combatConditions[]` already have stronger finite-duration,
  source, target, counterplay, visibility, and mechanical-axis validation. They are a
  specialized runtime effect carrier, not a static definition.
- `ShiningBlessingEffectState` is client-derived pending entitlement state. It is not
  a GM-authored active effect carrier and remains outside this contract.
- Static `combatEffect` and `structuredBonuses` values can describe an action or
  passive capability without creating an active status. Treating every definition as
  a live instance would duplicate mechanics and broaden the feature into skill, item,
  and combat-system materialization.

## Approaches Considered

### A. Distributed carriers with one shared authority — selected

Effects stay beside the owner or conflict that gives them meaning. A shared contract,
identity index, accepted-turn plan, lifecycle scheduler, and projection layer govern
all carriers.

Advantages:

- Preserves existing Mortal, NPC, afterlife-profile, and spiritual-conflict
  boundaries.
- Avoids a global hot file containing every realm and actor.
- Allows specialized mechanics without divergent identity, retry, expiry, and privacy
  rules.
- Lets dependent materializers add source adapters without redefining the effect
  lifecycle.

Costs:

- Requires coordinated planning and atomic writes across several carrier types.
- Requires every reader and mechanical consumer to use the common acceptance API.

### B. One global active-effect registry — rejected

A single global registry makes ticking easy, but it centralizes unrelated realm state,
duplicates owner-local projections, and turns one file into a high-contention point of
failure. It would also force spiritual-conflict conditions and ordinary actor effects
into one physical shape.

### C. Independent Mortal and afterlife implementations — rejected

Per-realm validators appear smaller initially, but identity, stacking, duration,
retry, privacy, and removal semantics would diverge. That fails the issue's
setting-agnostic lifecycle goal.

## Architecture

```text
GM effectChanges[] / effectResolutionReceipts[]
                         |
                         v
            raw command + event validation
                         |
             +-----------+-----------+
             |                       |
             v                       v
      target authority        source-definition adapters
             |                       |
             +-----------+-----------+
                         v
              cached EffectAcceptedTurnPlan
                         |
             +-----------+-----------+
             |                       |
             v                       v
       distributed carriers   client effect identity index
             |                       |
             +-----------+-----------+
                         v
       derived mechanics + safe console/browser projections
```

The client builds one `EffectAcceptedTurnPlan` against the validated pre-turn snapshot
and the effective same-turn identities produced by applicable materializers. Raw
validation, effect-aware companion validation, and commit normalization reuse the
same cached plan. Identity allocation therefore happens once and cannot change
between validation and publication.

## Command Surface

The common GM-facing command is conceptually `effectChanges[]`. Each entry declares
one operation:

- `apply`: request application from one exact definition to one exact target;
- `dispel`: remove an existing effect through an allowed dispel/counterplay route;
- `remove`: close an existing effect because an exact cure, cleanup, source, quest,
  or lifecycle event occurred.

A repeated `apply` is the only refresh/stack/replace/merge command. The client resolves
the outcome from existing state and the source-authorized stacking policy. The GM does
not directly set current stacks, replace IDs, decrement duration, consume uses, or mark
ordinary expiry.

New applications omit `effectId`. Operations against an existing instance use the
exact opaque `effectId` provided to the GM in technical context, never a name fallback.
Commands also carry an exact target selector, exact source selector and definition key,
requested source-authorized parameters, and accepted-turn event evidence.

`effectResolutionReceipts[]` closes bounded pending trigger work that required GM
narration or a permitted story decision. A receipt cannot change the effect
definition, target, source, profile, bounds, or technical identity.

Legacy `playerActiveEffectsChanges` and `NPCEffectChanges` cease to be accepted
effect-application routes. Active combatant arrays and `combatConditions[]` are
canonical carrier projections, not alternate unrestricted command routes.

## Common Canonical Envelope

Every active effect has a closed versioned envelope with these semantic sections:

| Section | Required meaning |
| --- | --- |
| Identity | Client-assigned `effectId`, schema version, entity kind, active lifecycle state |
| Realm | Exact Mortal, Chaos Sea, or Shining Abode realm |
| Target | Exact target kind and permanent/effective target identity |
| Display | Player-readable name, description, category, and visibility |
| Source | Exact source kind, source identity, definition key, and accepted authority |
| Mechanics | Non-empty ordered list of registered typed components |
| Lifetime | One closed duration/persistence mode and its exact remaining evidence |
| Stacking | Source-authorized `stackKey`, policy, maximum, and current count |
| Triggers | Declared event types, priorities, consumption policy, and bounded outputs |
| Removal | Allowed cure, dispel, source-loss, condition, or manual closure routes |
| Links | Exact related wound, skill/art, item, quest, location, faction, event, or combat authority |
| Chronology | Accepted creation turn/event and last applied lifecycle transition |

Technical receipt seals and full transition history live in the client-owned identity
authority rather than the player-facing semantic envelope. The canonical instance may
carry only the minimum opaque identity and current semantic state needed by its owner.

Active carriers contain live or suspended instances. Terminal instances leave the
carrier, while the identity authority retains their terminal transition so the same
event cannot recreate, expire, cure, or reward them twice.

## Registered Mechanical Components

An effect contains one or more ordered components that share target, source, lifetime,
and stacking lifecycle. The first implementation supports declared profiles needed by
the issue acceptance matrix:

- characteristic/stat modifier;
- advantage or disadvantage;
- resistance or damage reduction;
- damage over time;
- healing or resource restoration over time;
- action permission, restriction, or control;
- deterministic event reaction;
- source-bound wound consequence;
- specialized afterlife spiritual-combat condition.

Each profile has a closed payload validator and executor. Unknown profiles and
mechanics hidden only in prose are rejected. New setting-specific profiles require a
registered validator/executor/projection contract and GM documentation; adding an
arbitrary JSON field does not extend mechanics.

An instantaneous damage or heal definition does not become an active effect merely
because it is called an effect in a combat action. Only definitions with a runtime
lifetime, trigger, use, source-bound state, or explicit active-instance declaration
enter the materialization path.

## Source Definition Authority

`EffectSourceAuthority` is a read-only catalog composed from validated canonical
sources. Source-kind adapters expose a common materializable definition containing:

- exact source identity and definition key;
- allowed target kinds and realm;
- permitted component profiles and parameter bounds;
- resolved stack key, policy, and maximum;
- allowed lifetime modes and limits;
- trigger and ordering declarations;
- cure, dispel, removal, and source-loss behavior;
- visibility constraints and related companion identities.

Adapters cover representative skill, spiritual art, item, wound, quest, location or
hazard, faction or event, and Fate Card sources. They do not promote every static
definition. If the exact source does not authorize an active instance, `apply` fails.

Existing source formats may be interpreted by a closed adapter when their current
contract is sufficient. They are not rewritten into active effects. Future
materialization features may add stronger source descriptors, but must preserve this
common authority interface.

## Target Authority and Distributed Carriers

`EffectTargetAuthority` resolves one exact, case-sensitive target and its realm. It
rejects case variants, Unicode-confusable aliases, historical identities, ambiguous
same-turn references, and cross-realm selectors.

Owner-appropriate carriers include:

- the Mortal player effect state;
- named-NPC effect state;
- accepted Mortal combatant state for ephemeral enemies/allies with stable combat-local
  identities;
- applicable Guardian, Abode resident, and radiant-actor profile state;
- the active afterlife spiritual conflict for `combatConditions[]`.

A physical file may contain another authority's adjacent subtree, such as wound state.
The effect normalizer owns only the declared effect subtree, preserves unrelated
canonical data, and captures the whole touched file for rollback.

`game_state/effects/effect_identity_index.json` is the proposed client-owned global
identity authority. It records each active or terminal `effectId`, exact logical owner
coordinate, realm, stack coordinate, current lifecycle state, immutable source binding,
and transition history needed for idempotency. It is not a GM writable target.

## Stacking Model

Stack identity is scoped by realm, target, and source-authorized `stackKey`. The closed
policies are:

- `independent`: create a new effect identity for each permitted application;
- `stack`: increase count up to `maxStacks` using one governed instance;
- `refresh`: preserve identity/count and reset or extend lifetime exactly as declared;
- `replace`: terminate the current instance and create one replacement transition;
- `merge`: combine only the explicitly declared bounded fields using the profile's
  deterministic merge function.

The GM cannot choose another policy, raise `maxStacks`, rewrite a stack key, or submit
the post-stack count. Conflicting policies for the same logical coordinate fail before
publication.

## Lifetime Model

The closed lifetime modes are:

- `turns`: a positive number of accepted target turns;
- `uses`: a positive number of governed trigger consumptions;
- `until_time`: an exact canonical world-time deadline;
- `scene`: the exact accepted scene/conflict identifier;
- `source_bound`: active while an exact wound, equipped item, maintained skill, aura,
  quest state, or other validated source remains active;
- `condition_bound`: active while a registered mechanically checkable condition holds;
- `permanent`: allowed only when the exact source explicitly authorizes permanence;
- `manual`: allowed only with explicit registered cure/dispel/removal rules.

Display text such as "до рассвета" may accompany an exact deadline, but is not the
deadline. Numeric persistence sentinels and unsupported free-form duration strings are
invalid.

Realm transitions apply a source-declared suspend-or-expire policy. Ordinary effects
cannot silently move between realms. A future explicitly registered soul-bound profile
may authorize cross-life persistence; generic source prose cannot.

## Deterministic Lifecycle

For every accepted turn or afterlife exchange, the scheduler performs one composed
transition in this order:

1. Reconcile source-bound and condition-bound continuations.
2. Apply accepted removals and dispels.
3. Resolve new applications and stacking outcomes.
4. Select due periodic and event triggers.
5. Execute deterministic client-owned components or validate bounded GM receipts.
6. Consume uses and advance turn/time/scene lifetimes exactly once.
7. Expire completed instances and governed effect-owned companions.
8. Recompute derived mechanics and player projections from the complete accepted set.

Within a phase, ordering is declared priority, then ordinal `effectId`, then component
identity. Each application, trigger, tick, receipt, removal, and expiry has an exact
event/transition key. Replaying the same accepted input is a no-op rather than a second
application.

Effects may create declared downstream effects only through a finite acyclic dependency
graph. The composed planner detects cycles, duplicate transition keys, and runaway
expansion before mutation. It does not recursively execute arbitrary GM-authored JSON.

## Bounded Complex-Effect Resolution

Pure, fully deterministic components are calculated by the client. When an effect
requires GM narration or a constrained story-facing result, the harness produces a
client-owned pending resolution containing:

- exact effect, target, source, trigger, and event identities;
- allowed result kinds and canonical target paths;
- deterministic numbers or permitted numeric bounds;
- required companion changes;
- fields and authorities the GM must preserve;
- a full-turn resubmission obligation when rollback is required.

The GM returns a receipt for that exact pending request. Missing, stale, partial,
cross-target, or out-of-bounds receipts fail closed. The player receives only an
in-world waiting/failure message; pending packets and technical diagnostics remain
operator-only.

## Wound Boundary

A wound may authorize one or more source-bound effect instances. Each instance keeps an
exact wound link and its own effect identity and lifecycle.

- Dispelling or removing the effect never changes wound severity, healing state, or
  existence.
- Treating, healing, or deleting a wound is a separate wound transition owned by
  #1536.
- An accepted wound transition may cause its linked source-bound effects to expire or
  change according to the wound contract.
- Missing or ambiguous wound authority cannot be repaired by retargeting an effect to
  another wound.

## Afterlife Integration

Afterlife `combatConditions[]` remain the dedicated short-lived spiritual-combat layer.
Their `mark`, `ward`, `burden`, `opening`, and `vow` semantics, legal mechanical axes,
counterplay, visibility, affected operations, and exchange/scene duration remain
specialized.

The adapter aligns them with the common contract by providing client identity,
source/target authority, stack coordinate, transition evidence, deterministic
consumption/expiry, and terminal history. Their specialized payload is not flattened
into Mortal HP/status mechanics.

Persistent effects on Guardians, residents, and radiant actors use their accepted
actor/profile carriers and the same general envelope. Client-derived
`pendingShiningBlessingEffects` remains a separate entitlement lifecycle and cannot be
authored through `effectChanges[]`.

## Derived Mechanics

No characteristic, roll, resistance, action permission, periodic delta, or afterlife
axis is applied from raw commands or a partially valid array. Consumers receive one
accepted `EffectSnapshot` from the common authority.

The characteristic service applies the snapshot all-or-nothing. Player status
summaries such as `activeConditions` become derived display state rather than a second
GM-authored mechanical authority. Combat, training, inventory, interaction, and
afterlife consumers use registered component executors and never parse prose to invent
mechanics.

## Atomic Acceptance and Rollback

One accepted effect transition follows this order:

1. Validate the pre-turn effect index, all owner carriers, and applicable source/target
   authority without mutation.
2. Read effective same-turn identities from other accepted materialization plans.
3. Parse `effectChanges[]`, due lifecycle events, and pending receipts.
4. Build one in-memory plan with permanent identities and final carrier/index state.
5. Validate the complete composed effect set, stacking, references, companion deltas,
   and derived mechanics.
6. Capture before-images and acquire one canonical write lease for every touched path.
7. Publish all owner carriers, identity transitions, companions, and derived state.
8. Run post-normalization validation before releasing the accepted-turn transaction.
9. Restore every tracked path byte-for-byte and suppress stale output if any write or
   post-check fails.

The plan cache is bound to the accepted input fingerprint, validated snapshot/session,
and source/target authority. A stale plan cannot be reused after session replacement or
authority change.

## Validation Invariants

- Every active instance has one exact client identity and one exact logical carrier.
- Every index entry and carrier instance agree on target, realm, source, stack, and
  lifecycle state.
- Active and historical IDs, event IDs, and stack coordinates reject exact duplicates,
  case variants, and Unicode-confusable reuse where applicable.
- The GM never authors client identity, receipt, seal, transition history, or index
  state.
- Every source and target resolves exactly in the composed accepted state.
- Every component profile and payload is complete and source-authorized.
- Lifetime, trigger, stacking, and removal policies are closed and internally
  consistent.
- Repeated application and lifecycle events produce the deterministic declared result.
- Trigger dependencies are finite and acyclic.
- Ordinary effects do not cross realms.
- Invalid or partially materialized effect sets contribute no derived mechanics.
- Effect removal never mutates a source wound.
- Direct canonical effect/index mutation without an accepted operation is rejected.
- Missing or legacy non-empty carrier shapes are never silently promoted.

## Repair Policy

A bounded effect repair packet may describe one unambiguous raw command and only
GM-owned semantic fields such as a missing readable description, a missing requested
parameter already bounded by the source, or an incomplete declared counterplay entry.

Identity, target, source, realm, stack coordinate, receipt, history, lifecycle event,
duplicate/confusable, cycle, direct canonical mutation, and ambiguous owner errors are
protected authority failures. They produce no actionable retargeting packet. The
system restores the pre-turn snapshot and writes a path-bound operator diagnostic.

Repair success requires one complete coherent resubmission. A ready marker or partial
effect fragment cannot turn a rejected response into an accepted no-op.

## Player Projection and UI Parity

Console and browser use one recursive projection and acceptance authority. A visible
effect presents:

- readable name and description;
- in-world source label when visible;
- category and complete player-relevant mechanics;
- remaining turns, time, scene, uses, or source-bound explanation;
- current and maximum stacks;
- available cure, counterplay, or dispel actions;
- visible linked wound, item, skill/art, quest, or environmental context.

Private and GM-only effects are absent from ordinary rows, details, counts, actions,
maps, quests, news, actor dossiers, and serialized game-screen DTOs. Recursive
projection suppresses identity indexes, internal IDs, route selectors, source-authority
objects, receipts, transition history, pending requests, repair/diagnostic wrappers,
file paths, validation codes, and agent instructions.

Player, NPC, combatant, Guardian, resident, radiant-actor, and spiritual-conflict views
may use different presentation layouts, but must preserve equivalent visible semantics
and action eligibility.

## Documentation and Examples

The implementation change reviews and updates at least:

- `Rules/Block_2.txt`;
- `Rules/Block_5.txt`;
- `Rules/Block_6.txt`;
- `Rules/Block_12.txt`;
- `Rules/Block_14.txt`;
- `Rules/Block_15.txt`;
- `Rules/Block_17.txt`;
- `Rules/Block_19.C.txt`;
- `Rules/Block_25.txt` and `Rules/Block_25.A.txt`;
- `Rules/Block_CLI_Operations.txt`;
- corresponding Mortal worked examples;
- `CLI_API_Specification.md`;
- `OtherGuides/Afterlife_Contract_Matrix.md`;
- `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`;
- `Examples/E_CLI_Afterlife_Turns.txt`;
- `Examples/example_validation_manifest.json`;
- daemon/launcher prompt entrypoints that instruct the GM what to read and author;
- documentation coverage, example validation, and source-guard tests.

Required worked examples cover a buff, debuff, periodic effect, event-triggered effect,
environmental source, item/skill/art source, quest source, wound-derived source, stack,
refresh, expiry, dispel, and afterlife combat condition. At least one example shows a
malformed constructor producing bounded repair without client-owned edits.

## Test Strategy

Implementation follows red-green-refactor with the smallest relevant Focused filters.
The matrix includes:

- common envelope, registered profile, source, target, lifetime, stacking, trigger,
  removal, and identity-index contract tests;
- exact/case/confusable/duplicate target, source, effect, event, and stack identities;
- valid player, NPC, Mortal combatant, Guardian, resident, radiant-actor, and afterlife
  condition carriers;
- representative skill, item, art, Fate Card, quest, wound, environmental, location,
  faction, event, and hazard source adapters;
- apply, independent, stack, refresh, replace, merge, suspend, resume, tick, consume,
  expire, dispel, remove, and source-loss transitions at boundary values;
- periodic and triggered deterministic ordering, retry idempotency, cycle rejection,
  and bounded expansion;
- same-turn source and target identities from composed materialization plans;
- all-or-nothing characteristic and other derived-mechanics projection;
- effect removal without wound mutation and wound transition causing allowed effect
  cleanup;
- write-failure injection after every planned publication path plus post-check failure;
- byte/existence-exact rollback and stale-output suppression;
- bounded repair, protected-authority no-dispatch, coherent resubmission, and retry
  tests;
- console/browser semantic parity and recursive private/internal DTO suppression;
- explicit negative tests proving non-empty legacy carriers are unsupported;
- documentation, manifest, example, prompt, and source-guard coverage;
- a scaling control that detects repeated full-catalog scans per effect or trigger.

Verification uses related Focused tests during implementation, one Fast control at a
meaningful checkpoint, conditional FullValidation/LifecycleIntegration for the changed
accepted-turn and afterlife boundaries, and one PreMerge control immediately before
merge. GitHub Actions are not required or enabled for this work.

## Implementation Sequence

1. Common envelope, registered component contracts, source/target authorities,
   identity index, plan cache, and direct-mutation guards.
2. Mortal player, named-NPC, and combatant carriers plus derived-mechanics consumers.
3. Guardian, resident, radiant-actor carriers and the `combatConditions[]` adapter.
4. Lifecycle scheduler, trigger receipts, bounded pending resolution, and transition
   history.
5. Shared player projection, console/browser parity, privacy, and local actions.
6. GM rules, examples, manifests, source guards, failure injection, and final composed
   verification.

## Explicit Non-Goals

- Runtime migration, promotion, compatibility readers, or repair of old save formats.
- Re-materializing every skill, item, wound, quest, location, faction, or Fate Card as
  part of this feature.
- Wound healing/severity lifecycle, which belongs to #1536.
- Skill and spiritual-art progression materialization, which belongs to #1533/#1534.
- Quest materialization, which belongs to #1537.
- NPC/Guardian/resident/radiant-actor progression materialization, which belongs to
  #1538.
- General Mortal combat redesign or replacement of afterlife spiritual-conflict rules.
- Treating instantaneous action payloads as persistent active effects.
- Natural-language inference of identity, stacking, duration, mechanics, cure, or
  cross-realm authority.
- Turning client-derived Shining blessing entitlements into GM-authored effects.

## Design Self-Review

- Active instances and static source definitions have distinct, explicit boundaries.
- One shared authority governs distributed carriers without introducing a global
  semantic owner file.
- Client-owned identity, stack resolution, lifetime progression, and retry evidence are
  unambiguous.
- The duration model contains no magic persistence sentinel or prose authority.
- Complex effects receive bounded harness work instead of relying on prompt memory.
- Afterlife `combatConditions[]` retain their specialized semantics.
- Wound effects cannot mutate or heal their source wound.
- Atomic composition, rollback, repair, privacy, and UI parity cover every named owner
  family.
- Legacy migration is explicitly excluded and missing-vs-non-empty state behavior is
  defined.
- Dependent materialization issues can extend source adapters without redefining the
  common lifecycle.
- No unresolved placeholders, contradictory product decisions, or implicit
  compatibility promises remain.
