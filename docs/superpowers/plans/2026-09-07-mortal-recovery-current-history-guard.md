# Mortal Recovery Current-History Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reject absent, unreadable, malformed or semantically changed current Mortal wound history before fresh recovery arithmetic, without publishing or repairing state.

**Architecture:** The existing registry first proves the exact accepted-state capability/binding. The planner then reads history under the supplied active write lease, parses it and compares its canonical semantic fingerprint with the signed accepted-state seal. A synchronous explicit-lease reader uses the existing safe snapshot primitive and shared decoder, without recovery/reacquisition or sync-over-async.

**Tech Stack:** C#/.NET 8, xUnit, System.Text.Json, PowerShell 7 bounded C# lanes.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T069 Phase C with bounded T074 documentation/T177 verification in `specs/1536-complete-wound-materialization/tasks.md`.
- Work only in `E:/Games/worktrees/boe-1536-wound-materialization`, branch `1536-complete-wound-materialization`. Preserve unrelated `.serena/`; never stage broadly, change branches, migrate data, push, merge or close issues.
- `MortalWoundRecoveryPlanner.Plan(fs, lease, binding, woundId)` has no caller-supplied clock, plan, fingerprint, mutation, receipt, history, tick, or policy argument. Its four-field result is exactly `Disposition`, `Issues`, `ReplayReceipt`, and `Resolution`.
- Missing/detached/stale registry authority remains `Rejected` before filesystem history admission. Valid authority with invalid current history returns `InvalidHistory`, non-empty frozen `Issues`, null `ReplayReceipt`, null `Resolution`.
- Invalid JSON/root issues are the original `WoundHistoryState.Parse` issues. A valid semantic mismatch uses `mortal_wound_recovery_history_mismatch`; unreadable/unsafe file uses `mortal_wound_recovery_history_read_failed`, both at `WoundHistoryState.HistoryPath`.
- History admission is before wound/policy/time arithmetic. The clock remains `acceptedState.CurrentGameMinute`, never the malformed live clock. Semantically equivalent JSON formatting/BOM changes remain acceptable.
- Under one binding, complete current history must agree with the signed snapshot seal. A later accepted history publication requires a fresh signed pending snapshot and exported binding.
- The supplied active lease is sufficient even outside its ambient ExecutionContext. History reads must not recover pending publications, acquire another lease, write canonical data or register a common plan. Preserve the ordinary non-lease read path's recovery/hooks/retry behavior.
- This task does not select durable recovery receipt/result grammar, cadence consumption policy, legacy preparation metadata, spiritual art schema, or any recovery/death/heal/legacy publisher. Full T069-C/T070/T074/T177/#1536 remains open.
- Tests use the real initial wound publication and accepted-state export. Negative fixture tampering is not an authority producer; do not fabricate bindings, receipts, accepted rows, anchors or after-images. Do not weaken existing frozen assertions.
- One C# owner at a time; no concurrent source edits during runs. Use `scripts/test-csharp.ps1`, Focused five minutes then one Fast five minutes. No unbounded dotnet test, duplicate Fast, PreMerge or unrelated diagnostic suite. Record every actual artifact, failures, counters, diagnostics, timeout/duplicate/cleanup status.
- GM guidance, worked negative continuation, manifest and source guard change together. This is Mortal-only: no afterlife action, command, pending root, matrix row or spiritual scheduling rule changes; no conditional FullValidation is required for this boundary.

## File map

- `BookOfEternityClient/Core/FileSystemManager.cs`: explicit-lease synchronous reader; extract unchanged synchronous decoder.
- `BookOfEternityClient/Services/MortalWoundRecoveryPlanner.cs`: current-history admission and frozen InvalidHistory result.
- `BookOfEternityClient.IntegrationTests/MortalWoundRecoveryTests.cs`: only make existing class partial, retaining its RegressionIntegration category and existing tests/fixture.
- New `BookOfEternityClient.IntegrationTests/MortalWoundRecoveryTests.CurrentHistory.cs`: sixteen real file/lease/restart/no-write rows using the existing private fixture.
- `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`: one deterministic shared-guide/example/manifest text guard.
- `OtherGuides/Wound_Materialization_Contract.md`, `Examples/E_CLI_Wound_Materialization.txt`, `Examples/example_validation_manifest.json`: Mortal-only negative continuation and exact manifest coverage.

