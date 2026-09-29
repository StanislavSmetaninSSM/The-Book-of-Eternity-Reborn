# Live Spiritual Wound Turn Boundary

**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
**Owning requirements**: US3, FR-008, FR-031..039; T076..T092.
**Status**: approved by the owner on 2026-09-22; the live producer is implemented.
The staged-frontier verification is in progress; the broader #1536 task remains open.
**Rules**: `afterlife-spiritual-wounds-and-healing.md` remains the authority for
formula, caps, GM choice, consequences, re-trauma and healing. This document
specifies their transport, staging and final publication, not new gameplay costs.

## 1. One logical turn and one final publication

A spiritual exchange and its wound decision finish within the same logical
player turn. If the initial GM draft cannot know the client-computed wound
envelope, the client requests an internal GM continuation before accepting that
turn. It does not wait for the player's next action.

The original session, request, turn, pending snapshot, dice pool, source-action
coordinates and progression control remain unchanged. A continuation does not
spend OD again, reroll the exchange, add an injury roll, advance an afterlife
cycle or rotate pending Fate. The first GM prose remains provisional. Only the
final accepted narrative enters visible history and produces notifications.

There is one common accepted-mechanics publication, containing the exchange,
resource/effect consequences, explicit wound decisions, optional wound changes,
receipt/seal evidence and bound narrative together. Do not publish a canonical
source in turn N and require a new ordinary request N+1 to obtain the wound.
Do not implement an outer transaction around two canonical subturn publications.

## 2. Pending evidence is not an accepted receipt

Use distinct spiritual storage rather than inserting spiritual rows into the
existing closed Mortal occurrence/receipt parser:

| Path | Lifetime and owner |
| --- | --- |
| `game_state/control/spiritual_wound_capture_checkpoint.json` | Client-owned immutable original draft, accepted continuation inputs and allocation replay journal for the last committed unfinished advancement; never accepted history |
| `game_state/control/pending_spiritual_wound_decisions.json` | Client-owned resumable continuation for the original pending turn; never accepted history |
| `game_state/wounds/spiritual_wound_opportunity_receipts.json` | Client-owned durable spiritual instance/closure/source/decision evidence, published only by the common accepted plan |

The existing `wound_commands.json` remains the single common client-owned wound
command staging root. The new pending root is not a second command writer or a
second wound carrier. Existing `pending_wound_resolutions.json` continues to own
construction/narration repair packets; it must reference the same original turn
and pending spiritual authority when repairing a spiritual decision.

The pending packet contains the original snapshot identity, client-generated
continuation generation, exact ordered source coordinates, immutable validated
source prefix, current decision cursor, previously staged exact decisions,
original dice claims, required source/resource/effect/actor before-images and
candidate after-images, and exact preserved draft content. Mutable JSON is copied;
an exposed object cannot mutate private authority. Public GM context is a separate
visibility-safe projection and cannot recreate private authority.

Before final acceptance, the retained original-turn capture under its active
canonical lease is the only writer allowed to create or replace this unfinished
packet. It may advance the generation/cursor, append newly staged exact decisions
and newly validated sources, and update their owner-validated derived candidate
images, prefix fingerprint and explicitly permitted dependent draft fields. The
original rollback baseline, original dice claims, prior decisions and frozen source
evidence remain immutable. This staging write is not accepted publication. Only the
final common plan may consume the pending root.

Every persisted after-image records its exact relative path, existence and
content fingerprint. Paths are from a closed registered set; a submitted path
cannot authorize arbitrary file writes. Initially absent roots remain explicitly
absent in the original rollback baseline. Never overwrite or reseal the original
pending-turn snapshot to turn a derived packet into pre-turn authority.

Cold restart reconstructs source and previously staged decisions against the
original snapshot and retained candidate evidence before resuming the cursor.
Matching caller-provided hashes are comparison data, not proof of valid origin.
Retain the existing local portable snapshot trust boundary; do not add an unrelated
secret-key service or claim that an ordinary hash authenticates the GM's payload.
No accepted source/decision receipt is emitted just because a packet was persisted.

