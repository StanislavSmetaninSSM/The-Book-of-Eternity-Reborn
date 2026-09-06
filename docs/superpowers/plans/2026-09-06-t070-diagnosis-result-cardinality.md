# T070 Diagnosis Result Cardinality Alignment

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Never emit an accepted diagnosis intent that the existing closed history
result codec rejects solely because a success has no declared facts.

**Architecture:** Keep the complete diagnosis path schema and sealed factory unchanged.
Add the existing success-result cardinality invariant to the reducer's path/fact gate,
after path availability. An empty path remains structurally representable; an empty
failure remains terminal and durable; success may report facts already known to the player.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** GitHub [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T059/T070, approved diagnosis result
and accepted-command contracts in `data-model.md`. The existing frozen
`MortalWoundDiagnosisTests.History_RejectsAmbiguousOrUnsealedDiagnosisResult` and
`CommandParsing_RejectsAmbiguousOrUnboundDiagnosisResult` both reject `success_empty`.

Source inspection found that `ReadDiagnosisFactArray` and `ValidateDiagnosis` allow
empty `reveals`, while `ValidateDiagnose` only compares the complete declared sequence
and `Success` emits its typed history result without revalidating that result. Observe
the exact behavioral RED below before treating this discrepancy as reproduced.
Execute after treatment-member-shapes Task 1 review, before Task 2 implementation.

## Global Constraints

- Stay in the existing `E:/Games/worktrees/boe-1536-wound-materialization` worktree and
  `1536-complete-wound-materialization` branch. Preserve `.serena/` and unrelated edits.
- No new wound/diagnosis schema restriction: do not prohibit empty paths globally.
- A success carries a nonempty complete ordered declared fact list, not necessarily
  newly learned facts. A failure carries no facts and still seals one terminal attempt.
- Use the production diagnosis factory. No test-authored concrete diagnosis evidence,
  private request seals, guessed hash recipes, or trusted caller result substitution.
- Preserve path-availability diagnostic precedence and all sealed before/after checks.
- No change to history/command codecs, factory signatures, cure mechanics, public commands,
  response fields, state publication, prompts, or other transition kinds in this fix.
- Pure Fast-project tests only, bounded PowerShell 7 runner, one C# lane at a time.
  No broad Fast/Integration/FullValidation/PreMerge, filesystem, lease or restart test.
- No remote writes, push, PR, merge, issue closure, new branch or session cleanup.

### Task 1: Reject empty success before producing diagnosis intents

**Files:**
- Modify: `BookOfEternityClient.Tests/MortalWoundDiagnosisTransitionTests.cs` (add the
  following regression only; existing assertions and helpers stay intact).
- Modify: `BookOfEternityClient/Services/WoundTransitionReducer.cs` (`ValidateDiagnose`
  existing declared-fact conditional and its expected diagnostic text only).

Do not edit parent-owned plans/spec/tasks/ledger. Report any concrete discrepancy in
the provided code before changing the task's scope.

- [x] **Step 1: Add and observe the complete three-row regression**

Add this method before the private fixture helpers in the existing test class:

```csharp
[Theory]
[InlineData("success", false, false)]
[InlineData("failure", false, true)]
[InlineData("success", true, true)]
public void Reduce_DiagnosisResultCardinalityMatchesDurableHistory(
    string outcome, bool declaresKnownFact, bool accepted)
{
    var (beforeRoot, _) = Roots("failure");
    beforeRoot["treatment"]!["diagnosisPaths"]![0]!["reveals"] = declaresKnownFact
        ? new JsonArray("route:clean_and_suture")
        : new JsonArray();
    var before = Parse(beforeRoot);
    var request = Create(before, Parse(Advance(beforeRoot)), outcome);
    var reduced = WoundTransitionReducer.Reduce(request);
    if (!accepted)
    {
        AssertRejected(reduced, "wound_transition_diagnosis_fact_unauthorized");
        return;
    }

    Assert.True(reduced.IsValid, Issues(reduced));
    Assert.Equal(3, reduced.Intents.Count);
    Assert.Single(reduced.Intents.OfType<WoundCarrierTransitionIntent>());
    Assert.Single(reduced.Intents.OfType<WoundAttemptTerminalIntent>());
    var intent = Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>());
    var result = Assert.IsType<WoundDiagnosisTransitionResult>(intent.TransitionResult);
    Assert.Equal(outcome, result.Result);
    Assert.Equal(declaresKnownFact ? new[] { "route:clean_and_suture" } : Array.Empty<string>(),
        result.RevealedFacts);
    Assert.Equal(before.Treatment.KnownRouteIds, reduced.ProposedAfter!.Treatment.KnownRouteIds);
    Assert.Equal(before.Care, reduced.ProposedAfter.Care);

    var empty = WoundHistoryState.CreateValidated(1, Array.Empty<WoundHistoryTransition>());
    Assert.True(empty.IsValid);
    var creation = new WoundTransitionHistoryIntent(before.LastTransition.TransitionId,
        before.WoundId, "create", "operation_create_cardinality", before.Origin.EventRef,
        before.LastTransition.Turn, WoundHistoryState.ComputeNonexistentBeforeFingerprint(before.WoundId),
        WoundIdentityState.ComputeSemanticFingerprint(before), null, null, false);
    var created = empty.State!.AppendTransition(creation, Seal('8'),
        WoundHistoryState.ComputeOutputFingerprint(creation.OperationKey, creation.EventRef, "Created"),
        "Created");
    Assert.True(created.IsValid, string.Join("; ", created.Issues.Select(issue => issue.Code)));
    var appended = created.State!.AppendTransition(intent, Seal('8'),
        WoundHistoryState.ComputeOutputFingerprint(intent.OperationKey, intent.EventRef, "Diagnosed"),
        "Diagnosed");
    Assert.True(appended.IsValid, string.Join("; ", appended.Issues.Select(issue => issue.Code)));
    Assert.Equal(2, appended.State!.Transitions.Count);
    var replay = appended.State.ResolveReplay(
        WoundHistoryState.CreateReplayProbe(appended.State.Transitions[^1]));
    Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
    var durable = Assert.IsType<WoundDiagnosisTransitionResult>(replay.AlreadyAcceptedReceipt!.TransitionResult);
    Assert.True(JsonNode.DeepEquals(result.ToCanonicalJson(), durable.ToCanonicalJson()));
}
```

The remaining existing diagnosis path keeps all hidden fixture routes discoverable;
therefore the empty selected path is a valid full parsed wound, not invalid test setup.
`Advance` changes only last-transition metadata. The successful control intentionally
repeats an already known fact; it must remain accepted and durable.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTransitionTests.Reduce_DiagnosisResultCardinalityMatchesDurableHistory"
```

Expected behavioral RED: empty success is incorrectly accepted; both valid controls
pass including history append and exact replay. Record actual output, not an inferred run.

- [x] **Step 2: Apply the narrow semantic gate**

In `ValidateDiagnose`, after the unchanged path-availability check, prepend this predicate
to the existing disjunction that reports `wound_transition_diagnosis_fact_unauthorized`:

```csharp
evidence.ResultKind == "success" && evidence.RevealedFacts.Count == 0 ||
```

Keep the existing complete sequence/grammar checks and update the expected text to:
`nonempty complete declared reveals for success; no facts for failure`.
No helper extraction, factory exception, automatic downgrade to failure, or global parser
restriction is needed. The invalid request remains representable and returns no intents.

- [x] **Step 3: Verify, self-review and commit only the two owned files**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTransitionTests|FullyQualifiedName~MortalWoundDiagnosisTests.History_|FullyQualifiedName~MortalWoundDiagnosisTests.Reduce_Diagnose_|FullyQualifiedName~WoundTransitionResultTests|FullyQualifiedName~WoundTransitionReducerTests"
```

All selected rows must pass with clean build/output and complete runner cleanup.
Report every RED/GREEN artifact, command, total/executed/passed/failed/skipped count,
wall time, exit code, timeout, duplicate IDs, cleanup and build warnings/errors.
Commit: `fix(wounds): reject diagnosis success without declared facts (#1536)`.
Parent verifies the diff/artifacts and gets independent spec/quality review before completion.

## Verified checkpoint

Complete at `dc89683d..7c4dc6c8`; independent spec/quality Approved, zero
Critical/Important/Minor findings. Actual RED was 2/3
(`20260906-104230-549-19824-a80663785bfd4185af4afcbc5a08e22f-focused`). The first
GREEN was narrower than requested: 198/198, including only the new method from its
owning class (`20260906-104345-067-7584-e1ea35ecdea042a58e2a497c5ef4ec47-focused`).
The controller required the missing full class separately at unchanged code: 73/73
(`20260906-104759-343-30468-4d5a079a0bd34e8d9a40181b01f6791d-focused`). The three
new rows overlap; the complete requested behavior is covered by the two GREEN runs,
not by a falsely claimed execution of the original exact combined filter. All builds
and cleanup are clean, no timeout/duplicates/skips. Parent read actual summaries/log
evidence and the complete diff/review; the reviewer's runtime-evidence caveat is resolved.
Next: standalone treatment-member-shapes Task 2. No feature or issue closure.

## Documentation and completion boundary

This enforces the existing client-owned closed history/result contract already frozen
in tests; it introduces no GM response or player capability. No Mortal/afterlife prompt,
worked example, manifest, afterlife matrix, source guard or daemon entrypoint changes
are required for this narrow reducer alignment. Parent records explicit nonempty-success
wording in the durable data model to remove ambiguity; upcoming GM diagnosis/command
publication work must continue to document the complete authoring workflow.
T070/T177/#1536 remain open; this is not the full diagnosis publication implementation.
