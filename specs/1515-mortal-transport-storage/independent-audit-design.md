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

## Independent design review and resolved constraints (2026-11-17)

Astra High independent review accepts T1 DATA work but blocks activation pending
an explicit publisher route map and complete inventory. There is no universal
post-validation hook: ordinary validation already advances progression; joint
publication has separate prepared/completed/resume paths; final story delivery
rereads outputs and can run another repair. Every final publisher must consume a
bound audited candidate, not simply observe a Boolean earlier in its control flow.

Joint candidates must be reviewed against validated projected afterimages before
irreversible joint publication. A committed or resumed joint owner with missing
or inconsistent proof is held using its existing recovery contract; it must not
fall through to destructive ordinary full-turn repair. The earlier repair rule
applies only where the existing owner legally permits restoration/resubmission.
T3 first maps ordinary/late/browser/joint/resume paths and preserves their owners.

Packet coverage has two levels. T1 validates complete review of the supplied
packet only. T3 must prove the packet's versioned inventory covers the actual
turn: all rendered prose fields, authenticated baseline bytes and absences,
settled candidate namespace, pre-consumption report bytes, and rule/schema
revisions. A model listing every supplied document does NOT prove capture is
complete. Newly inserted files and changed/deleted output invalidate capture.

State-origin findings may report an observable unexplained change with state
references and no invented quote. Hidden/off-screen changes allowed by game
rules may remain silent: auditing must not force spoiler disclosure or prose
for every mechanical housekeeping field. The reviewer distinguishes allowed
silent changes from effects the player is told happened or would perceive.
Positive controls include both legitimate silence and explicit negation.

Transport investigation found read-only Codex sandbox is not read isolation and
`--ignore-user-config` does not prove absence of inherited tools/MCP. Therefore
T2 must either qualify a genuine restricted/no-tool transport or use a text-only
model API with no tool definitions and an explicitly configured provider. Do not
borrow an image API credential/configuration or call generic worker execution
"isolated". Transport qualification is unresolved, not an implemented property.

## Concrete T3 owner/route map (read-only design, not implementation)

Ordinary active console/browser: `GameEngine.TurnLifecycle.cs` calls accepted
validation near2074, rereads presentation near2115, runs post-accepted repair
near2123, then advances/appends near2147–2161. Late turns use the corresponding
1041/1063/1071/1084–1095 path; generic/transition turns use141/171/176/188–203.
All require a final fresh audit binding after any late repair, and publication
of that bound immutable presentation rather than another filesystem read.
Determine Mortal eligibility from authenticated original-turn realm; the separate
Chaos Sea life-evaluation request near2985–3006 is not a Mortal turn.

Ordinary candidate DATA source: existing
`AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync` in
`CanonicalStateNormalizer.MortalItems.cs:1788–2034`. Export immutable audit
contributions through its Result after settled validation, before living-world
report consumption. Its normalization beforeimages are NOT pre-turn originals.
`MortalLivingWorldPublication` already retains original documents/absence,
reportBefore and finalImages: reuse detached exports, not its lease-bound source.
Invoke the model only after this lease closes. Split success cleanup/progression
out of `ValidateCurrentGameStateOrShowErrorsAsync`; timeout/unavailable needs an
explicit held disposition, not TerminalRejected (whose callers delete artifacts).

Original capture uses the existing validated pending snapshot context and
`PendingTurnSnapshotReader.ReadCurrent(files, lease, selection)`, authenticating
required bytes/absences. Its current64-path selection limit cannot silently
truncate a full audit. Extend existing observed optional-path families where
needed. Namespace membership and new files join the final fresh comparison.
A `ReservedValidationReadSet` may reuse DATA only inside its eligible short lease;
close it before model await and use fresh owners afterward, including browser
fallback. Never retain it as publication permission.

Presentation: `BuildGameResponseFromFilesCoreAsync` normalizes narrative and
visible dialogue, reads combat/condition/interface prose, consumes wound
notification handoff, and can fall back to runtime narrative. Extract a pure
projection over captured inputs and reuse it for final GameResponse; do not call
the existing stateful builder speculatively for audit. Include permitted silent
world changes without forcing spoilers into prose.

Joint publication: `PublishCurrentMortalJointRepairAsync` builds preparation,
reservation, prepared/effect/final plans, frozen progression, commonPlan and
preflight under one lease, then takes/publishes. Its attempt/preflight cannot
cross model await. Investigate an exact detached full projection of baseline plus
all afterimages/deletions, OutputBasis narrative/interface and progression/report
before/after; close before take using `CloseCurrentJointAttempt` /
`CloseOwnedJointAttemptUnattempted`, require ClosedNoNativeAttempt, then audit.
Rebuild under a fresh owner and compare exact DATA/dependencies before take;
actual readback must equal audited projection. Deterministic identity after
unattempted close/rebuild is a REQUIRED UNPROVEN prerequisite, not a guarantee.

Cold/completed joint resumes enter `ValidateMortalJointPostPublicationAsync` and
must audit through actual completed-proof readers before fresh final progression
lease. Final joint story completion also needs its own bound-output fence.
Disagreement holds committed evidence; no ordinary rollback/repair bypass.
These decisions preserve the existing owners and avoid a competing transaction
framework, but still require route-specific runtime proofs before activation.
