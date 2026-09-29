# Implementation Plan: Test Suite Performance and Verification Lanes

**Branch**: `work/1505-test-suite-performance` | **Date**: 2026-07-31 | **Spec**: [spec.md](spec.md)

**Input**: Approved feature specification from `specs/1505-test-suite-performance/spec.md`.

## Current implementation: category selection (approved 2026-09-30)

Source: #1505 / #1536; [spec.md](spec.md), CATEGORY-SELECTION-DECISION rev1.
Status: T066–T071 verified, independent Astra XHigh corrections accepted
2026-09-30. Report to owner and stop for inspection; gameplay remains paused.
Publication amendment, 2026-09-30: the owner subsequently requested a GitHub
backup of all accumulated work, including wound materialization. T072 now permits
an inventory-reviewed checkpoint commit and non-force push of the existing
feature branch/history. The earlier no-stage/commit/push restriction below is
superseded for that preservation step. No merge, issue closure, gameplay work or
claim of full #1536 acceptance is authorized. Existing test evidence is preserved;
unchanged tests are not rerun merely to upload the same source tree.

Publication receipt, 2026-09-30: T072 completed. The
[snapshot e181539a](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/e181539a4330a20781fd71e2c0432b826e396c56)
preserves all 586 intended changed/new files together with 527 preceding local
commits on [the feature branch](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/tree/1536-complete-wound-materialization).
Independent Astra XHigh publication review accepted the inventory, content
matching and status wording. The non-force push succeeded after the owner
approved GitHub CLI's missing workflow permission; `git ls-remote` confirmed the
exact snapshot SHA. Eight local Serena files and ignored generated/runtime
artifacts remain local. Non-patch whitespace checks passed; 87 whitespace
findings in eight historical patch artifacts were preserved unchanged. This
verifies preservation only; no gameplay acceptance or broad test rerun occurred.

Execution: parent owns implementation using Superpowers executing-plans and TDD;
bounded catalog work can be delegated to Sol High; one independent Astra XHigh
reviews the completed coherent block. Publication follows the amendment above;
merge and gameplay remain paused.
Use the existing worktree. The sections after this current plan are historical.

### Design and files

- `tests/categories.json`: versioned, data-only category catalog. Each entry has
  `id`, `responsibility`, `excludes`, `changeHints`, conditional `related` links,
  `requirements`, `timeoutMinutes`, nullable measured `expectedSeconds`, and
  `selectors`. A selector names project `unit`, `integration`, `frontend` or
  `powershell` and `tests`: exact FQN methods, delimited class-member wildcards,
  or exact repository-relative test files. Frontend selectors declare `vitest`
  or `node` execution. No selector accepts an entire C# namespace or project.
  Category names are catalog data, never a runner enum. Large partial classes
  receive explicit method-family or method inventories, not one giant category.
- `scripts/testing/TestCategoryCatalog.psm1`: pure catalog validation, lookup,
  matching and union functions. `Read-TestCategoryCatalog(path)` validates
  structure, names, links and bounded budgets; `Resolve-TestCategorySelection`
  consumes catalog, requested IDs and discovered inventory, rejects empty or
  unmatched selectors, and returns exact selected methods/files with category
  provenance. `Test-TestCategoryInventory` checks all discovered tests have an
  owner without running them. Multiple membership is legal; execution is unique
  by project+method/file, preserving parameterized cases inside that method.
- `scripts/test-csharp.ps1`: retain the normal entrypoint and existing owned
  process/Windows Job Object/runtime cleanup/TRX mechanisms. Replace fixed lane
  selection with `-Category <ids>`, `-ListCategories`, `-ValidateCatalog`,
  `-SelectionFile <path>`, `-PlanOnly`, `-NoBuild`, `-TimeoutMinutes` and bounded
  `-Parallelism`. No-argument calls display usage; `-Lane` fails with migration
  help before workloads. Retain isolated process self-tests; remove obsolete
  aggregate-lane scheduling cases and guards rather than maintaining a second
  runnable all-suite implementation. No fallback to unfiltered test execution.
- `scripts/testing/TestCategoryExecution.ps1`: selected-project preparation,
  discovery and category descriptors use the runner's owned process functions.
  C# discovery uses `dotnet test --list-tests` only; execution uses unions of
  exact fully qualified method filters in bounded batches. Ordinary commands
  inspect only projects used by selected categories. `-ValidateCatalog` may
  discover all test projects and enumerate frontend files, executing no tests.
  Category metadata carries frontend-build/exclusive resource requirements;
  overlap deduplication merges requirements conservatively. Limit external
  processes to four; resource-heavy/exclusive work is serial. Parallelism must
  not silently multiply internal xUnit hosts beyond existing safe limits.
