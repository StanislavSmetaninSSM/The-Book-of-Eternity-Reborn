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

### Ordinary Save test-first WIP (UNRUN)

Eight selected `OrdinarySave` methods in the existing terminal-fences DATA module
author the five reviewed contours. The actual live module is never imported;
whole FunctionDefs and exact preflight/final assignments are extracted with AST.
Profile target guarding is checked from actual source; Save uses controlled UI
leaves and real clean-outcome/archive parsing. Temporary ZIP DATA permits
recomposed resource authority but refuses scoped byte drift, history fabrication,
duplicate metadata and path aliases. Original 11 fence methods remain separate.
Expected initial feature RED: eight methods fail at missing route/helper/gate or
existing inventory/final-success behavior; downstream new matrices remain unrun
until implementation. No runtime, world, production or finite_select change.
Source review and remote preservation precede this exact pure selection.

Independent test-source review strengthened the same eight methods before RED:
actual orchestration must capture fresh closing DATA, with live state/queue drift
refusing a proof; actual shared Save must reject an overwritten or deleted prior
archive despite one fresh ZIP. Scoped ZIP DATA duplicates and orphan pending
snapshot directories are refused. These downstream controls are still UNRUN.

### Ordinary Save causal RED and minimal source WIP

Independent review accepted initial pure8 RED: eight intended feature failures,
zero errors/skips, 0.025 s; full 32,067 sources and eight outputs unchanged. Missing
helpers/default deadline/route and actual inventory preflight/final-success gaps
were causal. Downstream archive/UI matrices were not executed in that RED.

Fixture-only implementation now adds ordinary-save binding without inventory IDs,
extracts the actual shared Save statements with expected-turn argument, and gates
ordinary Save after same-turn consumer return inside original work bound. It
checks finite accepted-state/story/history bytes, one new archive and unchanged
prior archive hashes, fresh post-Save DATA/queue, and explicit final Save proof.
Resource authority recomposition is excluded. The existing local Save guard test
follows the extracted helper and still proves it precedes deposit. No parser,
finite_select, production, deadline value or world changed. GREEN is UNRUN pending
independent source review. Mutable live/queue/archive checks remain fresh; small
per-Save DATA is reused, prior archives retain hashes rather than duplicate bytes.

### Ordinary Save pure GREEN accepted (not live qualification)

Independent review accepted the exact selected fourteen pure methods: eight new
ordinary-save cases, three existing clean-outcome cases, the shared local Save
interval guard, original final consumer-wait guard, and continuation binding.
All 14 passed in 0.166 s, no errors/skips; 32,067 sources and eight owning outputs
were unchanged before/after and matched reviewed source pins. The previously
blocked archive/closing/UI/deadline matrices executed. No C# build, native run,
new world, real GM decision, actual Save, transfer or Load was performed here.

This closes the bounded source/test block only. Next is a separately reviewed
fresh preparation using current fixture/profile bytes and unchanged accepted
compiled producer, then fresh scripted P01 and ordinary genuine request43 as
separate live phases. GM must decide from the actual prompt/current facts; no
preauthored grant. Failed mm24 is not revived. Original budgets, per-phase trace,
accepted predecessor and one-shot publication gates remain mandatory. The finite
archive scope deliberately excludes recomposed resource authority and does not
claim whole-state equivalence or a combined grant→transfer→Load PASS.

### Fresh mm25 preparation admission WIP (UNRUN)

Managed outputs and current standard producer remain reusable only after exact
closing joins. Ordinary-save changes add precisely two noncompiler paths beyond
the old finite eleven-path allowlist: mortal_console_routes.py and its directly
affected test-mortal-console-routes.py. The existing CurrentProducer method now
authors exact admission and mismatched-digest refusal for those paths; initial
RED is UNRUN. No broad allowlist, overlay, rebuild, old-world copy, new world or
export is part of this source checkpoint. Final preparation must independently
join the updated thirteen changed paths, two existing additions, exact producer
rows/modes, tools/native/terminal inputs and fresh absent destination.

Independent pure1 RED accepted the exact first missing-path refusal (0.004 s,
zero errors, unchanged 32,067 sources/eight outputs). The only preparation
contract change now adds those two reviewed path literals. GREEN is UNRUN pending
focused review. The original semantic design's locked-row/Back requirement is
historical: new ordinary43 has no inventory prologue or denial-prefix claim.
The mm19 denial remains separate evidence, not a combined scenario. P01 remains
scripted; a genuine ordinary43 response requires a fresh independent GM decision
and rationale after the actual request. No semantic template was changed to force
a grant; grant/denial and actual Save must be reported separately and honestly.

Independent affected CurrentProducer pure1 GREEN accepted: one method passed in
0.005 s without errors/skips; both exact added fixture paths, declared-digest
refusals and existing source/output/overlay/tool/mode controls executed. Full
32,067 sources/eight outputs were unchanged. This admits the finite thirteen-path
noncompiler contract only. Assemble and independently review fresh mm25 source,
current producer, modes/tools/native/terminal input joins before one setup-only
seed41 export and inert P01 profile. No new world or live action has run yet.

### Fresh mm25 setup-only preparation accepted