The approved C2-R1 checkpoint is the durable origin for this derived pending packet. Its
closed schema and exact rows are in data-model.md section 23.2. It retains the complete original
distributed draft as exact bytes and absence, separately records the immutable original snapshot
manifest, authority and turn-request fingerprints, and retains ordered continuation input changes
and causal allocation/time rows. Mutable repair, ready and pending controls stay with their own
transport owner and are not frozen as original physical witnesses.
The initial accepted offer is committed with advancement zero; later successful bounded
continuations append one advancement each. Every stored packet fingerprint is a comparison with
a packet rederived by the production owners, never authority to skip those owners. A changed
original action, actor, die, independent sibling, source or earlier decision fails replay even
if a caller recomputes hashes. The checkpoint remains private to the client and is not a GM
repair or player command surface.

Cold replay reconstructs the last committed draft from original A and its saved input changes;
it does not require current physical GM files to equal initial A. A later physical change can
only be considered as a new uncommitted bounded input after recovery, never as an earlier
committed decision. Each rederived C1 packet is strictly validated and matched to its recorded
step fingerprint; an intact final physical pending is compared structurally with the final
rederived packet. Pending contents are never replay input.

Dependent conflict JSON and the saved scene accept the canonical text writer's
single leading UTF-8 preamble once at the byte-decoding boundary. Physical before-images and signed
fingerprints retain the exact bytes. A second preamble, malformed UTF-8 or duplicate
JSON keys remains invalid; this does not broaden the permitted correction fields.

For every unfinished advancement, the capture validates the complete next state in memory under
one canonical lease, atomically replaces and reads back the checkpoint first, then replaces and
reads back the derived C1 pending packet. An ambiguous checkpoint write is resolved only by an
exact old/new byte read-back; any third image blocks continuation. If the pending write fails
after checkpoint commit, no later decision is admitted until strict cold replay restores that
last committed pending projection without new ID/time issuance. A missing or altered checkpoint
with an active pending packet blocks continuation; pending cannot manufacture its own origin.
An old-image read-back discards all advanced speculative owners and restarts from the old
checkpoint. If the old image was absent or `checkpoint:null`, there is no replay origin; the
client preserves those exact bytes/absence and starts a fresh original capture under the
unchanged signed baseline. A new-image read-back with failed pending discards owners and restarts
from the new checkpoint. Journal speculation cannot reverse already advanced owner state.
Initially absent service paths and their signed original before-images remain distinct from
the active payloads. Only the common final publication consumes both service roots and owns
their read-back and rollback. Persisting or repairing a checkpoint has no accepted spend,
wound, receipt, notification or visible-history effect.

## 3. Source validation before offering a decision

The real ordinary-turn raw validation path owns source admission before the
common plan can publish. The conflict owner projection alone is not proof.
Reuse the production conflict validation core with explicit signed baseline and
candidate context; do not recursively invoke full state validation from planning
or read post-publication values as if they already existed.

For each admitted harmful transition, independently establish:

- Exact conflict instance, ordered exchange/source coordinate, acting actor,
  affected persistent actor and unambiguous side membership.
- Valid prior/destination strain and continuous exchange history.
- Original selected dice, operation/matchup/modifier evidence, signed harmful
  margin in the affected side's direction, and no duplicate die claim.
- Registered source operation and applied art; raw integer 0..5 player/entity
  tiers, including the target's Spiritual Resilience. A mirror is not player
  authority, and a normalized/clamped tier is not raw validation evidence.
- Prior danger declaration, any accepted escalation, registered source ceiling,
  and exact explicit old-wound action reference when re-trauma is requested.
  The source ceiling is neutral IV for ordinary actions and for special sources
  without a wound declaration (user confirmation, 2026-09-08). A stricter ceiling
  or guarantee requires the exact pre-materialized canonical source; absence
  supplies neither a guarantee nor a prohibition on ordinary eligibility. A current
  GM seed, DTO, frame, hash, or caller cap is not proof of a prior declaration.
