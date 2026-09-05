# T070 Diagnosis History Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make diagnosis and alternative-authoring results immutable, closed, durable,
and exact-replay-aware before their accepted command/publication adapters are enabled.

**Architecture:** Extend the existing wound history with one closed typed result family;
retain the existing strict treatment codec as its treatment member. The history owns
canonical result sealing, append-only ordinals, and replay comparison; no caller-owned
mutable JSON or test-only fingerprint recipe becomes accepted authority.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** GitHub [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T059/T070 and T177's known RED gate.
The local source issue reference is authoritative; do not perform any remote writes.

## Global Constraints

- Work in `E:/Games/worktrees/boe-1536-wound-materialization`, on the existing
  `1536-complete-wound-materialization` branch. Preserve `.serena/` and unrelated changes.
- No migration, compatibility parser, dual write, or old-save fallback is permitted.
- `opportunity_decision` still owns create/worsen; `accepted_transition` still owns
  existing-wound diagnosis and alternative authoring. Do not rewrite tests to erase
  the latter family.
- History is append-only, ordered, and client-authored. Exact semantic replay returns
  the original typed receipt; changed result conflicts even with identical before/after.
- Previous canonical rows must remain byte-for-byte unchanged by an append.
- Diagnosis success contains the complete ordered reveal set; failure contains no facts.
- Alternative authoring appends one route and, for a hidden route, one reachable path.
  The full route/path, including readable names, must eventually be sealed; the existing
  mechanics-only `MortalWoundTreatmentRouteFingerprint` is insufficient for authoring.
- `CanonicalStateNormalizer` remains the only publisher. This foundation exposes no
  incomplete player command or new GM-authored response path.
- Fast remains the entire physically isolated deterministic test project with a five-minute
  limit. File/restart/publication/rollback coverage belongs in Integration.
- Use bounded `pwsh -NoProfile -File .\scripts\test-csharp.ps1`; do not overlap test lanes.
- No push, PR, merge, or issue closure is part of this local checkpoint.

## Approved contract and remaining sequence

Read `specs/1536-complete-wound-materialization/plan.md` around the T067 sealing
boundary (lines 515–547), `data-model.md` history results (lines 146–197), and T059
tests in `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs` before implementing.
The 65 existing RED rows are unfinished implementation, not obsolete fixtures:
11 history, 4 diagnosis, 27 alternative, 20 command, 3 response/repair.

This plan owns the history foundation only. The dependency-ordered remaining T070
work stays mandatory and will receive its own bounded implementation task packet:

1. Sealed diagnosis factory/reducer, including terminal attempts and complete reveals.
2. Append-only alternative factory/reducer and full route/path authority seals.
3. Closed accepted-transition command parsing/composition/recomposition for both kinds.
4. `GameResponse.woundTreatmentAuthorings` and kind-specific `decline|author` repair.
5. Source-derived authority, signed snapshots, common publication/cache/normalizer,
   cold replay/conflict, immutable prior history, zero partial writes, and rollback.
6. Executable GM lifecycle examples and documentation, then the full diagnosis class,
   adjacent controls, and meaningful Fast checkpoint.

No item above is completed by making only the 65 unit rows green. T070 and T177
remain open until their owning complete evidence exists. Existing treatment,
recovery, legacy and spiritual task ownership is not erased or renumbered.

---

### Task 1: Closed diagnosis/alternative results and immutable history

**Files:**
- Create: `BookOfEternityClient/Services/WoundTransitionResult.cs`
- Create: `BookOfEternityClient/Services/WoundHistoryState.TransitionResults.cs`
- Modify: `BookOfEternityClient/Services/WoundHistoryState.cs`
- Modify: `BookOfEternityClient/Services/WoundHistoryState.TreatmentReplay.cs`
- Modify: `BookOfEternityClient/Services/WoundTransitionReducer.cs` (history-intent shape only)
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs` (result cloning/sealing)
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs` (typed constructor argument only)
- Modify: `BookOfEternityClient.Tests/WoundHistoryStateTests.cs`
- Create: `BookOfEternityClient.Tests/WoundTransitionResultTests.cs`
- Read existing RED contract: `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs`

**Interfaces:**
- Consumes `WoundTransitionHistoryIntent`, canonical fingerprint writer, existing strict
  `MortalWoundTreatmentPersistedResult`, and the existing history parser/state validator.
- Produces these exact history-owned seams:

```csharp
internal static string ComputeTransitionResultFingerprint(JsonObject unsealedResult);
internal WoundHistoryParseResult AppendTransition(
    WoundTransitionHistoryIntent intent, string sourceFingerprint,
    string outputFingerprint, string readableSummary);
```

- Add one immutable typed `TransitionResult` to `WoundHistoryTransition`,
  `WoundHistoryReplayProbe`, `WoundAlreadyAcceptedReceipt`, and `WoundTransitionHistoryIntent`.
  Prefer a closed abstract `WoundTransitionResult` family with diagnosis, alternative,
  and the existing treatment implementation as sealed members. The existing
  `TreatmentResult` convenience property can project the treatment member; it must
  not become a second independent result authority. Keep typed treatment parsing,
  coordinate validation, request/receipt persistence, and cold replay strict.
- Return detached canonical JSON for serialization/fingerprint purposes only. Typed
  result objects expose immutable values, not mutable input arrays or JSON aliases.
  A converter or equivalent canonical projection must make serialization through the
  base type or `object` emit the same closed wire object, without runtime type metadata.

- [ ] **Step 1: Observe the existing narrow RED contract**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTests.History_DiagnosisResultRemainsDurable|FullyQualifiedName~MortalWoundDiagnosisTests.History_RejectsAmbiguousOrUnsealedDiagnosisResult|FullyQualifiedName~MortalWoundDiagnosisTests.History_ReplayComparesEveryTypedDiagnosisResultCoordinate"
```

Expected: 11 RED rows at the missing production result-fingerprint seam, clean build.
Do not run the entire Fast lane merely to rediscover the known 65 RED rows.

- [ ] **Step 2: Add result-boundary RED coverage**

In the new deterministic test class, construct canonical history JSON with the same
existing production seal helper used by T059. Add explicit cases for closed/duplicate
fields, wrong types, invalid IDs/fingerprints, unknown result kind, unordered-vs-reordered
fact sealing, duplicate/confusable facts, null/path pair mismatch, cross-kind rejection,
canonical input detachment, and result serialization through `object`/base/receipt.
Include an append case built from a parsed valid result and a history intent; assert:

```csharp
Assert.True(appended.IsValid);
Assert.Equal(before.NextOrdinal + 1, appended.State!.NextOrdinal);
Assert.True(JsonNode.DeepEquals(previousRows[0], appendedRows[0]));
Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
Assert.NotNull(replay.AlreadyAcceptedReceipt!.TransitionResult);
```

Mutating input JSON/arrays or returned JSON must not alter parsed result/history/probe.
Changing only result path, reveal order, result category, or full alternative result
under a recomputed result seal must conflict with the accepted operation.
Add plan-fingerprint coverage showing changed history-intent results change the sealed
plan input. Do not invent test-created evidence to exercise later factories.

- [ ] **Step 3: Implement the closed result codec and production sealing**

Exact diagnosis wire object:

```json
{"kind":"diagnose","diagnosisPathId":"diagnosis_exact","result":"failure","revealedFacts":[],"resultFingerprint":"sha256:..."}
```

Exact alternative wire object:

```json
{"kind":"author_alternative_treatment","authoringRequestRef":"authoring_public","addedRouteId":"route_exact","addedDiagnosisPathId":null,"routeFingerprint":"sha256:...","diagnosisPathFingerprint":null,"resultFingerprint":"sha256:..."}
```

Compute a domain-separated hash of the complete canonical unsealed JSON. Exclude only
the top-level `resultFingerprint`; preserve ordered arrays and explicit nulls. Do not
trust a supplied seal or mutate the input:

```csharp
var payload = unsealedResult.DeepClone().AsObject();
payload.Remove("resultFingerprint");
return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
{
    "book_of_eternity.wound.transition_result", "1",
    WoundAcceptedTurnFingerprintWriter.CanonicalJson(payload)
});
```

Use existing exact identifier/confusable identity/fingerprint/readable text and wound
fact vocabularies/bounds. Required result absence => `wound_history_missing_field`;
kind mismatch, failure with facts, success without facts => `wound_history_invalid_field`;
malformed seal => `wound_history_invalid_fingerprint`; well-shaped incorrect seal =>
`wound_history_result_fingerprint_mismatch`, each at the exact offending field.
Reject unknown/duplicate fields and wrong scalar/array types recursively. Alternative
path ID and seal are both null or both present. Required diagnosis/alternative results
cannot be null; non-result kinds may have omitted/null result but never another kind's
result. Register `author_alternative_treatment` in the history kind registry only here;
its reducer/materialization registration belongs to the following factory slice.

- [ ] **Step 4: Wire immutable append, replay receipt, and plan sealing**

History parsing dispatches by exact outer kind, preserves the strict treatment parser,
and returns an immutable result. Canonical serialization emits the result through its
own codec. `CreateReplayProbe` copies that immutable result. Extend exact equality:

```csharp
// In addition to every existing replay coordinate:
TransitionResultsEqual(transition.TransitionResult, probe.TransitionResult)
```

Exact replay returns a non-null `WoundAlreadyAcceptedReceipt` with the original result.
Changed result returns `wound_history_conflicting_replay` even for unchanged wound state.
Append derives global ordinal from `NextOrdinal` and wound ordinal from the existing
chain, clones/reseals the incoming result through its production codec, constructs one
row, and calls `CreateValidated`; it neither mutates nor reserializes prior row payloads
under a new schema. Respect history limits, chronology, terminality, and output seal.
Keep result cloning explicit in `WoundAcceptedTurnData.CloneTransitionIntent` (immutable
sharing is safe only for this closed immutable type), and include full canonical result
content/null in `WoundAcceptedTurnFingerprintWriter.AppendTransitionIntent`.
Do not implement diagnosis factory/reducer authority or command publication in this task.

- [ ] **Step 5: Direct-cutover old history fixtures and run GREEN controls**

The old generic history helper emits diagnosis rows without a result. Update that helper
to use the production codec/sealing helper while preserving each test's original
chronology/terminal/duplicate assertions. Add alternative kind to the registered-kind
matrix with a valid production-sealed alternative result. Do not accept old unsealed
diagnosis rows for compatibility. Keep treatment constructor calls type-compatible and
preserve all current treatment codec assertions.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundTransitionResultTests|FullyQualifiedName~WoundHistoryStateTests|FullyQualifiedName~MortalWoundDiagnosisTests.History_DiagnosisResultRemainsDurable|FullyQualifiedName~MortalWoundDiagnosisTests.History_RejectsAmbiguousOrUnsealedDiagnosisResult|FullyQualifiedName~MortalWoundDiagnosisTests.History_ReplayComparesEveryTypedDiagnosisResultCoordinate"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~History"
```

Expected: both coherent selections GREEN, clean builds, no timeout/duplicates and
complete cleanup. Use measured Focused headroom only if the history selection exceeds
the default limit. Run additional adjacent cache/reducer controls if a changed boundary
requires them, not all diagnostic lanes by default.

- [ ] **Step 6: Review and checkpoint**

Inspect diffs and request independent spec/quality review. Fix verified findings using
their owning RED/GREEN filter. This foundation changes only client-owned canonical
history; no new GM authoring or player command is exposed. Existing spec data-model
already defines both results. Record that no GM prompt/example/afterlife update is
required for this foundation alone; the later accepted-authoring/publication slice must
update them before the capability is exposed.

Stage only the exact files above plus this plan and T070 evidence in `tasks.md`, inspect
`git diff --cached --name-only`, then commit:

```powershell
git diff --check
git commit -m "feat(wounds): persist typed diagnosis history (#1536)"
```

Keep T070 and T177 unchecked. Continue the next bounded diagnosis factory/reducer task
without asking the sleeping user for routine confirmation.
