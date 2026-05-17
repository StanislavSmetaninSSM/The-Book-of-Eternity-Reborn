# Afterlife Combat Terminology Glossary

This glossary fixes the Russian player/GM labels for afterlife combat terms. Canonical JSON field names, enum values, command tags, and validation ids stay in English; UI and GM-facing prose should show the Russian label first and keep the canonical term nearby when it matters for authoring JSON.

| Canonical term | Russian label | Use in play |
|---|---|---|
| afterlife spiritual conflict | духовный конфликт посмертия | The whole afterlife combat-like contest system in `Chaos Sea` or ordinary active `Shining Abode`. |
| afterlife spiritual action | духовное действие посмертия | A player action inside an already active conflict; `/spiritual_action` only adds an explicit routing tag. |
| afterlife spiritual combat log | журнал духовного боя | Read-only player-facing log of active `exchangeLog[]` and resolved `recentConflicts[]`; shown through `/spiritual_combat_log`. |
| Spiritual Arts | духовные искусства | Client-owned afterlife combat upgrades stored in `soul_state.afterlifeCombatProfile.artTiers`. |
| exchange | обмен действиями | One resolved beat inside an active conflict; written through `afterlifeSpiritualConflictUpdate.mode=exchange`. |
| resolve | завершение конфликта | Terminal closure of an active conflict; written through `mode=resolve` and moved into `recentConflicts[]`. |
| repair_cancel | repair-отмена / ремонтная отмена | Non-reward cleanup of malformed or impossible conflict state. |
| diceAudit | аудит кубиков | Required visible-dice proof for contested exchanges and contested terminal resolutions. |
| rewardAudit | аудит награды | Required proof when a resolved victorious conflict grants Ink Feathers or Light Sparks. |
| criticalResult | аудит критического исхода | Required normalization proof when natural 20/1 changes the margin-derived `outcomeBand`. |
| counterPayoff | выигрыш контрприёма | Required measurable payoff for a `counter` with success/partial_success/countered: either `counterPayoff`, improved `conflictPosition`, or worsened `oppositionSideStrain`. |
| matchupAudit | аудит сопоставления действий | Required on new/current contested exchanges with `diceAudit`; records the player's operation, opposition operation, resolution lane, risk profile, and rationale for the tactical matchup. |
| controlState | состояние контроля / оков | Canonical control axis for afterlife spiritual combat. It is separate from strain and position: `level`, `controllerSide`, `controlId`, `sourceOperation`, `restrictedOperations`, and `summary` record who controls whom and what actions are restricted. Missing/null means no active control for legacy entries. `sourceOperation=binding|force_binding|force_incarnation|break_binding|incarnation_resistance|counter|guard|repair`; it is not a free operation id. |
| controlState.level | уровень контроля | `none` / нет контроля, `hindered` / стеснён, `bound` / скован, `locked` / запечатан. |
| operationType | тип операции | Mechanical type of an exchange/resolution, such as `pressure`, `guard`, or `force_incarnation`. |
| outcome | исход | Mechanical outcome of an exchange, such as `success`, `blocked`, `countered`, or `no_effect`. |
| side strain | напряжение стороны | `playerSideStrain` / `oppositionSideStrain`; tracks pressure on each side, not hit points. |
| conflictPosition | позиция конфликта | Current advantage state from `opposition_dominant` to `player_dominant`. |
| direct_duel | прямой поединок | Player side lead acts directly against opposition lead. |
| assisted_duel | поединок с поддержкой | Player is still lead, but supporters contribute minor bonuses/fictional leverage. |
| champion_duel | поединок чемпиона | A stronger ally/champion can be the lead contestant while the player supports. |
| forced incarnation | принудительное воплощение | Coercive Guardian attempt to send the soul into a life; not the same as voluntary `TriggerIncarnation`. |
| guardian_forced | принудительное Хранителем | Canonical trigger source for forced incarnation; requires strict conflict/provocation proof. |
| retainedRadianceRank | сохранённый ранг Сияния | Radiance rank retained after returning from Shining Abode to Chaos Sea. |
| enlightenmentRank | ранг Просветления | Chaos Sea progression rank used for afterlife conflict authority and art gates. |
| radianceRank | ранг Сияния | Shining Abode progression rank used for afterlife conflict authority and art gates. |
| art tier | уровень искусства | Upgrade tier of a spiritual art; ranks cap the maximum tier that can be purchased. |
| Source of Light | Источник Света | Full-Radiance Shining capstone scene closed through `pending_source_of_light_capstone.json`. |
| light_incarnate | Воплощение Света | Soul-owned capstone passive that adds explicit player-side dice modifiers in afterlife spiritual conflicts. |
| Incarnated Light | Воплощенный Свет | One-per-soul Soul Relic `source_of_light_incarnated_light`; in Mortal lives it gives characteristic bonuses through Soul Relic effects. |