- Successful resource/effect reductions for the exact source prefix. A failed
  subordinate stage cannot leave an offer, accepted receipt or resource spend.

The client then invokes the existing wound-opportunity calculation. Natural
critical display bands and `abs(margin)` are not substitutes for signed source
evidence. Arithmetic uses checked/wide intermediates; overflow is tested from a
genuinely valid source fixture, not inferred from an already-invalid example.

A validation-core extraction may be a separate prerequisite only when the
existing production validator actually consumes it. An unused calculator or
constructor does not close T081/T084/T085 or prove live ingress.

## 4. Ordered prefixes and dependent later exchanges

Do not assume that an ordinary GM draft contains only one new exchange. Existing
conflict replacement/history paths can carry an ordered batch. Freeze the source
prefix through the exchange that produced an offer, not downstream wound-dependent
results that have not been validated with that wound yet.

Resolve all offered sides of that exchange against the same completed exchange
evidence. Apply accepted decisions to the detached candidate state before
validating later exchanges. The same actor's wound effects must be available to
the later source/action checks that actually depend on them. This is candidate
reduction within one turn, not intermediate canonical publication.
Honor each registered effect's applicability and timing; this ordering does not
invent a universal immediate-effect rule for components with a different contract.

A continuation may change only the currently offered decisions, their required
wound prose, and explicitly identified dependent draft fields. It cannot change
the frozen source prefix, original action/actor/die coordinates, unrelated valid
siblings, prior decisions or their array order. Later arithmetic may need to be
recomputed using new wound contributions, but uses the same original dice claims;
it does not request a new die or repeat a resource spend.

The approved 2026-09-26 special-cost rule has bounded structural consequences:
an unchanged, unexecuted `force_incarnation` action may gain a previously absent
side audit when a newly installed applicable wound makes it costed. This includes
creating an absent `actionCostAudit` root containing only qualifying sides. The
new side has exactly `operationType`, `baseCost`, `minCost`, `artTier`,
`effectiveCost`, `before` and `after`; its operation is the original effective
`force_incarnation`, and its three cost/tier constants are zero. Existing sibling
audits retain their frozen evidence. Conversely, an existing prescribed force
side audit may be removed from an unchanged unexecuted action when the actual
next owner frontier proves zero applicable burden, for example after an earlier
Spend exhausts a finite-use wound effect. An empty root may be removed only when
it contained solely those qualifying sides; unrelated siblings remain exact.
A still-positive burden rejects removal as missing required evidence. A valid
but obsolete audit is dependent repair input, never permission to charge or
create a zero-cost audit. Null/scalar replacement, unrelated removal, extra
authority/special-art fields and changes to any original action/actor/die remain
forbidden. Shape eligibility is not authority: subsequent owner reconstruction
must prove the current actor's positive burden, exact amount and affordability
for addition/payment, or zero burden for removal/free evaluation.
Without that burden, the existing no-audit free path remains in force. Completed
exchanges never receive this exception; cold replay repeats the same checks.

The transport carries the exact permitted field set for each wave. Missing
decisions are unfinished input, not implicit `none`. An unsolicited, duplicate,
stale or mismatched opportunity reference fails closed. Changed frozen source is
a repair/conflict, never a silently regenerated opportunity. Failure to restore
valid authority aborts through the original turn rollback.

The number of source slots is fixed by the admitted draft and existing action
legality. Continuation cannot expand it to obtain extra actions. There is at most
one decision wave per eligible exchange, with the existing bounded repair policy
for invalid responses. Earlier exact accepted-in-candidate decisions are replayed
without consuming a second slot or producing a second outcome.

## 5. Durable instances, decisions and per-side policy

The spiritual receipt root has a closed versioned schema with conflict-instance
records and append-only source/decision rows. It stores no queue of canonically
accepted but undecided wounds. A final decision row includes its full independently
reconstructable source witness, original decision binding, decision fingerprint,
and optional wound/transition identities. `none` has neither wound nor transition.