One standard guarded seed41 export and exclusive inert P01 profile completed.
Independent review verified all eighteen artifact joins; eight actual inventories
(4 native, 483 runtime/deployed each, 249 helper, 8 relay, 536 world, 9 terminal,
17 dependencies), full retained play and current 32,067 source/723 managed/eight
owning-output pins. Export took 3.274 s with child0, both EOF, guardian ECHILD,
no emergency/failure/deadline; preparation took 7.9138 s. Seed41 remains setup-only,
validation0/notaccepted. Separate semantic review matched six initial beforeimages,
three absent outputs and exact authored P01 action/template. No GM/game launch or
acceptance was part of preparation. Private trace contains only its matching nonce
marker. Original limits remain 600/660/680/720/750. Scripted P01 is a separately
authorized next operation; genuine ordinary43 decision and Save remain later and
unqualified. Failed mm24 and all prior uncertain worlds are untouched.

### mm25 scripted P01 accepted, separate ordinary continuation pending

Independent complete review accepted P01 turn42: one exact authenticated request,
nine reviewed CAS writes/five timestamp bindings, one publication/execution and
one accepted story append/materialization. Original operation exited0 in213.682s.
All10,045 artifact hashes, current source with only the declared preparation-doc
delta,723 managed/eight owning outputs, actual packages and108 final canonical
pins joined. The unique request/nonce/original-owner consumer-return marker was
observed before the sole shutdown; daemon errors remained0→0, exact ACK/Stopped/
OwnedScopeEmpty and normal terminal/RPC/guardian closure were verified.

All9,198 captured physical events form3,066 complete observed triples across54PIDs,
with3,102 actual held-pin events; no captured malformed/partial/unmatched rows.
This is observed evidence, not exhaustive holder coverage or timing neutrality.
P01 is scripted transport/materialization, not an independent GM grant or Save.
The clean predecessor may now supply a separately reviewed ordinary-save P02
profile: ordinary player permission request, no inventory prologue/IDs, fresh GM
choice only after actual43, then scoped Save proof. No43/Save has run yet.

### mm25 ordinary P02 failed validation; no accepted43 or Save

The one ordinary player request43 ran without an inventory prologue. A separate
GM authored a fresh, reasoned defer decision before its packet: Council consent
was not established; access/items stayed unchanged and conversation advanced
500→505. One five-CAS response executed successfully, but the client rejected it.
The independent failed-outcome review distinguishes transport execution from
acceptance: zero accepted turns, no OrdinarySaveProof, no shutdown attempt.
Original operation exited1 after473.689s; guardian ECHILD/reaped3 with no emergency,
failure or deadline proves physical containment, not logical owner cleanup.
The bridge stopped under its existing stalled-validation-repair policy; no second
response or repair was submitted. mm25 is now UNKNOWN/nonpredecessor, not reusable.

Twelve errors concern the authored debug markdown's exact NPC Scope/Actor Brain
field contract, including commentary parsed as another Relevant actor. The
independent pre-publication review missed that machine-checked schema; semantic
reasonableness was insufficient. A thirteenth error rejects existing-NPC inventory
resend from UpdateNPCs. This packet never wrote npc_core: its current bytes equal
the authenticated pre-turn original, retaining P01's historical steward carrier.
Source inspection shows ordinary pre-send recognizes historical UpdateNPCs rows,
whereas response inventory validation with a usable snapshot rejects a non-scene
actor carrying a historical envelope without an unchanged-row exception. This
is a separate bounded interpretation seam, not grounds to delete an actor or
inventory, bypass validation, or repair the failed world.

Full frozen evidence remains external (21,309 artifact hashes); current32,067
source pins and723/eight outputs joined with the declared prior document delta.
Historical mm19 denial remains a separate proof, not an inventory prefix here.
No grant, transfer, Load or ordinary Save qualification is claimed.

Next authorized block is test-only: exact failed/corrected authored debug bytes
through the existing public ValidateAcceptedTurnReasoningAsync fixture, and a
separate true-snapshot unchanged historical-carrier response regression with
changed-inventory/fresh-resend negative controls. Reuse existing validators and
fixtures; source review before execution and causal RED before any production
proposal. No new live world or retry is authorized by this checkpoint.

### Frozen test-only response-contract block (UNRUN)

Existing reasoning fixture tests the exact authored failed/fixed debug objects
against ValidateAcceptedTurnReasoningAsync, not a parallel markdown parser. The
failed object must yield the exact twelve known reasoning/scope codes; corrected
labels preserve the independent defer choice. Minimal authored actor/journal/action
premises are isolated, and validation must leave fixture bytes unchanged.

Existing actor-materialization fixture uses the actual LiveTurnPreparationService
to seal a turn43 original. Selected full-state ActorMaterialization validation
should admit an exact unchanged same-section historical UpdateNPCs carrier. The
explicit ValidateResponse API with the same complete actor remains a negative:
a raw command is not retained canonical state. Inventory count/add/remove (array
retained), unrelated full-row mutation, missing snapshot and tampered snapshot
remain negative controls. Original manifest/authority bytes and all test state
must remain unchanged by validation. Existing continuity already recognizes an
unchanged historical carrier; no production exception is implemented here.

Official proposed selections mortal-author-debug-contract (two cases) and
mortal-retained-carrier-contract (eight cases) separate the authored
debug cases plus eight carrier cases across the existing unit/integration projects.
Fresh owning builds, PlanOnly exact10, then one bounded run only after source
review. A preparation/error is not causal RED; the unchanged-carrier failure must
follow real snapshot/equality prerequisites. No runtime/live preparation follows
a result automatically.

