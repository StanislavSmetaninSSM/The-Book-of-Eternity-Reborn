# Quickstart: Implementing and Verifying Complete Wound Materialization

**Feature**: `1536-complete-wound-materialization`  
**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## 1. Read the authority documents

Before editing, read:

1. `spec.md`
2. `research.md`
3. `data-model.md`
4. every file in `contracts/`
5. `tasks.md` after task generation
6. `docs/superpowers/specs/2026-08-26-complete-wound-materialization-design.md`
7. `.specify/memory/constitution.md`
8. `AGENTS.md` and `docs/testing.md`

Issue #1536 is the implementation tracker. #1535 effect materialization and #1543
resource authority are dependencies to extend, not replace.

## 2. Preserve the direct-cutover boundary

Do not add a migration, compatibility reader, dual write, loose wrapper parsing, or
fallback to legacy wound/effect shapes. Update active repository bootstrap data,
fixtures, docs, examples, and tests to schema version 1. The user explicitly waived
pre-release save compatibility.

## 3. Work in TDD slices

Implement only after the owning RED test exists. Keep player commands disabled until
their complete accepted lifecycle exists.

### Slice A: common kernel

Expected owning tests:

- `WoundMaterializationContractTests`
- `WoundIdentityStateTests`
- `WoundCarrierCatalogTests`
- `WoundEffectBatchPlannerTests`
- `WoundConsequenceBudgetTests`
- `WoundRepairPacketBuilderTests`
- accepted-mechanics cache/scale/rollback extensions

Prove strict schema, identity/carrier/history agreement, legal transitions, staged
wound/effect planning, no effect-driven healing, replay suppression, and all-or-nothing
publication before adding setting workflows.

### Slice B: Mortal adapter

Expected owning tests:

- `WoundOpportunityAuthorityTests`
- `MortalWoundOpportunityAdapterTests`
- `MortalWoundTreatmentContractTests`
- `MortalWoundRecoveryTests`
- `MortalWoundMaterializationLifecycleTests`

Prove signed formal, QTE, combat, trap, check, hazard, and narrative occurrences through
one current-snapshot contour; reject missing/stale/tampered occurrence bytes and every
complete-event-set sibling add/remove/reorder. Then prove optional GM choice, durable
decline/materialize replay receipts, guaranteed trigger, cross-setting free construction,
diagnosis reachability, procedure/course/guaranteed routes, exact resources/providers/
facilities, recovery/deterioration, and terminal history.

### Slice C: afterlife adapter

Expected owning tests/extensions:

- `SpiritualWoundOpportunityTests`
- `SpiritualHealingResolverTests`
- `AfterlifeWoundProgressionLifecycleTests`
- `AfterlifeSpiritualConflictValidationTests`
- `AfterlifeSpiritualConflictBalanceTests`
- `AfterlifeEntityProfileValidationTests`
- Elyara and Shining faction materialization tests

Prove all danger modes/formula boundaries, one wound per side, optional wound and
dissipation, bounded defeat, arts 0-V, combat healing, safe-cycle self/provider/entity
healing, natural recovery, Elyara invariants, and visible Shining healer role.

### Slice D: commands, parity, docs

Expected owning tests:

- `WoundPlayerProjectionTests`
- `WoundConsoleBrowserParityTests`
- Explorer/browser command tests
- prompt/source/documentation guards
- full lifecycle/rollback examples

Prove active-only `/раны`, separate History, hidden-ID targeting, privacy, stale-target
rejection, console/browser parity, worked GM examples, and afterlife matrix/manifest
coverage.

## 4. Focused verification rhythm

Use PowerShell 7 and the bounded runner. During implementation, run the smallest
coherent filter. Examples after the corresponding test classes exist:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~WoundMaterializationContractTests"
```

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~WoundEffectBatchPlannerTests|FullyQualifiedName~WoundIdentityStateTests"
```

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 `
  -Lane Focused `
  -FocusedProject Integration `
  -Filter "FullyQualifiedName~MortalWoundMaterializationLifecycleTests"
```

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 `
  -Lane Focused `
  -FocusedProject Integration `
  -Filter "FullyQualifiedName~AfterlifeWoundProgressionLifecycleTests"
```

If a coherent focused selection legitimately exceeds five minutes, measure it and pass
reasonable explicit `-TimeoutMinutes` headroom within the documented limit. Do not
delete coverage or micro-optimize solely to beat an obsolete temporary limit.

Run one Fast checkpoint after the common/realm integration is meaningful:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

