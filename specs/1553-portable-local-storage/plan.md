# Implementation Plan: Trusted local storage and cross-platform runtime

**Branch**: `codex/1553-load-filesystem` | **Updated**: 2026-10-04
**Source**: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)
**Requirements**: [spec.md](spec.md) | **Work**: [tasks.md](tasks.md) | **Decisions**: [research.md](research.md) | **Reproduction**: [quickstart.md](quickstart.md)

## T032-B4 browser RED source checkpoint — 2026-10-04

Base/ref verified `49b1b3649881d2f44322ecef14220e61686c42ae`, clean tree
`8dc8b639790c9efe76dbbe93e2f6ded3933b0322`. Spec Kit prerequisite and
spec/plan/tasks consistency check confirms the approved B4 browser slice. Initial
causal tests exercise real menu load decisions, late pending/token changes, required
menu/service refresh and stale generation; implementation and execution are pending.

The first RED carrier `5dde11f8` was fully restored into a new GitHub checkout:
4,806 physical blobs matched its exact tree and the checkout was clean. PlanOnly
then failed before build/discovery: the new category was accidentally nested in the
last category's selectors. This is a catalog authoring failure, not product RED;
fixed by appending at the actual top-level categories boundary. No tests ran.

Ruling: add one generation-bound required refresh bundle rather than tag independent
partial DTOs. Its menu/session/game/settings/audio are built inside the exact committed
SessionOperationContext and finalization fence, with no partial bundle on failure.
This avoids archive-list inference and does not change ordinary optional refresh.
Retain four typed outcomes and committed identity on all HTTP statuses. Revalidate
pending state and the exact pre-acquired UI lease in loader admission under the held
replacement lease. Both actual frontend handlers will use shell-owned dispatch and
continuation ownership; unmount/navigation/lost response cannot clear a required stop.

No new game/GM-authored capability or schema is added; prompts/examples need no change.
New browser-only owners, causal RED/GREEN, required frontend consumers, discovery-only
catalog audit, separate Sol 6.1 XHigh review and GitHub restoration remain pending.
T031-WORKER-PORTABLE, A4 native save-producer names, live console/GM, Windows public
clients and full #1553 remain open. No live browser attempt is part of this work.

### Browser first RED and typed transport candidate

At `67212bd9` the fresh selected build/discovery planned 9 cases in 1:48.036;
actual run completed 9/9 in 9.511 seconds, 0 passed/9 failed, no skips/duplicates,
owned cleanup complete. Seven missing-disposition failures are causal transport RED.
Two cut rows accidentally interrupted initial UI-lock publication and are fixture
failures, not load RED; the observer is now armed only after real load preparation.
Their corrected outcomes are unverified. Evidence: `recovery/evidence/load-browser-red`.

Candidate preserves typed load decisions through coordinator/menu/HTTP, repeats
pending/token admission under the actual replacement lease, and uses the exact
committed generation for required menu refresh. Required bundle endpoint tests are
written next and still RED/unimplemented. Candidate build/runtime and independent
review remain pending; frontend handlers are unchanged and B4 remains open.

### Browser handler RED source and runner invocation correction

Typed backend candidate saved at `9d9f5c72`. Its first combined invocation used
PowerShell `-File` from bash with a comma-separated argument; the runner correctly
rejected that single unknown category before any workload. Use `pwsh -Command`
with an explicit PowerShell array for multiple IDs. This is an invocation failure,
not a runtime test result. Actual GameLauncher/SettingsView handler tests now cover
17 decision/duplicate/unmount/navigation/lost-response cases before changing frontend
production. Their isolated harness was extended, not shared across cases. No new
frontend behavior, required bundle implementation or acceptance is claimed yet.

### Browser connected candidate — 2026-10-04 17:01 UTC

At `e681ff23`, selected C# build passed; combined runtime stopped after first owner:
9/9 executed, 6 passed/3 failed (3:04.288 including build), later descriptors unrun.
Two cut rows still observed the admission heartbeat's v2 write; corrected to recognize
actual BOELP3 intent before cutting replacement. The token fixture read BOM bytes via
JsonNode.Parse; it now uses BOM-aware text. These fixture corrections are not counted
as previously demonstrated loader defects. Native frontend RED separately exercised
17 actual handler assertions, all failed as intended (duplicate dispatch 2 versus 1,
missing retained stop, unsafe navigation and collapsed copy). Its file adapter reports
0/1 completed file cases on failure; raw Vitest 17/17 failures are retained distinctly.
The separate HTTP cohort executed 7/7: three existing consumers passed, four new bundle
requests failed with the expected absent-endpoint response. Cleanup complete, no skips.
Evidence is under load-browser-first-candidate, load-browser-handlers-red and
load-browser-bundle-red; overlapping results are not summed.

Candidate now implements the generation-bound required bundle and both actual frontend
handlers, synchronous shell-owned admission, shared refresh invalidation, navigation
ownership and a retained shell-wide stop. Known committed identity survives failure or
unmount. Unknown replies cannot enter current reconciliation. Explicit safe NotLoaded
can capture existing current generation without creation, then require the same complete
bound bundle. Recognized no-active-chapter absence is explicit, accepted only for known
non-loading/restored state; committed navigation requires a playable game surface. All
other required read failures discard the bundle. This is an approved-scope engineering
ruling, preserving prior absence rather than accepting arbitrary partial refresh.

Added absent-generation browser admission tests are still unverified and may require a
narrow non-creating guard correction. Required bundle finalization/rotation coverage,
frontend type/build/affected consumers, discovery and separate Sol 6.1 XHigh review are
pending. New current-source tests, source guards and exact evidence remain mandatory;
no B4/browser acceptance is claimed by this WIP checkpoint.

### Browser independent review corrections — WIP, 2026-10-04 17:12 UTC

Separate **gpt-6.1-sol / xhigh** review of `49b1b364..0491944` identified:
1. Existing UI lock helpers create absent generation during initial admission,
   revalidation and release. Fresh native tests executed 11/11: prior 9 passed and
   both new absence cases failed causally. Candidate now guards all three boundaries
   using ReadExistingSessionGeneration on held leases; current corrected C# is unrun.
2. A pre-load command/action response arriving after healthy load finish could publish
   old UI or send pendingGmAction to the new session. Three actual-provider cases failed
   causally. Candidate now retains an operation epoch through every response/catch/
   finally, invalidates it at load admission and clears old command presentation.
3. Required menu options could read cached pre-reconciliation settings. A new actual
   HTTP cached-settings assertion is written; its causal run and ordering correction
   are pending. Additional actual read/final-close rotation, tagged absence, missing
   authority and game-read failure cases were added with isolated host filesystem hooks.

At `0491944`, bundle/affected HTTP independently passed 7/7 in 15.503 seconds.
Initial frontend run passed its type and save consumers, then raw browser Vitest
30/33 (three provider fixtures lacked theme). Corrected provider fixtures and expanded
ownership coverage subsequently passed 42 browser assertions; selected unchanged
settings consumers also passed, for 69 completed assertions/file cases before the
next Node source-guard file failed two obsolete implementation-string expectations
(20/22 raw scenarios). Those guards now assert the new common handler ordering and
combined save/load continuation fence without dropping their prior requirement.
The later settings-reconciliation file was unrun in that partial command. Production
frontend typecheck/build completed in preparation. Exact overlaps are kept separately
in load-browser-frontend-candidate, load-browser-absence-red, load-browser-epoch-red,
load-browser-bundle-candidate and load-browser-frontend-consumers-partial.

Four deferred-refresh handler fixtures initially retained a stale test callback; they
now rerender after injecting the controlled refresh and pass. This is harness evidence,
not a production regression. Independent review remains open; no accepted browser/B4
claim. Publication incident evidence distinguishes returned cancellation text from the
owner's reported intent and from generic security flags; no bypass or reconnect occurred.

### Browser review candidate results and follow-up visibility — 2026-10-04 17:25 UTC

Saved review carrier `fd9ff1af` is verified remotely at tree `1ec4c52d`.
Native combined run completed25/25 in3:00.054: browser owner11/11 passed, including
both absence regressions; HTTP14 completed6passed/8failed. Seven new HTTP rows failed
before dispatch because the fixture read lowercase generationId from a Pascal-case
bootstrap document; the old same-owner-token fixture lacked explicit initial browser
authority. Neither failure is product RED. Fixtures now read admitted existing authority
and explicitly bootstrap the old-token root. The cached-options ordering remains
intentionally unfixed until that corrected causal HTTP test runs. Prior unchanged
browser11 evidence is not repeated for bundle-only changes.

Both actual frontend handlers reproduced lost nonblocking follow-up visibility:
raw44 cases42passed/2causal failures. Shell-owned follow-up retention and truthful
message are now implemented independently of the blocking latch. A confirmed identity
and unresolved cleanup obligation survive navigation while ordinary commands remain
allowed. A later healthy load does not erase earlier debt; its banner explicitly
identifies that historical load, never claiming it is the current loaded archive.
A same-shell successive-outcome and actual rendered status/alert assertion cover it.

The corrected frontend selection passed **62/62** in12.757seconds:45 actual load
handler/provider/refresh/render assertions,8 existing save assertions,5 settings-load
assertions and4 Node/typecheck file cases; the Node settings file also records22/22
internal scenarios. All6 descriptors completed, no skips/duplicates, cleanup complete;
production frontend typecheck/build passed. The final historical-label refinement is
still awaiting its narrow rerun. Existing separate settings consumer27 evidence remains
unchanged. Stored evidence: load-browser-review-candidate, load-browser-followup-red,
load-browser-frontend-green. These overlapping cohorts are not summed.

Independent Sol6.1XHigh review confirms the authority/epoch/old-command-view corrections
by source inspection and validated the evidence meanings. Final corrected HTTP tests,
cache ordering fix, discovery audit, final readback and review verdict remain open.

### Browser cached-settings causal result — 2026-10-04 17:33 UTC

Clean saved `8e99d677` passed the final actual browser handler/provider/refresh/render
cohort45/45 in2.024seconds. It includes truthful historical follow-up identity after
later healthy replacement, and no blocked continuation for that nonblocking debt.
The corrected actual HTTP cohort completed14/14 in2:27.352 including fresh build:
13passed and exactly the cached-settings case failed (expected musicEnabled=false,
menu reported true). Read/final-close rotation, explicit no-active absence, missing
authority/non-creation, mixed request, game-read failure and existing consumers passed.
This is causal product RED; no fixture failures remain in that run.

The narrow correction now builds required settings and audio before menu options,
inside the unchanged exact-generation binding/final-close fence. Final14-case GREEN,
production frontend rebuild, inventory audit and reviewer acceptance are pending.
Evidence: load-browser-handlers-final and load-browser-cache-red. Unchanged native
browser11, previous save/settings consumers and filesystem/resource/console cohorts
are not repeated solely for this required-bundle ordering change.

## Current verification scope — owner decision, 2026-10-04 15:45 UTC

Browser verification is now exclusively test-based at the code level, including
actual handlers/components and backend integration. Live runs use the console client.
This supersedes older live-browser/deferred-access entries below; do not add a cloud
browser, alternate hostname/tunnel or later browser visual run as a gate. Automated
checks are not described as visual QA. Live console/real GM, full gameplay/restart,
applicable Windows qualification and all other unchanged requirements remain open.
The latest saved source/checkpoint remains the starting point; no runtime code was
changed for this verification-scope decision.

## Accepted T032-B4 console integration — 2026-10-04

Source `e813ad8438a25d7882810a241bd51d0be9ad3f42` passed **16/16** native Linux
`portable-load-console` cases in **2:27.714** including a fresh selected build.
One descriptor and all 16 planned rows completed, with zero skips/duplicates and
complete owned-tree/runtime cleanup. Actual in-game options → Spectre save selection
→ required settings/UI refresh succeeds; committed refresh cuts retain truthful text
and stop the existing loop. Exact established-generation fencing, late pending/UI
owner refusal, absence-preserving refusal/rollback, all four decisions, old runtime
consumers, blocked menu keys and repeated-load suppression are covered.

The production correction is limited to typed console orchestration, optional client
admission on the loader's actual replacement lease, non-creating lock inspection and
safe loop/menu routing. No public filesystem/resource algorithm changed. Historical
RED, first-candidate, absent-authority, harness-build and redirected-surface failures
remain in source-specific evidence directories below. Their overlapping cases are
not added together or presented as a combined pass. Native Linux telemetry opt-outs,
PowerShell 7.6.6, SDK 10.0.401, runtime 8.0.31 and DOTNET_PROCESSOR_COUNT=1 remain as recorded.

[GREEN evidence](recovery/evidence/load-console-green/summary.json) and
[fresh full GitHub restoration](recovery/load-console-github-readback.json) are retained.
Separate **gpt-6.1-sol / xhigh** final review **PASS** at
`68b24ae660dfaeb251ed3728816ea789bd6f4289` found no remaining actionable correctness,
selection or missing-contract issue. The reviewer independently verified the actual
GREEN artifacts and fresh 4,805-file evidence-carrier restoration, including 23 evidence
JSON, two qualification/readback JSON and five TRX files. The absent-authority defect
and required-menu coverage gap are closed. This acceptance/scope-only carrier is
read back separately before handoff; no runtime/test/catalog/selection changes follow
its tested source. Fresh discovery-only audit
at `4f916e10` validated **224 categories / 10,573 method-or-file identities**, no
unmapped or stale selectors, in 3:03.147. Zero tests executed during that audit;
this does not add runtime coverage. All four freshly built project XML files parse,
and changed source files have no XML compiler warning. Operational continuation/
restart/no-blind-retry guidance is now in quickstart.md.
This remains client-owned; GM-authored fields/mechanics/prompts/examples are unchanged.

Next bounded block is browser typed load. Existing required browser refresh DTOs expose
no replacement generation, and loadBrowserState swallows partial failures. Use an
exact-generation-bound required refresh bundle (or equivalently proven per-surface
bindings), preserve typed result/identity on every HTTP status, revalidate the acquired
UI lease token plus pending state under LoadGameWithAdmissionAsync's held lease, and
latch unsafe continuation shell-wide even after unmount or newer navigation. Do not
use archive-list presence as load confirmation. Browser automated integration, live console/GM, native Windows
public-client, T031-WORKER-PORTABLE, A4 producer case-pair roundtrip and whole #1553 remain open.

## T032-B4 console integration WIP — 2026-10-04

Base and GitHub ref verified clean at `6c8b91d5bbf513a8896cce5d79661a2f9548137d`.
Spec/plan/tasks consistency check preserves the approved B4 revision: first console,
then browser transport and handlers. Console tests precede implementation: actual
four-state result, required service/console refresh failures retaining commit,
exact established-generation binding, late pending/UI-owner admission, existing
loop and menu refusal and repeat-load suppression after blocked continuation.
Three existing runtime consumers remain selected. Category `portable-load-console`
owns this bounded change; unchanged filesystem/resource cohorts are not repeated.

This RED source checkpoint has no implementation or test-execution claim. PlanOnly,
causal RED/GREEN, discovery and independent Sol 6.1 XHigh review remain pending.
The console will retain blocked evidence for the process and require restart for
reconciliation; no automatic load retry. Required generation-bound post-load UI
refresh cannot rewrite commit. Existing new-game rebind consumers retain their
current capture behavior. This is client-owned; GM-authored schema/mechanics and
examples need no change. Browser, native Windows public-client and live clients/GM,
worker portability, A4 native producer names and full #1553 remain open.

### Console causal RED and candidate

Published RED source `f5cd839e26571ad0d43de5980d09f2ffbddb3028` was restored
from GitHub into a new clean checkout; all 4,767 physical blob hashes matched tree
`64badb92098eb803ed92f2bac2c12d0418d2f0f8`. Fresh PlanOnly build completed in
2:17.309, nine planned/zero executed. Actual RED executed 9/9 in 7.992 seconds:
seven intended failures and two existing runtime-rebind controls passed, no skips,
duplicates or cleanup debt. Five causal failures expose late pending/owner mutation,
wrong generation adoption, uncaught console refresh and continued old loop after
service refresh failure; two typed-return assertions are contract-scaffold failures.
Evidence: recovery/evidence/load-console-plan and load-console-red.

Candidate uses the typed loader with held-replacement-lease admission, exact
established-generation rebind and preserved commit through required console refresh.
An unresolved result latches the process closed, stops existing/new game loops and
removes mutating main-menu choices until restart reconciliation. Successful in-game
load resumes its existing loop, avoiding nested loops. GREEN, extra rollback/uncertain
and required late-UI tests, discovery and independent review remain pending.

Publication incident chronology (UTC): first fetch_commit schema error 02:44:38
corrected to documented required argument names; no state change. Large catalog
create_blob awaited user approval and completed with exact local hash at15:03:17.
create_tree returned `user cancelled MCP tool call` at15:03:38; stopped further
implementation and reported exact operation. User explicitly approved retry; the
single same-call retry succeeded at15:05:15, followed by non-force ref publication.
Readback verifier initially mishandled Git-quoted Unicode paths at15:06:29; using
NUL-delimited ls-tree corrected the verifier and all hashes passed. No generic
cybersecurity flag was observed or inferred; no bypass/probe was attempted.

### Console first candidate verification and review correction

Candidate `57967624f35a14150be4b074b7106e6424c6449d` restored clean from GitHub:
4,775 tracked blob hashes match tree `73d33495c724c97fd4b9c5d74ddaf7590ab61069`.
Fresh native Linux selected build/run completed 12/12 cases in2:28.935,11passed,
1failed, zero skips/duplicates and complete cleanup. The new late required UI-refresh
test deliberately checks that its configured mutation cut was actually reached;
it was not. This case is not qualified; preserve the failure and inspect the actual
retained exception before changing its fixture. All existing rebind controls and
new generation/admission/commit/rollback/uncertainty cases passed individually.

Independent Sol6.1XHigh identified a real admission defect: existing lock InspectAsync
creates a generation even when absent. New test-first malformed-owner refusal and
publication rollback cases assert preservation of prior generation absence. These
cases and an enriched UI-refresh diagnostic are now added before the correction;
review and overall GREEN remain open. No filesystem/resource cohort is repeated.

### Console absent-authority RED and bounded correction

At published `e2f9b33b3487dd5fa29651860d9e2603f7ffc73b`, fresh selected run
executed 14/14 in 2:39.325: 11 passed and 3 failed, no skips or duplicates, cleanup
complete. The two new failures causally hit the unwanted generation-creation
publication before the load itself. The retained UI exception explicitly confirms
`config.json is absent` in ReadLocalSettingsAsync; no injection was reached.
Evidence is retained in recovery/evidence/load-console-absence-red.

Correction adds a dedicated non-creating lock inspection only for replacement
admission, leaving ordinary lock acquisition/refresh unchanged. The late-settings
fixture now uses real local bootstrap before producing its archive. Additional
actual in-game options → load-menu cases use an independent full current session,
real Spectre selection and required refresh; they assert commitment text, private
error redaction, resumed versus stopped existing loop, and no nested loop input.
Blocked menu keys are asserted exactly. New source remains WIP until GREEN and
focused independent re-review; healthy full-stage proof is not assumed from the
minimal load fixture. Browser and all previously listed open gates remain open.

### Console menu test harness build correction

Checkpoint `ca86ad589900c2ac22b34ee836292d71ff4afcfe` restored all 4,785 files
cleanly at tree `38ef46040eddee9fca4067f215bd0f5116d52405`. Its selected build
stopped after 1:15.986 with CS0535: the new counting test input omitted required
IConsoleInputSource.AssertCompleted. Zero tests were planned/executed; this is a
harness build failure, not GREEN. Add the missing assertion method and retain the
full failed build log in recovery/evidence/load-console-menu-build-failure. Runtime
code is unchanged by this correction. Separate earlier failures remain retained.

### Console redirected-surface fixture correction

At `f10b72a4eb897ff4e688ba4e3560477391974816`, 16/16 cases executed in 2:25.491:
14 passed, two actual menu cases failed in Spectre LiveRenderable.RemoveRange before
load dispatch. All absence/required-settings cases now passed, including the actual
configured mutation cut. A minimal reflection diagnostic of the shipped Spectre
assembly and identical StringWriter output established Width=80, Height=-1 in this
redirected Linux executor. The interactive test surface now explicitly owns a
120×40 profile; production rendering and validation are unchanged. Failed16-case
evidence is retained at recovery/evidence/load-console-menu-surface-failure.
Healthy menu/required-refresh acceptance still awaits a complete run and review.

## Accepted T032-B4 public entry — 2026-10-04

Separate **gpt-6.1-sol / xhigh** final review passed the bounded public API through
`59f1317952106150275bb2815ba6fc3ab247822b`, with no actionable public-entry defect.
Public `LoadGameWithOutcomeAsync` exposes the established four-state result; the
old bool wrapper delegates to it and means commitment only, even when postcommit
follow-up blocks continuation. It never authorizes retry or continuation. The active
original physical loader body is removed. Exact superseded tests are retired with
replacement proof; profile conflict and source preflight guards are migrated.

Evidence remains source-specific: public entry **6/6** at `d0a8be96`, six distinct
direct consumers passing within its retained partial 7/13 command, and final
**223-category/10,565-identity discovery**, zero execution, at `b72d07b7`. The separate
worker diagnostic is **0/1**, before Load dispatch in unchanged runtime code; earlier
initiating failure is unknown beneath the observed kernel32 cleanup exception.
**T031-WORKER-PORTABLE remains open**, with test and evidence retained. There is no
combined 13/13 pass, worker acceptance, new Windows execution or full B4 closure.

Final review also found three generated TRX trailing-whitespace lines. Only stored
TRX whitespace is normalized in this carrier; its stored length/hash is updated and
original bytes/hash remain preserved. No runtime, tests, catalog or selection changes
follow the reviewed source. Dates and metrics in earlier WIP notes retain
their historical meanings. [Fresh GitHub restoration](recovery/load-public-entry-github-readback.json) of
`59f1317952106150275bb2815ba6fc3ab247822b` verified all 4,765 physical tracked-file blob hashes,
clean checkout/connectivity, 25 public-evidence JSON and 6 TRX parses. No build
or test was repeated. This final acceptance/whitespace carrier is separately read
back from GitHub before handoff; neither restoration qualifies live gameplay.

Next: actual console and browser typed consumers. Bind console required runtime
refresh to the established generation and preserve commit on late failure; stop
unsafe same-process continuation. Recheck pending-turn/UI-owner admission under the
held replacement lease before mutation, retaining exact owner token release and
uncertain evidence. The existing browser replacement coordinator currently converts
all outcomes to success/failure; preserve typed disposition/source ID/generation,
follow-up and continuation flags through its DTO and every HTTP status. Both
GameLauncher and SettingsView must handle repeat dispatch, unmount/newer navigation,
lost response and required-refresh failure without automatic retry or erased commit.
The existing B4 spec is authoritative; refine this source-backed approach in the
same plan as needed. No repeated unchanged filesystem/resource cohort. A4 producer
case pairs, actual live clients/GM, full T032/B4/B5 and #1553 stay open.

## T032-B4 public typed entry WIP — 2026-10-04

Base/remote verified clean: `7deb7c3e9fd5262d0750f3abefddc57d6e941892`, branch
`codex/1553-load-filesystem`. Current constitution/spec/tasks and Spec Kit prerequisite
resolution checked. The existing execution plan is extended by the spec's B4
revision; no second implementation plan is introduced. Sole-writer ownership retained.

First bounded result: public typed contract plus commitment-only old bool wrapper,
removing its active physical Windows-only loader. The wrapper must return true after
a confirmed commit even if required refresh failed, so false never disguises commit
as rollback. Player-facing callers will use typed truth in the subsequent console
and browser blocks; B4 stays open until those cuts are implemented and reviewed.

Test-first source: six public-entry cases in a dedicated category, current producer
and isolated mutable fixtures. Tests cover commit/late refresh, exact rollback,
retained uncertain journal, rejected input and actual public API visibility. Runtime
code is unchanged in this RED checkpoint. PlanOnly, causal RED, GREEN, discovery,
independent review and later clients remain unrun. No prior resource/native cohort
will be replayed for a public wrapper-only change. No GM-authored schema/mechanic
changes; operational guidance will describe the completed client cutover.

### Public-entry causal RED and candidate

At published `44a3eb6911556bbe0c71fbf466cafc1cd099712d`, fresh selected build and
PlanOnly completed in 2:23.151 (six planned, zero executed). Actual six-case RED
completed in 6.278 seconds: five intended failures and one rejected-input control
pass, no skips or duplicates, all selection and owned cleanup complete. The original
public Linux load cannot reach portable commit/rollback/uncertain cuts; the typed API
is internal. Source-specific evidence is retained in `load-public-entry-plan` and
`load-public-entry-red` under recovery/evidence. Reflection visibility is scaffold
proof, separately from four actual behavior failures.

Candidate removes the active physical loader body, delegates its bool entry to the
single typed implementation and exposes the existing result/operation publicly.
Bool true means confirmed commit, including blocked follow-up; false says nothing
about retry safety. No typed orchestration, journal/recovery/settings/runtime ordering
changes. GREEN, catalog audit and separate Sol 6.1 XHigh review remain pending.
The still-unconverted player-facing consumers are the next B4 block, not acceptance.

### Public-entry review correction and evidence mapping

First candidate at `5645c6e1` completed six cases in 2:19.009 including fresh build:
five passed and one fixture assertion failed. The rollback fault used IOException;
the established transient retry loop correctly repeated it twenty times. Use the
existing deterministic InvalidOperationException cut instead. The failed result is
retained in `load-public-entry-fixture-failure`; no production retry rule is changed.

Independent Sol 6.1 XHigh review found active tests asserting removed physical-loader
behavior. Retire these nine SaveLoadServiceTests methods, retaining original evidence
and direct original-handler recovery coverage:

- LateStagingHardLinkFailsBeforeLifecycleAndPreservesLiveSession,
  StagingReplacementAtMoveBoundaryIsBlockedAndPreservesExactLiveSession,
  PostMoveHardLinkRestoresExactLiveSession, PostPublicationHardLinkBeforeActivationRestoresExactLiveSession,
  PostPublicationUnmanifestedConfigBeforeActivationRestoresExactLiveSession and
  LinkAddedAfterArchiveInitialValidationPreservesLiveSession (all prefixed LoadGameAsync_).
  Retained-inode/anti-owner/directory-move guarantees are superseded; actual confinement
  and permitted hard-link preservation are covered by NativeDirectoryLinkBoundaryRefusesBeforeSessionPublication
  and NativeHardLinkedMarkerReplacementPreservesOutsideSessionAlias. Exact namespace
  rollback and source/library/generation/runtime are covered by LaterMemberFailureRestoresCompleteSessionSourceLibraryAndGeneration,
  InterruptedTopologyRestoresExactBeforeNamespace and FreshGenerationInterruptionRestoresExactAbsence.
- LoadGameAsync_WhenCommitJournalWriteFails_RestoresDiskAndRuntimeSnapshot,
  LoadGameAsync_WhenRollbackMoveFails_PreservesBackupForStartupRecovery and
  UnresolvedLoadRollback_FencesCanonicalWritersUntilRecoverySucceeds create their
  faults solely through the removed ordinary loader. The unused test-only ILoadTransactionOperations
  adapter is removed. Current authority is covered by PriorJournalConflictDuringAcquisitionIsUncertainAndBlocksContinuation,
  ConflictAfterGenerationPublicationDoesNotClaimAnEstablishedGeneration, NativeExactStageJunctionRetainsIntentUntilRemovedAndFreshlyRecovered,
  FreshAcquisitionRecoversActualLoadWithoutExtractionSources and InterruptedTopologyRecoveryConvergesWithoutExtraction.
  ConfirmedCommitRetainsIdentityWhenRequiredRefreshFails and CommittedUnknownEmptyDirectoryDebtPreservesDecisionUntilFreshCleanup
  preserve postcommit truth. Those accepted source-specific runs are not replayed.

