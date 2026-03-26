# Сила Обители: дизайн-план

## Цель
Добавить для каждого Хранителя числовой параметр `Сила Обители`, который:

- растёт и падает по понятным игромеханическим причинам;
- влияет на реальные системы клиента, а не только на ролевой текст;
- усиливает как дружественную помощь, так и враждебное вмешательство;
- не ломает явный сценарий следующей жизни, заданный игроком;
- поддерживает политику Хранителей и проекты, направленные друг против друга.

## Ключевая модель

- `Репутация` отвечает на вопрос: Хранитель хочет помочь, навредить или безразличен.
- `Сила Обители` отвечает на вопрос: насколько сильным будет это воздействие.

Комбинации:

- высокая репутация + сильная обитель = сильная поддержка;
- низкая репутация + сильная обитель = сильное наказание/давление;
- слабая обитель ограничивает и помощь, и вред.

## Шкала

`Сила Обители = 0..100`

Тиры:

- `0..19` — `Угасающая`
- `20..39` — `Хрупкая`
- `40..59` — `Стабильная`
- `60..79` — `Могущественная`
- `80..100` — `Сияющая`

Начальное значение для нового Хранителя в v1:

- `35` для обычного newly materialized Хранителя
- допустим optional override пресетом для извечных Хранителей позже, но не в v1

## Откуда берётся рост

### 1. Квесты Хранителя

Изменение силы обители происходит только при значимом исходе, а не от каждого разговора.

- `Easy quest complete` = `+2`
- `Normal quest complete` = `+3`
- `Hard quest complete` = `+5`
- `Epic quest complete` = `+7`

Модификаторы:

- если quest payload содержит `supportsCurrentProject = true`: примерно `+33%` к приросту, минимум `+1`, округление вверх
- если quest payload содержит `defendsAgainstRivalPressure = true`: ещё `+1`

### 2. Помощь текущему проекту Хранителя

За самостоятельную помощь проекту, отличную от обычного квеста:

- `minor assist` = `+1`
- `meaningful assist` = `+2`
- `major breakthrough assist` = `+3`

Это должно оформляться не свободным текстом, а через project/progression outcome, который можно валидировать.

### 3. Завершение проекта Хранителя

Проект — один из главных способов накачать силу.

- `Minor project completed` = `+5`
- `Major project completed` = `+10`
- `Grand project completed` = `+16`

Если проект был offensive и бил по другому Хранителю:

- атакующий всё равно получает свой прирост силы;
- цель additionally теряет силу по формуле из раздела `Политика Хранителей`.

### 4. Подношения в Обитель

Чтобы был прямой sink/source вне квестов:

- каждые `50 Ink Feathers`, осознанно пожертвованные в Обитель = `+1`
- cap: не больше `+3` за одно возвращение между смертными жизнями
- `Soul Relic`:
  - `common/uncommon = +1`
  - `rare = +2`
  - `epic = +3`
  - `legendary/mythic/divine = +4`
- `Архив Души` (`archive_lore_fragment` / `archive_secret_record`):
  - `common/uncommon = +1`
  - `rare = +2`
  - `epic/legendary/unique = +3`

Это нужно формализовать отдельным whitelist-командным путём позже, а не оставлять на свободную импровизацию GM.

Дополнительный content-path:

- `Архивная консультация` у дружественного Хранителя:
  - `lore_fragment` -> materialize-ится в `+1 гарантированный дополнительный квест Хранителя` в следующей жизни
  - `secret_record` -> materialize-ится в `+1 visible rival clue budget` на следующую жизнь
  - действие инициируется клиентом через pending request; запись сразу резервируется в Архиве, но расходуется только при `accepted`
  - `rejected/cancelled` освобождает запись обратно в Архив
  - для materialization используется synthetic completed `lore_research`-effect с `projectOrigin = archive_consultation`
- `Archive project fuel`:
  - `lore_fragment` можно вложить в активный проект Хранителя как `+workDone`
  - `secret_record` можно вложить в активный проект Хранителя как `-pressure`
  - действие инициируется клиентом через pending request; запись резервируется до ответа GM
  - `accepted` materialize-ится GM как canonical project update/journal entry, `rejected/cancelled` возвращает запись в Архив

### 5. Резонанс смертной жизни

В конце жизни, если прожитая жизнь сильно усилила домен Хранителя:

- `weak resonance` = `+2`
- `solid resonance` = `+4`
- `major resonance` = `+6`

Примеры:

- Хранитель Знания: игрок основал школу, библиотеку, создал трактат
- Хранитель Власти: игрок построил устойчивую политическую систему
- Хранитель Выживания: игрок поднял колонию или вытащил людей из катастрофы

## Откуда берётся падение

Сила не должна только копиться. Падение должно быть таким же явным и проверяемым.

### 1. Провалы квестов Хранителя

- `Easy quest failed/abandoned` = `-1`
- `Normal quest failed/abandoned` = `-2`
- `Hard quest failed/abandoned` = `-4`
- `Epic quest failed/abandoned` = `-6`

Если игрок не просто провалил, а публично подорвал авторитет/замысел Хранителя:

- дополнительно `-2`

### 2. Провал или саботаж текущего проекта

- `Minor project stalled/ruined` = `-3`
- `Major project stalled/ruined` = `-6`
- `Grand project stalled/ruined` = `-10`

Если игрок осознанно помог rival side сорвать проект:

- дополнительно `-3`

### 3. Враждебные проекты других Хранителей

Успешный offensive project rival-Хранителя уменьшает силу целевой Обители.
Это один из центральных элементов политики между Хранителями.

### 4. Трата силы на вмешательство в следующую жизнь

Это важный расходный канал.

Когда Хранитель вносит `Коррективы Хранителя` в старт следующей жизни, это стоит силы:

- `minor correction` = `-5`
- `medium correction` = `-12`
- `strong correction` = `-20`

Это относится и к позитивным, и к негативным корректировкам.
То есть даже дружественный Хранитель реально тратит мощь, чтобы помочь.

### 5. Стагнация при нерешённых проектах

Если у Хранителя остаётся активный проект, а игрок полностью игнорирует его в течение полного цикла между смертными жизнями:

- `-2` за цикл для `Хрупкой/Стабильной` Обители
- `-3` за цикл для `Могущественной/Сияющей`

Причина: большая Обитель требует подпитки и поддержки, а не может бесконечно стоять без движения.

Это мягкий decay, не основной источник падения.

## На что Сила Обители влияет механически

Это ключевой раздел. Эффекты должны быть производными от силы и должны валидироваться клиентом.

## 1. Торговля у Хранителя

Текущее число слотов `4` заменяется на derived rule от силы Обители:

- `0..19` = `4` слота
- `20..39` = `5`
- `40..59` = `6`
- `60..79` = `7`
- `80..100` = `8`

Дополнительно:

- высокая сила повышает ceiling ассортимента;
- репутация продолжает влиять на цены и дружественность торговли;
- сила влияет именно на `количество` и `качество` ассортимента.

Разделение ролей:

- `Репутация` = пустят ли тебя к хорошему и насколько выгодно
- `Сила Обители` = существует ли у Хранителя богатый ассортимент вообще

## 2. Пул квестов Хранителя

Текущий cap available quests должен стать derived от силы:

- `0..19` = `2` available quests
- `20..59` = `3`
- `60..79` = `4`
- `80..100` = `4`

Дополнительно сила открывает потолок типов квестов:

- `Угасающая/Хрупкая` — только простые/обычные/локальные
- `Стабильная` — серьёзные и более влиятельные
- `Могущественная` — личные, редкие, меж-хранительские
- `Сияющая` — судьбоносные, политические, доменно-уникальные

Минимальный v1 difficulty-ceiling contract для `availableQuests`:

- `0..39` — не выше `normal`
- `40..79` — не выше `hard`
- `80..100` — не выше `epic`

Это ограничение применяется к новым `availableQuests`.
Уже взятые `activeQuests` и исторические `completedQuests` не должны ретроактивно инвалидироваться только из-за последующего падения силы Обители.

## 3. Guardian Gacha / посредничество Хранителя

Сила Обители даёт дополнительные бонусы поверх репутации:

- `bonusGachaCharges`
  - `0..39 = 0`
  - `40..79 = 1`
  - `80..100 = 2`
- `guardianRarityCeilingBonusSteps`
  - `0..59 = 0`
  - `60..100 = 1`

Репутация остаётся основным gatekeeper, но сильная Обитель даёт больше реального ресурса.

### Canonical definitions

- `guardianRarityCeilingBonusSteps`
  - это чисто числовой derived-модификатор
  - каждый `+1 step` поднимает допустимый ceiling качества/редкости на одну canonical ступень
  - step никогда не может обойти глобальный абсолютный max rarity cap системы

- `upgradedTradeSlot`
  - торговый слот, сгенерированный с применением `guardianRarityCeilingBonusSteps`

- `elevatedSlot`
  - гарантированный upgradedTradeSlot
  - effective rarity ceiling for elevated slot:
    `baseCeiling + guardianRarityCeilingBonusSteps + 1`
  - elevated slot не может превышать global absolute max rarity cap

## 4. Чужие нити судьбы

