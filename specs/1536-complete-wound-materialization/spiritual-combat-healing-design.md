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
assisted models, special-art healing variants, tier-V wound services and other
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

The existing bounded independent reviewer is resolving these exact seams before
final design/implementation: selected operation die/counter relation; typed C2
healing response/draft insertion and C4 completion representation; closed durable
combat result/original binding without safe schedule/natural payload; and fresh
UI selection→original correlation. No whole-framework or DFR audit is requested.

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
