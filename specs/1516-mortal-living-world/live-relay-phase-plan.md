# Seeded-live Mortal relay qualification plan

Owner-directed follow-up for #1515 / #1516 / #1538, 2026-11-17. This is distinct
from the already verified scripted engine journeys and #1589 Chaos relay run.
No initial incarnation, NLP completeness, Windows, or whole-feature acceptance
is inferred. The original qualified live-game-one-turn.py stays unchanged.

## Initial setup only

Use the exact existing reviewed Mortal journey preparation and initial scout,
council, crossing threat/project builders, moved into a shared test-only helper.
Keep the existing integration fixture's behavior and script authoring separate.
No output callback, repair response, receipt, pending snapshot, active owner,
queue, or prior live-game state is exported. The exporter refuses a nonempty
base before any copy and refuses overwriting a manifest. It is never invoked on
later phases. Failed initial preparation remains visibly incomplete, not a
usable accepted world.

The source example has Mortal World/incarnation 2, clock480, no stories. Existing
engine tests supply turn41 in memory. Ordinary CLI Continue instead derives its
turn from maximum story turn. Add one explicitly labelled [TEST SETUP ONLY]
marker41 (no requestId), coherent fresh session identity and manifest with setup
story hash. This marker is not counted as an accepted GM turn. Resource41 and
actor40 setup metadata retain their existing meaning. Validate the full initial
world with the actual current ValidationService, then prove ordinary Continue
emits the correct fresh request. The scout has progression mode none; do not
pretend to prove XP advancement on it.

## One action per bounded phase

New sibling live-mortal-phase.py takes a fresh diagnostic/queue directory, the
seed manifest, a UTF-8 player-action file and optionally the previous phase
result. The original session and published runtime stay at the same base.
Each phase starts ordinary client/bridge/daemon and selects Continue. A live GM
reads the actual queue prompt and current context and authors the real response
through the maintained relay. The driver never supplies scripted GM output.

Require fresh session/request/turn/action correlation, original submission
witness, exactly one corresponding new Mortal story entry, clean pending/ready
state, real player prompt, one original shutdown, durable same-identity Stopped,
foreground EOF and cleanup. Existing 750-second guardian phase bounds remain;
no phase continuation after unknown failure. Before the next phase, require the
previous successful receipt, same seed/session/base, retained accepted story,
current maximum turn and exact original Stopped identity. Refresh only the
operational launch configuration for a new queue; never reset canonical state.

## Evidence sequence

Initial setup exporter: cheap official narrow test with full validation,
story/session coherence, absence of pending state, and byte-preserving refusal
of a second export. Explicit refusal stub is the causal RED, not a gameplay bug.
Then independent source review and GREEN before any native live attempt.
Python inert admission checks cover first phase, successful continuation and
failed/wrong-world/stale-turn refusal without game processes.

Live phases exercise scout/crossing, council/project, completion/public news,
and quiet interval in one retained world. Independent observation checks actual
materialized state and immutable earlier receipts after each accepted turn.
A contradictory package must refuse and repair the same request, not start a
replacement action. #1515/#1538 scenarios require their own concrete actions and
canonical evidence; the scout journey alone cannot qualify them.

Optimization: reuse one current baseline and immutable published runtime; retain
one world across phases. No cross-turn cache of authorization or validation
results. Current owner and request checks stay fresh. Shared test-only builders
avoid duplicating or drifting expensive preparation. No measured speedup claim.

## Exporter causal RED and source correction

Coordinator reports fresh integration build 0 errors / 175 warnings in123.23s;
official initial exporter case completed1/1 FAIL in10.9355s at the explicit
not-implemented refusal, with complete owned cleanup. This is helper RED, not a
production defect. Implementation now shares the existing initial preparation
and actor builders with the old scripted tests; their author callbacks and
runtime session binding remain unchanged. The exporter copies only example
canonical game_state, not example output/saves/stories, then creates the explicit
setup marker and fresh chat session. Composers recompute resource owner authority
and effective storage access before validation, not during an accepted turn.

