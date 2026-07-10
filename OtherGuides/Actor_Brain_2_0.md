# Actor Brain 2.0

`Actor Brain 2.0` is the shared GM reasoning protocol for any actor whose state, memory, strategy, political position, journal, project, or relationship is materially changed in an accepted turn. It is additive: it must not simplify or replace the existing mortal-world `NPC Brain 2.0`.

## Core Contract

Every non-trivial actor decision should document:

- Identity and scope: actor name/id, role, current realm/layer, and why the actor is relevant this turn.
- Situation: what the actor perceives from their limited knowledge.
- Profile inputs: the exact traits, relationships, memories, role, domain, and current state that matter to this decision.
- Motivation: desire, fear, duty, oath, hunger, ambition, affection, loyalty, or survival pressure.
- Constraints: what the actor cannot know, cannot do, or refuses to do.
- Варианты стратегий: at least two genuinely different alternatives. Every numbered alternative records an explicit `Выгода / Benefit` and `Риск / Risk`.
- Chosen strategy: the selected action and why it wins over the alternatives.
- Rejected alternatives: why the other strategies are worse right now.
- State changes: exact state surfaces changed by this reasoning block.

When a relevant non-player actor receives a player action, speaks, reacts, fights, negotiates, or otherwise chooses behavior, this full decision audit is mandatory. The resulting subjective reaction is also appended in the same accepted turn to that actor's own canonical thought journal:

- Guardian: prefer `game_state/meta/guardian_thought_journal.json` through `guardianThoughtJournalUpdates`; `game_state/meta/guardians.json` -> `guardians[].musings` through `UpdateGuardians command=addMusings` remains a valid alternative. Write the reaction to exactly one of these surfaces, not both;
- Mortal NPC: `game_state/npcs/npc_journals.json` -> matching `NPCJournals[].journalEntries[]`;
- Guardian Abode resident: `game_state/meta/guardian_abode_residents.json` through `residentThoughtJournalUpdates`, normalized into `thoughtJournal`.
- Other significant afterlife entity: initialize actor-owned `gmThoughtsSummary` when first materializing the profile; every later decision appends a `ledger` / `progressionLedger` entry in `game_state/meta/afterlife_entity_profiles.json`, even when the current summary also changes.
- Shining faction: initialize `factions[].strategicMemory` through `shiningFactionStrategicMemoryUpdates` when first materializing the faction. Every later decision appends `shiningFactionChronicleUpdates` into `factions[].chronicle`; the mutable current strategy may change alongside that append, never instead of it.

The Actor Brain block is the GM's decision audit. The journal entry is the actor's concise first-person memory. They are different records and neither replaces the other. Append a fresh entry; do not edit an old journal entry to imitate a new thought.

Every independent non-player name in relevant actors must have a canonical memory owner. Do not invent a reasoning-only actor. Materialize a genuine Guardian, resident, afterlife entity profile, or Shining faction with a stable id, or remove a non-actor label from relevant actors and its standalone block.

Use `## Actor Brain 2.0` as the preferred heading. Blocks use `### Actor Name`. Existing accepted headings such as `## Reasoning`, `## Размышления NPC`, and `## Guardian Thoughts` remain valid compatibility headings.

## Mortal NPC Pack

`NPC Brain 2.0` remains the deep mortal-world roleplay protocol. Do not dilute it when generalizing Actor Brain 2.0. Mortal NPC reasoning must preserve, when relevant:

- knowledge audit and hidden/known information boundaries;
- personality, culture, faith, class, profession, social hierarchy, faction pressure, and family/social ties;
- relationship state and attitude toward the player;
- strategy matrix, chosen strategy, and rejected alternatives;
- the `фильтр привлекательности`: physical appearance, attraction, charisma, fear, disgust, social norms, and personal taste can materially change mortal NPC reactions;
- concrete State changes in NPC files, quests, journals, relationships, trade, faction surfaces, or scene state.