Сила Обители должна влиять и на оборону, и на нападение.

### Дружественное влияние

Если репутация с Хранителем положительная и он является действующим покровителем:

- `rivalArcDefenseClues`
  - `0..19 = 0`
  - `20..39 = 1`
  - `40..59 = 1`
  - `60..79 = 2`
  - `80..100 = 3`
- `rivalArcClarityTier`
  - `0..39 = 0`
  - `40..59 = 1`
  - `60..79 = 2`
  - `80..100 = 3`
- `rivalArcCounterQuestAccess`
  - `0..59 = false`
  - `60..100 = true`
- `rivalArcWarningTier`
  - `0..79 = 0`
  - `80..100 = 1`

### Враждебное влияние

Если Хранитель hostile:

- `rivalArcOffenseCap`
  - `0..19 = no formal hostile arc sponsorship`
  - `20..39 = background pressure only, no direct-target hostile arc`
  - `40..59 = one minor hostile arc`
  - `60..79 = one major hostile arc or one direct-target minor arc`
  - `80..100 = one major hostile arc with early-signal privilege`

## 5. Следующая жизнь: Коррективы Хранителя

Это главный эффект силы, но он не должен ломать сценарий игрока.

### Базовый принцип

Игрок задаёт `Сценарное Ядро`.
Хранитель не имеет права его переписать.
Он может добавить только `Коррективы Хранителя` поверх ядра.

### Сценарное Ядро: что неприкосновенно

Нельзя отрицать или заменять явные утверждения игрока о:

- роли/статусе/идентичности на старте;
- стартовом месте;
- жанре, эпохе, тональности;
- базовой premise мира;
- явно указанных стартовых отношениях;
- явно указанном стартовом положении/ресурсах, если игрок их зафиксировал.

Пример:

Если игрок написал:

- он король
- королевство процветает
- стартует во дворце

нельзя сделать:

- он не король;
- королевство разрушено;
- стартует рабом на другом континенте.

### Что можно корректировать

Можно добавлять только совместимые давления или блага, не противоречащие ядру:

- скрытая угроза;
- соперник;
- долг;
- тайный союзник;
- проклятие;
- политическая трещина;
- незримая защита;
- редкий ресурс;
- связанный чужой квест судьбы;
- стартовое преимущество, о котором игрок не просил, но которое не ломает premise.

### Бюджет корректив от силы Обители

Вместо словесных combinations клиент должен считать единый derived budget:

- `nextLifeCorrectionBudgetPoints`
  - `0..19 = 0`
  - `20..39 = 1`
  - `40..59 = 2`
  - `60..79 = 3`
  - `80..100 = 4`

Conversion:

- `minor correction = 1 point`
- `medium correction = 2 points`
- `strong correction = 3 points`

### Типы корректив

#### Minor

Небольшое смещение старта, не перестраивает кампанию:

- скрытый наблюдатель;
- удобный контакт;
- маленький долг;
- один важный документ;
- ранний знак опасности;
- мелкое защитное благословение.

Power cost: `5`

#### Medium

Заметное давление/помощь, формирует раннюю ось конфликта:

- тайный заговор;
- скрытая болезнь под контролем;
- сильный союзник;
- локальный rival claimant;
- доменно-подходящая стартовая возможность;
- активный долг/обязательство;
- заметный ресурс или серьёзная проблема.

Power cost: `12`

#### Strong

Крупная, но всё ещё совместимая корректива:

- опасная чужая нить судьбы уже движется против игрока;
- встроенная сеть саботажа;
- мощный закрытый покровитель;
- тяжёлое доменное проклятие, не отменяющее стартовый статус;
- политический/магический кризис, который не ломает explicit premise, а осложняет её.

Power cost: `20`

### Friendly vs Hostile corrections

Коррективы не только негативные.

#### Friendly correction examples

Игрок: король процветающего королевства.

Дружественный Хранитель может добавить:

- тайный советник, уже лояльный игроку;
- скрытый защитный договор;
- преданный орден;
- раннюю возможность укрепить трон;
- доменно-подходящий дар.

#### Hostile correction examples

Тот же игрок:

- скрытая rival soul уже собирает переворот;
- родовая клятва вот-вот потребует расплаты;
- часть двора куплена чужой стороной;
- в процветающем королевстве есть спрятанная трещина, о которой игрок не знает.

### UI-инвариант: игрок должен видеть причину

Нужно отдельное меню:

- `/коррективы_хранителя`

Там показывается:

- какой Хранитель внёс коррективу;
- friendly / hostile / neutral intent;
- сила Обители на момент вмешательства;
- тип и тяжесть коррективы;
- power cost;
- что именно было изменено;
- почему это произошло.

Формат записи:

```json
{
  "correctionId": "guid",
  "lifeId": "guid_or_turn_anchor",
  "sourceGuardianId": "guardian_x",
  "sourceGuardianPresetId": "azalia",
  "intent": "friendly | hostile | neutral",
  "severity": "minor | medium | strong",
  "category": "ally | threat | rival_arc | debt | resource | omen | illness | intrigue | blessing | curse",
  "powerCost": 12,
  "abodePowerAtApplication": 68,
  "scenarioAnchor": "Игрок — король процветающего королевства",
  "appliedAdjustment": "Во двор уже внедрён rival claimant, связанный с чужой нитью судьбы",
  "reason": "Враждебный Хранитель ответил на оскорбление и сорванный проект",
  "visibleToPlayer": true
}
```

## Политика Хранителей: проекты против других Обителей

Проект Хранителя может быть:

- `internal` — усиливает его самого
- `supportive` — помогает душе или союзной линии
- `offensive` — бьёт по rival-Хранителю

### Offensive projects

Для offensive project нужны:

- `targetGuardianId`
- `projectTier`: `minor | major | grand`
- `offenseType`: `sabotage | theft | influence | curse | rival_arc | market_pressure | revelation`

### Математика удара по цели

Грубая ранняя формула здесь больше не используется как источник истины.
Для offensive-проектов см. ниже раздел `Revised offensive impact formula`.
Именно он должен считаться canonical.

## Критический gap текущей системы: проекты Хранителей слишком примитивны

Сравнение с проектами фракций показывает, что у фракций уже есть полноценный project tracker, а у Хранителей пока почти нет:

### Что есть у фракций

- `activeProjects[]` и `completedProjects[]`
- нетерминальный `activeState`
- терминальный `finalState = Completed | Abandoned`
- атомарные surfaces:
  - `factionProjectUpdates`
  - `completeFactionProjects`
- обязательные trackable поля:
  - `projectId`
  - `projectName`
  - `description`
  - `totalResourceCost`
  - `resourcesSpent`
  - `totalTimeCostMinutes`
  - `timeSpentMinutes`
  - `totalSteps`
  - `currentStep`

Это и позволяет системе честно понимать:

- что проект идёт;
- насколько он продвинулся;
- что именно потрачено;
- что считается завершением;
- что считается abandon/срывом.

### Что есть у Хранителей сейчас

- только `currentProject`
- и в нём в основном:
  - `progressPercent`
  - `estimatedTurnsLeft / estimatedCompletionTurn`
  - `playerCanAssist`
  - `assistDescription`
- плюс history-like `completedProjects`, но без полноценного terminal lifecycle

### Почему этого недостаточно

При такой модели мы не можем строго и валидируемо определить:

- что именно считается `assist`;
- чем minor help отличается от meaningful/major help;
- чем completion отличается от обычного progress tick;
- когда project провален, stalled или intentionally sabotaged;
- как offensive guardian projects бьют по rival-Обители;
- как именно считать прирост/падение `Силы Обители`.

Иными словами: пока guardian project — это почти narrative progress bar. Для `Силы Обители` этого недостаточно.

## Что нужно перенять из системы проектов фракций

Для v2 guardian projects должны стать отдельным project-tracker surface со своей математикой.

### Новый canonical contract для guardian projects

Хранитель по-прежнему хранит ровно один текущий активный проект как основную ось, но сам объект должен стать полнее:

```json
"currentProject": {
  "projectId": "guardian_project_x",
  "projectName": "НАЗВАНИЕ",
  "projectTier": "minor | major | grand",
  "projectMode": "internal | supportive | offensive",
  "activeState": "Active | Gathering | Testing | Negotiating | Infiltrating | Fortifying | non-terminal stage",
  "description": "Текущая стадия проекта",
  "totalWork": 12,
  "workDone": 5,
  "totalSteps": 4,
  "currentStep": 2,
  "startedTurn": 80,
  "estimatedCompletionTurn": 95,
  "playerCanAssist": true,
  "assistDescription": "Чем именно может помочь душа",
  "targetGuardianId": null,
  "targetAbodeId": null,
  "stakes": "Что изменится при успехе/провале",
  "outcome": null
}
```

Дополнительно:

```json
"completedProjects": [
  {
    "projectId": "guardian_project_x",
    "projectName": "НАЗВАНИЕ",
    "projectTier": "major",
    "projectMode": "offensive",
    "completionTurn": 97,
    "finalState": "Completed | Abandoned | Sabotaged | Collapsed",
    "outcome": "Краткий итог",
    "targetGuardianId": "guardian_y"
  }
]
```

### Зачем новые поля