- Frontend: build/typecheck is separate from running tests. Selected Vitest
  files run with machine-readable results and a zero/skipped/failure check;
  selected standalone TypeScript assertion scripts compile to unique result
  directories and run via Node individually. Inventory covers all 36 existing
  test files, including files omitted from the old npm test list. Distinguish
  compile-time contracts explicitly if an executable assertion is absent.
  No `npm run verify`/bare Vitest fallback; frontend dependencies/builds are
  prepared only for selected consumers.
- `scripts/tests/test-category-catalog.tests.ps1`: focused executable assertions
  for metadata, matching, deduplication, partial classes and malformed selection.
  Existing `FastTestBoundaryTests` and `IntegrationTestBoundaryTests*` replace
  only obsolete lane policy tests; preserve process ownership, cleanup, TRX
  completeness and genuine test/source isolation safeguards.
- `tests/selection.json`: explicit reviewed CI selection, category IDs with
  reasons and affected contracts. CI consumes this record, never guesses all
  categories from a missing/invalid selection. The agent updates the selection
  for its actual change; review checks its scope against the PR. Execution
  artifacts record the current revision and working-tree content fingerprint,
  so earlier evidence is not presented as fresh verification.
- `.github/workflows/dotnet-ci.yml`, `.github/pull_request_template.md`,
  `.github/ISSUE_TRACKING.md`, frontend `package.json`, `AGENTS.md`,
  `docs/testing.md`, `docs/development-workflow.md`: migrate active instructions
  and CI to explicit categories; remove broad aggregate execution. Catalog is
  the canonical category description; docs explain list/plan/run/validate/add/
  split workflows and provide a copyable category template. Historical timing
  evidence stays labelled historical, never an active verification command.

### Budgets and isolation

Initial default category budget: 10 minutes including its execution; a group
expected to exceed five minutes is flagged for domain subdivision or measured
justification. Small infrastructure/frontend categories use smaller budgets.
Command preparation/discovery/cleanup is included in an explicit overall budget
(default selected budgets plus five minutes preparation, capped at 30 minutes).
No automatic larger retry; an unfinished selection fails and retains evidence.
These protective values can evolve with documented evidence; no fixed category
name or size ceiling is invented. Newly classified category duration is unknown
until measured; discovery counts never establish runtime performance.

Inventory the existing prepared-tree, spiritual frame/staged, Mortal recovery,
Explorer/browser and lifecycle caches. Record each mutable/runtime object owner,
copy boundary and cold-path guarantee in `docs/testing.md`. Keep measured useful
immutable templates only with evidence of independent materialization. Remove
unsafe sharing or simplify unjustified caches; add isolation regression tests
only where an actual risk/change needs proof. No broad gameplay suite is needed
to demonstrate category routing or inventory coverage.

### Sequence and verification

1. T066: failing pure catalog selection tests, implementation, focused green;
   inventory and domain mapping with explicit handling of oversized partials.
2. T067: replace runner selection with category descriptors; test missing/unknown
   categories, no-argument help and retired lane refusal before workload start,
   multi-category deduplication, dynamic theory completeness, cross-project
   selection, partial failure and cleanup. Preserve owned-process tests.
3. T068: integrate exact frontend files and CI selection; prove one selected
   frontend category excludes unrelated test files and build is not test-all.
4. T069: audit fixtures and reconcile all active workflow/docs/spec references.
5. T070: discovery-only full inventory validation and real selected category
   controls for catalog/runner/process/reporting plus the frontend adapter.
   Build affected C# projects with XML documentation enabled. Do not run every
   category for migration acceptance. Record exact selection, elapsed times,
   missing/skipped/duplicate results, cleanup and current revision.
6. T071: independent Astra XHigh review, repair validated findings and verify
   only affected categories; reconcile task evidence and report, then stop.

### Review focus and consistency

Tests must cover (a) an unmapped new method in a split partial class, (b) one
selection spanning both C# projects, (c) shared category membership with a
stricter runtime requirement, (d) empty/typo selection causing no workload,
(e) dynamic theory rows or missing results falsely appearing complete. Frontend
JSON/report errors and pure assertion-script failures must also remain failures.

Mapping: CAT-001/002/005 → T066; CAT-003/004/006/007/010 → T067;
CAT-004/005/006/009 → T068; CAT-008/009 → T069; acceptance1–6 → T070;
CAT-003/010 and acceptance7 → T071. No production/GM change is planned.
Spec Kit consistency check before implementation: requirements have tasks;
the approved no-full-run rule overrides historical lane requirements; all
tasks stay within #1505/#1536, keep test-first verification and independent
review, and preserve canonical-state contracts. No uncovered requirement or
unresolved product decision identified. Concrete catalog names remain executor
decisions, as required by the spec.