The narrow category now includes two additional independent refusal controls:
occupied base with missing manifest, and fresh base with occupied manifest.
Both require sentinel bytes unchanged and no partial copy. Positive assertions
cover actual setup-line hash, the complete story inventory/max41 and absence of
runtime main ownership, queue, input/output and ready artifacts. Fresh GREEN and
independent review are pending; no live result is claimed.

### Initial lore inventory correction

First implementation official exporter run: 3 executed, 2 PASS / 1 FAIL,
18.276s, complete owned cleanup. The real full validator refused exactly six
missing required lore files: codex_entries plus current_world world_setting,
geography, history, cultures and threats. Restricting initial copy to game_state
had omitted this non-runtime input. This is preparation failure, not gameplay
or validator failure. The correction copies only those six exact files from the
same reviewed example. No afterlife lore, draft example, output, saves or whole
snapshot is added. The positive exporter test independently checks the complete
six-file lore inventory and exact source bytes before full validation. Existing
integration default preparation is unchanged. Fresh verification is pending;
other planned categories were not executed in the failed run.

### Canonical UTF-8 raw setup witness correction

Next official run: 3 executed, 2 PASS / 1 FAIL, 14.3568s, complete owned cleanup.
Full initial validation passed. The exporter then incorrectly compared canonical
writer bytes with no-preamble UTF-8 bytes and reported a changed story.
FileSystemManager's writer explicitly emits a UTF-8 preamble. The exporter now
requires exactly that initial encoding, captures actual bytes before validation,
and requires byte-identical bytes afterwards. The setup-entry fingerprint covers
the actual raw first line including its single BOM, excluding only LF, matching
the live driver's raw-line contract. Decoded setup turn/session semantics remain
separate. The test checks exact BOM/LF and hashes the actual raw line. No state or
history integrity check was weakened; fresh verification remains pending.

### Preparation verification accepted, 2026-11-17

Final exporter build completed with 0 errors / 154 warnings in 67.51 seconds.
Official mortal-live-seed-export completed 3/3 PASS in 16.1158136 seconds:
one fully validated initial export and two independent byte-preserving refusal
cases (occupied base or manifest). One descriptor, no duplicates, skips or
timeout; runner-owned cleanup complete. Independent review read the actual TRX.

The affected existing consumers separately completed 7/7 PASS in 77.5598992
seconds: living-world journey 2, NPC progression journey 2, console rendering 2,
mixed mechanics labels 1. Four descriptors, complete owned cleanup and no skips,
duplicates or timeout. Independent review verified that the subsequent commit
8662939 changed only exporter and its tests; these seven do not call exporter,
so their successful evidence is retained without repeating unchanged expensive
journeys. Together these are ten bounded checks, not one aggregate run.

Runtime publication and a fresh one-time seed export may now proceed. No Mortal
live GM turn has run yet. Initial turn41 is setup-only. The independent semantic
omission audit remains unfinished and old relay run e remains logically
uncertain; neither is cleared by this preparation. Raw logs/TRX remain outside
Git. Next: immutable runtime provenance, actual first relay request and GM-authored
scout/crossing response, independent accepted-state and real shutdown readback.

### First Mortal startup refusal, not a gameplay run

Fresh immutable publication contains 481 files; Client/Bridge no-build publish
succeeded, six DLL/PDB output joins and native source/binary provenance matched.
The fresh exporter produced a fully validated Mortal seed with zero issues.
First phase ended after 25.363 seconds with `TimeoutError: original Running and
published transport`: the durable main owner remained Prepared, transport never
became ready, and no player action or GM response was sent (0 accepted turns).
The sole guardian finished driverExitCode 1, ECHILD true, emergencySignals 0,
failures 0 and deadline false. This establishes its physical descendant drain,
not normal logical stop: no original Running identity, shutdown attempts 0,
foreground EOF/termios and logical lifecycle unconfirmed. No continuation or
retry uses this world. Root-cause investigation is read-only before another
controlled attempt; the first failed world/diagnostics are preserved. This does
not invalidate the ten preparation checks or qualify any live Mortal scenario.

### Fresh short-path phase: real turn and observer mismatch