The initial combined runner reached eight setup failures at BOM manifest decoding
before validator invocation; its fail-fast ordering left the two debug cases
unrun. This is not causal RED. The test now decodes manifest text with the same
BOM-aware read pattern as existing fixtures while retaining exact raw bytes for
identity assertions. Separate exact official selectors let unrun Unit2 execute
first, then the corrected Integration8 once, without repeating a failing group
merely to reach another descriptor. Production and failed world remain untouched.

### Authored debug qualified offline; retained-carrier causal RED accepted

Two preparation defects are retained separately: initial Integration8 stopped at
raw BOM manifest decoding before validation, and the first Unit2 lacked the
actor-specific validated memory context. Neither is a production RED. The final
fixture uses actual preparation and the actual memory context (Usable/Mortal),
signed core/journal originals, a first-person append and matching timestamp.

Independent review accepted the final bounded outcome: Unit2 PASS reproduces the
exact twelve authored debug errors and clears all issues with the corrected
schema while preserving the GM defer decision. Integration8 has seven negative
controls PASS and only the byte-identical historical UpdateNPCs carrier FAIL at
npc_existing_inventory_resend_forbidden. Real Usable original/equality and
post-validation immutable bytes precede that assertion. Other selected-profile
resource/soul/player/guardian errors remain explicitly outside this targeted
predicate; this is not whole-state validation or live readiness. All34 evidence
hashes,32,070 source/eight Unit outputs and Integration output joins were verified;
both official runners completed owned cleanup without timeout.

No production fix is included in this checkpoint. A minimal proposed follow-up
would distinguish the existing full-state validation mode from explicit response
commands, retaining only exact same-section historical carrier DATA under a
usable current original. Whole-row, inventory, missing/unusable-original and
explicit-response negatives must remain strict. No failed world is reused and
no new live qualification follows from this offline result.

### WIP: exact retained historical carrier mode boundary

After the accepted causal RED, pass the existing response-creation flag through
its sole inventory predicate call. Only full-state validation with a usable
original, exact UpdateNPCs section/envelope membership and whole-row equality
uses the already-established historical DATA distinction. Explicit response
commands and mutated/missing/unusable originals retain their refusals. Scene and
ordinary missing-original behavior are unchanged. Tests are unchanged; GREEN is
UNRUN pending independent source review and fresh owning builds.

Optimization: reuse the already-loaded operation-local pre-turn actor map and
existing duplicate-safe semantic JSON comparator; no extra file read, authority
lookup, cache or normalization. One bounded row comparison needs no new cache.
This is client-side interpretation of retained DATA, not a new GM command or
permission: existing GM no-resend instructions and afterlife contracts remain
unchanged. The worked failed/fixed authored debug fixtures preserve the separate
GM defer decision. No live world, package refresh or retry is authorized here.

### Retained historical carrier correction: bounded GREEN accepted

Independent fixture_premises review accepted the exact source and final outcome.
Fresh Unit build took 52.87 s (20 warnings, zero errors); actual official
mortal-author-debug-contract ran exactly 2/2 PASS in 11.678 s. Fresh Integration
build took 1.33 s (zero warnings/errors); mortal-retained-carrier-contract ran
exactly 8/8 PASS in 20.224 s. The unchanged usable same-section historical row
no longer receives the targeted inventory resend error; all seven mutation,
explicit-response and missing/unusable-original controls still reject. The
separate authored debug fixture reproduces all twelve original schema errors
and clears all issues with corrected labels, without changing the defer decision.

All 36 external evidence hashes, recorded 32,070-source membership, eight owning
Unit outputs and Integration outputs joined; built/after inventories match.
Both official runners completed owned cleanup without timeout or skipped cases.
Unrelated minimal selected-profile baseline diagnostics remain outside the
asserted inventory contract. This is neither full-state nor live acceptance.
No world was changed, no failed world reused, no package published, and no
further game run is authorized by this result. Existing operation DATA and
comparison are reused; no new read/cache or measured speedup is claimed.

### Next bounded block: coherent offline ordinary response, then fresh mm26

SOURCE DESIGN ONLY; no producer/world/phase has been started. The exact proposed
new destination /workspace/shared/mm26-20261119 is absent, including links.
Historical mm25 and other failed worlds remain untouched and nonpredecessors.

The accepted debug2 and carrier8 qualify the strict authored reasoning schema
and one inventory predicate, not a complete response. Close that gap through one
new case in the existing GameEngineTurnLifecycleTests combined Mortal birth
fixture: retain its genuine accepted42 setup and dispatch one ordinary43 through
actual ProcessPlayerTurn. Extend its existing callback by stage; do not clone the
validator list, reseal authored snapshot authority, add a transport or use a
failed world. The second request must carry a fresh usable original, current
session/turn/schedule and unchanged historical steward. A deterministic offline
defer response changes only journal, five elapsed minutes and normal outputs;
its schema follows the corrected authored debug. It is test DATA, not the next
live GM's decision. Require accepted43, appended story, no repair, retired
request/original/ready markers, exact preserved NPC/access/item state and typed
idle/pre-send completion. This exercises the real normalization, progression and
accepted-turn validation pipeline. It does not qualify console Save, transfer,
Load or live transport. Reuse existing cleanup/runtime/seed and independent
per-test state; no new harness, cache, production or controller change.