### Execution checkpoint

2026-09-30: exact rev1 approved. Planning and source inventory only; no new test
execution yet. Existing stopped cutover control remains cancelled, not passed.

### Fixture audit — T069, 2026-09-30

Read-only audit; no fixture or production behavior changed. The parent inspected
the copy boundary and representative key/authority implementations after the
Sol High audit. Retain the existing templates below: no shared mutable owner was
found, and deleting the templates would restore repeated preparation. This is
not a claim that every cache has a fresh isolated benchmark.

| Existing fixture | Copy/authority boundary and decision |
|---|---|
| `PreparedFixtureTree` | Private bytes are written to a caller-owned root; arrays/owners are not returned. Retain as the common copy primitive. |
| `AfterlifeResourceCutoverTests.ConflictFrameCache` | Only fixed seed/dice/balance/signing inputs are cacheable; custom callbacks/hooks bypass it. Copies establish fresh authority. Retain. |
| `AfterlifeResourceCutoverTests.SpiritualGameEngineFixtureCache` | Template capture rejects signed requests, snapshots, decision/checkpoint/repair artifacts. Copies create new contexts and callbacks; mutation/deletion/source-disposal isolation tests already exist. Retain. |
| `AfterlifeResourceCutoverTests.SpiritualStagedPreparedFixture` | Copies roots, checkpoint arrays and reparsed accepted-A JSON into a fresh context/generation. Retain the existing isolation guard. |
| `MortalWoundRecoveryTests.FixtureCache` | Keys include all setup-shaping values and the evaluation clock; IntentTypes is expected-result metadata, copied on return. Fresh filesystem/lease/binding/correlation for each signed-byte copy. Retain. |
| `ExplorerWebCommandSeedTemplateFixture` | GUID case roots, keyed lazy source profiles and prepared-source hash checks. Manual profile keys must change with source content. Retain current distinct profiles. |
| `BrowserCommandPresentationAuditFixture` / `PreparedCommandDisplaySaveFixture` | Copies saves into per-case roots with fresh filesystem/state managers. Browser fixture checks source hashes; command-display template has no such assertion but exposes no mutating path in current consumers. Retain; add a guard if that boundary changes. |

Historical measured evidence is retained in the #1536 plan: the Mortal source
factory took 27.85 seconds once, versus 1.35–1.60 seconds per fresh signing/export
phase (copy/cleanup excluded). Existing three-root isolation and source-disposal
controls passed. Spiritual cold continuation creates a fresh engine on the same
physical root after durable pending; call this fresh-engine recovery, not a
separate-process restart. No new cache is introduced or made mandatory.

### Migration verification — T070 completed

- RED/GREEN: catalog initially absent; no-argument public entry incorrectly
  treated null as a selected item; malformed TRX counters could overstate rows;
  hidden frontend aggregate imports escaped selection. Each defect had a failing
  focused check before correction. Current pure scripts have 9 catalog, 7 entry
  and 6 report checks.
- Infrastructure `928976f5`: 94/94, three descriptors complete, 1:52.799 wall,
  93.04 seconds category execution, both cleanups successful, no duplicates.
  C# builds with XML documentation: unit 0 warnings/errors (7.36s), integration
  0 warnings/errors (16.79s). Earlier failed/partial runs remain failures.
- Discovery-only `0577e2bc`: 90 categories, 10,180 method/file identities,
  zero unmapped/stale selectors, 21.262 seconds, zero executed tests.
- Frontend standalone `09c29cbf`: shell-input 17/17, two descriptors complete,
  18.012 seconds wall, no duplicates, clean cleanup. Dormant connection-banner
  test now accepts the actual template CSS-class syntax while preserving order;
  standalone UI tests resolve source paths independently of caller directory.
- Final core selection `c9332aeb`: `-SelectionFile tests/selection.json` (four
  categories at that revision), 165/165 result entries including two explicit
  compile-contract files; all 8 descriptors complete, 2:29.411 wall, no skipped
  or duplicate results, both cleanups successful. XML builds have zero warnings
  and errors; phase timing is retained. Category execution: infrastructure
  95.89s, command-results 6.12s, shell-input 12.68s, shell-types 4.77s. Measured
  times are catalog metadata; remaining categories honestly remain unmeasured.
- Frontend workflow consumers: old aggregate-policy assertions were replaced
  with selected-entrypoint and CI contracts. RED 36/38, then GREEN `ef34d32b`
  38/38, 22.618s wall, complete cleanup and zero XML build warnings/errors.
  This new category is now included in the current selection record.
