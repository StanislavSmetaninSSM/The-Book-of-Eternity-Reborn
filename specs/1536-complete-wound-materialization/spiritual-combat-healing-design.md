# T095/T102/T103: counterable spiritual combat healing (DESIGN ACCEPTED)

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536

Current baseline: bounded free safe-cycle self and both current-Guardian aid
flows are accepted. This document selects the next missing combat behavior;
it is not an acceptance of implementation or a new prerequisite for those flows.
Bounded independent DESIGN ACCEPT: c3caf534c (Astra, 2026-10-28).
Implementation and runtime acceptance remain open.

## Player result and first scope

During an active direct spiritual duel in Chaos, the player chooses its own known
active I–IV spiritual wound for treatment through the existing guided action.
The action is a real counterable exchange. If it is admitted and not blocked by
the opponent, client-validated active healing changes wound/effects/History from
the existing authoritative d20 and art source. A blocked/countered paid attempt
spends its ordinary declared cost but cannot silently heal. One accepted exchange
spends OD once and publishes its exact wound result once; repair/replay cannot
reroll, repeat healing or repeat payment. NO safe world cycle or natural-recovery
tick runs during combat. Cancel/invalid selection produces no new healing attempt.

First scope is standard-art player→self, direct duel, conscious/capable actor and
visible active I–IV wound. Other healers/targets, unwilling aid semantics, champion/
assisted models, special-art healing variants, public/paid healer services and other
realms remain explicitly outside this initial slice; do not mark all US4 combat
or T095/T102/T103 complete from that subset. Resilience stays passive.

## Existing contract and concrete gap

- AfterlifeSpiritualConflictState.OperationTypes has no wound-healing operation;
  spiritual_healing is already a standard art. AfterlifeActionCostRules has no
  matching definition. Current cost validation looks up art tiers by operation
  string, requiring explicit operation→art mapping for a differently named action.
- Proposed operation name is heal_spiritual_wound, art spiritual_healing, base5,
  ordinary art reduction and final floor2. Existing registered afterlife_cost /
  exchangeId resource sources and exact before/after Spend remain the publisher;
  existing cost contributions/balances/rollback must compose, not be duplicated.
- SpiritualWoundSourceAdmission.PrepareExchange already validates and uniquely
  claims diceAudit.diceUsed.sourceIndex against signed AcceptedD20EventValues,
  retains the checked exchange and owns causal resource/effect work. It currently
  prepares harmful opportunities only when a side's strain increases. Healing
  needs a typed branch of that same admitted exchange, never negative severity
  or a fake harmful wound opportunity.
- The current draft insertion/ApplyPreparedWound/completion accepts create|worsen
  only. Extend that owned routing with a narrow healing variant using the SAME
  capture, effect draft, resource session, wound reducer/boundary effects and
  completion. Do not invoke safe Continuation/BuildCore as a second executor.
- SpiritualWoundRecoveryPlanner.Build always runs natural recovery after active
  except full heal. Factor/reuse its active-only stage construction; passing H0
  would still add1 and is forbidden for combat.

## Chosen shared route

Extend existing C2/C4 admission/draft/publication with an actual selected healing
source. A parallel combat publisher would lose resource/effect ordering and replay
ownership; reusing safe-cycle continuation would advance the wrong scheduler.
Cost/enum registration by itself has no player treatment result and is insufficient.

Use the actual selected wound identity/current fingerprint, player actor/art/
effect authority, conflict/exchange coordinates and the already admitted original
roll claim. GM cannot pick a replacement die or target after repair. The shared
SpiritualHealingResolver/calculation supplies bounded active improvement; common
publication must include every weaker GM-authored destination consequence graph,
exact cost, wound/effects, identity and History. No new registry/cache/receipt file.

### Real player selection and original correlation

The current guided /лечить hides treatment during activeConflict, and safe Stage
rolls a separate healing die. Neither is a combat entry. Extend the SAME shared
choice/confirmation/console one-shot/browser queue with an explicit combat-self
selection, mutually exclusive with safe self/aid: exact current conflictId, next
exchange ordinal, own woundId/semantic FP and generation/action binding. UI quotes
the actual OD cost and counter risk, not "free one world cycle". First source is
an actual active direct-duel player actor; unsupported actor/model/backlog refuses.