- `projectTier` нужен для математики силы Обители
- `projectMode` отделяет обычный внутренний проект от offensive-политики
- `activeState` делает видимой реальную нетерминальную стадию
- `totalWork / workDone` создают внутриигровую математику самого проекта
- `totalSteps / currentStep` дают дискретную progression axis
- `targetGuardianId / targetAbodeId` обязательны для offensive projects
- `finalState` нужен для честного различения:
  - завершён
  - брошен
  - сорван соперником/игроком
  - рухнул по своим причинам

## Guardian project lifecycle: как считать ход проекта

### 1. Создание

Каждый Хранитель обязан иметь ровно один активный `currentProject`.

При создании проекта задаются:

- `projectTier`
- `projectMode`
- `totalWork`
- `totalSteps`
- `workDone = 0`
- `currentStep = 0`
- `activeState = first stage label from recipe`

## На чём считать математику проектов Хранителей

Короткий ответ: не на фракционных ресурсах, а на **метафизической работе проекта**.

Для Chaos Sea у Хранителей нет смысла копировать `Wealth / Influence / Manpower` один в один.
Вместо этого у guardian projects должны быть свои canonical счётчики.

### Базовые проектные оси

У каждого проекта должны быть 4 основные числовые оси:

#### 1. `totalWork` / `workDone`

Главная мера того, сколько усилия вообще нужно проекту.

- `totalWork` = полный объём работы проекта
- `workDone` = сколько уже вложено

Это аналог `totalTimeCost + progress`, но в терминах Хранителей.

#### 2. `totalStages` / `currentStage`

Качественные этапы проекта.

Нужны, чтобы проект не был просто линейной полоской.

Примеры стадий:

- `Gathering`
- `Testing`
- `Binding`
- `Negotiating`
- `Infiltrating`
- `Fortifying`
- `Revealing`

#### 3. `stability`

Шкала целостности проекта: `0..100`

Она показывает, насколько проект устойчив.

Это важно для:

- sabotage;
- collapse;
- risky grand projects;
- offensive counterplay.

#### 4. `pressure`

Шкала внешнего давления: `0..100`

Используется только если проект contested/offensive или в него уже вмешиваются rival стороны.

Она показывает не просто “мир опасен”, а именно накопленное вражеское давление на проект.

### Почему именно эти 4 оси

Они позволяют честно и валидируемо различать:

- обычный прогресс;
- помощь игрока;
- тяжёлый, но ещё живой проект;
- sabotage;
- collapse;
- abandon;
- offensive-проект против rival-Хранителя.

Без этих осей всё снова схлопнется в narrative `progressPercent`.

## Важное уточнение: проектам Хранителей нужны не только общие формулы, но и типовые рецепты

Если оставить только общую математику `work/stages/stability/pressure`, система останется слишком абстрактной.

Поэтому у каждого guardian project должен быть ещё и `projectType`, а у `projectType` — свой рецепт:

- что именно двигает этот проект;
- что считается обязательным условием;
- что считается completion;
- что считается sabotage/collapse;
- какой effect он даёт при успехе.

Примеры типов:

- `abode_expansion`
- `abode_fortification`
- `relic_forging`
- `lore_research`
- `soul_preparation`
- `offensive_intrigue`
- `counter_rival_operation`

Общая математика остаётся общей, но recipe делает её понятной.

## Разбор на конкретном примере: проект `Расширение Обители`

Это как раз тот пример, на котором лучше всего объяснять механику.

### Что делает проект

`abode_expansion` не даёт произвольный narrative reward.
Он прямо увеличивает `currentPower` Обители после завершения.

Рекомендуемый effect в v1:

- `minor abode_expansion` = `+4 currentPower`
- `major abode_expansion` = `+8 currentPower`
- `grand abode_expansion` = `+12 currentPower`

### От чего зависит его математика

Для `Расширения Обители` нужны 5 источников математики:

#### 1. Tier проекта

Это базовый масштаб:

- `minor` — локальное расширение
- `major` — заметное усиление
- `grand` — большой скачок Обители

#### 2. Текущий tier самой Обители

Чем сильнее Обитель уже сейчас, тем труднее её расширять дальше.
Это принципиально важно, чтобы не было snowballing без сопротивления.

Modifier к сложности:

- если Обитель `Угасающая`: `+0` к сложности
- `Хрупкая`: `+2`
- `Стабильная`: `+4`
- `Могущественная`: `+6`
- `Сияющая`: `+9`

#### 3. Mood/состояние Хранителя

Не как flavour, а как реальный modifier:

- `focused` = `+1 CycleWork`
- `energized` = `+1 CycleWork`
- `irritated` = `-1 CycleWork`
- `melancholic` = `-1 stability recovery`

#### 4. Помощь игрока

Игрок не “просто помогает текстом”, а даёт конкретный project outcome:

- `minor assist`
- `meaningful assist`
- `major breakthrough`

Для `abode_expansion` это может быть:

- принести якорь/реликт для расширения;
- помочь стабилизировать новую часть Обители;
- закрыть утечку хаоса;
- добыть domain-compatible essence.

#### 5. Вражеское давление

Если другой Хранитель хочет помешать расширению:

- растёт `pressure`
- падает `stability`
- completion может сорваться

### Конкретная формула для `abode_expansion`

Для этого типа проекта я бы считал так:

#### Шаг 1: считаем общий объём работы

`totalWork = BaseWorkByProjectTier + CurrentAbodeTierTax + ExpansionTypeTax`

Где:

- `BaseWorkByProjectTier`
  - `minor = 8`
  - `major = 14`
  - `grand = 22`

- `CurrentAbodeTierTax`
  - `Угасающая = 0`
  - `Хрупкая = 2`
  - `Стабильная = 4`
  - `Могущественная = 6`
  - `Сияющая = 9`

- `ExpansionTypeTax`
  - для обычного `abode_expansion = 2`

Пример:

У Хранителя сейчас `currentPower = 35`, то есть `Хрупкая` Обитель.
Он запускает `major abode_expansion`.

Тогда:

- `BaseWorkByProjectTier = 14`
- `CurrentAbodeTierTax = 2`
- `ExpansionTypeTax = 2`

Итого:

`totalWork = 18`

#### Шаг 2: задаём стадии

Для `major abode_expansion`:

- `totalStages = 3`

Стадии можно назвать так:

1. `Закрепление нового контура`
2. `Наполнение силы`
3. `Стабилизация расширения`

#### Шаг 3: стартовые параметры

- `workDone = 0`
- `currentStage = 0`
- `stability = 75`
- `pressure = 0`

Если проект offensive-contested:

- `pressure` может стартовать с `10..15`

### Как проект движется по ходам

Каждый Chaos Sea turn = один cycle.

Формула:

`CycleWork = 1 + MoodBonus + AssistanceBonus - ContestPenalty`

Где:

- базовая работа для `major abode_expansion` = `1`
- `MoodBonus`
  - `focused/energized = +1`
  - иначе `0`
- `AssistanceBonus`
  - `minor assist = +2`
  - `meaningful assist = +4`
  - `major breakthrough = +6`
  - если игрок не помогал в этот turn: `0`
- `ContestPenalty = floor(pressure / 25)`

Минимум:

- `CycleWork` не может быть меньше `0`

### Как двигаются стадии у `abode_expansion`

Порогами от `totalWork`:

- Stage 1: достигнут при `workDone >= 6`
- Stage 2: достигнут при `workDone >= 12`
- Stage 3: достигнут при `workDone >= 18`

Но есть важное условие:

Для перехода в финальную стадию нужно:

- `stability >= 40`

Это означает:

- можно “набить работу”,
- но если расширение нестабильно, финализировать его нельзя.

### Как влияет sabotage

Пример hostile interference:

- rival action даёт:
  - `pressure += 20`
  - `stability -= 12`
  - `workDone -= 2`

Это не просто flavour:

- проект реально замедляется,
- может не дойти до completion,
- может рухнуть.

### Когда `abode_expansion` завершён

Только если одновременно:

- `workDone >= totalWork`
- `currentStage >= totalStages`
- `stability >= 40`

Тогда:

- проект получает `finalState = Completed`
- Обитель получает power gain по tier:
  - `minor +4`
  - `major +8`
  - `grand +12`

### Когда проект считается сорванным

#### `Sabotaged`

Если:

- external enemy pressure выбил проект,
- или игрок помог rival sabotage,
- и итогом стало `stability <= 0`

Тогда:

- `finalState = Sabotaged`
- loss to abode power:
  - `minor = -4`
  - `major = -8`
  - `grand = -12`

#### `Collapsed`

Если:

- внешнего sabotage не было,
- но расширение само развалилось,
- например из-за слишком рискованной стабилизации

Тогда:

- `finalState = Collapsed`
- penalty softer than sabotage:
  - `minor = -3`
  - `major = -6`
  - `grand = -10`

#### `Abandoned`

Если:

- Хранитель сам остановил расширение

Тогда:

- `finalState = Abandoned`
- `0 / -1 / -2` по tier проекта

## Почему эта модель лучше

На примере `Расширения Обители` уже видно, что математика зависит не от “что GM почувствовал”, а от:

- типа проекта;
- tier проекта;
- текущего tier Обители;
- помощи игрока;
- настроения Хранителя;
- вражеского давления;
- устойчивости проекта.

То есть проект можно:

- ускорять,
- тормозить,
- защищать,
- срывать,
- завершать

по понятным правилам.

## Нормальная математическая модель для guardian projects

Ниже — более строгая версия, которую можно потом переносить в rules/validator.

## 1. Разделение на recipe и runtime state

У любого проекта Хранителя есть:

### Recipe

То, что задаётся типом проекта и tier’ом:

- `projectType`
- `projectTier`
- `baseWork`
- `baseStability`
- `exposure`
- `basePressurePerCycle`
- `baseRecoveryPerCycle`

### Runtime state

То, что меняется по ходам:

- `workDone`
- `pressure`
- `stability`
- `currentStage`
- `pressureSources[]` optional later

## 2. Обязательные числовые поля проекта

Минимальный v1 math contract:

```json
"currentProject": {
  "projectId": "guardian_project_x",
  "projectName": "Расширение Обители",
  "projectType": "abode_expansion",
  "projectTier": "minor | major | grand",
  "projectMode": "internal | supportive | offensive",
  "totalWork": 18,
  "workDone": 7,
  "totalStages": 3,
  "currentStage": 1,
  "pressure": 18,
  "stability": 67,
  "activeState": "Fortifying",
  "startedTurn": 80,
  "estimatedCompletionTurn": 95,
  "playerCanAssist": true,
  "assistDescription": "..."
}
```

## 3. Базовые recipe-константы

### Tier constants

#### `minor`

- `baseWork = 12`
- `baseStability = 80`
- `basePressurePerCycle = 1`
- `baseRecoveryPerCycle = 1`

#### `major`

- `baseWork = 18`
- `baseStability = 70`
- `basePressurePerCycle = 1`
- `baseRecoveryPerCycle = 1`

#### `grand`

- `baseWork = 26`
- `baseStability = 60`
- `basePressurePerCycle = 2`
- `baseRecoveryPerCycle = 1`

### Exposure by project type

`exposure` показывает, насколько проект уязвим к внешнему давлению.

- `abode_expansion = 2`
- `abode_fortification = 1`
- `relic_forging = 2`
- `lore_research = 3`
- `soul_preparation = 2`
- `offensive_intrigue = 4`
- `counter_rival_operation = 3`

Чем выше `exposure`, тем больнее sabotage и давление.

## 4. Стартовые значения runtime state

### Total work

`totalWork = baseWork + AbodeTierTax + ProjectTypeTax`

Где:

#### `AbodeTierTax`

- `Угасающая = 0`
- `Хрупкая = 2`
- `Стабильная = 4`
- `Могущественная = 6`
- `Сияющая = 9`

#### `ProjectTypeTax`

- `abode_expansion = 2`
- `abode_fortification = 3`
- `relic_forging = 2`
- `lore_research = 1`
- `soul_preparation = 2`
- `offensive_intrigue = 4`
- `counter_rival_operation = 3`

### Start stability

`startStability = baseStability + floor(currentAbodePower / 25) * 5`

То есть:

- power `0..24` → `+0`
- `25..49` → `+5`
- `50..74` → `+10`
- `75..99` → `+15`
- `100` → `+20`

Clamp:

- максимум `100`

### Start pressure

`startPressure = projectModeModifier + scriptedContestModifier`

Где:

- `internal = 0`
- `supportive = 5`
- `offensive = 15`

Обычно:

- обычный внутренний проект стартует с низким давлением
- offensive project сразу contested

## 5. Что такое pressure математически

`pressure` — это текущая интенсивность внешних помех.

Диапазон:

- `0..100`

### Pressure bands

- `0..9` — `спокойно`
- `10..24` — `низкое давление`
- `25..44` — `повышенное`
- `45..69` — `высокое`
- `70..100` — `критическое`

### Pressure update per cycle

`pressure_next = clamp(pressure + basePressurePerCycle + hostileInput - pressureRelief, 0, 100)`

Где:

#### `hostileInput`

Это суммарный вход внешнего давления за цикл:

- no interference = `0`
- `minor interference = 10`
- `major sabotage = 20`
- `grand strike = 30`

#### `pressureRelief`

`pressureRelief = 1 + floor(currentAbodePower / 30) + playerDefenseBonus`

То есть:

- power `0..29` → `+1`
- `30..59` → `+2`
- `60..89` → `+3`
- `90+` → `+4`

`playerDefenseBonus`:

- none = `0`
- minor protective help = `1`
- meaningful protection = `2`
- major defensive breakthrough = `3`

### Что pressure делает practically

Pressure:

- замедляет `workDone`
- увеличивает износ `stability`
- повышает шанс terminal failure при долгом накоплении

## 6. Что такое stability математически

`stability` — это запас прочности проекта.

Диапазон:

- `0..100`

### Stability bands

- `80..100` — проект очень устойчив
- `60..79` — устойчив
- `40..59` — шаткий, но ещё жизнеспособный
- `20..39` — опасная зона
- `1..19` — предаварийное состояние
- `0` — terminal failure

### Safe pressure threshold

Проект умеет терпеть ограниченное давление без износа.

`safePressure = 15 + floor(currentAbodePower / 20) * 5`

Итого:

- power `0..19` → `15`
- `20..39` → `20`
- `40..59` → `25`
- `60..79` → `30`
- `80..99` → `35`
- `100` → `40`

То есть сильная Обитель лучше выдерживает помехи.

### Stability wear from sustained pressure

Каждый цикл:

`stabilityWear = max(0, floor((pressure - safePressure) / 15))`

Примеры:

- pressure `18`, safePressure `20` → wear `0`
- pressure `38`, safePressure `20` → wear `1`
- pressure `55`, safePressure `20` → wear `2`

Это означает:

- небольшое давление терпимо
- высокое давление начинает реально точить проект

### Direct stability damage from hostile actions

Помимо износа, sabotage наносит прямой урон:

`directStabilityDamage = max(0, baseHitDamage + exposure - defenseRating)`

Где:

#### `baseHitDamage`

- `minor interference = 4`
- `major sabotage = 8`
- `grand strike = 12`

#### `defenseRating`

`defenseRating = floor(currentAbodePower / 25) + playerCounterplayBonus`

База:

- power `0..24` → `0`
- `25..49` → `1`
- `50..74` → `2`
- `75..99` → `3`
- `100` → `4`

`playerCounterplayBonus`:

- none = `0`
- minor = `1`
- meaningful = `2`
- major = `3`

### Stability recovery

Каждый цикл:

`stabilityRecovery = baseRecoveryPerCycle + playerRepairBonus + moodRecoveryBonus`

Где:

- `baseRecoveryPerCycle = recipe value`
- `playerRepairBonus`
  - none = `0`
  - minor = `1`
  - meaningful = `2`
  - major = `3`
- `moodRecoveryBonus`
  - `focused = 1`
  - `energized = 1`
  - иначе `0`

### Full stability update

`stability_next = clamp(stability - stabilityWear - directStabilityDamage + stabilityRecovery, 0, 100)`

Это и есть основная формула живучести проекта.

## 7. Как считается workDone

`CycleWork = BaseWorkByTier + MoodBonus + AssistanceBonus - ContestPenalty`

Где:

### `BaseWorkByTier`

- `minor = 2`
- `major = 1`
- `grand = 1`

### `MoodBonus`

- `focused = +1`
- `energized = +1`
- `irritated = -1`
- иначе `0`

### `AssistanceBonus`

- no assist = `0`
- `minor assist = +2`
- `meaningful assist = +4`
- `major breakthrough = +6`

### `ContestPenalty`

Зависит от pressure:

- pressure `0..9` → `0`
- `10..24` → `1`
- `25..44` → `2`
- `45..69` → `3`
- `70..100` → `4`

Минимум:

- `CycleWork` не может быть меньше `0`

### Work update

`workDone_next = clamp(workDone + CycleWork - directWorkLoss, 0, totalWork)`

Где:

- `directWorkLoss`
  - none = `0`
  - `major sabotage = 2`
  - `grand strike = 4`

## 8. Как проект переходит между стадиями

Стадии двигаются threshold’ами от `totalWork`.

Для:

- `2 stages`
  - stage 1 at `50%`
  - final at `100%`
- `3 stages`
  - stage 1 at `33%`
  - stage 2 at `66%`
  - final at `100%`
- `4 stages`
  - stage 1 at `25%`
  - stage 2 at `50%`
  - stage 3 at `75%`
  - final at `100%`

Но финальная стадия разрешена только если:

- `stability >= 40`

Иначе проект “почти готов”, но не может быть safely finalized.

## 9. Как строго определять completion / sabotage / collapse / abandon

### `Completed`

Только если одновременно:

- `workDone >= totalWork`
- `currentStage >= totalStages`
- `stability >= 40`

### `Sabotaged`

Если:

- был внешний hostile action
- и `stability <= 0`

или

- проект явно завершён terminal outcome с враждебным срывом

### `Collapsed`

Если:

- внешнего hostile action в этом cycle-resolution нет
- но `stability <= 0`

То есть проект рухнул сам.

### `Abandoned`