The universal Actor Brain 2.0 layer is a wrapper around this mortal protocol, not a replacement. The rule is: do not simplify NPC Brain 2.0; keep its depth and add shared scope/state-change discipline.
Русское правило для промптов: `Actor Brain 2.0` не упрощает `NPC Brain 2.0`; он только расширяет общий формат анализа акторов.

## Guardian Pack

Use the Guardian pack for Хранители and Primordial Guardians. Record:

- domain, oath, core wound, fear, temptation, and long-term motive;
- relationship to the soul, reputation pressure, current trust/hostility, and known mortal-life evidence;
- past lives memory, Guardian quests, revealed secrets, and non-physical proof;
- incarnation authority, forced-incarnation willingness, restraint, mercy, or coercion;
- projects, abode power, alliances/rivalries with other Guardians, and consequences for the Chaos Sea;
- chosen strategy, rejected alternatives, and exact Guardian/Soul/project/journal State changes.

## Resident Pack

Use the Resident pack for Guardian Abode residents and afterlife companions. Record:

- abode/guardian link, bond to the soul, reason to stay, leave, help, betray, or ask for a quest;
- loyalty, anxiety, hope, shame, memory fragments, and hidden need;
- resident journal/log/history surfaces affected this turn;
- linked soul quest, relic grant, transfer request, or companion-echo consequences;
- chosen strategy, rejected alternatives, and exact resident/Soul Quest/relic State changes.

## Shining Political Pack

Use the Shining political pack for Shining factions, halls, gates, heads of faction, radiant actors, faction agents, and institutions. Record:

- power/status, faction gain or loss, public legitimacy, fear of exposure, and faction ambition;
- relationship to the soul, Guardians, residents, rival factions, Сареф, and `Крылья Ангелов`;
- pressure points: trade, projects, leadership, hall control, unrest, alliances, sabotage, or oath politics;
- Варианты стратегий: alliance, pressure, sabotage, negotiation, refusal, concession, legal challenge, war, or withdrawal;
- chosen strategy, rejected alternatives, and exact Shining faction / `shiningPoliticalActors` / campaign / trade / project State changes.

If a turn changes `shiningPoliticalActors[]`, the changed radiant actor must be declared in relevant actors and must have an Actor Brain 2.0 block or equivalent reasoning block. The same discipline applies to important Shining faction/head-actor changes even when the changed surface is political rather than personal.

## Minimal And Full Use

Minimal reasoning is acceptable for trivial stable contours: declare the actor/institution outside scope and explain why nothing changes.

Full Actor Brain 2.0 is required when the actor chooses a strategy, changes state, closes a pending contract, grants/refuses a reward, advances a quest, attacks, negotiates, sabotages, updates a project, or changes political posture.

If no non-player actor actually reacts or chooses behavior, the GM may use a genuinely actorless `Scene-local` scope and explain why. A named actor who speaks, receives the player's question, attacks, bargains, or makes a decision cannot be moved outside scope merely to avoid the full audit or journal update.

## Output Shape

Preferred `gm_thoughts_markdown` shape:

```markdown
## Охват NPC-анализа
- Режим: Mixed
- Релевантные акторы: Азалия, Канцлер Лучей
- Почему они релевантны: оба меняют state surfaces в этом ходу.
- Акторы вне охвата: остальные Хранители
- Почему они вне охвата: их проекты и отношения не меняются.

## Actor Brain 2.0
### Азалия
- Текущая локация: Сад нитей; остаётся у Камня Памяти.
- Ситуация: ...
- Данные профиля: домен памяти, осторожная привязанность к Душе, обещание не отнимать выбор.
- Мотивация: помочь Душе сохранить воспоминание, не сделав её зависимой от Хранителя.
- Ограничения: Азалия не может прожить выбор вместо Души и не раскрывает чужие воспоминания.
- Мысли: просьба искренняя, но готовый ответ ослабит самостоятельность Души.
- Варианты стратегий:
  1. Дать ограниченный ритуал. Выгода: Душа получит применимый способ защиты. Риск: она примет метафору за готовый щит.
  2. Отказать до испытания. Выгода: тайна Сада останется закрыта. Риск: отказ разрушит начальное доверие.
- Выбранная стратегия: дать ограниченный ритуал и вернуть ответственность Душе.
- Почему альтернативы отвергнуты: полный отказ несоразмерен безопасной и искренней просьбе.
- Действия: Азалия объясняет правило имени, обещания и возвращения.
- Изменения состояния: `guardianThoughtJournalUpdates` добавляет новую first-person запись в `guardian_thought_journal.json`.

### Канцлер Лучей
- Текущая локация: Зал Весов; остаётся на заседании.
- Ситуация: ...
- Данные профиля: ...
- Мотивация: ...
- Ограничения: ...
- Мысли: ...
- Варианты стратегий:
  1. ... Выгода: ... Риск: ...
  2. ... Выгода: ... Риск: ...
- Выбранная стратегия: ...
- Почему альтернативы отвергнуты: ...
- Действия: ...
- Изменения состояния: ...
```

A newly materialized NPC, Guardian Abode resident, afterlife entity, political actor, or Shining faction is a relevant actor when its canonical state first appears: initial creation cannot bypass Actor Brain scope. If the action carries a stable `npcId`, `residentId`, `guardianId`, `actorId`, or `factionId`, bind the decision and memory delta to that exact stable actor id. Two actors may share a display name; a journal entry from the other same-name actor is not valid evidence.

## Repair Harness

If validation writes `harnessRepairPackets[].kind = accepted_turn_output_artifact_repair`, treat `targetFiles[]` as an exact allowlist. A stale narrative/interface refresh does not make a valid Actor Brain stale: when `output/debug_logs.json` is absent from the list, do not rewrite it. When the debug log itself is listed, preserve or reconstruct the complete Actor Brain block and retain the exact accepted memory surface in `Изменения состояния:` (`NPCJournals[].journalEntries[]`, `guardianThoughtJournalUpdates` / `UpdateGuardians.addMusings`, `residentThoughtJournalUpdates`, afterlife entity `ledger`/`progressionLedger`, or `shiningFactionChronicleUpdates`). Never collapse that evidence into generic `state unchanged` wording. A genuinely actorless scope stays actorless.

If validation writes `harnessRepairPackets[].kind = actor_reasoning_subpoint_repair`, use its `debugLogTemplate` and fill every field above. Repair only `output/debug_logs.json.gm_thoughts_markdown`; do not re-resolve the scene.

If validation writes `harnessRepairPackets[].kind = actor_memory_persistence_repair`, the decision audit already exists. Append one concise first-person thought or actor-owned memory entry to the exact canonical surface listed in `targetFiles`, preserve old entries, and update only that actor's `Изменения состояния:` line in the packet-listed `output/debug_logs.json` so it names the journal command/surface actually used. During direct Guardian journal repair, `game_state/meta/guardian_thought_journal.json` is canonical on-disk state and must contain top-level `entries[]`; `guardianThoughtJournalUpdates` is a first-pass response command, not a replacement for the canonical root, and `schemaVersion` is not supported there. Do not rewrite narrative, interface options, or unrelated state: an internal memory-only repair does not invalidate the already authored player-facing scene. An existing Shining faction is repaired only by appending `shiningFactionChronicleUpdates`; do not use a `shiningFactionStrategicMemoryUpdates` rewrite as historical persistence. Finish repair through `Complete-BoeValidationRepair`.

If validation reports `afterlife_relevant_actor_missing_canonical_memory_owner`, follow the `afterlife_entity_profile_scaffold_repair` packet. Materialize a complete supported profile only when the named actor genuinely exists; otherwise remove the invented/non-actor label from relevant actors and its standalone Actor Brain block.