After independent source review, run only the new official bounded lifecycle
selector with fresh owning Integration build and exact PlanOnly count. Existing
passed debug/carrier groups need not repeat unless their premises change.
After independently accepted dry evidence, freeze actual current source/build
and full three bin inventories. Reuse the existing three guarded standard
no-build/no-restore publisher, zero overlays, six tool hashes, native/EventLog
provenance and closing hash/mode equality. The old producer is not presumed
current after C# changes. No publish before its exact input admission review.
Then the existing guarded seed41 export/exclusive P01 trace/profile preparation
may create only the reviewed absent mm26. Actual package/cleanup and semantic
prelaunch gates precede scripted P01. Accepted P01 must settle before a separate
ordinary-save43 profile/request and fresh independent GM decision. Original
600/660/680/720/750 budgets and one-shot reserve remain unchanged. A justified
defer counts only if the ordinary turn is accepted and its normal Save succeeds;
it is not a grant or a combined transfer/Load result.

The frozen test-only implementation uses one optional continuation callback in
the existing combined fixture, selected only by a new exact integration case.
All existing42 assertions finish first. The second dispatch directly validates
its real signed reader authority and exact original/current retained core; it
requires an UpdateNPCs historical steward with empty inventory and turn42.
It loads the existing corrected authored debug DATA, binding only actual actor
name/ID/location; existing output helper gains a default-off no-due omission
flag, requiring both actual due counts zero and absent report. Existing callers
are unchanged. The actual engine must accept43 with one story append, clock505,
exact journal afterimage, preserved relevant canonical bytes and retired markers.
Official selector mortal-ordinary-response-dry is exactly one case, UNRUN pending
independent source/command review. No Save or live claim follows from dry success.

The first dry attempt stopped before test discovery: Integration build succeeded
in 59.68 s (135 warnings, zero errors), but PlanOnly refused the new category's
related entries because they were strings rather than required id/when objects.
No fixture/turn ran, and this is setup-only, not a causal RED. Correct only those
two catalog entries; reuse the exact successful build after closed source/output
joins, then independently reviewed PlanOnly1/run1 in a fresh evidence directory.

### Coherent offline ordinary43 pipeline: accepted dry result

Independent fixture_premises review accepted the one actual engine case:
PlanOnly1/run1 PASS, 17.949 s test / 28.173 s official runner, no skips/errors,
timeout or duplicate test IDs, owned cleanup complete. All 90 external artifact
hashes and current six source pins joined; built/after 32,071 source membership,
eight Unit and eight Integration outputs are exact. The existing fixture accepted
birth42, then an ordinary43 deterministic defer with real signed core/journal
original, no due report, exact preserved inventory/access/history carrier,
clock505, one new story row and retired markers/directory/typed idle/pre-send.
No unexpected engine diagnostics were logged. This is complete bounded in-process
response-path evidence, not live GM, console Save, transfer or Load evidence.
The earlier catalog-only setup failure remains separate; no C# rebuild/repeat
was needed after that correction. Next is the separate exact current producer
input admission, then clean preparation; no package/world has yet been created.

### Current producer accepted before mm26 preparation

A final incremental owning build closed the catalog-only provenance gap in
1.35 s (zero warnings/errors), with unchanged compiled outputs, not newly emitted
binaries. Before/built source and eight Unit outputs matched; all 723 bin bytes
and modes plus Integration outputs remained identical to accepted dry evidence.
No allowlist or preparation implementation changed and no tests were repeated.

Independent fixture_premises review then accepted exactly three existing guarded
standard --no-build/--no-restore publishes. Actual complete 239/244/249 published
hash/mode inventories equal current bins plus native2/EventLog1 assets, with
zero overlays. Original children exited0 with both EOF and normal ECHILD guardian
reports, no emergency/failure/deadline; elapsed 1.119/1.073/1.318 s. All 746 artifact
hashes and closing 32,071 source/723 bins/eight owning outputs/assets14/tools6
were independently checked. This accepts producer bytes only; mm26 is still
absent. Next is exact separately reviewed seed-export/profile preparation, not
automatic live launch. All failed worlds remain untouched.

### Fresh mm26 preparation accepted; live remains unstarted

Independent fixture_premises review accepted the one standard preparation:
all 18 artifact hashes and eight actual hash/mode inventories matched
4 native / 483 runtime / 483 deployed / 249 helper / 8 relay / 536 world /
9 terminal source / 17 dependencies. All 539 retained play hashes matched the
exclusive profile's beforeimage. Fresh setup-only story41 has validation0 and
no accepted GM turn. The sole seed export exited0 in 2.725 s, both EOF, normal
ECHILD guardian, no emergency/failure/deadline and empty stderr; preparation
reported 7.278 s. Current source/owning/package joins were unchanged.

The generated P01 launcher and pinned profile were independently reconstructed
and checked with the original budgets. Physical trace is private and contains
only its exact StopNonce marker. No launch, origin, queue, request, probe or
candidate exists. mm26 is technically prepared, not live-qualified; separate
fresh semantic baseline and explicit one-launch admission still precede scripted
P01. Ordinary43 and its Save remain later independent stages, with no inventory
prologue and no predetermined GM outcome. Old failed worlds remain untouched.

### mm26 scripted P01 accepted as a clean predecessor

Independent fixture_premises review accepted outcome f3cf22ee: all 10,153
artifact hashes, 108 final canonical pins/config, runtime483/helper249 and
current32,071 sources/723 bins/eight owning outputs joined exactly, with only
the declared preceding checkpoint-doc delta. The genuine request42 received
one independently reviewed nine-CAS/five-UTC scripted response, execution0,
typed NotRequired480→500, and exactly one story42 after setup41. Storage remains
locked, chalk retained, cart parked and empty, steward historical UpdateNPCs42.