Если:

- Хранитель сам прекращает проект до terminal failure

## 10. Почему проект потерял стабильность: обязательный audit trail

Чтобы это не выглядело как произвол GM, у terminal/non-terminal update должен быть разбор причин.

Пример:

```json
"stabilityAudit": {
  "previous": 75,
  "pressureWear": 1,
  "directDamage": 9,
  "recovery": 2,
  "newValue": 67,
  "reason": "Major sabotage by rival guardian project plus minor stabilization by the Soul"
}
```

То же самое можно делать для pressure:

```json
"pressureAudit": {
  "previous": 0,
  "basePressure": 1,
  "hostileInput": 20,
  "relief": 3,
  "newValue": 18
}
```

Это нужно не обязательно сразу светить игроку в raw виде, но это должен понимать validator и от этого должен работать `repairHint`.

## 11. Что это даёт системе Силы Обители

Теперь change `Силы Обители` можно привязывать не к vague narration, а к terminal outcome проекта:

- `Completed`
  - даёт power gain по tier/type
- `Sabotaged`
  - даёт power loss по tier/type
- `Collapsed`
  - даёт меньший loss
- `Abandoned`
  - даёт `0` или мягкий loss

То есть:

- сначала честно считается проект;
- потом проект меняет `Силу Обители`;
- а уже `Сила Обители` влияет на торговлю, квесты, gacha, rival arcs и корректировки следующей жизни.

## 12. Набор типовых recipe для проектов Хранителей

Ниже — стартовый пакет проектов, на котором можно строить v1-v2.
Идея: GM не изобретает математику с нуля, а выбирает тип проекта и потом уже пишет theme/flavor.

## 12.1 `abode_expansion`

### Смысл

Хранитель расширяет границы Обители, создаёт новую часть домена, укрепляет её присутствие в Море Хаоса.

### Базовый профиль

- `projectMode = internal`
- `exposure = 2`
- `ProjectTypeTax = 2`

### Типичные стадии

- `Surveying the new contour`
- `Binding new space`
- `Stabilizing the expansion`

### Что двигает проект

- стабильная работа Хранителя
- помощь игрока с якорями, сущностью, стабилизацией
- дружественная доменная подпитка

### Что чаще всего ему мешает

- rival pressure
- нестабильность хаотической материи
- сорванная стабилизация

### Эффект при `Completed`

- `minor = +4 abode power`
- `major = +8 abode power`
- `grand = +12 abode power`

### Эффект при провале

- `Abandoned = 0 / -1 / -2`
- `Collapsed = -3 / -6 / -10`
- `Sabotaged = -4 / -8 / -12`

## 12.2 `abode_fortification`

### Смысл

Хранитель не расширяет Обитель, а делает её более устойчивой к rival influence, sabotage и враждебным нитям судьбы.

### Базовый профиль

- `projectMode = internal`
- `exposure = 1`
- `ProjectTypeTax = 3`

### Типичные стадии

- `Identifying weak seams`
- `Layering wards and anchors`
- `Final sealing`

### Что двигает проект

- defensive rituals
- редкие материалы/структуры защиты
- помощь игрока в закрытии слабых мест

### Что чаще всего ему мешает

- прямое sabotage со стороны rival guardians
- неудачная настройка ward-сетки

### Эффект при `Completed`

На жизнь/до следующего terminal state у этой Обители:

- `minor = +5 safePressure`
- `major = +10 safePressure`
- `grand = +15 safePressure`

Дополнительно:

- `minor = +1 defenseRating cap against hostile actions`
- `major = +2`
- `grand = +3`

Это не прямой прирост `currentPower`, а defensive multiplier.

### Эффект при провале

- `Abandoned = 0`
- `Collapsed = -2 / -4 / -6 abode power`
- `Sabotaged = apply temporaryProjectPenalty(nextInternalProjectStartingPressure = +10, applications = 1)`

### Temporary modifier contract

Временные проектные штрафы/бонусы не должны жить в тексте.
Они должны храниться как отдельный canonical effect.

Пример:

```json
"abodePowerEffects": {
  "temporaryProjectModifiers": [
    {
      "modifierId": "fort_fail_x",
      "modifierType": "next_internal_project_starting_pressure",
      "value": 10,
      "remainingApplications": 1,
      "sourceProjectId": "guardian_project_x"
    }
  ]
}
```

Правило:

- применяется к следующему matching project start
- после применения `remainingApplications -= 1`
- при `remainingApplications = 0` modifier удаляется

## 12.3 `relic_forging`

### Смысл

Хранитель создаёт, улучшает или стабилизирует доменный реликт/предмет для торговли, награды или gacha-пула.

### Базовый профиль

- `projectMode = supportive`
- `exposure = 2`
- `ProjectTypeTax = 2`

### Типичные стадии

- `Gathering components`
- `Forging / weaving / inscribing`
- `Tempering and final binding`

### Что двигает проект

- материалы
- доменная энергия
- помощь игрока редкими компонентами

### Что чаще всего ему мешает

- дефекты ковки
- нехватка редких элементов
- sabotage during binding

### Эффект при `Completed`

До следующего refresh trade/gacha cycle:

- `minor = +10% chance for upgraded trade slot`
- `major = +20% chance for upgraded trade slot and +1 rarity-quality ceiling step for one cycle`
- `grand = +35% chance for upgraded trade slot, +1 rarity-quality ceiling step and 1 guaranteed elevated slot for one cycle`

### Эффект при провале

- `Collapsed = no item created; -1 / -3 / -5 abode power`
- `Sabotaged = spoiled forging, -2 / -4 / -6 abode power`

## 12.4 `lore_research`

### Смысл

Хранитель исследует тайну, разлом, архив, доменный принцип или космический факт.

### Базовый профиль

- `projectMode = supportive`
- `exposure = 3`
- `ProjectTypeTax = 1`

### Типичные стадии

- `Collecting traces`
- `Interpreting patterns`
- `Breaking the seal of meaning`

### Что двигает проект

- информация
- образцы
- рассказы души
- следы из смертных миров

### Что чаще всего ему мешает

- ложные следы
- rival information theft
- unstable anomaly

### Эффект при `Completed`

- `minor = unlock 1 bonus lore fragment and +1 visible rival clue budget this life`
- `major = unlock 1 stronger lore fragment, +1 quest hook and +2 visible rival clue budget this life`
- `grand = unlock 1 major secret, +1 special quest line and +3 visible rival clue budget this life`

### Canonical side effects

- `minor`
  - `bonusLoreUnlocks = 1`
  - `questHookCount = 0`
  - `specialQuestLineUnlocks = 0`
- `major`
  - `bonusLoreUnlocks = 1`
  - `questHookCount = 1`
  - `specialQuestLineUnlocks = 0`
- `grand`
  - `bonusLoreUnlocks = 1`
  - `questHookCount = 1`
  - `specialQuestLineUnlocks = 1`

Эти side effects должны материализоваться не “по желанию GM”, а как derived consequences recipe-completion.

### Эффект при провале

- `Collapsed = ложная теория, 0 gain`
- `Sabotaged = knowledge leak to rival guardian, -3 / -6 / -8 abode power`

## 12.5 `soul_preparation`

### Смысл

Хранитель готовит для души следующий цикл воплощения: не переписывает сценарное ядро, а накапливает бюджет будущих корректировок.

### Базовый профиль

- `projectMode = supportive`
- `exposure = 2`
- `ProjectTypeTax = 2`

### Типичные стадии

- `Reading scenario openings`
- `Binding compatible adjustments`
- `Preparing correction channels`

### Что двигает проект

- доверие между душой и Хранителем
- ясность world setup
- наличие свободных “окон” в сценарии
- помощь игрока через диалог/выборы/согласованные условия

### Что чаще всего ему мешает

- враждебные rival corrections
- нестыкуемость со сценарием
- sabotage чужого Хранителя

### Эффект при `Completed`

Не даёт immediate power.
Он создаёт временный буфер для `Корректив Хранителя` в следующей жизни:

- `minor = +1 preparation budget point`
- `major = +2 preparation budget points`
- `grand = +3 preparation budget points`

Conversion rule:

- `1 point = 1 minor correction uplift`
- `2 points = 1 medium uplift`
- `3 points = 1 strong uplift`

Это должно суммироваться с обычным budget от силы Обители, но отдельно логироваться в `/коррективы_хранителя`.

### Эффект при провале

- `Abandoned = 0`
- `Collapsed = следующая жизнь без бонусного correction budget`
- `Sabotaged = hostile side gains 1 priority token for correction claim resolution`

## 12.6 `offensive_intrigue`

### Смысл

Политический/магический проект, направленный прямо против rival-Хранителя.

### Базовый профиль

- `projectMode = offensive`
- `exposure = 4`
- `ProjectTypeTax = 4`
- `targetGuardianId` обязателен

### Типичные стадии

- `Planting influence`
- `Undermining the rival's hold`
- `Triggering the decisive breach`

### Что двигает проект

- интриги
- leaked secrets
- чужие нити судьбы
- агентурное влияние
- помощь игрока в rival conflict

### Что чаще всего ему мешает

- защитные действия цели
- counter-project
- разоблачение интриги

