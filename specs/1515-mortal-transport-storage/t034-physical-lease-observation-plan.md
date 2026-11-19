# #1515/#1589 physical-lease observation: bounded qualification

## Goal and limit
The accepted mm23 capture proves terminal polling exhausted its existing 200 open attempts (199×50ms delays), but cannot identify one sustained holder versus repeated holders. Main-operation close markers do not show physical lease intervals; the canonical lock file is empty. Do not change retries, timeout, exceptions, owner admission or mm23. A known-holder test alone is redundant: the deliverable is the missing opt-in physical interval evidence, qualified in that same existing contention fixture before any further live decision.

## Existing source seams and finite change list
- `Core/FileSystemManager.cs`: `AcquireCanonicalWriteLeaseCoreAsync` physical constructor at4795, successful-open postvalidation catch4827–4841, three real successful lease-return branches (MainMetadata, SessionFinalization, ordinary), typed exhaustion4817, and `CanonicalWriteLease.DisposeAsync` actual `stream.DisposeAsync` around482. Preserve all existing control flow and disposal behavior.
- `Services/GmRuntime/MainOperationFailureDiagnostic.cs`: reuse its DEBUG-only `ReadNonce()` and best-effort stderr pattern gated by exact valid `BOE_MAIN_STOP_DIAGNOSTIC_NONCE`; add a small typed physical-lease observation/write method, not another logger/service/process abstraction. No file or canonical-state writes.
- `Core/FileSystemManager.MainRunFence.cs` only if needed for a read-only snapshot of already held admission tuple; never call DescribeClose to manufacture a terminal outcome, never reread/adopt main.json. Existing `_access`/retained original/remote data are the sole sources. Where a tuple is absent or unavailable, emit null and explain that limit.
- Existing `GameEngineTurnLifecycleTests.TerminalWaitContention.cs`, its current category `mortal-terminal-wait-contention`, and a small pure serializer/noninterference control in the same fixture/category. No new world/provider/relay/native-host framework.

## Observation contract
Capture one diagnostic-only occurrence per actual acquisition call when a valid nonce exists. Use process-local monotonic sequence plus PID, normalized root, requested purpose, UTC capture time and Stopwatch timestamp; optionally copy the existing full original operation/pin/close-ID/owner tuple as immutable DATA, never authority. Same occurrence follows its stream through disposal. No request/body/file content or secrets, no global retention/cache.

Emit these finite facts through the existing stderr channel:
1. `physical-opened` immediately after the successful FileStream constructor, BEFORE subsequent handle/path validation. This is physical possession, not a granted canonical lease.
2. `lease-granted` only at each existing successful return after its existing checks.
3. `physical-dispose-completed` only after actual stream DisposeAsync returns successfully, both pre-lease validation cleanup and ordinary lease disposal. Preserve the occurrence captured at open; do not invent a close on attempted/failed disposal. A separate `physical-dispose-failed` may record exception type only and leaves completion UNKNOWN.
4. `contention-exhausted` at the existing final supported sharing-failure branch, before throwing the exact same typed exception. Record observed attempt count, not an inferred holder. Optional first-contention event can establish the interval without per-attempt noise; no diagnostic record quota or sampling that loses lifecycle facts.

No-nonce/release builds remain no-op without JSON/timer/event allocations. A nonce change refuses emission; a failed diagnostic sink/serialization cannot change return, exception identity, retry count or cleanup. Logging may observe transitions but never authorizes retry, release or admission. Avoid blocking/deferred background writers or another channel lifecycle.

## Test-first within one coherent block
Extend the CURRENT three modes rather than execute a separate redundant known-holder project. Use the existing actual physical contention hooks and original independently opened raw canonical FileStream holder. A second public lease is unsuitable here because its outer gm-main-owner guard blocks the waiter before canonical acquisition. Release the raw handle only at first actual contention (released mode) or owned finally (exhausted mode), with no sleep as causality. Its actual open/dispose timestamps are separate test receipts, NOT BOE events or authority; assert released timestamp precedes waiter physical-open. Production waiter events are released1open/1grant/1close, exhausted0open/0grant/0close plus1exhaustion, integrity1open/0grant/1close. Existing five public-lease tests independently qualify actual granted-lease disposal. For exhausted mode retain unchanged retry window and exact typed failure, then release/join in owned teardown. For post-open-integrity use the existing sentinel: opened then successful physical cleanup but NO grant, same original sentinel identity.