### Task 1: Admit only valid current history under the exact recovery lease

**Files:** The eight paths in the File map; no other source/test/GM path is owned.

**Interfaces:**

- Existing `WoundHistoryState.Parse(string?, string)` returns `WoundHistoryParseResult` with `IsValid`, `State`, `Issues`.
- Existing `MortalWoundTreatmentAcceptedStateAuthority.MatchesCompleteHistory(WoundHistoryParseResult?)` canonical-serializes and compares the full signed history seal, not raw JSON bytes.
- Existing `FileSystemManager.ReadFileSnapshotCore(string)` performs safe stable regular-file reads and returns `CanonicalFileReadSnapshot?`; `EnsureValidCanonicalWriteLease` checks exact owner and active status independently of ambient context.
- Existing recovery test private `Fixture.Create`, `RestartForReplay`, `PrepareFreshContinuationTurn`, `TamperPersistedRecoveryEvidence`, `CorruptLiveClock`, byte capture and reflection `InvokePlan` supply real authority. Do not create a second fixture or modify frozen replay/publication tests.
- New internal overload: `string? FileSystemManager.ReadFileSync(CanonicalWriteLease writeLease, string relativePath)`. No public planner signature changes.

- [x] **Step 1: Add the sixteen file-backed behavioral rows and one doc guard.**

Change only the existing recovery class declaration to:

```csharp
public sealed partial class MortalWoundRecoveryTests
```