The old ProfileRepairRequiresExactPublishedFileAuthority is replaced by a real
pre-publication foreign-live-byte conflict test, with zero B1 publication, unchanged
other files/generation/runtime and preservation of the foreign edit. Its positive
profile repair remains. The existing preflight source guard now bounds checks to
actual PrepareLoadArchiveAsync and ReadSaveMetadataStreamAsync rather than searching
past a method into unrelated code; explicit original-handler helper checks remain.

`portable-save-original-load` becomes `portable-load-public-consumers` (seven exact
cases: profile pair, two leases, bound stale writer, real worker-decision/load ordering
and source guard). Historical 0/2 diagnostic bytes remain immutable and are not
relabeled successful. New consumer runtime and discovery remain pending; no Linux
worker-backend success is presumed. Public type docs also describe the pre-preparation
thrown stale/closing ambient fence. Full B4 and A4 producer work remain open.

### Public-entry GREEN; separate worker pre-load diagnostic WIP

At `d0a8be96`, fresh two-project PlanOnly built/discovered 13 cases in 2:41.363,
executing zero. Runtime completed only seven cases before fail-fast: four integration
consumers plus two unit controls passed; worker ordering timed out at line 365 while
waiting for worker validation, **before Load dispatch at line 367**. Six public-entry
cases were unrun in that partial command. Complete cleanup, exit 1, 19.123 seconds;
retain `load-public-consumers-plan` and `load-public-consumers-partial` unchanged.
A separate command then ran the missing public-entry owner: **6/6 passed**, no skips
or duplicates, complete selection/cleanup, exit 0, 6.943 seconds. This is separate
source-specific GREEN, not a claimed 13/13 run.

Split only the worker method into `portable-load-worker-ordering` (two-minute budget)
to inspect its actual early result without repeating six passing unrelated consumers.
A Task.WhenAny diagnostic now exposes completed worker failure before the validation
barrier; all original positive assertions remain. Do not guess a platform cause or
skip the body. The revised six-case public-consumer owner excludes this explicitly
retained separate precondition. No worker production change or filesystem/resource
rerun is authorized by this diagnostic; full worker/platform gates remain open.

### Public-entry verification complete; independent final review pending

At `b72d07b7`, the one-case worker diagnostic failed before load in 2:07.274 including
fresh unit build, zero skips and complete cleanup. It exposes DllNotFoundException
for kernel32.dll from PhysicalFileAuthority.TryDeleteEntry → TryDeleteDirectoryTree
→ FileSystemManager.DeleteRuntimeDirectory → BeginWorkerApplyTransactionAsync.
The observed cleanup exception does not establish the earlier initiating failure.
These production files are byte-identical to `7deb7c3e`; no load dispatch is reached.
Preserve `load-public-worker-blocker` and track **T031-WORKER-PORTABLE** explicitly;
never convert it to PASS or delete its positive test. It blocks full worker/game
qualification, while the bounded public-entry result is separately reviewed.

Final discovery-only audit at `b72d07b7`: **223 categories / 10,565 methods-files**,
zero tests executed, exit 0, 6.762 seconds, complete cleanup. Parsed generated XML
assemblies and exact operation/error classifications are in
`load-public-entry-audit/operation-incidents.json`; no generic cybersecurity flag
was observed. Existing compiler/analyzer warnings remain reported in build logs;
the new public-entry test has an xUnit2031 style warning (filtered Assert.Single),
not an XML warning or runtime failure. No source/guard/resource failure is hidden.

The public code has one complete six-case GREEN plus six distinct passing consumers
inside the explicitly retained partial run, not a 13/13 command. Separate Sol 6.1
XHigh source/correction/evidence review finds no remaining public-entry defect and
agrees that unchanged pre-load worker cleanup is a separate unmet prerequisite;
final audit/evidence readback is pending. T032-B4 remains open: actual console and
browser still use the bool compatibility path and must switch to typed outcomes
before any player-facing B4 acceptance. No live client/GM or new Windows claim.

## Accepted native filesystem handback — 2026-10-04

All runtime/resource/audit proof is published at
`f72830d5f66a3131ab0bb7c77c82280856439357` / tree `a2a7c94323faccc86922e8a395046f90a07e08be`.
[Fresh full GitHub restoration](recovery/load-filesystem-linux-github-readback.json)
verified 4,727 physical tracked files against exact Git blobs, clean checkout and
connectivity, all 123 changed files since 07d354e1, 71 JSON and 37 TRX parses. No build or
test was repeated during restoration. This proof names f72830d5; the subsequent
status/handoff carrier is separately verified after final independent review.

Separate **gpt-6.1-sol / xhigh** independently verified all twelve current resource
cases and 28 raw child reports, exact metrics/TRX/artifact hashes, 221/10570 discovery and
five actual XML assemblies. It explicitly approved the requirement map below,
and final frozen documentation/readback review passed at
`6133bfed117d1cbc0d28d5a7e0ea6146b081e8e2` with no actionable findings. The reviewer
independently reproduced the fresh restoration and explicitly approved scoped
T032-B1/B2/B3 and T032-B5-FS closure through this exact requirement map. Those four
flags are now reconciled. This acceptance-only carrier changes no compile input;
its final remote SHA and physical Git-blob readback are checked before handback.

| Generic task | Exact accepted requirement evidence |
| --- | --- |
| T032-B1 | FIX entry 9, aliases 24, admission 7, outcomes 4 and leases 9; METADATA 37 + transport 2; native-name 8. Actual internal entry, complete source/library/config preservation, detached profile/settings, same-root binding refusal, original-handler fences and exact typed generation outcomes. See `load-linux-initial-green-20261003`, `load-linux-native-names-green-20261003`, `load-linux-metadata-green-20261003` and returned Windows proof. |
| T032-B2 | Namespace 33, generation 2, connected topology 13, native 6, cleanup 4 and cold structural 10 in `load-linux-admission-consumers-green-20261003`; retained v1/v2 dispatch/publication/generation/original fixtures. One v3 decision supports both conversions, exact absence/bytes/directories, protected boundaries, whole-inventory preflight, generation-last rollback and unknown-evidence retention. |
| T032-B3 | Cold decision 19 + structural 10 and five separately bounded resource owners 12 cases / 28 children in `load-filesystem-linux-qualification.json`; actual cuts and journal-only fresh recovery, 64/128/near-512 MiB, 8192 entries / 2096128 name bytes / 9216 old files, exact state/source/library/generation and owned cleanup. |

The save producer pair limitation is explicitly tracked as open **T032-A4-NATIVE-NAMES**.
The reviewer confirmed it is outside LOAD-FS-001–008's internal load boundary;
valid independently authored manifested pairs and current-producer legal envelopes
are both proven. This does not assert a complete save/load round trip for that pair.
Public B4, full B5/T033/full T032 and whole #1553 remain open. No merge, force-push,
branch deletion or issue closure has occurred.

## Native Linux filesystem runtime proof complete — accepted, 2026-10-04

Production closure `2da4d545`; corrected test-probe closure `eed0ca99`.
All required native Linux cohorts and five separate current-probe resource commands
are GREEN. [Qualification](recovery/load-filesystem-linux-qualification.json) links
source-specific evidence: initial baseline 53 with nine unchanged lease consumers,
corrected original-name 52 (overlapping 44 baseline rows), metadata 39, path/batch 46,
namespace/native/cold/v1/v2 consumers 128, sampler 10. Do not add overlapping totals.

Current resource proof is **12/12 cases and 28 measured children**, not an aggregate
run: preparation 4 at 1:35.334; bulk publication 3 at 1:25.260; maximum-inventory publication 1 at
1:02.958; bulk journal-only recovery 3 at 1:28.207; maximum-inventory committed recovery 1 at
1:06.617. All descriptors, required samples, independent full-state/protected bytes,
owned exits and cleanup completed under unchanged heap/RSS/disk/child/category bounds.
Earlier source-specific passes, the real inventory non-pass and both causal RED
sequences remain separately retained. No source/Windows pass is relabelled Linux.

The [final discovery/XML audit](recovery/evidence/load-linux-final-audit-20261004)
validated **221 categories / 10,570 methods or files**, executing zero tests, in
6.961 seconds. Five actual generated XML assemblies parse; changed files have no
XML compiler warnings. Exact environment and [operation incidents](recovery/evidence/load-linux-final-audit-20261004/operation-incidents.json)
separate approval/network interruptions, unavailable terminal Git authentication,
unconfirmed object writes and ordinary test failures. No generic cybersecurity flag
was observed during these runs, and no screening-trigger probe was performed.

The small future Linux workflow selection now includes 71 cases (61 entry/identity/lease
plus 10 native sampler controls); the actual proof is the separately recorded union,
not a new 71-case execution. Current `tests/selection.json` owns the narrow sampler
correction; resource envelopes remain separate explicit category commands.
The returned native Windows proof remains bound to its original source. Independent
source review confirms the Linux-only name change and redundant Windows lookup guards;
ordinary successful cached sampler paths retain their prior behavior. No new Windows
runtime execution is claimed. Direct native Debian qualification supplies this gate;
no GitHub Actions execution is used as evidence in this continuation.

Final independent review and fresh full GitHub restoration passed as recorded above.
Scoped B5-FS and generic B1/B2/B3 are accepted through the verified requirement map;
final carrier SHA/readback verification precedes handback.
Public/console/browser B4, full B5/T033/full T032 and whole #1553 remain open. An adjacent
known limitation is unchanged: the save producer rejects arbitrary case-distinct pairs;
this load-only block now preserves a valid externally authored manifested pair. Full
end-to-end round-trip acceptance of that pair needs a separate narrow producer fix.
No GM-authored field, mechanic or prompt/example contract changed.

Earlier dated sections below retain chronological checkpoints, including historical
pending states; they do not override this current runtime-proof summary.

## Native Linux filesystem qualification WIP — 2026-10-03 UTC

Tracked block **T032-B5-FS**, branch `codex/1553-load-filesystem`. Restored a new,
clean native Debian 13 x64 checkout from GitHub and verified full remote/source SHA
`07d354e16ce06796dee435003bd5fd2a023be62e`. All C# files remain byte-identical to
`e593bcfa5e45502de9eb6d6b3949565f7c7881b2`; accepted Windows evidence is retained.
Official task-local PowerShell 7.6.6 (release SHA256 checked), SDK 10.0.401 and
ASP.NET/.NET runtime 8.0.31 were installed without system changes. All three
telemetry opt-outs were set before startup/install; `DOTNET_PROCESSOR_COUNT=1`,
XML documentation and isolated writable CLI/NuGet/XDG/temp state are used.
HOME and CODEX_HOME remain unchanged. Spec Kit prerequisite resolution succeeds
for this feature; the existing constitution/spec/plan/tasks and LOAD-FS-001–008
remain consistent with the bounded Linux qualification scope.

The exact initial `1553-load-linux.json` selection first receives fresh build and
PlanOnly discovery, then actual native execution. Subsequent separate commands
select metadata + metadata transport, batch admission + paths, and the explicit
`1553-load-admission-consumers.json` for namespace/native/cold/v1/v2 boundaries.
The five resource categories run separately with their existing 10/12-minute
category and 15-minute command bounds, independent mutable roots and unchanged
heap/RSS/disk/child controls. No full/Fast/PreMerge sweep or repeated Windows run.
Source-bound artifacts, actual counts, native-body distinctions and cleanup will
be recorded after each bounded block. No Linux test is yet reported as passed.
Independent separate **gpt-6.1-sol / xhigh** review, final GitHub restoration and
Linux acceptance are pending. Public/console/browser B4 and T033/live/full B5
remain open. This client-owned filesystem qualification changes no GM-authored
field, mechanic, prompt or example contract.

### Initial native Linux result and case-identity regression — WIP

At clean `e73f1d60c4f9be6d82d43bfc47d127b937eac92c`, fresh XML-enabled
PlanOnly built both selected projects and planned 53 cases across six descriptors
in 4:17.363; actual native execution then passed **53/53** in 26.307 seconds with
complete selection and owned/runtime cleanup. See `recovery/evidence/load-linux-initial-plan-20261003`
and `load-linux-initial-green-20261003`. An earlier PlanOnly process returned an
approval-review cancellation after build launch with no result/cleanup summary;
its incomplete log is retained separately and is not a test/build pass.

The separate Sol 6.1 XHigh review identified missing simultaneous case-distinct
Linux payload coverage: original archive payload/manifest inventories currently use
OrdinalIgnoreCase even for arbitrary names. Existing mixed-case rows do not prove
this LOAD-FS-001 requirement. A narrow native-names owner now adds two actual typed
commit/rollback rows plus exact duplicate/fixed alias/false digest refusals. The
valid manifested case pair is appended to a current-producer archive independently;
the existing save producer itself rejects case pairs and is not changed by this
load-only block. The first attempted causal run at `cbd5c81b` stopped during the
fresh build with four CS0103 errors from a missing Services namespace import in the
new test file; zero tests ran, cleanup completed in 2:10.092. The build failure is
retained in `load-linux-native-names-build-failure-20261003` and is not behavioral RED.
The import is corrected; two additional original-manifest ambiguity/double-claim
controls bring the narrow owner to seven cases. Production remains unchanged for
causal RED.
The approved spec already requires this behavior; no new game or GM contract.

### Native original-name correction — causal RED and candidate WIP

At clean `95a61076d84b255649b06ce8e42cacf6f8beaaf3`, the fresh native-name
run completed seven cases: four controls passed and three failed causally at the
unconditional original case-folded inventory (valid commit, exact pair rollback,
and independently wrong pair hash). No setup error or skip; selection and cleanup
complete, 2:01.263 including fresh build. Exact evidence is retained in
`load-linux-native-names-red-20261003`.

Candidate correction opts only typed Linux load into ordinal original payload and
manifest inventories. The unchanged original public reader and Windows still use
their existing comparer. Schema/manifest lookup selects an exact original entry
first, or one unambiguous original case alias; it never renames payloads. Each
manifest claim must bind a distinct original entry, retaining count/length/hash
coverage and rejecting ambiguous alias or double-claim attempts. A dedicated
multiple-manifest guard and eighth regression row retain the original refusal
when two manifest entries differ only by case. Only after all
original validation does the existing finite fixed-path materialization run;
a mapped collision now produces InvalidDataException before extraction. Save
production, ZIP limits, publication/recovery and all resource controls are unchanged.
Candidate has not yet been built/tested. Rerun native names plus changed original
admission/fixed-alias/entry/outcome consumers; prior unrelated lease checks need no
repeat. Remaining metadata/path/native/cold/resource qualification follows on the
fixed source. Independent review and native filesystem gate remain open.

### Native original-name correction GREEN — 2026-10-03 UTC

At clean `2da4d545c9398ab1db019609d14429f9480574c2`, fresh XML-enabled
integration build and selected execution passed **52/52** in 2:35.976: native
names **8** (two exact Linux pair decisions plus six original-identity refusals),
fixed aliases **24**, original admission **7**, entry **9**, outcomes **4**.
All five descriptors completed, no skipped/failed/duplicate cases, owned-tree and
runtime cleanup complete. Exact parsed TRX/plan/summary/build evidence is under
`recovery/evidence/load-linux-native-names-green-20261003`; large generated runner
logs are explicitly hash-recorded rather than implied embedded. This is distinct
from the earlier 53-case baseline; these overlapping totals are not added.
The earlier causal native-name run had seven cases; the eighth added guard preserves
the original multiple-manifest refusal after ordinal inventory admission.
Separate Sol 6.1 XHigh source review of the frozen candidate found no remaining
actionable source defect; final evidence review and full filesystem gate remain open.

The targeted correction leaves save production and all Windows/legacy reader path
comparisons unchanged. Windows duplicate inventories are still rejected before
lookup; new exact-entry claims and duplicate-manifest checks are redundant for that
existing case-folded dictionary. Typed Linux original-name behavior is the changed
native branch. Metadata qualification next performs a fresh unit build because the
integration-only correction build cannot authorize stale unit `-NoBuild` use.
Subsequent path/batch and explicit namespace/native/cold/v1/v2 consumers plus all five
separate resource owners remain required. Public B4/T033/full B5 remain open.

### Native Linux non-resource filesystem cohorts GREEN — 2026-10-03 UTC

Separate native Linux commands completed on the unchanged `2da4d545` C# closure:

- Metadata plus transport **39/39**, two descriptors, 52.131 seconds including
  fresh unit build at `2da4d545`; `load-linux-metadata-green-20261003`
- Fresh path/batch **46/46**, two descriptors, 7.868 seconds at docs/evidence-only
  `46414e4f`; `load-linux-path-batch-green-20261003`
- Explicit namespace/native/cold/v1/v2 consumers **128/128**, eleven descriptors,
  1:06.690 at `46414e4f`; `load-linux-admission-consumers-green-20261003`

All have complete selections, zero failed/skipped/duplicate cases and complete
owned/runtime cleanup. Native symlink, hard-link and FIFO bodies executed;
Windows-spelling rows are pure comparison-contract checks, not Windows filesystem
execution. The cold bodies use actual owned process cuts and journal-only fresh
recovery with extraction absent, retaining all independent namespace/protected
state assertions. Separate Sol 6.1 XHigh cleared the scoped name correction and
52-case evidence at `46414e4f`; no wider gate is inferred from that narrow review.

The explicit initial Linux selection now includes the eight native-name rows for
future affected runs. Its 61-case union is backed by separately named initial and
corrected cohorts, not a fabricated single 61-case execution. Existing category
budgets remain unchanged. Catalog discovery-only audit and five separately bounded
resource commands follow; heap 768 MiB/RSS 1 GiB/disk 5 GiB, 180 seconds per child
except maximum-inventory publication/cut 600 seconds, 10/12-minute category and
15-minute command controls are unchanged. No aggregate sweep or Windows replay.

### Native Linux resource qualification in progress — 2026-10-04 UTC

The source-bound [Linux qualification record](recovery/load-filesystem-linux-qualification.json)
tracks each separately executed resource category and all measured children. First
preparation command passed **4/4**, eight measured children, 1:45.197, complete
selection and owned/runtime cleanup. Actual 64/128/near-512 MiB and maximum inventory
preparation ran under unchanged controls; all eight children exited without guard
stops. Near-512 seed OS peak RSS was 556,322,816 bytes; every child stayed within
180 seconds. Separate bulk publication passed **3/3**, six measured children,
1:31.586 at clean `fd83927c`, exact committed After/source/library/generation and
complete cleanup. No claim is made for the three still-pending resource owners.

This preparation run transparently records HEAD `46414e4f` plus 27 modified/untracked
documentation/evidence/selection files during delayed publication. Those exact bytes
are now the verified `fd83927c` tree; all runtime/test C# and fresh binary inputs
remained the tested `2da4d545` closure. This is not described as a clean-source run.
The completed 39/46/128 non-resource bundles were separately reviewed and saved at
`fd83927c`; the source-only name correction/evidence was accepted narrowly by the
separate Sol 6.1 XHigh reviewer at `46414e4f`. Overall filesystem acceptance remains
pending final resource proof, catalog/XML audit, final review and fresh restoration.

### Native inventory publication monitor failure — causal investigation WIP

At clean `ff0dce30`, maximum-inventory publication completed **1/1 failed** in
50.183 seconds, with owned/runtime cleanup complete. The seed finished under its
180-second bound; publication reached actual member 12000 but its disk sampler
raised `Owned disk sampling encountered a link`. Phase completion, commit and
resource acceptance are not claimed. Exact partial measurements and TRX are kept
in `load-linux-resource-inventory-publication-failed-20261004`.

The Unix FileSystemInfo missing-attribute sentinel is -1, which also contains the
ReparsePoint bit. An independently refreshed missing entry can therefore be
misclassified before the existing FileNotFound handler. Deterministic native Linux
monitor controls now reproduce that boundary for regular and declared/unexpected
directories, retain stable-missing refusal and real existing/dangling link refusal,
and check exact stable byte counting. New `portable-load-resource-sampling` owns
ten cases. Only a test observation seam is added; the sampler is unchanged for
causal RED. No product code, limits, polling cadence or required samples change.

### Native sampler sentinel — causal RED and candidate WIP

At clean `8b77c357`, native .NET **8.0.31** executed all ten monitor controls:
**six causal failures and four passes**, 1:44.000 with fresh unit build and complete
cleanup. Every missing row logged actual cached attributes **-1**; existing file,
directory and dangling native links still refused, and stable byte counting passed.
Exact artifacts: `load-linux-resource-sampling-red-20261004`.

The candidate resolves only that missing sentinel through File.GetAttributes before
checking the link bit. Ordinary cached entries retain the original fast path.
FileNotFound tolerance is restricted to an active child's exact enumerated FileInfo
or an explicitly declared convertible/private directory; stable and unknown-directory
failures still propagate. Real links, all other errors, disk/RSS/time limits, final
OS peak and forced closed-boundary samples remain unchanged. No product code changes.
The ten controls must turn GREEN, then the actual failed inventory phase is rerun.
All five resource owners will be measured separately on the corrected probe; prior
preparation/bulk passes remain historical source-specific evidence, never erased.

### Native sampler correction GREEN — 2026-10-04 UTC

At clean `eed0ca99e7ad8ef8306c6f4758ed55cce722fd46`, fresh unit build and
`portable-load-resource-sampling` passed **10/10**, complete descriptor and cleanup,
1:51.078. All six formerly causal missing-sentinel rows now have their intended
active/stable/declared behavior; three real native link shapes still reject and
exact stable bytes remain measured. Separate Sol 6.1 XHigh source review found no
actionable candidate defect. Exact proof is in `load-linux-resource-sampling-green-20261004`.
The corrected-probe C# closure is `eed0ca99`; production remains `2da4d545` unchanged.
The qualification record preserves the two previous phase passes separately and
requires all five current-probe phases; no old pass or failed attempt is relabelled.
The failed maximum-inventory publication was the first actual envelope rerun and
now passed **1/1**, two measured children, 1:02.958 at clean `eed0ca99`, with
complete state/source/library/generation oracle and owned/runtime cleanup. Exact
ordinary (not diagnostic) proof is in `load-linux-resource-inventory-publication-green-20261004`.
Remaining preparation/bulk publication and both recovery categories will execute
separately on this corrected probe without another code change.

## Authorized local filesystem execution checkpoint

The owner is leaving autonomous execution running and explicitly authorized all
recommended choices/specs/plans/revisions, waiving further written review.
Execution plan revision 1 and its concrete v3 schema are therefore authorized.
Independent Sol 6.1 XHigh design review passed `8ca3fbca`; its scoped proof readback
also passed `532a0f36`. This accepts the design/tracking gate only, not load code.

Latest resource checkpoint: ordinary maximum-inventory publication at clean
`e593bcfa5e45502de9eb6d6b3949565f7c7881b2` passed 1/1 with two measured children,
complete selection and owned/runtime cleanup, command13:24.803 including fresh
XML-enabled build. Exact [evidence](recovery/evidence/load-resource-inventory-green/summary.json)
and [measurements](recovery/evidence/load-resource-inventory-green/metrics.json)
record 8192 original ZIP entries, 2096128 UTF8 name bytes, 9194739-byte v3 metadata
and 17441 members. Actual publication phase 393.583seconds, whole child 530.955seconds
under the amended 600-second bound; seed 101.802seconds under 180. Peak child
RSS 260673536 bytes and owned disk 36697323 bytes retain fixed heap/RSS/disk controls.
This is declared ordinary qualification, DiagnosticOnly=false; original180-second
failures and the separate diagnostic pass remain unchanged. Parent disk work fell
from 207.475 seconds / 2423 scans to 42.974 seconds / 459 scans, but whole child increased
from 486.530 to 530.955 seconds: do not claim an observed load speedup from reduced
monitor work.100ms is a polling delay, not a guaranteed sampling frequency.
The fresh selected control at clean `88df401032163aebc5eb95c2eb50c13fc4407f85`,
`tests/selections/1553-load-admission-consumers.json`, passed 128/128 across 11 complete
descriptors in 7:39.005, no skips/duplicates and complete cleanup. Exact
[artifacts](recovery/evidence/load-resource-admission-consumers/summary.json) cover
only changed scope/namespace/native/cold and affected v1/v2/host consumers;
fresh integration/unit builds preserve the unchanged e593 C# source closure.
Separate Sol 6.1 XHigh review cleared the actual ordinary many-publication evidence
with no findings. Its bounded acceptance does not close remaining resource phases.
All five separate current resource commands now passed 12/12 with 28 measured children:
preparation 4/4 in 5:19.202; bulk publication 3/3 in 4:42.302;
maximum-inventory publication 1/1 in 13:24.803 including fresh build;
bulk journal-only recovery 3/3 in 5:05.053; maximum-inventory committed recovery 1/1
in 10:29.286. Exact sources/measurements are consolidated in
[Windows qualification](recovery/load-filesystem-windows-qualification.json).
Many recovery measured 94.770 seconds / whole 99.345 under 180, following the real committed
cut at 409.037 seconds under 600; extraction was absent, one fresh normal acquisition
and complete independent state/generation/source/library checks passed. Repeated
acquisition is separately proved by the affected cold controls, not this resource case.
Final clean discovery audit at 9a8f8cdf passed 219 categories / 10565 methods/files with
zero executed tests in 25.077 seconds; five generated XML assemblies parsed,
24 changed C# files / 2613 introduced XML lines have no compressed blocks or touched-file
XML compiler warnings. Three unrelated existing CS1587 warnings are retained.
Exact [audit artifacts](recovery/evidence/load-filesystem-final-audit/summary.json)
and [XML diagnostics](recovery/evidence/load-filesystem-final-audit/xml-audit.json)
are saved. Later resource sources differ only in docs/evidence/CI settings from e593;
the required fresh build closure is preserved before NoBuild. Separate Sol 6.1 XHigh
cleared RESOURCE/ADMISSION/MONITOR code/native Windows through 84419ff0 after independently
checking every raw report and affected case, actual XML and source equivalence.
Two narrow documentation findings were corrected: attribute repeated acquisition
to cold controls instead of the one-lease resource host; include both metadata
authority and transport owners in the still-unqualified Linux command. No new
runtime or design revision was necessary.
[GitHub-only readback](recovery/load-filesystem-github-readback.json) restored 84419ff0
in a fresh owned sparse checkout: 204 current changed source/evidence files relative
to 5d2aa2ce, all exact physical Git blobs/SHA256, 101 JSON / 63 TRX parsed, clean status and
connectivity. The reviewer independently reproduced its counts/identities from the
actual restoration. This is scoped source/evidence delivery proof, not a full checkout
or behavioral rerun. Later proof/status commits leave e593 C# unchanged; final carrier
readback is verified separately. Native Linux/public-client acceptance
is not established by Windows results; the Linux workflow now carries all three
telemetry opt-outs and XML compiler settings but no actual run has been obtained.