Initial causal assertions require missing events AFTER the existing actual result/cleanup assertions. No nonexistent API should cause the RED. Add bounded pure output tests for missing/wrong nonce, malformed optional tuple refusal, throwing writer/serializer and exact exception/result preservation. Actual lifecycle cases verify one completion per real stream despite repeated Dispose, no positive completion on a controlled close failure if an existing seam can express it without replacing real disposal. If no such seam exists, verify failed-write/noninterference separately and do not pretend a synthetic close is actual OS-close proof.

Then implement only the observer plumbing, independent SOURCE review, fresh owning build and the one explicitly counted existing contention category; no broad suite. RED→GREEN is one test-first block, not sequential standalone projects for known mechanics and later observability. Existing invariant snapshots (request/ready/weather), original waiter joined, fresh subsequent lease and cleanup remain mandatory. Do not run before the design/source gates.

## Sufficient conclusions
An isolated GREEN proves emitted intervals correspond to real acquisition/grant/release and controlled competition, with noninterference and truthful absence on failures. It does NOT identify the historical mm23 holder or prove the normal retry policy wrong. It enables a later separately reviewed capture to distinguish actual competing processes/operations; any attribution still requires overlapping open→completed-dispose facts for that run. Missing events/sink failure/absent tuple remain UNKNOWN. No mm24 or live attempt is authorized by this plan.

## Accepted design refinements and initial test source
Same occurrence is captured before constructor and follows successful open through pre-grant failure cleanup and all three grant branches. Capture transition timestamps before calling the sink; physical-dispose completion precedes parent/main cleanup and stays true even if later cleanup fails. Existing nonce checks remain entirely no-throw/read-only. Initial test source extends existing3 lifecycle modes and adds5 actual-acquire nonce/sink/repeated-dispose cases (official8 total); production is unchanged. Missing physical events after otherwise proved actual results are intended RED. Direct typed serializer malformed-tuple controls are required before GREEN once its concrete typed API exists, not a missing-API compile failure in RED. No live/world run.

Initial expected RED is six failing cases/two passing missing-or-malformed nonce cases, not eight failures. Controlled wire assertions inspect ALL emitted physical rows: no nonce filtering may hide resealed disposal after nonce change; each grant joins its own opened/completed-dispose occurrence with ordered transition timestamps.

Corrected source follows independently verified partialRED6961749f: raw-holder restoration fixes only the fixture admission mismatch. Production remains unchanged; next exact8 run must still fail six missing-observer cases with two absence controls passing after original behavior and cleanup. No new lock or owner API.

## Corrected RED accepted; instrumentation WIP
Independent review accepted corrected outcome e9060ab5: fresh Integration56.53s (coarse shell57s),134warnings/0errors; official exact8 in22.7716963s gave6 causal missing-observer failures and2 absence passes. All original contention/result/cleanup/fresh-lease invariants preceded these failures; no setup errors. Earlier partialRED6961749f remains distinct. Source32064 and8 outputs joined.

Current unrun implementation captures one immutable context per physical acquisition and passes it to the actual lease; all3 grant branches and both actual disposal sites report the same occurrence. Remote tuple is the already held immutable original handshake, local tuple absent because local OperationPin has no protocol identifiers; no DescribeClose, reads, connection, authority or lock-policy change. Added typed malformed-tuple/nonce/stage/wire/throwing-sink controls inside existing repeated-dispose case, keeping exact8. Synchronous stderr overhead is unmeasured and can block; only exception/policy noninterference is claimed. No asynchronous logger or cache: per-acquisition immutable DATA reuse ends with the actual lease, timestamps/current nonce remain fresh. No-nonce path avoids context/JSON/timer allocations. Source review and fresh owning Integration GREEN remain required; no historical holder attribution or live/mm24 permission.

Before GREEN, two compile-only setup failures were preserved: missing file-scoped alias (7.15s), then mistaken local OperationPin.Reply reference (32.92s). No tests ran. The latter confused private RemotePin protocol mapping with the local reference-counted pin. Source reassessment removed the unavailable projection rather than inventing IDs or looking up another owner. Remote original grant remains held immutable DATA; local OriginalPin is explicitly null. Independent full added-symbol audit precedes the next build.

## Current result: bounded GREEN independently accepted
Outcome d662a484 independently verified all19 artifacts and exact32064 source/8output joins. Fresh owning Integration113.50s (shell114),182warnings/0errors; official PlanOnly8 and actual8/8PASS in25.0551318s, no timeout, complete owned runner cleanup. Released waiter emitted same occurrence open→grant→completed-dispose at attempt2 after actual raw-holder release; post-open sentinel emitted open→completed-dispose/no grant; exhausted waiter emitted only exhaustion at attempt200. Original behavior, task closure and fresh subsequent lease passed. Missing/malformed/changed nonce, throwing sink, repeated disposal and typed wire/tuple controls passed. Two earlier compile-only setup failures remain distinct.

