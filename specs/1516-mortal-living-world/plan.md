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