The clean feature baseline before implementation was Fast `4339/4339` in
`00:04:33.8143170`, result directory
`20260826-154530-205-21516-38708e3e7c1243dbb26267659d11b6b4-fast`.

## 5. Acceptance scenario matrix

### A. Optional bounded creation

1. Publish one signed accepted occurrence from each formal, QTE, combat, trap, check,
   hazard, and narrative producer, capture its exact bytes in the active pending-turn
   snapshot, and resolve it through correlation-only source input. Reject source
   presence, combat membership, a hazard, dice, or prose without the matching occurrence.
2. Seal a Mortal opportunity with maximum II from one exact occurrence and the complete
   ordered accepted-event set. Add, remove, replace, or reorder a sibling event and
   expect rejection with byte-identical canonical state.
3. Submit `decision=none`; expect no wound-history row, one durable decision receipt,
   and atomic consumption of the occurrence.
4. Restart and replay that exact decline; expect no command, transition, identity,
   narration, or notification. Change the decision for the consumed opportunity and
   expect a semantic conflict.
5. Repeat from a newly signed occurrence/snapshot with severity I and no
   `worseningTarget`; expect one wound/effect/history/receipt/output transaction.
6. Repeat with one explicit exact active-wound `worseningTarget`; expect a worsening.
   Reject null, partial, inferred, stale, foreign, terminal, or duplicate targets.
7. Submit severity III; expect a bounded repair packet and byte-identical canonical
   state.
8. Repeat with a proven guaranteed trigger and `none`; expect rejection until the
   promised legal wound is supplied.
9. Replay each accepted/declined/guaranteed operation 100 times; expect zero new
   identities, effects, history rows, narration, or notifications.

### B. No wound catalog

Materialize these through the same contract:

- contaminated post-apocalyptic laceration treated by cleansing/suture or an antibiotic
  course;
- magical crystalline burn treated by resonant dust/focus or a specialist ritual.

Assert that no wound-name/cure catalog lookup occurs, while exact registered
requirements/outcomes and resource authority are enforced.

For the post-apocalyptic procedure, cover every inclusive margin edge plus a natural 1
with a numerically successful total and a natural 20 with a numerically failed total;
hard requirements must still win. Reject a fresh attempt if any possible band is not a
legal complete transition against the sealed current wound; separately reject every
`success` band that has no applicable positive state change, including a natural-20
no-op. Prove an old route remains structurally valid while an inapplicable fresh attempt
rejects without consuming a new reroll opportunity.
Exercise normal, advantage,
disadvantage, cancellation, tie selection, contiguous dice claims, simultaneous/retried/
restarted claim collisions, and pool exhaustion without caller dice or modifier inputs.
For a player natural 1, prove the exact typed per-attempt Fate Shield adapter selects and
reserves the oldest eligible shield, remaps only `critical_failure -> failure`, consumes
it atomically, and remains restart-safe; prove NPC checks do not consume it and the legacy
GM five-argument report API remains unchanged. Reject a same-turn typed procedure plus
legacy GM critical report before either Fate claim is confirmed. Continue the antibiotic course at due time and its
inclusive deadline, reload from history between milestones, and prove deadline+1 uses
only the declared non-beneficial interruption with no current/future dose consumption.
Prove first-course start requires an empty active-course pointer, persists the complete
typed starting wound, rejects a concurrent second course, and restores exact next-
milestone authority after restart. Allow a procedure/guaranteed attempt during the course,
then prove heal/final completion/interruption clears the pointer.
Reject an empty final milestone/all-empty course; prove only intermediate milestones may
be empty, every non-empty milestone excludes no-op/complication/deterioration operations,
and the ordinal sequence contains an applicable positive treatment operation.
Reject an interruption's effectful or zero-difficulty complication and any neutral/
beneficial deterioration policy.
Separately prove trustworthy lost-dose/consent/co-presence evidence is `Unsatisfied` and
interrupts, while malformed/ambiguous/changed-seal evidence is `InvalidAuthority` and
only rejects. Persist and recompute the complete common/milestone requirement scopes,
successful bindings, typed loss witnesses, and one exact detached receipt; prove replay
does not expose those rows as consumption authority. Validate neutral/beneficial
interruption deterioration policies at the T062/T069 recovery-policy boundary. For the
magical guaranteed route, prove exact actor role, permanent
skill/capability identity, exact `woundDomain=physical`, severity, source fingerprint, operation-limit agreement,
aggregate operation/legacy bounds, deterministic unchanged capability projection from
that same skill, exact/confusable `skillId` uniqueness across current active/passive rows,
any separate sibling tier requirement with a canonical player/NPC skill export, and final
proof re-export from a same-turn composed skill after-image;
reject ordinary capability presence,
an idless or duplicate/confusable skill, a shape-valid transient guarantee, and a plan
that removes/changes the guaranteeing skill before publication. Include one complete typed
effectless complication draft and one complete effectful complication draft, and reject
their old colon-token/incomplete-payload forms before consuming a die. Exercise initial
same-proposal and later opaque `complicationRef` removal rewrite and reject proposal IDs
or canonical refs. Exercise multiple complication/legacy drafts with reused local nested
spellings and prove only operation/ref-namespaced bindings cross the effect boundary;
reject exact/confusable aggregate collisions. Require at most one final `heal`, checked
recovery/capability aggregation, non-heal severity reduction <=2, and heal-staged
reduction <=3 only to reach I. Confirm
that successful route completion is appended once without disabling a later new legal
attempt. Persist the full sealed request through command/pending/history, reload using
history alone, and prove the probe returns a detached request plus receipt with no
actionable intents before reading current state; tampered nested seals are
`InvalidHistory`. Exercise cosmetic plus 1-5-definition/1-5-application
`mechanical_effect` legacies, the eight-row/confusable-ref bounds, and one exact ordered
`WoundEffectOperationBatch` per mechanical legacy with no cosmetic batch. Prove each
batch exports only its matching `wound_legacy/legacyId` source, #1535 allocates every
effect ID, and missing/extra/reordered/merged/split batches reject. Reconstruct the exact
per-legacy #1535 result maps through `Prepare` then `Finalize`, retain non-public
`wound_legacy` authority, derived unique heal/legacy child coordinates, survival through
healing, and history survival after later effect removal. Treat an exact combatant/member wound by procedure/
course and a provider-owned guarantee; require accepted promotion before a target-owned
combatant guarantee.

