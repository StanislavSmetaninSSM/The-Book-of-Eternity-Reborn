# Detached complete spiritual-conflict validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete #1536 T081-A by separating immutable captured conflict inputs from complete evaluation, consumed by the existing production validator.

**Architecture:** Capture one immutable logical image per selected input through unchanged authenticated snapshot readers. Evaluate the full existing conflict checks against that frame, with fresh counters and history trackers each time. Preserve the original production early-return/read-failure paths, current offline/dice fallback and every gameplay calculation; candidate evaluation is not accepted wound-source authority.

**Tech Stack:** C#, .NET 8, xUnit Integration, System.Text.Json, PowerShell 7 bounded test lanes.

## Global Constraints

- Source issue: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536); owning task T081-A. Constitution: `.specify/memory/constitution.md`.
- Design: `specs/1536-complete-wound-materialization/contracts/spiritual-wound-live-turn-boundary.md`. One original player turn, internal GM continuation, one final common publication remain future live-integration requirements, not implemented by this refactor.
- Same worktree `E:/Games/worktrees/boe-1536-wound-materialization` and branch `1536-complete-wound-materialization`; no new branch/worktree or remote operation.
- Accepted prerequisite: `a54fc3d5`, specified and verified in `2026-09-08-spiritual-conflict-frame-baseline.md`. Preserve that genuine zero-error signed resource/publication/final-conflict fixture and its two tests.
- Scope is detached COMPLETE validation, not provisional checks, a wound witness, source admission, new pending/receipt files, source-prefix planning or continuation. T081-B..E and top-level T081/T084/T085 remain open.
- Preserve current arithmetic, clamped/offline compatibility readers, historical membership, rewards, terminal linkage, danger and Shining gates. No overflow fix, migration, new healing operation, authority flag or weakened test assertion.
- Existing snapshot lookup/readers stay unchanged. Security may reread physical controls; one logical captured input does not mean bypassing their authentication. Optional unused signed current Profiles and live request are explicitly uncaptured, not claimed absent.
- Production early-return input handling retains its original narrow acquisition. Only the original two resource-reader exception boundary remains caught; do not suppress unrelated errors or replace it with a trust bypass.
- Use `apply_patch` and absolute worktree paths. The companion patch has portable relative headers: rebase ONLY its two file headers to this worktree before calling the edit tool; never apply them in the default main checkout.
- One implementation agent owns all C# source/test edits and bounded C# execution. No parallel C# runs or source edits while a lane is active. Preserve unrelated `.serena/` without inspection/staging.
- GM synchronization assessment: this internal refactor preserves all GM/player-authored capabilities, fields, diagnostics and semantics. No Mortal/afterlife prompt, example, matrix or manifest change is required. If an actual runtime behavior deviation is discovered, stop and report it rather than silently expanding this rationale.

## File structure and complete production code

- Modify `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`: production entry acquires its root once, preserves early return, consumes the frame/evaluator; remove superseded acquisition methods, retain synchronous checkers.
- Create `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.Frame.cs`: immutable images/frame, authenticated capture, pure context construction and COMPLETE evaluator.
- Modify `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrame.cs`: only optional hooks forwarding in the already accepted test fixture.
- Create `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs`: real frame/publication/input/fault tests and diagnostic fingerprint helper.
- Create `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.Wounds.cs`: repeated reward/history evaluation controls using that owner's existing helpers.

The complete production code is the companion
[`2026-09-08-spiritual-conflict-frame-extraction.patch`](2026-09-08-spiritual-conflict-frame-extraction.patch),
SHA-256 `244159A213F49C735C07CBA694499C2AACF3943707748CBC10174E7792C7E5B8`.
It is an unapplied execution-plan artifact, not a claim that production is changed.
It expands every removal/replacement/new member, including the unchanged original
context; no implementation body is left to inference. Parent verified its removed
1013-line prefix against the actual source and exact retention of the seven pure
context/helper spans. The source below the old line 1014 remains untouched.

### Task 1: Extract and exercise complete validation through the production owner

**Files:** The five exact source/test paths above. Parent owns this plan, its
companion patch, Spec Kit synchronization, acceptance and the progress ledger.

**Interfaces:**

```csharp
// Members of ValidationService; complete definitions are in the companion patch.
internal Task<SpiritualConflictValidationFrame> CaptureSpiritualConflictValidationFrameAsync(
    SpiritualConflictFileImage? conflict = null);
internal IReadOnlyList<ValidationIssue> EvaluateSpiritualConflictValidationFrame(
    SpiritualConflictValidationFrame frame);
```

The existing production wrapper calls the same capture API with its already-read
image. A no-argument call captures the image itself. `WithCandidateImages` preserves
signed baseline/status/turn/dice/settings/request. All input records contain
immutable text/value data; `SnapshotDice` exposes a fresh copy. Evaluation performs
no filesystem read and creates fresh mutable local reward/history state.

- [x] **Step 1: Add only the executable pre-API contract/behavior test**

Use the complete code appendix below to create the resource partial containing
only `ConflictFrame_DetachedApiExistsAndEvaluatesRealPublishedState`,
`PublishCompleteConflictFrameAsync`, `ValidateCompleteConflictFrameAsync` and the
complete namespace-level `ConflictFrameIssueFingerprint` helper. Include their
shown using directives and class/namespace envelope. No new frame type may appear
in this first staged file. Do not modify production code or the fixture yet.

- [x] **Step 2: Observe the intended RED**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_DetachedApiExistsAndEvaluatesRealPublishedState"
```

Expected: real publication and complete owning-phase baseline pass, then
`Assert.NotNull(capture)` fails because the detached capture API does not yet
exist. A compile/setup failure is not this RED. After implementation, this same
test must actually capture/evaluate real published state and compare full issues;
an API-presence-only green is not sufficient.

- [x] **Step 3: Write remaining tests, then apply the exact extraction**

First add the complete strongly typed test code and exact fixture hooks edit in
the appendix (28 new rows in total, in addition to the two accepted baseline rows).
Then apply the complete companion production patch, rebasing only these headers:

```text
*** Update File: E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs
*** Add File: E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.Frame.cs
```

Every actual code body is supplied in the companion patch and appendix. Do not
build an alternate evaluator or invent context defaults. Do not describe the
temporary missing-type compile state between test edits and production edits as
semantic RED. Use the test observed in Step 2 as the component's pre-API RED.

- [x] **Step 4: Run focused GREEN and diagnose only actual failures**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.ConflictFrame_"
```

Expected: 30 rows pass (28 new + 2 retained), zero build warnings/errors, no skipped
rows, duplicate IDs, timeout or incomplete owned-tree cleanup. Use the default
five-minute limit for this narrow selection. If a test setup is invalid, fix only
the data against unchanged mechanics and preserve its assertions. If an extraction
deviates from existing mechanics/authority, diagnose and restore equivalence.
Report any needed product/authority decision before expanding scope.