The exact post-consumer marker/Completed close preceded the sole shutdown;
same original owner reached Stopped/OwnedScopeEmpty, daemon errors0→0, all
terminals exited0 with EOF/restores, sidecar normal CLOSE/join/EOF and guardian
clean. Elapsed207.23 s. Captured external trace has9,306 valid events/3,102
ordered complete context-consistent triples across54 PIDs; no physical prefix
in PTY raw. This is observed evidence, not exhaustive coverage or timing-neutral
instrumentation. No attribution of historical failed-world holders follows.

Next is a separately bound ordinary-save43 phase, without inventory prologue
or target IDs. The GM decides from its fresh actual request; no grant or defer
is preselected. Accepted43 and ordinary Save are not yet proven; transfer/Load
and historical mm19 denial remain separate. All failed worlds stay untouched.

### mm26 ordinary43 accepted; manual Save failed before publication

The fresh independent GM decided before authoring to defer chalk issuance under
current council-consent facts. The one reviewed five-write response preserved
access/items/core, appended only the steward journal and advanced500→505. Its
complete corrected debug schema was independently compared to the validated
fixture. Genuine43 was accepted with typed no-due evidence and a new story;
this is neither a grant nor a transfer. The consumer-return marker is captured
at20:09:56.6206292Z. No second response or repair was submitted.

Independent fixture_premises review verified failed outcome e254848d and all
12,434 artifact hashes. The real Options→Save→name s→empty description path
reached the application. After description cut629957, a current nonce/PID
failure marker starts629970: CoordinatedStatePublicationUncertainException
wraps IOException `Main run metadata or original owner admission is unavailable`.
The fixture's later clean-Save timeout is downstream, not an unrecognized
successful Save frame. No manual ZIP or success ACK exists; autosave42/43 are
not substitutes for the requested manual Save.

Source places the failure at initial ordinary canonical lease acquisition in
SaveLoadService.Creation.cs255, before held-lease archive preparation/publication.
Options calls this service directly; the canonical frame is nonparticipating
(FS4730), and MainRunFence178 refuses a non-Stopped main without retained original
access. Turn autosave uses the same Save service but runs under an admitted
turn context. No early-owner-release or physical-lock-holder attribution follows.

Original exec29705 exited1 after289.253s. Shutdown0, durable epoch2 Running with
no StopEvidence, foreground closure unproved; sidecar abort-15/join/EOF and
ECHILD guardian/reaped7 mean physical containment only. Captured trace11,522
rows has3,840 complete triples plus one open/grant without observed disposal,
60 PIDs, no malformed rows or physical prefix in client raw. This is not an
exhaustive observation claim. mm26 is now UNKNOWN/nonpredecessor; do not retry,
repair, continue or reuse its world. Accepted ordinary GM43 is proven separately;
manual Save and the combined scenario are FAILED. No production fix or new run
is implied by this checkpoint.

### Next bounded causal block: ordinary manual Save under an active main

Tracked #1515/#1589 continuation; no mm26 reuse, package refresh or live run.
The proposed regression uses the existing native guardian/neutral BridgeHost
fixture and actual typed InGameOptionsMenu with queued ordinary input. Seed one
PortableSaveFixture before starting the genuine owner, use a distinct consumer
FileSystemManager with no ambient grant, and retain the exact Running record.
Reuse the existing narrow FixtureHost wrapper through test-only accessibility;
no cloned reflection logic, synthetic main record or new transport/harness.
A direct unadmitted SaveLoad call remains an explicit refusal control. Capture
the menu result/failure, always stop/join the original owner, then assert the
positive archive/metadata/protected DATA and clean owner outcome. Initial RED
must be the actual admission error after setup witnesses, not setup/cleanup.

The intended participating boundary is the explicit Options Save action after
name/description entry, not prompts/ACK and not every arbitrary SaveLoad caller.
After causal RED, reuse existing SessionOperationContext current-generation
participation for that action only; close before rendering/ACK. Preserve the
actual SaveCreationResult through a later main-close failure, including a
committed-but-blocked result, with no retry or relabeling. MainRunFence, global
SaveLoad admission and foreign/closing/identity guards remain unchanged.
Independent source review must settle the exact typed result handling before
implementation. Existing console rollback/committed-warning controls and a
small affected original-main guard selection accompany GREEN, not broad suites.

Optimization: one real owner setup per new positive test, existing fixture and
service DATA only. No new cache, registry, worker or repeated discovery; owner,
generation, acquisition and closing checks stay fresh and cannot be cached.
No performance improvement is claimed; native setup/test elapsed is measured
separately. Test/source preparation remains UNRUN pending design/source gates.

Test-first WIP: one `manual-save-active-main` Integration case now authors the
actual neutral-host/menu boundary and direct default-admission refusal. It uses
the existing host wrapper with only test accessibility changed, the existing
guarded child dispatcher, typed shared runtime/turn43 and exact Running bytes.
The child freezes the menu exception and completes original normal cleanup
before the outer positive assertion, so expected RED is not a killed owner.
Production Options/SaveLoad/MainFence remain byte-unchanged. All new source is
UNBUILT/UNRUN pending independent source/command review.

Initial manual-Save regression build stopped before discovery/run: CS8418 at
the new fixture AudioService lifetime (IAsyncDisposable requires await using).
Reported56.65s/165 warnings/one error; no native child/test, not causal RED.
The exact test-only lifetime correction is WIP; production remains unchanged.
Failed evidence is retained separately, with no blind runtime retry.

