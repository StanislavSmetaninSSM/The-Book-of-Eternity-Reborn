# T070 Sealed Mortal Diagnosis Transition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce and validate one immutable, sealed Mortal diagnosis attempt that
reveals only the selected currently available path's exact facts, records success or
failure durably, and cannot heal or otherwise rewrite the wound.

**Architecture:** `MortalWoundTreatmentPlanner.CreateDiagnosisTransition` is the sole
production factory. It derives all wound-local authority from parsed before/after
wounds and the selected path, while accepting only the two already validated external
authority fingerprints. The reducer independently checks that sealed request, current
path availability, and the exact legal after-image. The completed history codec owns
the durable typed result and append/replay semantics. This slice is pure and write-free;
command/source authority and common publication remain subsequent mandatory work.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** GitHub [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T059/T070 and T177's remaining RED
gate. Approved contract: Spec Kit `plan.md` T067 sealing boundary and `data-model.md`
section 9 Diagnosis path plus history and lifecycle sections. Prior foundation:
`docs/superpowers/plans/2026-09-06-t070-diagnosis-history.md`, commit `16e08fca`.

## Global Constraints

- Work in `E:/Games/worktrees/boe-1536-wound-materialization`, on the existing
  `1536-complete-wound-materialization` branch. Preserve `.serena/` and unrelated changes.
- No migration, compatibility parser, dual write, or old-save fallback is permitted.
- `opportunity_decision` owns create/worsen; `accepted_transition` owns existing-wound
  diagnosis and alternative authoring. Do not erase either family to make tests pass.
- The factory derives complete-or-empty diagnosis facts, before/after/path/result seals,
  and the typed result. Callers and tests must not construct concrete evidence or invent
  a test-only fingerprint recipe; negative tests may tamper with factory output.
- Diagnosis success carries the complete ordered reveal set. Failure carries no facts
  and is still a terminal attempt. Neither outcome heals, changes effects, care, severity,
  recovery, authored route/path content, or undeclared display/complication content.
- Selected-path availability uses only facts already known in the before wound, not
  facts that a different as-yet-unperformed diagnosis might reveal. `gm_only` paths are
  never player-selectable. Structural graph reachability is not current availability.
- History is append-only and client-authored. Exact replay returns the original typed
  result; a changed result conflicts even if mechanics are unchanged.
- `CanonicalStateNormalizer` remains the only publisher. This task exposes no incomplete
  command, response, player UI, or GM-authored capability.
- Fast remains the entire physically isolated deterministic project with its five-minute
  limit. No filesystem, lease, restart, or publication test belongs in this pure task.
- Use bounded `pwsh -NoProfile -File .\scripts\test-csharp.ps1`; do not overlap test lanes.
- No push, PR, merge, issue closure, new branch, or session cleanup is part of this task.

### Task 1: Factory-owned diagnosis evidence and exact reducer result

**Files:**
- Create: `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.Diagnosis.cs`
- Modify: `BookOfEternityClient/Services/WoundTransitionReducer.cs` (diagnosis only;
  use a narrow partial file if needed instead of growing unrelated reducer sections)
- Optional narrow extraction: `BookOfEternityClient/Services/WoundTransitionReducer.Diagnosis.cs`
- Create: `BookOfEternityClient.Tests/MortalWoundDiagnosisTransitionTests.cs`
- Modify: `BookOfEternityClient.Tests/WoundTransitionReducerTests.cs` (old diagnosis
  fixture direct cutover only; preserve their original assertions)
- Read existing RED: `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs`
- Read existing production: `WoundTransitionResult.cs`,
  `WoundHistoryState.TransitionResults.cs`, `WoundAcceptedTurnData.CloneWound`, and
  `MortalWoundTreatmentContract.cs` diagnosis fact/reachability validation.

Do not edit history codecs, commands, response models, publication, other transition
kinds, other tests, or parent-owned plans/tasks/ledger without reporting a concrete
dependency first. No alternative factory in this task. Parent owns this plan's evidence.

**Exact frozen API:** one method with this name and arity, returning the request:

```csharp
internal static WoundTransitionRequest CreateDiagnosisTransition(
    string transitionId,
    string commandRef,
    string operationKey,
    string attemptId,
    string eventRef,
    int turn,
    WoundMaterializationEnvelope before,
    WoundMaterializationEnvelope proposedAfter,
    string diagnosisPathId,
    string resultKind,
    string requirementAuthorityFingerprint,
    string checkResultFingerprint);
```

Concrete evidence-record topology is internal. The result is the existing immutable
`WoundDiagnosisTransitionResult`; the history codec owns its seal. Bind the command
reference separately from the exact diagnosis path ID; a path ID is not a command ID.
Use an explicit detached request/evidence representation. Canonical path fingerprints
must include the complete path, including display name, requirements, empty v1 check,
ordered prerequisites/reveals, visibility and failure policy, but no parser SourcePath.
Use production canonical serialization and a versioned domain-separated seal; never
the mechanics-only route hash or a caller-provided path/result seal.

- [ ] **Step 1: Observe existing diagnosis factory RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTests.Reduce_Diagnosis_SealsOneTerminalAttemptAndExactRevealBoundary|FullyQualifiedName~MortalWoundDiagnosisTests.History_DiagnosisReducerIntentAppendsOnceAndReplaysExactResult"
```

Expected: exactly four rows fail at the absent production factory, with a clean build.
Do not run the whole still-RED diagnosis class or Fast to repeat known failures.

- [ ] **Step 2: Add focused authority and semantic RED cases**

Use the existing `WoundContractTestData.CreateActiveWound` and production parser, plus
small local fixture helpers. Do not copy the large original diagnosis test class.
Cover factory-owned evidence using success/failure and exact path identities:

- Both outcomes emit exactly one carrier replacement, one terminal-attempt intent,
  and one nonterminal history intent with exact attempt/path/result and result seal;
  no effect, heal, recovery, legacy, or duplicate intent.
- Success appends only not-yet-known routes in declared order and changes only declared
  unknown complications to `known_to_player`. Already public/known facts remain exactly
  unchanged; do not downgrade a public complication to a weaker visibility.
- Failure permits only required last-transition metadata and no known-fact delta.
- Known/public and currently unlocked hidden paths work; missing, case-confusable,
  `gm_only`, or not-yet-unlocked paths reject. Include a valid structurally reachable
  two-step discovery graph whose second step is not available yet.
- Validly parsed semantic after-image violations reject through the reducer, not an
  exception in the factory: unauthorized fact, missing/reordered/extra route delta,
  private or display leakage, changed route/path content, changed care/severity/recovery,
  and unrelated complication mutation. Existing precise diagnosis diagnostics remain.
- Post-factory mutation of operation/event/turn/transition/command/attempt/path/result,
  external requirement/check seal, before/after seal or wound/path after-image rejects
  with no intents. Include a changed after-image with its public after seal recomputed;
  the rest of the sealed evidence must still prevent transplantation.
- Factory-owned parsed inputs and fact arrays are detached. Mutating supplied mutable
  list/JSON representations or returned canonical JSON cannot alter the original request.
- Malformed/null evidence facts still reject without throwing. Produce a valid factory
  request first and then tamper; do not reintroduce the old four-argument evidence ctor.

Run the new class RED before implementation. Existing method names/signatures/assertions
in `MortalWoundDiagnosisTests.cs` are unchanged.

- [ ] **Step 3: Implement sealed factory and reducer validation**

Derive the path by exact ordinal identity in the parsed before wound. Success facts are
the complete immutable declared array; failure facts are empty. The factory seals the
complete request coordinates, command and attempt identities, selected path, full local
before/after fingerprints, external fingerprints, and complete typed result. Derive,
do not trust, wound ID and local facts/seals. Both external fingerprints must have the
existing lowercase SHA-256 shape. A sealed checksum is not future world authority:
later publication must independently validate those supplied external authorities.

The factory must allow a structurally valid but semantically illegal proposed-after
to reach reducer rejection, since that is the frozen negative-test boundary. Do not
silently repair it or grant authority merely by normalizing its known-route list.

The reducer validates evidence integrity and all exact bindings before using facts.
Keep generic existing request/identity/turn/ordinal checks. Use a narrow diagnosis seal
check before generic before/after comparisons where needed; do not change unrelated
transition diagnostics. Forged/null evidence is a diagnostic failure, never an exception.
The accepted pair is an active physical wound in `mortal_world`. This factory does not
provide an alternate authority route for spiritual diagnosis.

Current known facts are exact `route:<id>` values in `KnownRouteIds`, plus exact
`complication:<id>` values with public/known visibility. A selected public/known path
requires every prerequisite already present; a hidden path also has at least one such
prerequisite. No fixed-point iteration at attempt time and no `gm_only` admission.
World item/skill/provider/facility/location/quest checks remain in the later fresh
external authority boundary, represented here only by their already validated seal.

Success must preserve every field except ordered newly known route additions, exactly
declared unknown-to-known complication visibility, and last-transition metadata. Failure
must preserve every field except last-transition metadata. Do not change `Care.LastAttemptId`
for diagnosis: the terminal attempt belongs to intents/history. Preserve authored array
order and all display text. Reuse existing canonical comparison and fact vocabularies.

- [ ] **Step 4: Emit durable result and cut over old fixtures**

The reducer's success builder carries the factory's immutable result to
`WoundTransitionHistoryIntent.TransitionResult`, sets its exact attempt ID, and adds one
`WoundAttemptTerminalIntent(attemptId, diagnosisPathId, woundId)`. The history intent
remains nonterminal; success/failure terminalizes the attempt, not the wound. No effect
intent is emitted. History append and exact replay work through existing production seams.

Replace old `DiagnoseEvidence` construction in `WoundTransitionReducerTests` with
factory-generated request/evidence using matching transition coordinates. Preserve the
existing undeclared private fact/display/mechanics assertions. The forged-null case must
start from a genuinely valid selectable path/factory request before tampering its facts.
Do not retain an unsealed constructor/fallback to satisfy an obsolete fixture.

- [ ] **Step 5: Run coherent GREEN controls and review**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTransitionTests|FullyQualifiedName~WoundTransitionReducerTests|FullyQualifiedName~MortalWoundDiagnosisTests.Reduce_Diagnosis_SealsOneTerminalAttemptAndExactRevealBoundary|FullyQualifiedName~MortalWoundDiagnosisTests.History_DiagnosisReducerIntentAppendsOnceAndReplaysExactResult|FullyQualifiedName~WoundTransitionResultTests|FullyQualifiedName~WoundHistoryStateTests"
```

Expected: owning complete selection GREEN, warning/error/timeout/duplicate-free, cleanup
complete. Add a narrowly named adjacent control only for an actually changed boundary.
No Fast, Integration, FullValidation, or PreMerge in this pure checkpoint.

Inspect exact diff and factory ownership with source searches; request independent
spec/quality review. Commit only owned implementation/test files with
`feat(wounds): seal diagnosis transitions (#1536)`. Report all RED/GREEN artifacts and
limitations. This internal factory/reducer exposes no GM-authored capability, so no
Mortal World/afterlife prompt/example/manifest/matrix/daemon update is needed here;
those remain mandatory before later command/publication exposure.

Keep T070/T177/#1536 open. Next is the append-only alternative factory/reducer, then the
closed accepted commands, kind-specific response/repair, fresh source authority and
real common publication/cache/replay/rollback, executable GM examples, and full controls.
The three proven baseline treatment-publication failures remain recorded in the history
foundation plan; this task does not remove or weaken them.