Observed workflow checkpoint: one implementation owner and bounded read-only
assistance preserved ownership. Independent review found the B1 mixed-directory-case
defect and resource-monitor wording; both were corrected with specific evidence.
The missing-using, root sampler and catalog-placement failures were harness/build
rework, retained as non-passes. Fresh phase measurements disproved the initial 180-second
maximum-inventory estimate; private stage tracing enabled a narrow justified amendment.
Reduced monitor work did not prove faster load elapsed time. No model cost/speedup
percentage or routing improvement is inferred from these observations. Future public
UI changes select their affected caller contracts and do not repeat unchanged filesystem
resource qualification; preserve mandatory independent Sol 6.1 XHigh review.

Current task: T032-B5-FS handback. T032-B1-FIX, T032-B1-METADATA, T032-B2-NAMESPACE, T032-B3-COLD and RESOURCE/ADMISSION/MONITOR are cleared for code/native Windows,
while the separate native Linux gate remains open. Original baseline was `5d2aa2ce`.
Five-fix candidate source is now written after the recorded causal baseline:
Trim rejection and immutable declared-path canonicalization precede extraction/classification;
acquisition wraps only ordinary recovery at SessionReplacement and preserves coordinated
uncertainty through release failure; private preparation/cleanup carries both causes,
source and owned residue; Uncertain never claims an established generation and always
needs follow-up. Four directory-alias controls and original-manifest-hash refusal extend
alias ownership to 19 cases; outcomes remain 4, entry 9. First fresh candidate run
at `2b0ab01c` built and executed 19 aliases: 18 passed, trailing-config whitespace
remained causal RED because Windows resolution erased the space. Its fail-fast
selection did not execute outcomes/entry; no combined pass is claimed.
The bounded source consultation confirmed eight paths miss immediate refresh
consumers. Four manifested inventory cases at `9a54dd23` were causal RED at complete
staging admission on Windows (23/23 executed, 18 passed/5 failed, 3:22.138,
cleanup complete). Exact [artifacts](recovery/evidence/load-red/registry/summary.json)
are saved. At `ee573765`, fresh native Windows verification passed 45/45 cases:
23 alias + 4 outcome + 9 entry + 9 exact acquisition consumers, five descriptors,
no skips/duplicates, cleanup complete, 5:33.898. Exact [artifacts](recovery/evidence/load-fix-green/summary.json)
are saved. Independent Sol 6.1 XHigh source review found one P2 mixed-directory-case
inventory refusal and one documentation finding, with no other actionable findings.
The new manifested `LORE/MyCustom.JSON` row at `a3e55f0c` reproduced that P2 on Windows:
24/24 executed, 23 passed/1 failed at complete staging revalidation, 3:39.623,
cleanup complete. The correction uses native comparison only for observed inventory;
archive keys/bytes remain unchanged and Linux still uses ordinal equality.
Changed APIs now have multiline XML documentation. Original admission controls
add three typed corrupt/missing-resource/unknown-owner cases and four existing raw
budget/stream-position cases. Fresh XML-enabled native Windows verification at
`7f610a3165a095b55fed1f30ca9f37df9834dbd3` executed 40/40: aliases 24/24 and entry
9/9 passed, original admission 6/7 passed, complete cleanup, 4:20.138. Exact
[artifacts](recovery/evidence/load-fix-review/summary.json) are retained. The unknown-owner
test fixture had a stale persisted authority root and incorrectly expected an owner ID
where production reports issue codes; this was a fixture defect, not a production regression.
The correction establishes ledger/history agreement, composes and persists matching
authority, and asserts `resource_owner_unresolved` while excluding `root_stale`.
The remaining cleanup test XML comment is corrected. Fresh XML-enabled native Windows
verification at `60e539cf49fefb2d20edc2065aa3b8eb374f82e2` passed 7/7 original-admission
cases, no skips/duplicates, cleanup complete, 3:19.990; exact
[artifacts](recovery/evidence/load-original-green/summary.json) are saved. Client/integration
XML documentation parsed; no touched-file XML reference warning occurred (three existing
unrelated CS1587 warnings remain). The same clean source's discovery-only
[catalog audit](recovery/evidence/load-fix-catalog/catalog-audit.json) passed 203 categories,
10,520 methods/files, no unmapped/stale selectors, no test execution, cleanup complete,
1:29.822. Independent Sol 6.1 XHigh inspected source and actual evidence and cleared
B1 code/native Windows with all findings resolved. Distinct retained evidence covers
aliases 24, entry 9, admission 7, outcomes 4 and lease consumers 9; this is not a new
combined 53-case run. Metadata assistance prepared tests in ignored scratch and may now
enter the compile tree under its new narrow owner for causal RED before production.
Native Linux remains unexecuted; metadata, topology/cold/resources and public-client
acceptance are not implied by this B1 correction gate.

Metadata WIP: the parent inspected the independently drafted current-API tests and
installed `TrustedLocalFrameMetadataTests.cs` with explicit `portable-load-metadata`
ownership. The initial 33 cases cover real >1 MiB writer/reader decisions, independently
encoded small v2 compatibility, reordered/escaped boundary tokens and 26 late corrupt
schema/region cases preserving earlier state/evidence/generation. The production codec
is unchanged; only this new category runs first to establish causal cap failures.
The future optional internal observer will prove actual per-member encoding flush and
actual-token carry growth/release; a >64 KiB escaped relative token proves transport
followed by independent path refusal, not native long-path acceptance. After causal RED,
one Sol 6.1 High codec implementer owns Journal/FrameMetadata plus its narrow observer
and tests; the parent owns catalog, execution, integration and acceptance. Separate
Sol 6.1 XHigh reviews the completed codec block. No parallel builds/tests or public cutover.
At clean `4a9da995c34482a363521c560f59490102441d11`, initial native Windows metadata
verification executed 33/33, 30 passed/3 causal failures, no skips/duplicates, complete
cleanup, 3:04.086. Exact [artifacts](recovery/evidence/load-metadata-red/summary.json)
are retained. The real 4,096-member producer fails the existing 1 MiB writer budget;
both independently encoded >1 MiB pending/committed frames fail the reader budget.
All small independent/reordered/schema/region controls pass. Next: observer scaffolding
and causal flush/carry tests before implementing the streamed codec. The parent also
prepared four real-entry namespace conversion/harmless empty-directory preservation tests only in ignored
scratch for the later B2 block; they do not enter this compile/run or establish B2 evidence.

The optional constructor-owned observation seam and two transport tests are now
scaffolded without codec behavior changes. `portable-load-metadata-transport` owns
only these two causal measurements; the existing 33 authority cases retain explicit
method ownership in `portable-load-metadata`. The next fresh run selects only the
two new controls, avoiding a repeated unchanged cap/schema baseline. No observer
callback occurs in the neutral scaffold. At clean `a0cda0aa06f0d7b1e1265eff60c0e162c9135f47`,
fresh native Windows verification executed both selected transport controls and
produced the expected two causal failures: zero writer flush observations versus 101
required opening/member events, and no reader carry observations. No skips/duplicates,
cleanup complete, 2:47.976; exact
[artifacts](recovery/evidence/load-transport-red/summary.json) are saved. Codec implementation
is now released to the bounded Sol 6.1 High owner; no implementation/build/green/review
claim is made by this transport baseline. Full required-field omission coverage will
join the strict metadata owner with independently reset evidence and exact preservation.

The inspected codec candidate now directly writes existing logical members with
opening/per-member flush, patches the checked length slot and rebinds a closed,
self-contained frame. Its reader uses physical-region-limited 64 KiB token carry,
grows from actual incomplete bytes and releases oversized storage after completion;
strict decoded field bitsets and checked contiguous regions feed unchanged complete
path/hash/generation validation. The whole encoded header, duplicate header graph
and 1 MiB aggregate cap are removed; v1 dispatch/serialization remain unchanged.
The metadata owners now select 39 cases: transport 2, authority 37, including one
61-mutation required-field/type/null fact, two raw UTF-8 split cases and one
10-mutation syntax/numeric/invalid-UTF-8 fact. The parent inspected source and
test changes; no build, passing runtime, completed review, native Linux or resource
claim follows yet. Next: fresh two-owner verification, then unchanged-source bounded
v1/v2 publication/dispatch/generation consumers and discovery-only catalog audit.
At clean `3b3e9a7c6671406f1416182a5c9fcf84a773e30e`, fresh native Windows metadata
verification passed 39/39 cases, two complete descriptors, no skips/duplicates,
cleanup complete, 4:07.426; exact
[artifacts](recovery/evidence/load-metadata-green/summary.json) are retained. The same
fresh unit/host closure then passed 41/41 selected compatibility cases without rebuild:
two immutable original-v1 decisions, four bounded format-dispatch cases, six actual
cold generation cuts and 29 v2 publication/adapter controls. Four complete descriptors,
no skips/duplicates, cleanup complete, 0:51.505; exact
[artifacts](recovery/evidence/load-metadata-consumers/summary.json) are retained.
Independent Sol 6.1 XHigh source/contract review found no production findings and
confirmed the actual 39-case evidence; its four small test-helper XML comments are now
corrected. Final fresh XML-enabled discovery at clean `f822ef9337b862451351447b4b4add13bb5538d6`
passed 205 categories/10,530 methods, no unmapped/stale selectors, no test execution,
complete cleanup, 4:32.777; exact [catalog artifacts](recovery/evidence/load-metadata-catalog/catalog-audit.json)
are retained. Client/unit/integration XML parsed; only three pre-existing unrelated CS1587
warnings remain. Separate Sol 6.1 XHigh inspected source and actual artifacts and cleared
metadata code/native Windows with no remaining findings. Runtime is not repeated solely
for comments. Observed assertions establish
opening plus each member advance/zero pending bytes for 100/1,000 members with equal
largest-member pending peaks, actual >64 KiB token growth bounded by twice its filled
carry and immediate return to 64 KiB before subsequent tokens, and real >1 MiB
4,096-member producer/reader decisions. These are measured transport/count bounds,
not full legal-entry/name/live-deletion or process resource qualification; B3 retains
that separate envelope. No native Linux or public-client pass is claimed.

B2 begins with four connected real-producer tests covering both conversion directions,
empty/nonempty directory replacement and harmless old empty-directory preservation.
The new `portable-load-topology` owner selects only these cases for the unchanged
same-shape adapter's causal baseline. No v3 production implementation precedes this RED.
The first build at `24006de6` exposed a missing Services using in the newly installed
test partial. No tests executed, so this is a preparation failure rather than causal
RED. The import is corrected before the fresh selected rerun; production is unchanged.
At clean `dfc40c5d399daba74fb0c53ec83784a75fffa485`, native Windows ran all four
cases: harmless empty-directory preservation passed, all three conversions reached
the real lease/prepublication adapter and causally failed its strict file/ancestor
same-shape checks (NotLoaded, no publication phases). Complete selection, no skips/
duplicates, complete cleanup, 3:09.080; exact [artifacts](recovery/evidence/load-topology-red/summary.json)
are retained. This releases the reviewed B2 v3 implementation; it is not a B2 pass.
The first integrated candidate adds separate streamed v3 evidence/model files, narrow
non-following namespace observations, complete child/boundary/shape preflight, stable
transaction scratch anchors, nonrecursive conversion reconciliation and generation-last
publication/recovery. `FileSystemManager.LoadNamespace.cs` contains the complete capture,
plan construction and manager adapter; the older file-only adapter remains unchanged.
Typed load now consumes this namespace adapter, retaining old public callers for B4.
Twelve connected topology cases include eight actual structural/file/commit-stage cuts;
33 independent v3 cases include 94 required-field mutations and unknown late file/empty
directory and narrowed root/library/required-directory admission. The older later-member
test now cuts the second actual file publication, rather than assuming a pre-v3 node
index. First selected compilation/runtime and review are pending; no topology pass or
native Linux/cold/resource/public acceptance is claimed.
At clean `e343e19c1f5c8e366307a9996fe62b6c01ab7604`, fresh native Windows compilation
succeeded. The first selection ran 12 topology cases: four conversion/preservation
positives passed, eight one-shot IOException cuts rolled back then correctly retried
to commit under the preserved transient policy, so their terminal-rollback assertions
failed. This is a fault-fixture error; use InvalidOperationException without changing
retry policy. Fail-fast did not execute the unit owner; no 45-case pass is claimed.
The separately selected same fresh closure passed all 33 independent namespace cases,
no skips/duplicates, complete selection/cleanup, 0:30.312; exact
[mixed candidate](recovery/evidence/load-namespace-candidate/summary.json) and
[independent artifacts](recovery/evidence/load-namespace-independent/summary.json) are retained.
Independent Sol 6.1 XHigh found a valid pending absent-before-generation defect: the
generation-last file pass does not delete the new generation on rollback to Missing.
The new `portable-storage-namespace-generation` owner selects independent pending/
committed frames to establish causal RED before correction; the real generation-published
interruption joins the connected topology owner. These are exact original authority
requirements, not added scope. Missing helper/enum/attempt XML comments reported by
review are corrected for the fresh XML-enabled build; no runtime is repeated solely
for those documentation corrections.
At clean `a8ec943d2abe9be772b684f7df8a71376baf5ea2`, fresh XML-enabled native Windows
ran both independent generation decisions: committed passed, pending causally failed
after restoring members because the new generation file remained present. Both cases
executed, no skips/duplicates, complete cleanup, 2:41.474; exact
[artifacts](recovery/evidence/load-namespace-generation-red/summary.json) are retained.
The correction deletes the exact generation name last when rollback targets Missing,
after ordinary members/directories have been restored, preserving generation-last
authority and by-name publication. The connected real generation cut and corrected
nontransient warm cuts now run with the independent namespace admission consumers.
Independent Sol 6.1 XHigh scoped correction review found no further finding.
At clean `9c43fb9eb5da239fade03757573de0e69dc3c3ec`, fresh native Windows
passed all 48 selected cases: topology 13, independent namespace 33 and exact
generation 2, no skips/duplicates, complete selection/cleanup, 5:18.804 including
build; exact [artifacts](recovery/evidence/load-namespace-green/summary.json) are retained.
Ten independently drafted actual boundary/cleanup cases now join two narrow owners:
`portable-load-native-boundaries` (6) and `portable-load-cleanup-debt` (4).
The parent inspected the entire draft before installation; no production change
is made merely to satisfy these unexecuted cases. Affected typed entry/alias/outcome
and old version-dispatch/fixture consumers run in the same fresh build closure.
At clean `2746f1c1bee5bb3853240c9202c12bda5026c8de`, fresh XML-enabled Windows
executed all 53 selected cases: native boundary 6, cleanup debt 4, entry 9, aliases 24,
outcomes 4 and independent v1 fixtures 2 passed; dispatch passed 3/4. Complete selection,
no skips/duplicates, complete cleanup, 6:30.256; exact
[artifacts](recovery/evidence/load-namespace-consumers/summary.json) are retained.
The sole dispatch failure used BOELP3 as an unknown version, which v3 now intentionally
recognizes and safely rejects as the mismatched v2-shaped body. Its expected legacy
ReadJournal stack assertion was obsolete. The fixture now uses unsupported BOELP4,
retaining the same 128 MiB file/64 MiB child bound and all evidence/session assertions.
No production decoder or rejection policy changes. Only dispatch reruns after this
test correction, then discovery-only catalog/XML checks; no 53-case pass is claimed.
Fresh XML-enabled native Windows at clean `5a222cfaeccaa3afe493780b9eb6f6f5d7de5c80`
passed corrected dispatch 4/4, complete selection/cleanup, no skips/duplicates,
3:00.180; exact [artifacts](recovery/evidence/load-namespace-dispatch-green/summary.json)
are retained. Same-source NoBuild discovery-only [audit](recovery/evidence/load-namespace-catalog/catalog-audit.json)
passed 210 categories/10,545 methods, no unmapped/stale selectors, no tests executed,
complete cleanup, 0:19.849. Four compiled XML documents parse; no touched-file XML
reference warning occurred, with the same three unrelated existing CS1587 warnings;
[verification](recovery/evidence/load-namespace-catalog/xml.json) is recorded.
Independent Sol 6.1 XHigh cleared source, generation and fixture corrections with
no remaining findings; final actual-evidence review cleared B2 code/native Windows
through `d229823a`. All runtime results remain
source-specific separate cohorts, not a fabricated combined pass. GM prompts/examples
need no update for these client-owned local filesystem internals; no GM-authored
gameplay/state schema or public caller changed. Cold/resource/native Linux/public
acceptance is not established by this B2 evidence.
B3 cold scaffolding was drafted in ignored scratch while B2 verification ran, with
no compile-tree edits or runtime claims. The completed B2 gate now releases actual
cold harness/test installation. One retained Sol 6.1 High owns this coherent harness
draft; parent inspects/installs/qualifies it, separate Sol 6.1 XHigh reviews the block.
The draft has 29 isolated cases: 13 durable-decision cuts, 10 forward/rollback topology
cuts, four pending/committed unknown file/empty-directory refusals and two incomplete
exact scratch controls. Source is the real manifested current producer inside the
opaque library; complete After expectations derive ZIP hashes plus explicit preserved
directories/library, never the implementation's namespace plan. Every announced child
waits for parent termination; fresh normal acquisition runs without exact private
extraction. Generation regions/bytes, source/library, complete names and repeated
decisions are asserted. No native Linux, envelope or public-client result is implied.
At clean `e25591edea072aeeb65628718bf815f56e7dc06f`, fresh XML-enabled native Windows
passed all 33 selected cases: 19 cold decision/conflict/scratch, 10 cold topology and
four preserved host/reader dispatch controls, no skips/duplicates, complete selection/
cleanup, 4:57.690. Exact [artifacts](recovery/evidence/load-cold-green/summary.json)
retain child cut/OS/path/frame/generation/RSS/time reports. Category wall times are
77.138/45.217/12.985 seconds; fresh build took 149.635 seconds. The first actual run
needed no source/test correction. Same-source NoBuild discovery-only
[audit](recovery/evidence/load-cold-catalog/catalog-audit.json) passed 212 categories/
10,549 methods with no unmapped/stale selectors and no execution, complete cleanup,
0:20.997. Four compiled [XML documents](recovery/evidence/load-cold-catalog/xml.json)
parse, with no touched-file reference warning and the same three unrelated CS1587.
Independent Sol 6.1 XHigh source/contract review found no actionable issue; final
actual-evidence review cleared B3-COLD code/native Windows through `3d20be0a`, with
no remaining findings. Resource probes are drafted only in ignored
scratch while this immutable source qualifies; no bulk/native Linux result is claimed.
Resource harness is now installed for first qualification:12 independent rows in three separate ten-minute owners, actual current-producer seed and prepared/decision/acquisition phases. Parent inspected drafts; no production/archive policy changed. Start/stop sampling uses owned-child acknowledgements; whole-child guards include instrumentation/verification. Fresh XML-enabled preparation owner runs first with a15-minute command cap. No passing resource or Linux claim follows installation.
First fresh PlanOnly at ea32f5f2 failed build-unit after2:30.753: missing Services import for ResourceMaterializationContract in the new test, zero discovery/runtime cases, cleanup complete. Saved build-failure evidence is not a causal product RED. Parent adds the exact missing namespace and equivalent StartsWith assertions to remove three new analyzer warnings before a fresh retry; no production change.
Fresh corrected PlanOnly at ed48fef1 passed build/discovery in2:52.408 and planned four preparation rows. First actual NoBuild run executed4/4, all failed before seed qualification because the parent disk walker applied descendant-only path validation to its exact owned root;20.620 seconds, cleanup complete. Saved load-resource-harness-failure is not product RED. The narrow correction treats the exact root as nontransient before preserving strict descendant checks; no budget/archive/production change. Fresh rebuild and the same causal owner follow.
At clean e22ce0cd, fresh native Windows preparation qualification passed4/4, eight actual measured seed/phase children, no skips/duplicates, complete cleanup/selection,8:05.161 including build; selected runtime5:11. Original many archive has8192 entries,2096128 UTF8 name bytes,3242911-byte original manifest and9216 old files. Actual largest expanded archive536816474 bytes; original archive536974584 bytes. Phase preparation maxima48.80 seconds/120.63MiB OS peak RSS/1536.00MiB sampled owned disk; separately seed max99.51seconds wholechild/506.90MiB RSS. All unchanged768MiBheap/1GiBRSS/5GiBdisk/180secchild controls passed. Exact evidence: recovery/evidence/load-resource-preparation/. Publication/recovery/resource acceptance remains pending. Subsequent NoBuild uses the same fresh C# closure; this checkpoint changes only evidence/docs.
At a996a8da actual publication executed4/4:bulk64/128/near512 passed, many stopped at180.114 seconds (RSS153456640/disk13956804), no timeout of command, complete cleanup/selection,9:36.601 total. Saved load-resource-publication-stop retains actual three passes and failedrow; no full publication/resource PASS. Category split and existing operation progress/failure retention diagnose only many, with unchanged original archive/heap/RSS/disk/child limits and no production change. Next fresh narrow inventory run; bounded read-only algorithm consultation runs separately.
At clean97d68378 narrow inventory diagnosis executed1/1 and failed at the unchanged180-second child bound; whole command7:39.071, complete selection/cleanup. Actual progress: CanonicalLockOpened at76.600 seconds; no MutationAdmission reached; RSS154697728/disk13956817. Retained load-resource-inventory-diagnosis includes exact TRX partial events; this localizes the expensive interval without timing individual helpers. T032-B3-RESOURCE-ADMISSION now tracks the reviewed-waiver repeated-parent correction, original authority and fresh-call/no-cache boundaries; canonical spec/plan/tasks consistency checked before installation. No production fix or resource PASS is yet claimed.
Candidatec7296c5b installed the bounded correction and11 new native/batch cases. The first contract command was rejected before build/discovery because the new category was mistakenly nested under the inventory selector; zero runtime, no productRED. Correct its placement and validate with Read-TestCategoryCatalog before the fresh selected command.
At clean405f7fdd fresh path/batch passed46/46 (11new+35scalar), zero skips/duplicates, complete selection/cleanup,3:05.765; retained load-resource-admission-contracts. Sameclosure narrow many publication failed at180.059 seconds,5:00.459 whole command, complete selection/cleanup. Lock opened73.996s, first mutation admission166.734/index17000at170.225; no intent staged. These are whole-child parent timestamps including preparation, not phase-only elapsed. Retained load-resource-inventory-admission-stop includes partial events. Admission correction is source-reviewed without actionable findings but resource performance remains open. T032-B3-RESOURCE-TRACE records one separately bounded full-pipeline investigation before another algorithm change; original180-second qualification remains unchanged.
At clean1466980c actual separately bounded trace passed1/1 with two fully measured Windows children, no skips/duplicates, full state/generation/library/source and cleanup,12:40.170 command including build. Publication phase353.618s/whole486.530s; preparation before phase52.646s; RSS257871872/disk36697183; cumulative allocated20120557568bytes is not liveheap. Parent disk walks207.475s/2423samples/max477.582ms; no equal speedup is inferred. Saved load-resource-pipeline-trace preserves DiagnosticOnly with OriginalResourceAcceptance=false. Independent scoped TRACE source review found no validated defect; actual measured evidence completes the tracked investigation, not resource acceptance. T032-B3-RESOURCE-MONITOR writes1000ms plus durable-boundary disk sampling and a justified600sec onlymanypub/cut ceiling (~23percent measured headroom), separate12minute inventory owners/15minute commands; original failures remain. Original acceptance/resource/source/library/schema/grants unchanged; consistency verified before edits.
Process checkpoint: independent review caught a real generation-absence rollback
defect. Runtime exposed a transient-fault expectation error and the now-obsolete v3
unknown-version fixture; each received a scoped correction with original requirements
preserved. B2 consumer build preparation took 248.817 seconds versus selected runtime
about 82 seconds; passing unchanged controls were retained after the fixture correction.
No measured token savings or broad quality ranking is inferred from these observations.
The parent owns namespace observation/preflight/reconciliation and load integration;
the retained Sol 6.1 High codec implementer may own only the separate v3 descriptor/frame
files after release. This bounded delegation reuses known cursor machinery without
duplicating namespace investigation or sharing edits. Independent Sol 6.1 XHigh reviews
the completed topology block. The final metadata discovery spent 247.362 seconds building
its two projects; selected runtime controls remain retained rather than repeated.

Process checkpoint: independent review found the real mixed-directory inventory defect,
but initially missed the admission fixture's persisted-root mismatch; the real typed
runtime exposed it and one fixture correction rerun resolved it. No other correctness
findings were reported. The 40-case run spent 185.283 seconds building versus about
62 seconds executing selected categories; the seven-case correction run took 3:19.990.
Build/preparation dominates these cohorts, so unchanged passing controls are retained
and future work uses one coherent codec owner plus separate review, not per-helper
review cycles. No token/cost savings or overall model-quality improvement is inferred.

The first selection-only push after Actions enablement produced no registered run/check
suite; workflow lookup still showed only the disabled general workflow. The meaningful
workflow-file update and later targeted selection push also registered no run/check.
Explicit enable of the new workflow returned 404; repository API still confirms
Actions enabled with only the three selected actions. A read-only settings-page
attempt was unauthenticated and provided no diagnostic evidence. The reason remains
unknown; no default-branch merge, general-workflow enable or native Linux pass follows.
This is CI activation diagnosis, not archive screening.
WIP infrastructure: the new branch-push Linux workflow and explicit alias/outcome
selection are prepared. The first Linux job is a causal baseline, expected to expose
unfixed regressions; build/CI/catalog/native acceptance are not yet claimed. A fresh
Windows PlanOnly build of the same two owners is in progress at `e5ac0174`.
Windows baseline is now complete for the intended methods across two bounded calls:
PlanOnly at `e5ac0174` built fresh in 3:19.938, discovered 14 alias and 4 outcome cases,
executed none and completed owned cleanup. At `4df443db` (identical production/tests),
the combined NoBuild call ran aliases 14/14: 10 GREEN, 4 causal RED in 35.621 seconds;
fail-fast did not run outcomes. The separate outcome NoBuild call ran 4/4: 1 GREEN,
3 causal RED in 22.046 seconds, complete cleanup. No skips. Alias failures show
three unexpected Committed results and one wrong failure classification; outcomes
show prior recovery misclassified NotLoaded, a fabricated old established generation,
and missing private-cleanup follow-up. The corrected Shining Abode fixture reached
real load and passed on Windows. Native Linux fixed-name positives still need proof.
Safe source-bound artifacts: recovery/evidence/load-red/.

CI bootstrap ruling: GitHub API showed repository Actions enabled=false and the old
`.NET CI` state disabled_manually; no branch run existed. The approved native Linux
CI deliverable plus owner's autonomous recommended-choice authorization covers enabling
repository Actions narrowly for checkout@v4/setup-dotnet@v4/upload-artifact@v4 only,
while keeping the old general workflow manually disabled. Record API readback before
triggering the selected branch cohort. This is development infrastructure, not a change
to cloud screening or OS security; missing actual Linux execution remains a non-pass.
API readback now confirms enabled=true, allowed_actions=selected, github_owned_allowed=false,
verified_allowed=false and exactly the three required @v4 patterns. Initial requests
with allowed_actions while disabled were rejected 409 without mutation; the supported
enable-then-select sequence succeeded. The old general workflow remains manually disabled.
The explicit selection update triggers the native causal baseline; no Linux test result
is claimed until the actual run/artifacts are inspected.
Next: fresh Windows alias/outcome discovery and causal runtime baseline, then five
fixes and affected consumers. No native Linux load result is inferred; install
the explicitly selected Linux CI with this block. Parent implements; separate
Sol 6.1 XHigh reviews the completed code/tests/docs before block acceptance.