### Эффект при `Completed`

Атакующий:

- получает обычный power gain по tier:
  - `minor +4`
  - `major +8`
  - `grand +12`

Цель:

- теряет силу по canonical formula из раздела `Revised offensive impact formula`

### Эффект при провале

- `Abandoned = -1 / -2 / -3 to attacker`
- `Collapsed = backlash, -3 / -5 / -7 to attacker`
- `Sabotaged = counterstroke, target inflicts mirrored loss 1 / 2 / 3 to attacker`

## 12.7 `counter_rival_operation`

### Смысл

Защитный ответ на уже начатую враждебную операцию rival-Хранителя.

### Базовый профиль

- `projectMode = supportive`
- `exposure = 3`
- `ProjectTypeTax = 3`
- `targetGuardianId` обязателен

### Типичные стадии

- `Tracing the hostile thread`
- `Blocking entry points`
- `Severing the rival pressure`

### Что двигает проект

- информация о rival operation
- помощь игрока
- defensive rituals
- successful counterplay against rival arc

### Что чаще всего ему мешает

- lack of evidence
- too-high current pressure
- simultaneous second offensive project

### Эффект при `Completed`

- immediate `pressure -= 15 / 25 / 35` for target project family or active hostile pressure pool
- `stability += 5 / 10 / 15` to defended project if still active
- `+1 / +2 / +3 abode power`

### Эффект при провале

- no defensive relief
- if `Sabotaged`, hostile side gains additional `+10 pressure`

## 13. Как использовать recipes на практике

Алгоритм для GM/системы должен быть простым:

1. Выбрать `projectType`
2. Выбрать `projectTier`
3. Сгенерировать theme/flavor поверх recipe
4. Посчитать:
   - `totalWork`
   - `startStability`
   - `startPressure`
   - `totalStages`
5. Дальше каждый cycle применять общие формулы
6. На terminal outcome выдавать строго определённый effect

То есть flavour остаётся живым, но математика у типа проекта уже готова заранее.

## Единый источник истины для project math

Если в документе встречаются:

- общие fallback-константы;
- формулы recipe-конкретного проекта;
- пример на частном проекте;

приоритет такой:

1. `projectType recipe`
2. общие math-формулы cycle/update
3. fallback-константы по tier
4. narrative example

То есть:

- recipe-specific числа имеют приоритет над общими fallback-числами;
- пример не может переопределять формулу;
- completion rule везде один и тот же, если recipe явно не говорит обратного.

## Canonical per-cycle order of operations

Чтобы одинаковый state не обсчитывался по-разному, для каждого `Guardian Project Cycle` нужен жёсткий порядок.

Порядок на один цикл:

1. Прочитать предыдущий runtime state проекта
2. Собрать все входы текущего цикла:
   - passive base pressure
   - hostile actions
   - player assist
   - player defense / repair
   - mood modifiers
3. Посчитать classification scores:
   - assist score
   - defense score
   - sabotage severity score
4. Обновить `pressure`
5. Посчитать `directStabilityDamage` и `directWorkLoss`
6. Посчитать `stabilityWear`
7. Посчитать `stabilityRecovery`
8. Обновить `stability`
9. Посчитать `CycleWork`
10. Обновить `workDone`
11. Пересчитать `currentStage`
12. Проверить terminal conditions:
   - `Completed`
   - `Sabotaged`
   - `Collapsed`
   - `Abandoned`
13. Если terminal outcome есть:
   - применить delta к `abodePower`
   - записать history/audit
14. Если terminal outcome нет:
   - сохранить updated currentProject state

Completion check происходит после full update, не раньше.

## Scoring-контракты: как уменьшить произвол GM

Ниже — шкалы, которые превращают “вкус GM” в повторяемый score.

## 1. Assist Score

Каждая помощь игрока проекту оценивается по 4 осям, каждая `0..2`.

### Assist axes

- `DomainRelevance`
  - `0` не связано с доменом проекта
  - `1` косвенно полезно
  - `2` прямо бьёт в нужду проекта
- `RiskOrCost`
  - `0` почти бесплатно
  - `1` умеренный риск/расход
  - `2` заметная жертва, цена или опасность
- `ScarcityOrUniqueness`
  - `0` обычный вклад
  - `1` редкий вклад
  - `2` очень редкий / трудно заменимый вклад
- `DirectProjectImpact`
  - `0` только flavour support
  - `1` реально помогает
  - `2` напрямую двигает текущую bottleneck-стадию

### Formula

`assistScore = DomainRelevance + RiskOrCost + ScarcityOrUniqueness + DirectProjectImpact`

### Classification

- `0..2` = not qualified as project assist
- `3..4` = `minor assist`
- `5..6` = `meaningful assist`
- `7..8` = `major breakthrough`

## 2. Defense / Repair Score

Когда игрок защищает или чинит проект, используется такая же схема `0..8`.

Оси:

- `ThreatAddressed`
- `Timeliness`
- `RepairDepth`
- `CostOrRisk`

Classification:

- `0..2` = no meaningful defense
- `3..4` = `minor defensive help`
- `5..6` = `meaningful protection`
- `7..8` = `major defensive breakthrough`

## 3. Offering Score

Для подношений в Обитель:

Оси `0..2`:

- `DomainMatch`
- `Rarity`
- `PlayerSacrifice`
- `CurrentNeedFit`

`offeringScore = sum(axes)`

Classification:

- `0..2` = no power gain
- `3..4` = `+1 power`
- `5..6` = `+2 power`
- `7..8` = `+4 power`

Это заменяет расплывчатое `+2..+4 на глаз`.

## 4. Resonance Score

Для итогового резонанса смертной жизни:

Оси `0..2`:

- `DomainAlignment`
- `WorldScale`
- `Permanence`
- `Sacrifice`
- `PublicImpact`

`resonanceScore = sum(axes)` with range `0..10`

Classification:

- `0..2` = no resonance gain
- `3..4` = `weak resonance = +2 power`
- `5..7` = `solid resonance = +4 power`
- `8..10` = `major resonance = +6 power`

## 5. Sabotage Severity Score

Чтобы sabotage не определялся “по ощущению”, нужен score:

Оси `0..2`:

- `HostileReach`
- `ProjectExposure`
- `DamageIntent`
- `DamageAchieved`
- `PlayerComplicity`

`sabotageSeverityScore = sum(axes)` with range `0..10`

Classification:

- `0..2` = nuisance / no formal sabotage tier
- `3..4` = `minor interference`
- `5..7` = `major sabotage`
- `8..10` = `grand strike`

## Machine contract for Scenario Core

Чтобы `Коррективы Хранителя` не зависели только от вкуса GM, нужно формализовать `Scenario Core`.

## 1. Scenario Core Manifest

После принятия player-authored next-life setup должен существовать machine-readable manifest:

- `game_state/control/next_life_scenario_core.json`

Структура:

```json
{
  "candidateAssertions": [
    {
      "candidateId": "cand_world_condition_1",
      "source": "structured_field | confirmed_manual | extracted_freeform",
      "text": "Королевство процветает",
      "category": "world_condition",
      "confidence": 0.92
    }
  ],
  "scenarioCoreAssertions": [
    {
      "assertionId": "core_role_king",
      "category": "role_status",
      "value": "Игрок начинает королём",
      "explicit": true,
      "source": "structured_field | confirmed_manual | extracted_freeform_confirmed"
    }
  ],
  "openCorrectionSlots": [
    {
      "slotId": "slot_political_hidden_layer",
      "slotType": "political_hidden_layer",
      "maxSeverity": "strong",
      "allowsFriendly": true,
      "allowsHostile": true
    }
  ]
}
```

Rule:

- GM и validator связаны только `scenarioCoreAssertions`
- `candidateAssertions` не считаются жёстким ядром, пока не подтверждены или не пришли из явно структурированного поля

## 2. Assertion categories

Минимальные категории для v1:

- `role_status`
- `start_location`
- `world_premise`
- `world_condition`
- `starting_resources`
- `starting_relationship`
- `identity_anchor`

Все explicit player facts должны быть разложены по этим assertion’ам.

## 2.1 Deterministic extraction rule

При разборе player-authored setup в `Scenario Core Manifest` действует простое правило:

- если факт пришёл из структурированного поля старта, он автоматически становится `scenarioCoreAssertion`
- если факт вытащен только из большого freeform-описания, он сначала попадает в `candidateAssertions`
- candidate из freeform становится `scenarioCoreAssertion` только после явного подтверждения игроком
- если фраза описывает не стартовый факт, а пожелательный тон, мотив или атмосферу, она не становится hard assertion автоматически

Hard assertion examples:

- `Я начинаю королём`
- `Стартую во дворце`
- `Моё королевство процветает`
- `У меня есть верный советник`

Not automatically hard assertions:

- `Хочу красивую драму`
- `Пусть мир будет насыщенным`
- `Хотелось бы тайн`

Если формулировка пограничная, приоритет у более узкого, явно стартового факта.

## 2.2 Confirmation step

Перед применением `Корректив Хранителя` и стартом новой жизни игрок видит review:

- `Подтверждённое ядро сценария`
- `Извлечённые, но не подтверждённые факты`

Только подтверждённое ядро запрещено ломать.

