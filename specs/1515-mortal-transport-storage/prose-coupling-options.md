# #1515 prose-only transport/storage: unresolved contract boundary

## Proven state

The original issue requires rejecting prose-only vehicles/storage. The existing
specification describes structured creation and explicitly forbids deriving
mechanics from prose, but does not define semantic extraction of an arbitrary
player-facing sentence. The accepted-turn audit now proves both a usable vehicle
and usable private storage can be claimed in narrative while no such canonical
entity exists. The positive treatment control is Accepted, so these failures are
causal, not rejected-fixture artifacts.

## Existing architecture

`ValidateAcceptedTurnProseStateDeltasAsync` uses bounded known-skill/quest name
and phrase matching. `npcSkillClaims` is an explicit private report; its empty
array is intentionally not an NLP completeness proof. `LoreConsistency` exists
as a proposal-only worker type, not a mandatory independent accepted-turn gate.
There is no existing semantic auditor that can simply be enabled for this case.

A new `transportStorageClaims` field is currently unknown to the closed debug
schema. Its empty array would fail the protocol shape, not semantic coupling.
Do not present that failure as evidence that a false empty report is detected.
If the field were admitted with only row validation, a false empty array would
be accepted by design; row validation cannot prove there are no omitted rows.

## Why a lexical patch is not a complete solution

These are design counterexamples, not claims of executed classifier tests:

- `Вам подарили повозку` versus `Вам обещали подарить повозку`: completed grant
  versus promise. An acquisition stem alone cannot distinguish them.
- `Вы получили повозку` versus `Вы получили не повозку, а ее рисунок`:
  completed grant versus explicit denial with a different acquired item.
- `Отныне возок ваш` asserts ownership without the acquisition verbs.
- `Купец сказал: «Повозка теперь ваша»` can be dialogue, deception, or a real
  transaction depending on context, not just punctuation.
- `Ключи от амбара лежат в вашей ладони` does not alone prove usable access;
  keys may be false, insufficient, or for a different door.
- `Вы получили повозку. Во сне.` reverses the first sentence across a boundary.

Adding a mandatory report plus an RU/EN phrase detector could reject a declared
and tested subset, including some false empty reports. It cannot establish the
unqualified original criterion for free narrative, and may reject valid flavor,
quotation, hypothetical, negated, or future text. No such detector is added here.

## Honest options

1. Keep free narrative and define a bounded explicit reporting contract, with
   current-turn entity/birth/access evidence. Test missing, empty, partial,
   stale and forged reports and document that omitted undeclared prose remains
   outside the guarantee. This narrows the current requirement and needs an
   explicit product decision; do not silently close the original criterion.
2. Add an independently invoked semantic audit before acceptance, using existing
   worker infrastructure only where it really supports an owned mandatory gate.
   Define provider failure, cost, latency, retries, evidence freshness and repair
   semantics first. A model audit improves semantic coverage but is not a proof
   of arbitrary-language correctness. This is new behavior, not a local toggle.
3. Make mechanical grant/access statements client-rendered from accepted state
   and keep them separate from unrestricted flavor. This gives a strong guarantee
   for those rendered statements, but unrestricted surrounding prose still needs
   a declared scope or auditing policy if its claims must also be enforced.

No new external model call, report protocol or lexical filter is implemented.
The canonical-vehicle-path regression fix is independent, small, and verified.
The prose-only criterion remains open; issue closure/merge is not part of this
checkpoint.
