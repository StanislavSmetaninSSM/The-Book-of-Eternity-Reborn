# Mortal Living-World Implementation Plan

> Execute bounded blocks with TDD and independent review; do not treat a saved WIP block as feature completion.

Source: [#1516](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1516). Spec: [spec.md](spec.md).

## Goal and architecture

Enforce explicit time-correlated autonomous-world assessments backed by canonical effects, without simulating or inventing events. Existing domain reducers remain the only mutation owners. A pure typed contract validates detached original/current DATA. Runtime adapters authenticate source, prepare this data under existing leases, verify completed effects and publish private receipt/history with the same transaction.

Tech stack: existing .NET 8/C#, SDK 10 compiler, JSON, PowerShell 7 official category runner. No new runtime package or network service.

## Global constraints and design choices

- Mortal only; no legacy-save migration, hosted CI, broad test suite, merge or issue closure.
- Default meaningful time uses existing world/faction cycles (240/1440 minutes), including a newly crossed boundary this turn.
- Private command: `game_state/control/mortal_living_world_report.json`. Private history: `game_state/world/mortal_living_world_history.json`. Neither is a player news source.
- A report is evidence, not a JSON patch or reusable permission. Domain reducers own all changes; exact selected before/after values must differ and agree with authenticated source and settled canonical output.
- Reports identify time, entities/systems, causes, targets, knowledge, consequences and proof. `noSignificantChange` remains legal with bounded current-world assessment.
- Existing news projection currently renders event objects without filtering visibility. New private material never goes into those nodes. Explicit player-visible projection/reference checks are required.
- Bootstrap narrative opening is not retroactive autonomous mechanics; active fixtures and source documentation must identify this boundary.

## Research and owner map

- `ProgressionScheduleService.ValidateMortalOutcomeData` checks cycle counts/correlation/markers, not world effects. Pending counts precede the turn; they miss the initial large skip unless resulting time is included.
- `ValidationService.NpcWorldAndMeta` world-event validation lacks complete semantics.
- Ordinary raw gate: `GameEngine.ValidationAndRepair.Part03.CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync`.
- Ordinary publication: `AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync` in `CanonicalStateNormalizer.MortalItems.cs` owns canonical lease, before-images and rollback. Capture before any mutation, verify exact effects after reducers, then publish receipt under that same owner.
- Mortal joint acquisition+treatment bypasses that coordinator: `GameEngine.MortalJointRepairCanonicalPublication` → `CanonicalStateNormalizer.MortalJointRepair`. Report/history source/before/after must join `WoundAcceptedTurnPlanner.MortalJointRepairCommonPlan` BEFORE seal/take; no extra writes after sealing. Reuse pure checks before transaction readback.
- QTE terminal normalization in `QteSceneService.ApplyTerminalOutcomeStateChangesCoreAsync` has its own baseline; inspect current clock permissions and integrate the same gate where meaningful time is allowed. Do not append an ordinary receipt on intermediate QTE preparation.
- Existing NPC/faction/project/location consumers retain lifecycle ownership. No second project reducer.

## Implementation blocks

1. Pure assessment/effect contract and independent typed tests. Pin time-trigger boundaries, source correlation, closed shapes, complete semantic rows, unchanged audits, exact evidence and bounded repairs. No runtime completion claim from this block.
2. Ordinary source capture/publication adapter with current-root identity resolution, snapshot/absence coverage, raw and canonical gates, private receipt/history and real reached late rollback. Cover first skip, stale sources, partial domains and repeat.
3. Joint and QTE owner integration using existing pre-seal source/cohort contracts; preserve ordinary reducer behaviour, test real supported consumers and reject bypass.
4. GM docs/templates/examples/manifests/guards and impacted bootstrap fixtures; narrow new privacy projection guard where needed. Whole intended-diff independent review and acceptance reconciliation.

Each block follows RED → minimal implementation → focused GREEN, publishes WIP before long verification/review and records actual results below. Do not mark later blocks complete from a pure helper test.

## Optimization assessment

Pure contract operates once on detached typed/JSON DATA; no file reads or global cache. Runtime owner captures each requested source once per preparation, reuses parsed immutable input through the existing operation/source architecture, and freshly validates ownership, original identity, source closure and final effects at publication. No cached permission, mutable shared fixture or parallel dictionary cache. Measure expensive named integration phases; no speedup claim until measured. Unit fixtures are independently allocated minimal inputs.

## Review focus

First large time skip; missing original/absence proof; no-change audit hiding due autonomous work; unrelated/cosmetic effect evidence; private/undiscovered references in public news; joint/QTE bypass; receipt written outside rollback cohort; retry history duplication; stale source after preparation. Review writer receipts, live identity history, current carrier image and canonical/full fingerprint semantics together.

## Verification selection

New small categories will own typed contract, ordinary publication, owner integrations and docs. Select existing impacted progression/world consumers by actual code changes, not all categories. `tests/selection.json` will be reconciled to the entire intended diff. Linux selected runs do not establish Windows/live-provider acceptance. #1368 remains not_planned.

## Checkpoint 2026-11-16 — WIP planning

Branch `1516-mortal-living-world`, base `605ec71f1f9610d53f0c44f8a7ccf2c0860e3c6d` verified remotely. Fresh independent clone of branch into `restore1516-base` matched that SHA and was clean. Download-only recovery, not a build result. Shell push lacked credentials; GitHub connector created the branch successfully. No implementation changes existed before durable branch creation.

Read live issue, AGENTS, development/performance/testing policy, constitution and methodology skills. Independent read-only reconnaissance confirmed above gaps and bypass owners. No code/tests yet; no independent implementation review yet. All feature verification UNRUN. Next: publish these planning artifacts, then typed contract RED/GREEN.

Environment restored from official sources: Linux x64, SDK 10.0.404, runtime 8.0.34, PowerShell 7.7.0. Toolchain under `/workspace/shared/toolchain`, source `/workspace/scratch/2f2a1767475c/boe1516`; source `env.sh` to set SDK and writable XDG/CLI caches. Initial PowerShell startup failed on read-only default cache; using explicit writable XDG directories resolves setup. Raw diagnostics stay outside repository, `/workspace/shared/diagnostics1516`. No hosted CI dispatched.

### Causal DATA RED checkpoint

`d911facf` was remotely verified and restored cleanly in a separate checkout
before implementation. Official runner first completed 57/57 assessment FAIL,
all with the explicit admission scaffold exception; its fail-first behavior
left event tests unexecuted. A separate event category then completed 12/12 FAIL,
all with the explicit event scaffold exception. Both had no duplicate cases,
no timeout, complete owned cleanup. This is causal RED, not an acceptance claim.

Environment-only earlier attempts selected zero tests: MSBuild Unix IPC denial,
then missing ASP.NET Core 8 runtime. A fresh single-node build succeeded; official
`-NoBuild` execution worked after installing ASP.NET 8.0.34. These failed setup
attempts are not silently reclassified as red feature tests. No tests are skipped
or budgets weakened. Current next block adds real signed-original capture tests;
its source class is explicitly unimplemented until that block's causal RED.

### DATA GREEN and independent review

Official selected execution at remote `ebcee988` completed 69/69 PASS (57
assessment + 12 event cases), no skips/duplicates, complete cleanup, wall
12.053 seconds. The fresh independent Sol XHigh review nevertheless found three
Important coverage defects: canonical activity label/narrative-only edits could
count as effects; location storage/topology used section names rather than
canonical fields; referenced known-news rows lacked a safe public projection
check. All are accepted for regression-first correction. This checkpoint is
therefore not accepted for integration merely because its initial tests pass.

The next real-original category completed 8/8 causal RED, each failing exactly
at the explicit source scaffold after actual live preparation/signing. Fresh
integration build had 0 errors and 124 warnings; runner wall 12.240 seconds,
no skips/duplicates and complete cleanup. Source capture implementation now
replaces that scaffold and adds a current Mortal observation family; its GREEN
and impacted NPC-original-presence control checks remain pending.

Source GREEN attempt correctly refused all eight fixtures before report
validation: the generic live presence fixture had not created a progression
ledger. Its default supplied control was not a real scheduler preparation.
The source guard is retained. The fixture now calls the real scheduler before
live signing and asserts authenticated clock/schedule coverage before invoking
the tested source. This is an explicit fixture correction, not permission to
fabricate signed ledger DATA or weaken required originals. The six impacted
NPC-presence controls were unrun because the runner stopped at the failed source
category; they remain scheduled.

### Reviewed DATA correction and source runtime check

Remote `1fd85fb2` preserves the three review corrections. Official DATA selection
completed 85/85 PASS (57 assessment, 28 event cases), no skips/duplicates,
complete cleanup; run `20261116-024927-641-786-ac3b44e0b106413b85c67820177accd9-categories`.
The review regressions had first produced 18 PASS / 10 causal FAIL. Re-review is
pending. This does not qualify canonical publication.

All eight real living-world source cases passed. The six impacted NPC-presence
controls gave five PASS and one failure: their deliberate prior-family fixture
removed only the former NPC extension from the now larger current family, so
it no longer represented the exact prior base form. The fixture now explicitly
retains exactly the prior base keys before re-signing; the strict production
reader is unchanged. That corrected control is pending. Source independent
review is in progress, including realm/control correlation and closing fences.

### Further causal safety checks

DATA re-review accepted canonical field and public-news fixes but found the
same cosmetic-evidence defect through nested threat activities and faction
labels. Eight new cases produced 32 PASS / 4 expected FAIL out of 36 event
cases, complete cleanup, no duplicates, wall 9.561 seconds. Typed projections
now cover those admitted fields; GREEN and targeted re-review remain pending.

Independent source review found three Important issues: ambient cached working
bytes could defeat a physical closing fence; retained baseline plus timeChange
was undercounted; soul/schedule/current-control correlation was incomplete.
Twelve added source cases produced eight old PASS and twelve FAIL. Eleven new
failures were causal behavior/API-scaffold failures; the cache test instead
used a wrong physical fixture path and must be corrected and rerun before its
freshness fix is credited. Do not describe that setup error as causal RED.
The unchanged reader-family controls remain pending due fail-first execution.

Six real ordinary-owner tests then produced six causal FAIL: missing gates,
missing private receipts, missing effect refusal and unreachable late cut.
Each reached the actual coordinator and scheduler/live-original preparation.
Run `20261116-025804-991-593-06a0344a976a4d79ab42bcb4abba1a6e-categories`,
wall 13.382 seconds, complete cleanup, no skips/duplicates. Ordinary implementation
is next. This first owner block proves failed-publication rollback/retry only;
successful same-original reentry, durable treatment/diagnosis plan contributions,
joint and QTE remain explicit later gates, not inferred from plain normalization.

### Physical fence reproduction and ordinary implementation WIP

The cache fixture's second attempt exposed a lease-close refusal that masked
its body result. The corrected observation records both separately. At
`8ce70048`, source returned successfully on stale report bytes while independent
lease close rejected `Operation source changed`; this is now causal evidence
of a too-late fence, not a claimed successful publication. Nineteen other source
cases passed. Existing fresh-read suppression now surrounds source preparation
and both closing APIs. GREEN remains pending. One earlier interrupted build
failed CS2001 while a concurrent uncommitted owner file was moved back to held
scratch; no test executed and it is not a feature RED. Subsequent builds used an
explicit source freeze.

Seven real ordinary tests now give seven causal FAIL, including a reached
faction-reducer boundary clock change that opening-only admission missed. The
plain-owner adapter and exact rollback cohort are implemented next, not yet
verified. Same-original successful replay and special transaction owners remain
outside this bounded first implementation; they are not marked complete.

The combined DATA check completed 95 cases: 94 PASS, one expected FAIL for a
hidden event whose exact event ID also appears in world news. All nested activity
and faction-label regressions pass. The final small fix rejects this explicit
identity contradiction irrespective of the news row's visibility; unrelated
news, prose aliases and broader #1540 remain separate. GREEN/re-review pending.

### Accepted bounded source/DATA/plain-owner checkpoint

Remote `436500c4f742c7b7c586468a0fbcc8cf25cedbe8` was verified and restored
into a separate clean checkout with identical HEAD. Official fresh verification:

- source + impacted NPC presence + plain ordinary publication: 33/33 PASS,
  all three descriptors complete, no skips/duplicates, owned cleanup complete,
  wall 31.872 seconds. Source category 9.258 seconds; ordinary category 11.690.
- DATA assessment + event evidence: 95/95 PASS, complete selection/cleanup,
  no skips/duplicates, wall 12.656 seconds.
- Integration build: 0 errors, 166 warnings, 109.23 seconds. Unit build:
  0 errors, 54 warnings, 29.24 seconds. No hosted CI or broad suite.

Independent fresh-context Astra High source re-review resolved all three source
findings; Sol XHigh DATA re-review resolved semantic and hidden-news findings.
A separate fresh Astra High ordinary-owner review found no Critical, Important
or actionable Minor defect in the bounded adapter and seven real tests.
These verdicts do not approve whole-issue merge/closure or unimplemented owners.

The ordinary operation reuses detached original/final DATA and hashes already
parsed settled roots once. Physical source/output fences still read freshly;
no cross-operation cache or persisted authority is added. The source's small
original-root parse/clone overhead remains acknowledged, not called a speedup.

Remaining: real full-turn repair/resubmission/rollback boundaries, supported
lifecycle effects, treatment/diagnosis/joint and QTE paths, new private-history
projection proof, GM delivery/guards and full intended-diff selection. Determine
actual successful reentry requirements before inventing a new registry: strict
same-original refusal already prevents duplicate append, while failed-publication
retry is demonstrated. No claim is made that a serialized receipt grants reuse.