A lease-bound combat intent stage revalidates selected wound/art/effects/resource/
conflict facts and publishes only the closed client-owned intent before snapshot.
It has NO independent die, safe-cycle attempt or scheduler capability. Reuse the
existing wound command path and original browser retained-byte proof, with an
explicit combat family in actual manifest/request validators; never reinterpret
a safe request by field absence. Existing AttachPendingDiceAndGacha then provides
the ordinary original pool. The admitted C2 exchange claims selected sourceIndex/
roll mode against that signed pool and matches exactly one healing declaration to
the frozen selection/next exchange. Repair keeps that original and those claims.
Opposed margin determines interruption; it is not the healing check's margin.
SpiritualHealingResolver derives the active result from the same selected original
player roll(s), actual art/effects and wound difficulty; incompatible dice mode
refuses rather than silently selecting a convenient die.

### Scheduler-owned combat no-cycle path

No actual combat/no-cycle route exists in current BuildControlCore: ordinary Chaos
staging arms pending cycles≥1, and generic settlement rewrites LastUpdatedUtc even
for a zero control. Add a narrow lease-aware scheduler-owned BuildCombatControl
that reads actual active conflict and a valid canonical schedule, reusing the same
control calculation with zero expected/catchup and unchanged ordinals. Do not call
ordinary BuildControl first or fabricate a ProgressionControl in the caller.
For this first contour require an existing zero-pending/no-catchup basis; refuse
unsupported backlog without clearing or rebasing it.

Settlement authenticates the actual signed combat intent/control/original schedule
and creates an exact no-op contribution under the SAME accepted publisher, rather
than running generic timestamp serialization. Actual Engine assertions cover the
safe schedule/world counters, LastTickKey and absence of natural rows/results.
The zero-cycle contract applies only to authenticated current combat, never to a
caller-supplied all-zero control.
Accepted player turn/story and normal pending-dice rotation still occur; they are
not world-life cycles. Source read of FinalizePlayerTurn confirms ordinary turn
increment/story and Mortal-only progression call, not an independent Chaos cycle
counter; existing postaccept validation/cleanup must still complete. No additional
"entropy" mechanic is inferred from RNG draws or wording.

### Active-only draft, blocked overlap and durable outcome

Use the already admitted player roll(s), not a new safe healing roll. Operation
mapping is explicit heal_spiritual_wound→spiritual_healing; all old operation/art
mappings keep their current values, resilience is never an active operation.
The healing lane uses a declared healing-timing risk profile and ordinary counter
validation. Direct interruption includes the existing recovery-timing counter
operations and counter itself; successful healing cannot ignore such an admitted
counter. Opposed outcome and the independent healing calculation remain separate.
Special-art/NPC healing declarations are explicitly unsupported in this first
standard player-self slice, rather than silently becoming cost-only actions.

Extend the same capture/effect draft/resource routing/C4 completion with a typed
healing source/selection. For an unblocked admitted action derive the bounded
active-only shared program from SpiritualHealingResolver. The active prefix is
shared with safe Build; combat never calls the natural suffix, creates recover
rows or changes LastTickKey. The normal wound reducer/boundary-effect executor
handles each required stage and exact weaker graph; no second BuildCore/owner.
A check with no improvement still has an executed disposition; don't confuse it
with a missing source or unresolved decision.

For blocked/countered healing, execute ZERO wound stages and do not add a treat/
Care marker. The original selected wound/FP and roll remain the attempted source;
ordinary counter-pressure may independently worsen that same wound through the
existing harmful source/opportunity path. Its RetraumaWoundJson before-image is
unchanged by healing. Do not rebase it onto a healing marker or require the
blocked target to equal the later wounded afterimage. C4 requires both typed
outcomes and one ordinary declared OD spend in the same batch.

