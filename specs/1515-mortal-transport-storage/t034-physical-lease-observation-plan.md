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
