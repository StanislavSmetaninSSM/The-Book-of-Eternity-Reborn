# T095/T102/T103: counterable spiritual combat healing (DESIGN WIP)

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536

Current baseline: bounded free safe-cycle self and both current-Guardian aid
flows are accepted. This document selects the next missing combat behavior;
it is not an acceptance of implementation or a new prerequisite for those flows.
No combat-healing source has been changed or run yet.

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

## Chosen direction and bounded open questions

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
Accepted player turn/story and normal pending-dice rotation still occur; they are
not world-life cycles. Source read of FinalizePlayerTurn confirms ordinary turn
increment/story and Mortal-only progression call, not an independent Chaos cycle
counter; existing postaccept validation/cleanup must still complete. No additional
"entropy" mechanic is inferred from RNG draws or wording.

The only remaining bounded draft/result question is the durable blocked disposition
and its causal ordering if a legal counter also worsens the selected wound in the
same exchange. Current retrauma source requires exact retained wound JSON. Do not
silently rebase that source after a healing marker. The existing capture/current
owned-prefix APIs must define the ordering and result carrier before implementation;
no new receipt file, second executor or blanket no-op satisfaction is acceptable.

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