The new isolated short phase passed startup (native endpoint 88 bytes), ordinary
Continue and actual request 061733a37a4843b98b1a795d512b7fee / turn42. An independent
GM authored the four-hour scout wait from the actual prompt, not a canned fixture.
Actual repairs corrected npc versus npcActivity effect kind, scheduler cycle count,
and player-facing output freshness. Four relay consumer executions exited zero.
The canonical story gained turn42 and the living-world history gained an exact
session/request/turn receipt for480..720; scout activity advanced0..240 of480.

The driver incorrectly expected requestId in a Mortal story row; this story route
does not write that property. It therefore timed out and reports Success=false,
AcceptedGameTurns=0. These raw findings are not rewritten. At686.101s its one
original shutdown completed with LogicalLifecycleVerified=true; guardian reported
ECHILD, driverExitCode1, emergencySignals0, failures0 and deadlinefalse. Final
independent full outcome review and typed receipt verification are still pending.

Before the GM response, an explicit baseline captured state/stories/lore. All283
capture hashes match, raw snapshot manifest and authority match actual relay
Witnesses, the authority payload digest and93 snapshot file hashes match, and
its original manifestPayloadHash matches the newly published receipt token.
This is retained evidence, not invented retrospective capture. A bounded read-only
typed receipt/original-snapshot check and a corrected assessed-Mortal observer
will preserve request correlation without requiring a nonexistent story field.
No action is replayed and no raw driver result is promoted to PASS. GM-facing
semantic-kind guidance is a separate confirmed defect to repair before later turns.

Independent outcome review confirms one applied turn, but not a clean phase:
client exit/EOF/termios were not established (No observable phase progress45s);
bridge and daemon exited0 with EOF/termios, original owner Stopped. Guardian
physical drain does not substitute for client normal exit. No continuation is
admitted from this failed result. The actual partial scout inspection is not
completion, public news, XP, or whole-issue qualification.

### Detached live evidence checker: causal RED and implementation awaiting GREEN

The narrow `mortal-live-evidence-data` category completed 9 cases: 7 PASS and
2 expected refusal-stub FAIL, official runner 9.9625758 seconds with complete
cleanup. Owning build completed in 39.57 seconds, 0 errors / 66 warnings.
The two positives cover production receipt/manifest correlation and explicit
captured-file reads without mutation. Negative cases cover fingerprint/payload,
request/action/duplicate-property mismatch and extra/missing append.

The test-support-only implementation now uses the existing strict JSON parser,
production snapshot payload hash and production revision-2 receipt validator.
It requires exactly one appended receipt, an unchanged prior history prefix and
exact original request/snapshot correlation. The CLI emits detached DATA only,
never publication authority; missing current receipt is refusal. UTF8 inputs are
read once each, with at most one leading UTF8 BOM. No runtime owner, lease,
FileSystemManager, source mutation or retained cache is introduced. Fresh reads
are required for this one-shot observation; stable parsed DATA stays local to
one call. No speedup is claimed. Raw file authentication, current ownership,
actual domain effects and accepted story/GM output are separate driver checks.
Implementation is unbuilt/unrun pending independent review and official GREEN;
this does not reclassify the earlier failed live phase or authorize its replay.

Observer correction is restricted to assessed Mortal phases with a mandatory
living-world receipt. CLI now requires an immutable evidence-helper DLL after the
action-file argument, before optional prior-result. It captures one pre-action
state baseline, then original pending manifest/authority/snapshot at the actual
request. First relay Witnesses must match those raw records. Story matching uses
actual Mortal turn/player/realm plus immutable prefix and one append, rejects a
conflicting optional requestId, and separately binds exact session/request/turn
through the production-typed receipt checker and hash-bound final GM narrative.
Missing or mismatched receipt refuses. Earlier accepted living-history bytes
must remain exact at continuation. Failure and cleanup admission are unchanged;
no previous failed result can become a continuation token. The checker is invoked
once after acceptance candidate, never in the polling loop. Original absent
requestId was causal inert RED; corrected story/receipt checks pass with eight
negative tuple/fingerprint cases plus raw-snapshot tamper refusal. Actual live
verification of this revised observer is still UNRUN.

