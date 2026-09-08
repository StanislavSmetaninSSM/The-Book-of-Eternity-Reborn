# Live Spiritual Wound Turn Boundary

**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
**Owning requirements**: US3, FR-008, FR-031..039; T076..T092.
**Status**: implementation design, not a statement that the live producer exists.
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
| `game_state/control/pending_spiritual_wound_decisions.json` | Client-owned resumable continuation for the original pending turn; never accepted history |
| `game_state/wounds/spiritual_wound_opportunity_receipts.json` | Client-owned durable spiritual instance/source/decision evidence, published only by the common accepted plan |

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
