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