Corrected manual-Save causal RED independently accepted (9e65b17e): owning
build 59.95s, 139 warnings/zero errors; official exact one menu test fails only
at the positive Save assertion. Actual active original, direct unadmitted Save
refusal, protected bytes and normal original Stopped/OwnedScopeEmpty cleanup
all pass. The real menu reaches MainFence178 via CreateSave255/Options1221,
creates no archive, and retains the admission failure. All 44 evidence hashes
and built/closing pins join. This is distinct from the earlier compile-only
failure. Next correction is only post-input Options Save participation, with
established outcome and replacement precedence retained; no failed world reuse.

Options-only correction WIP: after both input prompts, existing participating
current-session admission encloses actual Save and closing. The exact decision
maps to existing main outcomes; uncertain/blocked decisions prevent readmission.
Typed later close failures retain SaveCreationResult; actual replacement remains
the original exception, with committed notice only and no success ACK/retry.
SaveLoad and MainFence are untouched. Exact bounded GREEN selection is five:
real hosted positive1, actual post-commit close/replacement2, existing portable
committed-warning/rollback menu2. Replacement control alters only its isolated
fixture generation at the established finalization hook, never hosted authority.
One owner per test and fresh generation/closing checks; no caching or framework.
All implementation/control changes remain unbuilt/unrun pending source review.

First correction run is not GREEN: five executed, hosted positive and both
existing rollback/warning controls pass, two new late oracle assertions fail.
Build122.46s/187 warnings/zero errors; official runner14.926s, cleanup complete.
Independent46-artifact review confirms exact typed committed/replacement results
before the failing assertions. Source proves TextComposer paste draining consumes
prequeued keys before Save, so remaining-key assertion was invalid. The test now
counts every delegated ReadKey including fallback and snapshots the count in
real post-commit closing, after both prompts; no later read is permitted.
The long notice capture was not persisted, so wrapping is not claimed proven;
the isolated renderer now has deterministic240-column width and emits complete
capture in test output and on failed assertion. Production bytes are unchanged. Outcome817852f4 is
retained separately; corrected source review precedes any further execution.

Corrected bounded GREEN independently accepted (d94490f5, 46 exact artifacts):
fresh owning Integration build54.41s/139 warnings/zero errors, official exact
five tests PASS/zero skips/errors/timeouts, runner14.605s and complete cleanup.
Actual hosted manual Save produces unique valid ZIP43/save_turn43 with exact
asserted protected members and success notice; direct unadmitted API remains
refused, original Running identity closes Stopped/OwnedScopeEmpty normally.
Both real post-commit closing controls retain the decision; generation replacement
wins unchanged, committed notice is captured, no ReadKey occurs after closing.
Existing known rollback and committed-warning menu cases remain passing.
Full source32072/8 owning outputs built==after and Integration joins are exact.
The original compile-only failure and later3PASS/2oracleFAIL remain separate.
Only Options caller participation changed; SaveLoad/MainFence remain unchanged.
This qualifies the isolated hosted/manual boundary, NOT new producer, live GM
plus Save, Load/transfer or reuse of mm26. No additional runtime is implied.

mm27 current producer independently accepted (aa5930c5/published685ad072):
one Unit owning refresh31.42s/45warnings/zero errors joined current Main/Bridge/
Support copies, leaving723 producer rows and accepted Integration8 unchanged.
Three standard guarded no-build/no-restore publishes completed in about1.119s
apiece, original guardian ECHILD/driver0/both EOF/no emergency/failure/deadline.
All747 artifact hashes and complete239/244/249 byte+mode inventories join
current bins plus native2/EventLog1 extras, zero overlays; source32072/tools6/
assets14/Unit8 close unchanged. This accepts producer bytes only. Next stage
is one separately reviewed fresh mm27 setup41 export/profile, then separately
admitted scripted42 and ordinary43. No failed mm26 bytes are reused.

mm27 fresh setup/profile independently accepted (1cb64a17): standard export
setup41/validation0, no accepted GM, export2.974s/total7.419s with clean original
guardian/EOF. All16 artifacts, eight exact inventories and full539 play bytes
join current producer; exclusive private trace/profile/launcher intact. Exact
P01 launcher c8cdca08/profile629ce308 keeps original600/660/680/720/750 limits.
Independent semantic baseline agrees with unchanged scripted P01 template.
Only a separately authenticated genuine42 response may be published once;
ordinary43 decision and accepted-turn Save remain separate, unqualified stages.

mm27 scripted P01 independently accepted as closed predecessor (cb9d2e1d):
all9812 artifact hashes/current108 world+config/runtime483/helper249 and source/
723+8 exact with declared saved doc delta. One genuine42 response39451a7a,
execution0/story41→42/typed NotRequired480→500; closed storage/chalk and parked
empty cart materialized. Exact post-consumer original close precedes sole
shutdown, same owner Stopped/OwnedScopeEmpty, daemon0→0, terminal/RPC/sidecar
normal EOF/restores and guardian clean. Captured8964events/2988orderedtriples/
54PIDs/4399231bytes, no client trace prefix; not exhaustive. Next separate
ordinary-save43 asks permission without inventory prologue or presumed grant.