For treatment item publication, consume one unit from a three-unit stack and prove the
same permanent identity/receipt/carrier remains active at count two with one exact
`consume` transition. Repeat two selected claims against one stack and prove two sequential
before/after transitions rather than one aggregate row. Add item-owned `instance_fixed`
resources and prove maximum/current scale exactly by each remaining/source-count ratio;
reject an inexact quantum or non-instance-fixed capacity without spending a separately
selected resource. Consume the final unit and prove carrier/equipment removal, terminal
`consumed` identity, historical owner authority, and retirement of every live coordinate.
Repeat the final consume with a container, quest, bond, and other companion reference and
prove each rejects before any write; only supported inline equipment clears automatically.
Using only the seven closed ordinary item response properties, repeat with same-turn item
creation/transfer and an NPC guarantee touching the same `npc_core.json`. Prove the sealed
baseline exactly matches the live root after item, ordinary NPC core/trade, and inventory
journal normalizers. Change NPC-core authority, NPC-trade pending bytes, and training
pending bytes independently and prove each invalidates recomposition. Then prove the common
candidate applies the B.2 NPC skill projection and item consumption over that verified
baseline so one final root preserves every change. Set one unrelated response property
non-null and prove the treatment envelope rejects it. Inject a post-write failure and cold
replay, proving byte-exact rollback and no second item/resource transition. Project the
same accepted create/transfer phase twice from detached current/backup roots and prove its
privately derived root receipt/create/transfer transition IDs, command removal, carrier/
index roots, and fingerprint are identical; source-guard that neither path calls the writing
item transition service or random receipt/transition overloads.

For Mortal recovery, create an untreated `requires_stabilization` wound at minute 100,
stabilize it through the sealed treatment/publication path at minute 150, and assert the
condition anchor is cleared while recovery is rebased to 150. At minute 170 submit a
source-shaped formal re-trauma through the manifest-bound T064 adapter, real
`StateDistributor`, `ValidationService`, and common publisher. Assert the client derives
untreated care plus `not_stabilized`, the new deterioration anchor names the exact
published `worsen` transition at minute 170, and the recovery anchor remains byte-
identical to the stabilization-rebased value.

### C. Wound/effect separation

1. Create a wound with pain and bleeding effects.
2. Dispel pain; assert wound severity/care/recovery unchanged.
3. Heal the wound; assert remaining owned bleeding ends.
4. Place an unrelated curse and Saref `memory_suppression` on the owner; assert both
   survive healing.

