# Mortal Living-World Implementation Plan

> Execute bounded blocks with TDD and independent review; do not treat a saved WIP block as feature completion.

Source: [#1516](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1516). Spec: [spec.md](spec.md).

## Current status, 2026-11-17 — reopened verification, WIP

The earlier completion claim below is superseded. Genuine multi-turn engine
verification exposed a prepared-schedule source mismatch. The local correction
and journey tests survived environment recovery but were not remotely saved;
this checkpoint preserves them as UNVERIFIED WIP before fresh build/testing.
No completed four-interval journey or prose-only rejection is claimed.
Next: build and execute the single elapsed-no-change control, then diagnose
remaining full-state refusals without suppressing validation.

## Recovered runtime evidence, 2026-11-17 — WIP

Remote 9b6680d0 preserved the exact recovered source tree. Fresh integration
build passed (0 errors, 171 warnings, 160.67 seconds). The official two-case
journey category produced 1 PASS / 1 FAIL in 24.059 seconds with complete cleanup.
The actual elapsed-no-change ProcessPlayerTurn now succeeds; the rich journey
was refused before dispatch because the seed used empty strings for canonical
non-member fields that materialization requires to be JSON null.

This checkpoint restores those nulls and makes failure diagnostics conditional,
avoiding unnecessary full-state validation on successful turns. Fresh build:
0 errors, 171 warnings, 124.25 seconds. Official rerun: 1 PASS / 1 FAIL,
36.115 seconds, no skips/duplicates/timeouts, complete cleanup. The rich journey
is now causally refused by generic faction validation: all four canonical null
membership fields are required to be strings. No journey event is accepted.
Do not replace nulls with strings or suppress validation; repair the canonical
Mortal-vs-command validation routing with focused negative controls next.

Independent source review also identifies absent NPCActivityUpdates consumption,
missing contradictory-package/same-request repair coverage, and incomplete
receipt/news persistence assertions. These remain open, not proven runtime fixes.
The schedule fix does not cache authority; conditional diagnostics remove repeated
work without skipping production validation. No speedup claim is made. No merge,
closure, hosted CI, live model or UI acceptance. Raw diagnostics remain outside Git.

## Focused faction regression preparation — UNRUN

Added a narrow ten-case official category for canonical faction shape/completeness:
one desired-success split bundle, four malformed membership fields, three missing
sidecars, and two incomplete raw creation commands. Tests use existing minimal
fixtures, not GameEngine. Production is unchanged; build/discovery/runtime of
this block are unrun. Expected positive RED is the canonical-vs-raw routing defect.
The journey author now supplies complete purpose/currentAgenda/principles command
data without changing unrelated values. No source finding is claimed as PASS.

## Focused canonical RED and raw boundary preparation — WIP

The ten-case official focused run completed in 12.650 seconds: 9 PASS / 1 FAIL,
no skips/duplicates/timeouts, complete cleanup. The desired-success canonical
split bundle is rejected for embedded resources/ranks plus four null membership
fields. Fresh integration build had 0 errors, 171 warnings, 125.04 seconds.
Five additional raw nonmember cases now cover file shape, raw materialization and
public response validation. These additions are UNRUN. Source review confirms
raw nonmember materialization also requires null, so nullable handling must cover
both Mortal forms while embedded resource/rank requirements stay raw-only.
The existing narrow selector owns all fifteen rows; production remains unchanged.

## Fifteen-case RED and minimal correction — WIP, GREEN unrun

Official focused run: 13 PASS / 2 FAIL in 13.403 seconds; no skips, duplicates or
timeout; complete cleanup. The raw positive has exactly eight null-as-string
errors (four file-shape and four public response errors), while raw materialization
accepts the same complete payload. Canonical positive exposes split resources/ranks
plus four nullable fields. Integration build: 0 errors, 171 warnings, 123.27 seconds.

The minimal production correction routes exact canonical faction-core state
separately: embedded resources/ranks are not required there but supplied objects
remain checked and mandatory sidecar completeness remains intact. Four membership
fields use the existing nullable-string validator for both Mortal raw creation
and canonical core. Raw embedded carriers, commands and the shared generic string
validator remain unchanged. No new reads, parsing, cache or authority reuse.
Journey seed now uses only canonical split resource/structure carriers. Fresh
build, focused GREEN and next actual journey remain UNRUN at this checkpoint.

Recovery note: a86c85a0 transferred one test file incompletely. 8b3c5957 repairs it
and its full tree matches local verified 811645fd exactly; the 15-case RED ran from
the complete source, not the damaged transfer. Use repaired or later checkpoints.

## Faction GREEN and reached living-world refusal, 2026-11-17

Fresh b120e037 integration build: 0 errors, 171 warnings, 157.04 seconds.
The official combined run completed all 17 cases in 31.373 seconds: 16 PASS /
1 FAIL, zero skips/duplicates/timeouts, complete cleanup. All 15 focused faction
cases PASS; the real quiet elapsed turn also PASS. The unchanged four-turn
journey now clears its initial baseline and reaches real turn42 contract repair.
The first scout progress effect is refused with mortal_living_world_effect_mismatch
and mortal_living_world_effect_not_meaningful: NPCActivityUpdates did not change
canonical currentActivity. The test deliberately fails rather than accept a
fictional effect or manually patch canonical progress. No later journey turn is
claimed executed. This confirms the missing NPC activity consumption identified
by source review, not a need to weaken living-world evidence checks.

Next bounded block: demonstrate actual activity command consumption with the
existing normalizer and exact rollback/identity controls; integrate only through
existing publication ownership, preserving simultaneous NPC skill/core changes
or refusing unsupported combinations. No new activity system or cache is implied.
After that, rerun the retained world journey and complete explicit receipt/news
persistence plus contradictory-package/same-request repair evidence. Issue remains
open; no merge/closure, live model, UI or full feature acceptance is claimed.

## NPC activity component reproduction — test-only UNRUN

A new sparse actual-normalizer case authors simultaneous supported NPCCoreChanges
and NPCActivityUpdates against a genuine existing actor, exact mirrored core and
an unchanged sibling. Whole-core expected equality protects receipts/history,
checks command consumption and no replay. It does not fabricate an accepted plan
or claim accepted-turn ownership. Category: mortal-living-world-npc-activity-normalization.
Source/build/discovery/runtime are unqualified until execution; no activity
production code is changed. This component gate supplements, never replaces,
the already reached real GameEngine refusal.

User's new requirement: final acceptance needs a LIVE GM relay journey with the
root assistant acting as GM. Scripted regression responses above are technical
regression evidence only, not live relay acceptance. Relay preparation runs
separately while this defect is reproduced and corrected.

## Recovered environment and actual NPC activity RED — WIP

The execution environment was replaced during the first component build; its
process key changed and local checkout/toolchain/diagnostics disappeared. All
source was already saved at 5ffda1ed. A clean clone restored that exact commit.
The interrupted build is UNKNOWN, not a test result. Historical result summaries
above remain recorded but their old local raw logs were lost.

Official sources restored SDK 10.0.404, .NET/ASP.NET 8.0.34 and PowerShell 7.7.0.
Integration restore passed; fresh single-node build with shared compilation off
passed with 0 errors, 171 warnings in 143.03 seconds. Official single component
run now gives genuine RED: expected activity time240, actual0, after real core
command consumption; test883ms, runner26.950s, 1 executed/1 failed, no skips,
duplicates or timeout, complete cleanup.

The drafted update-only consumer is UNCOMPILED/UNRUN. It uses existing normalizer
lease/rollback ownership, original actor/activity preconditions and CURRENT core
output, preserving other fields and mirrored actors. Operation-local immutable
DATA preparation is reused; command freshness is checked before publication and
consumption. The sidecar enters existing rollback inventory. Pending/specialized
plans, whole-carrier/pending/consumed/transition owner overlaps, sealed skill
candidates and terminal-completion combinations fail closed before writes until
composition is supported. No completion semantics, new cache or new publication
authority is introduced. Negative guard and ordinary accepted-item/effect tests
remain necessary; component NoPlan success alone will not qualify this consumer.

## Activity guard preparation — WIP, unrun

Saved production checkpoint 28f8fd32 builds successfully: production project,
0 errors, 19 warnings, 53.63 seconds. No runtime GREEN is implied.
Independent source review identified permanent-ID alias ambiguity and Int64
versus canonical Int32 mismatch; both are corrected before build. The existing
caller always replaces progression context with Validated.Binding, retaining its
Mortal realm check. Command-only on-disk core is rejected, not silently restored;
StateDistributor's ordinary command payload merge preserves canonical arrays.

Three cheap guard theories cover 29 cases against the actual normalizer:
original/current aliases, case variants, duplicates and divergent mirrors;
integer overflow, terminal/unknown patches; missing originals/activities,
command-only disk core and direct activity drift. They assert unchanged core,
sidecar and original bytes. Each has its own sparse filesystem; no expensive
world or mutable fixture is shared. These are UNCOMPILED/UNRUN until the official
activity category executes. The existing positive, guards and real-engine journey
are the next selection; accepted item composition remains a separate open gate.
No new GM syntax is introduced; this repairs consumption of the already documented
nonterminal NPCActivityUpdates command. No live-relay acceptance is claimed.

## Activity component GREEN and next journey fixture refusal

At ac5144cb, fresh integration build passed: 0 errors, 152 warnings, 89.65s.
Official `mortal-living-world-npc-activity-normalization -NoBuild` completed
30/30 PASS (one positive, 29 guards), runner15.454s, no skips/duplicates/timeouts,
complete owned cleanup. This proves the component, not general accepted-item
composition or live GM execution.

Official real-turn journey category then completed 1 PASS/1 FAIL in30.222s,
with complete cleanup. The first rich scout interval now succeeds through the
actual GameEngine; the next turn43 reaches faction_core_changes_target_not_existing.
The scripted author had overwritten the on-disk faction core with a command-only
object, deleting its current canonical target. This is a fixture authoring defect,
not permission to relax the existing target guard. The draft correction preserves
the current faction root and appends its command, as ordinary distribution does.
The correction is UNCOMPILED/UNRUN; later project/news intervals remain unaccepted.

## Third journey interval: completion type defect — confirmed RED

After d36c30a5's faction-author correction, fresh integration build passed with
0 errors, 129 warnings in64.45s. Official journey completed 1 PASS/1 FAIL in39.299s,
no skips/duplicates/timeouts, complete cleanup. The scout and council/project
progress intervals now complete. Turn44 project completion is refused because
completedProjects[0].completionTurn is a string, but canonical validation requires
a nonnegative integer. The existing completion reducer calls GetNodeString on
an integer command; even the earlier publication-only test expected string42.
That helper-level PASS did not qualify the full engine state validator.
Next: sparse typed completion regression, causal RED, preserve nonnegative Int32
without guessed fallback, then rerun the affected lifecycle and journey cases.
No completed four-turn/live-GM journey is claimed.

Independent source review also flagged a possible raw flat-project identity-index
gap; it did not cause this runtime refusal and remains unverified. Do not silently
expand acceptance or weaken existing target checks to avoid investigating it.

## Project completion sparse regression preparation — test-only WIP

One desired-success actual-normalizer Fact and eight invalid-turn cases are added
under mortal-living-world-project-completion-type. The positive requires JSON
Int32, whole tracker equality including sibling/history, command consumption and
bounded canonical tracker validation. Missing/null/string/negative/fraction/
overflow/boolean/object inputs must refuse without tracker or backup mutation.
No production change yet. Build/discovery/runtime are UNRUN; expected causal RED
is string output for the positive and missing refusal for the eight invalid inputs.
Sparse per-test state avoids re-running full GameEngine setup for this type gate. A
separate pure-DATA unit case exercises MortalLivingWorldEvents.Validate: canonical
int7 is accepted, while matching canonical/report string7 must be rejected. It
currently exposes Nonnegative's numeric-string fallback, which masked the domain
integer mismatch in prior publication-only evidence. This additional case is
UNRUN and changes no production behavior; resource Costs semantics are untouched.

## Completion evidence first RED and split runner categories

Fresh integration build0errors129warnings78.31s; unit build0errors54warnings33.28s.
The combined type selection planned10cases but stopped its other descriptor after
unit failed: 1 executed/1FAIL, incomplete9integration cases, runner16.510s,
complete owned cleanup, no timeout or duplicate. The unit failed exactly because
matching string7 produced no lifecycle issue after typed int7 control succeeded.
This is causal evidence RED, not an executed ten-case regression.
Split independent category owners into project-evidence-type (one unit case) and
project-completion-type (nine integration cases) so fail-fast does not obscure
causal outcomes. No tests or source behavior changed; integration RED is next.

## Typed completion correction after complete causal RED — WIP

The separated official project-completion-type run executed all9cases: 0PASS/9FAIL,
11.322s, complete cleanup, no skips/duplicates/timeouts. Eight invalid commands
failed because no exception was thrown; the positive failed at the Int32 assertion
because the output was string. Together with the distinct one-case evidence RED,
these demonstrate both faults without a full-world fixture.

Draft source requires an explicit nonnegative Int32 command completionTurn before
changing the tracker, then retains that numeric value. No parse, fallback turn,
legacy save conversion or guessed completion is added. Living-world project
completeness no longer accepts numeric strings. Existing lifecycle and journey
expected canonical output now use integers, matching the existing domain contract.
No new GM syntax or afterlife system: this corrects the already numeric Mortal
completion command; canonical state validation is not weakened. No cache is useful
for this scalar type check; existing cloned tracker and original reads are reused.
Independent source review accepts the bounded code and five impact categories.
Its documentation findings are corrected: Rule19.F explicitly requires the actual
nonnegative JSON integer completionTurn; E_Block_21 uses integer184; both existing
project-ID fixtures include integer42 without changing their intended identity
negative. A narrow source/fixture guard accompanies these updates. The saved
selection adds the new type categories and reopened engine/activity checks; no
aggregate selection is dispatched. This correction is UNCOMPILED/UNRUN.
Next: fresh builds, separate type gates, existing event/lifecycle consumers, then
the real-engine journey. All acceptance and live-GM gaps remain explicit.

## Typed completion and scripted full-engine journey GREEN — bounded result

Current tested source/docs snapshot is c791a911 (tree755b80bc7d008a8d0194232d7c9ddf2972c49a3c,
verified equal to the complete local index). Fresh builds: production0errors/
19warnings54.39s; integration0errors/129warnings88.23s; unit0errors/40warnings33.38s.
Official NoBuild type categories completed11/11PASS in23.664s:9actual-normalizer
completion cases and2evidence/documentation cases. The numeric-string evidence
case and nine reducer cases have prior causal RED; the added documentation guard
has no prior RED claim.

Official event-evidence, lifecycle-publication and real-turn-journey categories
completed53/53PASS in64.087s:47detached event cases,4real lifecycle publications,
2GameEngine journey tests. All planned descriptors/cases completed, no skips,
duplicates or timeouts, complete owned cleanup. The four-interval rich journey
now actually accepts scout progress, council/project progress, project completion
with threat reduction/news, and a quiet interval through ProcessPlayerTurn;
story/history counts, canonical effects and intervening state reloads pass.
The separate elapsed-no-change actual turn also passes.

This is scripted GM regression evidence, NOT the user's required live relay GM
acceptance. Stronger explicit source-bound receipt/news persistence assertions,
contradictory-package/same-request repair, activity plus ordinary item composition
and the raw flat-project index concern remain open. No merge/closure/full feature
or Windows/afterlife claim. Current complete raw measurements remain outside Git
under /workspace/shared/diagnostics1516; only these compact findings are published.
No speedup claim: scalar type checks are cheap, no new cache/read authority is added;
full GameEngine setup is retained because actual lifecycle interaction is tested.

## Explicit journey persistence assertions — test-only WIP

The retained four-interval engine test now additionally matches every receipt to
its real dispatched session/request/turn and actual prepared manifest hash, exact
authored report and immutable prior receipt bytes-as-DATA. It checks three public
news rows against each authored event, no private receipt/audit fields in those
rows, no news mutation during the final quiet interval, and identical history/news
files across each real state refresh. Captured expected DATA is private per-test;
no owner/authority is manufactured or cached. This extends the same real journey,
not another expensive duplicate fixture. These assertions are UNCOMPILED/UNRUN,
not a new defect or causal RED claim. Same-request repair and live relay remain
separate open gates.

## Historical bounded completion checkpoint, 2026-11-16

The bounded #1516 implementation and selected verification are complete. No
merge, pull-request merge, or issue closure is performed by this assignment.
Runtime/test source is frozen at `acd4cf382ac950156168296d48c39c27b22f7731`,
verified remotely and restored into a separate clean checkout. This final
documentation closeout does not change executable source or test selection.
Earlier WIP/UNRUN/FAIL entries below are chronological evidence, not current status.

The corrected actual joint rollback packet passed 2/2 in 308.477 seconds runner
wall, with complete cleanup and no skips, duplicates or timeout. It now reaches
RetryableHeld and exact restoration checks after real post-commit receipt loss,
as well as reached common-member failure and one owning retry. The observer-only
fix distinguishes forward publication from compensation by actual journal images;
production rollback and validation guards are unchanged.

Final fresh builds: Integration 0 errors / 167 warnings / 109.66 seconds; Unit
0 errors / 54 warnings / 31.83 seconds. Official `-NoBuild -ValidateCatalog`
passed in 13.128 seconds: 1,776 categories and 14,526 methods/files. Official
`-NoBuild -SelectionFile tests/selection.json -PlanOnly` passed in 11.177 seconds:
21 descriptors, 245 planned cases, no selected duplicates. Both metadata runs
executed zero tests and completed owned cleanup.

Retained TRX evidence reconciles all 245 unique selected cases to their latest
passing targeted runs: assessment 57; events 47; receipt DATA 26; original source
20 plus existing NPC-source controls 6; ordinary publication 7; lifecycle 4;
engine repair 5; diagnosis 13; pure QTE 29; QTE runtime 10; news privacy 4; guidance
4; held-lease controls 2; actual delivery 2; joint baseline 1; joint admission 3;
valid publication 1; short publication 1; rollback 2; cold recovery 1. This is
cumulative impact-selected evidence, not a single full-suite or all-245 rerun.
Unchanged successful cases were not repeated merely to repackage selection.

Independent implementation/owner reviews and final requirements/selection reviews
found no unresolved blocking issue. Source-level findings included genuine owner
lease propagation and preseal QTE ordering; fixture omissions and observer
misclassification were corrected without weakening production guards. Performance
uses existing operation-owned immutable DATA, batched QTE reads and the existing
single joint calculation; live lease/source/effect and recovery checks remain
fresh. No new parallel cache or cached publication permission was introduced.
Measured joint costs are not advertised as a speedup.

Boundaries remain explicit: Mortal only; no new simulation AI or forced events,
no old-save migration, no afterlife/spiritual-combat work, no hosted CI, Windows
PowerShell 5.1/live-provider qualification or broad suite. Diagnosis is bounded to
its existing planned T1 no-significant-change route; local QTE refuses a report
or newly crossed autonomous cycle before durable handoff. The two-witness
portable-local rewrite limit in spec/data-model remains visible. Private reports
and history are not public news; this is not completion of #1540 or the closed
not_planned #1368. Raw diagnostics remain local and are not committed.

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

## Current-owner continuation follow-up, 2026-11-16

The next fresh integration build passed (0 errors, 167 warnings, 111.79 s).
The canonical vehicle-context control is now GREEN; QTE completed 7/9 PASS,
with causal offer and Begin failures (no required refusal). Their guards are
being added before capture/handoff, with a separate exact armed-continuation
preseal regression rather than claiming the later shared guard is sufficient.
Engine rollback now passes 4/4, including actual late-critical rollback before
dispatch. A bounded follow-through test is added separately to exercise omitted
report refusal and genuine resubmitted publication; no full accepted-turn result
is inferred from a fixture that cancels after unrelated later validation.

Diagnosis representative reached publication after correcting actual backup
mapping, then failed because retained Compose rechecked its old physical source
lease. The correction readmits source through the genuine current diagnosis
owner and registry, compares exact old opening DATA, and preserves the old lease
fence. This is not recovery permission derived from a serialized receipt.

Joint representative completed one genuine test in 51 seconds, not a timeout.
Its common candidate failed the existing exact carrier-composition equality:
living history had been appended after the typed carrier proof was built. Move
that contribution into the existing single retained calculation before carrier
assembly, binding its identity under the same resource owner. Do not relax the
constructor equality or perform a second full calculation. The lossless gzip
capture closed completely (188,506,330 stored bytes, 1,964,017,508 decoded bytes,
5,456,150 rows); no raw artifact was uploaded. This newly reached failure is not
evidence of a comparable speedup. Fresh GREEN remains pending for these fixes.

## Verified continuation blocks, 2026-11-16

At 479ccb98, integration build passed (0 errors, 167 warnings, 111.22 s),
then engine repair completed 5/5 including the real omitted-report refusal and
repaired publication boundary. Diagnosis compensation/retry representative
passed; the subsequent full selected diagnosis packet passed 13/13 in 104.696 s
runner wall, covering direct completion drift and retained resource-cost
completion. These are bounded noSignificantChange diagnosis results.
Unit build passed (0 errors, 54 warnings, 32.64 s); documentation guards passed
4/4, pure QTE DATA and batched read-count guards 29/29, and actual daemon-render
plus existing worked-example coverage 2/2. All owned cleanup completed with no
duplicates, skips or timeouts. No unchanged broad suites were run.

QTE runtime passed 9/10: offer and Begin now refuse before handoff. The final
reached regression shows that a late malformed report is refused only after
terminal selection persisted its continuation fingerprint/state. The final
correction must check the selected terminal before that first durable mutation,
not rely on rollback from the later shared distributor guard.

Joint now reaches sealed publication and compensates back to RetryableHeld.
The representative completed in 143 seconds, not a timeout. Passive timings
locate failure inside existing canonical item validation, before resource and
final readback validation. Its assertion previously discarded the returned
issues; improve that diagnostic assertion before one further representative
run rather than changing production on speculation. No joint completion GREEN
is claimed. Latest full captures remain local-only.

Bootstrap source inspection found no contract update required: the actual
Mortal opening builds narrative worldEventsLog before authenticated baseline
snapshot and writes turn_request afterward. It is not the bare-maintenance
exception. Unreferenced opening narrative is not retroactively an autonomous
report event; real elapsed cycles during that turn still require assessment.
This is a traced no-update rationale, not a new full bootstrap runtime result.

## Canonical item held-lease finding, 2026-11-16

Final QTE runtime passed 10/10 after the preseal ordering correction; integration
build passed with 0 errors, 167 warnings in 110.49 s. The single diagnostic joint
rerun exposed the exact refusal: canonical item validation tried to acquire
`gm-main-owner.lock` while its genuine caller already held that lease. Its
canonical branch dropped the supplied writeLease at both original-snapshot and
pre-turn-item-catalog reads, unlike the existing raw item/location/resource paths.
The current minimal fix forwards the same actual lease through those two existing
APIs, without a new cache, lock bypass, catch/retry or weaker physical guard.
A narrow category selects the existing actual-held-lease positive test and a
new foreign-lease rejection control before the next genuine joint representative.
Joint completion remains unqualified until that run succeeds.

## Held-lease proof and next reached joint refusal, 2026-11-16

At 83f1aa75 the fresh Integration build passed (0 errors, 167 warnings,
110.51 seconds). The canonical item actual-held-lease positive and foreign-lease
negative both passed, 2/2 with complete cleanup. The genuine joint representative
then reached the next validation boundary and failed in 132.160 seconds, without
a timeout: accepted storage access entries did not match accepted location metadata.
The previously observed nested lock error is absent in this run. This is not a
joint success or a comparable speedup claim. Inspect the fixture's canonical
map/access and authenticated original before another expensive run; preserve the
production projection guard. The entire eight-case joint contract remains pending.

Independent source inspection confirmed the next refusal is a deficient test
baseline: the shared component seed provides accepted map metadata but omits its
client-owned storage-access projection. The correction is confined to the eight
new living-world joint tests: a pre-original wrapper derives the existing
projection, writes it under the actual lease and validates readback. Older joint
fixtures and production guards remain unchanged. A cheap ninth test proves the
real original captures exact map/access bytes before another expensive joint run.
That new test and the corrected joint path are UNRUN at this checkpoint.

Final intended selection replaces the prior #1515 selection; semantic joint
buckets replace the oversized eight-case category. Representative debug gates
are excluded from that selection. Protective budgets are not measured forecasts.
Official final discovery/PlanOnly and runtime joint qualification remain pending.

## Genuine joint publication representative GREEN, 2026-11-16

At 4cda54d7, fresh Integration build passed (0 errors, 167 warnings,
107.32 seconds). The canonical seeded baseline and exact signed-original test
passed 1/1 (test 4.124 s; runner 15.702 s). The unchanged genuine valid joint
representative then passed 1/1 (test 125.432 s; runner 136.155 s), including
common-cohort history publication and both real progression phases. Both runs
completed cleanup without duplicates, skips or timeouts. This fixes a test
baseline, not production storage-access semantics. No comparable speedup is claimed.
The remaining admission, rollback, recovery and short-action cases still require
their own outcomes; one representative does not qualify the full joint contract.

## Remaining joint cases and observer finding, 2026-11-16

The frozen 4cda54d7 build also passed joint admission 3/3 (172.245 s runner),
cold phase recovery 1/1 (131.255 s), short-action publication 1/1 (129.599 s),
and actual daemon/manifest delivery 2/2 (14.850 s). All owned cleanup completed
without duplicate IDs, skips or timeouts. The two-case rollback packet completed
in 309.857 s: reached common-member failure and exact owning retry passed
(test 167.633 s); post-commit receipt disappearance failed at an early observer
count (test 132.785 s). Its drift count was one, but the path-only common-batch
counter also counted the compensation transaction, giving two rather than one.
Independent source review confirms compensation uses the same publication
mechanism and member paths. The minimal test-only correction classifies first
history creation from the actual journal's absent-to-present image transition.
It does not assert that restoration has a present-to-absent image: the injected
fault already removed history. Keep the RetryableHeld and complete before-image
restoration assertions; they were not reached in the failed test and remain
unqualified until the corrected rollback packet passes. Production is unchanged.

## Real ordinary-engine journey follow-up, 2026-11-16 — planned, not yet verified

The user explicitly requested a simulation of the issue's Mortal events rather
than more assertions of completion. Re-reading the live issue confirms meaningful
time, autonomous NPC/faction actions, locations, projects, consequences and public
news must remain coherent; an explicit bounded quiet audit is also legal.
Earlier publication helpers use sparse state. The prior real-engine repair probe
cancels after unrelated whole-state validation and does not prove a completed
player turn. This is an evidence gap, not yet a demonstrated production defect.

Use a copied complete current example session with valid canonical actors and
faction/project/location state. Drive actual ProcessPlayerTurn with deterministic
GM-authored outputs correlated to each real turn_request. Assert final story
append, incremented turn, exact canonical effects and client-owned receipt, then
reload before the next interval. Exercise scout/threat, faction/project progress,
project completion with public news, and a legitimate quiet interval. Include a
contradictory effect and same-request repair without permitting false acceptance.
No real model/provider quality or NLP completeness claim follows from scripted GM
output. No afterlife, UI redesign, legacy player migration or hosted CI is added.

Ruling: complete existing canonical entities may be seeded at initial setup to
focus on autonomous lifecycle, not unrelated birth workflows. Subsequent turns
must use real existing reducers and authority; no resetting receipts, suppressing
validators, resealing authoritative state or reconstructing the world per turn.
New test-only evidence may already pass existing code; do not manufacture RED.
A genuine production defect requires an observed focused reproducer before fix.

### Genuine engine RED and first causal correction (not yet GREEN)

Two official journey attempts after fresh build show a legal elapsed no-change
turn refusing with `mortal_living_world_source_invalid`: prepared schedule changed
before source capture. Root cause: ordinary engine takes rollback backups before
BuildControlForNextTurnAsync prepares schedule, but did not register that prepared
schedule among staging validation originals. Source correctly rejects old/new
bytes. The narrow correction signs prepared SchedulePath while keeping pre-turn
rollback bytes unchanged. A new post-preparation schedule-tamper test preserves the
negative boundary. Neither source byte equality nor restoration is weakened.

Full copied example first required current storage-access projection at initial
setup; it is derived by the actual composer before snapshots. The richer seed
also exposed incomplete faction fixture fields. These setup failures are not
claimed as production defects. The connected journey remains unaccepted and NPC
activity command consumption still needs actual runtime verification.