This qualifies finite local physical interval observation and exception/policy noninterference, not remote-owner lifecycle or exceptional OS-disposal completion. Authored remote tuple serialization is DATA only. Synchronous stderr overhead has not been isolated; whole-category timing is not a speedup measurement or nonblocking guarantee. Historical mm23 holder remains UNKNOWN. No live/mm24 run, prior-world repair or lock-policy change is authorized. Future clean packages must include the actual new owning compiled outputs and sources; old immutable runtime packages must not be patched/reused as current.

## Actual remote-helper qualification follow-up: fixture SOURCE WIP
Reuse existing `terminal-main-operation-helper-snapshot-stopping` only. Its pre-Stop actual canonical write already acquires a SessionMutation lease under the retained remote helper; initial generation capture may acquire another, so do not demand one total mutation. Driver sets a fresh nonce only in that PowerShell child's environment. Fixture captures real helper PID and before/after-write monotonic timestamps while alive, then after actual Dispose requires both reply/stderr joins and validates ALL physical rows from retained stderr against nonce/PID/root/full Active original grant. Each occurrence requires unique ordered open/grant/dispose; at least one cycle lies within actual pre-Stop write bounds. Existing same-endpoint snapshot-end refusal/Failed exact close/durable Stop/guardian remain unchanged. No production/runtime/world changes.

Fresh owning Unit/TestSupport and exact official existing regression3 (one native plus two tiny PS compatibility cases) required after independent source review. Native15s/outer20s unchanged; no local8 rerun. Full source/compiled-output/native/package joins must be current, never old immutable runtime substitution. This follow-up is UNRUN and does not authorize live/mm24.

## Actual remote-helper result: independently accepted
Outcome374126e6 verified all36 artifacts, source32064/owning8 output joins and6 actual producer DLL/PDB/native/compiler joins. Fresh Unit/TestSupport39.63s/70warnings/0errors; exact official3/3PASS15.3691565s. Actual helper emitted9 physical events: separate generation, actual write and finalization triples, all same full Active original grant/nonce/PID. Write occurrence2 is strictly bounded by the real pre-Stop write timestamps, not the generation cycle. Same helper then refused actual snapshot-end after Stop and settled exact Failed close with both reply/stderr joined; original owner Stopped/OwnedScopeEmpty and guardian complete/no emergency/failure/deadline. Raw UTC bytes remain intact; PowerShell derived row timestamps reserialize at local offset with the same instant/fraction, not identical text.

This closes the bounded actual remote projection/physical lifecycle gate only. Local8 were not rerun. Historical mm23 holder remains UNKNOWN; full live scheduling/P01/P02/P03 and exceptional OS disposal failure remain unqualified. No new live launch or prior-world reuse is authorized. Future preparation must use current compiled production, not old immutable producers with only PS overlays.

## 2026-11-19: explicit physical-only routing, test-first WIP

Tracked #1589/#1515. Both independent design reviews accepted the closed DEBUG
contract: physical observation defaults disabled; exact `stderr` is for controlled
fixtures only and requires the external path to be absent; exact `external`
requires an explicitly owned private external directory and matching 32-byte
`owner-nonce`. Unknown/empty/malformed or changed route/path/nonce suppresses
physical output without fallback. Existing failure/stop stderr is unchanged.

The initial test-only block preserves production bytes and adds twenty real
public-lease routing cases to the existing eight-case local category (28 planned).
Existing local and native fixtures now explicitly request stderr. Cases cover
default/unknown route, valid stderr/external, rejected path/marker/privacy/link
forms, route/path drift, actual disposal and unchanged failure channel. All are
UNRUN pending saved source review. Initial RED cannot claim later negative
assertions ran after its first failing assertion. Before GREEN the concrete sink
API also needs explicit empty-value DATA (environment setters may remove empty
values), special entry/collision/partial failure controls and fresh marker drift;
these must not be silently claimed from this initial matrix.

Implementation remains gated on causal RED. Use only an immutable per-acquisition
route/path/nonce DATA record and fresh per-emission validation after the existing
transition timestamp. TrustedLocalFileScope is ordinary namespace DATA, not a
retained authority handle; require ValidateDirectory(false), concrete private
Unix directory/marker/event modes and no links/special entries or canonical-root
overlap in either direction. Observer creates no directory or marker. Each event
is a private CreateNew PID/occurrence/closed-stage file, written and closed
synchronously; collision/partial failure is retained UNKNOWN with no retry,
repair, deletion or stderr fallback. No registry/cache/worker/async logger.