- Actual retired `npm run verify` exits 2 with migration help before tests.
  All three CI PowerShell blocks parse; the environment-requirements block
  resolves the current selection, and warning-budget checks pass 0/158 for
  both selected builds. GitHub Actions itself was not run.
- Independent Astra XHigh review found no further executable runner/adapter
  defect; it confirmed the complete core result and preserved useful guards.
  Two P2 findings require corrected semantic catalog ownership/conditional
  links and migration of active constitution/contributor instructions. The
  latter is synchronized through Spec Kit constitution 2.0.0, templates,
  workflow docs and active quickstarts; historical commands remain explicitly
  superseded. Final correction review and inventory evidence follow below.

### Final acceptance checkpoint — T071, 2026-09-30

- Semantic catalog fixes: actual effect/resource normalizer classes have their
  own category; soul/afterlife cases no longer belong to Mortal. Generic
  command-registry guards are separated from realm-specific methods. Conditional
  links explain shared normalization, command descriptors and afterlife example
  consumers without an automatic graph-wide run.
- Restored category-name assertions for the browser checklist: RED `05e10f0d`
  37/38, then GREEN `b4344289` 38/38 in 17.586s, complete selection, no duplicates
  and clean owned/runtime cleanup. The prior full selected core control remains
  valid: no core runner/adapter behavior changed after `c9332aeb`.
- Final `-ValidateCatalog` artifact `c2fbbf69`: 91 categories, 10,180 unique
  method/file identities, empty Unmapped/StaleSelectors, zero executed tests,
  47.321s including fresh XML builds. Integration build 17.98s and unit 2.37s,
  both zero warnings/errors. Scoped `git diff --check` passed.
- Independent reviewer `/root/category_migration_review`, explicitly selected
  `gpt-6-astra` / `xhigh`, inspected original requirements, exact migration diff,
  code/consumer fixes and actual artifacts. Final recheck accepted both P2
  corrections and found no remaining confirmed blocker; selected verification
  is sufficient. Process assessment is in `docs/development-workflow.md`.
- No gameplay, state or GM-authored contract changed. Mortal/afterlife prompts,
  game examples, manifests and contract matrices need no update for this test
  infrastructure block. No full suite or serial-all-category run was performed;
  most category runtimes are still unknown. CI configuration and local command
  blocks were checked, but GitHub Actions was not executed.
- No stage, commit, push, merge or issue closure. Migration is complete; stop
  for owner inspection before resuming #1536 gameplay implementation.

## Historical lane implementation (superseded)

## Summary

Keep the public production validator behaviorally unchanged while adding an
internal, non-empty flags-based selection of the existing 26 ordered validation
phases. Prove equivalence and fail-closed selection behavior first, then migrate
295 broad guardian-suite calls to reviewed test-side profiles. Physically split
fast and integration sources around a dependency-free TestSupport library, add
enforceable slow-test traits and project/source guards, retain an isolated
prepared Guardian fixture snapshot, and use a project-routed,
discovery-balanced PowerShell runner with one deadline and retained
JSON/TRX/log evidence.
Keep the complete GameEngine turn-lifecycle class in an explicit
LifecycleIntegration lane and retain only ten reviewed lifecycle sentinels in
routine PreMerge. The Phase 45 capacity amendment tracked by #1502 likewise
keeps all 358 afterlife spiritual-conflict cases in RegressionIntegration and
admits only an exact ten-method sentinel manifest to routine PreMerge.
The #1526 suite-growth correction keeps the same plan and caps, retains measured
PreMerge costs for underestimated integration classes, and removes repeated
save loading from the browser-presentation audit through prepared templates
with isolated per-row clones.
The #1547 measured deadline correction preserves that complete schedule and
raises only the globally bounded PreMerge deadline from 20 to 30 minutes after
the green core plus isolated process/E2E lower bound stopped fitting the former
cap.
The #1551 Fast-boundary repair keeps the five-minute Fast contract and existing
runner ceilings unchanged. It moves measured canonical I/O, restart,
contention, process, host, and end-to-end workflows into the Integration
project with exact category manifests, while extracting deterministic QTE
grading/input tests from their file-backed fixture so they remain in Fast. Its
final-review remediation applies the same semantic split to Daren and protects
both splits with executable Roslyn method/row inventories plus syntax-aware
class-category ownership.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7/Windows PowerShell-compatible lane script.

**Primary Dependencies**: Existing `ValidationService`, `FileSystemManager`, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, `dotnet test`.

**Storage**: Existing file-backed JSON fixtures plus ignored
`TestResults/test-lanes/` logs, summaries, and TRX output.