Independent review found that the CLI catch did not include InvalidDataException.
A real RunAsync test with tampered receipt confirmed causal RED: 10 executed,
9 PASS / 1 FAIL, 10.9145601 seconds, complete official cleanup. Console capture
is isolated in the existing nonparallel console collection and restored in finally.
The boundary now explicitly catches InvalidDataException and emits the flat
exit-2 refusal. This correction is frozen awaiting combined official GREEN.

### Corrected evidence/guidance verification

Final unit build: 0 errors / 25 warnings, 9.58 seconds. Official two-descriptor
run passed16/16 in13.0172528s (typed evidence10, authored guidance6), no skips,
duplicates or timeout, complete cleanup. Subsequent fresh integration build:
0 errors /129 warnings,64.38s. Actual daemon delivery plus example manifest
inventory passed2/2 in15.0048176s, complete cleanup and no skips/duplicates/timeout.
The detached CLI then checked the retained ml2 baseline/current/request in0.315s,
exit0, valid=true/publicationAuthority=false, exact original identity/token and
canonical declaration/receipt fingerprints, prior0/current1. This is a read-only
verification of the applied turn, not permission to continue its failed phase.

Independent review corrected two observation boundaries: observed current player
prompt is now cleanup DATA before fallible evidence verification, while accepted
flags remain after it; the helper time is capped by the original work deadline
(maximum30s) so cleanup retains its reservation. The immutable helper's SHA is
recorded before and checked around invocation; its adjacent production DLL and
deps/runtimeconfig must also be pinned by the separate preparation manifest.
Cheap inert checks cover the budget edges, cleanup source ordering, conflicting
optional story identity, typed tuple/fingerprint negatives and raw snapshot tamper.
No production state/owner/guard was relaxed and no new retained cache was added.
Fresh live verification of the corrected observer remains outstanding.

### ml3 interrupted observation: WIP, 2026-11-18

Source checkpoint 9f78941b2cd83374be610fd2d5c3899aa3e5dc3a was used for a fresh
isolated seed and one actual four-hour scout-wait submission. This is not a
continuation of failed ml2. Preparation pinned 481 runtime files, 248 evidence
helper files and eight relay files; no source/runtime build changed during the
attempt. The new session is seeded_live_mortal_391d601491e141f89b6a0ab113a33b67,
request 1a708f66a9fc48ed8b57781188feae1c, turn42. The GM read its real prompt and
published one independently authored response, with no inferred calendar hour,
no scout completion and no XP claim.

After execution-session recovery, the original command handle was unavailable.
There is no final probe result and the guardian output is empty. Last retained
client bytes are at monotonic77.251s; last completed status RPC at78.114s, followed
by an attempted status at78.136s without a result. This does not establish why
or exactly when observation/execution stopped. Current command-local process
visibility cannot prove that the original processes exited.

Independent read-only review verified original request and snapshot/authority
against queue Witnesses and the single published reply hash. No consumer start,
executing-response or execution receipt is present. No narrative output, ready
signal, story42 or living-world receipt is present. Canonical clock and NPC bytes
still equal the captured baseline; the story remains the seed41 marker with its
original manifest hash. The original request/snapshot remain pending. Durable
owner state remains Running without StopEvidence, which is neither liveness nor
cleanup proof. No shutdown attempt is in the retained journal. Do not replay this
action, reuse this world, promote the result or delete its evidence.

This checkpoint changes documentation only. Existing 16 unit and two delivery
checks are not rerun. Stable prepared runtime is retained; no new cache or owner
reuse is introduced. Independent review has assessed the recorded artifacts;
actual downstream execution, physical/logical closure and full live qualification
remain unverified. First establish the original run's state using supported
bounded status-only diagnostics; do not start another game run on uncertainty.

One supported status-only diagnostic subsequently used the immutable client DLL
and existing helper with a 10-second RPC bound. It completed in0.595s, helper
exit0 with stdout/stderr EOF, returning ok=false: Bridge FIFO openat failed
(errno6), before status dispatch. This proves that this endpoint could not be
opened for writing with a current reader; it does not prove process death or
cleanup. No game start, stop, ownership recovery or second submission occurred.
The original guardian/result channel remains unavailable. The safe next step is
to restore that original observation channel or obtain explicit orchestration
termination evidence; no fabricated Stopped record, deletion of locks, forced
pending cleanup or replay is permitted. The cause remains unknown, not a proven
product defect or repaired infrastructure failure.

