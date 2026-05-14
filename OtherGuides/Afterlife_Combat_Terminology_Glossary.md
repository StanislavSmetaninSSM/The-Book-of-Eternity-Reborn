# Afterlife Combat Terminology Glossary

This glossary fixes the Russian player/GM labels for afterlife combat terms. Canonical JSON field names, enum values, command tags, and validation ids stay in English; UI and GM-facing prose should show the Russian label first and keep the canonical term nearby when it matters for authoring JSON.

| Canonical term | Russian label | Use in play |
|---|---|---|
| afterlife spiritual conflict | духовный конфликт посмертия | The whole afterlife combat-like contest system in `Chaos Sea` or ordinary active `Shining Abode`. |
| afterlife spiritual action | духовное действие посмертия | A player action inside an already active conflict; `/spiritual_action` only adds an explicit routing tag. |
| Spiritual Arts | духовные искусства | Client-owned afterlife combat upgrades stored in `soul_state.afterlifeCombatProfile.artTiers`. |
| exchange | обмен действиями | One resolved beat inside an active conflict; written through `afterlifeSpiritualConflictUpdate.mode=exchange`. |
| resolve | завершение конфликта | Terminal closure of an active conflict; written through `mode=resolve` and moved into `recentConflicts[]`. |
| repair_cancel | repair-отмена / ремонтная отмена | Non-reward cleanup of malformed or impossible conflict state. |
| diceAudit | аудит кубиков | Required visible-dice proof for contested exchanges and contested terminal resolutions. |
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

## Spiritual Art Operation Rules

These are mechanical rules, not flavor synonyms. If a player writes prose, classify it into one primary operation and keep the state delta inside that operation's allowed lane.

| Art / operation | Valid use | May change | Must not do | Example |
|---|---|---|---|---|
| `pressure` / Давление | Directly challenge the opposing lead contestant. | Mainly `oppositionSideStrain`; optionally terminal resolve after a later valid close. | Do not treat it as a free `conflictPosition` maneuver or binding. | "I press on the Guardian's broken oath" can move opposition strain `clear -> strained`. |
| `guard` / Защита | Prevent or reduce incoming strain/consequence against the player side. | `playerSideStrain`, blocked consequence, defensive `incomingAction` audit. | Do not damage opposition strain directly; use `counter` for reversal. | "I shield the soul-fracture" can keep player strain from worsening. |
| `counter` / Контрприём | React to a concrete incoming operation. | `incomingAction`, blocked/countered audit, possibly position/strain swing on success. | Cannot be used without `incomingAction`; it is not a standalone attack. | "As he binds me, I turn the thread back" must name the incoming bind/pressure. |
| `maneuver` / Манёвр | Shift advantage without raw overpowering. | `conflictPosition`. | Successful maneuver must not directly change `playerSideStrain` or `oppositionSideStrain`. | `contested -> player_advantaged` without strain damage. |
| `binding` / `force_binding` / Наложение оков | Control after leverage. Requires `player_advantaged`, `player_dominant`, setup, or `decisive_player_success`. | binding/lock state, restricted future actions, setup for resolve. | Cannot be spammed from neutral `contested` on ordinary success. | After gaining advantage, the soul seals the opponent's route. |
| `break_binding` / Разрыв оков | Answer an existing binding, forced handoff, or coercive lock. | binding state, forced handoff state, position if the break creates leverage. | Not a generic attack or defense against ordinary pressure. | Break a name-seal before it becomes forced incarnation. |
| `incarnation_resistance` / Сопротивление воплощению | Resist `force_incarnation` / `guardian_forced`. | forced-incarnation proof state, resistance audit, possibly `resolutionState`. | Not a replacement for `guard` against ordinary pressure. | Resist a Guardian trying to throw the soul into a life. |
| `champion_coordination` / Координация чемпиона | Support a `champion_duel` where an ally is lead contestant. | champion-side support modifier, `conflictPosition`, side support audit. | Cannot be used in `direct_duel` as if the player were lead. | The soul guides an allied Guardian's strike while staying supporter. |

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
