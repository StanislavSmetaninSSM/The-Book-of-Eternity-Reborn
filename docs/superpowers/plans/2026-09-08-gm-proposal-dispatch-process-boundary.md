# GM proposal dispatch process-lane ownership

**Source issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T177-GM-PROCESS-OWNERSHIP.
**Method:** approved Fast-boundary maintenance, systematic debugging, literal mechanical move, TDD and independent review.
**Goal:** Keep real worker processes out of Fast without changing any dispatch behavior, assertion or timeout.
**Baseline:** e8b1c71e050233db181159d74c655027fcf6c435 in E:/Games/worktrees/boe-1536-wound-materialization.
**Companion:** adjacent 2026-09-08-gm-proposal-dispatch-process-boundary.patch; exact apply_patch move and all edits.

## Accepted checkpoint — 2026-09-09

Source01cae542 preserves the original whole moved body after removing only
the class Trait; old blob1ac899e331fd1fbfef75f6bc29bd8181c4230812, six Facts.
Ownership RED0/1 artifact20260908-231252-943-16020-d1e6cfaccbc74e729b470bc5f3a5f72e-focused
became GREEN1/1 at20260908-231620-175-3988-118aa11306ac45db86698eb267f22ec1-focused;
the same TestId is03011f09-1182-65b1-8913-e5db725ee840. Functional/category7/7
at20260908-231809-855-19604-87174d53e3d54be188bb15652d53f98a-focused took1:07.8194621.
All clean build/cleanup, no timeout/skips/duplicates. Parent read actual evidence
and the full independent SpecCompliant/QualityApproved0C/I/M review.

The first corrected Fast exposed B0's unrelated unregistered source-inventory
path (6015/6016, preserved); reviewed narrow correctionab2dd976 resolves it.
Final combined Fast7870/7870 in4:07.0563038/default5m and already-required
GM FullValidation1920/1920 in12:26.4604013/default15m both pass. Exact artifacts
are in 2026-09-08-spiritual-source-action-gm-sync.md. This accepts the child
ownership task only, not T177/C1/C2/D/E or full #1536. No runtime/deadline change.

## Diagnosis and bounded decision

The combined B0/B1/GM Fast run
20260908-230104-211-45628-6a89577d5423478d84a44292886d68cc-fast
completed only 1406 rows (1405 PASS / one FAIL) in1:58.4975388, not full Fast.
DispatchAsync_FakeAnalysisWorker_StoresFindingsProposalInInbox expected Completed,
but obtained WorkerTimedOut after about16s. Build0warnings/errors, no lane
timeout or duplicates, owned-tree cleanup complete. Preserve this failed run.

The exact unchanged test passes alone in3s test time, Focused1/1 in20.5709356:
20260908-230512-383-9500-46a7153db8da4085abc7d2f9e34ceb55-focused.
The source creates real canonical files, launches powershell.exe, and configures
TimeoutSeconds=10. GmWorkerBridgePool starts the timeout before workspace/launch.
This is evidence of load-sensitive process execution, not proof of an afterlife
regression or of a particular OS scheduling cause. The clear ownership defect
is independent of whether the isolated retry passes.

All six Facts share one real dispatch/filesystem workflow: four launch a worker,
two prove no matching worker or stale-generation prevents launch. Existing
GmWorkerBridgeLifecycleTests and GmWorkerValidationRepairDelegatorTests use the
same class-level ProcessIntegration ownership, including negative no-launch rows.
Move the whole six-Fact class consistently. Do not widen the ten-second worker
deadline, five-minute Fast lane, runner concurrency, production code, or tests.
GmWorkerBridgeTestFixtures already lives in TestSupport; no helper duplication
or Integration-to-Fast reference is needed.

## Exact files

- Move BookOfEternityClient.Tests/GmWorkerProposalOnlyDispatchTests.cs to
  BookOfEternityClient.IntegrationTests/GmWorkerProposalOnlyDispatchTests.cs.
  The sole content addition is class-level Category=ProcessIntegration.
  Original Git blob: 1ac899e331fd1fbfef75f6bc29bd8181c4230812.
- BookOfEternityClient.Tests/FastTestBoundaryTests.cs: one reviewed-heavy entry.
- BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs: one
  exact Process/E2E manifest entry.
- docs/testing.md and specs/1505-test-suite-performance/research.md: synchronize
  the executable heavy inventory from67 to68 and explain this ownership.

## Task 1: literal move with unchanged behavior

- [x] Record this task in active1536 plan/tasks before source edits. No new branch.
- [x] Apply only the companion's first Fast ownership-manifest hunk.
- [x] Run PS7 scripts/test-csharp.ps1 -Lane Focused -Filter
  "FullyQualifiedName~ReviewedHeavySources_ExistOnlyUnderIntegrationTests".
  Expected semantic RED0/1 after clean compile: the actual file is still in Fast.
- [x] Apply the remaining companion, including apply_patch Move to. Verify old
  path absent/new path unique; after removing only the inserted class Trait line,
  the entire moved source must equal the baseline blob (LF-normalized).
  Six Fact signatures and every assertion/helper/script/timeout remain exact.
- [x] Run the same Fast ownership selection GREEN1/1; unchanged exact test ID.
- [x] Run PS7 scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration
  -Filter "FullyQualifiedName~GmWorkerProposalOnlyDispatchTests|FullyQualifiedName~ProcessAndE2ETestSources_MatchReviewedManifest".
  Expected7/7: unchanged6 functional Facts plus existing category-manifest guard.
  Compare all six FQNs with the initial Fast TRX. Assembly changes can change IDs;
  no removed/replaced method, skip, assertion or test-budget change is allowed.
- [x] Inspect actual diff/summary/log/TRX, exact literal postimage and inventory68.
  Request independent review. Preserve initial Fast failure/isolated retry/RED.
- [x] Parent runs one corrected combined B0/B1/GM+ownership Fast after named
  controls/review; this is required verification of the move, not a blind retry.
  The prior afterlife-sensitive FullValidation remains required afterwards.
  Do not run full ProcessIntegration, duplicate owner selections or PreMerge.
- [x] Record evidence and accept this child only; full T177/C1/C2/D/E/#1536 remain open.

## Scope and GM synchronization rationale

Test placement and test-development documentation only. Mortal and afterlife game
capabilities, validators, state, commands, prompts, examples, manifests and daemon
contracts do not change, so no GM-facing edit or separate FullValidation is needed
for this move. The already-pending GM9 FullValidation is not waived.
Parent owns source/C# sequencing. Metadata-only C2 planning may continue; no source,
index or HEAD mutation during tests. No remote actions or child/session cleanup.