Execution ledger uses existing Spec Kit task IDs and this canonical checkpoint;
local Superpowers workspace is `.superpowers/sdd/ordinary-load-plan/`. Ruling:
adapt numeric task-start/task-done bookkeeping to existing task IDs and runner
evidence rather than creating a competing plan or repeating a passing run solely
to append a ledger line. User category/checkpoint rules take precedence.
Pre-flight: B1 fixes preserve the typed boundary consumed by metadata/topology;
v2 streamed regions are reused by v3; v3 is the load/cold host dependency; resource
probes consume that same real load; final native handback waits for every gate.
No interface conflict was found.

## Historical local load-filesystem planning checkpoint — revision 1, 2026-10-03

Task #1553, T032-B-LOCAL-DESIGN; source base and published branch starting point
`5d2aa2ceadd8f4424e3ccaf0249a8bb164f32fae`. The owner requested locally completing
the whole filesystem load module before returning public-client/game work to the cloud.
The existing managed Windows worktree is reused on `codex/1553-load-filesystem`, created
from the verified remote checkpoint and immediately published; cloud source is preserved.

The [specification revision](spec.md#local-load-filesystem-continuation--revision-1-2026-10-03)
covers T032-B1/B2/B3 and the filesystem qualification part of B5, including native Linux
CI. It preserves accepted ordinary-save results, current archive/type/schema limits,
one B1 decision, exact library/source/history/config preservation, original legacy
recovery and truthful typed outcomes. Current source is still the initial Load slice:
nine GREEN entry cases, five unfixed review defects and one saved/unverified fixture fix.

Status: the owner explicitly approved exact specification revision 1. The updated
[execution plan revision 1](ordinary-load-plan.md), [namespace data model](data-model.md),
research decisions and dependency-ordered tasks describe the entire authorized module.
The new v3 schema is an explicit pending design addendum; no production, tests, catalog,
CI or runtime changed or executed by this local planning block. Next: cross-artifact
consistency, independent concrete-design review and written plan/design approval, then
the causal five-fix block. The local parent owns implementation; an independent
Sol 6.1 XHigh reviewer is required by the owner's latest instructions, overriding
older Astra routing in historical documents. The read-only Astra High consultation
resolved the concrete topology problem (complete trie, stable scratch anchors and
protected boundaries); it is not implementation/native evidence.

The incremental Spec Kit planning phase reuses this existing feature and load plan
instead of overwriting the accepted historical checkpoint with setup-plan's template.
It adds the concrete namespace data model, updates existing research/quickstart and
maps LOAD-FS-001–008 to B1-FIX, B1-METADATA, B2-NAMESPACE, B3-COLD, B3-RESOURCE and
B5-FS. CI is installed with B1 for bounded native evidence; final B5-FS acceptance
waits for all filesystem gates. Existing B1/B2/B3/B5 aggregate tasks remain open.
Optional commit hooks are fulfilled by the reviewed documentation checkpoint;
no unrelated global agent-context rewrite is required for this existing feature.

Resource controls are proposed and reviewed with the written plan: load children
768 MiB heap/1 GiB RSS/5 GiB owned disk/180 seconds, separate 10-minute phase owners
and 15-minute runner commands. These account for source/extraction/live bytes and
two full before/after journals; they do not change save-probe bounds or certify
arbitrary larger old trees. Safe resource refusal and missing native execution are
explicit non-passes. No latency or model-cost saving has been measured for this plan.

No exact platform-flag cause is known; successful archive/filesystem checks before the
flag and later GitHub publication do not identify a triggering operation. No screening
experiment is proposed. Windows execution is local; Linux needs actual CI results.
Whole Load/client/live-GM/overall acceptance remains open.

Planning checks: Spec Kit prerequisites resolved the exact feature and existing tasks,
research, new data-model and quickstart. The read-only equivalent consistency pass
mapped 8/8 local requirements to six dependency-ordered execution owners, with no
unmapped local work or identified critical/contract inconsistency. It checked preserved
constitution/TDD/authority/GM boundaries, concrete types, budget/dependency/approval
states and native evidence limits. Local Markdown file links and git diff --check
passed. This is document verification; no build, category discovery, runtime test or
CI ran. Independent Sol 6.1 XHigh reviewed the full eight-document diff
`6dd0dbeb52b42a45934e957ad45b8d5119fd1ce3..8ca3fbcab5ba92f0ffe2db1c109229246e589eac`
and related publication/scope/codec/load/acquisition source: PASS, no actionable
design or preserved-contract finding. No reviewer edits/builds/tests/CI. Written
execution-plan/v3 approval remains pending; all implementation/native gates remain open.
Remote full SHA was verified through ls-remote, and GitHub API checkpoint/data-model
blob identities matched Git with decoded document readback. This is publication
verification, not final clean-checkout/runtime restoration.

## Historical T032-B1 admission RED fixture correction WIP — 2026-10-03

Corrected-casing tests and earlier outcome evidence are remotely verified at
`39a3e9245ee2de4f9d12e645eed94bb830b7959f`, tree `52067fa0e9928cfada08f895fe190354289f96f0`.
The [alias build/plan](recovery/evidence/load-alias-build-plan-20261003/manifest.json)
passed in 4:13.1540426. The [first fourteen-case run](recovery/evidence/load-alias-first-red-20261003/manifest.json)
completed all fourteen with no skips and complete cleanup in 12.3553662 seconds.
Thirteen fail at the intended new load boundary: actual committed aliases, missing
canonical imported authorities, invalid aliased config, or the too-late selected-source
collision guard. One is a **noncausal fixture preparation failure**, before Load:
`shining_abode_state.json` was seeded as `{}`, which the current public save producer
refuses. This must not be counted as a Shining load RED.

The fixture now uses `ShiningAbodeState.CreateDefaultState()`, the same supported
fallback already used by the resource-owner composer for absent Shining state. Failed
producer diagnostics are also printed before the fixture assertion. Production,
selectors, category membership and all outcome semantics are unchanged. Next: publish
this bounded fixture/evidence correction, fresh build and rerun the unresolved alias
owner only to establish its complete causal RED before the five production corrections.
No passing entry or outcome cohort is repeated; all wider gates remain open.

## T032-B1 outcome RED and casing-contract correction WIP — 2026-10-03

The regression checkpoint is remotely saved at `654a1bdcc9e63d4ab036b5d70cc32951c54d4052`,
tree `c800a5dcb5f7a866fb262e699fe5a62e09680bab`. Its updated catalog blob
`6793a786382513f3940ff5f2c389f8f1e5116ea7` and tree/commit/ref completed normally;
remote SHA was read back. [Fresh build/PlanOnly](recovery/evidence/load-review-initial-build-plan-20261003/manifest.json)
passed in 3:49.4131828 and discovered sixteen cases, with zero executed.
The separate [outcome RED](recovery/evidence/load-outcomes-red-20261003/manifest.json)
completed 4/4 cases in 10.8518025 seconds: three intended outcome failures and one
unchanged committed-refresh control pass, no skips, complete owned cleanup. Each
failure reaches its named actual recovery/preparation/after-generation boundary.

Scoped source re-review corrected an initial review assumption before running the
case-name tests: the current Windows producer can emit case-renamed fixed authorities,
because AddDirectoryToArchive preserves actual enumerated relative spelling. Therefore
blanket case refusal would narrow current-produced saves. The accepted narrow correction
is to validate the original archive/manifest/hashes first, reject Trim/resolver mismatch,
then map only a finite explicit authority set to canonical constants before collision,
required-path, extraction and image planning. Preserve exact payload bytes except the
already-approved detached profile projection; arbitrary Linux names stay ordinal.

Eight corrected positive cases require canonical materialization, bytes and runtime
state. Together with four Trim refusal cases, invalid aliased config and a selected-source
collision caused by authority mapping, the alias owner now has fourteen cases. The old
eight refusal expectations were only compiled/discovered, never executed. Current
production remains unchanged from 654a1bdc; none of the five P2s is fixed yet. Next:
publish this corrected test/evidence WIP, fresh build/PlanOnly, then only alias RED.
The already-recorded outcome RED and initial nine GREEN are not repeated at this step.

The finite preparation-authority set is not a claim of a complete registry of every
runtime-consumed fixed path. Broader fixed-path casing coverage remains a required
B1/cross-platform qualification question before full-load acceptance; do not silently
case-fold arbitrary payload names. Wider admission/type/history/outcome tests, streamed
metadata, full topology, cold/resource, public clients and native/live gates remain open.

## T032-B1 initial GREEN and alias regression WIP — 2026-10-03

The held fourteen-file source/evidence checkpoint was published after explicit user
approval as `fc9c551113d94014d155aa4b35d511b1d3d29f8d`, tree
`5933920f918a69196afb41ad8ef917df95f03dd0`; GitHub ref, ls-remote and clean local
HEAD agree. The [fresh first GREEN](recovery/evidence/load-entry-first-green-20261003/manifest.json)
passed all nine entry cases with no skips, complete selection/owned cleanup in
3:45.7517312 including build. Exact initial RED remains linked below. This is a
bounded first slice, not acceptance of full T032-B1 or public Load.

Independent GPT-6 Astra XHigh review found a concrete archive/canonical spelling
mismatch: ZIP normalization retains leading/trailing spaces, while `ResolvePath`
trims the complete relative name. Consequently an imported ` config.json` can bypass
settings decoding, a leading-space saves prefix can add a previously absent library
member, and an ephemeral alias can bypass omission. Exact selected-source aliases
already reject through resolved collision checks; existing canonical library files
also fail the absent-before preflight rather than being overwritten.

The completed independent review found five P2s. In addition to Trim aliases, Linux
case aliases can pass case-insensitive archive validation and then omit exact canonical
soul/resource/config/profile paths; prior B1 recovery failure during lease acquisition
can be mislabeled a safe NotLoaded result; combined failed preparation/cleanup loses
its follow-up flag; and Uncertain after generation publication incorrectly claims the
old generation as established. These are current-path defects, distinct from later gates.

Twelve isolated archive spelling/casing cases and four actual outcome cases are now
owned by narrow `portable-load-alias-admission` and `portable-load-outcomes` categories.
The committed-refresh case is an unchanged passing control. Null-by-default hooks after
closed archive extraction and before failed-preparation cleanup enable causal fault
injection; no outcome/admission bug has been fixed yet. No already-passing nine-case
repeat belongs to this RED. Next: publish this test/evidence WIP, fresh build/PlanOnly
and the sixteen-case causal run, then fix the five demonstrated boundaries and run
affected entry/alias/outcome owners. Broader metadata/topology/cold/resource/client/native
gates remain open. These new cases have not run at this source checkpoint.

## T032-B1 causal RED and first same-shape implementation WIP — 2026-10-03

The frozen first source checkpoint `3e07eabdf6c7ad5710745531016d52b42684d321`
(tree `8beed8d2704a36e15b1747903910966da39f3d00`) is remotely verified and
[restored from GitHub](recovery/load-entry-restoration-20261003.json) in a fresh
empty clone: 4,393 tracked files, exact HEAD/tree, clean checkout, connectivity
fsck and all six changed files byte-identical. No test ran in that restore.

On that exact clean source, [fresh integration build/PlanOnly](recovery/evidence/load-entry-build-plan-20261003/manifest.json)
passed in 3:21.6191168 and selected exactly six methods/nine cases. The
[causal RED](recovery/evidence/load-entry-red-20261003/manifest.json) completed
9/9 cases in 8.2643732 seconds: 0 passed, 9 failed, no skips, complete owned
cleanup. All fixtures produced valid current saves and reached the explicit
NotImplemented entry; positive/rollback cases missed preparation/B1 cuts and
negative cases required their precise collision or generation-bound guard.
This is the intended RED, not the historical original Load diagnostic.
Environment: Debian 13 x64, .NET SDK 10.0.401/runtime 8.0.31, PowerShell 7.6.6;
fresh owned CLI/XDG/HTTP/plugin/scratch/temp, unchanged HOME/CODEX_HOME, reused
immutable packages, processor count 1, telemetry off, certificate generation false.
The exact commands, hashes, build log, plans, summaries and causal TRX are linked.
Oversized discovery logs are hash-recorded rather than embedded.

The next source WIP connects owned non-legacy extraction, selected-source/type/hash
validation, detached profile/settings preparation, complete live/incoming file images
and deletions, one replacement-generation B1 decision, and typed post-commit follow-up.
The library and selected ZIP are excluded from all replacement writes; source topology
collisions and nested-bound loads reject before lifecycle acquisition. Existing live
config is retained exactly when absent from the archive; both absent uses detached
standard defaults without persistence. Public bool/client load and the shared codec
are unchanged. This first same-shape source deliberately still refuses destination
topology conversions; required T032-B2 remains open.

**Not yet run on this implementation:** build/GREEN, wider admission/outcome tests,
metadata/resource/cold/native/client checks or independent review. The shared v2
metadata 1 MiB producer limit is still present and must be replaced/qualified within
T032-B1. The nine entry cases are a bounded first causal slice, not complete T032-B1
acceptance. Next: preserve this source/evidence WIP remotely, build and run only the
same nine changed cases, then expand the connected admission/outcome and metadata proof.
No complete suite, accepted-save sweep, application/provider or installation ran.
No GM-authored contract change; T032-B1 through B5, full T032/T033/B4/B5 remain open.

## T032-B1 first causal entry WIP — 2026-10-02

Independent Astra XHigh design review and coordinator acceptance completed at
`05105d58d9a4167bb96c446ea3c26eefabe47122`, tree `302c240a57120a836b34906429345c61ac2d60b4`.
All three admission/source/settings P2s and the final per-member writer-flush P2 are closed.
T032-B0 is complete; T032-B1 implementation remains open. The approved [load plan](ordinary-load-plan.md)
and source/ref branch remain unchanged. Work is cloud-only; no desktop/native handoff.

This first source/test WIP adds `Services/SaveLoadService.Loading.cs` with only the typed
result and an explicitly unimplemented internal entry returning NotLoaded plus
NotImplementedException. Public `LoadGameAsync`, actual callers and the shared codec are
unchanged. `PortableLoadReplacementTests.cs` adds six integration methods/nine cases:
current-producer connected replacement preserving the complete library and root import.zip;
named second-member rollback; archive/retained-live config normalization (two); both-absent
defaults without persistence; direct same-root bound refusal followed by an allowed unbound
load; exact/ancestor/descendant selected-source collisions (three). Each fixture owns its
mutable root, uses the real public save producer and validates its soul/resource archive
before the load call. Negative cases assert precise guard type/message; generic missing
behavior cannot pass them. Positive/rollback cases require actual preparation and B1 cuts.

The new `portable-load-entry` owner explicitly selects those six methods; `tests/selection.json`
now describes only this nine-case causal block. Three-minute category bound, no frontend build,
no shared fixtures. No accepted-save owner, shared-codec runtime repeat, old Load diagnostic or
whole-suite run belongs to this invocation. Cold/resource, full topology, actual clients and
native proof remain later gates; these tests do not imply them.

**Not run at this source checkpoint:** fresh build, PlanOnly, behavioral RED, catalog audit
or any application/provider. Preserve this exact WIP remotely before execution. Next commands,
from the checkout with task-local supported toolchain/telemetry opt-outs and fresh mutable
CLI/XDG/HTTP/plugin/scratch/temp directories (HOME/CODEX_HOME unchanged, existing immutable
NuGet packages reused): `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category portable-load-entry -PlanOnly -Parallelism 1`,
then the same category with `-NoBuild` only after the successful fresh selected build.
Record expected/completed counts and fixture/setup/compiler failures separately from genuine
missing-behavior RED. Publish exact evidence before the connected implementation continues.

## T032-B0 ordinary-load design WIP — 2026-10-02

### Writer-buffer clarification — 2026-10-02

Scoped independent review of `b84f782be10b053a86ba8751f3e3af1f0a81cc4b` closed the three
admission/settings/source P2 findings and accepted the connected same-shape core direction.
One precise codec P2 remained: `Utf8JsonWriter(Stream)` holds encoded bytes until Flush.
The [metadata algorithm](ordinary-load-plan.md#streaming-metadata-algorithm-for-t032-b1)
now requires header flush, a flush after every completed member on the same writer/JSON
state, and tail flush before the final length-slot patch. Its causal test observes bytes
arriving before all members are emitted, zero pending bytes after each flush and peak
pending encoding independent of total member count for a fixed largest member/token.
This two-document correction changes no runtime/tests and runs no build/test/audit.

Publish this correction for the same reviewer's focused readback. The reviewed T032-B1
causal test/core TDD may then begin; implementation of the shared metadata writer stays
held until that readback arrives. Public bool/client cutover, full topology, cold/resource
envelope and native/client/provider gates remain open. Work remains cloud-only while the
owner's Windows computer is offline; no desktop handoff is part of this checkpoint.

### Scoped design-review correction — 2026-10-02

The initial four-doc WIP is remotely saved at `9edb04afbcd5e713df39c1b2bf31ecd5783f01f2`,
tree `83eb084b736f046bc08277b22eb7a4b583136189`. Fresh GitHub-only clone restored 4,391
files, clean HEAD/tree, all four changed blobs and required instructions; connectivity
check passed. No build/test/audit ran. Sole writer resumed from that verified clean SHA.

Independent Astra XHigh design review found three P2 gaps, no P1 or owner-policy question.
The [load plan](ordinary-load-plan.md) now source-verifies and defines: (1) protect the
exact admitted archive path even at `game_session/import.zip`, reject incoming file/topology
collisions before mutation, and preserve it through rollback/cold recovery; (2) strictly
decode and normalize both archive and retained-live config on a detached fresh receiver,
then apply the resolved baseline after commit while preserving exact disk bytes, including
`consoleFontSize:0` versus preview 28/default 20; (3) reject an existing same-root generation
binding before load preparation, preserving the stale fence. Actual console main-menu and
browser replacement call contexts are unbound at load entry, so neither path is disabled.
A direct nested-bound load case is required separately from the old bound-writer test.

The same correction makes the T032-B1 metadata gate concrete: retain v1 and existing v2
framing, stream header fields and patch the private length slot, parse the bounded physical
metadata region with explicit token-memory behavior, preserve arbitrary supported property
order and decoded strict field checks, then validate the complete descriptors/regions/hashes
before recovery mutation. The narrow causal/compatibility test list and exact existing
consumer owners are recorded. This scoped review can clear that algorithm and the connected
same-shape core without another unspecified codec design loop. Full topology, cold/resource,
public callers, native Windows and provider/gameplay gates remain required and open.

Status remains **docs-only WIP awaiting the same reviewer's scoped re-review**. This revision
changes the same four feature documents only; historical maps, production, tests, category
catalog, selection and evidence are untouched. Source reads and diff/link/consistency checks
are the only local verification. No runtime/load diagnostic, build, catalog audit, desktop
handoff or installation ran. Source/ref ownership returns after exact remote verification.

Source/ref ownership resumed on `codex/1553-save-windows` at clean verified
`2ffb84572d12f2ad47b219e33ee7b50ccf0e5418`, tree
`4c57bd15eaedf9ba463f2dd4e6c034cd36e287f5`; GitHub ref and `git ls-remote` agreed.
The [ordinary-load implementation plan](ordinary-load-plan.md) preserves the useful
current-source report at accepted `ddaade72` (production unchanged through the entry
SHA), reconciles it with this canonical feature and adds T032-B0–B5 in [tasks](tasks.md).
The historical [cutover](save-load-cutover.md) and [admission](save-load-admission.md)
source maps remain unchanged; the new load plan explicitly supersedes their tentative
topology limit and unresolved both-absent settings baseline.

The first connected block is owned archive preparation → complete live/incoming images
and deletions → the same B1 replacement/generation decision → typed outcome and canonical
runtime refresh. It includes the replacement-specific image adapter and streaming the
existing v2 metadata frame without its save-only 1 MiB whole-header cap/allocation;
existing v1/v2 evidence, type/link/schema/region checks and all archive budgets remain.
Incoming limits do not cap the independently larger old deletion inventory. The whole
save library and selected ZIP stay outside replacement through commit/rollback/cold recovery.

Full valid file↔directory conversion is a required T032-B2 design/recovery gate before
public caller cutover, not an optional final limitation. Both-absent config uses detached
standard fresh `GameSettings` defaults from the existing product startup contract, keeps
disk absence and affects runtime only after confirmed commit. Missing archive config
with admitted live config retains its exact persisted bytes. Source references and causal
cold/default tests are in the load plan; no mutable preview or new initial-profile authority.

This checkpoint changes only four feature documents. No production, tests, catalog,
selection or historical evidence changes; no test/build/PlanOnly/catalog-audit, original
Load diagnostic, application, provider or installation execution. Self-checks are
source/ref identity, documentation diff/links and the Spec Kit consistency pass; they do
not prove runtime behavior. The existing task-local toolchain can be reused after reading
its environment script; no toolchain setup or dependency reinstall was needed here.

**Initial checkpoint status:** docs-only WIP; the subsequent review/correction status is recorded above. Publish and verify this small
checkpoint before review; the next action is coordinator-owned independent GPT-6 Astra
XHigh design review of the connected boundary, streamed metadata semantics, topology gate,
settings authority and task/test sufficiency. No production/test implementation before
that disposition. T032-B0 and all load tasks remain unchecked; ordinary-save acceptance
below is unchanged. Full T031/T032, T033/live clients/GM, overall B4/B5 and full platform
acceptance remain open. Quiescent storage proof cannot stand in for provider confirmed-stop
or actual gameplay. No GM-authored schema or gameplay contract changes in this plan.

<a id="active-t032-a--ordinary-save-creation"></a>


## Accepted ordinary-save capability — 2026-10-02

The coordinator accepted ordinary-save T032-A, T032-A1, T032-A1-RESOURCE, T032-A2 and
T032-A3 at `ddaade72ae44936f6cf61970afb2bc80225b7731` after independent GPT-6 Astra XHigh
spec and code/test/evidence PASS and terminal GitHub restoration readback. The proof-only
supplement was fetched into the same fresh clone: 4,390 tracked files, clean checkout,
exact final HEAD/tree and all 81 changed files verified. This is ordinary-save acceptance;
Load, full T031/T032, live clients/GM, T033/B4/B5 and overall platform acceptance remain open.

Next: review the reconciled [ordinary-load plan](ordinary-load-plan.md) and T032-B tasks
above before implementation. The historical [cutover](save-load-cutover.md) and
[admission](save-load-admission.md) maps retain their source identities. The known original
Load 0/2 diagnostic remains preserved; this continuation neither reopens nor replays it.

The accepted Linux continuation began from clean `24f3a1ad19b8bcbe854dc6643755773ea1c8a1ea`
(tree `ea546bc3022d06a516127d3c3705cfe6c8d461fd`). The coordinator accepted the
preceding bounded Linux storage 16+10 and caller/resource 5-new+33-existing+3-resource
blocks after actual independent GPT-6 Astra XHigh final spec/quality PASS, no remaining
findings. The three-string evidence sanitization correction at `24f3a1ad` is included.
These exact historical execution sources and manifests remain authoritative; the
older pending-review wording below records the historical handoff, not current status.

This continuation leaves runtime unchanged. One fixture now seeds retained legacy residue directly and asserts the exact real admission guard; its existing method moved to one narrow owner without changing catalog membership. Fresh successful unit/integration
builds at `f331a523` are source-equivalent through this entry checkpoint; relevant trees,
runner/catalog and binaries are verified before `-NoBuild` reuse. Debian13 x64,
.NET SDK10.0.401/runtime8.0.31, PowerShell7.6.6; owned fresh CLI/XDG/HTTP/plugin/scratch/temp
roots, immutable existing NuGet packages, unchanged HOME/CODEX_HOME, telemetry off,
certificate generation false, processor count1 before startup. Locked Node24.19.0/npm11.9.0
dependencies are already installed; unchanged package/lock identities are verified.

| Bounded command group | Current result and scope |
| --- | --- |
| consumer-contracts + profile-consumers | [Preserved partial](recovery/evidence/save-linux-consumer-preparation-failure-20261002/manifest.json):22/23 formal pass in20.0089718s =13 real pass +9 Windows-only no-ops;1 fixture-preparation failure, [profile3/3 GREEN](recovery/evidence/save-linux-profile-green-20261002/manifest.json) separately in8.7747541s at65f0b219; complete cleanup. Five portable static counterparts already accepted, not repeated. |
| legacy-residue-admission (one method split from consumer-contracts) | [GREEN1/1](recovery/evidence/save-linux-residue-green-20261002/manifest.json) in9.4941779s at cleanced9b540 after [fresh integration build/PlanOnly1](recovery/evidence/save-linux-residue-build-plan-20261002/manifest.json) in4:01.4468040. Exact guard/type/root, evidence/session/library and cleanup passed; no production change. |
| browser-save-creation + engine-save-presentation | [GREEN11/11](recovery/evidence/save-linux-boundaries-green-20261002/manifest.json) in21.1472112s at89cbf56b, complete cleanup:6 real backend+HTTP outcomes and1 source guard;2 real console save decisions,1 committed-save agent diagnostic and1 synthetic-uncertainty presentation. All required save hooks and exact identities/outcomes asserted. |
| browser-save-presentation + settings-notices + settings-consumers + shell-types | [GREEN](recovery/evidence/save-linux-frontend-green-20261002/manifest.json) at3b01ad5b in19.5306415s:7/7 descriptors,45 formal results =4 Node file cases +39 Vitest cases +2 pure compile contracts. Save Node8, settings Node22, save-handler Vitest8 and settings reconciliation6 retain exact barriers; existing consumer Vitest25 plus2 Node files passed. Production build/typecheck passed in7.8142216s; existing bundle-size/npm-env warnings preserved, no GUI claim. |
| engine-save-player | [GREEN2/2](recovery/evidence/save-linux-engine-player-green-20261002/manifest.json) at clean5b31ba9e in29.2853188s, separate command; both actual save cuts, exact accepted soul/story, terminal artifacts and journal/conflicting archive assertions passed; complete cleanup. |
| engine-save-waiting | [GREEN2/2](recovery/evidence/save-linux-engine-waiting-green-20261002/manifest.json) at clean5b31ba9e in27.1751202s, separate command; both actual save cuts, exact accepted soul/story, terminal artifacts and journal/conflicting archive assertions passed; complete cleanup. |
| engine-save-late | [GREEN2/2](recovery/evidence/save-linux-engine-late-green-20261002/manifest.json) at clean5b31ba9e in23.8487787s, separate command; both actual save cuts, exact accepted soul/story, terminal artifacts and journal/conflicting archive assertions passed; complete cleanup. |

The active selection now inventories31 owners across the complete ordinary-save prerequisite and immediate caller change, including the single-method residue split and explicit Linux-only FIFO/Windows-only original-profile controls. This impact inventory is not an aggregate execution request. Total catalog membership is preserved;13 already-passing real consumer bodies and9 no-ops are not repeated. Execution uses only each
bounded group above; passing storage/caller/resource cohorts are not replayed. Native
Windows original-profile and physical/swap controls keep their existing source-specific
evidence; they supply no Linux proof. Original Load0/2 is separately pre-existing,
regression attribution retracted and wrappers reverted at `958b4645`; it is not rerun.
No bound relaxation, admission bypass or fake phase reach is permitted. Newly exposed
separate dependencies are reported before expansion. Required save cut and terminal
state must be reached for an engine result to qualify.

Bounded consumer execution and independent review/coordinator acceptance are complete at `ddaade72`. The [canonical discovery-only audit](recovery/evidence/save-linux-consumer-audit-20261002/manifest.json) passed at176deddb in10.3228811s:198 owners, unchanged10,503 identities, zero unmapped/stale selectors and zero tests executed. It used supported `-ValidateCatalog -NoBuild` with the verified fresh compatible builds. [Fresh GitHub-only restoration](recovery/save-linux-consumer-restoration-20261002.json) verified exacte0ec857a/tree6d7fabc5 in a new empty directory:4,389 tracked files, clean checkout, connectivity fsck exit0, all64 embedded artifact hashes and428 source-identity assertions across10 new bundles. Nine large generated discovery logs are explicitly hash-recorded but not embedded; exact causal TRX, plans, summaries, build and frontend evidence are retained. No tests/builds ran in the restore and no passed runtime owner was repeated. This proof/status-only supplement leaves runtime/tests/catalog and all executed source identities unchanged. The accepted ordinary-save scope is the five completed tasks named above. Full T031/T032, live clients, real GM/gameplay, T033/B4/B5 and full platform acceptance remain open. No GM-authored contract change. Group1 failure is before the save call: synthetic stale browser marker setup uses the original descriptor-bound create-only writer on Linux (SaveLoadServiceTests.cs:590), raising PlatformNotSupportedException. Ordinary admission is not reached. Exact stack and unchanged source identities are preserved; this does not establish a save regression. Correction is now GREEN with the exact legacy guard; backend/presentation, frontend and all three separate engine owners are now GREEN. The final independent review accepted this causal correction and its evidence.


### Accepted ordinary-save criteria map

These are separate, source-bound cohorts, not one aggregate execution. The earlier common image11/shared40 overlap and must not be summed. Their saved catalog patches remain part of those historical source identities. No previously passing storage/caller/resource cohort was repeated.

| Gate | Existing prerequisite and returned Windows proof | Native Linux proof and disposition |
| --- | --- | --- |
| T032-A1 image/codec/shared engine/adapter | Common image11 at `f343cac1`, shared40 at `fcd71676`, dispatch4 at `26c0327c`; source-derived original-v1 fixture2 at `d4460793` plus exact carrier/runtime26c0327c; ordinary create-only1 at `2b757469` plus carrier. Windows cold14 at `ecf0a923`, format29 at `ec5e5ccf`, boundary14 at `23d94fb7`. | Accepted cold16 at `e5a009c3` and generation6/admission3/FIFO1 at `6322cb31`; accepted public-producer resource3 at `cc087195`. Shared image/publication/codec source remains byte-identical to26c0327c. The coordinator accepted the complete bounded prerequisite at `ddaade72`. |
| T032-A1-RESOURCE | Actual updated Windows public SaveGame producer/resource3 at `36589e5b`:15 children,64/128/near-512MiB full BEFORE/AFTER and cold decisions. Earlier32 format/resource formal results at `ec5e5ccf` remain distinct. | Accepted current public-producer3/15 children at `cc087195`, original768MiB heap/1GiB RSS/3GiB disk/120s per-child bounds; exact archive/library/generation/sentinel checks. Earlier monitor failure2/3 at `ce7750da` and fixture correctionf331a523 remain explicit. The coordinator accepted this resource gate at `ddaade72`. |
| T032-A2 closed ordinary preparation/create-only/outcomes/listing | Returned Windows core19 and boundary13 at `dafd37e5`, bound10 at `e6506c56`, retention/outcomes12 at `b674db67`; historical consumer23 passed bodies and original Load diagnostic separated. | Accepted new static5 at `f22ea963` plus exact catalog and affected ordinary33 at `4e57bd58`, current resource3; current consumer13 real passes at `06025c16` plus causal retained-residue1 at `ced9b540` and9 Windows-only no-ops excluded. Listing/schema/source/lease/retention semantics retain real assertions. The fixture-only direct residue seed and exact admission guard passed final independent review; no production correction. |
| T032-A3 profile/browser/console/autosave continuation | Windows profile3 plus original physical-receipt1 at `36589e5b`; engine player/waiting/late each2 at `7c52a5e6`; save Node8/handler Vitest8 at `b674db67` and settings Node22/reconciliation6 plus build at `7c52a5e6`. | Profile3 at `65f0b219` is2 actual leased-mirror scenarios +1 API/reflection guard. Backend/presentation11 at `89cbf56b` is6 actual browser+HTTP outcomes,1 source guard,2 console save decisions,1 actual committed-save/agent diagnostic and1 synthetic uncertainty presentation. Frontend at `3b01ad5b`:4 Node file cases (save8/settings22 scenarios),39 Vitest cases and2 pure compile contracts, production build/typecheck; response-loss/unmount/required-refresh barriers and exact created identity retained. Real player/waiting/late owners each2 passed separately at `5b31ba9e` with existing bounds and actual save-cut/accepted-state/terminal assertions. The coordinator accepted this bounded caller capability at `ddaade72` after final independent review. |

Current full ordinary-save selection includes the image/codec/dispatch/original-v1/create-only/cold/generation/admission/FIFO/resource prerequisites and all immediate save/profile/browser/console/frontend consumers, with the Windows-only original-profile owner explicit. This is an impact/OS inventory, not a request to execute the union. Linux FIFO and Windows original physical controls retain their separate platforms; historical Windows-only consumer returns are not Linux proofs.

Separate unfinished scope: Load (known original diagnostic0/2), full T031/accepted-turn and receipt migration, live browser/console gameplay acceptance, real provider/GM continuity, T033/B4/B5 and overall platform acceptance. Automated backend/component/engine fixtures do not establish live GUI or GM acceptance. No GM-authored contract changed. Only the five ordinary-save task flags named above are now complete; all wider task flags remain unchanged.

### Final GitHub restoration and writer handoff — 2026-10-02 17:58 UTC

Complete bounded source/evidence is published at
`3f50036ce07b8406ac5fd50fe23d666100785c23`, tree
`6ecd89e2dc9aef93475990b20ac0fda3ef2b3027`; remote SHA and every changed file
were verified. A [fresh GitHub-only restoration](recovery/save-linux-caller-restoration-20261002.json)
into a new empty directory restored **4,313 tracked files**, exact HEAD/tree,
clean worktree and successful connectivity fsck. All **40 stored artifact hashes**
and **255 source-identity assertions** in nine native Linux bundles matched;
there are no omitted artifacts. The first five-case source is explicitly commit
plus its verified exact catalog patch, now delivered as normal content. No runtime
tests ran in the restoration. Production, TestSupport and crash-host source remain
unchanged from the entry checkpoint; returned Windows and earlier Linux evidence
are intact.

This proof-only supplement does not change tests, runtime, catalog or executed
source identities. Source/ref ownership returns to the coordinator with the frozen
diff for actual independent Astra XHigh review and acceptance. The block remains
unreviewed until that gate completes. All broader unfinished tasks remain open.
Original hash-bound build logs retain their generated terminal blank lines;
log-only diff whitespace warnings do not alter the source/document checks or
stored result integrity. No passing cohort needs repetition for this supplement.

## Linux caller/resource handoff WIP — 2026-10-02 17:52 UTC

Bounded execution is complete; **independent review and coordinator acceptance are
pending**. The five new static caller checks passed 5/5 at `f22ea963` plus its exact
saved catalog patch; the five existing affected caller owners passed 33/33 at clean
`4e57bd58`. Both retain exact earlier sources and complete evidence below. No
ordinary-save production defect or change was needed. The only existing C# source
change is the narrowly guarded process-exit sampling correction in the resource
fixture, causally justified by the separately preserved 2/3 result at `ce7750da`.
All returned Windows source/evidence and the accepted prior Linux storage 26 remain
intact; no unchanged caller or storage cohort was repeated.

The [corrected resource PlanOnly](recovery/evidence/save-linux-resource-corrected-plan-20261002/manifest.json)
confirmed 3 cases in **5.4518381 seconds**, zero executed. At clean evidence-containing
source `cc087195af6fb507d9c9e1edc6612e214a536aec`, the separate
[current-producer Linux resource command](recovery/evidence/save-linux-resource-green-20261002/manifest.json)
passed **3/3 in 2:20.9029096**, exit 0, with all **15 actual child reports**: producer,
pending publication/recovery and committed publication/recovery for 64/128/near-512 MiB.
It reused the freshly corrected `f331a523` unit build; intervening changes are evidence
and documentation only. The shared monitor correction is the reason all 3 resource
cases were requalified. All exact before/after hashes, generation, full library,
outside sentinel, archive and owned cleanup assertions passed.

The near-512 MiB public SaveGame ZIP was **536,973,730 bytes**, expanded **536,815,510**.
Maximum child-reported peak working set was **619,077,632 bytes**, parent-sampled RSS
**619,835,392**, sampled logical disk **2,673,468,064**, and child duration
**66,889.7966ms**. Original 768 MiB heap/1 GiB RSS/3 GiB disk/120-second bounds remain unchanged.
The command ran alone with no build/restore/audit overlap. No failure, skip,
duplicate, unrun case or timeout occurred; complete owned/runtime cleanup and zero
owned synthetic temp remainders are recorded. The measured limits are this bounded
native Linux result, not an optimization or whole-platform claim.

Source/case/artifact identities, original/stored hashes and unrounded 15-child
measurements are preserved in the bundles. The discovery-only audit is197 categories/
10,503 identities with zero unmapped/stale selectors, not a test run. The active catalog
is normal Git content; no carrier, outstanding approval or uncertain mutation remains.
The seven-owner selection is this bounded block only and must be reconciled against
full intended change before integration. Remaining native consumer/profile, engine,
frontend and deferred live-client work follows separately. Original physical-read
controls, historical Windows-only swap behavior and known original Load diagnostic
retain separate scope. T032-A1/A2/A3/full T032-A and all wider unfinished tasks stay open.
No GM schema, gameplay contract or prompt/example capability changed.

Next: verify a fresh GitHub-only checkout of the final evidence, freeze the exact diff,
and return sole source/ref ownership for actual independent GPT-6 Astra XHigh review.
No passed cohort is repeated solely for review.

### Fresh ownership audit and corrected build — 2026-10-02 17:46 UTC

At clean corrected source `f331a5231a00823ae33f3fd87b11b349a46aa034`, the
[discovery-only audit](recovery/evidence/save-linux-catalog-audit-20261002/manifest.json)
passed **197 categories/10,503 identities**, zero unmapped/stale selectors and
**zero executed tests**, exit 0 in **5:39.3676455**. Fresh integration build
**272.3735738 seconds** and unit build **54.7081824 seconds** both completed;
owned/runtime cleanup is complete. This supplies the fresh corrected unit binaries
for the separate resource rerun. No unchanged passing caller/storage cohort is repeated.

The preceding invocation with `-ValidateCatalog -Parallelism 1` was rejected by
PowerShell parameter binding before any setup/build/discovery/test; its exact
failure is preserved in the audit bundle. `-Parallelism` is not in the unchanged
runner's Audit parameter set. The supported documented `-ValidateCatalog` form
then ran successfully; all actual runtime commands retain `-Parallelism 1`.
For fresh integration restore, the prior downloaded packages were copied into this
block's own NuGet cache before adding the required Roslyn 5.3.0 dependency; the
predecessor cache was not mutated. No resource child ran concurrently with these
builds, restores or discovery work.

### Resource monitor correction WIP — 2026-10-02 17:36 UTC

The causal 2/3 failure is saved at `dcb0d22a552ceda3e8c419154367532bb608ce88`
before the fixture correction. The monitor now catches only `InvalidOperationException`
from refresh/RSS sampling when `HasExited` confirms the **same owned process** has
terminated, then continues through its existing wait, exact exit-code, final disk,
report/platform/heap/peak-RSS validation and owned cleanup. An unreadable live or
unknown process still fails; no bound, deadline, workload, RSS/disk sample interval,
archive assertion or production source is changed. The existing observed failing
resource test is the causal regression; no unrelated test/category is added.

Fresh unit build is required after this test-source correction. All three resource
workloads require requalification because they share this monitor in all five child
modes; the previously passed64/512 cases are repeated for this explicit dependency,
not for review. Caller5+33 and storage 26 are unchanged and are not repeated. This WIP
is unbuilt/unrun and awaits the same independent review with the complete caller block.

### Resource sampling fixture failure preserved — 2026-10-02 17:33 UTC

At clean `ce7750daba51859a4cab8632d7ed7472bcb450ab`, the separate resource
[PlanOnly](recovery/evidence/save-linux-resource-plan-20261002/manifest.json)
confirmed three cases in 4.2004092 seconds. The isolated
[runtime result](recovery/evidence/save-linux-resource-monitor-failure-20261002/manifest.json)
completed **three formal cases: two passed, one failed**, exit 1 in **2:24.0996788**.
64 MiB and near-512 MiB each completed all five child stages. The 128 MiB producer
and pending publication passed; its pending recovery monitor then raised
`InvalidOperationException: Process has exited, so the requested information is not available`
at `TrustedLocalStreamResourceTests.Run`, line164, reading `Process.WorkingSet64`
after the while-loop's separate `HasExited` observation. This is a parent sampling
lifecycle race, not a demonstrated save/recovery data defect; the remaining128 MiB
proof is incomplete. Twelve validated child reports are preserved in TRX, not fifteen.
Command/owner did not time out; owned/runtime cleanup completed with no synthetic
fixture remainders. Bounds and workload remain unchanged. No full resource pass
or production correction is claimed.

Next: preserve this exact failure before the smallest causal fixture correction;
prove normal child exit during sampling cannot discard its exit/report validation,
while active-child sampling failures and all original safety checks still fail.
Only the affected resource fixture owner requires requalification after that change;
the already GREEN5+33 ordinary caller cases and storage 26 need no repetition.

### Native Linux affected callers GREEN — 2026-10-02 17:28 UTC

At clean source `4e57bd58d05a6565853cf5f07a7e9b3402551e44`,
[affected-owner PlanOnly](recovery/evidence/save-linux-affected-plan-20261002/manifest.json)
confirmed **five descriptors/33 cases** in **4.7829922 seconds**, zero executed.
The subsequent [runtime cohort](recovery/evidence/save-linux-affected-green-20261002/manifest.json)
passed **33/33 in 23.3950927 seconds**, exit 0: entry2, read/refresh9, outcomes10,
retention-release2 and bound-outcome10. Every descriptor/body completed without
failed/skipped/duplicate/unrun cases or timeout; owned/runtime cleanup is complete
and zero owned synthetic temp remainders remain. The unchanged fresh unit build
from the preceding five-case PlanOnly is reused; there is no production correction.
The managed release-seam cases do not claim injected native handle-failure proof.

All five static caller fixtures and their original build/runtime evidence are
preserved at `4e57bd58`, after catalog normalization at `1d329f70`. Next is the
separate current-public-producer resource owner under unchanged protective bounds,
followed by fresh discovery-only ownership audit. These native caller cohorts do
not include the historical nine guarded Windows bodies, original Load, real
engine/frontend or live-client acceptance, which remain separate.

### Five native Linux static caller checks GREEN — 2026-10-02 17:25 UTC

[Fresh PlanOnly/build](recovery/evidence/save-linux-five-plan-20261002/manifest.json)
and [five-case runtime](recovery/evidence/save-linux-five-green-20261002/manifest.json)
used source `f22ea96354f1f18d5a400db01d7c52579b3425d7` plus the exact remotely
saved catalog patch. Both summaries record one changed catalog and fingerprint
`300F1BE329683A1247650BC8482E2EE154BD9739ADE728821C2247C73CFBD2DB`.
The runtime passed **5/5 in 9.0645892 seconds**, exit 0, with all bodies executed,
no skips/failures/duplicates/unrun cases or timeout, complete owned/runtime cleanup
and zero owned synthetic temp remainders. Linked expired-member selection was
asserted at the actual retention boundary; committed archive plus failed follow-up,
no-follow ZIP contents, exact outside/library/generation bytes, mandatory-root
outcome and one-open hard-link metadata assertions passed. These tests confirm
already-implemented behavior: no manufactured RED or production fix was needed.

Normal catalog publication is verified at
`1d329f70bf680819545a0631442cde41f75c5a2d`, tree
`7c96c09fe0f330bc36e6a73201246649d61f0cb3`; catalog and checkpoint bytes match
fetched remote objects. The original tested source identity is not relabeled by
this evidence commit. Next: affected five ordinary caller owners, then separately
current-producer resource 3/fifteen children and discovery-only audit. Independent
review and complete T032-A1/A2/A3 acceptance remain pending.

### Caller catalog normal delivery — 2026-10-02 17:23 UTC

The exact five-caller catalog is now normal `tests/categories.json` Git content:
blob `1dd6d00b183e7aba02e5655173dab545f11dbb15`, 904,946 bytes,
SHA256 `320c752159ab1a93093e08ef72a34be4700c020ebe334446579479040b8e3aaf`.
The [exact patch and identity packet at f22ea963](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/tree/f22ea96354f1f18d5a400db01d7c52579b3425d7/specs/1553-portable-local-storage/recovery)
remain immutable historical provenance; the active packet is retired and must not
be reapplied. No runner/schema change, full catalog reformat, desktop transfer or
new credentials were used. The first immutable blob request lost its approval
window without creating the expected object; ref stayed at f22ea963. The owner
explicitly renewed approval, the same create_blob call was retried exactly once,
and it returned the exact expected blob. No ref mutation was uncertain.

Fresh canonical PlanOnly/build on `f22ea963` plus that exact verified catalog
patch completed exit 0 in **3:18.6223525**, Build-unit **192.709182 seconds**,
with exactly **five planned cases and zero executed**, no timeout and complete
owned/runtime cleanup. No runtime or acceptance claim follows from discovery.
The native five-case runtime cohort uses the same fresh unchanged binaries.

## Linux save caller WIP — 2026-10-02 17:07 UTC

Issue #1553, T032-A1/A2/A3, same branch at verified entry
`0d7f6c89d370716cad374e397898f7dedfdd452c`, tree
`31b4648d1081cbeedb7d386986a386308039f6e2`. The preceding Linux storage 26
block passed actual independent GPT-6 Astra XHigh spec/quality review without
findings and was accepted by the coordinator. Its cold16 and generation6/admission3/
FIFO1 evidence and distinct tested sources below remain unchanged; do not rerun them.

Five new isolated portable caller tests cover pre-existing linked manual destination,
deterministically selected expired autosave link with truthful committed follow-up,
linked descendant exclusion from a complete manifested ZIP, mandatory linked source
root refusal, and public listing of a pre-existing hard-linked archive through exactly
one opened stream. Existing fixture seeding, ordinary entrypoints and observation-only
hooks are reused. All nine historical Windows-only consumer controls remain intact.
The new catalog owner is `portable-save-filesystem-callers`; related owners' stale
Linux exclusions are corrected without changing membership. No production change is
needed or claimed before running these tests. Profile refresh may independently commit
before archive preparation fails; these fixtures do not invent whole-session atomicity.

This WIP is unbuilt/unrun and unreviewed. Publish the small source/evidence checkpoint
and exact catalog patch plus after-image identity before the full catalog upload and
before long verification. `tests/selection.json` records only this bounded seven-owner
block, preserving prior selections in Git; eventual integration must reconcile the
whole intended change. Execute the five new checks first after a fresh PlanOnly build;
then the five affected ordinary save/read/outcome/retention/bound owners, split by their
catalog budgets. Run current-producer resource 3/fifteen children separately afterward
with unchanged 64/128/near-512 MiB, 768 MiB heap/1 GiB RSS/3 GiB disk/120-second child
limits. New ownership requires fresh discovery-only `-ValidateCatalog`, not execution
of the complete inventory. Every runtime uses canonical `scripts/test-csharp.ps1`
with `-Parallelism 1`; `-NoBuild` only follows fresh successful required builds.

The official verified toolchain is unchanged: SDK 10.0.401, runtime 8.0.31,
PowerShell 7.6.6, Spec Kit 1.0.13. This block uses new owned mutable CLI/XDG/NuGet
HTTP/plugin/scratch/temp state, reusing only the preceding downloaded package cache.
HOME/CODEX_HOME remain unchanged and all three telemetry opt-outs, certificate=false
and processor-count1 apply before startup. Spec Kit prerequisite check exit 0 resolves
this feature and tasks. Equivalent scoped consistency review found no changed product
requirement or task gap. Existing isolated checkout is retained under sole writer ownership.

Ruling: add evidence for the agreed static/no-follow/hard-link caller semantics, not
Linux emulation of superseded Windows physical pinning or simultaneous external swaps.
If a new fixture reveals a defect, preserve the causal failure before a minimal fix.
No broad suite, original known-failing Load diagnostic, historical whole-byte resource
experiment, native Windows execution, user saves, provider/browser-live or desktop work.
Coordinator owns independent Astra XHigh review and acceptance. Full T032-A1/A2/A3,
T032-A, Load, engine/frontend and wider platform acceptance remain open. These tests
change no GM-authored schema or gameplay contract, so no GM prompt/example update applies.

## Linux return WIP — 2026-10-02 16:25 UTC

Tracked scope: **T032-A1 native Linux storage qualification**, on returned branch
`codex/1553-save-windows` at verified base
`6161cfd6ad139fe91a3c28ee71bcda1b1e3e7162`, tree
`e40e3f7cf8467ff691fe40a7b32b628080b3fb76`.
A fresh GitHub-only checkout restored 4,240 tracked files with a clean worktree
and successful connectivity integrity check. Source is normal Git content; no
carrier is active. The older feature branch remains untouched. The inherited
Windows evidence dates and results below are retained as recorded.

The returned evidence audit checked 105 stored hashes and all 24 source identities;
independent Astra XHigh accepted the bounded evidence/scope check. No implementation
change is needed or claimed by this preparation checkpoint. At the initial WIP checkpoint the native Linux runs
were **unrun**; the first completed result is recorded below. Official toolchain setup reports verified package hashes and
SDK 10.0.401, .NET/ASP.NET 8.0.31, PowerShell 7.6.6 and Spec Kit 1.0.13; ordinary
network timeouts were resolved by bounded same-source retry. Actual startup
versions and run-specific mutable paths are checked before runner startup. Setup
success, build success and runtime success are separate gates.

Run only the canonical `scripts/test-csharp.ps1` entrypoint, serialized with
`-Parallelism 1`, after a fresh successful PlanOnly build. First select
`portable-storage-stream-cold-phases,portable-storage-stream-cold-before,portable-storage-stream-cold-conflicts`:
source defines nine publication phases, two interrupted 256 MiB before-image
recoveries and five conflict/cleanup cases; actual discovery must confirm 16.
Preserve that result remotely before selecting
`portable-storage-stream-generation,portable-storage-image-admission,portable-storage-stream-linux-fifo`:
six generation ordering, three pre-intent admission and one actual Linux FIFO case.
The FIFO body asserts Linux and must execute. The six-owner selection records
only this bounded prerequisite block; the [prior caller selection](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/6161cfd6ad139fe91a3c28ee71bcda1b1e3e7162/tests/selection.json) remains immutable. Reconcile the full intended save-change selection before
eventual integration; passing this block does not replace caller/resource proof.
`-NoBuild` may reuse only this fresh
successful build while selected project source remains unchanged.

Ruling: omit old publication/dispatch/format/V1/create-only reruns because their
relevant core/runtime/test bytes retain accepted Linux evidence; if the equivalence
assessment is wrong, those omitted boundaries need new focused qualification.
Cold and generation/admission/FIFO are the new native prerequisites. Actual
current-producer resource, caller, engine/frontend and wider client/platform work
remain separate, open blocks. Full T032-A1/T032-A/T031/T032 remain open. No GM
payload/gameplay contract changes, new gameplay examples or full-suite execution
are implied.
### First native Linux cold result — 2026-10-02 16:35 UTC

Both commands tested clean normal source `e5a009c3078934d7b7cd98d894c1fd4d2d3bab59`,
tree `b240460251ca3d463c8641a1b4bc27399b4f808f`, with unchanged runtime/tests.
The [fresh PlanOnly build](recovery/evidence/stream-linux-cold-plan-20261002/manifest.json)
completed in **4:45.5768465**, exit 0, planning exactly three descriptors/16 cases
and executing zero. The subsequent [native Linux cold cohort](recovery/evidence/stream-linux-cold-green-20261002/manifest.json)
used that fresh build with `-NoBuild`, passed **16/16** in **34.1109435 seconds**,
exit 0: nine publication cuts, two interrupted 256 MiB before-image rollback cuts,
and five byte/type/link/generation conflict or committed-cleanup cases. All three
descriptors completed with no failed/skipped/duplicate/unrun cases or timeout.

The three TRX files retain 44 actual child reports: 19 intended abrupt exits at
asserted phase/member indices, 21 successful canonical acquisitions, and four
expected evidence-preserving conflict refusals. Exact before/after/absence,
generation, full library and outside hard-link sentinel assertions passed.
Owned process/runtime cleanup completed and the run-owned temp root retained zero
`boe-*` fixture/runtime directories. This is process-crash evidence only.
Official toolchain provenance, source identities and original/stored artifact
hashes are in the bundles; mutable CLI/XDG/NuGet/temp state was unique to this block,
HOME/CODEX_HOME were unchanged. Evidence publication/review remains distinct from
the tested source. Next: save this result, then qualify the separate native Linux
generation6/admission3/FIFO1 cohort on unchanged fresh binaries. Resource/callers
and full T032-A1 acceptance remain open.

### Native Linux generation/admission/FIFO result — 2026-10-02 16:41 UTC

The first cold evidence was preserved at `6322cb31d766571b63e3054fcecca89aa9f3ec24`
and all 13 saved files matched remote Git objects before this second block.
At that clean evidence-containing source, [boundary PlanOnly](recovery/evidence/stream-linux-boundary-plan-20261002/manifest.json)
confirmed three descriptors/10 cases in **4.3449383 seconds**, zero executed.
The [boundary runtime cohort](recovery/evidence/stream-linux-boundary-green-20261002/manifest.json)
passed **10/10** in **17.5154694 seconds**, exit 0: generation ordering six,
pre-intent image admission three and actual native FIFO one. Both reused the fresh
`e5a009c3` unit build; runtime/test/project/runner/catalog bytes were unchanged.

All bodies executed, all three descriptors completed, with no skips, failures,
duplicates, underfilled descriptors or timeout. Six generation cases asserted
actual publication phase/member ordering, separate cold acquisition, exact fresh
or BOM/case/extension-preserving generation bytes, and present-empty versus absent
members. Three admission cases asserted the boundary was reached exactly once,
zero publication phases and exact typed generation/candidate rejection plus retained
library/sentinels. The FIFO body asserted Linux, successfully created the FIFO,
reached last-member index 2 and observed cold `InvalidDataException` refusal before
earlier rollback; its exact data/generation/journal/library/outside assertions
passed. Owned process/runtime cleanup completed, with zero `boe-*` temp remainders.

This completes execution of the two bounded Linux storage cohorts, pending the
coordinator's independent Astra XHigh review. Fresh GitHub-only restoration is
verified below.
No production, test, category or resource-bound change was needed. Native current-
producer resource qualification and remaining caller/engine/frontend/live-client
work remain open; T032-A1/T032-A2/A3 and full T032-A are not closed by these results.
Both evidence cohorts are preserved at `10dde95aac5517fbd8e6234e2d8a5cd6c17dd314`,
tree `6e05bb5b89279be1698705fdc2040495199932b6`. A fresh single-branch shallow
GitHub clone into a new empty directory restored all **4,263 tracked files**, exact
HEAD/tree, clean worktree and successful connectivity fsck. All **19 stored artifact
hashes** and **76 recorded source-identity assertions** across the four bundles
matched. No runtime tests ran in this restoration. The subsequent proof-only
supplement records this result; it changes no implementation, test,
catalog, selection, executed result or acceptance boundary. Source/ref ownership now
returns to the coordinator for independent review and the next separately bounded
block. The original hash-bound generated build log retains its terminal blank
line; that log-only whitespace warning does not affect source/document checks or
result integrity. Do not repeat passed cohorts for review.

## Current Windows/Linux caller checkpoint — 2026-10-03

Implementation source: `36589e5b6935d197ddeb9441c750237d55610956`,
`codex/1553-save-windows`; evidence supplement `a9f04c635610857fe275dd7dee1792d5a5fbb855`
is published and [restored from GitHub](recovery/save-caller-windows-restoration-20261003.json)
into a fresh clean checkout: 4,239 tracked files, Git object integrity and 85 artifact
hashes across 20 evidence bundles verified. No runtime tests were repeated in that clone.
Independent Astra XHigh accepted the bounded Windows caller block with no remaining
actionable code/test findings. T032-A2/A3 remain unchecked for native Linux qualification
and full cross-platform acceptance. Restore ordinary Git source, not historical carriers.
[Handoff](save-task-handoff.md) gives restore and split verification commands.

Ordinary creation uses one held snapshot lease, a closed create-only ZIP, one B1 image
publication and exact destination/outcome retention through owned cleanup, logging,
retention and UI follow-up. Listing keeps leased seekable streams, raw archive preflight
and existing limits. Profile refresh preserves byte-exact no-ops and original physical
receipt dispatch. Finalization retains typed primary save failures with replacement
priority, and failed retention release blocks continuation. Browser save confirmation
checks actual required refreshed surfaces and the exact created ID; HTTP/network failure,
missing ID or lost ownership latches the known commit and stops later actions. Ordinary
refresh semantics remain unchanged, and already sent requests are not claimed cancelled.

Native Windows evidence is separated by tested source; these counts are not one run:

| Source | Evidence and bounded result |
| --- | --- |
| `dafd37e5` | [Core](recovery/evidence/save-core-windows-green-20261003/manifest.json): 19/19, 27.566s; [browser/console/entry](recovery/evidence/save-caller-boundaries-windows-green-20261003/manifest.json): 13/13, 1:03.504. |
| `e6506c56` | [Bound correction](recovery/evidence/save-bound-green-retention-red-20261003/manifest.json): 10 bound controls passed; the same 12-case run had two causal retention-release failures, corrected below. |
| `b674db67` | [Retention/outcomes](recovery/evidence/save-retention-windows-green-20261003/manifest.json): 12/12, 29.553s. |
| `b674db67` | [Consumer partial](recovery/evidence/save-consumer-partial-green-20261003/manifest.json): 23/24 integration passed; the original Load failure is pre-existing, unit profile3 unrun in that invocation. Current consumer owner retains the 23 ordinary controls; profile3 has a separate owner. |
| `b674db67` | [Frontend partial](recovery/evidence/save-frontend-partial-green-20261003/manifest.json): save Node8 and actual handler Vitest8 passed, shell types/settings consumers passed; formal38 passed, 5/7 descriptors complete. The old source guard failed and settings reconciliation was unrun there. |
| `7c52a5e6` | [Settings controls](recovery/evidence/save-settings-controls-green-20261003/manifest.json): Node22 scenarios plus reconciliation Vitest6, formal7 across two complete descriptors; production frontend build/typecheck passed. |
| `7c52a5e6` | [Player](recovery/evidence/save-engine-player-green-20261003/manifest.json): 2/2, 3:43.752; [waiting](recovery/evidence/save-engine-waiting-green-20261003/manifest.json): 2/2, 3:16.316; [late response](recovery/evidence/save-engine-late-green-20261003/manifest.json): 2/2, 2:59.541. All three completed selection and owned cleanup. |
| `36589e5b` | [Profile controls](recovery/evidence/save-profile-controls-green-20261003/manifest.json): actual original Windows receipt1 and existing profile consumers3, all4 passed in 59.152s including fresh XML build. Prior digest-text/BOM fixture failures are preserved; production was unchanged by those corrections. |
| `36589e5b` | [Current producer/resource](recovery/evidence/save-current-producer-resource-green-20261003/manifest.json): 3/3 passed in 4:29.756, all15 owned children within original bounds. Public SaveGame completed at 64/128/near-512 MiB; full BEFORE/AFTER decisions and cold recovery preserved exact library/generation/sentinels. |

The current near-512 MiB ZIP is 536,973,574 bytes. The producer peaked at 504.69 MiB
RSS, publication children at 44.25 MiB (maximum parent-sampled observation; child-reported
maximum 44.15 MiB), and sampled logical disk at 2,684,879,529 bytes.
Its longest child was the producer at 73.06s sampled wall time, below the unchanged
120s deadline; heap768 MiB/RSS1 GiB/disk3 GiB guards remain unchanged.
All required Windows execution cohorts are now complete. Original Windows profile
verification is Windows-only and must not be included in Linux commands. Engine player,
waiting and late are separate bounded commands; their measured four-minute category
budgets preserve the real accepted-state boundary, not a speed improvement. Resource
qualification is separate and retains its original per-child bounds. No native
lease-dispose fault proof or live browser GUI acceptance is claimed.

The original Load diagnostic is a pre-existing extraction-before-acquisition admission
collision at `6dc8c218` and `0f4fe247`, already documented in
[save-load-cutover.md](save-load-cutover.md).
The [logged diagnostic](recovery/evidence/save-original-load-diagnostic-20261003/manifest.json)
remains 0/2. Independent review retracted the P1 caller-regression attribution after
counterevidence; both speculative Load refresh wrappers were reverted at `958b4645`.
Original Load body/receipts remain separate/open. `portable-save-original-load` is a
retained known-failing diagnostic, not part of save-creation acceptance or selection.

Independent review caught real closing, retention-release and browser-refresh defects;
logged counterevidence corrected the Load hypothesis. The corrected bounded Windows
caller block passed final independent Astra XHigh review; no measured savings or wider
platform acceptance is inferred. Two direct original-receipt fixture issues (hex text case and BOM
decoding) required corrections; neither changed production or raw receipt assertions.
The [discovery-only audit](recovery/evidence/save-caller-catalog-audit-20261003/manifest.json)
verified 196 categories and 10,498 identities with zero unmapped/stale cases and zero
tests executed; the caller selection contains 18 distinct category owners.
Linux paths are implemented but have not run natively here.
Linux prerequisite/caller qualification, full T032-A1/T032-A/T031/T032
and wider game/platform acceptance remain open. No GM/gameplay contract changed, so
client-owned save transport needs no GM prompt/example capability update.

### Historical prerequisite and RED evidence

These results belong to their named historical source, not to the caller checkpoint:

- Windows prerequisite source `23d94fb7` was independently accepted at evidence
  `0f4fe247`, including fresh 4,080-file/37-artifact readback. [Cold 14/14](recovery/evidence/stream-windows-green-20261002/manifest.json)
  belongs to `ecf0a923`; [format 29/29/resource 3/3](recovery/evidence/stream-windows-resource-green-20261002/manifest.json)
  to frozen `ec5e5ccf`; [boundary 14/14](recovery/evidence/stream-windows-boundaries-green-20261002/manifest.json)
  to `23d94fb7`. The resource rows preserve 15 sequential owned children and full
  BEFORE/AFTER at 64/128/near-512 MiB. Near-512 ZIP is 536,973,572 bytes and BEFORE
  536,973,604 bytes; v2 publication peak RSS is at most 44.12 MiB versus the producer's
  502.68 MiB. Original per-child bounds remain 120 seconds, 768 MiB heap, 1 GiB RSS and
  3 GiB disk, with an exclusive eight-minute owner. No archive support ceiling changed.
  [Fresh restoration](recovery/stream-windows-restoration-20261002.json) retains source/artifact identities.
- [Public-entry RED](recovery/evidence/save-caller-entry-windows-red-20261002/manifest.json)
  at frozen `23d94fb7` reached completed archive preparation but zero B1 decisions;
  [helper RED](recovery/evidence/save-helper-windows-red-20261002/manifest.json) and
  [outcome RED](recovery/evidence/save-outcomes-windows-red-20261002/manifest.json) at
  `7499f91f` preserve 1/9 and 0/5 respectively. Later corrected core GREEN is separate.
- [Browser backend RED](recovery/evidence/save-browser-windows-red-20261002/manifest.json)
  preserves five actual old-recorder-route failures. [Browser presentation RED](recovery/evidence/save-browser-presentation-red-20261002/manifest.json)
  compiled and exercised five intended failing notice scenarios; the failed Node file's
  adapter counts do not mean no scenarios executed. Earlier engine fixture/timeout
  results remain historical and must not be reclassified as completed GREEN cohorts.
- [Player timeout RED](recovery/evidence/save-engine-player-windows-red-20261003/manifest.json)
  at `dafd37e5` reached the actual save cut and exposed primary-outcome masking,
  then exceeded its former two-minute owner: no final TRX, formal zero cases and
  core unrun there. [Real-refresh RED](recovery/evidence/save-frontend-refresh-red-20261003/manifest.json)
  preserves the actual network/HTTP failure assertions before the save-only fix.

The owner authorized Linux source implementation after Windows prerequisite review,
without waiving native Linux execution or cross-platform acceptance. Linux FIFO is
implemented and compiled but has not run here. The native Linux coordinator must run
cold/resource, generation/admission/FIFO and related caller owners in bounded commands.

## Historical initial Windows restoration — 2026-10-02

Owner authorized local implementation and native Windows verification, with Linux
execution returned to the original coordinator. Branch: `codex/1553-save-windows`,
base `6dc8c218c99cff0e4934ca3476544cda12882e48`. The active three-file
carrier was verified byte-for-byte and applied once; its carrier-excluded tree
is `24ca7470571f740f9d920a1a49d05e637b717bc7`. Source now resides in normal
Git files. The two carrier files are retired; do not apply historical patches.
The initial Windows checkout converted patch line endings; exporting the exact
Git blob restored its recorded 32,388 bytes/hash before any patch application.

Tracked block T032-A1-WIN: finish scoped recovery-observer plumbing, own and run
the existing 14 separate-process v2 cases on Windows, preserve phase reach and
exact library/outside sentinels, then review remaining prerequisite coverage.
Use three focused categories (publication cuts, large before-image restoration,
conflict/committed cleanup) and relevant v1/image-adapter compatibility only.
No full suite. Existing synthetic state is per-test and child ownership stays
explicit. These tests exercise ordinary app-owned crash recovery, not an unknown
prior diagnostic. No security settings or permissions are changed.
Preflight: recovery hook must reach the existing shared recovery engine; callbacks
must not introduce a second recovery authority. Cold tests prove journal-only
restoration after candidate removal; resource qualification remains a separate
workload gate. SaveGame cutover remains gated by full prerequisite acceptance.
Native RED ac470723 at a340f5f7: fresh XML build succeeded; both large-before
cases failed (expected phase exit 73, actual normal return 0), 2 executed of
14 planned, zero timeout, complete owned cleanup, command 3m14.906s. Evidence:
recovery/evidence/stream-windows-red-20261002. The missing recovery-observer
forwarding is now wired; tests also assert exact observed phase/index and full
library membership. This fix is WIP pending GREEN and independent review.
Linux and full T032-A1/T032-A remain open.

## Historical T032-A — ordinary save creation source/evidence chronology

**Continuation:** [save-task-handoff.md](save-task-handoff.md) gives the current restore point, remaining proof gates and ordinary-save completion boundary.

**2026-10-02: technical gate complete; T032-A1 image/codec/adapter implementation is WIP. Full ordinary save creation is still open.** Existing [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), approved [spec](spec.md), [T032-A/T032-A1](tasks.md) and the [reviewed resource prerequisite](save-resource-gate.md) govern this work. Independent actual Astra XHigh accepted the source/evidence design at frozen `0ebe3910`, conditional on the exact temp/active authority matrix, then accepted its scoped correction at `7cd1985b`. The coordinator opened only the internal image, strict v2 codec, shared B1 operations and canonical image-member adapter block. SaveGame/browser/profile/listing/autosave cutover follows after prerequisite proof and independent review. T030-G remains accepted; full T031/T032 remain open.

Initial clean local/GitHub base was `fc3f8bb9c9e36408c85cf37bbea9f35cb890a972`, tree `dbca7b4084aa2a633a4fb4745c9ea133cf5fd0b7`. The [preserved source reconciliation](save-creation-cutover.md) remains byte-identical (SHA256 `35e89d331372499d8fe27a328802013ba9f645a36334e88b8e4a3ebf157e5742`, 33,422 bytes), with its immutable source-only provenance. Its seven runtime blobs matched that initial accepted tree; this is historical source equivalence, not a claim about the changing current implementation. [Original cutover](save-load-cutover.md) and [load admission](save-load-admission.md) remain binding.

### Completed technical gate and current evidence

- [x] Actual public SaveGame causal RED: both calls reached completed archive preparation exactly once. Publication failed at the descriptor backend with zero B1 commits; real private cleanup hit kernel32 and retained save-staging. [Two intended failures](recovery/evidence/save-entry-causal-red/manifest.json), tested `1789cd32`, 2:01.0257523, complete owned fixture cleanup. The earlier [two resource-parent fixture failures](recovery/evidence/save-entry-fixture-failure/manifest.json) remain noncausal historical evidence
- [x] Resource decision: [three measurements](recovery/evidence/save-resource-qualification/manifest.json) at `a7709934`, 1:21.1184474, showed unchanged production of 536,815,500 expanded bytes into a 536,973,722-byte ZIP under the child-only 768 MiB GC bound. Unchanged B1 committed the 64 MiB candidate, failed 128 MiB base64 serialization before intent, and failed near-512 MiB cloning as Uncertain. The same library/generation/candidate invariants and separate cold acquisitions were checked. This is engineering evidence, not full envelope or save capability acceptance; no archive cap/minimum RAM was added
- [x] Preserve and review the proposed single-journal prerequisite, including original v1 handling and the exact temp/active matrix. [Frozen GitHub-only restoration proof](recovery/save-gate-restoration-proof.json) describes `0ebe3910` only. [Discovery-only audit](recovery/evidence/save-gate-catalog-audit/manifest.json) at that source checked 170 categories/10,446 identities, zero unmapped/stale selectors, fresh builds, zero tests, 3:34.0700746. Subsequent image/codec tests require a later audit
- [ ] Complete T032-A1 implementation/proof/review. [Image helper GREEN](recovery/evidence/save-images-green/manifest.json) tested `f343cac1`: 11/11, fresh build, 2:18.2664284. [Shared v2/adapter first GREEN](recovery/evidence/save-stream-first-green/manifest.json) tested `fcd71676`: 40/40 in 2:39.0260950, fresh build and complete cleanup. The former is preserved at `00dc36cd`, the latter at `a8a9f300`; tested-at and evidence-containing commits are distinct. Neither accepts the whole prerequisite or save callers

Historical image development evidence stays separate: [zero-build/zero-test shallow-launch failure and corrected portable launcher](recovery/evidence/save-image-launcher-failure/manifest.json); [one causal byte scaffold failure plus ten pre-scaffold dot-segment fixture failures](recovery/evidence/save-images-scaffold-partial-red/manifest.json), tested `22b486bc`; [11 intended scaffold failures](recovery/evidence/save-images-normalized-red/manifest.json), tested `52967dc2` with justified fresh-build reuse and preserved at `f343cac1`; [29-case stream scaffold RED](recovery/evidence/save-stream-scaffold-red/manifest.json), tested `00dc36cd`, 27 intended failures/two original-v1 control passes, preserved at `050306c2`. No failure is silently relabeled GREEN.

**Preserved dispatch checkpoint — no diagnostic continuation:** the format-dispatch [causal RED](recovery/evidence/save-stream-dispatch-red/manifest.json) at `a8a9f300` is followed by the already completed [four-case GREEN](recovery/evidence/save-stream-dispatch-green/manifest.json) at `26c0327c`: 4/4 passed, fresh unit build, exit 0, 2:26.4767827, one complete descriptor and complete owned cleanup. Existing summary/TRX/source identities were independently read back without execution after the run. The bounded plausible-v1 prefix check retains original v1 decoding and rejects unknown binary prefixes before bulk allocation. This result does not accept the whole prerequisite.

At 2026-10-02 02:08 UTC the implementation agent turn ended with the platform message “This content was flagged for possible cybersecurity risk.” The notification does not identify the triggering operation. The completed GREEN log predates it; the triggering operation is unknown; the uncertain workload was not resumed, reconstructed or moved. Read-only process inspection found no dotnet, PowerShell, Python or Git command still running. Existing evidence and the exact later local WIP are preserved here rather than counted as completed work.

**Unrun WIP:** `TrustedLocalStreamRecoveryTests`, additions to its shared owned fixture/host and an unused `LocalPublicationRecoveryObserver` hook were written after the completed run. They describe ordinary process-crash phase recovery, large before-image rollback, later-member/generation conflicts and interrupted committed cleanup, but no build, test, discovery or wiring acceptance is claimed for them. Independent actual Astra XHigh read-only review of frozen `36c2bb23` plus the `c991f482` proof supplement found no concrete new defect in implemented image/codec/adapter code and verified all 13 artifacts in the four inspected bundles. It did not accept T032-A1. The recovery observer is unwired, the 14-case new class has no catalog owner, and its bytes remain unbuilt/unrun. Existing v1 controls synthesize inputs through a shared new `Header` helper and are smoke controls, not independent historical-fixture proof. The source-only checkpoint below supplied original-v1 fixture provenance. Subsequently authorized ordinary valid-data compatibility checks passed on the isolated source, as recorded under T032-A1-V1 below; this main-branch integration runs no runtime command. The uncertain diagnostic and unrun cold scaffold, including callback plumbing, are outside this source-only integration. Required recovery/resource/native proof remains open and must be settled under applicable permissions before caller cutover. Cold interruption/large-before rollback, full new-v2 resource measurements, remaining generation/consumer coverage, main-branch discovery and coordinator-owned native proof/review remain unrun or incomplete.

### Source-only original-v1 fixture checkpoint — 2026-10-02

This historical source-only block started from verified local/GitHub `74e69ab02aec1c244a7e866059508d0053ddbfe3` on `1553-cross-platform-runtime`, with the three already-applied carrier deltas preserved. [Two independent source-derived fixtures and provenance](recovery/fixtures/original-v1-source-derived/README.md) pin the accepted original-v1 contract at `fc3f8bb9c9e36408c85cf37bbea9f35cb890a972` and six historical source blobs. They represent pending and committed forms of one valid four-member transition: replacement, absent-to-empty creation, deletion and explicitly bound exact generation bytes. Immutable JSON/base64/SHA-256 fields and path-normalization rules are preserved; no old or new application/helper was invoked to produce them and no historical-binary-emission claim is made.

Only source/JSON inspection, base64/encoding/hash calculations, Git identity/diff checks and remote preservation were permitted in that data-only block. Catalog edits clarify the existing in-process and synthetic-v1 scope without adding selectors; the 14-case cold class remains byte-identical, unbuilt, unrun and unowned. Production runtime, test bodies, shared crash fixture/host and recovery-observer plumbing remain unchanged from the entry state. No build, application, test, discovery, benchmark, malformed-input probe or native operation executes. The latest recorded dispatch GREEN is 4/4 at its earlier source, not a new result here. Static checks passed for both exact original-v1 shapes, 12 present/four absent image descriptors, base64/length/SHA-256, BOM-aware generation bindings, nonoverlapping placeholder/scratch names and six historical blob identities. All 1,615 tracked C# source files match the entry-state SHA-256 snapshot; catalog membership/selectors remain unchanged. `git diff --check` passed. This does not prove original-v1/generation runtime compatibility or close any T032-A1 proof checkbox.

Remote fixture checkpoint [4e0221d17643a3213aa61a2e972343c5dbe96b46](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/4e0221d17643a3213aa61a2e972343c5dbe96b46) was read back from the GitHub branch. A new empty-directory `git clone --depth 1 --single-branch --branch 1553-cross-platform-runtime` from GitHub recovered all 3,997 tracked files with clean raw checkout and no local object alternates. Raw tree `95ec4cc71d59ef0ab874a2197b23c51a3409b5c9`; its 12,395-byte packet passed hash/before-image checks and applied once only in that fresh clone. All three after-image hashes and both immutable fixture identities matched. Full applied tree `d69f5c28047783320585d27890f75ee22162e7bc`; carrier-excluded source tree `bfff38ecd76dd523cda38e0c088a57ab0b55116c`. This is source/artifact restoration only, with no execution. These identities describe that checkpoint; the following documentation-only save records this proof and refreshes the carrier without changing fixture or runtime bytes.

Independent actual Astra XHigh read-only review accepted the bounded fixture/source-provenance and documentation block at `92bd725fea22ae54fafb484c17941d13ff87a169` on 2026-10-02 02:47 UTC, with no finding. It verified both fixture identities, every payload/generation binding, all six source references, the exact packet/applied trees, all 1,615 unchanged C# files and description-only catalog changes. This accepts source-derived fixture data and status accuracy only; it performs no runtime check and does not accept T032-A1, close an issue or open the SaveGame/client cutover. The source-only data block is complete. The later ordinary valid-data original-v1 compatibility cases were separately authorized, executed and reviewed on the isolated source below; the earlier generic termination remains unexplained. The uncertain diagnostic/cold scaffold has not been resumed, reconstructed or moved; this is not a blanket prohibition on ordinary save work. Cold v2 recovery/large-before-image rollback, actual v2 resource envelope, remaining generation/adapter boundaries, main-branch catalog discovery and Windows stream lifetime remain incomplete. Whole-document generation and legacy-v1 allocations remain outside any bounded bulk-image memory claim.

<a id="t032-a1-v1--accepted-isolated-compatibility-main-integration-wip"></a>

### T032-A1-V1 — accepted isolated compatibility and main integration

The two ordinary valid-data cases were independently accepted on frozen `1553-v1-fixture-compatibility`: source/evidence review at `bd23ad76b1758c8b268733d6da04c3d2876d12a4`, followed by final documentation/restoration-proof review at `c94469e8c5bb80072020001182085066589d6dd5`, both actual GPT-6 Astra XHigh with no findings. This is the existing FR-007/current-format exception, not a new historical-save support policy. Only T032-A1-V1 is closed; full T032-A1, T032-A and caller cutover remain open. The generic earlier termination did not identify an operation; its cause is still unknown, and its interruption/resource diagnostic is not resumed.

[Immutable evidence](recovery/evidence/original-v1-compatibility/manifest.json) describes **tested source `d4460793abbd61ec9e884004c93bf4154f979111` plus its own carrier**, excluded tree `4ac5cdc7ea75b0927feee672ef547a5fb6c306c3`, and unchanged runtime `26c0327c20b41e82e647287a455b7a8018f0796a`. One fresh unit PlanOnly build was followed by one `-NoBuild` run: **2/2 passed in 6.9895472 seconds**, one complete descriptor, no failures/skips/duplicates/timeouts and complete owned/runtime/fixture cleanup. Pending restores all four Before images; committed retains all four After images, including exact absence/empty/binary/BOM-generation bytes and identity, both sentinels and journal/sibling cleanup. A separate fresh integration build and zero-test audit checked **174 categories / 10,461 identities**, no unmapped/stale selectors, in **10.0979045 seconds**. These isolated-source results are not executions of the integrated main tip. The accepted test is imported byte-for-byte as blob `9aa64077be1b12639a7816464bcfd0edf4e48ccd`; original pending/committed/provenance bytes and every saved artifact remain unchanged. The evidence manifest's then-pending final documentation review is historical; the later c94469e8 review above is the current disposition.

Main integration starts from verified remote `068151c6366c96614b02a057b680d82eeebfbbf2`. A fresh GitHub-only checkout was clean, passed `git fsck --full`, and restored raw tree `3c58d02c007aafc02f3abe0ef053094b505b3897`. The existing 13,992-byte packet (SHA256 `58cb29cc840e99e3c82daa548d91a834dbf5229915017b89e699eab2644bc3e1`) applied once after all three before hashes matched: full applied tree `f413a00a71c4bb0907986081c004ea94b4c043e0`, carrier-excluded tree `ece35c4d4ef3bf8e5e912aaf49fdd29f936cb7b3`. Initial identities of all 3,997 tracked files were captured before integration. All production C# and preexisting tests/fixture/host bytes remain identical to that applied main baseline. In particular, main retains its unused `LocalPublicationRecoveryObserver` declaration and existing 14-case unbuilt/unrun/unowned scaffold; these were absent from the isolated tested source. The new owner selects only the accepted original-v1 method. Existing category descriptions/selectors remain intact.

The [saved isolated restoration proof](recovery/v1-compatibility-restoration.json) remains exact and historical. [Main integration source identities](recovery/v1-main-integration.json) record the reconciled files and invariance; the main save-creation packet remains the single active apply-once mechanism and still carries its three paths, including the original FileSystemManager hook delta. The isolated carrier is available only via [immutable c94469e8 history](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/tree/c94469e8c5bb80072020001182085066589d6dd5/specs/1553-portable-local-storage/recovery), and must not be applied to main. This source/evidence integration has no new build, application, test, discovery, benchmark, install or native execution; independent GPT-6 Astra XHigh review passed for integration `10933f73d0c4b18199b8d87c0ad0cc637950a62a` and proof supplement `5504396e808160c8f341ccb01b908a766cef2e68` on 2026-10-02 with no actionable findings, and the coordinator accepted this bounded integration.

[Fresh GitHub-only main restoration proof](recovery/v1-main-restoration.json) now verifies frozen integration `10933f73d0c4b18199b8d87c0ad0cc637950a62a`: 4,011 tracked files, initially clean raw tree `e6aa9ae523d4dd13b49073057d67afc445aaeb22`, full fsck and exact 22,086-byte packet (SHA256 `4514a0c9ca2a22bd3d576367ee05218f5a78db1915ab1ebcc788245aec49091d`) applied once. Full applied tree `96a51f65979084cec6ec2613af253548a59f64b2`, carrier-excluded `d37e635e085f24248bcc280cb32e45acb3cb429e`; all 1,615 prior C# files, 173 prior category objects, 13 exact imports, three immutable fixture JSON files and ten historical evidence artifacts verified against GitHub-only recovery. Zero build/test/discovery/runtime operations ran. This proof describes that frozen source; the subsequent proof/link-only checkpoint changes no source, test, owner or historical artifact and refreshes the same three-path carrier. The same independent review accepted both named integration/proof checkpoints on 2026-10-02 with no actionable findings; the coordinator accepted this bounded source/evidence integration. This status-only closure changes no runtime, tests, catalog, selection or historical evidence and does not close full T032-A1 or SaveGame/client cutover.

The saved first-unit-build log reports development-certificate installation. The preserved [nonretroactive disclosure](recovery/evidence/original-v1-compatibility/manifest.json) records that later starts alone set `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`; no certificate/key/trust inspection or change is made here. Source recovery does not qualify Windows stream lifetime or full SaveGame/client behavior, and no new GM schema/example follows from this test/evidence-only block.

The [historical apply-once packet](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/6dc8c218c99cff0e4934ca3476544cda12882e48/specs/1553-portable-local-storage/recovery/save-creation-pending.json) was verified and retired in the Windows continuation above. Current source is normal Git content; do not reapply it. Preserve source/evidence before long runs and use only affected categories; full-suite and unrelated accepted cohorts remain excluded.

<a id="t032-a1-create--accepted-isolated-collision-main-integration"></a>

### T032-A1-CREATE — accepted isolated collision; reviewed main integration

The one ordinary adapter case is independently accepted at frozen [ee17c13d](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/ee17c13d2ae8e251cd2a4111f61e9419054e321b), including final proof/status review by actual GPT-6 Astra XHigh, with no findings. [Exact imported evidence](recovery/evidence/create-only-adapter/manifest.json) distinguishes tested source `2b757469fd3f3596c4e32006be1ecbbdbdc59cb5` plus its carrier from unchanged runtime `26c0327c`. Fresh unit PlanOnly built and planned one case, executed zero, in **3:50.9379247**; one unchanged `-NoBuild` run passed **1/1**, exit 0, **5.6039534 seconds**, no failures/skips/duplicates/timeouts and complete cleanup. The first real image-adapter create commits; the identical-name second create with Before=absence explicitly rejects while exact archive/candidate/generation identities and bytes, three synthetic library sentinels and complete membership remain unchanged, with no journal/sibling scratch. This proves a tiny structurally valid ZIP container, not a complete game-save schema or SaveGame caller.

The missing integration build separately passed with zero errors/three existing CS1587 warnings in **2:21.59**. Its subsequent **zero-test** ownership audit at `4d312a62` checked **175 categories / 10,462 identities**, no gaps, in **7.2951308 seconds**. All 12 stored artifact hashes are preserved; three large discovery inventories are explicitly hashes-only. The [exact isolated restoration proof](recovery/create-only-adapter-restoration.json), SHA256 `620027663f195d353166fb7d1bca074fd6a5650825447d74d253f35e077f20c8`, verifies **9ac5422e**, not the later metadata tip: 4,022 files and its exact current carrier/source/artifacts, no execution. The manifest's then-pending final metadata review is historical; the ee17c13d review above is its disposition. All telemetry opt-outs and `DOTNET_GENERATE_ASPNET_CERTIFICATE=false` were applied before official-tool startup; no certificate/key/trust inspection or change accompanies this import.

Main integration is a normal single-parent source/evidence checkpoint from verified `ebd808e93ea60b5434396445b18738ae22f09d05`, not a merge. [Integration identities](recovery/create-only-main-integration.json) capture its exact applied baseline, byte-identical import, all preexisting C# and 174 prior category objects; the existing three-path carrier preserves all prior main deltas. No production fix was needed or imported. The retained recovery observer, shared fixture/host and 14-case scaffold remain unchanged, unwired/unbuilt/unrun/unowned. The isolated audit is not a main discovery audit. No build, test, discovery, installation, runtime or diagnostic command runs for this integration. Independent GPT-6 Astra XHigh passed frozen main integration `b2111b7cd0f66414d69af3d06751f617d8285948` for spec compliance and code/document quality, with no actionable findings. [Fresh GitHub-only restoration](recovery/create-only-main-restoration.json) verified 4,029 files, clean raw tree `3581b03abf543ce54b483c8a6c966f7285e8c710`, full fsck and one exact 31,472-byte packet application (SHA256 `14ada12e8c5cb2219116d3e45697229f93747efa4478b1f817911c33d376cdc5`): full applied `cd9cc426fd8efe167e4112f9819623dfdf860e55`, carrier-excluded `18f3b03959a4345c563787a17926054b7b3790b2`. All 1,616 prior C# files, 520 historical evidence/fixture files, 174 old owners and 15 imports were verified. This proof describes b2111b7c; the subsequent proof/status-only supplement preserves all source/test/catalog/selection/evidence identities and refreshes the same carrier. The coordinator accepted that bounded frozen import and factual handoff; this later proof/status supplement records its evidence. Only T032-A1-CREATE additionally closes; accepted T032-A1-V1 remains accepted and full T032-A1/T032-A/SaveGame/cold/resource/native gates remain open. The [handoff](save-task-handoff.md) is the concise continuation entry.

### Subsequent capability and proof boundary

After that gate, use TDD in bounded saved blocks: one held snapshot lease; fully closed/flushed disk archive; generation/absence-checked create-only B1 publication; owned private save-scratch cleanup; validated stream save listing under owned/supplied quiescence before archive interpretation; prepared browser save without a zero-member v6 recorder or nested lease/publication; Task-only ordinary profile-mirror repair while original load retains real receipts; exact created destination and committed/rolled-back/uncertain/follow-up outcomes through console, HTTP/frontend and the three actual autosave callers. Separate pre-save mirror repair remains an explicit prior decision unless a complete coupled set is proven. Post-commit cleanup/logging/owner/menu trouble cannot reverse archive success. Retained debt is classified before normal fencing and blocks retention/compensation/later writes when unresolved.

Select only new save owners and exact changed B1, profile, browser, transport/frontend and autosave consumers using `scripts/test-csharp.ps1`; update selection reasons/catalog ownership and preserve failures, partial/zero-test outcomes, parsed sanitized TRX and exact tested identities. Assert actual reached cuts, full bytes/generation/library/outside-hardlink sentinels and separate-process recovery via real public APIs. Native verification is assigned only by the coordinator; browser GUI remains deferred. No accepted T030-G/T030-F cohort is rerun for planning.

Load, library replacement/deletion, full logical receipts/accepted turns/browser-v6/worker migration, external Daren, B4 and B5 are out of this capability. Save libraries and `BasePath/client_profile/qte_showcase_rewards.json` remain untouched except the expressly created ZIP and separately permitted autosave retention. Original physical paths/evidence stay explicit. The feature is client-owned storage/outcome handling, not a GM-authored schema/gameplay change; synchronize operational and outcome documentation with actual behavior, without inventing a new GM payload example.

<a id="active-t030-g--ordinary-backup-lifecycle"></a>

## Accepted T030-G — ordinary backup lifecycle

**2026-10-01: bounded T030-G accepted and complete.** Independent actual GPT-6 Astra XHigh accepted spec compliance and code/test quality, including exact normal-source delivery and actual native evidence at `e13dc5a26d648bf48593876614d4082ae251b0ee`, tree `a0e33f6a110c5948705d657f0a895bb17ee1d99c`. Both review findings are closed. Tested normal source is `fda03346e38ba4e6a16bff015d6ddc88a91b7f3b`, tree `d79419a10e562915cc87d276487453408844c7ac`, descended from accepted base `5e0292fd1969eb9ec047a100dca0318d560fceb0`. The full chronology and all exact intermediate identities remain in the [immutable e13dc5a2 plan](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/e13dc5a26d648bf48593876614d4082ae251b0ee/specs/1553-portable-local-storage/plan.md#active-t030-g--ordinary-backup-lifecycle) and unchanged evidence below. This closes only T030-G; full T030/T031 and #1553 remain open.

Ordinary public sync/async and supplied-lease backup creation, restoration and cleanup now use complete existing B1 decisions. Creation preserves exact source bytes in a fresh absence-guarded name. Restore publishes target bytes and backup deletion together; cleanup publishes evidence deletion. Real lease/generation fences, path/type/link validation, exact BOM/binary/empty bytes and absence, arbitrary valid canonical backup names, platform path comparison and outside hard-link contents remain protected. Empty/absent no-ops create no journal or generation; fresh generation participates in the same nonempty decision. Original recorder/recovery/evidence routes retain their physical handlers and callbacks; mixed ordinary/original restoration is rejected. No journal/schema/GM format or synthetic physical identity was added.

StateDistributor and immediate QTE continuation preserve typed unresolved outcomes, stop later compensation/hold release, and resolve same-lease debt before follow-on work. Committed distribution and backup results survive cleanup, diagnostics and owned-release trouble. P1 recovery now classifies retained debt before ordinary bound-generation fencing while leaving debt-free mismatch a known error. P2 retains the exact primary distribution exception on the same outer uncertainty object alongside the restore cause. Whole distribution, QTE, accepted turns and preparation remain multiple decisions; this slice does not make those complete flows atomic.

### Distinct verification evidence

- **Linux: 42 methods / 94 distinct passing cases**, established by the [exact passing union](recovery/evidence/backup-linux-plan/passing-union.json) of three separately saved cohorts: [43/43 API/boundary/warm](recovery/evidence/backup-lifecycle-core-green/manifest.json) at `261bda1b` plus packet, [50/50 cold/actual consumers](recovery/evidence/backup-lifecycle-continuation-green/manifest.json) at `7a40bbaf` plus packet, and [19/19 review corrections](recovery/evidence/backup-review-correction-green/manifest.json) at `e2d1afff` plus packet. This is not an aggregate run at the final checkpoint. Fresh applicable builds, actual reached boundaries, no skipped/unrun/duplicate cases and complete owned cleanup are retained per cohort.
- **The exact real Linux quarantine case passed** in the 50-case continuation after unchanged live preparation and actual StateDistributor fixture setup. Real treatment/resource/item claims, QTE compensation boundaries, separate-process public-API crash recovery, unknown bytes/generation, links/FIFO, case distinctions and synthetic save ZIP preservation were exercised. This resolves that exact former prerequisite, without claiming full Linux gameplay or whole preparation.
- **Windows: actual 30/30 passed**, 14 methods / seven complete descriptors, once at normal `fda03346`: API 6, bound-generation debt 1, namespace/link/hard-link/lease/debt boundaries 13, cold hard-link recovery 2, original routing 4, actual QTE integration 3 and extended-root/case alias 1. [Native manifest and artifacts](recovery/evidence/backup-windows-20261001/manifest.json) record exit 0, wall `00:08:07.4900708`, fresh builds, actual fixtures/assertions, no failures/skips/unrun/duplicates/timeouts and zero owned cleanup remainders. Manifest SHA256: `22308c540cc989d6a53fd352a246df02f563a3b6a851d1688331ed9c48f27cc5`. No native quarantine case was selected in this subset.
- Discovery-only [ownership audit](recovery/evidence/backup-final-audit/manifest.json) passed **168 categories / 10,443 methods or files**, with zero unmapped/stale selectors. [Linux planning](recovery/evidence/backup-linux-plan/manifest.json) records 42 methods / 94 cases / 14 descriptors; [native planning](recovery/evidence/backup-native-plan/manifest.json) records 14 / 30 / seven. These commands executed zero tests.

All historical failures remain distinct: [six initial API failures](recovery/evidence/backup-lifecycle-initial-red/manifest.json), [unit zero-test build failure](recovery/evidence/backup-lifecycle-build-failure/manifest.json), [integration zero-test build failure](recovery/evidence/backup-consumer-build-failure/manifest.json), [five StateDistributor REDs](recovery/evidence/backup-distributor-red/manifest.json), [QTE one-pass/two-failure RED](recovery/evidence/backup-qte-red/manifest.json), [P1 bound-debt RED](recovery/evidence/backup-generation-debt-red/manifest.json) and [P2 distinct-cause RED](recovery/evidence/backup-compensation-cause-red/manifest.json). Both compile failures were fixture corrections against declared APIs, not production accommodations. Original/sanitized hashes and parsed XML results remain unchanged. Source/Linux review accepted the bounded capability at `f3e26dd8`; the final normal/native evidence review at `e13dc5a2` closes the remaining T030-G delivery gates.

### Delivery and remaining scope

Source/tests/catalog/selection are normal Git files, matching all [17 delivery identities](recovery/backup-delivery-source.json). The four exact after-images and original 28,296-byte packet remain pinned to [immutable 3deb2918 history](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/tree/3deb291865afa84f47a3ed6050e674f1f6745327/specs/1553-portable-local-storage/recovery); only the two active carriers were retired. Normal source needs no patch. The [historical handoff proof](recovery/backup-handoff-recovery.json) retains its own initial/full-applied/carrier-excluded trees at `f3e26dd8`; those are not final normal-tree identities. Fresh GitHub-only final evidence recovery at `e13dc5a2` verified 3,923 tracked files, clean checkout/connectivity/history, all 17 identities, four after-images and nine native evidence hashes. Follow the [accepted recovery instructions](recovery/README.md#accepted-t030-g-backup-lifecycle-recovery).

Full StateDistributor/QTE/accepted-turn/preparation atomicity, browser-v6 receipts, archive/library/save-load/staged-workspace and external Daren authority, GM/provider, B4/B5 and full gameplay/platform acceptance remain open. Current-schema closed-game editing is allowed; logical once-only authority, real leases/generation, validated paths and accidental-loss recovery are preserved. No merge, force-push, branch deletion or issue closure is part of this block. This closure changes documentation and the bounded T030-G checkbox only; no runtime, test, catalog, selection or evidence mutation, and no behavioral rerun.

<a id="active-t030-f--ordinary-canonical-directory-tree-deletion"></a>

## Accepted T030-F — ordinary canonical directory-tree deletion

**2026-10-01: bounded T030-F accepted and complete.** Independent actual GPT-6 Astra XHigh accepted both spec compliance and code/test quality at evidence checkpoint `6756a64b32e523c73efe9603549cba55725ebbf8`, tree `e5c447d3810e1381c8ff2fe1a75f125d5e4871ff`; all review findings are closed. Tested normal source is `89ea5e001135d1ae76e3cd0c755c70febdfec1cd`, tree `d74398a7a1b3e97da83c5a5d2c1ca78ba6bd80d8`, descended from accepted base `2e3cb6deb772b96fbcec53b98b5d2d50e0bdbc0e`. This closes only ordinary recorder-free canonical tree deletion and its explicit original-route separation. Full T030/T031, whole preparation and #1553 remain open.

The ordinary API validates the real lease/current generation, requested tree and every descendant before publishing exact file before-images → absence as one existing B1 member set. It revalidates the complete namespace after hooks, preserves platform case rules and outside hard-link bytes, and uses existing normalized Windows path projection. Empty/absent trees create no journal or generation. Committed cleanup debt preserves scratch parents; same-lease repeat deletion of the tree or its empty ancestor resolves B1 recovery before nonrecursive empty-directory pruning. Unknown bytes/generation conflicts retain evidence and stop mutation. Postcommit cleanup or diagnostic failure cannot reverse the byte result. Original recorder/recovery, warm browser cleanup after recorder removal and evidence-parent cleanup retain their explicit original physical handler. No receipt/schema, SessionOperationContext or GM-authored contract changed.

### Verification and historical evidence

- **Linux: 28 methods / 58 distinct passing cases**, established as a [verified union of separate saved cohorts](recovery/evidence/directory-deletion-linux-plan/passing-union.json), not an aggregate execution on the final checkpoint. Source/Linux review was accepted at `48629924`. The [immutable detailed checkpoint](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/6756a64b32e523c73efe9603549cba55725ebbf8/specs/1553-portable-local-storage/plan.md#distinct-t030-f-linux-evidence) retains every exact source, RED, partial count, correction and result, including the original kernel32 RED, same-lease debt RED, wrong-project discovery failure and corrected uncertainty assertion.
- **Windows: actual 30/30 passed in 14 methods / four complete descriptors**, once at normal `89ea5e00`: real quarantine 1, original browser 4, extended-root 1 and common filesystem boundaries 24. [Native manifest and artifacts](recovery/evidence/directory-windows-20261001/manifest.json) record fresh applicable builds, exit 0, wall `00:10:48.5515160`, no failure/skip/unrun/duplicate/timeout, actual link/hard-link and separate-process bodies, and zero launcher/runtime/fixture remainders. Manifest SHA256: `17e90a3d62b682a1700b16ce84c4b88f9008a03a9f40074cfedaa4eb26020200`. The exact command is retained in [normal recovery](recovery/README.md#accepted-t030-f-recovery).
- Discovery-only [catalog audit](recovery/evidence/directory-deletion-final-audit/manifest.json) passed **149 categories / 10,410 methods or files**, with zero stale/unmapped selectors. [Linux planning](recovery/evidence/directory-deletion-linux-plan/manifest.json) and [native planning](recovery/evidence/directory-deletion-native-plan/manifest.json) executed zero tests; discovery is separate from behavioral evidence.

Original failed/partial artifacts and raw/sanitized hashes remain immutable. The five XML-safe corrected copies preserve identical test identities/outcomes and their original provenance: [initial RED](recovery/evidence/directory-deletion-initial-red/xml-corrections.json), [initial partial GREEN](recovery/evidence/directory-deletion-initial-green-partial/xml-corrections.json) and [19-case continuation](recovery/evidence/directory-deletion-continuation-green/xml-corrections.json). The final reviewer checked all six native hashes and four XML TRX, actual reached bodies/counts, cleanup, all 17 delivery identities and five historical packet after-images. No behavioral rerun accompanies this docs closure.

### Delivery and remaining boundary

At the accepted T030-F closure, source/tests/catalog/selection are normal Git files and the [17 delivery identities](recovery/directory-delivery-source.json) match that immutable closure; later T030-G source has its own identities. The five exact [historical packet after-images](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/f35810be27975454fbf3cc276089bd89e444c3bd/specs/1553-portable-local-storage/recovery/directory-deletion-pending.json), including research.md, matched tested normal `89ea5e00` and evidence `6756a64b`; that is a claim about those named checkpoints, not every later head. Only the two temporary packet carriers were retired. Fresh GitHub-only recovery of `6756a64b` verified 3,835 tracked files, clean checkout/connectivity, the 17 delivery identities, five after-images and six native evidence hashes. Historical [pre-normalization recovery](recovery/directory-handoff-recovery.json) remains tied to `48629924`. Follow the [current recovery instructions](recovery/README.md#accepted-t030-f-recovery) without replaying a retired packet.

**Historical T030-F Linux limit, subsequently resolved for this exact consumer:** at closure, the unchanged fixture passed live preparation (Procedure.cs 1065–1079), then failed during StateDistributor (1183–1186) before returning, because CreateBackupCore reached original descriptor-bound create-only publication. Quarantine assertions at line 256 onward were not reached in that [preserved failed consumer result](recovery/evidence/directory-deletion-consumers-partial/manifest.json). Exact console snapshot and terminal-error cleanup consumers passed in the same partial run. The [backup lifecycle map](backup-lifecycle-cutover.md) remained research at that checkpoint. T030-G now implements that separately assigned boundary and its saved 50-case Linux continuation reaches and passes the real quarantine assertions; full T031 remains open.


Whole live/console preparation still publishes request/manifest/authority/tree/dice/snapshot separately. Full browser-v6, accepted-turn/normalizer, save/load/library, worker, B4/B5 and gameplay/GM acceptance remain open. Synthetic save ZIPs are unchanged; this client-owned storage change needs no new GM example. The next implementation boundary must be assigned under the existing task/plan. Closure changes only documentation and the T030-F checkbox, with no source, catalog, selection or evidence mutation.

## Accepted T031-A — ordinary coordinated publication

**Accepted 2026-10-01 19:31 UTC:** independent GPT-6 Astra XHigh review accepted spec compliance, code/test quality, exact normal delivery, native reached bodies and evidence at `6c446229976239f6a5430e2935ff16d9db618884`, tree `01ed7e2cc11be1bb1e51d71e4aed95df49f4d30c`. No finding remains; the retry-sensitive fault-fixture P2 is closed. This closes only T031-A. The complete intermediate chronology, RED/fixture qualifications and exact source/packet identities remain in [immutable 6c446229 plan history](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/6c446229976239f6a5430e2935ff16d9db618884/specs/1553-portable-local-storage/plan.md) and unchanged evidence bundles. This documentation closure requires only diff/link/hash/checkbox and fresh recovery checks, followed by scoped readback; no behavioral rerun.

Ordinary recorder-free `CoordinatedStateWriteHelper` now prepares one complete existing B1 member set after every initial semantic/exact guard. It preserves CommitGate→real canonical lease, supplied-lease and zero-write/guard-only behavior, exact before bytes, UTF-8 BOM, platform-aware duplicate destinations and final-write ordering. True means committed; false means baseline mismatch or confirmed rollback; uncertainty remains distinct and cannot become safe retry. Owned release preserves the primary failure and always attempts stream/parent/ambient cleanup; established-result diagnostics cannot change the result. B1 journal, retry/observer model and generation admission remain unchanged. Original recorder/recovery routes retain their original callbacks, apply/undo and authority; invalid mixed hooks fail explicitly.

Actual NPC, Guardian, Shining and resident callers keep recovery-capable reads/admission/publication outside forgiving in-memory planning catches. QTE uncertainty bypasses only the later domain-compensation catches and reaches fixed Russian recovery notices without raw paths or promises of unchanged state. Ordinary `MemberPublished` cuts are pre-decision; later `final:*` and accepted backup-cleanup controls preserve their separate gameplay acknowledgment/compensation meaning. Resident public cleanup still has three independent preamble publications outside this helper boundary. No full normalizer/browser-v6 migration, new journal, synthetic identity or GM schema change is introduced; no new GM gameplay example is required.

### Distinct T031-A verification

These are separate runs at their recorded sources, not a new aggregate run on the final tip:

- [Lease/helper/dependency GREEN](recovery/evidence/coordinated-lease-green/manifest.json): **20/20** at `86eff080a2172cd53a53aef13538780e02ea1c6c` plus packet, including six existing real-lease/lock/ordinary-reader consumers
- [Helper boundary GREEN](recovery/evidence/coordinated-boundary-green/manifest.json): **50/50** at `ed0cec422339228139cff8805b4d680fd0866129` plus packet: helper 27, actual authority factory 1, real leases 10, actual-helper cold-process recovery 12; exit 0, **3:10.7882438**
- [Caller GREEN](recovery/evidence/coordinated-cleanup-green/manifest.json): **20/20** at `a7eaf3cdc783bda74b718f8a704b2b8bca852719` plus packet, including retained-evidence admission and malformed/known-abort controls; exit 0, **4:19.5932273**
- [Partial consumer attempt](recovery/evidence/coordinated-consumers-partial/manifest.json): **nine executed, eight passed, one Linux preparation failure, 26 unrun** at `5cc0bc984e31c0606097c2100aa9e8d0afc5f17b` plus packet; exit 1, **4:41.0285665**. The real wound case failed in unchanged physical directory cleanup before the changed helper; that failure remains preserved historically; its exact preparation/backup prerequisite is qualified by the later T030-F/T030-G work
- [Exact continuation GREEN](recovery/evidence/coordinated-continuation-green/manifest.json): **26/26** at `4daa2d52a8f9f26897279e06f54482535fd8d3b4` plus packet, after non-transient MemberPublished fixture correction: 23 phase/domain-compensation, one progression and two uncertainty/UI cases; exit 0, **4:15.9422436**. The earlier eight passing consumers were not repeated
- [Audit](recovery/evidence/coordinated-final-audit/manifest.json): **137 categories / 10,387 methods or files**. Expanded [Linux plan](recovery/evidence/coordinated-linux-plan/plan.json): **66 methods / 110 cases / 13 descriptors**; [native plan](recovery/evidence/coordinated-native-plan/plan.json): **12 methods / 16 cases / two descriptors**. These commands executed zero tests. Independent review verified that the Linux plan is the exact union of saved passing cases, including overlapping earlier cohorts, not a 110-case aggregate execution
- [Actual native Windows GREEN](recovery/evidence/coordinated-windows-20261001/manifest.json): **16/16**, 13 unit + three integration, 12 methods/two descriptors at normal tested source `5bfa0a1b017f4830e202be7fd8cb25d5c180082a`, tree `b5091deaa949ae4c1a16deee98a1a93942aac71d`; exit 0, **7:57.9395266**, fresh builds on Windows 10.0.26200 X64 / SDK 10.0.401 / PowerShell 7.6.6. Actual file-symbolic-link setup, cold hard-link cuts, case/separator/authority, owning-lease release/reacquisition, original browser accept/decline callbacks and real wound preparation/quarantine bodies passed. No elevation, security change, fixture workaround or silent platform return. Evidence was saved at `9ce7d9e278625b07c59fbbf218081d490050152f`; final `6c446229` changed only delivery wording. Manifest SHA256: `f473aa1bfce4d7c134a43b28cb4e6d21e392096f6baec9c523b7c87b77e8b1a8`

Completed GREEN selections had fresh required builds, complete descriptors/owned cleanup and no failed/skipped/unrun/timed-out/duplicate cases. The separately retained failed/partial runs keep their actual outcomes. Original/sanitized artifact hashes, commands, environment and method/case multiplicities are in the manifests; all historical artifacts remain unchanged. The corrected Windows alias fixture now has native execution; the old Linux backslash fixture error and resident observer/count errors are not reclassified as production failures or passes.

### Normal delivery and remaining limits

All source/tests/catalog/selection are normal Git files, with [31 exact delivery identities](recovery/coordinated-delivery-source.json) and nine former packet after-images verified through final `6c446229`. The full applied index at frozen `34089bc1` was `c582289ce6278ecfe059c52e0c6bb2085a38b815`; excluding exactly the two active packet carriers yielded source proof `a2cbf6b9b9897ced72774b476d327359a8a053a2`. Normal `5bfa0a1b` then differed only in plan/README. These are distinct trees, not interchangeable final checkpoint identities. [Normal recovery instructions](recovery/README.md#accepted-t031-a-normal-recovery) retain immutable packet history; no active recovery patch is needed or should be replayed.

Fresh GitHub-only native source restoration verified 3,758 tracked files, clean checkout/connectivity, all 31 identities and nine after-images before execution. Final evidence restoration/readback verified those identities and all four native artifacts. Source recovery verifies bytes and provenance, not another build/test or power-loss guarantee. The current default selection remains the bounded Linux reproduction plan; the separate native selection owns the real wound qualification.

T031, accepted-turn/normalizer/browser-v6/external-Daren/save-load/full-worker migration, gameplay/GM flows, B4/B5 and overall platform acceptance remain open. At T031-A closure, native wound success did not close the separate Linux directory/preparation dependency. Later T030-F/T030-G work resolved the prerequisite for the exact real Linux quarantine case, without completing whole preparation or accepted-turn migration. Original browser service fixtures do not establish live browser acceptance, which the user deferred. The [remaining B3 map](research.md#later-b3-accepted-publication-seams-read-only-2026-10-01) records a proposed cleanup boundary without claiming implementation or deciding a wider transaction. Next work requires separate assignment; the present source/evidence remain frozen.

T030-E stays accepted at normal source `29f485748819a8d95fd94b483f96e98c3f6e4b81`, final evidence `9232216fd23909477d908c4433ac5debc8db4b7f` and docs closure `e244a890`: separate Linux 76 and native Windows 25 results, independent Astra XHigh acceptance at 14:50 UTC. [Reader evidence](#accepted-t030-e--ordinary-canonical-readers) and [immutable closure history](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/e244a890000f7ae789eb24aa85dbec789b837aee/specs/1553-portable-local-storage/plan.md) retain that earlier block's identities and limits.

## Summary

Complete Linux/Windows portability while preserving the existing game and persistent arbitrary interactive GM workflow. Use the approved trusted-local-player model: exact bytes/absence, content hashes, generation and participating-writer ownership. Closed-game current-schema edits are permitted; simultaneous external editing is unsupported. Roots/exact external grants, type/link/schema/archive validation, once-only gameplay authority and non-following cleanup remain required. No synthetic physical identity, privileged service or protection against the computer owner is added.

One common B1 journal handles a single file or a declared complete member set: same-filesystem flushed stages, exact before images, generation binding, full-member recovery preflight, durable commit before cleanup and restart-safe cleanup. Unknown bytes/generation retain evidence. Outcomes are explicit Committed/RolledBack/Uncertain; post-commit failure cannot roll back accepted files or claim old settings remain saved. The claim is process-crash recovery, not independently verified power-loss durability.

Canonical/lifecycle leases, mutation boundaries, generation revision invalidation, pending-GM and local UI-owner gates remain. Old journal evidence uses its original supported handler or blocks before common bootstrap; formats are never reinterpreted or erased to gain admission. Original physical-recorder/save-load routes remain separate until their complete consumers migrate. Ordinary generation reading deliberately stays below its own operation fence to avoid recursion.

## Technical Context

C# targets .NET 8; the current tests require SDK 10 and PowerShell 7. Frontend is React/TypeScript/Vite. Verified Linux tools are SDK 10.0.401, .NET/ASP.NET 8.0.31, PowerShell 7.6.6, Node 24.19.0/npm 11.9.0 and Spec Kit 1.0.13. Set all three supported telemetry opt-outs before tool startup and use isolated mutable homes/caches/temp roots as documented in quickstart. The restricted Linux runner uses `DOTNET_PROCESSOR_COUNT=1`; this is an observed build workaround, not a product prerequisite.

Storage uses .NET BCL filesystem/JSON/hash operations and the single private member journal; no storage dependency or privileged service is introduced. Scope is the existing single-player console/local-web application on Linux and Windows. Preserve existing behavior and resource bounds without introducing an arbitrary file-size or performance promise. Focused C# categories, frontend contract checks and separately qualified process/live checks supply verification; commands and source identities are below and in quickstart.

## Constitution Check

Issue/spec traceability, client parity, canonical state ownership, test-first work, isolated selected-category verification and independent per-block review are retained. Player notices remain Russian and avoid raw technical diagnostics. Storage-only changes keep current GM payload schemas and client-owned authority; any later workflow/ownership change must update its operational guides/examples/source guards in the same block. The approved trusted-player constraint supersedes the listed owner-resistant requirements in spec.md. Pre-release historical compatibility is not assumed: current-format legacy evidence requires its original supported handler or blocks migration. The test/category restrictions and serialized writer/review workflow remain as documented below.

The branch begins at explicitly authorized wound-merge base `f6dc2a1ce3e73f5e6940f686c97b863c9f7a8173`. Lost earlier local portability commits were not reconstructed from conversation. #1536 gameplay acceptance remains separate. The approved storage design and constitution supersession are recorded in spec.md; no renewed design approval is needed for the planned storage cutover.

## Project Structure

- Feature requirements, current plan, tasks, research and quickstart remain in `specs/1553-portable-local-storage/`; the exact previous plan is `plan-history-through-b3a.md`, and sanitized evidence/manifests are under `recovery/evidence/`
- Common storage lives in `BookOfEternityClient/Core/TrustedLocalFileScope.cs`, `TrustedLocalFilePublication.cs`, and the `FileSystemManager.TrustedLocalStorage.cs` / `FileSystemManager.GenerationSnapshot.cs` partials; retain original manager legacy routes until their bounded migration
- Prepared settings use `Core/LocalSettingsPreparation.cs`, `ConsoleSettingsSession.cs`, `ConsoleSettingsPreview.cs`, `Services/SystemModService.Prepared.cs`, and `WebUi/BrowserLocalWriteCoordinator.Prepared.cs`, with actual entrypoints/menu and browser service/frontend consumers
- Future replacement/worker/save-load changes belong beside these existing Core and Services consumers; B4 uses the existing bridge/runtime projects, and B5 uses the existing platform service boundaries rather than new parallel frameworks
- Focused tests live in `BookOfEternityClient.Tests/` and `BookOfEternityClient.IntegrationTests/`; shared cold-process fixtures are under `tests/fixtures/`, with exact ownership/selection in `tests/categories.json` and `tests/selection.json`. Browser component/contract tests remain in `BookOfEternityClient.WebFrontend/`

Structure decision: use small focused partials and existing caller/coordinator boundaries; keep one journal and preserve original-format recovery separation. No additional application project is justified by the accepted storage slice.

## Complexity Tracking

No new constitution exception is introduced. The approved trust-model supersession is explicit in spec.md; bounded legacy handlers remain transitional, and native B4/B5 candidates require qualification before selection. No extra abstraction or dependency is justified solely by this documentation cleanup.

## Accepted blocks and evidence

- **B1a scope**: source `18461c67fccfd478eb4dadb4d2d3136b3ce952e6`, 35/35 Linux cases and independent Astra XHigh acceptance. **B1b journal**: runtime `8d36e7083d9fe89cde70a2f145ba6109c3d6c649`, accepted checkpoint `795d79bec71459ad2d90121df06cba9d85a2cf32`, 66/66 Linux cases, cold-process recovery and corrected BOM/Windows-spelling policy. Pure spelling checks are not Windows filesystem execution. Exact commands and review rounds remain in the archive.
- **B2a bootstrap/ordinary writes**: strict actual entrypoints, exact before-image reader and explicit legacy recovery scope; both P2 fixes accepted at `cbc6b29c681a82145d0bb35f9b0fd7dcae9c4345`. Invalid existing config is preserved with a clear error. Source/catalog delivery later normalized at `d3773edf3e43516170b44e12421ed345fb0445ef`. Earlier raw logs lost with the old workspace are retained observations, not reconstructed artifacts.
- **B2b browser settings/audio**: one prepared config/projection set; audio config only; runtime follows durable outcome. Frontend request, dispatch, refresh and failed-load reconciliation checks cover stale/unmounted work and follow-up notices. Backend 66-case and frontend 34-case covering evidence, exact earlier consumers and scoped Astra acceptance at `44f80acb78d991543a97f6b703081e7c1d1e4e54` are linked in the archive and [evidence directory](recovery/evidence/). These are automated/service/component results, not live browser execution.
- **B2c console**: detached draft/temporary preview; existing save triggers publish config, projection and system-mod manifest together. MainMenu synchronization, true console ownership, retry/edit/reload/discard and committed follow-up behavior are preserved without holding a lease across a prompt. Source `48357174ccdada168b31545b2057e0f5a0f6db99` passed [37 affected cases](recovery/evidence/b2c-connected-green/manifest.json), including five scripted processes, following the earlier 42-case preparation/menu cohort. [Discovery](recovery/evidence/b2c-final-audit/manifest.json) found 113 categories/10,301 methods. Connected review and literal-only P3 readback accepted through `88a02f36fc072e66633055265b3f6a3aee7ce02e`.
- **Linux live console**: [observations/transcript](recovery/evidence/b2c-live-console/manifest.json), same frozen B2c source, actual TERM=dumb, ordinary W/S/Enter/Escape menus, QTE true→false persisted before leaving settings, all three member hashes retained through two exit-0 processes and restart. No scripted/agent/web flags, GM, audio or image acceptance is implied.
- **Windows scripted console**: [manifest](recovery/evidence/b2c-windows-20261001/manifest.json), [summary](recovery/evidence/b2c-windows-20261001/summary.sanitized.json), [TRX](recovery/evidence/b2c-windows-20261001/portable-console-settings-client.sanitized.trx). At immutable `88a02f36`, Windows 10.0.26200 X64, SDK 10.0.401/PowerShell 7.6.6, `-Category portable-console-settings-client -Parallelism 1`: 5/5, no failures/skips, exit 0, 3:37.1480884, complete owned cleanup. Covers scripted menu, interrupted QTE save/restart, ordinary/blocked language discard. No Windows PTY/browser/GM/audio/font/full-game or B3a execution claim. Preserve evidence bytes/CRLF and published hashes.

### B3a exact verification

The current normal source is byte-equivalent to the tested patched source. The [historical packet](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/tree/ff34aa221d2b7d88faaf3af7f25f3e0a4a737b3e/specs/1553-portable-local-storage/recovery) retains retired patches and exact before/after blobs. Before packet retirement and Windows documentation additions, applied tree was `865246abd9135a1e59a21033a8bdea7188a948da`. Normal reader manager blob is `3afd41d4a0a08780d32839c9a8f80dd1e158f35d`; catalog `b803fccaafbb24a5824d00984117b3ead6d3afa3`; selection `bf910c649b0c9d278996984f7c23ecbb41ae97ce`.

- [Build prerequisite](recovery/evidence/b3-reader-build-prerequisite/manifest.json): fixture CS0136, zero tests; not behavioral RED
- [Reader RED](recovery/evidence/b3-generation-reader-red/manifest.json): source `6f2e95e3`, 24/24 intended failures including bounded existing FIFO hangs
- [Common covering GREEN](recovery/evidence/b3-generation-reader-green/manifest.json): `a6fc91c7` plus its exact packet, 138/138 reported passes, exit 0, 4:27.2557159; 24 reader + 34 bootstrap + 6 integration + 3 unit + 71 publication
- [Legacy commit-gate RED](recovery/evidence/b3-legacy-generation-gate-red/manifest.json): `fca31f35` plus packet, 7 executed/6 reported passes/1 intended failure; runner stopped after integration, three unit cases unrun. [Correction GREEN](recovery/evidence/b3-legacy-generation-gate-green/manifest.json): `81cee6b2` plus packet, 10/10 reported passes, exit 0, 3:50.9894749. The real pre-publication fault proves original-reader admission and exact evidence retention, not successful legacy load replacement
- [Source-guard RED](recovery/evidence/b3-generation-source-guard-red/manifest.json): `cd90edb9` plus packet, 1 intended failure with valid fresh NoBuild prerequisites. [GREEN](recovery/evidence/b3-generation-source-guard-green/manifest.json): `32be77c8` plus packet, 1/1, fresh unit build, exit 0, 2:17.6065775; checks the actual wrapper/shared strict parser and retains other lease/classification assertions
- [Final discovery](recovery/evidence/b3-generation-final-audit/manifest.json): 115 categories/10,310 methods, zero tests, exit 0, 10.9127409 seconds. [Final plan](recovery/evidence/b3-generation-final-plan/manifest.json): four categories/five descriptors/140 planned cases, zero executed, exit 0, 9.4020393 seconds. The 140 plan is not another test run

All covering runs recorded complete owned cleanup. Three retained legacy physical-reader assertion bodies are Windows-gated and did not execute on Linux; their reported passes do not prove Windows behavior. The 21 sanitized B3 artifact hashes, counts, source equivalence and catalog ownership were independently checked. The final selection is portable-session-generation, portable-session-generation-consumers, portable-client-bootstrap and portable-storage-publication. Exact source/patch identities, commands and original/sanitized hashes live in each bundle and the archive. Do not repeat passing cohorts solely for normal publication or documentation changes.

<a id="current-checkpoint--b3b-accepted-ordinary-readers-next"></a>

## Accepted B3b — replacement, residue and root keys

- Clear and worker-only rotation prepare one complete generation/deletion member set under lifecycle-then-replacement leases and publish through the existing B1 journal. Full namespace/type/conflict preflight precedes mutation; uncertainty retains evidence. Only an established committed transition/recovery advances revision. Selected input/output/ready JSON, game_state, lore/stories and worker controls retain their exact exclusions: bridge status/window binding and opaque gm_context_pack, plus unselected config/saves/images/mods. The context-pack subtree is excluded before descent; lookalike siblings and Linux case-distinct members retain their proper selection.
- Clear deletes files durably and may leave structural empty directories. The browser pending inspector/form projection share leased, non-following logical-content inspection; zero-byte files remain content and links/wrong types/unreadable entries remain errors. The actual console AfterlifeLocalActionGuard already ignored directories and now has nested-residue coverage. The historical ConsolePendingProjection name was corrected to BrowserPendingProjection because the production caller is ExplorerWebCommandService; historical RED bytes remain unchanged. This is helper/caller coverage, not a new console process run.
- Proposal-ID reuse removes only a validated recursively-empty destination after generation/task admission. Nonempty/unknown/link/type conflicts and original legacy evidence stay intact. Linux proposal positives reach the explicit Windows-only directory-backend rejection and prove admission, **not successful proposal publication**. Full worker staging/publication remains later B3 work.
- One shared in-process root key normalizes supported ordinary/extended drive and UNC spellings for revision interning and session bindings, preserving Linux case distinctions, root separators and existing comparers. BasePath and actual lock/journal/member paths stay unchanged. The native correction applies the existing Windows handle-path normalizer to both comparison operands, retaining original acquisition, identity/type/link checks. The strengthened alias writer asserts its direct pre-write SessionReplacedException and both generations, preventing an unrelated earlier lease error from being hidden by the final operation fence.

### B3b distinct verification evidence

These are separate runs at their recorded sources, not one aggregated result or a rerun on the final tip. Exact commands, sources/patches, counts and original/sanitized hashes are in each manifest.

- **Linux initial replacement:** [15-case RED](recovery/evidence/b3b-replacement-initial-red/manifest.json) (14 intended failures, one retained legacy guard pass), then [15/15 GREEN](recovery/evidence/b3b-replacement-initial-green/manifest.json) at `00e2ce0b` plus its exact packet
- **Linux expanded boundary/consumers:** [37-case RED](recovery/evidence/b3b-boundary-red/manifest.json) (seven intended failures), then [86/86 reported GREEN](recovery/evidence/b3b-boundary-green/manifest.json) at `f38a68f6` plus its packet: replacement/console guard 38, bootstrap 34, ordinary writers eight and UI-owner consumers six. Includes actual hard-link/outside-byte, invalid namespace, unknown-member/generation, FIFO and four abrupt cold-recovery scenarios. The extended-Windows-root body returned early on Linux and supplies no native evidence
- **Linux proposal admission/consumers:** [8-case RED](recovery/evidence/b3b-proposal-red/manifest.json) (three intended failures), then [30/30 reported GREEN](recovery/evidence/b3b-proposal-consumers-green/manifest.json) at `438d5bbe` plus its packet: proposal eight, integration 13 and unit nine. Three Windows junction/dangling-link bodies returned early; Linux proposal admission does not establish the worker backend
- **Linux root keys/consumers:** [6-case RED](recovery/evidence/b3b-root-keys-red/manifest.json) (five scaffold failures, one retained Linux pass), then [17/17 GREEN](recovery/evidence/b3b-root-keys-green/manifest.json) at `37a56703` plus its packet: six root-key cases and eleven exact bound-operation/cache consumers. Pure Windows spelling policy is distinct from filesystem execution
- **Discovery/planning, zero tests:** [121-category/10,340-method audit](recovery/evidence/b3b-final-audit/manifest.json), [123-case Linux plan](recovery/evidence/b3b-final-linux-plan/manifest.json) and [six-case Windows plan](recovery/evidence/b3b-final-windows-plan/manifest.json) at `bcecdd3a` plus its packet. After the comparison correction, [fresh builds/discovery](recovery/evidence/b3b-handle-fix-audit/manifest.json) at `82e30691` plus its packet validated **122 categories / 10,340 methods or files**, no unmapped/stale selectors, exit 0; [exact plan](recovery/evidence/b3b-handle-fix-plan/manifest.json) selected two descriptors/seven cases, zero executed
- **Native Windows original result:** [six cases at `830e160d`](recovery/evidence/b3b-windows-paths-20261001/manifest.json), **4 passed / 2 failed**, exit 1, 3:36.8466801. All bodies ran. The extended-root clear and alias lease failed at the physical handle/expected-path comparison. Three link/junction rejection cases passed; the original alias writer's reported pass proved only the outer exception/unchanged bytes because the final fence could hide the lease error. This result remains immutable historical RED
- **Native Windows correction:** [`e5ee470eecf3da01172894f3c81a3fe4da25709d`](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/e5ee470eecf3da01172894f3c81a3fe4da25709d), tree `6a2bcd2d7abde7f5db20c7e44353dbcf7773f61d`; `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category portable-storage-windows-handle-comparison -Parallelism 1` passed **7/7**, exit 0, **3:15.4926899**, Windows 10.0.26200 X64 / SDK 10.0.401 / PowerShell 7.6.6. The two reproduced failures, strengthened direct alias fence, two opened-lock path-swap rejections and two hard-linked-lock variants all executed; fixtures succeeded, both descriptors completed, no skips/unrun cases/timeouts/duplicates, and owned cleanup completed. [Manifest, summary and two TRX files](recovery/evidence/b3b-windows-handle-comparison-20261001/manifest.json), manifest SHA256 `99468aefa0d7b35eb986170d8a639efa1cd4466afe93b491db574fd4e4e9083d`, retain exact identities and hashes

All listed completed behavioral runs retain owned cleanup evidence. Independent review verified native hashes/counts, causal assertions, source invariance and normal delivery before the 12:37 UTC bounded acceptance. Actual Windows scope is the selected local filesystem scenarios; no actual UNC-share, broad storage, full game/new-game, GM/provider, browser, audio or PTY acceptance follows. Storage replacement does not make the full NewGameFlow atomic: later bootstrap writes occur after lifecycle lease release. Provider-writer shutdown remains B4. Current GM payload schemas and gameplay mechanics are unchanged, so this client-owned storage/documentation closure requires no new GM gameplay example.

## Accepted T030-E — ordinary canonical readers

Ordinary async/sync text and bytes, validated presence and bytes-plus-real-handle-timestamp snapshots now share the trusted-local open/read helpers with B2. Exact bytes/BOM/null/empty, cancellation, bounded sharing retries (including observed Linux errno 11), recovery/rechecks/quiescence, no-reacquire/absence policy, hooks, real owning leases and B2 generation fences are preserved. Browser v6 rollback explicitly uses original readers for tracked before-images **before recorder attachment**, manifests, restoration and cleanup. Private physical, runtime/archive/backup/load/receipt readers and the nonrecursive generation reader remain unchanged; no identity is fabricated and no full transaction boundary is migrated here.

- **Linux RED and partial results:** [initial reader RED](recovery/evidence/ordinary-readers-red/manifest.json) at `e4990d8c` plus packet executed 15/15 (11 pass/four intended FIFO failures). [First covering attempt](recovery/evidence/ordinary-readers-covering-failure/manifest.json) at `642f5828` executed 9/44 (eight pass/one transient-lock failure, 35 unrun); an earlier malformed launcher argument started no workload. [Retry diagnostic](recovery/evidence/ordinary-readers-retry-red/manifest.json) at `eaa7266d` observed actual `IOException.HResult = 0x0000000B` (one failure, 15 unrun). These are preserved failures, not aggregated passes
- **Linux covering GREEN:** [76/76](recovery/evidence/ordinary-readers-green/manifest.json) at `e688dfbeea2890c96691215306a02faa8760be9c` plus packet, applied tree `d5ed321e58a1c4085ba3793dc685a37ac9f4e858`; seven descriptors complete, exit 0, **6:00.3004909** including fresh integration/unit builds, no failures/skips/timeouts/duplicates and complete owned cleanup. The four selected owners cover ordinary APIs, actual hard-linked coordinated input/publication with unchanged outside alias, handle timestamp, Linux FIFO pre-open/post-hook rejection, absence/observation/recovery, actual StateManager bootstrap and UI-lock consumers
- **Discovery and planning, zero tests:** [125-category/10,347-identity audit](recovery/evidence/ordinary-readers-final-audit/manifest.json) and [25-case native plan](recovery/evidence/ordinary-readers-native-plan/manifest.json), both at `4ea9b3b5` plus packet, used the exact fresh successful builds and completed cleanup. After source review identified two stale physical-hook fixtures, the [fresh fixture build/plan](recovery/evidence/ordinary-readers-fixture-plan/manifest.json) at `9091b6d9` plus its one-file packet built both projects and planned the unchanged two-descriptor/25-case selection, exit 0, **5:28.0471434**. None of these commands executed a test, and no native RED was invented
- **Native Windows GREEN:** [25/25 actual cases](recovery/evidence/ordinary-readers-windows-20261001/manifest.json) at normal source `29f485748819a8d95fd94b483f96e98c3f6e4b81`, tree `539e296627618c3497d67b5d872e6c522c9b18f3`; `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category portable-ordinary-read-windows -Parallelism 1`, Windows 10.0.26200 X64, SDK 10.0.401/PowerShell 7.6.6, exit 0, **3:07.1109345** including fresh builds. Integration 13/13 plus unit 12/12, both descriptors complete, no failed/skipped/unrun cases, timeout or duplicates. All mandatory hard-link/junction fixtures and platform bodies executed; FIFO was excluded. Both corrected fixtures used real leases/original-handler writes and asserted exactly one physical-hook hit; quarantine asserted actual absence. Owned launcher/runtime/synthetic-fixture remainders were **0/0/0**. Manifest SHA256 `b9aac0ac60a8f09943fa2ad3f35e07a3a6104420d8739102e097e72a53f2da40` retains source, method/case multiplicities and original/sanitized hashes

Independent source re-review accepted both fixture P2 corrections at **14:12 UTC**, publication/evidence readbacks confirmed exact equivalence and artifacts, and the final **14:50 UTC** Astra XHigh review accepted T030-E with the native result. The final evidence checkpoint changes no runtime/tests/catalog/selection. Normalization and failed/partial/zero-test/live results remain separately traceable in the immutable plan and [recovery history](recovery/README.md#accepted-t030-e-recovery). No accepted B3b cohort or passing Linux reader cohort was repeated solely for delivery.

This establishes the selected ordinary reader and retained original-reader boundaries, not full accepted-turn atomicity, successful worker backend, save/load, GM/provider, actual browser/console game flow, audio or PTY acceptance. Socket creation was denied and device fixtures were unavailable; neither was retried/created, and only owned FIFO supplies Linux special-file runtime evidence. Current GM payloads, gameplay/ownership schemas and logical snapshot/history/once-only authority are unchanged; no new GM gameplay example is required.

## Remaining B3 accepted-publication and save/load

The **T031-A ordinary coordinated-helper slice** is accepted with its exact evidence and limits recorded above. The [source-backed recommendation](research.md#t031-proposed-coordinated-publication-boundary) remains the approved scope: one complete existing B1 decision with initial guards/leases/bytes preserved, explicit original recorder/recovery routes, narrow error propagation and distinct storage versus later gameplay acknowledgment. Full accepted-turn/normalizer/browser-v6/worker/save-load migration remains outside this slice.

Follow the [concrete source map](research.md#later-b3-accepted-publication-seams-read-only-2026-10-01). Recorder-free normalizer writes already use B2; signed copied-cut recovery exists. Preserve logical one-shot/history/snapshot authority and only group complete boundaries justified by focused evidence. The final mechanics loop does not encompass earlier normalizers or later held-treatment settlement. The observed Linux LiveTurnPreparationService → DeleteDirectoryTree → kernel32 dependency is preserved in that historical map and [partial-run evidence](recovery/evidence/coordinated-consumers-partial/manifest.json). Later T030-F/T030-G work qualifies the exact real Linux quarantine consumer as well as its earlier native result. T031 helper acceptance cannot close full Linux turn-preparation readiness.

Deferred health/reminder calls write system_mods.json, plus config.json only when EnabledSystemMods normalization changes it. Reuse detached manifest preparation under their existing active lease and one B1 member set; preserve signed pending-snapshot game_settings.json bytes and do not apply menu pending-GM admission inside active repair. Browser v6 recorder/staging, exact-granted external Daren profiles and save/load staging/restoration remain explicit migration work with original-handler-or-block recovery. Accepted T031-A reuses B1 with CommitGate→lease, zero-write/guard-only, duplicate and uncertainty semantics preserved. Real GM turn/save-load/restart acceptance remains required.

## B4 persistent interactive GM and B5 platform helpers

The [B4 research](research.md#b4-transport-narrowing-recommendation-2026-10-01) retains a .NET 8 owner, repaired ConPTY and a small Linux PTY/supervisor as the proposed shape. Exploratory libvterm qualification did not establish an acceptable candidate, and its further diagnostic was stopped under a platform restriction. The earlier libvterm recommendation is withdrawn; the node-pty/xterm fallback remains unqualified. This records status only and does not establish a newly verified vulnerability or authorize further diagnostic work. Users must not need a C compiler. Preserve one persistent arbitrary CLI, readiness/liveness, manual/automatic input arbitration and per-profile paste/framing/newline/submit/interrupt/exit. CancelQueuedRequest, InterruptCurrentTurn and StopSession/StopOwnedScope differ; post-submit timeout may be UnknownOutcome despite known liveness. Ordinary turn completion does not restart the CLI.

The [OpenCode mini/pure evidence](recovery/evidence/opencode-mini-probe-20261001/) proves two PINE/OAK turns in one OpenCode 1.18.34 PTY with multiline bracketed paste plus separate Enter, zero model tools and exit 0. It does not prove recall, full-screen TUI, native clipboard gestures, all-descendant cleanup, Windows or game-bridge integration. Reuse this profile when integration is ready; do not repeat an unintegrated probe. Managed-browser diagnostics stopped after an explicit organization restriction; no further diagnostics/tunnel workaround belongs to this task.

[B5 research](research.md#b5-platform-helper-map-read-only-2026-10-01) identifies both MP3 decoding and WaveOut output, owned audio lifetime and honest device/backend errors; clipboard adapter plus actual composer error propagation; terminal font capability and premature preview-save copy; existing portable desktop-open dispatch with visible failures/manual paths; and browser audio lifetime/documentation drift. Miniaudio is only a qualification candidate. Keep B2 preview ownership, narrow QTE newline work to reproduced cases, and select actual consumers. No B5 hardware/runtime capability is established by source inspection or the five Windows B2c process checks.

## Verification, documentation and durability discipline

Follow [development workflow](../../docs/development-workflow.md), [testing](../../docs/testing.md), the constitution and existing Spec Kit artifacts. Each bounded code block uses behavioral RED, necessary GREEN, exact consumer ownership, saved source/evidence before long runs/review and independent Astra XHigh review. Run scripts/test-csharp.ps1 with affected categories only; discovery-only audit validates metadata. No full-suite/Fast/PreMerge/all-category sweep, shared mutable fixtures or redundant passing reruns. Keep failed, partial, zero-test, source-only, scripted-process, live and unrun evidence distinct. GM formats/ownership or workflow changes require synchronized operational guides/examples/guards in the same block; storage-only helper changes do not invent gameplay examples.

Publish bounded WIP before long tests/review and verify the complete remote SHA and tree. Large file normalization may use an exact small recovery packet followed by a serialized normal Git publication; never leave unverified local bytes as the only copy. Keep each connector payload bounded, pre-yield long calls, report a stall promptly and do not blindly repeat uncertain writes. On HTTP401 stop new edits, check existing authentication harmlessly, reconcile existing objects/ref and retry the same non-force update once if access is restored; otherwise request reconnection through the coordinator. Do not alter credentials/security settings.

Restore from GitHub and current [recovery instructions](recovery/README.md), not conversation summaries. The old workspace disappearance has no proven cause; scratch remains replaceable. Preserve small sanitized summary/TRX manifests and a role-based environment/canary fingerprint, excluding credentials, private paths and unrelated inventory. The earlier vanished runner remains inconclusive; its denied/incomplete attempt and authorized isolated successful run are separate archived evidence. No unknown lease or process ownership may be bypassed.

Dependencies remain R0→B1→B2→B3; B4 research can overlap but source/ref ownership is serialized. US1 maps to B1/B2, US2 to B1/B3, US3 to B3/B4/B5, US4 to B4 and US5 to B5. Tasks mark only accepted bounded outcomes; full Linux/Windows readiness waits for actual remaining capability evidence.