An instance start row is immutable. Later terminal acceptance appends one immutable
closure row; it never edits or reseals the start row. A display conflict ID may bind a
new instance only after the prior instance has exactly one accepted closure row.

Conflict instance identity is not just a GM display `conflictId` or an index in
`recentConflicts`. A new instance binds its accepted start to client turn authority.
First admission of an already-active supported current-schema conflict binds its
exact signed baseline once; subsequent turns reuse that recorded instance. This
does not read obsolete save formats or repair invalid current profiles. A reused
display ID after a terminal instance cannot resurrect that instance's offers.

The conflict's `sideWoundState` is a checked projection of the durable instance
and decision rows, not a competing GM-authored authority. Reduce decisions in
source order. `none` consumes its exact opportunity, but not the side's new-wound
allowance. Create consumes that allowance. Later ordinary worsening targets only
that exact conflict wound. Explicit older-wound re-trauma retains its separately
proven target and does not relabel the older wound as this conflict's new wound.

Exact same-turn replay is a no-command/no-spend/no-notification result. A changed
decision under the same coordinate is a conflict. Old public references are not
permanent permission to apply a historical source in a newer turn. The durable
ledger survives the existing 20-entry recent-conflict pruning; pruning does not
reopen consumed opportunities or erase per-instance new-wound evidence.

## 6. Final composition, failure and restart

The common plan incorporates the spiritual source/decision stage even when no
wound transition is produced. An explicit decline must not disappear through a
"no wound work" early return. A source/instance-only stage is real common-plan
work, not a fabricated wound transition used to force a save.

Compose exactly one final conflict after-image with the existing resource owner
companion. Common publication owns all writes, read-back validation and consumed
pending files. No post-publication side-seal save or independent receipt writer
is allowed. Read-back checks source/decision/side/wound/effect/history agreement.

On cancellation, failed write or failed read-back, restore the original turn
baseline, including absence, and remove only staging belonging to this operation.
On restart during a continuation, retain that original baseline, reconstruct
the candidate prefix and resume the exact pending wave. On restart after a
completed publication, prove exact receipt/plan agreement before treating any
leftover pending file as consumed; its mere presence never replays the action.

`none` may appear in the accepted conflict audit but does not emit an acquisition
notification. Create/worsen uses existing acquisition narration binding and the
direct wound notification/detail route. No draft acquisition prose is displayed
as a completed turn before those bindings pass.

## 7. Delivery and verification gates

The first end-to-end decline scenario is an internal implementation milestone,
not a reduced release. A lawful materialize proposal must not be converted to
`none`, excluded as an unsupported mechanic, or sent into permanent repair.
Final US3 delivery includes lower/create/worsen/explicit re-trauma, all required
existing spiritual source families, persistent actor effects/current-side
contributions, danger/escalation visibility, bounded defeat and optional
dissipation. Healing, safe cycles and command UX remain their separate tasks.

Start with a production-valid signed source fixture: zero Error issues from the
owning raw validation and final conflict validation, real common publication and
read-back. Existing tests that filter selected error codes are not that proof.
Then mutate one authority element per regression. No hand-authored accepted
command, seal, carrier, receipt, history or after-image may replace the live producer.

Required integration controls cover no-wound and materialize, prefix-dependent
second exchange, simultaneous sides, cold restart before/after the decision,
unchanged dice/resource/progression identities, write/read-back failure,
initially absent roots, decline replay, changed source/decision, older re-trauma,
recent-history pruning and unchanged nonempty combat-condition siblings.

When these runtime surfaces are implemented, synchronize the afterlife matrix,
turn guide, rules/glossary, wound and output API guides, daemon/launcher dispatch,
worked afterlife examples, manifest and documentation/source guards in the same
change. Run the required focused documentation control and conditional
FullValidation at that implemented contract checkpoint. This design-only update
does not yet advertise a new GM command or require a runtime example claiming it
works. Mortal receipt schemas and Mortal producer timing remain unchanged.

## 8. Concrete production decomposition