Create the complete new partial file:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
{
    [Theory]
    [InlineData(false, "malformed")]
    [InlineData(true, "malformed")]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "empty_history")]
    [InlineData(true, "empty_history")]
    [InlineData(false, "changed_fingerprint")]
    [InlineData(true, "changed_fingerprint")]
    [InlineData(false, "directory")]
    [InlineData(true, "directory")]
    public void T069C_CurrentHistoryFailurePrecedesRecoveryArithmeticWithoutWriting(
        bool restart,
        string mutation)
    {
        var scenario = Scenario.CheckedOverflow() with { StartsStabilized = false };
        using var fixture = Fixture.Create(scenario);
        if (restart)
            fixture.RestartForReplay();
        fixture.AssertCarrierIdentityHistoryAgreement();
        AssertPlannerResult(InvokeRecoveryWithoutAmbientLease(fixture), scenario, fixture.WoundId);

        var historyPath = fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath);
        WoundHistoryParseResult? parsed = null;
        switch (mutation)
        {
            case "malformed":
                File.WriteAllText(historyPath, "{");
                break;
            case "missing":
                File.Delete(historyPath);
                break;
            case "empty_history":
                File.WriteAllText(historyPath,
                    "{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}");
                break;
            case "changed_fingerprint":
                fixture.TamperPersistedRecoveryEvidence("history");
                break;
            case "directory":
                File.Delete(historyPath);
                Directory.CreateDirectory(historyPath);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        if (mutation != "directory")
        {
            parsed = WoundHistoryState.Parse(
                File.Exists(historyPath) ? File.ReadAllText(historyPath) : null,
                WoundHistoryState.HistoryPath);
            Assert.Equal(mutation is "empty_history" or "changed_fingerprint", parsed.IsValid);
        }
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();

        var result = InvokeRecoveryWithoutAmbientLease(fixture);

        AssertInvalidHistory(result);
        var issues = Values(result, "Issues").Select(Assert.IsType<ValidationIssue>).ToArray();
        if (parsed is { IsValid: false })
        {
            Assert.Equal(
                parsed.Issues.Select(static issue => (issue.Code, issue.FilePath, issue.Actual)),
                issues.Select(static issue => (issue.Code, issue.FilePath, issue.Actual)));
        }
        else
        {
            var issue = Assert.Single(issues);
            Assert.Equal(mutation == "directory"
                ? "mortal_wound_recovery_history_read_failed"
                : "mortal_wound_recovery_history_mismatch", issue.Code);
            Assert.Equal(WoundHistoryState.HistoryPath, issue.FilePath);
        }
        Assert.DoesNotContain(issues, static issue =>
            issue.Code == "mortal_wound_recovery_checked_time_overflow");
        AssertRecoveryAdmissionDidNotWrite(fixture, governedBefore, treeBefore);
        Assert.Equal(mutation == "directory", Directory.Exists(historyPath));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void T069C_CurrentHistoryAdmissionPreservesSemanticHistoryAndSealedClock(
        bool restart,
        bool reformatWithBom)
    {
        var scenario = Scenario.RequiresStabilization();
        using var fixture = Fixture.Create(scenario);
        if (restart)
            fixture.RestartForReplay();
        fixture.AssertCarrierIdentityHistoryAgreement();
        var historyPath = fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath);
        if (reformatWithBom)
        {
            var original = File.ReadAllBytes(historyPath);
            var history = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(historyPath)));
            var reordered = new JsonObject();
            foreach (var property in history.Reverse())
                reordered.Add(property.Key, property.Value?.DeepClone());
            File.WriteAllText(historyPath,
                reordered.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var reformatted = File.ReadAllBytes(historyPath);
            Assert.False(original.AsSpan().SequenceEqual(reformatted));
            Assert.True(reformatted.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
            Assert.True(WoundHistoryState.Parse(
                File.ReadAllText(historyPath), WoundHistoryState.HistoryPath).IsValid);
        }
        fixture.CorruptLiveClock();
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();

        var first = AssertPlannerResult(
            InvokeRecoveryWithoutAmbientLease(fixture), scenario, fixture.WoundId);
        var repeated = AssertPlannerResult(
            InvokeRecoveryWithoutAmbientLease(fixture), scenario, fixture.WoundId);

        Assert.NotNull(first);
        Assert.NotNull(repeated);
        Assert.Equal(Required(first, "AuthorityFingerprint"), Required(repeated, "AuthorityFingerprint"));
        Assert.Equal(Required(first, "TickKey"), Required(repeated, "TickKey"));
        AssertRecoveryAdmissionDidNotWrite(fixture, governedBefore, treeBefore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void T069C_CurrentHistoryDoesNotOverrideRejectedRegistryAuthority(bool staleBinding)
    {
        using var fixture = Fixture.Create(Scenario.RequiresStabilization());
        var binding = fixture.Binding;
        if (staleBinding)
            fixture.PrepareFreshContinuationTurn(45, "history_guard_stale_binding");
        File.WriteAllText(fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath), "{");
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();

        var result = InvokeRecoveryWithoutAmbientLease(
            fixture, binding, staleBinding ? fixture.WoundId : "wound_other_current");

        AssertPlannerAuthorityRejected(result);
        AssertRecoveryAdmissionDidNotWrite(fixture, governedBefore, treeBefore);
    }

    private static object InvokeRecoveryWithoutAmbientLease(
        Fixture fixture,
        WoundAcceptedTurnBinding? binding = null,
        string? woundId = null)
    {
        Task<object> operation;
        using (ExecutionContext.SuppressFlow())
            operation = Task.Run(() => InvokePlan(fixture, binding, woundId));
        return operation.GetAwaiter().GetResult();
    }

    private static void AssertRecoveryAdmissionDidNotWrite(
        Fixture fixture,
        IReadOnlyDictionary<string, byte[]?> governedBefore,
        IReadOnlyDictionary<string, byte[]> treeBefore)
    {
        Fixture.AssertAllGovernedBytesUnchanged(fixture.FileSystem, governedBefore);
        fixture.AssertCanonicalTreeBytesUnchanged(treeBefore);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem, fixture.Lease, out _, out _));
    }
}
```

Add this fact to `PromptDocumentationCoverageTests.Wounds.cs` beside the scalar
course documentation guard, without changing that existing guard:

```csharp
[Fact]
public void MortalRecoveryCurrentHistoryDocumentation_DescribesClientOwnedAdmission()
{
    var guide = ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md");
    var example = ReadRepoFile("Examples", "E_CLI_Wound_Materialization.txt");
    var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
    foreach (var document in new[] { guide, example })
    {
        foreach (var required in new[]
        {
            "mortal_wound_recovery_history_guard_v1",
            "InvalidHistory", "mortal_wound_recovery_history_mismatch",
            "mortal_wound_recovery_history_read_failed",
            "before recovery arithmetic", "accepted snapshot's sealed world minute",
            "does not publish recovery, healing or a receipt",
            "Never hand-write history, a tick, an anchor or a receipt"
        }) Assert.Contains(required, document, StringComparison.Ordinal);
    }
    Assert.Contains("mortal_wound_recovery_history_guard_v1", manifest, StringComparison.Ordinal);
    Assert.Contains(nameof(MortalRecoveryCurrentHistoryDocumentation_DescribesClientOwnedAdmission),
        manifest, StringComparison.Ordinal);
}
```

- [x] **Step 2: Observe semantic RED before production/documentation changes.**

Run sequentially from the worktree with PowerShell 7:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 5 -Filter "FullyQualifiedName~MortalWoundRecoveryTests.T069C_CurrentHistory"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~MortalRecoveryCurrentHistoryDocumentation_DescribesClientOwnedAdmission"
```