mm27 ordinary43 outcome independently verified as split, not overall PASS
(217e9d5e,13376 artifacts): genuine independent GM defer was accepted once,
500→505 without access/item changes. Actual ordinary manual Save committed
`s`/turn43: sole ZIP CRC good,110 unique entries, exact13 scoped DATA members
and accepted story/history absence. This is NOT autosave substitution. However
no Save ACK/fresh-player proof or normal shutdown occurred. Strict Save window
at cut679589 contains a641-byte BOE_MAIN_OPERATION_MARKER active-received from
actual foreground21/currentnonce/original Active pin, then exact successline.
Maintained emulator also shows success; exact parser correctly rejects extra
JSON. Source OpenAsync54→Mark→Console.Error195 routes the healthy participating
admission marker into samePTY. Physical trace prefix remains absent; this is
not the old admission refusal, geometry, timeout or Save publication failure.
Owner remains Running, sidecar abort−15/joinEOF, guardian ECHILD/driver1 proves
physical containment only. mm27 is nonpredecessor, no ACK/retry/repair/reuse.
Next source-only design must route healthy markers via existing explicit
external diagnostics (or narrowly suppress healthy-only output), preserving
failure diagnostics; no parser stripping, grammar relaxation or timeout change.

Bounded healthy-marker routing plan (source/test-first only, reviewer design GO):
- Keep all failure Report/Write stderr bytes and exception semantics unchanged.
  Healthy Mark honors existing explicit diagnostic route: external goes through
  the same fresh private/nonfollowing/outside-root/32-byte owner-marker/CreateNew
  checks as physical trace; invalid explicit configuration never falls back.
  Preserve legacy nonce-only stderr ONLY when both route and directory are absent;
  explicit stderr requires absent directory. No new environment switch/logger.
- Four existing Mark calls receive their already-known files.BasePath as ordinary
  DATA. Closed existing stages only; distinct PID-marker-occurrence filenames,
  actual pin/close payload unchanged, capture time before synchronous sink IO.
  Factor only existing private writer checks; no authority reads, retries,
  overwrite, directory creation, async collector or timing-neutral claim.
- Test-first causal seam reuses the isolated genuine hosted manual-Save fixture.
  In its child only, actual consumer stderr and actual menu use the same writer,
  restored in finally. Exact post-description success interval must be clean;
  actual Active marker must instead join external PID/nonce/original operation.
  This is a merged-stream model, NOT an actual PTY/emulator qualification.
  Existing original owner cleanup still precedes outer feature assertions.
- Minimal direct Mark controls retain unset/stderr behavior and reject malformed
  explicit routing while proving actual failure Report remains on stderr. A
  tiny mixed-sink DATA check must authenticate physical versus healthy filename/
  prefix/stage/nonce/PID disjointly, reject unknown/partial rows, and keep physical
  triple invariants untouched. Live freezing must not silently ignore new files.
- SelectionPrompt smoke remains physical-only: its isolated local fixture never
  opens a hosted original, so no healthy Mark is expected; do not broaden that
  assertion. relay_phase_evidence admission validates namespace+marker only and
  does not enumerate event payloads. No terminal parser/grammar, timeout or world
  changes; exact source/count/command gates precede RED and later implementation.

Healthy routing test-first WIP selection: existing hosted Save probe gains only
mode1 (same real owner, direct refusal, actual Save and normal Stop). Mode1
installs private external routing only after original start, merges actual
consumer stderr/menu into one isolated writer, restores both channels/env and
keeps all normal cleanup before outer interval/marker assertions. This scripted
menu does complete its authored ACK; it models the offending output interval,
not live controller ACK qualification. Five direct route cases preserve actual
failure Report and legacy/explicit stderr, while three malformed explicit modes
must suppress healthy output. Expected initial C#6 =4 featureFAIL/2PASS.
One separately selected actual-artifact mixed DATA method initially fails the
missing classifier callable; its downstream typed/malformed/foreign cases are
unexecuted until implementation. No production diagnostic/Save/parser changes,
no runtime yet. Exact source/count/command review required before bounded RED.

### Healthy marker causal RED accepted; minimal routing implementation WIP
Independent outcome `a863b910` verifies 58 artifacts: mixed DATA fails only
missing classifier (downstream unexecuted); actual C#6 gives four intended
failures and two legacy/explicit stderr passes. Real owned hosted Save commits
turn43, direct unadmitted Save refuses, protected bytes stay exact, original
stops normally. Its exact merged tail contains one authentic Active marker then
success. Failure Report assertions pass in every direct route case. No live
retry or mm27 mutation. This is not a new PTY qualification.

Minimal WIP routes the four healthy stages using already-known BasePath and the
existing explicit external sink's fresh checks. Both-unset preserves legacy
stderr; malformed explicit configuration never falls back. Failure Report/Write
remain unchanged. Shared external writer retains physical filename/serialization
and namespace semantics; marker filenames are disjoint. Offline typed mixed
classification rejects unknown/foreign/partial rows, with physical occurrence
triples still a separate required proof. SelectionPrompt stays strict physical-
only because its local isolated route does not open a hosted main operation.
No current GREEN or new package/live claim; exact source review precedes tests.

The new mixed artifact parser is currently test-wired only, not a live freezer
caller. Every subsequent live preparation is blocked until its outcome freezer
actually invokes this strict classifier on every event file, separately counts
both families and retains unchanged physical triple proofs. It must not ignore
unrecognised files. Historical frozen evidence is not rewritten. Identity DATA
uses exact JSON types, not Python numeric/boolean coercion.

