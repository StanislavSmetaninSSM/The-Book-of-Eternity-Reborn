# Independent Mortal narrative/state audit

Issues: #1515 and #1516. User selected an independent AI check on every Mortal
turn, including quiet turns and empty/omitted author reports, with full-turn
repair on disagreement and explicit latency/cost/semantic limits. Implementation
branch: `1515-1516-independent-mortal-audit`, base
`fa5d5ec8a20851f6b7401e5e7b27b455c9deb9d7`.

## Intent and alternatives

Actual engine probes accept a claimed new vehicle/storage without materializing
it (#1515), and destruction of a crossing omitted from a no-change report (#1516).
Validating only rows that the author chose to report cannot detect this omission.
A phrase/regex filter both misses paraphrases and rejects promises/negations.
Client-rendered mechanical statements do not constrain contradictory free prose.
The selected solution retains free narrative and adds an independent semantic
review before turn acceptance. It improves detection; it is not a proof of
arbitrary-language completeness or a second authority over game mechanics.

## Required protocol

1. The client captures immutable audit DATA after candidate reducers and normal
validation, before story append, turn advancement, command cleanup and releasing
rollback capability. The packet includes all player-facing narrative, exact
original and candidate canonical state, consumed-report evidence and relevant
rules, not only the author's list of claims. Source session/request/turn,
session generation and candidate attempt are part of a versioned digest.
2. A fresh reviewer context reads only that packet. It independently extracts
asserted mechanical events, separates promises, dreams, negation, speculation,
quoted/unverified speech and existing facts, and compares both directions:
prose claims against state, and material state changes against explanation.
3. The closed response has explicit pass/fail/uncertain; complete document-review
coverage; exact narrative quotes; and evidence references resolved only within
packet DATA. Unknown/duplicate fields, foreign packet, missing coverage, bad
quotes, unresolved pointers, failure/uncertainty or malformed output cannot pass.
A well-formed semantic mistake is still possible and is not advertised away.
4. A model result never authorizes mutation/publication. The engine reacquires
current ownership and checks the exact packet dependency identities/absence,
source and output freshness before acceptance. A changed packet needs new audit.
No live filesystem lease is held while waiting for the model.
5. Missing/contradictory claims return to existing bounded full-turn repair from
the authenticated original. The GM may fix text or submit legitimate state;
the audit does not invent entities, infer mechanics or silently repair files.
Timeout, unavailable reviewer, cancellation and malformed output hold the turn;
no fallback accepts it. Existing recovery and rollback remain authoritative.

## Transport and isolation

The existing text model route is Codex CLI through worker runners, not the image
OpenRouter API. Generic Analysis is optional, has an insufficient result schema,
and its default command bypasses sandboxing; it cannot be used unchanged as a
mandatory gate. Use a dedicated read-only adapter with an immutable packet,
structured-output schema, fresh ephemeral context and bounded cancellation.
Never use `--dangerously-bypass-approvals-and-sandbox`. Reviewer receives no
canonical write scope, repair/apply tools or credential values in its packet.
Do not broaden its runtime or network permissions automatically. Actual isolation
and launch failures must be verified before enabling this gate in production.

## Costs, performance and limits

One extra model call per candidate is expected; repair changes incur another.
Capture canonical DATA through existing authenticated owner/read-set surfaces.
Reuse only immutable packet data within the attempt; no new global cache or TTL
permission cache. Current owner/generation/source/closing fences are always fresh.
Do not silently omit oversized input; report unavailable/incomplete review.
Measure packet preparation, model latency and final recheck separately. Only
focused tests are run; no hosted CI or all-suite invocation. Live model tests
prove observed semantic behavior, while scripted tests prove protocol/lifecycle.
Windows and other providers require their own evidence. Spiritual turns excluded.

## Delivery gates

Protocol tests: false-empty/missing report must not skip dispatch; nonasserted
text is legitimate; malformed/duplicate/unknown response and stale packet fail;
evidence cannot escape packet or invent facts. Integration: both genuine engine
REDs, valid mechanical creation/destruction, quiet turn, repair/re-audit, stale
reply after changed text/state, unavailable/cancel, single story/turn acceptance
and full rollback. Live reviewer: original omission pairs and promise/dream/
negation/quoted-speech controls, including prompt injection in narrative.
Synchronize GM/daemon repair guidance and worked examples before activation.