The source/effect/wound dependency cannot be implemented by simply invoking the
old final validator earlier. The existing raw effect phase prepares wounds before
resource reduction; a new spiritual witness needs the causing resource/effect
result first. The production sequence is:

1. Capture the original signed baseline and detached candidate roots once.
   The ordinary state validator consumes the same extracted complete checker;
   raw source admission additionally requires strict signed input.
2. Check source structure and baseline integrity without exporting authority.
   Pending final currency/terminal linkage is explicit unfinished work, not an
   exemption from those checks.
3. Reduce the ordinary source prefix with existing resource/effect semantics.
   Preserve allocated resource/effect identities and the original operation
   transcript. A second nominally identical full-plan build is not replay proof:
   those planners may allocate fresh identities.
4. Finalize against actual applied/replayed resource transitions and the completed
   effect transcript, then compare exact candidate soul/Shining/conflict images.
   A pending effect/resource receipt wave is not a completed source prefix.
5. Export the private wound source witness, obtain the bounded decision, and
   continue from the retained candidate prefix. Final wound/effect/carrier assembly
   and one whole-turn equality check precede the sole retained publishable plan.

`AcceptedMechanicsPlanner.BuildAcceptedPlan` already contains the usable boundary
after resource reduction, effect finalization and registered outcome `Project`,
before final carrier assembly. Extract a typed intermediate consumed by that
existing planner as well as continuation. Do not register a temporary source-only
publishable plan in the cache while waiting for GM input.

The source gate proves exact conflict resource mutations in the ordered transcript;
it must not equate the last combat audit's `after` with the final ledger after
legitimate later triggers. Start-plus-harm and terminal exchanges need explicit
proof: the current ordinary conflict resource draft requires active conflicts on
both sides of the turn and therefore cannot be treated as complete proof for
those cases. A null draft is absence of that proof, not automatic source approval.
All these lawful source contours remain required by the live rollout.

The detached-checker prerequisite retains existing offline checks, one-use
historical payload behavior and exact reward/terminal rules. It does not silently
change balance, repair malformed tiers or fix hypothetical overflow. Source-specific
strict actor membership, raw art tiers, strain continuity and cross-exchange die
ownership remain explicitly required after extraction. Each independently reviewed
implementation unit needs its own complete-code execution plan and owning RED/GREEN
evidence; this architecture contract is not permission to mark those units complete.

## Chronological spiritual resource execution — owner decision 2026-09-16

Source: #1536 / T081-B2C-J2-C2-ORDER; owner chose chronological exchanges over
legacy whole-turn resource phases. Execute the original ordinary command and
initial lifecycle prefix once using its existing internal resource/effect order.
Then execute exchanges in accepted journal order. Finish both sides and all
causal descendants of an exchange before admitting the next exchange; unresolved
receipts/missing evidence retain the exact current executor and accepted prefix.
Do not move a later exchange's cost before an earlier exchange's recovery.
Within each prefix/exchange, existing phase, priority, dependency and causal rules
still apply. Original ordinary command files remain immutable after capture;
new commands cannot be injected retroactively into the completed prefix.

This deliberately differs from the fixed adapter's global sorting when that
sorting interleaves ordinary outcomes and exchange costs. Keep fixed-adapter
behavior unchanged. Cross-mode identity/semantic parity applies only when their
execution order agrees; live continuation/reconstruction must preserve exact
accepted chronological history regardless. Preserve same-owner IDs, arbiter,
ledger and accepted activations through every wait. Do not approximate this with
a global deferred-phase threshold.

Worked ordering examples: ordinary spend1/gain1 forms prefix6->5->6, followed by
exchange cost3 yielding3. In a separate two-exchange sequence, recovery3->5 must
precede the next cost3 yielding2. A rejected receipt changes neither prefix.
These are internal design examples, not a newly exposed GM command. Integrated
C2/D/E runtime enablement still requires the tracked GM prompts/matrix/worked
examples/manifests/source guards and conditional FullValidation in the same
cutover change; this capture alone cannot publish an accepted turn.