Optimization: reuse immutable occurrence/route identity only during its lease;
mutable route/marker/path checks remain fresh per emission. Synchronous IO may
block and perturb lock timing; promise exception/policy noninterference, not
timing neutrality or exhaustive holder coverage. A completely dropped occurrence
is undetectable. Measure observed event count/bytes and whole inert elapsed.

After reviewed local GREEN: existing remote3 with explicit child stderr, then
ONE existing fullroute1 external same-PTY gate under unchanged45/55/60. Require
actual foreground PID intervals, zero physical prefix in raw/emulator/cells,
physical raw exactly equal to ordered emulator WRITE bytes and normal owned
cleanup. No mainwire run, no live world, no mm24 preparation yet. Fresh owning
Integration and Unit/Support outputs are required after production changes.

### Routing RED accepted; minimal implementation WIP

Saved test-source b26fb8f3 was independently accepted and run once: fresh
Integration build69.02s/160warnings/0errors; exact PlanOnly28 and actual28
completed23.8894143s,19causal FAIL/9PASS, no timeout/setup failure and owned
cleanup complete. All nineteen failures are forbidden physical stderr output
after real acquisition/disposal. Existing8 and explicitstderr positive passed.
The first failing assertion prevented downstream external assertions/fresh-lease
checks in those nineteen cases; they are not reported as qualified. Independent
review joined18artifacts,32065source and8outputs. No native/UI/world run.

Implementation WIP now adds a closed raw route/path parser, immutable captured
route/path and fresh drift checks, private external marker validation and one
CreateNew event per PID/occurrence/stage. Existing Mark/Report are unchanged.
External routing currently requires Linux special-entry classification and Unix
private modes; other platforms refuse rather than invent an ACL/special-file guarantee. Explicit controlled stderr remains portable
DEBUG behavior. Scope retains only ordinary namespace DATA, no handles/authority.

The same routing theory now has26rows, with fresh marker/nonce changes, actual
FIFO marker/directory and linked-parent refusal; one additional typed DATA Fact
covers absent-versus-empty raw values, duplicate/partial file preservation, FIFO
event refusal and the actual serializer with a partial-throwing writer. This is
35planned local cases including prior8. Partial-write DATA does not claim a real
OS disk-full/close-failure reproduction. Production never deletes/repairs these
files; only the isolated fixture deletes its own temporary tree after assertions.
All implementation/expanded tests are UNRUN pending exact source review.

### Routing local35 and actual remote3 independently accepted; UI source WIP

Saved4a83d1c6 source passed local35/35 with fresh Integration116.53s,
183warnings/0errors, actual24.2185702s, owned cleanup complete. Independent
review joined18artifacts and32065source/8outputs; all expanded controls ran.
Fresh Unit/Support32.76s/44warnings/0errors then exact remote3/3 passed
15.3317252s with explicit child stderr. Independent review joined36artifacts,
32065source/8outputs/6producer assemblies and native provenance; actual helper
PID529 emitted9correlated Active-pin events, one write-bounded mutation cycle,
then original end refusal/Failed close and durableStopped/OwnedScopeEmpty with
helper0, bothreadjoins and clean guardian. No new game/world or historical-holder
claim; no actual diskfull/OSclose-failure qualification.

Next source-only change is existing smoke.py fullroute branch: private external
trace directory/marker before the same foreground child, explicit child-only
route/nonce/path. After actual child0/EOF/receipt, check current childPID/root
correlation, exact observed triples/private files, no physical prefix in raw or
acknowledged snapshots, and raw==captured==ordered emulatorWRITE payloads. Record
observed count/bytes and check/whole elapsed; missing whole events remain
undetectable. SamePTY, UI grammar, terminal controls and45/55/60 unchanged.
This UI wiring is UNRUN pending source review; do not infer foreground qualification
from local35 or separately drained helper stderr. Owning32.76s build can be reused
for Python/doc-only wiring if exact compiler/output joins hold. No mainwire or live.

### External fullroute FAILED, independently verified; no retry

The sole source-feff35c0 fullroute attempt failed1/1 at original45.031495s
(official56.2528697s). Independent review joined129artifacts,32065source/8outputs
and exactly two noncompiler deltas versus the32.76s build. Locked denial returned
empty action with unchanged DATA; retrieve opened its storage panel only.
No transfer/Save/deposit/Load occurred. Raw12686bytes equals26ordered emulator
WRITEs; raw and all26snapshots contain no physical prefix. External69events,
30528bytes,23complete triples belong to actualforegroundPID304/currentnonce,
private0700/0600 paths and exact localroot/nullpin. These are observations of the
captured prefix, NOT accepted whole-UI qualification or exhaustive holder proof.