## 3. Correction slots

Коррективы Хранителя нельзя применять “в пустоту”. Каждая корректива должна быть привязана к `openCorrectionSlot`.

Стартовый whitelist slot types:

- `political_hidden_layer`
- `social_hidden_layer`
- `occult_hidden_layer`
- `resource_complication`
- `resource_blessing`
- `ally_thread`
- `rival_thread`
- `debt_or_oath`
- `protection_or_omen`

## 3.1 Deterministic slot generation table

Каждая assertion category автоматически порождает разрешённые типы correction slots.

### `role_status`

Порождает:

- `ally_thread`
- `rival_thread`
- `debt_or_oath`
- `protection_or_omen`

### `start_location`

Порождает:

- `social_hidden_layer`
- `occult_hidden_layer`
- `resource_complication`
- `protection_or_omen`

### `world_premise`

Порождает:

- `occult_hidden_layer`
- `rival_thread`
- `ally_thread`

### `world_condition`

Порождает:

- `political_hidden_layer`
- `social_hidden_layer`
- `occult_hidden_layer`
- `rival_thread`
- `protection_or_omen`

### `starting_resources`

Порождает:

- `resource_complication`
- `resource_blessing`
- `debt_or_oath`

### `starting_relationship`

Порождает:

- `ally_thread`
- `rival_thread`
- `debt_or_oath`

### `identity_anchor`

Порождает:

- `protection_or_omen`
- `rival_thread`

## 3.2 Slot validity examples

Если assertion:

- `Игрок — король`

то допустимы:

- rival claimant
- hidden court faction
- oath debt
- secret protector

но недопустимы:

- `Игрок не король`
- `Игрок начинает нищим беглецом`

Если assertion:

- `Королевство процветает`

то допустимы:

- скрытый заговор
- тайная rival line
- отложенная угроза
- незримое благословение

но недопустимы:

- `королевство уже разрушено`
- `в стране повальный голод с первого кадра`, если это ломает explicit prosperity fact

## 4. Compatibility rule

Корректива валидна только если одновременно:

- attached to existing `openCorrectionSlot`
- severity <= `slot.maxSeverity`
- не отрицает ни один `scenarioCoreAssertion`
- не дублирует несовместимую уже applied correction

Это и есть machine barrier против “GM просто решил сломать старт”.

## 4.1 Correction claim resolution

Friendly и hostile корректировки не должны конфликтовать по неявному вкусу GM.
Для этого нужен детерминированный алгоритм захвата correction slots.

### Candidate correction object

```json
{
  "candidateCorrectionId": "corr_x",
  "sourceGuardianId": "guardian_a",
  "intent": "friendly | hostile",
  "slotId": "slot_political_hidden_layer",
  "severity": "minor | medium | strong",
  "powerCost": 12,
  "claimStrength": 0,
  "budgetSource": "base_abode_power | soul_preparation_bonus"
}
```

### Claim strength formula

`claimStrength = PowerBand + SeverityWeight + PreparationBonus + IntentModifier`

Где:

- `PowerBand`
  - `0..19 = 0`
  - `20..39 = 1`
  - `40..59 = 2`
  - `60..79 = 3`
  - `80..100 = 4`
- `SeverityWeight`
  - `minor = 1`
  - `medium = 2`
  - `strong = 3`
- `PreparationBonus`
  - none = `0`
  - completed `soul_preparation` bonus token = `1`
  - hostile priority token from sabotaged `soul_preparation` = `2`
- `IntentModifier`
  - default `0`
  - active patron guardian = `+1`

### Resolution order

1. Собрать все candidate corrections, которые укладываются в budget guardians
2. Посчитать `claimStrength`
3. Сгруппировать по `slotId`
4. Для каждого slot оставить кандидата с максимальным `claimStrength`
5. При равенстве:
   - побеждает active patron guardian
6. Если равенство осталось:
   - побеждает higher currentPower
7. Если равенство всё ещё осталось:
   - lexical guardianId tie-break
8. После выбора winners списать `powerCost`
9. Если guardian overspent из-за нескольких winning slots, prune его weakest winner до возврата в budget

Это делает correction-layer детерминированным.

## Revised offensive impact formula

Ранняя грубая формула `BaseLoss + AttackerTierBonus` недостаточна.
Итоговый урон по rival-Обители должен зависеть и от защиты цели.

`TargetLoss = max(0, BaseLossByTier + AttackerBonus - TargetShield)`

### `BaseLossByTier`

- `minor = 3`
- `major = 6`
- `grand = 10`

### `AttackerBonus`

`AttackerBonus = floor(attackerCurrentPower / 25)`

То есть `0..4`.

### `TargetShield`

`TargetShield = BaseTargetShield + FortificationBonus + CounterOperationBonus + PlayerDefenseBonus`

Где:

- `BaseTargetShield = floor(targetCurrentPower / 30)` giving `0..3`
- `FortificationBonus`
  - none = `0`
  - minor active fortification effect = `1`
  - major = `2`
  - grand = `3`
- `CounterOperationBonus`
  - none = `0`
  - minor = `1`
  - major = `2`
  - grand = `3`
- `PlayerDefenseBonus`
  - none = `0`
  - meaningful defense this cycle = `1`
  - major defense = `2`

Это делает offensive politics двусторонней, а не только “атакующий всегда прав”.

## Какие atomic commands нужны Хранителям

Guardian projects больше не должны жить как сложная вложенная структура внутри общего `UpdateGuardians`.
Для них нужен отдельный top-level command surface, как у faction projects.

Canonical command set:

- `startGuardianProjects`
  - явное создание нового активного проекта Хранителя
- `guardianProjectUpdates`
  - только нетерминальные изменения проекта
- `completeGuardianProjects`
  - перевод проекта в terminal state

Rule:

- `UpdateGuardians` больше не является source-of-truth surface для project tracker logic
- guardian project lifecycle обслуживается только этими отдельными командами
- `UpdateGuardians` может содержать только облегчённые guardian-side эффекты, которые не являются самим project tracker

Почему:

- project tracker уже слишком сложен для generic nested sub-command
- нужен отдельный validator layer
- нужен отдельный журнал для UI
- нужен отдельный repair path без смешения с обычными guardian updates

## JSON payload contract для guardian project commands

### `startGuardianProjects`

```json
{
  "guardianId": "guardian_x",
  "project": {
    "projectId": "guardian_project_x",
    "projectType": "abode_expansion",
    "projectTier": "major",
    "projectMode": "internal",
    "projectName": "Расширение Обители",
    "activeState": "Surveying",
    "totalWork": 18,
    "workDone": 0,
    "totalStages": 3,
    "currentStage": 0,
    "pressure": 0,
    "stability": 75,
    "startedTurn": 80,
    "estimatedCompletionTurn": 95,
    "playerCanAssist": true,
    "assistDescription": "..."
  }
}
```

### `guardianProjectUpdates`

```json
{
  "guardianId": "guardian_x",
  "projectId": "guardian_project_x",
  "activeState": "Binding",
  "workDone": 8,
  "currentStage": 1,
  "pressure": 18,
  "stability": 67,
  "pressureAudit": { "...": "..." },
  "stabilityAudit": { "...": "..." },
  "workAudit": { "...": "..." }
}
```

### `completeGuardianProjects`

```json
{
  "guardianId": "guardian_x",
  "projectId": "guardian_project_x",
  "finalState": "Completed | Abandoned | Sabotaged | Collapsed",
  "outcome": "Краткий итог",
  "abodePowerDelta": 8,
  "targetGuardianId": null,
  "offensiveImpactAudit": null,
  "pressureAudit": { "...": "..." },
  "stabilityAudit": { "...": "..." },
  "workAudit": { "...": "..." }
}
```

## Canonical storage for guardian projects

Guardian project state should not rely on incidental embedding inside the full guardian object.
Нужен отдельный canonical tracker file:

- `game_state/meta/guardian_projects.json`

Recommended structure:

```json
{
  "activeProjects": [
    {
      "guardianId": "guardian_x",
      "project": { "...": "..." }
    }
  ],
  "completedProjects": [
    {
      "guardianId": "guardian_x",
      "project": { "...": "..." }
    }
  ],
  "temporaryProjectModifiers": [
    {
      "guardianId": "guardian_x",
      "modifierId": "fort_fail_x",
      "modifierType": "next_internal_project_starting_pressure",
      "value": 10,
      "remainingApplications": 1
    }
  ]
}
```

Minimal guardian object may still carry a lightweight summary/reference for convenience in `/хранители`,
but authoritative project math and history live in `guardian_projects.json`.

## Detailed journal for player display

Поскольку guardian projects теперь полноценная системная поверхность, им нужен отдельный journal file:

- `game_state/meta/guardian_project_journal.json`

Purpose:

- detailed player-facing history
- readable chronology
- visible project milestones
- sabotage / repair / completion explanations
- no need to reconstruct the full story from raw tracker diffs

Recommended structure:

```json
{
  "entries": [
    {
      "entryId": "gpj_x",
      "turn": 84,
      "guardianId": "guardian_x",
      "projectId": "guardian_project_x",
      "eventType": "started | progressed | assisted | pressured | sabotaged | stabilized | completed | collapsed | abandoned",
      "visibility": "public | guardian_only | player_known",
      "title": "Расширение Обители продвинулось",
      "summary": "Новый контур закреплён, но rival pressure возросло.",
      "details": [
        "Стадия: Наполнение силы",
        "Pressure: 8 -> 18",
        "Stability: 76 -> 67",
        "Work: 6 -> 8"
      ]
    }
  ]
}
```

Journal rules:

- every `guardianProjectUpdates` producing meaningful visible change should append a journal entry
- every terminal outcome must append a journal entry
- entries can be filtered by `visibility`
- raw audits remain canonical math support, while journal is for readable UX

## Player-facing UI requirement

Нужна отдельная player-facing поверхность:

- `/проекты_хранителей`

Она должна читать `guardian_project_journal.json` и `guardian_projects.json` и показывать:

- текущие активные проекты по Хранителям
- текущую стадию
- pressure / stability / work summaries
- историю заметных проявлений
- завершённые/сорванные проекты
- visible связь проекта с `Силой Обители`

Иными словами:

- tracker file = машинная истина
- journal file = удобное представление для игрока
- `/хранители` может показывать краткое summary
- `/проекты_хранителей` показывает подробный журнал

## Audit payloads for scoring

Чтобы classification scores не оставались “в голове GM”, каждая соответствующая операция должна уметь нести audit.

### `assistAudit`

```json
{
  "DomainRelevance": 2,
  "RiskOrCost": 1,
  "ScarcityOrUniqueness": 1,
  "DirectProjectImpact": 2,
  "assistScore": 6,
  "classification": "meaningful assist"
}
```

### `offeringAudit`

```json
{
  "DomainMatch": 2,
  "Rarity": 1,
  "PlayerSacrifice": 2,
  "CurrentNeedFit": 1,
  "offeringScore": 6,
  "powerGain": 2
}
```

### `resonanceAudit`

```json
{
  "DomainAlignment": 2,
  "WorldScale": 2,
  "Permanence": 1,
  "Sacrifice": 1,
  "PublicImpact": 2,
  "resonanceScore": 8,
  "classification": "major resonance"
}
```

### `sabotageAudit`

```json
{
  "HostileReach": 2,
  "ProjectExposure": 2,
  "DamageIntent": 2,
  "DamageAchieved": 1,
  "PlayerComplicity": 0,
  "sabotageSeverityScore": 7,
  "classification": "major sabotage"
}
```

## Derived эффекты, которые должен считать клиент

Хранить нужно прежде всего `currentPower` и историю.
Остальные эффекты лучше считать client-side как derived fields.

Обязательные derived rules v1:

- `tradeSlotCount`
- `guardianQuestCap`
- `bonusGachaCharges`
- `nextLifeCorrectionBudget`
- `rivalArcDefenseClues`
- `rivalArcOffenseCap`
- `rivalArcClarityTier`
- `rivalArcCounterQuestAccess`
- `rivalArcWarningTier`
- `guardianRarityCeilingBonusSteps`

Жёсткие piecewise rules:

- `tradeSlotCount`
  - `0..19 = 4`
  - `20..39 = 5`
  - `40..59 = 6`
  - `60..79 = 7`
  - `80..100 = 8`
- `guardianQuestCap`
  - `0..19 = 2`
  - `20..59 = 3`
  - `60..79 = 4`
  - `80..100 = 5`
- `bonusGachaCharges`
  - `0..39 = 0`
  - `40..79 = 1`
  - `80..100 = 2`
- `guardianRarityCeilingBonusSteps`
  - `0..59 = 0`
  - `60..100 = 1`
- `nextLifeCorrectionBudget`
  - `0..19 = 0`
  - `20..39 = 1 minor`
  - `40..59 = 2 minor or 1 medium`
  - `60..79 = 3 minor or 2 medium`
  - `80..100 = 4 minor or 2 medium + 1 strong`
- `rivalArcDefenseClues`
  - `0..19 = 0`
  - `20..39 = 1`
  - `40..59 = 1`
  - `60..79 = 2`
  - `80..100 = 3`
- `rivalArcOffenseCap`
  - `0..19 = no formal hostile arc sponsorship`
  - `20..39 = background pressure only, no direct-target hostile arc`
  - `40..59 = one minor hostile arc`
  - `60..79 = one major hostile arc or one direct-target minor arc`
  - `80..100 = one major hostile arc with early-signal privilege`
- `rivalArcClarityTier`
  - `0..39 = 0`
  - `40..59 = 1`
  - `60..79 = 2`
  - `80..100 = 3`
- `rivalArcCounterQuestAccess`
  - `0..59 = false`
  - `60..100 = true`
- `rivalArcWarningTier`
  - `0..79 = 0`
  - `80..100 = 1`

В дальнейшем можно добавить:

- `maxProjectTier`
- `projectAssistMultiplier`
- `guardianServiceTier`

## Deterministic RNG rule for probabilistic effects

Если derived-эффект использует вероятность, она не должна зависеть от “настроения GM”.

Правило:

- все probabilistic guardian-derived effects используют deterministic client-side RNG
- seed строится из стабильных canonical значений соответствующего цикла

Базовый seed pattern:

`guardianId + tradeCycleId + projectId + currentPowerBand`

Это относится как минимум к:

- upgraded trade slot chance from `relic_forging`
- elevated stock generation
- future similar abode-power probabilistic effects

Таким образом, вероятность остаётся математической, а не narrative whim.

## Предлагаемый state contract

В guardian object:

```json
"abodePower": {
  "currentPower": 35,
  "tier": "Хрупкая",
  "lastUpdatedAt": "ISO",
  "history": [
    {
      "timestamp": "ISO",
      "change": 4,
      "reason": "Завершён normal quest, усиливший текущий проект",
      "source": "guardian_quest_complete",
      "relatedQuestId": "quest_x",
      "relatedProjectId": "project_y",
      "relatedGuardianId": null
    }
  ]
}
```

Отдельно хранить:

- `game_state/world/guardian_corrections.json`
- `game_state/meta/guardian_projects.json`
- `game_state/meta/guardian_project_journal.json`

Где:

- `guardian_corrections.json` хранит applied corrections текущей жизни
- `guardian_projects.json` хранит authoritative tracker проектов
- `guardian_project_journal.json` хранит player-facing подробный журнал

## Этапы внедрения

### Phase 1: состояние и математика

- добавить `abodePower` в guardian contract;
- добавить историю силы;
- добавить client-side derived helpers;
- добавить validator rules для power bounds и history shape.

### Phase 2: переработка проектов Хранителей в полноценный tracker

- усилить `currentProject` до полноценного project tracker;
- вынести tracker в отдельный canonical file `guardian_projects.json`;
- добавить отдельные top-level commands:
  - `startGuardianProjects`
  - `guardianProjectUpdates`
  - `completeGuardianProjects`
- добавить `projectTier`, `projectMode`, `activeState`, `totalWork`, `workDone`, `totalSteps`, `currentStep`, `stakes`;
- добавить terminal outcomes: `Completed | Abandoned | Sabotaged | Collapsed`;
- ввести отдельный guardian project lifecycle и атомарные project commands;
- добавить `guardian_project_journal.json` и `/проекты_хранителей`;
- зафиксировать, как assist, completion и sabotage конвертируются в изменение Силы Обители.

### Phase 3: базовые источники роста/падения

- квесты Хранителя;
- помощь и провал `currentProject`;
- завершение проекта;
- offensive project impact;
- расход силы на коррективы следующей жизни.

### Phase 4: реальные эффекты для игрока

- торговые слоты > 4;
- quest cap;
- gacha bonus charges;
- rival arc defense/offense derived rules.

### Phase 5: Коррективы Хранителя

- ввести `guardian_corrections.json`;
- добавить меню `/коррективы_хранителя`;
- зафиксировать правила `Scenario Core vs Guardian Correction`;
- добавить prompt/validator rules, запрещающие ломать explicit player setup.

### Phase 6: политика между Хранителями

- offensive projects;
- target power loss;
- defensive counterplay;
- последствия для rival arcs и Chaos Sea politics.

### Phase 7: балансировка

- подправить числа после ручных smoke-тестов;
- отдельно проверить:
  - power snowballing;
  - чрезмерно punitive hostile starts;
  - friendly boosts, которые делают игру слишком лёгкой;
  - частоту offensive project damage между Хранителями.

## Что нужно проверить отдельно перед кодом

- где сейчас хранить project tier и offensive target;
- нужен ли отдельный whitelist для `offerings to abode`;
- какой файл лучше использовать для `guardian_corrections.json`;
- сколько guardian quests реально стоит показывать в UI без перегруза, если cap вырастет до 4-5;
- нужно ли ограничить число одновременно применённых corrections к одной жизни.

## Предлагаемые v1 ограничения

- не больше `2` guardians могут вносить корректировки в старт одной жизни;
- не больше `1 strong` hostile correction на жизнь;
- friendly correction не может полностью нейтрализовать hostile correction бесплатно;
- hostile correction не может отменять explicit player-written start fact;
- offensive guardian project не должен опускать target below `0` одним ударом, если только серия ударов не накопилась честно.
