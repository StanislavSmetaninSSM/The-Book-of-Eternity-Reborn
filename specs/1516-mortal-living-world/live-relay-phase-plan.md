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
