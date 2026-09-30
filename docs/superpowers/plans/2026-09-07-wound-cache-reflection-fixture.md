# Wound Cache Reflection Fixture Repair Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Repair the verified #1536 T177 test-harness arity mismatch without weakening registry authority or assertions.

**Architecture:** Keep strict reflection matching. The two ordinary wound-stage test wrappers explicitly pass the two null optional production authority defaults; the empty-item registration wrapper supplies the existing typed empty inputs and projection-root maps. No production method or generic reflection fallback changes.

**Tech Stack:** C#/.NET 8, xUnit, PowerShell 7 bounded runner.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T177 in `specs/1536-complete-wound-materialization/tasks.md`.
- Stay in existing `1536-complete-wound-materialization` branch/worktree. No migration, new branch, remote action, unrelated `.serena/` edit or unresolved legacy/art-schema decision.
- Change only the two ordinary-stage reflection wrappers and the source-confirmed empty-item registration wrapper in `BookOfEternityClient.Tests/AcceptedMechanicsPlanCacheTests.Wounds.cs`. Keep every test and assertion.
- Explicit null treatment-continuation and reservation arguments match the production ordinary-stage contract. Do not forge non-null authority, bypass the reservation gate, change registry methods or make Invoke automatically fill missing arguments.
- Existing tests remain real behavioral oracles for invalidation, cache rechecks and foreign/omitted stage rejection. A reflection exception is a fixture failure, not successful coverage of those assertions.
- One C#/source owner, five-minute Focused/Fast bounds. Finish each run before source edits; no test skip, weak assertion, timeout increase or duplicate Fast.
- This test-only correction exposes no GM/gameplay/afterlife contract. No prompt/docs/example/manifest/source-guard update or conditional FullValidation is needed. T177/full #1536 remain open.

## Source diagnosis and boundaries

The catalog Fast artifact
`20260907-091154-571-41752-6830455b3aaa49f3a0a354d43b337d9b-fast`
executed6450 rows and reports three failures in AcceptedMechanicsPlanCacheTests:
CommonPlannerExceptionClearsEveryAuthority, FinalizationRechecksCurrentEffectCache,
and CommonValidationRejectsOmittedCurrentWoundBundle. All stop in
AcceptedTurnStateHarness.Invoke with Sequence contains no matching element.

Parent inspected the exact source. Both registry methods
GetOrBuildWoundEffectValidated (AcceptedTurnAuthorityRegistry.cs:2933) and
GetOrBuildWoundFinal (:3028) now take four parameters: the original pair and
optional treatmentContinuationAuthority/reservationAuthority, both defaultnull.
Harness wrappers at AcceptedMechanicsPlanCacheTests.Wounds.cs:2326/2334 still
send only two; Invoke at2395 matches exact name and parameter count. Reflection
does not apply C# optional-argument expansion at that selector.

WoundStageReservationAgrees (:4837) explicitly accepts both null when the prepared
ordinary stage has no treatment continuation, and requires genuine reference-equal
private authority otherwise. No relaxation is needed. The wrapper-only fix restores
the existing ordinary test route. All involved registry/harness source files are
unchanged across catalog BASE564de32a..690288f4, so this is not a catalog mutation.

Seven existing AcceptedTurnState_ facts cover this seam: preparer exception,
finalizer exception, effect-planner exception, common-planner exception, current
effect-cache finalization recheck, foreign bundle and omitted bundle rejection.
Expected initial cohort is seven executed, six reflection failures and one passing
preparer control. Inspect actual evidence instead of relying on this prediction.
The two finalization/foreign-bundle tests will also exercise the final wrapper once
the effect wrapper can be reached.

Parent checked a potential second cause: the simple common-cache fixture has
session_a/request_a/snapshot_a while WoundInput has session_wound_cache /
request_wound_cache / snapshot_wound_cache; it intentionally seeds a separate
cache entry for invalidation tests. Do not rewrite those inputs to bypass the
newer same-binding missing-wound-bundle gate. The exact bound foreign/omitted
bundle tests already use CreateWoundCommonAcceptedInput and must stay unchanged.