## Spiritual Art Names

| Art id | Russian label | Mechanical meaning |
|---|---|---|
| `pressure` | Давление | Direct spiritual pressure against the opposing lead contestant. |
| `counter` | Контрприём | Countering or reversing an incoming operation. |
| `guard` | Защита | Preventing incoming strain or consequence against the player's side. |
| `maneuver` | Манёвр | Shifting `conflictPosition` without raw overpowering. |
| `break_binding` | Разрыв оков | Resisting or breaking spiritual bindings and forced handoffs. |
| `binding` | Наложение оков | Imposing a bounded spiritual bind after winning leverage. |
| `incarnation_resistance` | Сопротивление воплощению | Resisting `guardian_forced` incarnation attempts. |
| `champion_coordination` | Координация чемпиона | Improving side-vs-side support when an ally is the lead contestant. |

## Player Commands

| Command | Russian alias | Use |
|---|---|---|
| `/spiritual_conflict` | `/духовный_конфликт` | Shows the active afterlife spiritual conflict, sides, `conflictPosition`, side strain, exchange count, and full JSON audit. |
| `/spiritual_combat_log` | `/журнал_духовного_боя` | Shows the afterlife combat log: active `exchangeLog[]`, resolved `recentConflicts[]`, dice, position/strain deltas, rewards, and full JSON audit. |
| `/spiritual_combat_help` | `/духовный_бой` | Shows the player-facing combat guide: commands, tactics, Spiritual Arts, position, dice, bounded criticals, rewards, and upgrades. |
| `/spiritual_action` | `/духовное_действие` | Sends one explicit tagged action inside an active conflict. Ordinary roleplay prose is still valid when it clearly acts inside the active conflict. |
| `/spiritual_arts` | `/духовные_искусства` | Shows ranks, art tiers, upgrade costs, and performs client-owned Spiritual Art upgrades. |

## Spiritual Art Operation Rules

These are mechanical rules, not flavor synonyms. If a player writes prose, classify it into one primary operation and keep the state delta inside that operation's allowed lane.

| Art / operation | Valid use | May change | Must not do | Example |
|---|---|---|---|---|
| `pressure` / Давление | Directly challenge the opposing lead contestant. | Mainly `oppositionSideStrain`; optionally terminal resolve after a later valid close. | Do not treat it as a free `conflictPosition` maneuver or binding. | "I press on the Guardian's broken oath" can move opposition strain `clear -> strained`. |
| `guard` / Защита | Prevent or reduce incoming strain/consequence against the player side; even a setback guard against direct `pressure` mitigates so `playerSideStrain` worsens by at most one rank. | `playerSideStrain`, blocked consequence, defensive `incomingAction` audit. | Do not damage opposition strain directly; use `counter` for reversal. | "I shield the soul-fracture" can keep player strain from worsening, or turn a crushing pressure into only `clear -> strained` on setback. |
| `counter` / Контрприём | React to a concrete incoming operation. | `incomingAction`, blocked/countered audit, and a measured payoff on success/partial_success/countered: `counterPayoff`, better `conflictPosition`, worse `oppositionSideStrain`, or reversed/weakened existing opposition `controlState`. | Cannot be used without `incomingAction`; cannot be a standalone `pressure`; cannot create fresh player control from none; successful/partial_success/countered counters cannot only heal `playerSideStrain`. | "As he binds me, I turn the thread back" must name the incoming bind/pressure and show the reversal. |
| `maneuver` / Манёвр | Shift advantage without raw overpowering. | `conflictPosition`. | Successful maneuver must not directly change `playerSideStrain` or `oppositionSideStrain`, and cannot bypass active opposition `controlState` without first weakening/removing that control. | `contested -> player_advantaged` without strain damage. |
| `binding` / `force_binding` / Наложение оков | Control after leverage. Requires `player_advantaged`, `player_dominant`, setup, or `decisive_player_success`; `force_binding` requires strong leverage and a broader payoff. | `controlState`: create or strengthen player control only when active opposition control is absent; `force_binding` must restrict at least two distinct operations. | Cannot be spammed from neutral `contested` on ordinary success; cannot answer active opposition control; cannot be recorded as strain or position only; failed binding/force_binding outcomes (`blocked`, `countered`, `setback`) leave `controlState` unchanged on both sides, including player-control rewrites and opposition anti-control deltas. | After gaining advantage, the soul seals the opponent's route: `controlState.level none -> hindered`; force binding might restrict both `maneuver` and `binding`. |
| `break_binding` / Разрыв оков | Answer an existing binding, forced handoff, or coercive lock. | `controlState`: weaken, remove, or reverse opposition control; legacy forced handoff state if present. | Not a generic attack or defense against ordinary pressure; success must change the control/coercion state. Same-level narrowing of opposition `restrictedOperations` counts as weakened `controlState`; equal/reordered sets do not count. | Break a name-seal: `controlState.level bound -> hindered` or `bound -> none`. |
| `incarnation_resistance` / Сопротивление воплощению | Resist `force_incarnation` / `guardian_forced`. | forced-incarnation proof state, resistance audit, possibly forced-incarnation `controlState`. | Not a replacement for `guard` against ordinary pressure or `break_binding` against ordinary binding control; failed incarnation_resistance outcomes leave forced-incarnation `controlState` unchanged. | Resist a Guardian trying to throw the soul into a life. |
| `champion_coordination` / Координация чемпиона | Support a `champion_duel` where an ally is lead contestant. | champion-side support modifier, `conflictPosition`, side support audit. | Cannot be used in `direct_duel` as if the player were lead. | The soul guides an allied Guardian's strike while staying supporter. |
| `recover_spiritual_power` / Собрать Средоточие | Spend the turn gathering spiritual focus and restoring ОД. | `actionEconomy.player.current` through `actionCostAudit`. | Not an attack, not a defense against direct pressure, not a way to ignore incoming control. | Recovering against guard/passive can restore +3 ОД; recovering into pressure/control is punished and restores only 0..1 ОД. |

