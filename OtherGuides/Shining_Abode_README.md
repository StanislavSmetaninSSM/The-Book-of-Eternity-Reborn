# Shining Abode Docs README

## Зачем нужен этот файл

Этот README нужен как **карта чтения** для всех документов по Сияющей Обители.

Его цель:

- быстро понять, **какой документ главный**
- не читать старые Shining notes как равноправные источники истины
- не спутать:
  - gameplay formulas
  - current runtime/lifecycle constraints
  - political layer
  - final implementation contracts

Если ты начинаешь новую работу по Сияющей Обители, **начинай с этого README**, а не со старого implementation plan.

---

## Главный принцип

Сейчас документы по Сияющей Обители надо читать **не как четыре независимых спецификации**, а как layered stack.

При конфликте приоритет такой:

1. [Shining_Abode_Consolidation_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md>)
2. [Shining_Abode_Faction_Politics_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Faction_Politics_Addendum.md>)
3. [Shining_Abode_Implementation_Plan_Rebased.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan_Rebased.md>)
4. [Shining_Abode_Implementation_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan.md>)
5. [Shining_Abode_Endgame_Design_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Endgame_Design_Plan.md>)

Простой смысл:

- **Consolidation Addendum** отвечает на вопрос:  
  `как это должно быть реализовано сейчас, без оставшихся decision gaps`
- **Rebased Plan** отвечает на вопрос:  
  `как Shining должна встроиться в текущую codebase и guardian-policy model`
- **Old Implementation Plan** отвечает на вопрос:  
  `какие формулы, shapes и таблицы уже были придуманы для core mechanics`
- **Endgame Design Plan** отвечает на вопрос:  
  `какой был исходный gameplay intent`
- **Faction Politics Addendum** отвечает на вопрос:  
  `почему политический слой устроен именно так`

---

## Future Branch Docs

Ниже лежат документы, которые относятся не к текущему обязательному Shining contract, а к **будущим расширениям после завершения базовой Сияющей Обители**.

Сейчас такой документ:

- [Shining_Abode_Player_Founded_Guardian_Design.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Player_Founded_Guardian_Design.md>)

Его смысл:

- это отдельная future-branch спецификация для механики, где вознесённая душа после возвращения в `Chaos Sea` может учредить собственного Хранителя;
- этот документ **не переопределяет** текущий Shining source-of-truth stack;
- обращаться к нему надо только если задача прямо касается этой новой ветки.

---

## Working Audit Backlog

Для текущей post-implementation доработки есть отдельный рабочий backlog-аудит:

- [Chaos_Sea_And_Shining_Audit_Backlog.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Chaos_Sea_And_Shining_Audit_Backlog.md>)

Его смысл:

- это не source-of-truth design spec, а **рабочий список подтверждённых проблем и недопоказанных player-facing данных**;
- в нём фиксируются:
  - gameplay defects
  - lifecycle / validation gaps
  - player-facing data completeness gaps
- после каждого fix-pass этот backlog надо обновлять, а не переписывать задним числом историю найденных проблем.

---

## В каком порядке читать

### Если задача про реализацию

Читай так:

1. [Shining_Abode_README.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_README.md>)
2. [Shining_Abode_Consolidation_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md>)
3. [Shining_Abode_Implementation_Plan_Rebased.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan_Rebased.md>)
4. только потом consult:
   - [Shining_Abode_Implementation_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan.md>)
   - [Shining_Abode_Endgame_Design_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Endgame_Design_Plan.md>)

### Если задача про баланс, таблицы, rarity, costs, project outputs

Читай так:

1. [Shining_Abode_Consolidation_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md>)  
   сначала, чтобы не опереться на deprecated shape/ownership assumptions
2. [Shining_Abode_Implementation_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan.md>)
3. [Shining_Abode_Endgame_Design_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Endgame_Design_Plan.md>)

### Если задача про runtime, lifecycle, validation, ownership, state files

Читай так:

1. [Shining_Abode_Consolidation_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md>)
2. [Shining_Abode_Implementation_Plan_Rebased.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan_Rebased.md>)
3. old docs только как secondary formula reference

### Если задача про фракционную политику

Читай так:

1. [Shining_Abode_Consolidation_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md>)
2. [Shining_Abode_Faction_Politics_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Faction_Politics_Addendum.md>)
3. [Shining_Abode_Implementation_Plan_Rebased.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan_Rebased.md>)  
   только чтобы не выйти за текущие runtime/lifecycle constraints

---

## Что считать source of truth по разным вопросам

### Final schema / ownership / wire contracts

Source of truth:

- [Shining_Abode_Consolidation_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md>)

Именно там надо искать:

- final faction shape
- `charter` vs `leadership`
- `shiningPoliticalActors[]`
- `pending_shining_faction_foundings.json`
- `pending_shining_faction_realignments.json`
- `pending_shining_faction_leadership_transitions.json`
- receipts/history ownership
- precedence and lock rules

### Current codebase integration rules

Source of truth:

- [Shining_Abode_Implementation_Plan_Rebased.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan_Rebased.md>)

Именно там искать:

- `preparedIncarnationPackage` as higher-priority mode
- `/reenter_shining_abode`
- `/return_to_chaos_sea`
- strict guardian-policy expectations
- validation/normalizer parity
- ownership boundaries between files

### Core formulas and mechanical tables

Primary source:

- [Shining_Abode_Implementation_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan.md>)

Secondary intent source:

- [Shining_Abode_Endgame_Design_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Endgame_Design_Plan.md>)

Именно там искать:

- radiance thresholds
- light sparks rules
- faction/project formulas
- card family payloads
- gate generation steps
- project compatibility tables
- costs and rewards

### Political rationale and former-leader semantics

Source of truth:

- [Shining_Abode_Faction_Politics_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Faction_Politics_Addendum.md>)

Именно там искать:

- why charter is separate from current leadership
- why guardian does not become resident after losing power
- why `radiant_actor` exists
- why faction loyalty is separate from ordinary abode devotion

Но если politics addendum конфликтует с consolidation addendum, **главнее consolidation**.

---

## Что нельзя делать при чтении этих документов

Не надо:

- читать старый `Shining_Abode_Implementation_Plan.md` как единственный current spec
- реализовывать old flat faction shape параллельно с new nested `charter/leadership`
- заново принимать решения, уже зафиксированные в consolidation doc
- вытаскивать runtime/lifecycle semantics из lore/design notes, игнорируя rebased plan
- считать, что current head still defines faction patron identity

Особенно опасные устаревшие assumptions:

- flat `factionName/favoredArchetype/patronEffectFamily/headActorType/headActorId` as final canonical faction shape
- `faction.headActorId -> guardians.json` as the only allowed head reference
- отсутствие explicit political request/receipt flows
- implicit hall derivation for `player_founded` factions

---

## Быстрый cheat sheet

Если вопрос звучит так:

- `Какой JSON shape у фракции?`
  - смотри `Consolidation Addendum`
- `Какой файл владеет этим состоянием?`
  - смотри `Consolidation Addendum`, потом `Rebased Plan`
- `Как работает handoff mode и preparedIncarnationPackage?`
  - смотри `Rebased Plan`
- `Какие формулы силы, тиров, карточек и costs?`
  - смотри `Implementation Plan`
- `Почему guardian не становится resident после потери власти?`
  - смотри `Faction Politics Addendum`
- `Какой ответ главный, если документы спорят?`
  - смотри порядок приоритета в начале этого README

---

## Практическое правило для будущей работы

Если ты:

- планируешь новую Shining feature,
- перепроверяешь старый план,
- или реализуешь код,

то рабочая последовательность должна быть такой:

1. подтвердить решение по [Shining_Abode_Consolidation_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md>)
2. проверить, не конфликтует ли оно с [Shining_Abode_Implementation_Plan_Rebased.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan_Rebased.md>)
3. только потом брать formulas/tables из [Shining_Abode_Implementation_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Implementation_Plan.md>)
4. если нужен мотив или design intent, дочитывать [Shining_Abode_Faction_Politics_Addendum.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Faction_Politics_Addendum.md>) и [Shining_Abode_Endgame_Design_Plan.md](</E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Endgame_Design_Plan.md>)

Коротко:

**Consolidation tells you what is final.  
Rebased tells you how it must fit the current game.  
Old plan tells you the formulas.  
Politics addendum tells you why the political layer exists.**
