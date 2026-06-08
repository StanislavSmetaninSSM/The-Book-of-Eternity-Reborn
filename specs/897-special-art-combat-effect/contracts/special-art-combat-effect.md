# Contract: `specialArts[].combatEffect` (#897)

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/897

## Purpose

`specialArts[].combatEffect` is the ordinary afterlife-spiritual-combat value of a named special art. It complements, but does not replace, `effectSummary`:

- `effectSummary` remains player/GM-readable story and special-art summary, including Saref/storyline relevance where safe.
- `combatEffect` states why the art is useful in ordinary afterlife combat when compared with an upgraded standard art sharing the same `baseOperation`.

The field is GM-authored contract data. It constrains legal GM resolution and player display; it is not a deterministic per-art execution engine.

## Initial Shape

Each current-contract teachable/special afterlife art should expose:

```json
{
  "artId": "mirror_pressure",
  "name": "Зеркальное давление",
  "baseOperation": "pressure",
  "costMultiplierPercent": 150,
  "effectSummary": "Story-safe summary of the art.",
  "combatEffect": {
    "summary": "Short player-facing ordinary-combat niche.",
    "trigger": "When this extra effect can apply.",
    "mechanicalAxis": "rollMode",
    "allowedPayoff": "May grant a condition-backed advantage source when the trigger is met.",
    "limit": "Once per conflict until the named mark is cleared.",
    "auditRequirement": "specialArtAudit.effectNote must name the trigger and the exact rollMode/source or state delta used."
  }
}
```

Required `combatEffect` fields:

- `summary`: non-empty, meaningful, player-facing, not generic placeholder text.
- `trigger`: non-empty condition for when the effect can apply.
- `mechanicalAxis`: one legal afterlife axis.
- `allowedPayoff`: non-empty description of the legal payoff inside existing afterlife rules.
- `limit`: non-empty finite limit, cost, counterplay, or consumption rule.
- `auditRequirement`: non-empty instruction for what `specialArtAudit.effectNote` / `specialArtAudits[].effectNote` must record when the effect is used.

Allowed `mechanicalAxis` values unless implementation documents a tighter enum:

- `rollMode`
- `conflictPosition`
- `controlState`
- `sideStrain`
- `tempoAdvantage`
- `counterPayoff`
- `actionEconomy`
- `actionCostAudit`
- `combatCondition`

`combatCondition` means the effect creates, consumes, blocks, or modifies a #898 `combatConditions[]` entry through that contract's required fields and payoff rules. It does not create a second condition vocabulary.

## Validation Expectations

Validation should:

1. Accept old/legacy profiles that omit `combatEffect` only under the documented compatibility path.
2. Require `combatEffect` for current examples and newly-authored/current teachable arts where the chosen compatibility rule can identify them.
3. Reject empty/missing required fields.
4. Reject generic placeholder summaries such as "unique effect applies", "special effect", "combat bonus", or equivalent flavor-only text with no trigger/payoff/limit.
5. Reject unsupported `mechanicalAxis` values.
6. Reject `combatEffect` text that obviously bypasses `baseOperation`, ignores the tactical matrix, grants unlimited passive stacking, duplicates hard `controlState` without contest/counterplay, or uses Mortal HP/status vocabulary.
7. Preserve existing owner identity, `baseOperation`, `costMultiplierPercent`, `upgradeCost`, and training validation rules.

## Player-Facing Display Expectations

Default player-facing surfaces that show special arts, including `/spiritual_arts`, `/afterlife_profiles`, and browser-rendered shared command results when applicable, should show enough safe combat-effect text for upgrade decisions:

- summary / ordinary combat niche;
- trigger;
- allowed payoff or legal axis;
- limit/counterplay;
- optionally audit/use hint in player-safe wording.

Do not show raw JSON, `game_state/` paths, debug DTO names, hidden/GM-only fields, `gmThoughtsSummary`, unrevealed Saref/Wings spoilers, or private condition text.

## GM Prompt / Conflict Audit Expectations

GM-facing prompts/docs/examples must instruct the GM to:

1. Read `specialArts[].combatEffect` before resolving a named special art.
2. Preserve `baseOperation` as the primary operation lane.
3. Apply only legal existing surfaces: `rollMode.*.advantageSources/disadvantageSources`, `conflictPosition`, `controlState`, `playerSideStrain`, `oppositionSideStrain`, `tempoAdvantage`, `counterPayoff`, `actionCostAudit`, `actionEconomy`, or #898 `combatConditions[]`.
4. Record the application in `specialArtAudit.effectNote` or `specialArtAudits[].effectNote`.
5. Include cost multiplication through `costMultiplierPercent` / `actionCostAudit` when the named special art changes ОД cost.
6. Avoid premature Saref/Wings disclosure in player-visible text.

## Example Requirements

This feature must add or update worked examples with at least:

- one player-owned learned special art whose `combatEffect` gives non-Saref combat value over the base operation;
- one non-player Guardian/opposition special art with multiplied ОД and concrete `specialArtAudit.effectNote` tied to `combatEffect`;
- one mapping to `rollMode` advantage/disadvantage or `conflictPosition`;
- one mapping to `controlState`, `tempoAdvantage`, `sideStrain`, `counterPayoff`, `actionEconomy`, `actionCostAudit`, or `combatCondition`.

## Follow-Up Boundaries

#894 should use this final field shape to add or revise the ten Predvechnye Guardian dossier arts. #896 should finalize broad examples/regression coverage after #894. This feature may include minimal representative examples required by #897, but it should not rewrite the full Guardian dossier set.