### Separate initial simulation admission

The coordinator subsequently authorized checking a wholly separate new world,
without contacting or modifying ml3. Independent source review permits this:
main locks are base-local; bridge/native endpoints use distinct temporary root,
run and pipe identities; relay is exact session/queue-local; guardian binds only
its own checked descendant lineage. No old result becomes a continuation token.
A fresh exporter creates a different session, no pending state or main authority,
with a new phase/queue/report and no previous-result argument. Copies must not
hardlink mutable files, and no ml3 GM response is reused. In addition to the
existing driver's four XDG home paths and TMPDIR, launch sets fresh HOME,
DOTNET_CLI_HOME and mode0700 XDG_RUNTIME_DIR, with a fresh diagnostic nonce and no
old debug output paths. Physical CPU/RAM/disk remain shared; this is logical
isolation, not a claim of separate hardware. Immutable verified binaries can be
reused without another build. This avoids redundant preparation computation while
retaining fresh seed, authority and request checks. Any later success or cleanup
belongs solely to this new run; ml3 remains unknown and preserved untouched.

### ml4 first actual Mortal turn and closure, 2026-11-18

A wholly fresh ml4 world used the unchanged9f78941 source/runtime, with the
reviewed isolation above. Preparation manifest SHA256 is
2da80c50383ffd2680b234e4840e512613d7d753f79a729c93f4c12229dab681.
Ordinary Continue produced request83e992518ee2432c83db658b2fde94af/turn42 in
session seeded_live_mortal_42b89938600e4f488e7dce151320716d. The independent GM read
that actual prompt and authored a new response. One relay execution completed
exit0/child exited/I-O drained, without a repair round or copying an old packet.

The driver reports one genuine action and one accepted turn, Success=true,
235.565 seconds. It verified the exact seed-story prefix plus one Mortal story,
original snapshot Witnesses, matching final GM narrative and a production-typed
revision2 receipt (prior0/current1, publicationAuthority=false). Receipt SHA256
is28045b763e305c3da07d4a0b5197d610321f7a5f0cec5435ad82e12364b2fd9c.
Canonical time advanced480..720 and the scout activity progressed0..240 of480
minutes, step0..1 of2. This is partial progress, not activity completion, XP,
public news, #1515 materialization or whole-issue acceptance.

Exactly one original shutdown returned success; original run
3ce0e6ee76664c5e9f41ae1027d0f41f has durable same-identity Stopped/OwnedScopeEmpty.
Client, bridge and daemon all exited0 with full EOF and restored termios; relay
execution and RPC helper were closed. Guardian independently returned ECHILD,
driverExitCode0, emergencySignals0, failures0, deadlinefalse. These statements
belong only to ml4; ml2 remains failed and ml3/e closure remains unknown.
Independent outcome review confirmed raw request/snapshot/packet/story/receipt and
all original closure evidence; the next retained-world phase is admitted on ml4
only, using this exact result and the unchanged seed manifest.
No implementation changed or broad test suite ran; immutable runtime reuse
avoided recompilation while the new original authority/seed/effects stayed fresh.

### ml4 p02 startup refusal before action

A separate p02 phase attempted retained-world continuation after the verified
p01 result. The runtime/helper/relay hashes and exact prior seed/request/story/
history/Stopped record were checked first. No exporter or world reset was used.
The daemon refused startup: an existing status file named pid111 from the prior
p01 daemon and Assert-SingleDaemonInstance reported it already running. The
phase ended17.596s with zero genuine actions and zero accepted turns. No GM
response was authored or submitted. Its new owner17ebeb3f27d44417bb71191e15bf240e
was shut down once with durable same-identity Stopped/OwnedScopeEmpty; guardian
reported ECHILD, driverExitCode1, no emergency signals, failures or deadline.
Client exit-2 and daemon exit1 make this a failed phase despite bounded physical
and logical owner closure; it is not a continuation token.