Exchange metadata alone is not durable: activeConflict/exchangeLog is removed on
resolve and recentConflicts is capped20. Extend the EXISTING private
`game_state/wounds/spiritual_wound_opportunity_receipts.json` ledger with a closed
combat-healing source/outcome family. It is not a positive/guaranteed harm source.
Keep injury source/decision bijection and its (instance,exchange,affectedSide) key
unchanged; healing has a distinct family-tagged identity so both may name the same
player/exchange without collision.

Proposed current closed ledger forms: existing schema1 continues to be produced
for injury-only ledgers; schema2 is emitted only with a mandatory nonempty combat
source family, adding `nextHealingSourceOrdinal`, `nextHealingDecisionOrdinal`,
`healingSources`, `healingDecisions`. A schema2 source must have exactly one outcome,
including blocked, and existing outcomes may never be dropped on later injury
append/closure. Both are current producer forms, not an old-save fallback. The
unchanged lifecycle subprojection stays its own current schema1. Unknown/empty/
cross-family shapes refuse; no raw/evidence/save migration.

Each healing source binds actual conflict instance, exchangeId/ordinal, actual
session/request/turn/token, exact original intent/fingerprint and target-before,
healer/art/effect source, signed selected dice indices/check and exact registered
resource source/operation. The outcome binds that source once, closed disposition resolved|blocked, full actual wound/effect/History stages for
resolved (including executed checks with no health improvement) and an EMPTY
healing-stage list for blocked, plus the
actual typed OD transition/result. Serialization is comparison DATA; only the
actual capture and current resource/effect owners issue completion/publication.

For resolved wound History, add a distinct closed `spiritual_combat_healing`
transition-result kind using the same active math/stage DATA and actual ledger
outcome linkage. Do not widen safe spiritual_healing result2/3/4 with optional
cycle/control fields or fabricate a schedule. Blocked has no wound History row;
its persistent ledger source/outcome and resource/conflict history are mandatory.
C3/C4 common reduction contributes the ledger afterimage with conflict/resource/
wounds, and completion Consume/SealOriginalSpiritualReduction validates exact
source/resource/effect owner tuple, full resolved chain or blocked-empty disposition
BEFORE C4 authority issuance. Settled reader crosschecks ledger, resource transition,
actual wound History/identity stages and original binding without repeating spend.

Causal requirement: blocked healing plus real worsening of the SAME selected wound
must complete with one OD spend, normal worsening/reset, zero healing stages and
unchanged intent/dice. Closing the conflict cannot lose the outcome. Removing only
its healing decision while preserving source/harm/OD rows must fail the required
bijection/completion/replay; absence is never successful implicit no-op.

## Finite acceptance and implementation boundary

1. Cheap T095 existing-API RED proves missing healing cost/art/matchup admission;
   base5, art tiers1/2/3/5 give ordinary costs4/3/2/2 before legitimate composed
   modifiers, with final floor2. Tier0 is diagnosis, not an active art; resilience
   must not become a declared active operation or OD source.
2. A real existing C2/C4 typed caller with a lawful initial player wound admits
   healing, reuses exact original roll/source, authors required weaker graphs and
   atomically publishes genuine active improvement + typed OD Spend + History.
   Countered/no-effect case proves no healing/natural tick and one ordinary spend.
3. Missing/foreign/stale wound/art/dice/counter/source, invalid graphs and duplicate
   attempts refuse or repair on the SAME original. Current exact resource source,
   wound/effects and owner guards remain; no read-only DATA substitutes permission.
4. Actual common publication fault rolls conflict/resource/wound/effect/History
   back together; replay is inert. Safe scheduler/LastTickKey/natural rows remain
   unchanged, not merely a zero fake natural result.
5. Extend the existing console/browser guided action and real Engine source with
   equivalent target/method/cost/confirmation/result semantics, safe Russian text
   and no typed technical IDs. Backend-only enum/helper completion is not the
   final player-result. GM CLI/Matrix/Wound/example/manifest guards move with code.