Original child was killed/joined/drained and owner termios restored; sidecar
ABORT-15 joinedEOF is not normalCLOSE. Normal childEOF/configuredrestore and
managed receipt are absent; inert fixture remains diagnostic DATA. Guardian
ECHILD/driver1/noemergency/failure/deadline proves physical containment only.
No new live world or package is admitted; no timeout increase/replay/repair.

Both reviewers reconstructed the narrow inert caller error from source/protocol:
selected inventory frame WRITE43 ends11617 atcursor26; final pre-key drain
WRITE45 adds49spaces to11666/cursor75, then Enter. Existing choose stores its
successor cut BEFORE send, unlike live finite_select's post-send returned cut.
Thus predecessor grammar can see49continuation spaces as a standalone old frame
before the real close and correctly refuse. Local11617 is source-derived, not
logged variable evidence. Next bounded test-first DATA must exercise actual
nestedchoose+real parser and finaldrain, compare11617/11666, audit BOTH inert
arrow/Enter boundaries and uncertain-send state. Options/Save post-send cuts and
Save ACK's deliberately retained full outcome interval remain unchanged. No
parser/geometry/name/lock/sink/budget change is justified by this failure.

The next test-only source block keeps actual production/driver logic unchanged:
existing Python DATA grows17→20 methods. The AST seam retains actual run setup
and all nested definitions and replaces only the unexecuted route tail with
choose/state return. Real parser, retained-window checks and fresh_route_key
remain active. Authored source-shaped failed-window rows exercise all67footer
splits (including no-extra-padding baseline), finaldrain same-selection redraw
before one Down, and uncertain Enter preserving original exception without
publishing a new cut/previous. Planned initial RED is three new method failures
with original17 unchanged; actual results remain UNRUN until source review.
No raw terminal capture is committed and no source guard is relaxed.

### Inert action-cut causal RED accepted; narrow correction SOURCE WIP

Independent t022 review accepted pure20:17passing methods/3failed methods,
68failure entries and0errors in0.458s (Enter66 padding continuations, arrow
stale boundary, uncertain Enter premature state). All32065source/8output pins
were unchanged. Immutable failed-raw DATA separately confirms cut11617 refuses
while actual input cut11666 yields the two complete successor panels; no process
or emulator was launched. The source-derived localcut remains distinguished
from the directly recorded inputcut.

Correction moves only BOTH inert choose boundary samples to after successful
synchronous send. Actual callback still returns None; no API/parser/live-client
change. A thrown uncertain send cannot publish cut/previous state. Existing
final-drain predicate and original deadlines remain unchanged. Two trivial
len(captured) reads require fresh mutable state, not a cache; no extra parsing or
preparation is introduced. Source review and pure20 GREEN are pending; the
external samePTY fullroute remains FAILED/unqualified until a separately gated
single inert run. No new package/world or mm22/mm23 replay is admitted.

Independent t022 outcome review accepted the corrected pure20 GREEN:20/20PASS
in0.427s, zeroerrors, exact32065source/8output before=after and only the two
chooser/doc RED→GREEN deltas. The immutable failedraw comparison is also exact.
The source correction is accepted within authored DATA scope, not actual UI.
Next requires one independently admitted existing fullroute1 in a new absent
diagnostic root, same100x25/samePTY and45/55/60 limits. Current compiled source
and eight outputs equal the32.76s owning build; four explicit noncompiler
deltas are smoke external wiring, chooser, its DATA and this plan. No rebuild
or local35/remote3 rerun is warranted by these Python/doc-only changes.

### Corrected external same-PTY inert gate accepted

Independent geometry review accepted outcome943983bc…a524d and213artifact hashes:
exact PlanOnly1/actual1PASS14.8962632s, inner20DATA PASS, driver3.2226348s.
Current32065source/8outputs equal before=after with exactly the four declared
noncompiler deltas versus the32.76s owning build; Node/17dependencies exact.
Raw51876bytes equals94orderedWRITEs in189command/reply pairs, with94snapshots
and normalCLOSE. Two real upstream CPR5;1 replies and51routekeys remain distinct.
All168private external events/56complete triples match actualforegroundPID303,
currentnonce/exact isolatedroot/nullpin; no physical prefix occurs in raw or
any snapshot. Five actual stage receipts pass, including retrieve/deposit,
Save s/emptydescription/turn43, and Load archive focus→Back without commit.