Expected: Integration sixteen executed, ten semantic FAIL (`Rejected` overflow
instead of `InvalidHistory`) and six existing-behavior controls PASS; doc guard
one semantic FAIL (absent prose). A compile/setup failure is not semantic RED:
repair the test setup without weakening real-authority assertions and record it.
Do not proceed if the real baseline fixtures cannot reach the existing planner.

- [x] **Step 3: Add the write-free explicit-lease reader and history admission.**

In `FileSystemManager.ReadFileSync(string)`, retain the complete existing
recovery/read/hook/reacquisition/finally block. Replace only its trailing decoder
block (from `if (snapshot == null)` to `return reader.ReadToEnd();`) with:

```csharp
        return DecodeFileSnapshot(snapshot);
```

Immediately after that method, insert the overload and single shared decoder:

```csharp
    internal string? ReadFileSync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        // The explicit lease already supplies publication quiescence, even when
        // its ambient execution context did not flow to this synchronous caller.
        return DecodeFileSnapshot(ReadFileSnapshotCore(relativePath));
    }

    private static string? DecodeFileSnapshot(CanonicalFileReadSnapshot? snapshot)
    {
        if (snapshot == null)
            return null;

        using var stream = new MemoryStream(snapshot.Content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
```

In `MortalWoundRecoveryPlanner.PlanRegistered`, immediately after the complete
capability/lease/binding rejection gate and before `var wound = ...`, insert:

```csharp
        var historyFailure = ValidateCurrentHistory(fileSystem, writeLease, acceptedState);
        if (historyFailure is not null)
            return historyFailure;
```

Add these private methods beside `RegistryFailure` without modifying the
arithmetic, receipt shape, registry, parser or accepted-state export:

```csharp
    private static MortalWoundRecoveryPlanningResult? ValidateCurrentHistory(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState)
    {
        WoundHistoryParseResult history;
        try
        {
            history = WoundHistoryState.Parse(
                fileSystem.ReadFileSync(writeLease, WoundHistoryState.HistoryPath),
                WoundHistoryState.HistoryPath);
        }
        catch (Exception exception) when (exception is IOException or
                                             UnauthorizedAccessException or
                                             InvalidDataException)
        {
            return HistoryFailure(
                "mortal_wound_recovery_history_read_failed",
                "one readable safe canonical wound-history file under the active lease",
                exception.GetType().Name);
        }

        if (!history.IsValid)
            return InvalidHistory(history.Issues);
        if (!acceptedState.MatchesCompleteHistory(history))
        {
            return HistoryFailure(
                "mortal_wound_recovery_history_mismatch",
                "complete current history matching the accepted snapshot's semantic history seal",
                "current history differs from the accepted snapshot");
        }
        return null;
    }

    private static MortalWoundRecoveryPlanningResult HistoryFailure(
        string code,
        string expected,
        string actual) => InvalidHistory(new[]
        {
            new ValidationIssue(
                WoundHistoryState.HistoryPath,
                IssueSeverity.Error,
                "The current Mortal wound history cannot authorize recovery.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint:
                    "Restore client-accepted history and re-export the signed accepted state; never hand-write history, a tick, an anchor or a receipt.")
        });

    private static MortalWoundRecoveryPlanningResult InvalidHistory(
        IReadOnlyList<ValidationIssue> issues) => new(
        MortalWoundRecoveryPlanningDisposition.InvalidHistory,
        new ReadOnlyCollection<ValidationIssue>(issues.ToArray()),
        null,
        null);
```