- [x] **Step 5: Verify owning boundaries, self-review and commit**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeSpiritualConflictValidationTests" -TimeoutMinutes 15
```

These are sequential commands, not concurrent test hosts launched by different
agents. The existing spiritual-conflict owner's retained scheduler cost is 350
seconds (`scripts/test-csharp.ps1`), already beyond the ordinary five-minute cap
before build/discovery, so its complete coherent selection receives bounded
15-minute headroom. Record actual discovery count, TRX count and duration. If it
does not fit, report evidence; do not weaken coverage, reshape lanes or launch an
unbounded test command. The resource owner measured 1:47 for its prior 47 rows and
keeps the default five-minute limit.

Inspect final diffs and evidence before committing only the five owned code/test
files with `refactor: detach complete spiritual conflict validation (#1536)`.
Write the exact RED/GREEN/neighbor commands, artifacts, counters, warnings,
deviations and self-review to the controller-specified report file; release C#
ownership after the commit/report. Do not mark Spec Kit tasks or issues complete.
Parent performs independent task-scoped review, artifact inspection and one
meaningful Fast control for this combined T081-A checkpoint. No duplicate Fast,
full-solution test, PreMerge or unrelated diagnostic lane is part of this task.

## Controller self-check and acceptance boundary

- T081-A's complete frame, current production consumer, genuine signed baseline,
  candidate/read isolation and repeated-state checks are covered by one bounded
  extraction task; no new gameplay requirement is being interviewed or deferred.
- The companion defines every new production type/method. Appendix fixture
  helpers already exist in accepted `a54fc3d5`; optional hook forwarding uses an
  existing context constructor. All later test names/signatures match the patch.
- Parent reviewed missing/empty/malformed input, signed/offline request behavior,
  current-profile read omission, exact source helper retention, and original
  resource-catch semantics. The two resource fault rows use a genuinely validated
  existing manifest object, not a forged frame or validity flag.
- The existing legacy reward/history fixtures establish full issue equivalence
  and exact boundary behavior, not a falsely claimed zero-error whole-game state.
  Only the real resource/publication fixture carries that owning-phase zero-error
  claim. These tests remain Integration because they use canonical files/leases.
- Fast and independent review remain required before parent acceptance of T081-A.
  Source-prefix planning, witness/finalization and same-turn GM continuation stay
  explicitly tracked in T081-B..E; this task does not close the full wounds feature.

## Source-confirmed test corrections during execution

The first complete run executed 29 rows with 26 PASS and three failures. Parent
traced them against unchanged production APIs before revising the test appendix:

- `FileSystemManager.FileExists` invokes the read-attempt hook too. An absent
  conflict has the original null read plus existence check (two hook events);
  present input has one. Do not remove the production existence check.
- A readable Shining root must be the existing `CreateDefaultState()` shape,
  then set `lightSparks=0`; a root with only currency is not valid resource-owner
  fixture data. This adds no new game-state schema or normalization behavior.
- Manifest usability and individual signed-file readability are separate existing
  boundaries. `ReadValidatedPendingTurnSnapshotFileAsync` returns null for a
  byte/hash mismatch while a structurally usable manifest remains usable.
  Preserve the signed realm/profile authority: do not downgrade to current/offline
  context when an individual signed root is absent. The corrected two-row theory
  separately checks actual detached-authority corruption and a tampered signed
  soul. The latter retains signed turn/dice, never captures current Profiles,
  and reports the existing active-wrong-realm error from unavailable signed soul.

This adds one row: 28 new + 2 retained = 30 focused rows. The temporary runtime
deviations made during diagnosis were not accepted; parent mechanically verified
both production files were restored exactly to the companion patch. A seven-row
green obtained with those deviations is historical diagnostic evidence only.
The original executable pre-API RED remains valid and is not repeated. Resume
with the corrected three-method selection (eight rows), then all 30 and the
specified owner controls. Exact correction diagnostic:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_ProductionEarlyReturnDoesNotAcquireUnusedContext|FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_CapturedImagesSurviveAllFilesystemInputsChangingWithoutReads|FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_SnapshotTamperingPreservesManifestAndFileAuthorityBoundaries"
```

## Accepted checkpoint — 2026-09-08

T081-A is accepted at `4cb7d774` (review range `d13bae58..4cb7d774`).
The parent inspected all five changed files and mechanically confirmed both
production files exactly match the companion extraction. Independent review is
Spec compliant / Quality Approved: 0 Critical, 0 Important, one minor report
counter correction resolved below. The review's external-test-evidence caveat
is resolved by the parent's direct summary, log and TRX audit of all nine runs.

Artifacts below are under `TestResults/test-lanes/`; PASS/total counts executions.

| Artifact | PASS/total | Wall time | Evidence |
|---|---:|---:|---|
| `20260908-030611-733-44564-2991e36fedda4c4a95f4562f1c61a577-focused` | 0/1 | 1:20.464 | Required semantic RED after genuine publication |
| `20260908-031001-978-30508-a19b5ba256bb4f83a5179668f6e3677e-focused` | 26/29 | 1:41.376 | Initial three fixture/expectation failures |
| `20260908-031413-457-30404-e6341d2499c8461eba5778d1714970b9-focused` | 5/7 | 0:52.196 | Historical transient-runtime diagnosis |
| `20260908-031654-959-48044-401408f96a1441d28726a86cfe0f59b1-focused` | 7/7 | 0:58.850 | Rejected temporary runtime; NOT accepted GREEN |
| `20260908-032640-559-45760-60c5b50cbced4629a3a1c95cac55fa88-focused` | 8/8 | 1:01.088 | Corrected three-method selection; original runtime restored |
| `20260908-032750-639-29392-56d80d25f2a4480d8a439831a25a902a-focused` | 30/30 | 1:17.090 | Complete frame selection |
| `20260908-032915-682-36572-17d50284ac0f47e88ccb68c2f045bb30-focused` | 71/71 | 2:39.156 | Complete resource owner |
| `20260908-033200-710-41528-6f70d1a713c44eb6925e262c2b2adecb-focused` | 449/449 | 3:34.789 | Complete spiritual-conflict owner |
| `20260908-033832-061-49908-17c96c4d28eb45d7a65517dc4094b333-fast` | 7753/7753 | 4:23.090 | One parent Fast, default five-minute bound |

All nine builds have zero warnings/errors; no run timed out or skipped a test,
and all owned process trees were cleaned up. The accepted GREEN artifacts have
zero failed tests. The spiritual owner discovers 445 descriptors and executes
449 cases: its existing five-row `MalformedActiveCombatConditionCases` MemberData
shares one TRX test ID but has distinct execution IDs. Fast discovers 7699
descriptors and executes 7753 cases across 26 TRXs; no execution-ID duplicate or
cross-TRX duplicate test ID exists. The parent's comparison with accepted Fast
`20260907-222435-922-50316-773d6d82fc79454e815e8d13858f7af8-fast` found the same
3954 methods and exactly the same execution-row count for every method. Dynamic
theory expansion is not a duplicate execution. The implementer report's former
449/449 discovery/execution wording has been corrected to 445/449.

No Mortal World or afterlife prompt, documentation example, matrix, manifest or
GM source guard needs a change: this is an internal, behavior-preserving complete
validator extraction with the existing production consumer. No new GM capability,
field, authority or diagnostic is exposed. T081-B..E, top-level T081/T084/T085 and
the full wounds feature remain open; the 79/177 top-level accepted count is
unchanged. Next is B1 ordinary reduction/assembly, followed by mandatory B2 real
causal prefix execution; neither is implied by this acceptance.

## Reviewed executable code appendix

The following complete appendix was inspected by the parent against the accepted
fixture and the exact production patch. Its proposal/evidence labels preserve
provenance: no executable result is claimed until the steps above run. Main-plan
scope, ordering and verification instructions above govern execution.

# T081-A complete detached conflict frame — executable test-code proposal

2026-09-08; #1536. Controller execution-plan input only, not applied code, test
evidence, review acceptance, or completion. No repository source/test/docs writes
and no C#/build/test commands were performed. Only metadata proposals were edited.

Read parent plan:
`docs/superpowers/plans/2026-09-08-spiritual-conflict-frame-baseline.md`.
The fixture child owns the actual published zero-error baseline and its verification.
This proposal must be independently inspected and adapted to that accepted helper
version before the parent applies/runs it. It does not assume that reading the
fixture draft proves its tests pass.

## Interface and fixture edits

The tests consume the complete corrected frame API in the companion
`spiritual-conflict-frame-complete-code-proposal.patch`:

```csharp
internal Task<ValidationService.SpiritualConflictValidationFrame>
    ValidationService.CaptureSpiritualConflictValidationFrameAsync(
        ValidationService.SpiritualConflictFileImage? conflict = null);
internal IReadOnlyList<ValidationIssue>
    ValidationService.EvaluateSpiritualConflictValidationFrame(
        ValidationService.SpiritualConflictValidationFrame frame);
```

These are signature descriptions with explicit containing type, not additional C#
declarations to paste. Actual declarations are in the companion patch. Frame
`Candidate`, `Baseline`, `HasValidatedSnapshot`, `SnapshotTurnNumber`,
`SnapshotDice`, `DifficultySettings`, `TurnRequest`, and
`WithCandidateImages(SpiritualConflictCandidateImages)` are internal.
`SnapshotDice` returns a fresh array. `Candidate.Profiles` and `TurnRequest`
are nullable: null means intentionally not captured, not a captured absent file.
`WithCandidateImages` cannot replace signed status, baseline, dice, turn, request
or settings. No tests call the private frame constructor or inject a validity flag.

The accompanying production metadata patch has also been corrected for the
parent's signed-profile review: current Profiles is captured only offline, both
offline builders use `?.Text`, and signed mode retains its actual signed-profile
reader. The review skill led to verification against the two existing async
authority builders, not a new authority policy.

Apply this exact fixture-only edit after baseline acceptance (not during its child's
current task); no change to the snapshot/security helper is required:

```diff
*** Begin Patch
*** Update File: BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrame.cs
@@
 using System.Text.Json.Nodes;
+using BookOfEternityClient.Core;
@@
-    private static async Task<ResourceMaterializationTestContext> CreateCompleteConflictFrameContextAsync()
+    private static async Task<ResourceMaterializationTestContext> CreateCompleteConflictFrameContextAsync(
+        FileSystemManagerHooks? hooks = null)
     {
-        var context = await ResourceMaterializationTestContext.CreateAsync();
+        var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
*** End Patch
```

The existing `ResourceMaterializationTestContext.CreateAsync(FileSystemManagerHooks?
hooks = null)` already supports this. Other required helpers remain the parent's
actual `WriteCompleteConflictFrameExchangeAsync(ResourceMaterializationTestContext)`,
`AssertNoConflictFrameErrors(IEnumerable<ValidationIssue>)`, `PeekPlanAsync` and
the real common publisher; no forged plan, receipt or publication is introduced.

## TDD staging: executable RED, then complete typed coverage

1. After the baseline fixture is accepted, add only the following members from the
   complete resource partial below: `ConflictFrame_DetachedApiExistsAndEvaluatesRealPublishedState`,
   `PublishCompleteConflictFrameAsync`, `ValidateCompleteConflictFrameAsync`,
   plus the complete namespace-level `ConflictFrameIssueFingerprint` helper.
   Use the shown using directives and partial class envelope. These members have
   **no compile-time reference to any new frame type**. No fixture hooks edit is
   necessary for this first run.
2. Run exactly the reflection method before production detachment exists:

   ```powershell
   .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_DetachedApiExistsAndEvaluatesRealPublishedState"
   ```

   Expected semantic RED: the real raw publication and final owning-phase baseline
   are zero-error, then `Assert.NotNull(capture)` fails. A fixture/setup/build
   failure is **not** this RED and must not be reported as such.
3. After that isolated semantic RED, parent adds the hooks forwarding edit and
   the remainder of both complete test files below BEFORE applying the reviewed
   production extraction. The typed tests describe the required implementation;
   their temporary missing-type build is not a second semantic RED. Parent then
   applies the extraction and reruns the same reflection method plus the bounded
   frame filter. The reflection method now
   invokes the **real captured frame and evaluator** and compares complete issues;
   it does not become a meaningless “method exists” green.
4. Keep the rest strongly typed. Do not add all of it before the first RED and
   describe missing-type compile failures as evidence. Do not use reflection to
   bypass signed authority or manufacture frames.
5. Suggested final bounded frame control:

   ```powershell
   .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.ConflictFrame_"
   ```

   Use the default five-minute Focused cap for these 28 new rows; do not enlarge it
   without measured evidence. Parent owns the neighbors/Fast checkpoint and the actual artifact inspection.
   No control was executed by this drafting agent.

## Coverage and intentional limits

28 new xUnit rows: 24 resource-frame rows and 4 specialized reward/history rows.
The preexisting baseline/signed-die sensitivity rows are additional, not duplicated
here as new source methods.

| Rows | Actual boundary |
| --- | --- |
| 1 | Executable pre-API semantic RED; real frame evaluation after implementation |
| 3 | Published zero-error baseline, signed bad die, current/candidate terminal linkage; ordered complete issue equality |
| 1 | All selected current and every mapped signed snapshot input changed after capture; zero observed read activity and unchanged results; new capture sees invalid input |
| 1 | SnapshotDice getter alias immunity in original and derived frames |
| 1 | Candidate isolation; signed realm/player tier/entity tier cannot be replaced by alleged current authority |
| 1 | Offline current realm remains current authority; profiles captured, signed status remains false |
| 1 | Signed nonempty dice omit only the extra live-request capture; signed current Profiles remain null/unread |
| 5 | Production early branch: missing, empty, whitespace, malformed, non-object conflict; no snapshot, forbidden unused current reads |
| 4 | Offline absent/empty/malformed/mixed-array request; current fallback shape/pool behavior on a recent proof |
| 2 | Genuinely signed absent/empty manifest dice use captured live fallback; later request mutation cannot affect old frame |
| 1 | Tampered snapshot is unusable without re-signing or authority bypass |
| 2 | Genuine prevalidated scope plus exact signed definitions/state read faults; narrow caught resource context, skipped state read on definitions failure, deterministic detached/wrapper equality |
| 2 | Nonzero ink-feather/light-spark rewards do not accumulate across repeated evaluation; candidate delta negative |
| 2 | Active/recent historical occurrence trackers are fresh per evaluation and cannot authorize a duplicate occurrence |

The fingerprint includes every public diagnostic field, internal repair-target
sequence, issue order, multiplicity and nulls. It asserts currently unused
structured repair contexts remain null rather than silently ignoring them.
Specialized legacy reward/history fixtures are not relabelled zero-error: they
retain full equality plus exact owning-boundary assertions; only the genuine
resource/publication baseline carries zero-error assertions.

Read-demand tests count the existing canonical attempt hook for comparisons and
also monitor canonical-open/runtime-open/existence-follow-up hooks during detached
evaluation. There is no IO facade substitution. Nonempty signed dice does not
mean global zero request reads: both paired fixtures run real authenticated
lookup/readers and retain their request-control reads. The only difference is
the extra fallback image acquisition (one canonical attempt). Signed profile
assertions use the exact current path, not its authenticated snapshot path.

The no-snapshot early-return rows arm failures on unused current-context paths;
security-wide snapshot reads therefore cannot confound the preservation check.
After the production check they disarm the hook and explicitly capture the
complete frame, as the API intentionally permits. The capture contract is not
weakened to make production's original early route lazy.

Offline active exchanges deliberately call `WithoutCurrentTurnDiceAuthority`
(source `ValidateConflictExchange`); the mixed-array fallback sensitivity row
therefore uses a recent proof, which actually consumes live fallback authority.
The test does not invent stricter offline source admission. Signed live fallback
mutates dice only while preserving existing session/request/turn identity; no
authority re-signing follows that mutation. Both captures explicitly assert
`HasValidatedSnapshot`. This preserves current full-state behavior, not future
T081-C signed-only loading.

No arithmetic/overflow production change, source witness, provisional API,
continuation API, unused-helper acceptance, or old-fixture relaxation is included.
The two resource-acquisition fault rows exercise the narrow existing catch without
global security suppression. This 28-row proposal tests actual final frame
inputs/evaluation rather than attempting to redesign the snapshot loader.

### Additional two resource-fault rows: exact stage and security boundary

Add `ConflictFrame_ResourceSnapshotReadFailurePreservesNarrowCaughtContext` and
`ReadGenuinelyValidatedConflictFrameManifestAsync` together with the remaining typed
tests in stage 3, after the isolated reflection semantic RED and BEFORE production
extraction. They use the already specified fixture hooks forwarding edit and the
existing `ConflictFrameReadProbe`; no additional production/helper signature or
test-only flag is needed. Both rows stay in the same five-minute focused control.

The only extra reflection accesses the existing private authenticated lookup and
its returned private manifest type. The new frame API remains strongly typed.
The helper first asserts no prevalidated override exists, invokes the unchanged
lookup, asserts actual `Usable` and a nonnull manifest, and returns that same object.
No serialization, fabricated manifest, re-signing or claimed validity is used.
The existing `UsePrevalidatedPendingTurnSnapshotScope(object?)` preserves this
already-typed object's identity (AcceptedTurnAndInkFeathers lines 14–32). It skips
only the repeated initial lookup just as that production scope already does.

Faults are armed afterward at exactly the real manifest's signed definitions or
state snapshot path. The hook's `InvalidOperationException` is not a transient IO
retry and propagates from `OpenCanonicalReadStreamAsync` through the bytes reader
into the original narrow two-resource catch. No stack matching or global call-count
threshold is used. A definitions fault has an open event for definitions and none
for state; a state fault has both. Each successful signed reader still follows
`ReadValidatedPendingTurnSnapshotFileAsync` and its detached authority/current
request checks; positive observed authority/request attempts verify those reads
were not globally bypassed. The faulted reader itself cannot verify bytes it never
opened, and this is precisely the old caught acquisition-failure case.

Both tests assert signed status survives, `ResourceAcquisitionFailed` is true,
both resource images are null, and every other captured input matches the unfaulted
control. COMPLETE evaluation still emits `afterlife_conflict_resource_projection_missing`,
is repeatable without reads, and exactly matches the production wrapper under the
same scope/fault. After scope disposal and hook removal, genuine acquisition again
has the original baseline and zero owning-phase errors. No fault operation writes
any snapshot or authority bytes. This is drafted code only; no C# run is claimed.

## Complete new resource partial

Target: `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs`.

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string FrameSoulPath = "game_state/meta/soul_state.json";
    private const string FrameRequestPath = "input/turn_request.json";
    private const string FrameManifestPath = "game_state/control/pending_turn_snapshot.json";

    // Stage RED: this method and the non-frame helpers compile before the API exists.
    [Fact]
    public async Task ConflictFrame_DetachedApiExistsAndEvaluatesRealPublishedState()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var expected = await ValidateCompleteConflictFrameAsync(context);
        AssertNoConflictFrameErrors(expected);

        var capture = typeof(ValidationService).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(method => method.Name == "CaptureSpiritualConflictValidationFrameAsync" &&
                method.GetParameters() is { Length: 1 } parameters && parameters[0].IsOptional &&
                parameters[0].DefaultValue is null);
        Assert.NotNull(capture); // First executable semantic RED, not a missing-type build.
        var pending = Assert.IsAssignableFrom<Task>(capture!.Invoke(context.Validator, new object?[] { null }));
        await pending;
        var resultProperty = pending.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var frame = resultProperty!.GetValue(pending);
        Assert.NotNull(frame);
        var evaluate = typeof(ValidationService).GetMethod(
            "EvaluateSpiritualConflictValidationFrame",
            BindingFlags.Instance | BindingFlags.NonPublic, null, [frame!.GetType()], null);
        Assert.NotNull(evaluate);
        var actual = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            evaluate!.Invoke(context.Validator, [frame]));
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(actual));
        AssertNoConflictFrameErrors(actual);
    }

    [Theory]
    [InlineData("published")]
    [InlineData("unauthorized_die")]
    [InlineData("terminal_game_over")]
    public async Task ConflictFrame_WrapperAndDetachedHaveIdenticalCompleteIssues(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        AssertNoConflictFrameErrors(await ValidateCompleteConflictFrameAsync(context));
        if (mutation == "unauthorized_die")
        {
            var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
                AfterlifeSpiritualConflictState.StatePath));
            root["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        }
        else if (mutation == "terminal_game_over")
        {
            var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameSoulPath));
            soul[AfterlifeSpiritualConflictState.TerminalGameOverProperty] = new JsonObject
            {
                ["state"] = AfterlifeSpiritualConflictState.TerminalSoulDissipationState,
                ["message"] = AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage,
                ["conflictId"] = "conflict_resource_cost"
            };
            var original = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
            var candidate = original.WithCandidateImages(original.Candidate with { Soul = FrameImage(soul) });
            AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(original));
            Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(candidate), issue =>
                issue.Code == "afterlife_conflict_player_soul_dissipation_unlinked_game_over");
            await context.WriteExactJsonAsync(FrameSoulPath, soul.ToJsonString());
        }

        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        var expected = await ValidateCompleteConflictFrameAsync(context);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(actual));
        if (mutation == "published")
            AssertNoConflictFrameErrors(actual);
        else
            Assert.Contains(actual, issue => issue.Severity == IssueSeverity.Error &&
                issue.Code == (mutation == "unauthorized_die"
                    ? "afterlife_conflict_dice_value_not_authorized"
                    : "afterlife_conflict_player_soul_dissipation_unlinked_game_over"));
    }

    [Fact]
    public async Task ConflictFrame_CapturedImagesSurviveAllFilesystemInputsChangingWithoutReads()
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks);
        // Populate both optional captured inputs before a genuine fresh signed capture.
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["lightSparks"] = 0;
        await context.WriteExactJsonAsync(ShiningAbodeState.StatePath, shining.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath,
            """{"difficulty":"normal"}""");
        await context.CaptureValidatedPendingSnapshotAsync(42, "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        await PublishCompleteConflictFrameAsync(context);
        // This fixture normally has no settings; readable normal settings require
        // their actual complete dice audit even though the modifier remains zero.
        var configuredRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        configuredRoot["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["difficultyAudit"] = new JsonObject
        {
            ["difficulty"] = "normal",
            ["source"] = "game_state/core/game_settings.json.difficulty",
            ["oppositionModifier"] = 0,
            ["rewardMultiplierPercent"] = 100
        };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, configuredRoot.ToJsonString());
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.Null(frame.Candidate.Profiles);
        Assert.Null(frame.TurnRequest);
        Assert.NotNull(frame.Baseline.Conflict);
        Assert.NotNull(frame.Baseline.Soul);
        Assert.NotNull(frame.Baseline.Shining);
        Assert.NotNull(frame.Baseline.Profiles);
        Assert.NotNull(frame.Baseline.ResourceDefinitions);
        Assert.NotNull(frame.Baseline.ResourceState);
        Assert.False(frame.Baseline.ResourceAcquisitionFailed);
        var expected = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(expected);
        var candidateBefore = frame.Candidate;
        var baselineBefore = frame.Baseline;

        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameManifestPath));
        var signedPaths = Assert.IsType<JsonObject>(manifest["files"]).Select(
            pair => pair.Value!.GetValue<string>()).ToArray();
        var currentPaths = new[]
        {
            AfterlifeSpiritualConflictState.StatePath, FrameSoulPath, ShiningAbodeState.StatePath,
            AfterlifeEntityProfileState.StatePath, AfterlifeSpiritualConflictState.DifficultySettingsPath,
            FrameRequestPath, ResourceMaterializationTestContext.DefinitionsPath,
            ResourceMaterializationTestContext.StatePath, PendingTurnSnapshotAuthority.AuthorityPath,
            FrameManifestPath
        };
        foreach (var path in signedPaths.Concat(currentPaths).Distinct(StringComparer.Ordinal))
            await context.WriteExactJsonAsync(path, "{ deliberately changed after capture");

        probe.Start();
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        var again = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        probe.Stop();
        Assert.Empty(probe.Events);
        Assert.Same(candidateBefore, frame.Candidate);
        Assert.Same(baselineBefore, frame.Baseline);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(actual));
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(again));
        var changed = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.False(changed.HasValidatedSnapshot);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(changed),
            issue => issue.Code == "afterlife_conflict_state_invalid_json");
    }

    [Fact]
    public async Task ConflictFrame_SnapshotDiceGetterCannotMutateCapturedOrCandidateFrame()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        var clone = frame.WithCandidateImages(frame.Candidate);
        var before = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(before);
        var exposed = Assert.IsType<int[]>(frame.SnapshotDice);
        Assert.Equal(new[] { 15, 5, 12, 8 }, exposed);
        exposed[0] = 14;
        Assert.NotSame(exposed, frame.SnapshotDice);
        Assert.Equal(new[] { 15, 5, 12, 8 }, frame.SnapshotDice);
        Assert.Equal(new[] { 15, 5, 12, 8 }, clone.SnapshotDice);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(before),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
        Assert.Equal(ConflictFrameIssueFingerprint.Create(before),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(clone)));
    }

    [Fact]
    public async Task ConflictFrame_CandidateCannotReplaceSignedRealmOrPlayerAndEntityTierAuthority()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        var originalImages = frame.Candidate;
        var expected = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(expected);
        Assert.Null(originalImages.Profiles);
        var soul = JsonNode.Parse(frame.Candidate.Soul.Text!)!.AsObject();
        soul["currentRealm"] = "Mortal World";
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 5;
        var profiles = JsonNode.Parse(frame.Baseline.Profiles!)!.AsObject();
        foreach (var profile in profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>())
            profile["standardArts"]!["pressure"] = 5;
        var candidate = frame.WithCandidateImages(frame.Candidate with
        {
            Soul = FrameImage(soul),
            Profiles = FrameImage(profiles)
        });
        // Merely changing current alleged authority cannot alter this signed evaluation.
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(candidate)));
        Assert.True(candidate.HasValidatedSnapshot);
        Assert.Same(frame.Baseline, candidate.Baseline);
        Assert.Equal(frame.SnapshotTurnNumber, candidate.SnapshotTurnNumber);

        var root = JsonNode.Parse(candidate.Candidate.Conflict.Text!)!.AsObject();
        var active = root["activeConflict"]!.AsObject();
        active["realm"] = "Shining Abode";
        var cost = active["exchangeLog"]![0]!["actionCostAudit"]!;
        cost["player"]!["artTier"] = 5;
        cost["opposition"]!["artTier"] = 5;
        var changed = candidate.WithCandidateImages(candidate.Candidate with { Conflict = FrameImage(root) });
        var issues = context.Validator.EvaluateSpiritualConflictValidationFrame(changed);
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_active_realm_mismatch");
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_action_cost_art_tier_authority_mismatch");
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_opposition_action_cost_art_tier_authority_mismatch");
        Assert.Same(originalImages, frame.Candidate);
        Assert.Null(frame.Candidate.Profiles);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }

    [Fact]
    public async Task ConflictFrame_OfflineCandidateUsesCurrentRealmAndCapturesProfilesWithoutAcquiringSignedStatus()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        await context.DeleteAsync(FrameManifestPath);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.False(frame.HasValidatedSnapshot);
        Assert.NotNull(frame.Candidate.Profiles);
        var original = ConflictFrameIssueFingerprint.Create(
            context.Validator.EvaluateSpiritualConflictValidationFrame(frame));
        var soul = JsonNode.Parse(frame.Candidate.Soul.Text!)!.AsObject();
        soul["currentRealm"] = "Mortal World";
        var changed = frame.WithCandidateImages(frame.Candidate with { Soul = FrameImage(soul) });
        Assert.False(changed.HasValidatedSnapshot);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(changed),
            issue => issue.Code == "afterlife_conflict_active_wrong_realm");
        Assert.Equal(original, ConflictFrameIssueFingerprint.Create(
            context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
        // Offline is deliberately not a newly strict signed-only source loader.
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)), original);
    }

    [Fact]
    public async Task ConflictFrame_SignedDiceSkipOnlyTheAdditionalLiveRequestAndCurrentProfilesRead()
    {
        var populatedProbe = new ConflictFrameReadProbe();
        var emptyProbe = new ConflictFrameReadProbe();
        await using var populated = await CreateCompleteConflictFrameContextAsync(populatedProbe.Hooks);
        await using var empty = await CreateCompleteConflictFrameContextAsync(emptyProbe.Hooks);
        await empty.CaptureValidatedPendingSnapshotAsync(42, "Chaos Sea", preGeneratedDices1d20: []);

        populatedProbe.Start();
        var populatedFrame = await populated.Validator.CaptureSpiritualConflictValidationFrameAsync();
        populatedProbe.Stop();
        emptyProbe.Start();
        var emptyFrame = await empty.Validator.CaptureSpiritualConflictValidationFrameAsync();
        emptyProbe.Stop();
        Assert.True(populatedFrame.HasValidatedSnapshot);
        Assert.True(emptyFrame.HasValidatedSnapshot);
        Assert.Null(populatedFrame.TurnRequest);
        Assert.NotNull(emptyFrame.TurnRequest);
        Assert.Null(populatedFrame.Candidate.Profiles);
        Assert.Null(emptyFrame.Candidate.Profiles);
        Assert.Equal(0, populatedProbe.Attempts(AfterlifeEntityProfileState.StatePath));
        Assert.Equal(0, emptyProbe.Attempts(AfterlifeEntityProfileState.StatePath));
        var securityReads = populatedProbe.Attempts(FrameRequestPath);
        Assert.True(securityReads > 0); // Existing verification reads remain real.
        Assert.Equal(securityReads + 1, emptyProbe.Attempts(FrameRequestPath));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "afterlife_conflict_state_empty")]
    [InlineData(" \r\n\t", "afterlife_conflict_state_empty")]
    [InlineData("{", "afterlife_conflict_state_invalid_json")]
    [InlineData("[]", "afterlife_conflict_state_invalid_json")]
    public async Task ConflictFrame_ProductionEarlyReturnDoesNotAcquireUnusedContext(
        string? conflictText, string? expectedCode)
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await ResourceMaterializationTestContext.CreateAsync(probe.Hooks);
        Assert.False(context.FileSystem.FileExists(FrameManifestPath));
        if (conflictText is not null)
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflictText);
        probe.ForbiddenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FrameSoulPath, ShiningAbodeState.StatePath, AfterlifeEntityProfileState.StatePath,
            AfterlifeSpiritualConflictState.DifficultySettingsPath, FrameRequestPath,
            ResourceMaterializationTestContext.DefinitionsPath, ResourceMaterializationTestContext.StatePath
        };
        probe.Start();
        var expected = await ValidateCompleteConflictFrameAsync(context);
        probe.Stop();
        // FileExists also invokes this hook after the null read; both attempts are
        // the unchanged production path, not an extra acquisition by the frame.
        Assert.Equal(conflictText is null ? 2 : 1,
            probe.Attempts(AfterlifeSpiritualConflictState.StatePath));
        if (expectedCode is null)
            Assert.Empty(expected);
        else
            Assert.Equal(expectedCode, Assert.Single(expected).Code);

        // Explicit capture is a complete operation even when production returns early.
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.Equal(conflictText is not null, frame.Candidate.Conflict.Exists);
        Assert.Equal(conflictText, frame.Candidate.Conflict.Text);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected),
            ConflictFrameIssueFingerprint.Create(context.Validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("{", false)]
    [InlineData("{\"turnNumber\":42,\"preGeneratedDices1d20\":[15,\"ignored\",5,null,12,8]}", true)]
    public async Task ConflictFrame_OfflineLiveDiceFallbackPreservesExistingShapeSemantics(
        string? requestText, bool hasDice)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        await context.DeleteAsync(FrameManifestPath);
        if (requestText is null)
            await context.DeleteAsync(FrameRequestPath);
        else
            await context.WriteExactJsonAsync(FrameRequestPath, requestText);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var dice = root["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!.DeepClone();
        dice["diceUsed"]![0]!["value"] = 14;
        // Offline active exchanges deliberately drop current-turn dice authority.
        // Recent proofs do consume the captured fallback: exercise that real route.
        root["activeConflict"] = null;
        root["recentConflicts"] = new JsonArray(new JsonObject
        {
            ["mode"] = "resolve", ["dangerMode"] = "hostile",
            ["conflictId"] = "offline_frame_resolution", ["realm"] = "Chaos Sea",
            ["sideModel"] = "direct_duel", ["resolutionState"] = "resolved",
            ["operationType"] = "pressure", ["playerOutcome"] = "won",
            ["resolvedAtTurn"] = 42, ["diceAudit"] = dice
        });
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.False(frame.HasValidatedSnapshot);
        Assert.NotNull(frame.Candidate.Profiles);
        Assert.NotNull(frame.TurnRequest);
        Assert.Equal(requestText is not null, frame.TurnRequest!.Exists);
        Assert.Equal(requestText, frame.TurnRequest.Text);
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)),
            ConflictFrameIssueFingerprint.Create(actual));
        Assert.Equal(hasDice, actual.Any(issue => issue.Code == "afterlife_conflict_dice_value_not_authorized"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_SignedAbsentOrEmptyDiceUseCapturedLiveFallback(bool emptyDice)
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks);
        await context.CaptureValidatedPendingSnapshotAsync(42, "Chaos Sea",
            preGeneratedDices1d20: emptyDice ? Array.Empty<int>() : null);
        // This changes only live fallback data; identity/turn and all signed bytes stay intact.
        var request = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameRequestPath));
        request["preGeneratedDices1d20"] = new JsonArray(15, "ignored", 5, null, 12, 8);
        await context.WriteExactJsonAsync(FrameRequestPath, request.ToJsonString());
        await PublishCompleteConflictFrameAsync(context);
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.Equal(42, frame.SnapshotTurnNumber);
        Assert.True(frame.SnapshotDice is null || frame.SnapshotDice.Length == 0);
        Assert.NotNull(frame.TurnRequest);
        var expected = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        AssertNoConflictFrameErrors(expected);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)),
            ConflictFrameIssueFingerprint.Create(expected));
        var capturedRequest = frame.TurnRequest;
        request["preGeneratedDices1d20"] = new JsonArray(14, 5, 12, 8);
        await context.WriteExactJsonAsync(FrameRequestPath, request.ToJsonString());
        probe.Start();
        var replay = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        probe.Stop();
        Assert.Empty(probe.Events);
        Assert.Same(capturedRequest, frame.TurnRequest);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(expected), ConflictFrameIssueFingerprint.Create(replay));
        var recaptured = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(recaptured.HasValidatedSnapshot);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(recaptured),
            issue => issue.Code == "afterlife_conflict_dice_value_not_authorized");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_SnapshotTamperingPreservesManifestAndFileAuthorityBoundaries(
        bool tamperDetachedAuthority)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await PublishCompleteConflictFrameAsync(context);
        var accepted = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(accepted.HasValidatedSnapshot);
        AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(accepted));
        if (tamperDetachedAuthority)
        {
            await context.WriteExactJsonAsync(PendingTurnSnapshotAuthority.AuthorityPath,
                "{ deliberately invalid detached authority");
        }
        else
        {
            var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameManifestPath));
            var path = manifest["files"]![FrameSoulPath]!.GetValue<string>();
            await context.WriteExactJsonAsync(path, """{"currentRealm":"Mortal World"}""");
        }

        // Manifest usability and each signed file's readability are separate existing
        // boundaries. Never re-sign the corruption or substitute current profile data
        // when an otherwise usable manifest's signed soul bytes fail their own hash.
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.Null(frame.Baseline.Soul);
        var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
        if (tamperDetachedAuthority)
        {
            Assert.False(frame.HasValidatedSnapshot);
            Assert.NotNull(frame.Candidate.Profiles);
        }
        else
        {
            Assert.True(frame.HasValidatedSnapshot);
            Assert.Null(frame.Candidate.Profiles);
            Assert.Equal(accepted.SnapshotTurnNumber, frame.SnapshotTurnNumber);
            Assert.Equal(accepted.SnapshotDice, frame.SnapshotDice);
            Assert.Contains(actual, issue =>
                issue.Severity == IssueSeverity.Error &&
                issue.Code == "afterlife_conflict_active_wrong_realm");
        }
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCompleteConflictFrameAsync(context)),
            ConflictFrameIssueFingerprint.Create(actual));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConflictFrame_ResourceSnapshotReadFailurePreservesNarrowCaughtContext(
        bool failDefinitions)
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks);
        await PublishCompleteConflictFrameAsync(context);
        var control = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(control.HasValidatedSnapshot);
        Assert.False(control.Baseline.ResourceAcquisitionFailed);
        Assert.NotNull(control.Baseline.ResourceDefinitions);
        Assert.NotNull(control.Baseline.ResourceState);
        AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(control));

        // Real existing authenticated lookup, before any fault or prevalidated scope.
        // The exact returned object is retained; never deserialize/construct a manifest.
        var validatedManifest = await ReadGenuinelyValidatedConflictFrameManifestAsync(context.Validator);
        var filesProperty = validatedManifest.GetType().GetProperty("Files");
        Assert.NotNull(filesProperty);
        var files = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
            filesProperty!.GetValue(validatedManifest));
        var definitionsSnapshotPath = files[ResourceMaterializationTestContext.DefinitionsPath];
        var stateSnapshotPath = files[ResourceMaterializationTestContext.StatePath];
        var faultPath = failDefinitions ? definitionsSnapshotPath : stateSnapshotPath;
        Assert.NotEqual(definitionsSnapshotPath, stateSnapshotPath);
        Assert.True(context.FileSystem.FileExists(faultPath));

        using (context.Validator.UsePrevalidatedPendingTurnSnapshotScope(validatedManifest))
        {
            // Only the selected physical signed resource path throws. Existing readers
            // still load detached authority and current request for every readable root.
            probe.ForbiddenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { faultPath };
            probe.Start();
            ValidationService.SpiritualConflictValidationFrame frame;
            try
            {
                frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
            }
            finally
            {
                probe.Stop();
            }

            Assert.True(frame.HasValidatedSnapshot);
            Assert.True(frame.Baseline.ResourceAcquisitionFailed);
            Assert.Null(frame.Baseline.ResourceDefinitions);
            Assert.Null(frame.Baseline.ResourceState);
            Assert.Equal(control.Candidate, frame.Candidate);
            Assert.Equal(control.Baseline.Conflict, frame.Baseline.Conflict);
            Assert.Equal(control.Baseline.Soul, frame.Baseline.Soul);
            Assert.Equal(control.Baseline.Shining, frame.Baseline.Shining);
            Assert.Equal(control.Baseline.Profiles, frame.Baseline.Profiles);
            Assert.Equal(control.SnapshotTurnNumber, frame.SnapshotTurnNumber);
            Assert.Equal(control.SnapshotDice, frame.SnapshotDice);
            Assert.Equal(control.DifficultySettings, frame.DifficultySettings);
            Assert.Equal(control.TurnRequest, frame.TurnRequest);
            Assert.Contains(probe.Events, entry => entry.Kind == "open" &&
                entry.Path == definitionsSnapshotPath);
            Assert.Equal(!failDefinitions, probe.Events.Any(entry =>
                entry.Kind == "open" && entry.Path == stateSnapshotPath));
            Assert.True(probe.Attempts(PendingTurnSnapshotAuthority.AuthorityPath) > 0);
            Assert.True(probe.Attempts(FrameRequestPath) > 0);

            probe.Start();
            var actual = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
            var replay = context.Validator.EvaluateSpiritualConflictValidationFrame(frame);
            probe.Stop();
            Assert.Empty(probe.Events);
            Assert.Contains(actual, issue => issue.Severity == IssueSeverity.Error &&
                issue.Code == "afterlife_conflict_resource_projection_missing");
            Assert.Equal(ConflictFrameIssueFingerprint.Create(actual),
                ConflictFrameIssueFingerprint.Create(replay));

            probe.Start();
            List<ValidationIssue> wrapped;
            try
            {
                wrapped = await ValidateCompleteConflictFrameAsync(context);
            }
            finally
            {
                probe.Stop();
            }
            Assert.Equal(ConflictFrameIssueFingerprint.Create(actual),
                ConflictFrameIssueFingerprint.Create(wrapped));
            Assert.Contains(probe.Events, entry => entry.Kind == "open" &&
                entry.Path == definitionsSnapshotPath);
            Assert.Equal(!failDefinitions, probe.Events.Any(entry =>
                entry.Kind == "open" && entry.Path == stateSnapshotPath));
            Assert.True(probe.Attempts(PendingTurnSnapshotAuthority.AuthorityPath) > 0);
            Assert.True(probe.Attempts(FrameRequestPath) > 0);
        }

        // Restore only the fault hook and dispose the existing scope. No disk bytes,
        // hashes, identity, authority, or rollback membership were changed for the fault.
        probe.ForbiddenPaths = null;
        var recovered = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(recovered.HasValidatedSnapshot);
        Assert.False(recovered.Baseline.ResourceAcquisitionFailed);
        Assert.Equal(control.Baseline, recovered.Baseline);
        AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(recovered));
    }

    private static async Task<object> ReadGenuinelyValidatedConflictFrameManifestAsync(
        ValidationService validator)
    {
        var overrideField = typeof(ValidationService).GetField(
            "_prevalidatedPendingTurnSnapshotOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(overrideField);
        Assert.Null(overrideField!.GetValue(validator));

        var lookupMethod = typeof(ValidationService).GetMethod(
            "LoadValidatedPendingTurnSnapshotLookupAsync",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(FileSystemManager.CanonicalWriteLease)], null);
        Assert.NotNull(lookupMethod);
        var pending = Assert.IsAssignableFrom<Task>(
            lookupMethod!.Invoke(validator, new object?[] { null }));
        await pending;
        var resultProperty = pending.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var lookup = resultProperty!.GetValue(pending);
        Assert.NotNull(lookup);
        var statusProperty = lookup!.GetType().GetProperty("Status");
        var manifestProperty = lookup.GetType().GetProperty("Manifest");
        Assert.NotNull(statusProperty);
        Assert.NotNull(manifestProperty);
        Assert.Equal("Usable", statusProperty!.GetValue(lookup)?.ToString());
        var manifest = manifestProperty!.GetValue(lookup);
        Assert.NotNull(manifest);
        return manifest!;
    }

    private static ValidationService.SpiritualConflictFileImage FrameImage(JsonObject root) =>
        new(true, root.ToJsonString());

    private static Task<List<ValidationIssue>> ValidateCompleteConflictFrameAsync(
        ResourceMaterializationTestContext context) =>
        context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));

    private static async Task PublishCompleteConflictFrameAsync(ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.Same(plan, await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
    }

    private sealed class ConflictFrameReadProbe
    {
        private readonly ConcurrentQueue<(string Kind, string Path)> _events = new();
        private bool _enabled;
        internal IReadOnlyList<(string Kind, string Path)> Events => _events.ToArray();
        internal HashSet<string>? ForbiddenPaths { get; set; }
        internal FileSystemManagerHooks Hooks => new()
        {
            BeforeCanonicalReadOpenAsync = path => Record("open", path),
            AfterCanonicalReadAttemptAsync = path => Record("attempt", path),
            BeforeRuntimeFileReadOpenAsync = path => Record("runtime", path),
            BeforeCanonicalExistenceFollowUpProbeAsync = path => Record("existence", path)
        };
        internal void Start() { _events.Clear(); _enabled = true; }
        internal void Stop() => _enabled = false;
        internal int Attempts(string path) => _events.Count(entry =>
            entry.Kind == "attempt" && string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
        private Task Record(string kind, string path)
        {
            if (!_enabled)
                return Task.CompletedTask;
            var normalized = path.Replace('\\', '/');
            _events.Enqueue((kind, normalized));
            if (ForbiddenPaths?.Contains(normalized) == true)
                throw new InvalidOperationException("Unexpected conflict-context read: " + normalized);
            return Task.CompletedTask;
        }
    }
}

internal static class ConflictFrameIssueFingerprint
{
    internal static string[] Create(IEnumerable<ValidationIssue> issues) =>
        issues.Select(issue =>
        {
            // This owning checker does not attach structured cross-subsystem repair context.
            // Do not silently omit it if that contract changes.
            Assert.Null(issue.FactionRepairClassification);
            Assert.Null(issue.MortalItemRepairContext);
            Assert.Null(issue.MortalLocationRepairContext);
            Assert.Null(issue.EffectRepairContext);
            Assert.Null(issue.WoundRepairContext);
            return JsonSerializer.Serialize(new
            {
                issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code,
                issue.Actor, issue.Section, issue.Expected, issue.Actual, issue.RepairHint,
                RepairTargetFiles = issue.RepairTargetFiles.ToArray()
            });
        }).ToArray(); // Preserve order, duplicates, nulls and all diagnostics, not just Error codes.
}

```

## Complete specialized reward/history partial

Target: `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.Wounds.cs`.
This file contains only the two repeated-evaluation scenarios, reusing the owning
suite's existing real helpers. No wound-source loader or admission rule is implied.

```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_RepeatedEvaluationDoesNotAccumulateNonzeroRewards(bool shining)
    {
        var realm = shining ? "Shining Abode" : "Chaos Sea";
        await WriteSoulStateWithInkFeathersAsync(shining ? 20 : 50, realm);
        if (shining)
            await WriteShiningStateWithLightSparksAsync(8);
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            realm,
            shining ? AfterlifeSpiritualConflictState.RewardCurrencyLightSparks
                : AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: shining ? 3 : 30), realm: realm);
        await WriteRewardTurnSnapshotAsync(
            preTurnSoulJson: BuildSoulStateJson(realm, inkFeathers: 20),
            preTurnShiningJson: shining ? BuildShiningStateJson(5) : null,
            preTurnConflictJson: BuildActiveConflictRootJson(realm: realm));

        var frame = await _validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        var root = JsonNode.Parse(frame.Candidate.Conflict.Text!)!.AsObject();
        Assert.Equal(shining ? 3 : 30,
            root["recentConflicts"]![0]![AfterlifeSpiritualConflictState.RewardAuditProperty]!["finalAmount"]!.GetValue<int>());
        var original = _validator.EvaluateSpiritualConflictValidationFrame(frame);
        Assert.DoesNotContain(original, issue =>
            issue.Code == "afterlife_conflict_reward_currency_delta_mismatch" ||
            issue.Code == "afterlife_conflict_reward_wrong_currency" ||
            issue.Code == "afterlife_conflict_reward_not_allowed" ||
            issue.Code == "afterlife_conflict_reward_missing_currency_baseline");
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCoreAsync()),
            ConflictFrameIssueFingerprint.Create(original));
        for (var run = 0; run < 3; run++)
            Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
                ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));

        var changedImages = shining
            ? frame.Candidate with
            {
                Shining = new ValidationService.SpiritualConflictFileImage(true, BuildShiningStateJson(7))
            }
            : frame.Candidate with
            {
                Soul = new ValidationService.SpiritualConflictFileImage(true, BuildSoulStateJson(realm, 49))
            };
        var changed = frame.WithCandidateImages(changedImages);
        Assert.Contains(_validator.EvaluateSpiritualConflictValidationFrame(changed),
            issue => issue.Code == "afterlife_conflict_reward_currency_delta_mismatch");
        Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
            ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_RepeatedEvaluationDoesNotConsumeHistoricalOccurrences(bool recentProof)
    {
        await WriteSoulStateWithLightIncarnateAsync();
        if (recentProof)
        {
            await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
            {
              "schemaVersion": 1,
              "activeConflict": null,
              "recentConflicts": [
                {
                  "dangerMode": "hostile",
                  "conflictId": "frame_historical_without_turn",
                  "resolutionState": "resolved",
                  "operationType": "guard",
                  "playerOutcome": "won",
                  "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
                }
              ]
            }
            """);
        }
        else
        {
            await WriteConflictStateWithRawExchangeAsync($$"""
            {
              "exchangeId": "frame_historical_exchange_without_turn",
              "operationType": "guard",
              "outcome": "success",
              "before": { "conflictPosition": "contested" },
              "after": { "conflictPosition": "player_advantaged" },
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
            """);
        }
        await WriteValidatedConflictSnapshotFromCurrentAsync(
            "Continue with the already accepted historical conflict payload.");

        var frame = await _validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        var original = _validator.EvaluateSpiritualConflictValidationFrame(frame);
        Assert.DoesNotContain(original,
            issue => issue.Code == "afterlife_conflict_light_incarnate_modifier_mismatch");
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCoreAsync()),
            ConflictFrameIssueFingerprint.Create(original));
        for (var run = 0; run < 3; run++)
            Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
                ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));

        // One accepted occurrence must not authorize a second identical current row.
        var candidateRoot = JsonNode.Parse(frame.Candidate.Conflict.Text!)!.AsObject();
        var rows = (recentProof ? candidateRoot["recentConflicts"]
            : candidateRoot["activeConflict"]!["exchangeLog"])!.AsArray();
        Assert.Single(rows);
        rows.Add(rows[0]!.DeepClone());
        var duplicate = frame.WithCandidateImages(frame.Candidate with
        {
            Conflict = new ValidationService.SpiritualConflictFileImage(true, candidateRoot.ToJsonString())
        });
        var duplicateIssues = _validator.EvaluateSpiritualConflictValidationFrame(duplicate);
        Assert.Contains(duplicateIssues, issue =>
            issue.Code == "afterlife_conflict_light_incarnate_modifier_mismatch" &&
            issue.FilePath.Contains(recentProof ? "recentConflicts[1]" : "exchangeLog[1]", StringComparison.Ordinal) &&
            issue.Actual?.Contains("auditTurn=missing", StringComparison.Ordinal) == true);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
            ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }
}

```