Tracked execution stays inline on the existing qualification branch: final bounded
design → causal test → coherent shared implementation/tests/GM sync → independent
SOURCE/PREP and dependency-based official category selection → fresh affected
owning builds/PlanOnly/actuals → independent OUTCOME. Existing holds are not new
combat prerequisites. Reuse operation-local source/cost/roll/effect DATA; retain
fresh actual lease/original/current closing checks. No speedup claim or new cache.

## Inline implementation checklist

- [ ] T095 existing-API cost singleton: unchanged pressure precondition, then
  missing heal_spiritual_wound definition RED; independent literal costs4/3/2/2
  for artI/II/III/V and passive resilience. The initial failure qualifies only
  the missing cost contract, not art mapping, spend or healing.
- [ ] Extend the actual operation/art/counter consumers and shared active-only
  wound program; add closed combat intent to the existing guided selection and
  original validators, without a safe attempt or extra draw.
- [ ] Extend the same admitted exchange/draft/resource/completion and existing
  receipt ledger with resolved/blocked healing, exact source/outcome bijection
  and the closed combat History result. Prove blocked+same-wound worsening and
  omission-only refusal after closure.
- [ ] Wire scheduler-owned no-cycle staging and authenticated exact no-op
  settlement; preserve existing safe routes and all source/effect/resource guards.
- [ ] Complete real console/browser C2/C4 journeys, graph repair, rollback and
  inert replay; synchronise GM contracts/worked example/manifest and focused guards.
- [ ] Freeze coherent source/tests/docs, independent SOURCE/PREP, fresh affected
  owning builds, literal official PlanOnly and bounded actuals, then OUTCOME.

The first RED uses one pure existing static API and allocates no session, files,
game fixture or cache. Unit refsfalse against current accepted Main/Support is
sufficient because this scaffold changes no production source. Category
spiritual-combat-healing-cost owns exactly that one Fact/60 seconds. Later owning
build/selection is chosen from the complete implementation's actual dependencies;
the current singleton is not an end-to-end completion claim.

## Healing-only C2 packet consumer clarification

The injury-only pending schema1 continues to require nonempty sources. Combat
uses closed pending schema2 with mandatory healingSources (exactly one in this
slice), healingDecisions and a separate cursor; injury arrays may be empty. The
actual initial checkpoint/pending pair is committed before wound mutation, at
WoundReadVersion0. Its opaque pending fingerprint is reconstructed as that same
schema2. Source validation derives original conflict/dice context from the
mandatory healing witness and uses one unique claim set. Generation and wave
count the union of eligible exchange ordinals, not the number of source families.

Healing participates in prefix fingerprints, append-only comparison, rebuilding,
transport and replay. completed_unpublished requires both cursors exhausted and
the actual owner-issued resolved/blocked outcome. C3 accepts schema2 only with the
same authenticated pair and current resource/effect/frontier proof, then appends
the existing companion ledger through C4. Blocked is also a paired outcome;
no_offer and the unchanged truly no-decision source-only route cannot hide it.
Causal controls cover healing-only empty injury arrays through pair→C3→C4,
omission-only healing outcome refusal with dice/spend intact, and one-wave
healing plus counterharm. This clarifies an existing consumer gap without changing
the accepted player scope or creating another executor, file or design gate.

The required next-resource-boundary control uses one healing exchange at original
ordinal0 followed by an existing ordinary exchange. The same checkpoint1 selected
submission carries a closed combat_healing child containing the exact owner-issued
outcome and its decision fingerprint; it does not pretend to be an injury command.
Cold replay reexecutes that selected decision before the existing bounded dependent
correction/resume. Repeated healing remains unsupported. Cross-family same-wave
equality applies only to the actual healing exchange; later injury sources retain
their ordered wave, full original evidence and unique dice claims. This extends the
same saved transport rather than using no_offer/source-only to skip a decision.


2026-10-28 actual-consumer clarification (no new healing authority): the signed
original observes the exact closed combat source union in the existing presence
dictionary. Ordinary producers retain their current default form. The same actual
staged combat projection selects capture paths and presence before signing;
reader allowlist knowledge never substitutes for a signed false observation.
Full Begin admission and both original byte/absence evidence remain mandatory.