`InvalidDataException` must be explicit: it does not derive from `IOException`.
The catch maps only read/path failures; it does not mask programming/lease errors
or rewrite parser diagnostics. Exception messages/absolute paths are not exported.

- [x] **Step 4: Synchronize the Mortal worked negative continuation.**

In both the common wound guide and worked example, insert this exact paragraph
after `contracts remain in the complete route model. Scheduled natural recovery remains separate and unfinished.`
in the `mortal_wound_treatment_scalar_course_v1` prose. Keep it inside the existing
example CDATA; do not add a JSON fence, heading or invented GM command:

```text
Mortal recovery admission example (`mortal_wound_recovery_history_guard_v1`): the
client has accepted a wound and sealed the current pending snapshot. If its current
history is missing or malformed, the planner returns `InvalidHistory` with the
original `wound_history_*` parser issue before recovery arithmetic. A valid but
changed history returns `mortal_wound_recovery_history_mismatch`; an unreadable or
unsafe history file returns `mortal_wound_recovery_history_read_failed`. The same
check applies after reopening the session. This is invalid authority, not a
missed dose, an elapsed recovery tick or permission to improvise healing. Equivalent
JSON formatting remains valid; arithmetic uses the accepted snapshot's sealed world minute,
not a newly edited live clock. The client must restore accepted history or prepare
and export a fresh accepted snapshot after a legitimate publication. Never hand-write history, a tick, an anchor or a receipt.
This admission check does not publish recovery, healing or a receipt; scheduled
natural recovery publication and durable replay remain unfinished.
```

In the existing `wound_mortal_scalar_course_v1` manifest entry, replace its
`coverageLimit` with the following exact single property; append the following
suffix to the existing `validationRoute`; append the one requiredText string.
Do not create duplicate properties or a new entry:

```json
"coverageLimit": "Complete authored route and whole-wound applicability only; actual atomic publication, dose settlement, pointer history, restart and replay are proved by Integration CourseScalarPublication_ lifecycle tests, not by this shape guard. The Mortal recovery admission continuation is text-guarded here and behaviorally proved by T069C_CurrentHistory integration tests; no recovery publisher or durable receipt is claimed."
```

```text
; PromptDocumentationCoverageTests.MortalRecoveryCurrentHistoryDocumentation_DescribesClientOwnedAdmission
```

```json
"mortal_wound_recovery_history_guard_v1"
```

- [x] **Step 5: Verify actual owner regressions and final documentation consumers.**

