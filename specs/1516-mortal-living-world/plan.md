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

## Lifecycle and guidance RED, 2026-11-16

At WIP `9918bc126f1ae187d883c2ad8dfd492ef73d46c1`, official Unit build
completed with 0 errors and 96 warnings (83.61 s). The event category selected
47 cases: 38 previously accepted cases passed; nine new lifecycle cases failed
at the explicit four-argument lifecycle scaffold. The four authored-guidance
cases failed for the missing template, guide reference and examples. These are
bounded RED observations, not whole-feature failures or acceptance. Both runs
completed cleanup without skipped or duplicate cases.

The engine integration build then failed before execution because the new test
omitted the `SharedJsonOptions` namespace. Adding the Configuration using fixes
that setup issue; no engine behavioral RED is claimed yet. QTE clock/consumer
scaffolds are separate and unexecuted.

A concrete independent architecture consultation selected a common deterministic
receipt representation over authenticated source tuple, complete report and
assessed clock/window. Full settled-world serialization is not required by the
receipt contract and is not the live safety gate. Actual final clock/effect
validation, current source ownership and exact output-byte fences remain
mandatory. Diagnosis/joint contributions must enter their genuine common plan
before sealing, and final semantic checks must gate owner completion. A receipt
alone never grants replay, acceptance or cross-lease authority. This design is
not yet implemented or qualified.

The next WIP batch built both projects successfully (Unit 82.19 s/96 warnings;
Integration 53.38 s/125 warnings, both zero errors), but catalog preflight
rejected two new QTE categories with empty change hints before any tests ran.
Those metadata hints are corrected; the same compiled sources will be used
for the official `-NoBuild` runs. This is setup failure, not behavioral RED.

## Lifecycle DATA, guidance and privacy selected results

At metadata-corrected `e789df5e`, the unchanged compiled sources passed event
DATA 47/47, authored guidance 4/4, and common player-news projection 4/4. The
latter includes two new report/history noninterference cases plus existing
accepted/hidden location controls. This does not claim a browser session or
complete #1540 privacy audit. The independent review found no confirmed code
blocker in the lifecycle/guidance scope; its stale catalog exclusion was fixed.

Actual full-daemon rendering and its existing skill worked-example companion
passed 2/2, official run `20261116-035235-515-592-49a0fc7a3a034f16b6dec6903f9abbd7`,
19.962 s wall, complete cleanup. All compiled sources remained frozen throughout.

New QTE DATA scaffold: 27/27 expected NotImplemented failures. Receipt DATA
scaffold: 26/26 expected failures. Real QTE consumer cases: 3 pass and 4 fail,
requiring causal inspection before implementation. Real outer engine rollback:
2 pass and one causal failure because the repair collector incorrectly requests
client-owned living-world history from the GM after publication. No engine
critical-health-loop acceptance is implied by these three cases.

The diagnosis test packet executed five cases but all stopped at a preexisting
fixture omission: the canonical location/storage seed lacks the required #1515
derived storage-access projection. This is fixture/setup failure, not diagnosis
feature RED. Correct it through the existing production projector before
scheduler/original capture, without weakening validation, then rerun.

The official category runner stops after its first failure. Therefore the first
combined unit packet completed only 4/5 descriptors (82 cases, 55 pass/27 fail),
and the combined integration packet stopped on diagnosis. Remaining receipt,
engine, QTE and actual-render categories were run separately. No complete batch
PASS is claimed. Raw output stays outside committed source documentation.

## Receipt revision 2 and real-owner follow-up, 2026-11-16

Receipt DATA passed 26/26 and pure QTE clock DATA passed 27/27 on the fresh
Unit build (0 errors, 96 warnings, 84.94 s). Independent receipt source review
found no blocker; repeated private history validation remains a nonblocking
preexisting cost opportunity, not a measured regression or speedup claim.
The original seven ordinary publication cases remain GREEN with revision 2.

Runtime lifecycle executed four cases: project completion and false-birth
rollback passed; genuine project/flag birth failed because the ordinary owner
still called the three-argument helper without selected authenticated absence.
This is the causal RED for the narrow runtime absence wiring. The earlier broad
ordinary selector also selected these four cases, so their repeated execution
is not additional distinct coverage. That selector is narrowed to its seven
original methods before subsequent runs.

Engine history exclusion passed all three original cases. The new late-health
loop case failed before its injected stage: its minimal faction/full-resend
fixture violates current raw faction contracts. It is being replaced with a
legitimate no-change, no-faction scenario; no reentry behavior RED is claimed.
QTE direct/short controls passed five cases, while both offer/Begin cases still
stop at vehicle birth-receipt setup even after using the supported empty root.
Their candidate/source mismatch requires diagnosis, not weaker validation.
Diagnosis now reaches genuine common composition: missing, T0 and unsupported
reports were wrongly admitted, and the accepted T1 plan omitted the living
receipt/report cohort. Those are causal integration RED observations.