Child0/EOF/configuredbaseline restore and separate owner-original restore are
proved; fixture removed, sidecar normalCLOSE/join/EOF/empty stderr and guardian
ECHILD0/noemergency/failure/deadline. The earlier failed outcome remains separate.
Trace validation9.68ms is NOT isolated sink-write overhead; synchronous IO may
perturb timing and observed intervals are not exhaustive. This closes the
bounded external-routing inert UI gate, not hosted Load, live GM, historical
mm23 holder attribution or new package/world admission. Next is read-only exact
current-production package preparation review; no old world/package mutation.

### Current producer refresh SOURCE WIP, not executed

The accepted owning build is physical-routing-remote32.76s, not historical39.63s.
Read-only re-enumeration still finds723current bin members (236/241/246) and
exact18changed paths versus old mm23 input pins, but the full newly frozen map,
not that count, is the admission authority. New current producer uses three
standard ProductionMainLinuxFixture publish commands with --no-build --no-restore,
existing native provenance and exactly three publish extras per project: native
supervisor/manifest plus restored EventLog.Messages win runtimeTarget. No overlays.

Authored publish-current.py/sh reuse existing run_owned guardian per50s publish,
with full source/bin/Unit/tool/native/restore-asset admission before checkout
imports or destination creation, strict full published membership and post-command
source/bin checks. Failed/unknown preparation stops without repair/retry. Scripts
and concrete external input manifest require independent admission before execution.
No game/world/export is included. Live external route inheritance is a separate
read-only source mapping now: every relevant process must use an admitted owned
marker matching its actual nonce; default-disabled silence is not coverage proof.
No already accepted local/native/UI test is repeated for package-only preparation.

Producer admission review corrections: compare full published hash+mode rows to
current bin+three extra asset rows, without chmod/repair, and reassert the
original reviewed input-manifest hash at every closing check. Still UNRUN.
Live trace design uses actual canonical base/play, not merely game_session;
pinned profile values must match launch environment/marker, and failed partial
assets remain retained/refused rather than repaired or reused.

### Fresh producer accepted; live trace admission test-first SOURCE WIP

Geometry independently accepted producer79ac6a83…cbaa/all745artifacts: actual
239/244/249published inventories equal current bins+three exact assets in hash
AND mode, zerooverlays. Three guarded standard publishes completed1.3195/1.1183/
1.1683s with ECHILD/driver0/bothEOF/noemergency/failure/deadline. Closing32067
source/723bins/14assets/tools/8Unit pins remained exact. No world/export occurred.

Next test-first block extends existing test-live-terminal-fences7→11 methods,
without executing live module: real temporary trace namespace/env matrix, actual
Terminal/RPC constructor AST, actual final child-environment→Popen slice, and
actual receipt prelaunch slice. Production/profile sources remain unchanged.
Initial missing callable is feature RED only: its downstream matrix/launch
refusal controls are explicitly UNEXECUTED until the real API exists. Matrix
requires pinned profile versus environment, actual play-root separation,
private regular marker32bytes, no links/specials, no mutation/repair/fallback.
Actual child scopes use the existing shared StopNonce; failure/load nonces are
not physical identities. Trace checks remain fresh per launch because env and
namespace can drift; no cache or new process abstraction is introduced.
Source review and bounded pure11 RED are pending. No live/profile/world action.

Pure11 RED matched7PASS/4explicit missing-callable failures/0errors0.446s,
independently accepted. Minimal admission SOURCE WIP now adds real pinned-profile
load and read-only private external trace validation; all actual direct child
paths check it, with final client environment formed before any PTY acquisition
and rechecked before Popen. Profile hash is mandatory, no old-profile fallback.
Shared inherited helpers remain unchanged. Actual28namespace cases plus profile
hash/refusal controls and constructor/RPC/receipt sentinels await GREEN review.
Old rollback tests now supply real trace assets; affected Mortal38 setup builder
supplies the same exact profile/env/marker without bypassing real admission.
No exclusive profile creation or launcher wiring is claimed yet; those require
the separate actual-function source/DATA gate before any world preparation.

Independent t022 accepted pure admission GREEN:11/11 in0.529s and affected
Mortal38/38 in1.049s, all source/output/artifact joins exact. This proves admission
DATA only, not profile construction or launch. Next test-first source extends
existing submit/profile DATA26→28 with two actual-source methods: exclusive
trace-function namespace and generated launcher assignment (P01/P02). No complete
profile script is executed. Expected initial failures are missing creation API
and absent physical-route/profile-hash exports; downstream creation controls
remain unexecuted until implemented. Newly owned prepared phase is supplied by
existing fresh-root preparation; trace/profile/launcher must not already exist.
Failed partial trace remains retained and blocks re-entry, no delete/chmod repair.
The whole phase/root absence admission remains with the existing preparer.