### Healthy external marker routing bounded GREEN accepted
Independent outcome `2d0f4ac8` verifies all 70 artifacts. Exact mixed DATA1 and
official marker6 plus existing physical35 pass, no skips/timeouts/duplicates,
owned cleanup complete. Owning Integration build: 117.01 s, 190 warnings,
0 errors; source 32,073 and recorded outputs unchanged after run. Actual hosted
menu suffix is exact success plus LF, with no marker in merged output. Its one
external Active marker (PID531) joins the exact original and all nine physical
rows (three ordered complete triples). The real mixed classifier independently
validated every observed file. Real turn43 archive CRC and exact normal original
Stopped/OwnedScopeEmpty plus guardian ECHILD/0 verified. Failure stderr and legacy
unset/explicit stderr controls pass. No exhaustive event/timing-neutral claim.

This closes only the bounded routing regression. It is not actual live/PTy
qualification; actual live freezer wiring is still mandatory before any next
launch. No old world was touched, no Save parser/timeout/fence changed, and mm27
remains a nonpredecessor despite its previously committed archive. Current
producer must be refreshed from genuinely current owning outputs before new
preparation; recorded Unit copies are not assumed current after this build.


### Next finite block: actual mixed freezer integration, test-first
One affected DATA method uses the exact immutable accepted hosted mixed trace,
copied only into temporary private directories for malformed/foreign/orphan/
missing-stage controls. It first fails missing actual summarizer; downstream
controls are not yet executed. Physical-only emitter IDs remain observed-only,
not an OS ownership inventory. Critical marker PIDs must join complete physical
pin groups, with foreground PID independently bound from launch journal. Record
foreground Active+mutation/finalization, not attribution of a specific Save
without an independent operation interval. A narrow actual freezer entrypoint
will bind settled phase profile/probe/journal bytes and invoke the same helper
for every event file, preserving separate physical triplets and marker counts.
No old frozen outcome or world is modified. No runtime or new package yet.

### Actual offline mixed freezer DATA accepted
Independent `f9f58bf8` verifies both selected pure methods, seven artifacts,
32,074 source and eight output before/after joins. Every immutable accepted
mixed file is classified; temporary malformed/orphan/order/foreign-path controls
execute. Actual freezer entrypoint binds phase profile/probe/journal SHA and
settled launch exit, writes exclusive output, and keeps route success separate.
Helper marker timing must lie within its same-pin physical lease, followed by
ready; foreground PID comes from unique actual client launch journal. Other PIDs
remain observed-only, and non-helper Active-only rows are labelled as such.
This ordinary-participation freezer does not require scripted P01 to emit a
foreground marker. No new runtime, no old outcome rewrite, no exhaustive claims.

### mm28 current producer accepted, no world yet
One necessary Unit owning refresh completed 29.59 s (45 warnings, 0 errors),
restoring current Main/Bridge/Support copies while exact accepted Integration8
and producer723 byte/mode rows stayed unchanged. Independent owning `5799817a`.
Three standard no-build/no-restore guarded publishes then completed normally,
zero overlays; independent producer `5f7f8f78` / published `60a491be` verifies all
746 artifacts and exact239/244/249 full rows/modes. Current32,074 sources,723bins,
assets14/tools6/Unit8 close exactly. No tests repeated, no old world touched.
Next clean mm28 preparation remains separately pinned and gated; live semantic
and current-authority checks still required. Ordinary outcome must call the
actual strict mixed freezer; scripted P01 trace has its own scoped review.

### Clean mm28 preparation technically accepted
Independent `30ca2402` verifies all16 artifacts, eight inventories and unchanged
whole play/profile baseline; fresh setup41 only, validation0/not accepted. One
export+profile preparation7.4168s with normal original exporter EOF/ECHILD/0.
Exact launcher `09a53d5f` / profile `ff36be56`, private marker-only trace and
original budgets. No origin/queue/game launch at this checkpoint. Distinct GM
baseline and actual request/content/owner/reserve gates still apply before the
single scripted P01; ordinary43 and its Save remain separate later evidence.


### mm28 P01 clean runtime, offline local marker shape found
Independent frozen `e6d57552` verifies all8641 artifacts: actual scripted42,
consumer-return before one shutdown, original Stopped/OwnedScopeEmpty and normal
terminal/RPC/guardian closure. New offline classifier rejected four null-close
before/ready rows from two local helpers after successful shutdown; no game
failure or rerun. Source and exact physical intervals distinguish local no-remote
projection, not remote closure. Preserve rejected evidence unchanged.
A single explicit offline-only regression will classify whole local helper
sequences after pinned Stop UTC, with forged-null/pre-shutdown/incomplete/cross-
pin negatives. Accepted runtime/evidence/package stays byte-identical; no blanket
null acceptance or OS-lineage claim. Original P01 must only be reread, never run
again. Ordinary43 remains deferred until this bounded evidence review closes.

### Offline local shape GREEN; mm28 P01 predecessor accepted
Independent `27cf5d97` verifies three pure PASS and actual same frozen P01
reclassification; original `e6d57552` remains byte-identical (8641 artifacts).
Exact7,795-file trace inventory:7,653 physical/2,551 complete triples plus141
healthy markers, including two explicitly local post-shutdown helper triplets.
All local groups are null projection with real successful original Stop boundary,
correct physical acquisition/finalization order and no OS-lineage claim. Sealed
all-file union has no dropped/ignored entry; default runtime classifier remains
strict and unchanged. Actual P01 accepted42 + normal one shutdown is now a clean
predecessor. No runtime repeat, no physical world repair. Next ordinary43 profile
is separately bound; fresh GM decision is not preselected, Save/cleanup remain
unproved for that next live phase.