**Testing**: xUnit focused filters, source/project guards, bounded benchmark
runs, two final Fast controls, the diagnostic Integration categories changed by
the source moves, one conditional DeepValidation control for the historical
category-boundary change, one conditional LifecycleIntegration control, and
one final bounded PreMerge control only when merge is explicitly requested.

**Target Platform**: Local Windows development machine; implementation remains portable .NET code.

**Project Type**: Local console/browser game-client repository with one runtime
project, a non-test TestSupport library, and physically separate fast and
integration test projects.

**Performance Goals**: At least 5x on the fixed two-test guardian benchmark;
Fast at most 5 minutes; Focused defaults to 5 minutes and permits an explicitly
justified bounded override up to 15 minutes for a measured coherent selection;
LifecycleIntegration defaults to 10 minutes and permits the same explicitly
justified 30-minute ceiling when the complete measured class has grown;
DeepValidation at most 15 minutes; PreMerge at most 30 minutes.

**Constraints**: Public validation still runs all 26 phases in canonical order; no gameplay, state schema, issue-code, prompt, documentation-example, console, browser, or frontend behavior changes; no unbounded full-suite run.

**Scale/Scope**: The original baseline contained 6,560 discovered cases, 965
broad validation calls, 460 guardian cases, and 295 broad guardian calls across
eight partial source files. The #1551 amendment starts from 7,797 Fast cases in
29 descriptors and four measured mixed-descriptor walls of approximately
3:30, 3:55, 1:05, and 4:00 without build time.

**Source Issue(s)**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

**Contract Scope**: Internal validation orchestration and test infrastructure only.

**Verification Commands**:

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.sln --no-restore --verbosity minimal
dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal
dotnet build BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj --no-restore --verbosity minimal
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane RegressionIntegration
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
.\scripts\test-csharp.ps1 -Lane LifecycleIntegration
.\scripts\test-csharp.ps1 -Lane DeepValidation
.\scripts\test-csharp.ps1 -Lane PreMerge
```

The diagnostic lanes are not serial final gates. Run focused controls during
implementation, one Fast control at a meaningful checkpoint, and one PreMerge
control immediately before merge. Do not repeat Fast immediately before
PreMerge or serially run all diagnostic lanes unless a focused failure requires
diagnosis. `Complete` is a temporary alias for `PreMerge`.
LifecycleIntegration and DeepValidation are conditional and explicit; this
branch ran each once because their category boundaries changed.

## Constitution Check

*GATE before research: PASS. Re-check after design: PASS.*

- **GitHub traceability**: #1505/#1526/#1547 retain accepted historical
  evidence; the current Fast project-boundary repair is tracked by open issue
  #1551 and linked from the active Spec Kit artifacts.
- **Spec Kit fit**: The implementation spans production validation orchestration, many guardian test files, traits/source guards, scripts, documentation, and multiple sessions.
- **Player-facing integrity**: No console, browser, copy, or player interaction changes.
- **Contract/state authority**: Validation rule bodies, issue codes, canonical schemas, state normalization, and GM-authored contracts remain unchanged. Mortal/afterlife prompts, examples, manifests, and contract matrices therefore need no update.
- **Test-first path**: Selection API compilation/fail-closed/equivalence/order tests precede production edits; source guards precede guardian migration and lane categorization.
- **Verification evidence**: Focused tests, project/source guards, the retained
  Fast checkpoint and relevant historical diagnostic controls, one final
  PreMerge summary, Serena health, and final diff checks are required.
- **Agent orchestration**: Work remains in the current Codex session. No subagent report will be treated as verification evidence.

## Project Structure

### Documentation (this feature)

```text
specs/1505-test-suite-performance/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

No external API or persisted-data contract is added, so `contracts/` is not required.

### Source Code

```text
BookOfEternityClient/
└── Services/
    ├── ValidationService.cs
    └── Validation/
        ├── GameStateValidationPhase.cs
        └── ValidationService.ValidationPhases.cs

BookOfEternityClient.Tests/
├── ValidationPhaseSelectionTests.cs
├── QteDeterministicLogicTests.cs
├── DarenQteDeterministicLogicTests.cs
├── FastTestBoundaryTests.cs
└── ordinary fast sources

BookOfEternityClient.TestSupport/
└── shared fixtures and helpers without test-SDK/xUnit dependencies

BookOfEternityClient.IntegrationTests/
├── GuardianValidationProfiles.cs
├── GuardianSystemRegressionTests*.cs
├── GameEngineTurnLifecycleTests.cs
├── IntegrationTestBoundaryTests.cs
├── FastBoundaryTestInventories.cs
├── TestLaneSourceGuardTests.cs
├── QteSceneServiceTests.cs
├── DarenQteShowcaseTests.cs
├── MortalWoundRecoveryTests.cs
├── MortalWoundTreatmentCapabilityAuthorityTests.cs
├── GmWorkerLiveSmokeTests.cs
├── LocalWebUiSmokeTests.cs
├── WebUi/BrowserMortalWorldGenerationFencingTests.cs
├── WebUi/BrowserStorageTransportParityTests.cs
└── full-validation, regression-integration, process, and E2E sources

scripts/
└── test-csharp.ps1

docs/
└── testing.md
```

