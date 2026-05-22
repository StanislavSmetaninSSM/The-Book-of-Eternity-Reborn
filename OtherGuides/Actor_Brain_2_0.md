# Actor Brain 2.0

`Actor Brain 2.0` is the shared GM reasoning protocol for any actor whose state, memory, strategy, political position, journal, project, or relationship is materially changed in an accepted turn. It is additive: it must not simplify or replace the existing mortal-world `NPC Brain 2.0`.

## Core Contract

Every non-trivial actor decision should document:

- Identity and scope: actor name/id, role, current realm/layer, and why the actor is relevant this turn.
- Situation: what the actor perceives from their limited knowledge.
- Motivation: desire, fear, duty, oath, hunger, ambition, affection, loyalty, or survival pressure.
- Constraints: what the actor cannot know, cannot do, or refuses to do.
- Варианты стратегий: at least the plausible alternatives the actor could choose.
- Chosen strategy: the selected action and why it wins over the alternatives.
- Rejected alternatives: why the other strategies are worse right now.
- State changes: exact state surfaces changed by this reasoning block.

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
- Ситуация: ...
- Мысли: ...
- Варианты стратегий: ...
- Выбранная стратегия: ...
- Почему альтернативы отвергнуты: ...
- State changes: ...

### Канцлер Лучей
- Ситуация: ...
- Мысли: ...
- Варианты стратегий: ...
- Выбранная стратегия: ...
- Почему альтернативы отвергнуты: ...
- State changes: ...
```