### Task 1: Restore exact ordinary registry calls in the existing test harness

- [X] **Step 1: Reproduce the complete existing affected cohort.**

No test/source edit yet:
```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~AcceptedMechanicsPlanCacheTests.AcceptedTurnState_"
```

Read actual summary/TRX/error messages. Expected seven executed, one controlPASS,
six reflection failures. Record this honestly as a fixture RED, not evidence that
invalidation/replay assertions were reached. If a different failure appears,
report its actual source before extending the planned edit.

- [X] **Step 2: Replace only the two wrappers with explicit optional defaults.**

```csharp
        // Reflection needs explicit defaults; ordinary stages carry neither authority.
        internal WoundEffectBatchPlanningResult GetOrBuildWoundEffectValidated(
            WoundPreparedAcceptedTurnPlan prepared,
            EffectAcceptedTurnInput input) =>
            Invoke<WoundEffectBatchPlanningResult>(
                "GetOrBuildWoundEffectValidated",
                prepared,
                input,
                null,
                null);

        internal WoundAcceptedTurnPlanningResult GetOrBuildWoundFinal(
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchPlanningResult effect) =>
            Invoke<WoundAcceptedTurnPlanningResult>(
                "GetOrBuildWoundFinal",
                prepared,
                effect,
                null,
                null);
```

Do not change Invoke, its exception unwrapping, test bodies/assertions, production
signatures, helper identities or current authority validators.

- [X] **Step 2B: Repair the newly exposed empty-item registration wrapper.**

Parent confirmed the actual intermediate run:
`20260907-092934-738-26368-10959816cf3c45ed8d51708b4c57e32b-focused`
has seven executed, five PASS and two fixture failures, both at
RegisterEmptyMortalItems. Its six arguments target the current ten-parameter
RegisterMortalItemsValidated (registry3988), which also requires routes, transfers,
current and backup projection roots. These are empty real typed fixtures, not
treatment authority. Existing SeedAcceptedTurnAuthority already supplies them.
Cache.Register (MortalItemAcceptedEffectSourceAuthority.cs:1394-1422) independently
clones both roots, so a single local empty-root map may safely supply both inputs.

This is an approved extension of the same T177 fixture repair, not another task,
and retains original BASE9f3e1455. The first runtime commit is4170e01d; its two
wrapper changes remain. Restore the exact planned reflection/defaults comment
above GetOrBuildWoundEffectValidated (it was omitted in that first commit).
Replace only RegisterEmptyMortalItems with:
```csharp
        internal void RegisterEmptyMortalItems(
            string sessionId,
            string snapshotToken)
        {
            var emptyProjectionRoots =
                MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.ToDictionary(
                    static path => path,
                    static _ => (JsonNode?)null,
                    StringComparer.Ordinal);
            Invoke<object?>(
                "RegisterMortalItemsValidated",
                sessionId,
                snapshotToken,
                "sha256:wound-cache-test-mortal-items",
                Array.Empty<MortalItemAcceptedTurnAuthority.NewCandidate>(),
                Array.Empty<MortalItemAcceptedTurnAuthority.StableCandidate>(),
                Array.Empty<string>(),
                new Dictionary<string, MortalItemRouteAuthority>(StringComparer.Ordinal),
                Array.Empty<MortalItemAcceptedTransfer>(),
                emptyProjectionRoots,
                emptyProjectionRoots);
        }
```

Do not rerun the already observed RED just to repeat it. Continue Step3 cohort
and owner, then the still-not-run single Fast. Every assertion and production
gate remains unchanged. Report the first run as six reflection fixture failures,
the intermediate run as five passing behaviors/two newly exposed fixture errors,
and final actual results; neither early run was a full behavioral GREEN.

- [X] **Step 3: Confirm affected behaviors and the owner class.**

Run Step 1's exact command: expected seven/seven PASS. Then:
```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~AcceptedMechanicsPlanCacheTests"
```

Expected owner GREEN, actual count to record. If another genuine boundary fails,
do not alter unrelated assertions or production; give parent the exact artifact,
message and minimal diagnosis before deviating.