Source investigation is pending: the daemon singleton path uses a numeric PID
liveness predicate, whereas the prior daemon's exit is already proven. PID reuse
or namespace mismatch is a hypothesis, not yet an independently reproduced root
cause. Do not delete the status/lock or relax admission to retry. Establish the
specific source identity failure with a cheap test before changing code. The
accepted p01 turn remains separate evidence; p02 did not run the proposed next
game action. No broad suite or extra game run has been performed.

### Guarded ml5 first live turn, 2026-11-18

The ml4 startup defect was causally reproduced and corrected under #1589; see
bridge-fifo-transport-plan.md for the independently accepted 112 narrow results,
failed intermediate probes and exact O_NONBLOCK correction. Fresh ml5 uses the
published c2f40beb checkpoint (production91513a7), not the intentionally stale
local HEAD. Its immutable preparation manifest SHA256 is
c7d42644e55d14bc0a32d38b9875a77645806c494f0007cc92db5157d580357f;
483 runtime files,249 helper files,4 native and8 relay files were verified.
No ml3/ml4 state or pending response was reused or changed.

Actual request6063bf9cf3d648d68cea968dda28804c/turn42 belongs to fresh session
seeded_live_mortal_0b704a2f0a1947e99ab21ced597a2e05. The independent GM authored
seven actual response writes; one relay execution exited0 and drained without
repair. Ordinary Continue accepted exactly one action/story after the unchanged
setup41 prefix. The production-typed receipt has publicationAuthority=false,
prior0/current1 and fingerprint
sha256:b359684f44f00d98c08663bc5e5831f3fc46f9e5f39bf0624823fa4023d800fa.
Time480..720 and scout Active0..240/480, step0..1 remain partial progress, not
completion, XP, storage/transport materialization or whole-issue qualification.

The original phase succeeded in267.721s. One original shutdown ACK matches run
f905ec3f8dcd4fee966392e0c0f8d2cb/epoch1 and durable Stopped/OwnedScopeEmpty.
Client, bridge and daemon exited0 with EOF and restored termios; RPC helper
exited0 with both EOF. Guardian confirmed ECHILD, driver0, no emergency signals,
failures or deadline. Daemon errorCount0 and terminalSettled=true match retained
guard ownerb31d156dbbe9483a972374d986384fa2/settled=true. Independent outcome review
verified request/snapshot/response/story/receipt and all original closure evidence.
A second retained-world phase is now admitted using this exact p01 result, the
same seed and immutable runtime, but a fresh phase/queue/nonce and GM response.
This does not resolve the separate unknown ml3/e histories or qualify Windows.

### ml5 p02 client preparation refusal, no GM action

Retained-world p02 used fresh phase/queue/nonce and independently admitted p01
evidence. Preparation manifest SHA256 is
668d160a1ce6cbfa036ef398c6a54b0459475eee4b82b8f91fb5e9f75d111b96.
The corrected Linux daemon actually restarted and later stopped cleanly with
zero turns/errors and an exact guarded settled pair. Ordinary client Continue
instead reported twice: Explicit worker admission is required before canonical
recovery. This occurred before any actual request or GM response. The original
phase is Success=false, zero actions/accepted turns, 30.434s. Story [41,42] and
the sole receipt42/history remain byte-identical to p01; pending/ready are absent.

One original shutdown ACK matches new run6810ecac4ac6422694ae8b514be86dcb/epoch2
and durable Stopped/OwnedScopeEmpty. Bridge and daemon exited0 with EOF/termios.
Client exit, EOF and restored termios were not observed. Guardian recorded
ECHILD, driver1, reaped2, zero emergencies/failures and no deadline; physical
descendant drain is not normal client exit. Independent outcome review confirms
these boundaries. No continuation uses this failed result and no world/status
or diagnostics are manually repaired. The accepted p01 remains separate.

Read-only source tracing locates this exact exception at a disposed local worker
root context. A prior local menu admission followed by a separately owned remote
main is a candidate lifecycle explanation, not yet a causal test or fixed bug.
Next is a cheap existing-fixture reproduction and independent admission/lifetime
design review, without weakening ownership checks or replaying the game action.