### D. Spiritual danger and occurrence

Run training, controlled, hostile, and annihilation fixtures at every formula threshold.
Assert:

- training forbids wounds unless pre-escalated;
- controlled caps at II;
- GM may decline or choose lower severity;
- one new wound per side, later worsening only;
- older wounds require explicit re-trauma;
- each spiritual consequence persists as one of the eight registered actor-profile
  effects, projects an exact typed current-side contribution, and never becomes a
  `spiritual_conflict_side` effect or `combatConditions[]` row;
- a non-empty real combat-condition sibling remains independent and byte-identical,
  while conflict close clears only derived wound evidence and retains the actor's wound,
  source graph, root binding, and effect;
- non-training defeat has a bounded anti-repeat result;
- dissipation remains optional.

### E. Spiritual healing

For wound II and healer tier II, cover all four margin bands plus natural 1/20. Assert
the tier gate precedes natural 20. In conflict, prove base cost 5, legal reduction,
floor 2, typed spend, and counterability. Outside conflict, prove one safe cycle, no
action-point spend, no second attempt for the same wound/cycle, and ordinary natural
recovery after a failed session.

### F. Universal natural recovery

- Tier 0 severity IV heals after 20 safe cycles.
- Tier V severity IV heals after 4 safe cycles.
- Progress overflow carries across severity steps.
- Worsening resets current-step progress.
- Player, Guardian, resident, and another actor use the same cycle-key rule.
- Exact replay of a safe cycle adds nothing.

### G. Providers

- Elyara is visible from first Chaos Sea entry, tier V, in the Lazaret, and cannot be
  downgraded.
- Severity I-IV public quotes are 25/50/100/200 at 100%; multiplier endpoints and
  upward rounding are exact.
- Accepted failed/partial attempts charge once; cancel/rollback charges zero.
- Negotiated compensation feeds the same resolver.
- Every Shining faction has a visible `healing_support` resident tier I-V, but public
  command access exists only with a service profile/access proof.

### H. Commands and privacy

Compare console/browser fixtures:

- primary active list excludes healed wounds;
- History is separate;
- target choices list Self first;
- full wound detail or the guided treatment flow starts within at most two selections
  after opening its command;
- same-name nearby actors are visibly disambiguated without IDs;
- moved/stale target fails before resource use;
- hidden routes, symptoms, private NPC wounds, seals, and validator fields never leak;
- costs, choices, outcomes, and errors are semantically equal.

### I. Atomic rollback

Inject every representative owner/severity/slot/effect/treatment/resource/narration
error, correct it through the bounded packet, and prove the repair succeeds without
rewriting valid siblings. Inject publication failures at wound, effect, resource,
profile, scheduler, history, and output stages. Compare exact bytes/existence for every
snapshot path after recovery. Replay each accepted event, attempt, course milestone,
and cycle 100 times and prove no duplicate identity, effect, charge, roll, cycle,
notification, or history row.

## 6. GM documentation acceptance

The completed change must include and validate worked examples for:

- Mortal visible treatment, hidden diagnosis, alternative cure, partial/failure,
  recovery, healing, canonical skill guarantee, and typed client-owned Fate Shield
  treatment compatibility while preserving the legacy GM report path;
- spiritual no-wound choice, lower wound, guarantee, over-limit repair, combat healing,
  self/provider/NPC natural recovery;
- Elyara paid and negotiated service;
- Shining visible healer without mandatory public access;
- terminal History and independent lasting legacy;
- independent Saref memory suppression.

Run focused documentation guards:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests"
```

Then the conditional afterlife/example boundary control required by `AGENTS.md`:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

Use `RegressionIntegration` when the spiritual-conflict exhaustive matrix changes or a
related failure needs diagnosis; it is not an automatic companion to every edit.

## 7. Frontend verification

When browser UI/routes/components change:

```powershell
Push-Location .\BookOfEternityClient.WebFrontend
npm run verify
Pop-Location
```

Use the project-preferred browser skill for rendered desktop/mobile interaction and
privacy checks once the local client can expose the completed workflow. Capture exact
commands/state fixtures and screenshots in PR evidence where visual behavior changed.

## 8. Final controls

Immediately before merge, do not repeat Fast solely as a ritual. Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Completion evidence must name result directories and counts for focused controls,
conditional FullValidation, frontend/browser checks when applicable, and PreMerge. It
must also record Mortal/afterlife prompt/doc/example updates or the explicit no-update
rationale for each reviewed surface.