## C4 GM continuation transport — approved revision2, 2026-09-26 (#1536)

The exact public request/response field contract is the C4-GM-TRANSPORT section
of spec.md. The optional spiritualWoundContinuation envelope is carried by the
existing validation_repair_request/ready roots and by WorkerTaskPacket/
WorkerProposal. No new root, HTTP endpoint, player command or narrative key is
introduced. Both routes converge on the same C2 owner, not StateDistributor's
unbound raw-command route. Ordinary repair and worker contracts remain unchanged
when this envelope is absent. Empty errors is lawful only for a real continuation
request; a correct optional offer must not be represented as a validation error.

At phase decision the response has exactly one current-offer decision. At phase
dependent_draft it has zero decisions and only the issued path/JSON-pointer
corrections plus ordinary response/timestamp changes are allowed. Every pointer
is derived from actual C2 dependent-result validation, never selected by the GM.
Source actors, actions, dice, completed prefixes and independent siblings remain
immutable. Recheck exact session/request/snapshot/checkpoint/phase/offer and any
pending-submission binding both before worker apply and before ready publication.
Do not retain a canonical write lease across an external GM wait.

Approved DEPENDENT-FRONTIER-DECISION revision1 (2026-09-28) permits staged
dependent corrections using the same envelopes. A response/Ready completes
exactly the issued frontier A. Reconstruct A's full envelope from real private
owners and validate the whole candidate against A before deriving a successor B.
Only a complete prescribed audit, actual nonempty GM critical narration and
ordinary source validation may open later permissions. Future B edits in A are
rejected even when they would be legal after A. Distinguish rejected, fully
resolved and proven advanced results internally; strict resolved-draft validation
still requires no outstanding dependency. Exact A cleanup then fresh B dispatch
must retain pendingSubmission without resubmission or canonical publication.
Worker post-apply and pre-Ready checks use the same proof, preserving rollback,
reservation, original tuple, session generation and exact request/Ready witnesses.
Cold recovery reconciles outstanding issued transport before automatic Resume;
public files are comparison data, never authority. Old A is not a response to B.
Public JSON fields and hash formula remain unchanged; unsupported result
dependencies still block. Approved revision2 adds the private correction journal
below to make this boundary durable.
Worked GM contract: spiritual_wound_dependent_frontiers_v1.

Revision2 (2026-09-28) stores accepted correction images in the optional closed
pendingSubmission.dependentDraftProgress array. Its exact schema is in data-model.md.
The engine consumes matching A Ready, proves full A admission and the next supported
boundary, then appends under the existing canonical lease with exact old/new/
ambiguous checkpoint handling. Confirmed private commit durably activates B before
public B publication; ordinary advances, pending packet, saved decision, command
and selection allocations remain unchanged. Workers never append the journal.
Reopen genuine owners after commit; cold replay derives each row's permissions and
pre-row correlation independently and freezes its complete accepted image. The
current draft may modify only the first uncommitted frontier. Public B without
private progress, or changed accepted A text in a B draft, is rejected. Recognize
obsolete A after a commit-before-publication crash only through its reconstructed
old envelope and exact transport bytes; mismatching transport blocks unchanged.
Final normal advancement must retain cumulative accepted images before clearing
pendingSubmission/progress. This adds no partial ordinary execution or canonical
publication, and no cryptographic protection against coherent external rewriting
of all private history and hashes.

Before dispatching dependent_draft, persist the exact outstanding selection in
the existing private checkpoint's closed pendingSubmission extension. The last
committed advancement and its pending projection are unchanged. The extension
retains the exact composed command (including scene binding) or the automatic
guarantee_satisfied no-command proof, together with real allocation/time rows
through that selection and before the invalid later exchange. Cold replay must
rebuild real owners, replay that retained prefix and revalidate the selection;
the DTO, physical command or hash alone never proves it. Exact normal advance
and submission clearing share one checkpoint replacement; pending update follows
the existing checkpoint-first recovery rule. Full schema and comparison rules
are in the C4 pending-submission section of data-model.md.