- [X] **Step 4: One bounded Fast, self-review and exact-file commit.**

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
git diff --check
```

Audit actual summaries, every completed TRX, errors, build warning/error counts,
timeout/cleanup and duplicate identities. Fast discovery is the number of log
lines matching `^\s{4}BookOfEternityClient\.Tests\.`, NOT summary.Tests.Total
and NOT descriptor count. Explicitly use the actual discovery log count:
```powershell
@(Select-String -LiteralPath "<actual Fast artifact>/dotnet-test.log" -Pattern '^\s{4}BookOfEternityClient\.Tests\.').Count
```
Compare that count against the completed TRX Total, accounting for theories whose
rows expand only at runtime. A raw count difference is not an uncompleted count
or an exact missing identity set. No additional Fast, FullValidation
or PreMerge. If Fast uncovers another failure, record it without waiver or
unrelated repair. Commit only the named test file.

- [X] **Step 5: Independent review and parent acceptance.**

Parent inspects exact recorded task BASE..candidate, full changed fixture,
actual artifacts and fresh Spec Compliance / Quality review before checking
this bounded task. Keep full T177 open: one fixture correction is not overall
feature verification, integration completion or merge authority.

## Parent self-review

- Root cause and four-argument production signatures were directly inspected.
  Null/null is the existing ordinary default, not a success flag or new bypass.
- Exactly three wrappers change; strict Invoke stays useful for detecting future
  interface drift. Current tests/assertions, including exception unwrapping and
  rejection codes, remain the oracles.
- The actual test class/fully qualified method prefix were checked. The first
  run observes the broader seven-fact fixture failure before the fix; owner
  verification then exercises other shared harness consumers.
- Prior Fast is failure-discovery evidence, not behavioral RED for assertions
  that never ran. Only post-fix GREEN proves those behaviors are reached.

## Parent acceptance — 2026-09-07

Accepted exact range `9f3e14552a80d1151498124eb44495b7053c2009` through
`7460800d83991f856162acac1b42a72d8918859b` (runtime commits `4170e01d` and
`7460800d`). Fresh independent review reports Spec Compliant / Quality Approved,
zero Critical, Important or Minor findings. Parent inspected the full changed
fixture, unchanged production signatures and null/null reservation gate, independent
projection-map cloning, and every actual summary/TRX/error log below. Reviewer
outside-diff caveats are resolved by those direct source and artifact checks.

- Fixture RED: `20260907-092810-929-49188-b737157716bc42d3bc56de5aebbea1e2-focused`,
  seven executed, one PASS / six reflection-arity FAIL, 1:08.933.
- Intermediate: `20260907-092934-738-26368-10959816cf3c45ed8d51708b4c57e32b-focused`,
  seven executed, five PASS / two empty-item reflection-arity FAIL, 31.857 seconds.
- Affected behavior GREEN:
  `20260907-093622-003-49124-8d2b6e3743534a0b90e03f3896c4370f-focused`, 7/7, 1:09.406.
- Owner GREEN: `20260907-093735-357-25384-4884cd9705ca42869b779d1512b919c9-focused`,
  208/208, 36.882 seconds.
- One Fast: `20260907-093816-867-32324-46e815b089614fbe80b9fb7d063d4cc0-fast`,
  7,580/7,580 PASS across 26 TRXs, 3:13.691 within the unchanged five-minute limit.
  All runs have zero build warnings/errors, no timeout or duplicate executions,
  and successful cleanup. Fast has no skipped or not-executed rows.

Fast discovery lists 7,526 entries; seven non-enumerated theories expand into 61
runtime cases (+54). Parent reconciled every method's discovery/result counts:
all other methods match. This is complete execution, not a negative/missing count.
The exact audit and review are retained under git metadata `sdd/` as
`wound-cache-reflection-fixture-task-1-parent-artifact-audit.md` and
`wound-cache-reflection-fixture-task-1-review.md`.

No gameplay or GM-authored contract changed, so Mortal/afterlife prompts, examples,
manifests and source guards need no update for this test-only repair. No duplicate
Fast, FullValidation or PreMerge was run. Full T177, T070 and #1536 remain open;
the overall feature count remains 77/177 top-level tasks.
