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