Before ordinary raw admission, GameEngine captures the original signed intake
outside its cached-snapshot override, commits the first C2 pair, and runs the
current offer/dependent/completed loop. Only completed_unpublished enters C4-A
common publication. Live output follows the binding described below. The existing
atomic recovery contract restores the signed original turn or retains the complete
accepted turn; a working repair transport alone does not prove either outcome.

The C4 live-output implementation binds every real insertion in chronological
order to its stage-specific wound image and the final committed scene. Creating
and then worsening the same wound produces separate acquisition and worsening
messages; guarantee_satisfied contributes no insertion or extra notification.
The common transaction compares all retained output bytes before accepting the
binding even when no insertion produces a notification. The GameEngine handoff
rechecks them before take-once delivery and constructs the response from the
retained immutable texts, so later file changes cannot mix a new scene with
accepted notifications. Detached chronological cost comparisons remain scoped
through both final validation passes while the signed original snapshot exists;
the complete published exchange log, including its historical prefix, must match.
An output substitution during publication uses the existing signed rollback.
This is an internal completion of the established narration/notification rule:
GM-authored response shape, commands and fields are unchanged, and the existing
worked C4 materialize example remains the authoring contract. It does not provide
durable notification recovery across a crash after common publication.

The existing US7 atomic recovery contract has three deterministic integration probes:

- A mixed between-file publication cut is copied byte-for-byte under the warm
  publication lease to a fresh root. The original warm operation finishes separately;
  its compensation or success is not counted as cold recovery. Fresh-root startup
  and the late caller must restore the signed original or retain the complete accepted turn.
- After common publication but before story append, an interruption leaves the
  physical cut in place; a fresh FileSystemManager and GameEngine use that same root.
- After story append but before terminal cleanup, the same-root fresh-engine route
  retains the complete accepted turn and retires inactive snapshot evidence as below.

These probes verify coherent world/output/history/story and terminal cleanup, with
no duplicate wound, payment or story and no replacement GM decision. They are not
OS process-kill tests, do not require forced rollforward, and do not prove physical
exactly-once notification delivery. A source-only receipt or a diagnostic archive
cannot authorize acceptance, replay or rollback. GM waits for the current client
request and must not resend a saved choice. No command, field or durable authority
is introduced by this evidence record.

Inactive snapshot cleanup retains evidence separately from execution authority.
After a successful rejected late-turn rollback, only the input still matching
that original session/request/turn is removed; failed rollback preserves the
remaining snapshot evidence. After startup has no current input, terminal,
repair or spiritual continuation work, an unambiguous same-session old snapshot
may be copied to `diagnostics/inactive-pending-turn-snapshots/` and retired from
its active paths only after complete byte verification. The content-addressed
index and blobs are diagnostic copies; no validator, publisher or rollback path
uses them as authority. The helper never follows arbitrary manifest paths,
never sweeps unrelated rollback files, and does not change world, output or
story. Copy/readback failure and source drift retain active originals; partial
removal retains a verified complete diagnostic cohort. Foreign, future, active
or malformed metadata remains in place. This introduces no GM-authored field,
command or canonical gameplay schema, and proves neither seamless crash
rollforward nor physical exactly-once notification delivery.

A failed initial capture may return to ordinary raw resource diagnostics only
after a fresh private classification proves no_checkpoint, and only actual
ordinary errors can enter the established repair/fail-closed classifier.
Confirmed checkpoint advancement followed by a pending write failure instead
uses real C2 recovery. Persistent recoverable write failure retains the signed
turn and saved selection through the existing accepted-turn caller preservation
route; it cannot become terminal rollback or a second GM choice. A later cold
entry must authenticate and replay that same checkpoint before publication.
Preserve the exact correlated completion signal for normal cold lifecycle entry
and stop the current gameplay loop on this held disposition. Explicit re-entry
may attempt recovery; do not spin on the same persistent write failure or invent
a new terminal success signal. Treatment-publication retry behavior is unchanged.