## Action Economy / ОД

ОД are afterlife-only spiritual action points. They are not Mortal HP, stamina, energy, or combat resources.

- Active conflicts carry `actionEconomy.player` and `actionEconomy.opposition` with `current`, `max`, and `source`.
- Every new/current exchange that spends or restores ОД must carry `actionCostAudit.player`: `operationType`, `baseCost`, `minCost`, `artTier`, `effectiveCost`, `before`, and `after`.
- Formula: `effectiveCost = max(minCost, baseCost - artTier)`.
- Base/min costs: `pressure 3/1`, `guard 2/1`, `counter 4/2`, `maneuver 3/1`, `binding 4/2`, `force_binding 5/2`, `break_binding 3/1`, `incarnation_resistance 3/1`, `champion_coordination 2/1`, `recover_spiritual_power 0/0`.
- `recover_spiritual_power` restores ОД up to `actionEconomy.player.max`: success +3, partial_success +2, punished recovery +0..1.
- Punished recovery happens against `pressure`, `maneuver`, `binding`, `force_binding`, or `force_incarnation`; strong timing is against `guard`, `counter`, `none`, or `passive`.
- `withdraw`, `surrender`, and `negotiate` remain legal at 0 ОД unless a later contract explicitly changes terminal choice rules.

## Tactical Matchup Matrix

Every new/current contested exchange with `diceAudit` must also include `matchupAudit`. This is the "rock-paper-scissors" layer: the GM still narrates freely, but the state delta must follow one primary mechanical lane.

`matchupAudit` required fields:
- `playerOperation`: the player's primary operation, matching `exchange.operationType`.
- `oppositionOperation`: the opposition's primary answer, incoming operation, or `none`/`passive`; if `incomingAction` is present, this must match `incomingAction.operationType` or `incomingAction.finalOperationType`.
- `primaryResolutionLane`: the lane that decides the exchange; for ordinary player-led exchanges it matches `operationType`.
- `riskProfile`: one of `offensive_pressure`, `safe_defense`, `risky_reversal`, `position_play`, `control_leverage`, `anti_control`, `champion_support`, `recovery_timing`, or `terminal_choice`.
- `matchupRationale`: one or two sentences explaining why this lane, not GM preference, decides the result.