**Structure Decision**: Keep phase selection in the runtime validation namespace
because the public facade and internal test overload share the same dispatcher.
Keep shared fixtures in a non-test support library. Keep ordinary fast sources
and slow integration sources in separate test projects without a reverse
IntegrationTests-to-Tests reference. Use one repository script and one testing
guide as the stable local interface.

## Phase 0: Research and Baseline

1. Preserve the issue comment's bounded measurements and fixed counts.
2. Pin the two-test guardian benchmark in `research.md` and `quickstart.md`.
3. Inventory direct broad validation files and real process/E2E classes.
4. Record why test filtering alone, Release builds, fixture-copy optimization,
   and parallelization alone do not address the measured multiplier.

## Phase 1: Validation Selection, Test First

1. Add RED tests that reference the missing internal selection API.
2. Cover empty and unknown masks, single-phase isolation, canonical combined
   order, all-phase/public equivalence, and repeated-call state isolation.
3. Add an internal 32-bit flags enum with 26 single-bit phases and `All`.
4. Route the public no-argument method through `All`.
5. Make the existing three phase groups conditionally dispatch phases in their
   original order, with one mask validation at the top.
6. Run the new focused tests and representative existing validation tests.

## Phase 2: Guardian Migration

1. Add test-side named profiles for the eight guardian partial-domain files.
2. Add a RED source guard enforcing no more than eight broad guardian calls and
   requiring any survivor to carry `FullValidation`.
3. Mechanically replace broad calls with the appropriate reviewed profile.
4. Run representative methods from each domain; add only the missing phase
   demonstrated by a failed assertion.
5. Run all guardian cases under a bounded command and compare discovery/results.
6. If the complete/fast budget is still missed, use discovery-validated,
   non-overlapping domain/method chunks. Keep the shared partial class intact
   and do not introduce shared mutable fixtures merely to obtain parallelism.
7. If repeated fixture initialization remains material, capture one prepared
   fixture snapshot in memory per test host and prove independent materialized
   roots.

## Phase 3: Verification Lanes

1. Add class- or method-level `FullValidation`, `RegressionIntegration`,
   `LifecycleIntegration`, `PreMergeSentinel`, `ProcessIntegration`, and `E2E`
   traits to the inventoried slow groups.
2. Extract common fixtures/helpers into `BookOfEternityClient.TestSupport`
   without a test SDK or xUnit dependency.
3. Move every reviewed slow source into
   `BookOfEternityClient.IntegrationTests`; keep the fast project independent
   from integration discovery.
4. Add source/project guards for partial-class ownership, dependency direction,
   direct/fixture-mediated full validation, file-backed regression integration,
   and known process/E2E entry points.
5. Implement `scripts/test-csharp.ps1` with explicit project routing,
   lane-specific hard caps including the amended bounded PreMerge deadline,
   timestamp/PID/GUID result
   directories, JSON/TRX/log output, cross-descriptor duplicate detection,
   a gated Windows launcher assigned to kill-on-close Job Object containment
   before target release, exact owned-tree verification after root exit, and
   `Process.Kill(true)` only as an uncontained-live-root fallback.
6. Test plan construction, executable process lifecycle, TRX aggregation, and
   source/project guards without launching an actual full suite.

## Phase 4: Performance and Integration Verification

1. Run the fixed two-test guardian benchmark at least three times after build;
   compare median runner time with the approximately 20-second baseline.
2. Run focused migration batches, every reviewed Guardian domain selection,
   and the retained broad-sentinel manifest under bounded controls.
3. Build the production solution and both test projects sequentially.
4. Run two consecutive Fast controls below five minutes each.
5. Run LifecycleIntegration exactly once; require all 186 reviewed cases below
   its ten-minute cap.
6. Retain the accepted DeepValidation result because PlanOnly proves the
   23-descriptor/1,950-case selection is unchanged and excludes lifecycle
   tests; require at least 1,950 results below 15 minutes.