Run the new sixteen-row Integration filter from Step2 after implementation; then
run these sequential bounded owner controls (the owning filter includes new rows
again with the existing T069B controls so the complete boundary has one artifact):

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 5 -Filter "FullyQualifiedName~MortalWoundRecoveryTests.T069C_CurrentHistory|FullyQualifiedName~MortalWoundRecoveryTests.T069B_|FullyQualifiedName~FileSystemManagerTests.ReadFileSync_DirectoryAtOptionalFileBoundaryFailsClosed|FullyQualifiedName~FileSystemManagerTests.ReadFileSync_RegularFileAtIntermediateParentFailsClosed|FullyQualifiedName~FileSystemManagerTests.ReadFileAsync_DirectoryAtOptionalFileBoundaryFailsClosed|FullyQualifiedName~FileSystemManagerTests.ReadFileAsync_RegularFileAtIntermediateParentFailsClosed|FullyQualifiedName~ExampleDocumentationValidationTests.MortalWoundScalarCourseWorkedExample_ParsesCompleteRoute"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~MortalRecoveryCurrentHistoryDocumentation_DescribesClientOwnedAdmission|FullyQualifiedName~WoundTreatmentScalarCourseDocumentation_UsesCompleteMortalRoute|FullyQualifiedName~WoundHistoryStateTests"
.\scripts\test-csharp.ps1 -Lane Fast
```

Expected all selected rows PASS. Run one Fast with its unchanged five-minute cap.
If that control times out without a failing test, reconcile discovery and every
completed TRX, then run only the exact uncompleted selection once through bounded
Focused. Complete, non-overlapping case coverage may accept this implementation
prerequisite; it does not make the original Fast successful or close T177's
performance/final-verification obligation. An actual failing test still blocks
acceptance. This development checkpoint is not the final PreMerge gate.
Record actual discovered/executed counts separately; raw differences caused by
runtime-expanded theories are not unfinished counts. Inspect every TRX plus
summary/build/error/cleanup metadata, not merely command exit. No complete recovery
class run: its separate durable producer/replay contract remains unimplemented.
No FullValidation: these docs/example/manifest changes are explicitly Mortal-only.
If a genuine new owner failure appears, diagnose it and report before broadening
scope; do not hide or weaken it. No source changes while any lane is running.

- [x] **Step 6: Self-review, exact-file commit and independent acceptance.**

Check all eight owned paths and `git diff --check`; recursively check the final
manifest for duplicate properties. Stage/commit only those exact paths. Report
all changes, semantic RED/GREEN, exact commands/artifact directories, actual
counters and diagnostics, and concerns in the supplied metadata report file.
Parent generates a full recorded-BASE..HEAD review package, independently inspects
source/diff/actual artifacts and obtains fresh Spec Compliance/Quality review.
Only after those gates mark this bounded guard complete. Full T069-C/T070/T074/
T177/#1536 and the overall77/177 task count remain open/unchanged.

## Accepted bounded prerequisite — 2026-09-07

Exact source range `b2bc29f872fb16ea2df436b85096eef0252a517e` to
`f9cd385ca9e64bdfb7a55e8b101d05f5f2910eda`. Parent inspected the complete
eight-file diff, actual source, every result artifact below, final GM changes,
recursive manifest duplicate-property scan and `git diff --check`. Independent
Astra/high review is Spec Compliant / Quality Approved: zero source defects,
zero Critical/Important findings and one retained historical Minor process finding.
The reviewer explicitly accepted the documented development-checkpoint correction
below after reading `AGENTS.md` and `docs/testing.md`; original Fast stays exit 124.

All artifact directories are under `TestResults/test-lanes/`:

| Artifact | Actual evidence |
| --- | --- |
| `20260907-111020-033-3864-1c0c92d9393244af8d42f2aae61f550f-focused` | Integration RED: 6 PASS / 10 expected semantic FAIL, 2:34.324; clean build. |
| `20260907-111050-243-33760-fb3e4efdead547ddbddc4fbf3675f246-focused` | Documentation RED: 1 expected missing-marker FAIL, 1:22.806; two MSB3026 file-lock warnings. |
| `20260907-111130-957-26684-8989161b17d24fc6b6ed19c04467309c-focused` | Duplicate Integration RED: same 6 PASS / 10 FAIL, 1:24.185; one MSB3101 file-lock warning. |
| `20260907-111300-145-50040-b0193bf2570c49b38cded3bec1b59f32-focused` | Repeated documentation RED: 1 expected FAIL, 0:14.700; clean build. |
| `20260907-111431-499-27168-5d7b5929cb7c4850ac59d3e82cd74a85-focused` | New Integration cases: 16/16 PASS, 2:11.630. |
| `20260907-111655-030-39292-c38fc0d4d81d449392aaa55531cf8832-focused` | Integration owners: 42/42 PASS, 2:06.850. |
| `20260907-111902-757-21260-41f373ba809f4c15bfa004c23b5a5356-focused` | Owning parser/documentation cases: 51/51 PASS, 0:33.901. |
| `20260907-112003-876-33800-506b04f7c4d6406aa01e2eb34f55de8d-fast` | One Fast: 7,282 PASS across 21 completed TRXs; timeout 5:00.289, exit 124. |
| `20260907-113236-579-48616-99329022fd574476b7827bca1ff57adf-focused` | Exact uncompleted selection: 347/347 PASS, 1:28.436, five-minute limit. |

Fast discovery has 7,575 rows; seven existing runtime-expanded theories add 54.
Parent reconciled every method: combined 7,629 PASS, no missing methods, no
cross-run method/testId/executionId overlap. Raw method IDs and collapsed complex
argument display labels can repeat inside existing theories; these are not duplicate
executions. All final owner/continuation builds and all cleanup checks are clean;
no skipped tests. This proves complete case coverage, not a successful Fast lane.

The first three RED runs overlapped and caused the three disclosed lock warnings.
This is a process deviation retained for final review. Subsequent runs were drained
before source edits; formatting-only changes followed owner completion and preceded
Fast. No additional RED or whole-Fast rerun was used to erase the evidence.

GM synchronization is Mortal-only: shared wound guide, worked negative continuation,
existing manifest entry and one deterministic source guard. No afterlife contract,
pending/action surface or afterlife example changed, so FullValidation is not
required for this prerequisite. Durable recovery publication/replay, cadence policy,
T069-C/T070/T074/T177, whole-feature completion and final PreMerge remain open.
Top-level completion remains 77/177. Full report, parent audit and independent review
are in this worktree's `sdd/mortal-recovery-current-history-guard-*.md` metadata.

## Parent self-review

### Verification-policy correction after measured timeout

The initial plan's expectation of one completed Fast was stricter than the user's
repository policy, which requires a bounded Fast control during implementation,
keeps Fast fast, permits measured diagnostic follow-up and forbids spending the
wound task on repeated timing attempts. One Fast was run with the five-minute
cap unchanged; it timed out after7,282 PASS. Parent ran only its uncompleted347
cases once through Focused, all PASS, with no cross-run method overlap. All7,629
cases are covered (7,575 discovery plus54 known theory expansions), but the Fast
exit124 remains recorded and T177 remains open. This clarifies this prerequisite's
acceptance scope rather than changing the user's Fast limit, test assertions,
test placement or the full feature's final gate. No second Fast is authorized by
this correction. Independent review must assess this documented policy correction
and complete evidence before bounded acceptance; no source change follows from it.

- This is a bounded independent admission guard, not an attempt to finish Phase C
  before its durable receipt producer. All replay/result/cadence schema choices
  remain outside scope, and the existing four-field public API is unchanged.
- Exactly ten negative file states are exercised after real initial publication
  and both fresh/cold binding export. A valid overflow baseline proves that new
  InvalidHistory dominates numerical planning; parser-valid tamper is asserted
  before the semantic mismatch expectation.
- Four positive controls retain the full real sealed-clock result and stable
  fingerprints despite a malformed live clock, including changed raw history
  bytes with property reordering, indentation and UTF-8 BOM. Two negative authority
  controls prove that stale binding/wrong wound still wins before bad history.
- Every Plan invocation in the new matrix suppresses ambient context flow but
  retains the exact active lease. The shared sync decoder prevents text behavior
  drift without replacing the ordinary reader's recovery or hook sequence.
- Filesystem operations in tests target only the fixture-owned temporary history
  path, and byte/existence assertions cover all governed roots and the complete
  non-runtime tree. Directory fault remains a directory after the read.
- The new partial family keeps the existing class-level RegressionIntegration
  category; deterministic documentation guard stays Fast. No suite taxonomy or
  time limit is changed. Existing tests/fixtures retain all assertions.
- GM prose explains the actual validator failure and permitted client recovery,
  not a prompt-only patch or manual state surgery. No spiritual contract changes.