| Player operation | Strong against | Countered by | Required gameplay effect |
|---|---|---|---|
| `pressure` / Давление | `maneuver`, passive repositioning, exposed guard. | `guard`, `counter`, stronger opposing pressure. | Worsen `oppositionSideStrain`; cannot improve `conflictPosition` or add binding/control state. |
| `guard` / Защита | `pressure`, immediate consequence, unsafe direct clash. | `maneuver`, leverage-backed binding, eventual position loss if used passively. | Reduce/prevent `playerSideStrain` or consequence; cannot worsen `oppositionSideStrain` or improve `conflictPosition`. |
| `counter` / Контрприём | Named incoming `pressure`, binding/control, or coercive direct action. | `maneuver`, withdrawal, surrender, negotiate, `none`/`passive`; it also fails hard on bad rolls. | Requires `incomingAction`; success/partial_success/countered needs payoff; setback needs downside (`playerSideStrain`, worse `conflictPosition`, or `counterBackfire`). |
| `maneuver` / Манёвр | Passive guard, waiting, positional weakness. | `pressure`, opposing maneuver, binding/control. | Move `conflictPosition`; cannot directly change side strain or bypass active opposition `controlState`. |
| `binding` / `force_binding` / Наложение оков | Opponent after leverage or decisive success. | `break_binding`, counter-control, lack of leverage. | Add/advance `controlState` only after advantage, setup, or decisive success. |
| `break_binding` / Разрыв оков | Binding, forced handoff, coercive lock. | Stronger control, dominant opposition position. | Remove/weaken/reverse `controlState`; same-level narrowing of opposition `restrictedOperations` counts as weakened `controlState`; equal/reordered sets do not count. |
| `incarnation_resistance` / Сопротивление воплощению | `force_incarnation` / `guardian_forced`. | Winning forced-incarnation pressure after the player loses/surrenders/concedes. | Resist forced lifecycle handoff only; voluntary incarnation is not combat. |
| `champion_coordination` / Координация чемпиона | `champion_duel` where an ally is lead. | Pressure against the champion side, disrupted support, invalid side model. | Improve champion-side support/position; cannot replace direct-duel actions. |
| `recover_spiritual_power` / Собрать Средоточие | `guard`, `counter`, `none`, `passive`. | `pressure`, `maneuver`, `binding`, `force_binding`, `force_incarnation`. | Restore ОД through `actionCostAudit`; capped by `actionEconomy.player.max`; punished timing restores only 0..1 ОД. |

## Position Modifiers

`conflictPosition` is not flavor. In every contested exchange with non-`contested` `before.conflictPosition`, `diceAudit.modifierBreakdown` must include the starting position as exactly one explicit modifier with exact matching `position`; do not split, duplicate, blank, omit, or add extra `conflict_position` entries:

| Starting position | Required modifier |
|---|---|
| `player_advantaged` | `modifierBreakdown.player[]` contains `{ "modifierType": "conflict_position", "source": "conflictPosition", "position": "player_advantaged", "value": 2 }` |
| `player_dominant` | `modifierBreakdown.player[]` contains the same shape with `position="player_dominant"` and `value=4` |
| `opposition_advantaged` | `modifierBreakdown.opposition[]` contains the same shape with `position="opposition_advantaged"` and `value=2` |
| `opposition_dominant` | `modifierBreakdown.opposition[]` contains the same shape with `position="opposition_dominant"` and `value=4` |

`contested` means zero `conflict_position` entries. The modifier uses the position before the exchange because the roll is made from the starting tactical state; the after-state records what changed.

## Control State

`controlState` is the mechanical meaning of оковы / контроль. It does not deal strain and does not move position by itself; it restricts action freedom and creates leverage for later exchanges.

Canonical shape:

```json
{
  "level": "hindered",
  "controllerSide": "player",
  "controlId": "control_example_001",
  "sourceOperation": "binding",
  "restrictedOperations": [ "maneuver", "binding" ],
  "summary": "The soul pins the opponent's route through the current."
}
```

Rules:

- Missing or `null` `controlState` is legacy-compatible and means `none`.
- In a new/current exchange where active `controlState` already exists or the exchange creates/changes active control, write both `before.controlState` and `after.controlState`; if there is no active control on one side of the snapshot, write `null` or `{ "level": "none" }` instead of omitting the field.
- Active control must use `level=hindered|bound|locked`, `controllerSide=player|opposition`, non-empty `controlId`, `sourceOperation=binding|force_binding|force_incarnation|break_binding|incarnation_resistance|counter|guard|repair`, non-empty `restrictedOperations`, and `summary`; `sourceOperation` is not a free operation id.
- `binding` / `force_binding` on success/partial success must create or strengthen player control only after active opposition control is absent: `none -> hindered`, `hindered -> bound`, or `bound -> locked`.
- If opposition `controlState.restrictedOperations` names a spiritual operation, that operation cannot succeed until the control is answered first through `break_binding`, a valid `counter`, `incarnation_resistance` for forced-incarnation control, negotiation, surrender, or a failed/blocked restricted attempt.
- Failed binding/force_binding outcomes (`blocked`, `countered`, `setback`) leave player `controlState` unchanged, including same-level `controlId`/`restrictedOperations` rewrites.
- `force_binding` requires strong leverage: `player_dominant`, ready setup, or `decisive_player_success`.
- Successful `force_binding` must have a broader control payoff than ordinary `binding`: `restrictedOperations` contains at least two distinct supported operation ids.
- `break_binding` on success/partial success must weaken, remove, or reverse opposition control.
- Same-level narrowing of opposition `restrictedOperations` counts as weakened `controlState`; equal/reordered sets do not count.
- Failed incarnation_resistance outcomes leave forced-incarnation `controlState` unchanged.
- `pressure` must not create or change `controlState`.
- `maneuver` cannot improve `conflictPosition` while opposition control is active unless the control is first answered through `break_binding`, valid `counter`, `incarnation_resistance` for forced-incarnation control, negotiation, surrender, or concession.
- `guard` may stop a new incoming control from being applied, but it does not remove existing control.

## Conflict Reward Audit

Afterlife conflict rewards are mechanical, not flavor. A reward is allowed only for a resolved contested player victory with `diceAudit.outcomeBand = player_success` or `decisive_player_success`.

| Realm | Currency | Russian label | State delta |
|---|---|---|---|
| `Chaos Sea` | `ink_feathers` | Чернильные Перья | `metaStateUpdates.inkFeatherChanges.add` must equal `rewardAudit.finalAmount`. |
| `Shining Abode` | `light_sparks` | Искры Света | `shining_abode_state.json.lightSparks` must increase by `rewardAudit.finalAmount`. |

`rewardAudit` must include `realm`, `currency`, `baseAmount`, `opposingLeadStrength`, `sideModel`, `startingConflictPosition`, `challengeTier`, `outcomeMultiplierPercent`, `riskMultiplierPercent`, `riskReason`, `finalAmount`, and `narrativeReason`. No reward is allowed for `repair_cancel`, `no_effect`, voluntary withdrawal/surrender, pure negotiation/no-contest, duplicate reward for the same `conflictId`, wrong realm, or wrong currency.

## Critical Result Audit

Natural 20 / натуральная 20 and natural 1 / натуральная 1 are bounded criticals. Bounded criticals are symmetric: a favorable critical for the player (player-side natural 20 or opposition-side natural 1) raises a worse margin result only to ordinary `player_success`, while an unfavorable critical for the player (player-side natural 1 or opposition-side natural 20) lowers a better margin result only to ordinary `opposition_success`. Opposed criticals cancel and use the margin band. A critical does not create `decisive_player_success` or `decisive_opposition_success` by itself; decisive outcomes still require the margin to already reach the decisive threshold.

When a critical changes the margin-derived band, `diceAudit.criticalResult` must include `playerNaturalRoll`, `oppositionNaturalRoll`, `marginOutcomeBand`, `normalizedOutcomeBand`, `scaleLimit`, and `narrativeConstraint`. `scaleLimit` is the "no impossible mosquito victory" field: it explains the maximum plausible effect for this action, power gap, side model, and current conflict position.

## State Value Labels

| Canonical value | Russian label |
|---|---|
| `opposition_dominant` | противник доминирует |
| `opposition_advantaged` | преимущество противника |
| `contested` | спорная позиция |
| `player_advantaged` | преимущество игрока |
| `player_dominant` | игрок доминирует |
| `clear` | устойчиво |
| `strained` | напряжено |
| `fractured` | надломлено |
| `overwhelmed` | подавлено |
| `broken` | сломлено |
| `active` | активен |
| `concession_pending` | уступка ожидает закрытия |
| `surrender_pending` | сдача ожидает закрытия |
| `retreat_pending` | отступление ожидает закрытия |
| `ready_to_resolve` | готов к завершению |
| `resolved` | завершён |
| `repair_cancelled` | отменён repair-путём |

## Authoring Rule

Use Russian terms for player-facing explanations and prose, but never translate the JSON keys or enum values inside canonical state. Example: write "обмен действиями (exchange)", then persist `"mode": "exchange"` and `"operationType": "guard"`.
