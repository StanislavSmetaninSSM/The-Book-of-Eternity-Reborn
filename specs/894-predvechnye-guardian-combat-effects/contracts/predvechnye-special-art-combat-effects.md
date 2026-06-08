# Contract/Design Map: Predvechnye special-art combat effects (#894)

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/894

This file maps the #894 dossier-content pass to the existing #897 `specialArts[].combatEffect` and #898 `combatConditions[]` contracts. It is not a new runtime schema.

## Shared authoring rule

Each built-in Predvechnye Guardian `dossier.md` special-art paragraph should keep the existing layers:

1. `Особое духовное искусство:` art name and base operation.
2. `Художественный эффект:` story/Saref-safe narrative identity.
3. GM note requirement saying what must be recorded when the art is used.
4. New `Боевой эффект:` ordinary afterlife-combat niche.

The `Боевой эффект:` clause must be directly translatable to #897 fields:

- `summary`: ordinary combat niche, not generic flavor.
- `trigger`: when the extra niche can apply.
- `mechanicalAxis`: legal #897/#898 axis.
- `allowedPayoff`: the permitted outcome inside existing afterlife combat rules.
- `limit`: finite cost/counterplay/once-per-condition/contest boundary.
- `auditRequirement`: what the GM must name in `specialArtAudit.effectNote`.

## Per-Guardian effect map

| Guardian | Art | Base operation | Ordinary combat niche | Preferred legal axis/payoff | Required limit/counterplay |
| --- | --- | --- | --- | --- | --- |
| Azalia | Пламя Избранной Клятвы | binding | Exploits a target's voluntary allegiance, false devotion, or self-chosen promise instead of raw restraint. | `combatCondition` mark/burden or `rollMode` advantage against a named promise. | Works only when the GM can name a real promise/desire; honest refusal or self-sufficiency clears/blocks it. |
| Brann | Клеймо Честной Трещины | pressure | Exposes a structural defect in a defense, order, vow, or formation so pressure weakens that exact support. | `conflictPosition`, `rollMode`, or `combatCondition` opening. | Must name the defect; improvisation or repaired structure can close the opening. |
| Elyara | Милость Незаживающей Раны | guard | Reduces, delays, or reroutes one severe consequence while preserving a real spiritual price. | `sideStrain`, `counterPayoff`, or ward-like `combatCondition`. | Cannot erase the price; the remaining scar/debt/condition must be audited. |
| Ilarion | Якорь Невытравленного Имени | guard | Anchors one truth, name, memory, or witness against erasure/substitution. | `combatCondition` ward, `rollMode` disadvantage to erasure, or `controlState` boundary protection. | Protects one named fact only; breaking it requires explicit spiritual effort/counterproof. |
| Lissara | След, Которого Не Было | maneuver | Makes the enemy spend tempo on a false trace, wrong position, or already-abandoned angle. | `tempoAdvantage`, `conflictPosition`, or `rollMode` disadvantage against a named pursuit. | Fails if the false trace is not plausible; repeated use teaches the enemy the pattern. |
| Lucian | Лунный Разрез Клятвы | break_binding | Cuts one oath/seal/false-light layer and reveals the hidden payload without deleting all consequences. | `combatCondition` clearing/reveal, `counterPayoff`, or `actionCostAudit` against a named seal. | One layer per use; what is revealed can still be dangerous or costly. |
| Myriel | Пепельная Формула Чужого Мира | pressure | Punishes imported metaphysical rules or alien-law incompatibility that does not fit the current realm. | `rollMode`, `sideStrain`, or `combatCondition` burden tied to the foreign rule. | GM must name the alien law; local adaptation or accepted translation can reduce the pressure. |
| Seret | Разомкнутый Договор | break_binding | Reveals a legal exit clause and turns hidden binding conditions into contestable terms. | `counterPayoff`, `combatCondition` reveal/clear, or `actionEconomy` for a lawful exit action. | Does not waive the price; hidden terms must become visible and contestable rather than vanish. |
| Varak | Трещина в Строю | pressure | Splits formation discipline by returning agency to one combat node or subordinate actor. | `conflictPosition`, `tempoAdvantage`, or `combatCondition` opening on a formation node. | Requires a node with suppressed agency; loyal consent or renewed command can close the split. |
| Veyra | Маска Среди Крыльев | maneuver | Creates a temporary role/access vector that passes a first check but carries contradiction risk. | `actionEconomy`, `conflictPosition`, or `rollMode` advantage for one access/misdirection move. | One scene/checkpoint role; contradiction, direct scrutiny, or overuse can expose it. |

## Non-closing references

#896 remains the follow-up for worked examples and broad regression/docs coverage after these dossier clauses land. PR text for #894 must not use GitHub closing keywords near #896.