Independent exclusive-profile RED accepted:26PASS/2feature failures/0errors0.145s.
Minimal SOURCE implementation now checks trace/profile/launcher absence before
profile writes, creates only owned700directory and exclusive0600marker via
os.open, serializes actual profile once and hashes those bytes, and creates the
launcher exclusively0700. Failed partial assets remain, no chmod/delete/repair.
Actual-source tests now reach both P01/P02 launcher composition with the digest
returned by real profile serialization, unique exact export lines, and refusal
of existing/dangling outputs before trace creation. Full profile/world remains
UNRUN; pure28 GREEN is pending source review. No published runtime bytes change.

Independent exclusive profile GREEN accepted:28/28PASS0.161s, exact source/output
joins. Old manual preparation still requires five bin deltas/eight overlays and
old producer/package/log paths; it is NOT a valid new-current-producer contract.
Next narrow source correction will require complete current==producer output
maps, zero overlays, explicit reviewed noncompiler source additions, separately
pinned native directory and the actual current standard publish evidence paths.
One new actual-function pure method in the existing test file establishes this
contract; select ONLY CurrentProducer (one method), not the already closed28.
Test-first source is UNRUN and main preparation remains unchanged. No mm24
creation/export or fake no-op overlays; old worlds and producers untouched.

CurrentProducer pure1 missing-contract RED independently accepted0.005s; no
other tests ran. Minimal manual SOURCE now removes old overlay/delta application,
requires producerOutputPins==outputPins and contentOverlays==[], and binds reviewed
noncompiler changes/additions to a finite explicit Python/doc path set, not suffix
exclusions. Native directory is separate from the new producer; actual published
metadata/outcome/helper-publish evidence hashes are checked before and after.
Full destination package equality still includes private-mode normalization only.
Pure1 adds props/arbitraryPython/resource refusal controls; SOURCE/GREEN pending.
No world preparation or producer mutation has occurred.

Independent CurrentProducer GREEN accepted:one selected method PASS0.006s,
positive+all11negative subcases, no skips; all32067source/8outputs and six evidence
hashes exact. No closed28/native repetition. Next is only read-only final mm24
input/readiness assembly using accepted fresh producer239/244/249, current
admission/profile sources, zero overlays and separate native provenance. Actual
setup-only export/profile creation requires independent integrated source/input
gate; launch and response publication remain later explicit gates.

Integrated preparation review added two concrete closure requirements before
creation: reuse accepted publisher six-tool resolved path/SHA pins at admission
and closing, and bind actual published full hash+mode rows to its pinned immutable
published.json before copying and at closing. Both are now SOURCE WIP; existing
CurrentProducer pure1 is extended with actual-function mode/member and tool-drift
controls, not a new suite. Final current input must include toolPins. No prep ran.

The two integrated closure corrections are now independently source/DATA accepted.
The same affected CurrentProducer method passed once (0.008 s, exit 0), including
actual-function tool and full published-mode refusal controls; all 32,067 sources
and eight owning outputs matched before/after. External outcome `03c2ac81…` retains
full evidence. This is detached DATA only, not real tool/preparation admission.
Current mm24 input must bind accepted publisher tool pins and this saved source;
independent integrated input review remains mandatory before any seed export.

## mm24 scripted P01 accepted, P02 not started

Both independent outcome contours accepted external outcome `96c2d329…`: all
10,720 artifact hashes, single authenticated request/response and accepted turn42,
current package/source/output joins, canonical materialization and normal owned
cleanup. The same-request post-consumer marker preceded the sole shutdown;
daemon errors remained 0→0, original scope stopped empty and all children exited0.
Observed physical trace: 9,873 events, 3,291 complete ordered triples, 4,796,628
bytes across 53 total PIDs, including foreground20 and bridge50. No malformed or
partial observed file or PTY physical prefix was found. Captured intervals include
instrumentation; neither exhaustive coverage, timing neutrality nor the historical
mm23 holder is established. P01 used the explicitly scripted authored scene, not
an independent GM decision or access grant. P02 remains unstarted. Its next gate
is exact retained predecessor/package/world binding plus exclusive fresh trace and
profile, using the reviewed phase function; genuine GM choice is deferred until
an authenticated request, with no predetermined council consent or grant.

## mm24 P02 failed before request; autoplay work stopped

