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
- `WoundAcceptedTurnPlannerTests`
- `WoundConsequenceBudgetTests`
- `WoundRepairPacketBuilderTests`
- accepted-mechanics cache/scale/rollback extensions

Prove strict schema, identity/carrier/history agreement, legal transitions, staged
wound/effect planning, no effect-driven healing, replay suppression, and all-or-nothing
publication before adding setting workflows.

### Slice B: Mortal adapter

Expected owning tests:

- `MortalWoundOpportunityTests`
- `MortalWoundTreatmentContractTests`
- `MortalWoundRecoveryTests`
- `MortalWoundMaterializationLifecycleTests`

Prove formal and narrative opportunities, optional GM choice, guaranteed trigger,
cross-setting free construction, diagnosis reachability, procedure/course/guaranteed
routes, exact resources/providers/facilities, recovery/deterioration, and terminal
History.

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
  -Filter "FullyQualifiedName~WoundAcceptedTurnPlannerTests|FullyQualifiedName~WoundIdentityStateTests"
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

1. Seal a Mortal opportunity with maximum II.
2. Submit `decision=none`; expect no wound and a consumed opportunity result.
3. Repeat from a fresh fixture with severity I; expect one wound/effect/history/output
   transaction.
4. Submit severity III; expect a bounded repair packet and byte-identical canonical
   state.
5. Repeat with a proven guaranteed trigger and `none`; expect rejection until the
   promised legal wound is supplied.
6. Replay each accepted/declined/guaranteed operation 100 times; expect zero new
   identities, effects, history rows, narration, or notifications.

### B. No wound catalog

Materialize these through the same contract:

- contaminated post-apocalyptic laceration treated by cleansing/suture or an antibiotic
  course;
- magical crystalline burn treated by resonant dust/focus or a specialist ritual.

Assert that no wound-name/cure catalog lookup occurs, while exact registered
requirements/outcomes and resource authority are enforced.

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
  recovery, and healing;
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