7. Run exactly one PreMerge control below its single 30-minute deadline,
   retaining JSON/TRX/log evidence and at least 4,240 results, including
   completed ProcessIntegration and E2E phases, the exact ten lifecycle
   sentinels, and the exact ten spiritual-conflict sentinels. The full
   358-case spiritual-conflict matrix remains in RegressionIntegration.
8. Re-index Serena, confirm green health, no owned child processes,
   `git diff --check`, Spec Kit consistency, and review findings.

Final verification does not serially execute all diagnostic lanes. A failing
bounded control is narrowed with only the smallest relevant focused or
diagnostic selection.

## Phase 5: Suite-Growth PreMerge Capacity Correction

This section records the accepted historical #1526 decision. Phase 6 changes
only its now-obsolete final-gate deadline after new measured suite growth.

1. Retain the #1526 capacity-red PreMerge and compare its descriptor TRX
   timings with isolated and browser-only concurrent focused controls.
2. Reject the Fast-drain-only experiment after its exact run still exceeds the
   deadline, then trace the repeated setup on the remaining critical path.
3. Add RED/GREEN executable guards for retained PreMerge class costs and for
   one loaded browser-audit template per selected save in each test-host fixture
   with isolated per-row clones.
   Preserve filters, cases, assertions, phase order, the four-host ceiling, and
   the two-Fast ceiling.
4. Run the four ExplorerWeb shards as the first resource-isolation wave because
   paired evidence shows severe Fast-overlap contention. Drain that wave before
   starting the remaining parallel descriptors; do not change either cap,
   filter, or exclusive phase boundary.
5. Benchmark all 166 browser-audit rows, verify template immutability at fixture
   disposal, measure the two generated shards together, and replay the updated
   plan before the final control.
6. Reuse one empty ExplorerWeb directory skeleton per test host and immutable,
   hashed seed profiles for repeated deterministic theory setup; preserve a
   distinct writable root for every row.
7. Verify the focused guards, one PlanOnly contract, one meaningful Fast
   checkpoint, and one exact PreMerge control under the explicitly approved
   20-minute deadline. No other default lane timeout, concurrency cap, filter,
   phase, case, or assertion changes. A later measured coherent Focused
   selection or complete LifecycleIntegration run may opt into its separately
   bounded override (15 minutes for Focused, 30 minutes for LifecycleIntegration)
   without changing the five- or ten-minute default or any final-gate deadline.

## Phase 6: Measured PreMerge Deadline Correction

1. Retain the #1547 capacity-red run: `6,608/6,608` available core results
   green with zero duplicates and complete cleanup, followed by a global
   timeout before the exclusive process/E2E tail completed.
2. Retain the isolated ProcessIntegration result (`523/523` in `3:32`) and the
   prior isolated E2E timing to prove that the approximately 17-minute core plus
   exclusive tail cannot fit the 20-minute deadline.
3. Add a RED/GREEN executable runner guard for a 30-minute PreMerge default.
4. Change only the PreMerge/Complete lane-wide deadline to 30 minutes. Preserve
   every other lane timeout, concurrency ceiling, filter, case, assertion,
   phase boundary, phase order, retained cost, and scheduling wave.
5. Synchronize `docs/testing.md`, the #1505 data model/research/quickstart, and
   active implementation-plan final-gate expectations.
6. Run one updated PlanOnly contract and one exact PreMerge control. Require
   completed ProcessIntegration and E2E, all official results green, zero
   duplicate IDs, timeout false, and complete owned-tree cleanup.

## Phase 7: Fast Physical-Boundary Repair

1. Extend `FastTestBoundaryTests.ReviewedHeavySources` into an exact relative
   path manifest and add the #1551 canonical, process, and host sources before
   moving them. Extend `IntegrationTestBoundaryTests` with their required
   `RegressionIntegration`, `ProcessIntegration`, or `E2E` categories. Run the
   two boundary tests first and retain the expected RED result while the files
   are still in Fast.
2. Move the mandatory canonical group into Integration with
   `RegressionIntegration`: `MortalWoundRecoveryTests.cs`,
   `MortalWoundTreatmentCapabilityAuthorityTests.cs`,
   `WebUi/BrowserMortalWorldGenerationFencingTests.cs`, and
   `WebUi/BrowserStorageTransportParityTests.cs`.
3. Split `QteSceneServiceTests.cs` at its semantic boundary. Keep all
   deterministic key normalization and grading/effective-requirement tests in
   fixture-free `QteDeterministicLogicTests.cs` under Fast. Move the canonical
   runtime, persistence, rollback, console, and save/archive tests plus their
   fixture helpers to Integration as `QteSceneServiceTests.cs` with
   `RegressionIntegration`. Preserve every theory row and assertion exactly
   once.