The eight-case joint category exceeded its 300-second category budget and
completed no TRX descriptor; official overall timeout is not a batch verdict.
Its retained stdout nevertheless contains two reached failures: valid genuine
common sealing omitted HistoryPath (103 seconds), and a post-commit drift probe
could not reach a history member that was never published (175 seconds).
The first establishes the required preseal contribution gap. Do not rerun eight
expensive fixtures just to reproduce that RED. Subsequent verification starts
with one genuine representative under a protective 600-second category budget.
Use the fixture's existing scoped stable-DATA reuse and lossless gzip sink,
without shared mutable fixtures or weakened fresh ownership/output fences.
The old plain diagnostic capture is retained locally; none is uploaded to GitHub
or Spaces. Cost comparisons must account for the newly reached work.

Joint raw-string setup required a four-dollar interpolation delimiter. A JavaScript
replacement initially collapsed the dollars in its published copy; the exact
unchanged local source was then published as `50e31cf3`. The successful integration
build used those frozen bytes (0 errors, 167 warnings, 110.39 s), not the malformed
intermediate remote revision. This is publication/setup correction, not behavior.

The joint completion marker retains the accepted post-original report declaration
and exact receipt/history footprint. Fresh recovery recomputes the declaration
against the authenticated original clock/domains and actual completed effects.
When the original records report absence, the marker is the owner-issued accepted
comparison DATA; its public hashes are not a cryptographic signature. The
single-file rehashed-history test checks the supported exact footprint fence;
it does not establish resistance to a coordinated rewrite of the affected joint
completion marker and living-world history by the same user process. These are
the two relevant client-owned witnesses; the authenticated original and clocks
need not change for that limitation to apply. No new signing scheme or after-seal
sidecar is introduced.

## Owner contribution WIP checkpoint, 2026-11-16

Diagnosis and joint living-world contributions now join the real common owner
before sealing, rather than relying on an ordinary sidecar. Independent review
found and corrected diagnosis persisted semantic reconstruction and exact
same-tuple opening-guard freshness; direct and retained completion have separate
phase-appropriate fresh checks. Thirteen diagnosis cases are now selected,
including actual resource-cost completion and drift controls. These changes are
UNBUILT/UNRUN at this checkpoint, not accepted GREEN.

Ordinary lifecycle now forwards selected authenticated absence; the critical
engine-loop fixture is corrected to a legitimate no-change scenario; QTE has
focused response/canonical-vehicle controls to isolate the earlier setup failure.
No engine reentry or QTE offer/Begin fix is claimed yet. Joint representative uses
existing scoped stable-DATA reuse and lossless gzip captures; fresh owner/effect
checks remain mandatory. Next: fresh integration build, bounded categories,
then one genuine joint representative, not the timed-out eight-case batch.

## Targeted owner checks and next correction packet, 2026-11-16

Fresh integration build at e9cf3bad passed (0 errors, 167 warnings, 112.83 s).
The first runner attempts stopped before discovery because the new representative
category used a string instead of the required related {id,when} object. That
metadata-only correction was independently approved and saved at d3a24e65;
Read-TestCategoryCatalog then loaded all 1,768 entries. No behavioral RED is
claimed for the catalog setup error.

On the unchanged built sources, lifecycle publication passed 4/4, ordinary
publication 7/7, and original-source guards 20/20. All selections completed
with zero duplicates, no timeout and owned cleanup complete. Engine tests gave
3 PASS and one causal late-critical failure: history publication was reached,
but the subsequent repair did not request full original-turn resubmission.
The current WIP fixes this through the existing rollback/repair owner, carrying
fresh prepublication GM path obligations as DATA, not permission.

QTE gave 6 PASS/3 FAIL. The new public-response control proves canonical vehicle
receipt validation was incorrectly applied to a response fragment; canonical
missing-receipt rejection itself passed. The minimal current correction gates
that existing check by its canonical context, preserving raw vehicle commands.
Offer/Begin guards are not yet implemented or behaviorally qualified.

Diagnosis gave 4 PASS/9 FAIL: exact preseal contribution and three admission
cases now pass. All runtime cases stopped at the same storage-access preimage
mismatch because the harness passed an empty backups dictionary. New fixture
calls use genuine LivePrepare manifest.files, including signed storage access;
production storage validation is unchanged. Next run starts with one real
compensation/retry representative before the full completion-drift packet.
The forthcoming owner/fixture/docs correction packet remains WIP until rebuilt
and selectively verified. Joint representative is still pending. No raw logs
are published; no whole-feature or hosted-CI acceptance is claimed.
