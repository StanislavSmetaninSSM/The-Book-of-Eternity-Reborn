# combatConditions Contract (#898)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/898

## Purpose

`combatConditions[]` records temporary tactical states in afterlife spiritual conflicts. A condition answers what is now easier, harder, more expensive, risky, protected, or vulnerable. It must map to existing afterlife combat axes and must not become generic stat stacking.

## Minimum condition object

```json
{
  "conditionId": "azalia_oath_flame_001",
  "name": "Разогретая клятва",
  "kind": "burden",
  "polarity": "debuff",
  "source": {
    "sourceType": "special_art",
    "actorType": "guardian",
    "actorId": "azalia",
    "artId": "flame_of_chosen_oath"
  },
  "target": {
    "side": "opposition",
    "actorType": "guardian",
    "actorId": "azalia",
    "displayName": "Азалия"
  },
  "affectedOperations": ["guard", "negotiate", "counter"],
  "mechanicalAxes": ["rollMode"],
  "payoff": {
    "effect": "disadvantage",
    "level": "ordinary",
    "sourceType": "combat_condition"
  },
  "duration": {
    "type": "next_matching_operation",
    "remainingUses": 1
  },
  "counterplay": [
    "break_binding against the oath context",
    "admit the true desire in negotiation",
    "let the condition expire by choosing an unaffected operation"
  ],
  "visibility": "player_visible",
  "summary": "Цель хуже защищается и спорит, пока действует из желания быть выбранной.",
  "auditRequirement": "When consumed, rollMode must include this condition as a source and the exchange must explain the fictional trigger."
}
```

Runtime accepts compatibility aliases where they match existing authoring patterns: `name` or `displayName`, `source.sourceType` or `source.type`, `target` object or `targetSide` plus optional actor fields, and `mechanicalAxes[]` or single `mechanicalAxis`. The preserved concepts are identity, source, target, kind, affected operation(s), legal mechanical axis/payoff, duration, counterplay, visibility, summary, and audit requirement.

## Required kinds

- `mark`: identifies a target, structure, oath, law, wound, mask, or trace that can later be exploited.
- `ward`: protects against one named consequence or operation type, usually by reducing/capping severity or imposing disadvantage on the attacker.
- `burden`: soft debuff that makes named operations harder, costlier, or riskier without forbidding them like hard `controlState`.
- `opening`: one-use opportunity/setup for the next eligible operation.
- `vow`: conditional oath/deal/delayed consequence that triggers when a promise, restriction, contradiction, or named behavior occurs.

## Legal mechanical axes

Conditions may reference or explain existing legal axes:

- `rollMode` advantage/disadvantage sources.
- `conflictPosition` movement.
- `controlState` softening/narrowing only through legal anti-control operations.
- `playerSideStrain` / `oppositionSideStrain` changes through legal operation payoffs.
- `tempoAdvantage`.
- `counterPayoff`.
- `actionCostAudit` / ОД costs.
- `specialArtAudit.effectNote` / `specialArtAudits[].effectNote` as audit explanation.

## Prohibited behavior

- No indefinite passive `+X` modifiers.
- No unlimited stacking.
- No bypassing `baseOperation` or the tactical matchup matrix.
- No duplicate representation of `controlState`; hard action restriction remains `controlState`.
- No hidden condition without source, target, duration, and counterplay.
- No Mortal-world HP/status vocabulary.
- No premature Saref/Wings spoilers in player-visible text.

## Lifecycle expectations

Active conditions must either remain actionable with remaining duration/uses and counterplay or be marked consumed/expired/cleared according to implementation conventions. A consumed/expired condition must not remain active without status update. If a condition affects a roll or action, the exchange/audit must reference that condition and explain why it applied.

## Backward compatibility

Absence of `combatConditions[]` remains valid for old saves/profiles and conflicts. Validation only enforces shape and lifecycle when the field is present.