4. Move `GmWorkerLiveSmokeTests.cs` into Integration with
   `ProcessIntegration`, because it launches a real PowerShell worker. Move
   `LocalWebUiSmokeTests.cs` into Integration with `E2E`, because it exercises
   an in-process HTTP host and complete browser API flow without launching a
   child process.
5. Run `Focused` Integration selections for each moved category group and a
   focused Fast selection for `QteDeterministicLogicTests`. Replay Fast
   `PlanOnly` and require unique, non-overlapping membership.
6. Run one bounded Fast checkpoint. If wall time is above four minutes or does
   not retain at least one minute of hard-limit headroom, continue through the
   already-measured ranking and move only sources that satisfy the same
   semantic rule. The reviewed second group is the following exact relative
   source paths under `BookOfEternityClient.Tests`:
   - `ShiningCoreActionResolutionValidationTests.cs`
   - `GuardianCorrectionServiceTests.cs`
   - `ShiningBlessingEffectStateTests.cs`
   - `AfterlifeNotificationStateTests.cs`
   - `DarenQteShowcaseTests.cs` (mixed: move only its 12 file/service rows and
     retain its 77 deterministic rows in Fast as
     `DarenQteDeterministicLogicTests.cs`)
   - `ShiningTradeRequestStateTests.cs`
   - `BrowserLocalWriteCoordinatorTests.cs`
   - `TrainingServiceTests.cs`
   - `NpcTradeServiceRequestFlowTests.cs`
   - `MortalWoundOpportunityAdapterTests.cs`
   - `ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs`
   - `WebUi/BrowserShiningRelicForgeParityTests.cs`
   - `WebUi/BrowserResidentInteractionsParityTests.cs`
   - `WebUi/BrowserAfterlifeArchiveParityTests.cs`
   - `WebUi/BrowserShiningIncarnationGatesParityTests.cs`
   - `WebUi/BrowserInkFeatherFateParityTests.cs`
   - `WebUi/BrowserNpcSocialParityTests.cs`
   - `WebUi/BrowserTradeParityTests.cs`
   When T061 is triggered, the exact group also unconditionally includes the
   real-worker candidate `GmWorkerValidationRepairDelegatorTests.cs`, classified
   as `ProcessIntegration`; classify every other listed source as
   `RegressionIntegration`. For every moved source, preserve the same relative
   path below `BookOfEternityClient.IntegrationTests`, update both exact
   manifests in the same group, and retain every test row and assertion.
7. Protect both semantic splits with exact Roslyn inventories of Fact/Theory
   method names and normalized InlineData arguments: QTE `66/51` and Daren
   `77/12`. Resolve required categories only from attributes on the expected
   top-level class; comments, strings, and method-level traits are decoys, not
   ownership. These inventories do not hash method bodies; T064 whole-range diff
   review verifies body and assertion preservation.
8. Repeat focused category verification and Fast PlanOnly after any second
   group, then run two representative Fast controls. Both must finish below the
   unchanged five-minute hard limit; approximately three minutes is the
   preferred operating target. Require zero duplicate test IDs and complete
   owned-process cleanup.
9. Update `docs/testing.md`, `research.md`, `data-model.md`, and `quickstart.md`
   with the final source/category manifest and retained run IDs. This is an
   internal test-scheduling correction only, so Mortal World and afterlife GM
   prompts, examples, manifests, client commands, and runtime contract docs do
   not change.

## Complexity Tracking

No constitution violations require justification. The production enum/overload
is internal, uses the existing phase methods, and adds no new persistence or
external service.

## Current test-lane boundary amendment — #1536 / #1505, 2026-09-30

The owner-approved #1536 TEST-LANE-BOUNDARY-DECISION revision 1 updates FR-014
and SC-013 of this feature. Its canonical selection design and execution tasks
are in `specs/1536-complete-wound-materialization/plan.md` and `tasks.md`
(`T081-D-PERFORMANCE-LANE-SPLIT-*`); the older phase plans above remain historical
evidence. The full 704-case ordinary spiritual cutover matrix now belongs to a
separate `SpiritualCutoverIntegration` lane with a 30-minute hard limit. PreMerge
keeps eight fixed complete-path sentinels, the other ordinary Integration
selection, full Fast and its exclusive ProcessIntegration/E2E phases. The five
cutover ProcessIntegration cases remain in their existing phase. Full matrix
control is required when related spiritual conflict, wound, recovery, C2/C3/C4,
or contract behavior changes. Both controls must distinguish discovered
per-method minimums from completed TRX cases and reject missing methods, skipped
cases, duplicate IDs and incomplete cleanup. Current completion evidence will
be recorded in the active #1536 plan; neither 30-minute limit is raised here.