Independent outcome review verified external `26aa06e4…` and all 2,423 artifacts.
Original launch17433 exited1 after42.372s: no genuine action/request43, GM decision,
answer or accepted turn; shutdown attempts0. Seven committed inputs reached the
locked row and its returned inventory, then one Down. Source/protocol reconstruction
identifies a distinct descriptor-lifetime defect: live finite_select retains the
entry-only after_menu predecessor requirement after advancing the cut to20651;
the complete718-byte Equipment redraw contains no old predecessor-close, so the
strict parser refuses. Cleanup repeats that descriptor and also times out. This
is not the previous padding bug, geometry, echo or physical-trace contamination.
No controller/parser/timeout change or replay was made.

Story42, location and inventory remain unchanged; six operational control/context
files differ. Original owner remains Running/epoch2 and daemon unsettled. RPC
helper0/EOF, sidecar ABORT-15/join/EOF and guardian ECHILD/driver1/reaped6 prove only
their scoped closure/physical containment, not logical game cleanup. The trace has
1,649 captured events:549 complete triples and one bridge open/grant without a
captured dispose. mm24 is not a continuation predecessor and will not be revived.

The historical mm19 denial proof remains separate: exact probe `d0c4abcd…` contains
locked selection17961, Back/fresh prompt22914 and unchanged story/history/queue and
location/inventory hashes. Its later permission turn failed; this is not a combined
grant→transfer chain or a reusable predecessor. Ordinary player text already uses
the genuine supported relay path without an inventory prologue. There is no ID-
direct /storage_move console command or existing external manual-input handoff in
this owned driver. Shipped structured Web storage is a separate real host/session
path, not the current console endpoint. No new interface or browser architecture
was implemented. The selected next scope is an independently reviewed finite
ordinary-request→fresh GM decision→ordinary Save path on clean proven state;
transfer/Load remain separate later proofs. No new world or runtime is authorized
by this checkpoint itself.

## Selected short path: ordinary request and Save (design only)

Independent design review admits a bounded test-first Python/source block, not a
new world or runtime run. Use a closed `ordinary-save` route containing only Route,
requiring the accepted P01 predecessor. Restrict inventory layout preflight to the
two existing storage routes; no inventory IDs, selection, transfer or Load in this
path. Keep the existing request, typed receipt, story, owner and one-shot pipeline.
The current historical restored-data path is fixed to accepted44→45 and cannot
admit P01/42 unchanged; exact old P01 config bytes were not recovered. Prefer the
existing fresh seed→scripted P01→ordinary43 sequence, explicitly two live stages.
Failed mm24 is never revived or treated as a predecessor.

Extract the existing ordinary Save UI statements into one helper taking the exact
expected saved turn. Existing storage caller keeps expected_turn−1; ordinary caller
uses expected_turn. Before Save, require the same-turn consumer-return observation
within the original work660 bound (no renewed deadline); final cleanup retains680.
Then capture immutable per-Save DATA: accepted story; soul/player; world clock and
schedule; map/location index/current location; inventory/item identity; vehicles;
NPC core and NPC/item journals; actual containing file of any granted-state field;
and living-history bytes or explicit absence. Do not compare all operational pins
or synthesized resource-owner authority bytes. Save uses the unchanged Options→
Save→name→empty description→clean success→ACK→fresh-player path. Require exactly
one new archive, no existing archive overwrite, unique nonaliased asserted ZIP
members and metadata/current accepted turn, exact scoped archived DATA and live
post-Save bytes. Preserve queue/action/accepted-turn counts and pending/ready
absence. Absence of history never becomes a fabricated receipt.

Tests will use actual source functions/AST and temporary authored archive DATA in
existing Python test files only: closed route/binding and no inventory preflight;
real extracted Save statements/expected-turn distinction; archive/story/state/
history/duplicate-member refusals; consumer-return-before-Save and zero Save calls
on refusal/expired original bound; uncertain ACK and warning refusal. Freeze the
actual discovered method selection for independent source review before any run.
No C# suite, production change, finite_select fix, new protocol, browser or manual
input interface is part of this block. Original budgets and failure stop remain.
The per-Save snapshot is small operation-local immutable DATA, reused for archive
and closing comparison; it never caches ownership or permission.

The ordinary-save final success predicate must additionally require its explicit
matching Save proof (turn/story/archive/scoped DATA), not merely generic accepted-
turn cleanup. A pure actual-final-predicate negative covers accepted+clean with
missing or mismatched Save evidence. The finite scenario list above is chosen;
resource_owner_authority is intentionally outside byte equality because production
unconditionally recomposes it during Save. Authored archive DATA must model that
difference without rejecting a correct Save. Five test contours are route/binding,
actual Save and archive matrix, uncertain ACK, postaccept ordering/deadline, and
route-specific final success; actual method counts are frozen after authoring,
not guessed in this design. Only affected pure classes and directly affected
shared-Save caller controls will run after source review.
