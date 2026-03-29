# Сияющая Обитель: жёсткое gameplay-ТЗ

## 1. Цель системы

`Сияющая Обитель` — это post-ascension layer, в котором игрок собирает `пакет следующего воплощения`.

Ключевая формула:

- `Море Хаоса` = расширение roster.
- `Сияющая Обитель` = сборка следующей жизни.

Главное требование к системе:

- она должна быть механически детерминированной;
- она не должна ломаться о любую необычную фракцию, которую придумают ГМ и игрок;
- она не должна убивать нарратив фиксированными сюжетными шаблонами.

Поэтому все ключевые сущности строятся в два слоя:

- `Narrative skin`
  - имя
  - описание
  - tone
  - ideology
  - target
- `Mechanical frame`
  - effect family
  - project archetype
  - tier
  - cost
  - payload

---

## 2. Главная модель

### 2.1. Пространство

Сияющая Обитель — это одна высшая Обитель. Внутри неё находятся:

- `Зал Согласия`
- `Врата Воплощения`
- `Залы Фракций`

### 2.2. Фракции

Каждая фракция имеет:

- `главу`
- `резидентов`
- `зал`
- `base strength`
- `силу фракции`
- `проекты`
- `favored archetype`
- `patron effect family`
- derived `trade profile`
- `forge eligibility`

Прямое соответствие модели Моря Хаоса:

- `Обитель Хранителя` -> `Зал Фракции`
- `Сила Обители` -> `Сила Фракции`
- `Проекты Обители` -> `Проекты Фракции`
- `Резиденты Обители` -> `Резиденты Фракции`

Жёсткое правило связи с залом:

- authoritative ownership зала хранится только в `faction.hallId`
- любые обратные ссылки зала на фракцию считаются derived-view, а не canonical state

### 2.3. Источники фракций

В Сияющей Обители есть два типа фракций:

- `ascended_guardian`
  - создаются из вознесённых Хранителей игрока
- `native_radiant`
  - открываются тратой `Искр Света`
  - materialize-ятся ГМом
  - получают свободную identity, но обязаны иметь canonical mechanical frame

---

## 3. Сияние

`Сияние` состоит из двух независимых механик:

- `Radiance XP / Tier`
  - постоянный рост
  - не тратится
  - определяет лимиты и потолки
- `Искры Света`
  - отдельная расходуемая валюта Сияющей Обители
  - тратится на сильные действия
  - полностью пополняется только при true activation / re-activation Сияющей Обители
  - current v1 full refill points: ascension activation / re-activation и sealed re-entry after `return_to_chaos_sea`
  - ordinary `reenter_shining_abode` не refill’ит запас, а возвращает игрока с текущим stored остатком
  - normal `Life Evaluation -> Chaos Sea` сам по себе refill не делает
  - current v1 intentionally treats this as a persistent scarce currency across ordinary post-life returns into the same stored active state
  - это deliberate v1 balance shape, а не случайная спецификационная дыра
  - при ordinary return через `Chaos Sea` сохраняет ценность для следующего `reenter_shining_abode`, но в sealed-state становится inert до следующего вознесения

### 3.1. Таблица рангов Сияния

| Tier | XP range | Blessing picks | Draft size | Supported projects | Faction Strength cap | Forge ceiling | Native faction discovery |
|---|---:|---:|---:|---:|---:|---|---|
| 0 | 0-99 | 1 | 4 | 1 | 50 | `reshape` | недоступно |
| 1 | 100-219 | 2 | 6 | 1 | 65 | `retune_property` | базовая нативная фракция |
| 2 | 220-379 | 2 | 7 | 2 | 80 | `strengthen_band` | сильная нативная фракция |
| 3 | 380-579 | 3 | 8 | 2 | 90 | `stabilize_echo` | элитная нативная фракция |
| 4 | 580+ | 4 | 10 | 3 | 100 | `uplift_rarity` | лучшая нативная фракция |

### 3.2. Получение XP Сияния

| Источник | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| Успешное открытие новой нативной фракции | уже оплачено discovery-стоимостью | `+20 XP` | ускоряет рост ранга |
| Первое завершение проекта данного archetype у фракции в текущем ascension | project cost | `+10 XP` | поощряет развитие разных направлений |

Жёсткие правила:

- seeded completed projects, пришедшие вместе с новой discovery-фракцией, не дают дополнительный `+10 XP`
- только player-facing `complete_project` может засчитать archetype в per-ascension XP tracker

### 3.3. Основные расходы Искр Света

| Действие | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `discover_native_faction` | `25 🪶 + 20 Искр Света` | pending discovery | расширяет roster сильных фракций |
| `invest_in_faction` | `10 🪶 + 5 Искр Света` | `+8 factionStrength` | усиливает карты, торговлю и forge-сервисы |
| `complete_project:tier1` | `20 🪶 + 10 Искр Света` | completed project | даёт supportable mechanical asset |
| `complete_project:tier2` | `30 🪶 + 15 Искр Света` | completed project | даёт более сильный supportable asset |
| `complete_project:tier3` | `40 🪶 + 20 Искр Света` | completed project | даёт наиболее сильный supportable asset |
| `forge_relic.reshape` | `10 🪶 + 10 Искр Света` | смена формы реликвии | меняет form-tag без роста силы |
| `forge_relic.retune_property` | `20 🪶 + 15 Искр Света` | замена 1 свойства | меняет build реликвии |
| `forge_relic.strengthen_band` | `30 🪶 + 20 Искр Света` | усиление 1 свойства на 1 band | прямой рост силы реликвии |
| `forge_relic.stabilize_echo` | `25 🪶 + 15 Искр Света` | `+15 manifestation quality` к одной companion-реликвии | делает descent стабильнее |
| `forge_relic.uplift_rarity` | `45 🪶 + 30 Искр Света` | `rarity +1` | повышает ceiling реликвии |

Обычные операции Сияющей Обители допустимы только если одновременно выполнены:
- `currentRealm = Shining Abode`
- `availability = active`
- если операция не относится к bootstrap handoff, то `prepared incarnation package` не существует

---

## 4. Сила фракции

`FactionStrength` — числовая величина `0..100`.

### 4.1. Базовые значения

| Тип фракции | Базовая сила |
|---|---:|
| Фракция вознесённого Хранителя | 35 |
| Нативная фракция, открытая на Tier 1 | 55 |
| Нативная фракция, открытая на Tier 2 | 60 |
| Нативная фракция, открытая на Tier 3 | 65 |
| Нативная фракция, открытая на Tier 4 | 70 |

Жёсткое правило:

- `baseStrength` фиксируется один раз при materialization фракции и дальше хранится как canonical state;
- у нативной фракции это число вычисляется на момент discovery-resolve и потом больше не зависит от будущего `Radiance Tier`.
- `residentCount` для силы фракции считает только резидентов с `ascensionState = ascended` и `shiningFactionId = factionId`.

### 4.2. Источники роста силы

| Источник | Значение | Ограничение |
|---|---:|---|
| Каждый вознесённый резидент фракции | `+3` | максимум `+15` суммарно |
| Завершённый project tier 1 | `+8` | без общего project-count капа |
| Завершённый project tier 2 | `+12` | без общего project-count капа |
| Завершённый project tier 3 | `+16` | без общего project-count капа |
| Каждый `invest_in_faction` | `+8` | максимум `3` раза на фракцию за одно ascension |

Инвестиции являются временным ascension-бонусом:

- они усиливают фракцию только в рамках текущего ascension;
- после нового вознесения `investCountThisAscension` сбрасывается;
- `FactionStrength` пересчитывается без старых инвестиций.

### 4.3. Полосы силы

| Band | Value | Card rarity ceiling | Trade tier | Service multiplier |
|---|---:|---|---:|---:|
| Dormant | 0-24 | `common` | 0 | `0.75x` |
| Stable | 25-49 | `uncommon` | 1 | `1.00x` |
| Strong | 50-74 | `rare` | 2 | `1.25x` |
| Radiant | 75-100 | `radiant` | 3 | `1.50x` |

### 4.4. Что даёт сила фракции

| Механика | Прямой effect |
|---|---|
| Blessing cards | верхний предел rarity для patron/project/descent cards этой фракции |
| Trade | определяет размер витрины и ceiling качества товаров |
| Services | в v1 влияет только на forge `stabilize_echo` |
| Project outputs | повышает качество generated project-cards и secondary effects |

`tradeTier`, `serviceMultiplier` и `tradeProfile` в v1 считаются derived-значениями. Они не должны рассматриваться как authored source of truth.

Жёсткое правило dormant trade:

- если `tradeTier = 0`, магазин не открывается вообще
- slot-бонусы от `provision` и `resource_support` начинают работать только при `tradeTier >= 1`

---

## 5. Хранители, резиденты и patron-карты

### 5.1. Вознесённый Хранитель

Каждый глава фракции всегда даёт:

- `1 patron card`
- `1 patron effect family`
- `1 favored archetype`
- authored identity/tone фракции

Patron card не обязана иметь фиксированное название.  
Она обязана иметь:

- `displayName`
- `displaySummary`
- `effectFamily`
- `rarity`
- `effectPayload`

### 5.2. Что даёт резидент без relic

Резидент без `grantedRelicId`:

- даёт `+3` к силе фракции
- активирует один role-modifier своей роли
- не создаёт отдельную blessing-card

Жёсткое правило membership:

- принадлежность резидента к фракции определяется только его `shiningFactionId`
- список резидентов фракции является derived-view, а не отдельным canonical state

### 5.3. Что даёт резидент с relic

Резидент с `grantedRelicId` делает всё то же самое, что и обычный резидент, и дополнительно:

- может породить `1 descent card`, если у его фракции есть поддерживаемый project archetype `passage`

Это единственный механический канал, который ведёт его обратно в смертную жизнь.

### 5.4. Роли резидентов

Одинаковые роли в одной фракции не stack’аются. Если во фракции два резидента одной роли, применяется роль один раз, но оба всё ещё дают `+3 strength`.

| Role | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `archive_support` | нет | усиливает remembrance/revelation контур | `memory` cards этой фракции дают `+1 memory reroll`; `revelation` projects этой фракции стоят на `5 🪶` меньше |
| `forge_support` | нет | делает refinement выгоднее | все forge actions этой фракции стоят на `5 🪶` и `5 Искр Света` меньше |
| `social_support` | нет | усиливает social/route cards | все `social` cards этой фракции получают `+5 relation`; `route` cards дают `latestTurn - 1` |
| `resource_support` | нет | усиливает resource и trade | все `resource` cards этой фракции дают `+50 money` и `+1 common material`; если `tradeTier >= 1`, trade stock `+1 slot` |
| `descent_support` | нет | усиливает descent | все `descent` cards этой фракции получают `latestTurn - 3` и `manifestationQuality +15` |

Если изменение резидента меняет `shiningFactionId`, `residentRole`, `ascensionState` или `grantedRelicId`, уже открытые Врата становятся stale.  
`FactionStrength` пересчитывается только если при этом меняется resident count фракции.

---

## 6. Нативные фракции

### 6.1. Discovery

| Действие | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `discover_native_faction` | `25 🪶 + 20 Искр Света` | `1` pending discovery | открывает новую сильную фракцию Сияющей Обители |

Ограничения:

- `Tier 0` не может запускать discovery
- одновременно может существовать только `1` pending discovery
- повторный вызов при наличии pending discovery отклоняется без списания ресурсов

### 6.2. Минимальный mechanical package новой фракции

После резолва discovery ГМ обязан materialize-ить фракцию с такими элементами:

- `1` глава
- `2..4` отдельных resident records с `ascensionState = ascended` и `shiningFactionId = newFactionId`
- `1` зал, на который новая фракция ссылается через `hallId`
- `1 favored archetype`
- `1 patron effect family`
- базовая сила по `Radiance Tier`
- `2` уже завершённых проекта:
  - один проекта ее favored archetype
  - один проект любого archetype

Жёсткие правила discovery-resolve:

- сначала из текущего `Radiance Tier` до reward вычисляется и сохраняется canonical `baseStrength` новой фракции
- discovery даёт только `+20 XP` как отдельную награду игроку
- два seeded completed projects не дают дополнительный `+10 XP`
- seeded completed projects не заносятся в per-ascension XP tracker
- если Врата уже были открыты, текущий draft становится stale и требует нового `open_gates`

### 6.3. Примеры нативных фракций

Это не whitelist, а примеры допустимых materialization-профилей:

| Пример display name | Favored archetype | Gameplay-профиль |
|---|---|---|
| `Кузнецы Луча` | `refinement` | сильная перековка и relic-oriented patron cards |
| `Хранители Памяти` | `remembrance` | memory cards, rerolls и archive-эффекты |
| `Смотрители Врат` | `passage` | descent-cards и стабильные входы в смертную жизнь |
| `Хор Рассвета` | `accord` | social cards и улучшение ранних союзов |
| `Писцы Сияния` | `revelation` | lore cards и гарантированные ранние зацепки |
| `Дом Тихой Щедрости` | `provision` | resource cards и усиленная витрина |

### 6.4. Зачем это нужно игроку

Каждое discovery:

- расширяет pool patron cards
- расширяет торговлю и forge-доступ
- даёт новые archetype-направления
- даёт сильные проекты, которых может не быть у head-фракций игрока

---

## 7. Проекты фракций

### 7.1. Главный принцип

Проект в Сияющей Обители — это не фиксированная line из короткого whitelist.

Проект всегда состоит из двух слоёв:

- `Narrative skin`
  - `displayName`
  - `summary`
  - `toneTags`
  - `targetFactionIds[]`
- `Mechanical frame`
  - `projectArchetype`
  - `outputEffectFamily`
  - `tier`
  - `cost`
  - `strengthReward`
  - `supportEffect`

### 7.2. Project archetypes

Для v1 использовать закрытый каталог archetypes:

- `revelation`
- `accord`
- `provision`
- `remembrance`
- `refinement`
- `passage`
- `warding`
- `subversion`

Любая фракция может иметь проект любого archetype.  
`Favored archetype` даёт bias, а не whitelist.

### 7.3. Стоимость и reward по tier

| Tier | Стоимость | Strength reward | Base project-card rarity |
|---|---|---:|---|
| 1 | `20 🪶 + 10 Искр Света` | `+8` | `common` |
| 2 | `30 🪶 + 15 Искр Света` | `+12` | `uncommon` |
| 3 | `40 🪶 + 20 Искр Света` | `+16` | `rare` |

Если `projectArchetype == favoredArchetype` фракции:

- стоимость уменьшается на `5 🪶 + 5 Искр Света`
- минимум `1 🪶 + 1 Искра Света`

### 7.4. Lifecycle проекта

| Status | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `active` | нет player-facing cost | проект существует только как authored in-progress сущность | не даёт `strengthReward`, не supportable, не создаёт cards |
| `completed` | дополнительной цены нет | проект завершён | даёт `strengthReward`, может быть supported, участвует во Вратах |
| `retired` | дополнительной цены нет | проект снят с активного механического контура | не даёт `strengthReward`, не supportable, остаётся только в истории |

Правила:

- `projectId` уникален глобально
- у фракции может быть несколько `completed` проектов одного archetype
- support включается на конкретный `project instance`, а не на archetype
- dedupe во Вратах идёт только по `card.dedupeKey`, а не по самим проектам
- player-facing v1 flow не использует отдельное `start_project`
- в v1 новая механическая единица проекта появляется через `complete_project`

### 7.5. Что считается поддерживаемым проектом

Поддерживаемым считается любой `completed project`, у которого:

- есть canonical `projectArchetype`
- есть canonical `outputEffectFamily`
- есть fixed `supportEffect`

Игрок поддерживает не “фразу в лоре”, а конкретный project instance.

### 7.6. Operation contract проектов

| Действие | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `complete_project:tier1` | `20 🪶 + 10 Искр Света` | новый `completed` project | даёт `+8 strength`, становится supportable, может давать card |
| `complete_project:tier2` | `30 🪶 + 15 Искр Света` | новый `completed` project | даёт `+12 strength`, становится supportable, может давать card |
| `complete_project:tier3` | `40 🪶 + 20 Искр Света` | новый `completed` project | даёт `+16 strength`, становится supportable, может давать card |
| `support_project` | нет | `completed project` становится `supported` | включает project-card и archetype-specific bonus этого project instance |
| `retire_project` | нет | перевод в `retired` | убирает проект из активной механики и снимает его strength/support effect |

Если `projectArchetype == favoredArchetype`, стоимость completion уменьшается на `5 🪶 + 5 Искр Света`.

Повторный `support_project` на уже supported project считается no-op.
Lookup проекта для `support_project` всегда идёт через пару `factionId + projectId`; project из другой фракции для этой операции невалиден.
Lookup проекта для `retire_project` идёт по той же паре `factionId + projectId`.
Все `project`-операции допустимы только в active Shining Abode state.
Пара `projectArchetype + outputEffectFamily` обязана соответствовать canonical compatibility table; несовместимый authored project не считается валидным gameplay-project.

Жёсткое XP-правило:

- `complete_project` может дать `+10 XP` только если archetype ещё не был учтён у этой фракции в текущем ascension
- discovery-seeded projects это правило не триггерят
- после нового вознесения per-ascension XP tracker очищается, и archetype снова может дать свой первый `+10 XP` в новом ascension

### 7.7. Support effects по archetype

| Archetype | Что даёт support | Прямой gameplay-эффект |
|---|---|---|
| `revelation` | `+1 project-card` | добавляет `lore` или `memory` candidate; `latestTurn - 2` для generated lore cards |
| `accord` | `+1 project-card` | добавляет `social` или `route` candidate; `social` cards этой фракции получают `+5 relation` |
| `provision` | `+1 project-card` | добавляет `resource` или `route` candidate; если `tradeTier >= 1`, trade stock `+1 slot` |
| `remembrance` | `+1 project-card` | добавляет `memory` или `lore` candidate; даёт `+1 reroll` во Вратах |
| `refinement` | `+1 project-card` | добавляет `relic` или `resource` candidate; unlock forge actions |
| `passage` | `+1 project-card` | добавляет `descent` или `route` candidate; включает descent cards резидентов с relic |
| `warding` | `+1 project-card` | добавляет `survival` или `social` candidate; `survival` cards этой фракции получают `recovery +10%` |
| `subversion` | `+1 project-card` | добавляет `social`, `lore`, `memory` или `descent` candidate; target faction получает ровно `-5 effective strength` на текущий Gates build |

Под effective strength понимается временное значение текущего Gates build, используемое только при генерации и сортировке cards. Stored `factionStrength` не уменьшается.

Trade-slot бонусы от `provision` и `resource_support` применяются только если у фракции уже есть `tradeTier >= 1`.

Жёсткие правила `subversion`:

- проект archetype `subversion` обязан иметь ровно `1 targetFactionId`
- target не может совпадать с source faction
- несколько supported `subversion` проектов по одной цели не stack’аются выше `-5`
- effective strength цели не может опускаться ниже `0`
- penalty влияет только на текущий Gates snapshot

---

## 8. Перековка реликвий

Forge actions доступны только у фракции, у которой есть поддерживаемый project archetype `refinement`.

| Forge action | Требуемый Tier | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---:|---|---|---|
| `reshape` | 0 | `10 🪶 + 10 Искр Света` | меняет form-tag | меняет совместимость/визуальную форму без смены rarity |
| `retune_property` | 1 | `20 🪶 + 15 Искр Света` | заменяет 1 свойство | меняет build реликвии |
| `strengthen_band` | 2 | `30 🪶 + 20 Искр Света` | повышает 1 свойство на `+1 band` | прямой рост силы реликвии |
| `stabilize_echo` | 3 | `25 🪶 + 15 Искр Света` | применяет echo-stability buff | выбранная companion-реликвия получает `manifestationQuality +15` |
| `uplift_rarity` | 4 | `45 🪶 + 30 Искр Света` | повышает rarity на `+1 step` | увеличивает ceiling свойства и качества реликвии |

`forge_support` резидент снижает стоимость каждого forge action на `5 🪶 + 5 Искр Света`.

---

## 9. Blessing cards

`Blessing card` — это единственная форма бонуса, которую игрок реально уносит через Врата в следующую смертную жизнь.

### 9.1. Главный принцип

Карты не должны быть жёстким списком authored-имен.  
Они должны быть:

- свободными по presentation
- фиксированными по `effectFamily` и payload

У карты есть:

- `displayName`
- `displaySummary`
- `effectFamily`
- `rarity`
- `effectPayload`
- `dedupeKey`

### 9.2. Effect families

Для v1 использовать закрытый список families:

- `lore`
- `social`
- `resource`
- `memory`
- `descent`
- `survival`
- `relic`
- `route`

### 9.3. Payload по rarity

| Family | Common | Uncommon | Rare | Radiant |
|---|---|---|---|---|
| `lore` | `1 clue by turn 12` | `1 clue by turn 10` | `1 clue by turn 8` | `2 clues by turn 8` |
| `social` | `+10 relation` | `+15 relation` | `+20 relation` | `+25 relation` |
| `resource` | `100 money, 1 common` | `150 money, 2 common, 1 uncommon` | `225 money, 3 common, 1 uncommon` | `300 money, 4 common, 2 uncommon` |
| `memory` | `+1 memory option` | `+1 memory option, +1 memory reroll` | `+2 memory options, +1 reroll` | `+2 memory options, +2 rerolls` |
| `descent` | `encounter by turn 12, quality +5` | `by turn 10, quality +10` | `by turn 8, quality +15` | `by turn 6, quality +20` |
| `survival` | `first ruinous failure downgraded` | `same + recover 10%` | `same + recover 20%` | `same + recover 30%` |
| `relic` | `1 reroll token` | `2 reroll tokens` | `2 rerolls + free reshape` | `3 rerolls + free retune` |
| `route` | `1 route option by turn 10` | `1 route option by turn 8` | `1 route option by turn 6` | `2 route options by turn 6` |

### 9.4. Локальные определения payload-терминов

| Термин | Точное значение |
|---|---|
| `clue by turn N` | ГМ обязан вставить указанное число явных lore-зацепок не позже данного mortal turn; если на одном и том же earliest turn есть несколько допустимых anchors, берётся lexical `anchorId` |
| `first qualifying ally` | первый mortal-world NPC или faction-contact, который не стартует hostile и имеет relation-state; если таких несколько на одном turn, берётся lexical `entityId` или `factionId` |
| `memory option` | один дополнительный слот выбора в memory-selection шаге перед стартом следующей жизни |
| `memory reroll` | один reroll token только для memory-selection шага; не влияет на draft Врат |
| `route option` | один дополнительный ранний жизненный маршрут или opportunity-seed, который должен появиться не позже указанного turn; при одинаково ранних вариантах берётся lexical `routeSeedId` |
| `ruinous failure` | первый провал в смертной жизни, помеченный severity `ruinous`; он понижается на один severity-band |
| `manifestationQuality` | additive modifier к descent-resolution score для соответствующего relic-bearing resident |

### 9.5. Источники card candidates

| Источник | Сколько даёт | Условие |
|---|---:|---|
| Глава каждой фракции | 1 patron card | фракция существует |
| Каждый поддерживаемый completed project | 1 project-card | у проекта есть `outputEffectFamily` |
| Каждый relic-bearing resident | 1 descent card | у его фракции поддерживается archetype `passage` |

### 9.6. Правила dedupe

- `head` и `project` cards dedupe-ятся по payload-based `dedupeKey`
- `resident_descent` cards никогда не схлопываются между разными резидентами
- если два разных вознесённых резидента дают одинаковый `descent` payload, игрок всё равно видит две разные карты выбора

### 9.7. Правила rarity

Для каждого card source:

- `head` использует rarity = `min(radiance ceiling, effective faction ceiling)`
- `resident_descent` использует rarity = `min(radiance ceiling, effective faction ceiling)`
- `project` использует:
  - `base rarity` по `project tier`
  - если effective faction band = `Radiant`, rarity повышается на `+1 step`
  - итоговая rarity затем clamp’ится до `min(radiance ceiling, effective faction ceiling)`

### 9.8. Draft rules

При открытии Врат:

1. Собрать все candidate cards.
2. Применить temporary `subversion` penalties к целевым effective strengths.
3. Для каждого кандидата вычислить final rarity.
4. По rarity построить base payload.
5. Применить resident role modifiers.
6. Применить supported-project modifiers.
7. После всех модификаторов пересчитать `dedupeKey`.
8. Схлопнуть дубликаты по `dedupeKey`, оставив:
   - более высокую rarity
   - при равенстве — карту от более сильной фракции
   - при новом равенстве — приоритет `head > project > resident_descent`
9. Отсортировать итоговый список по:
   - rarity descending
   - effective factionStrength descending
   - source priority descending
   - cardId ascending
10. Взять первые `Draft size` карт по `Radiance Tier`.

Любая build-changing операция после `open_gates` делает текущий draft stale:

- `support_project`
- `unsupport_project`
- `invest_in_faction`
- `complete_project`
- `retire_project`
- `discover_native_faction` resolution
- изменение `shiningFactionId`
- изменение `residentRole`
- изменение `ascensionState`
- изменение `grantedRelicId`

Stale draft нельзя:

- выбирать
- reroll’ить
- подтверждать во Врата

Игрок обязан заново вызвать `open_gates`.

### 9.9. Порядок модификаторов и caps

| Модификатор | Когда применяется | Stack rule | Прямой эффект |
|---|---|---|---|
| `subversion` penalty | до вычисления rarity | максимум один `-5` на цель | режет effective strength цели |
| Resident role modifier | после base payload | одинаковые роли в одной фракции не stack’аются | усиливает payload карты или снижает cost |
| Supported archetype effect | после resident modifiers | stack only if effect явно зависит от числа supported projects | меняет payload или Gates state |
| `memory rerolls` | внутри payload memory-card | stack by card payload | используются только на memory-selection шаге |
| `gates rerollsRemaining` | отдельно в Gates state | `+1` за каждый supported `remembrance` project | меняет только текущий blessing draft |

### 9.10. Picks и rerolls

| Механика | Значение |
|---|---|
| Число picks | по `Radiance Tier` |
| Базовое число rerolls | `0` |
| Дополнительные rerolls | `+1` за каждый поддерживаемый проект archetype `remembrance` |
| Frozen candidate snapshot | полный отсортированный список cards, построенный в `open_gates` |
| История показа | хранит все `cardId`, которые уже были выведены в текущем `draftVersion` |

`Reroll`:

- удаляет из текущего draft `2` lowest-score cards
- подставляет следующие `2` карты из frozen sorted candidate snapshot, которых ещё не было среди уже показанных `cardId`
- не меняет already-selected cards
- использует cursor/history текущего `draftVersion`, поэтому повторные reroll детерминированы
- требует минимум `2` unselected cards; иначе reroll недоступен

---

## 10. Врата Воплощения

Врата не требуют отдельной валютной цены, но доступны только в ordinary active Shining Abode state:

- `currentRealm = Shining Abode`
- `availability = active`
- `prepared incarnation package = null`

| Действие | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `open_gates` | нет | draft current cycle | показывает текущий deterministic card pool; если candidate pool короче `Draft size`, draft просто открывается укороченным |
| `reroll_gates_draft` | `1 reroll` | частичная смена draft | заменяет 2 weakest unselected cards, только если в frozen snapshot есть 2 unseen replacements, а затем заново canonical-sort’ит draft |
| `enter_mortal_life_from_shining_abode` | нет | frozen `prepared incarnation package` | выбранные cards materialize-ятся в следующем воплощении из сохранённого snapshot’а |

Все действия Врат наследуют тот же ordinary Shining Abode guard: выбор, снятие выбора и reroll недоступны вне `currentRealm = Shining Abode`, вне `availability = active` или при уже существующем `prepared incarnation package`.

После подтверждённого входа:

- выбранные cards упаковываются в `prepared incarnation package` вместе с их frozen payload
- `selectedCardIds` и полный список `selectedCards` внутри package совпадают по длине и порядку
- выбранные picks всегда остаются уникальным подмножеством текущего draft
- `availableBlessingCards` очищается
- `selectedBlessingCardIds` очищается
- поддерживаемые проекты и фракции не удаляются
- любой reroll после нового `open_gates` строится только из frozen candidate snapshot этого конкретного draftVersion
- после успешного mortal bootstrap этот frozen package потребляется и очищается
- после `enter_mortal_life_from_shining_abode` игрок покидает Сияющую Обитель и передаётся в bootstrap следующей смертной жизни
- до успешного mortal bootstrap `currentRealm` не переводится в mortal mode и остаётся afterlife-safe
- только успешный mortal bootstrap записывает concrete mortal-world realm value для следующей жизни
- пока `prepared incarnation package` существует, runtime находится в canonical pending-bootstrap handoff state, а не в обычной активной Сияющей Обители
- пока `prepared incarnation package` существует, обычные Shining Abode / Chaos Sea / Guardian interactions недоступны; разрешён только mortal bootstrap
- для global realm routing это определяется не только по `currentRealm`, а по паре `currentRealm = Shining Abode` + `prepared incarnation package != null`

Следующая жизнь должна читать blessing effects из frozen package, а не пересобирать их из уже изменившихся фракций.

---

## 11. Возвраты и выход из Сияющей Обители

| Действие | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `return_to_chaos_sea` | нет | покидание Сияющей Обители | переводит игрока в `Chaos Sea`, seal'ит Сияющую Обитель, очищает transient Gates/discovery state, сбрасывает canonical поля Просветления и сохраняет радиантный/структурный прогресс |

Нормальный возврат после завершённой смертной жизни в current v1 runtime:

- `TriggerLifeEnd` запускает dedicated Life Evaluation lifecycle
- canonical completion point этого lifecycle: accepted turn, чей `manifest.SourceLabel` распознаётся через `LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(...)`
- current v1 source labels для этого: `оценки жизни` и `автоматической оценки жизни`
- normal post-life destination этого lifecycle остаётся `Chaos Sea`
- этот normal route не seal'ит Сияющую Обитель и не меняет сохранённое `shining_abode_state.availability`; seal происходит только через явный `return_to_chaos_sea`
- после каждой принятой оценки жизни runtime активирует `game_state/control/afterlife_return_guard.json` с `reason = post_life_return`, но это только protective guard первого ordinary afterlife turn
- этот guard не является отдельным completion-marker’ом и не означает automatic return в `Shining Abode`
- ordinary afterlife turn consum’ит этот guard только если он semantic-valid; повреждённый guard или parsed guard с неверным `reason` обычным ходом не расходуется и остаётся blocking до runtime-normalization
- ordinary post-life route не возвращает игрока в active Shining Abode автоматически
- если `shining_abode_state.availability` уже хранится как `active`, обратный вход из `Chaos Sea` делается через отдельное действие `reenter_shining_abode`
- `reenter_shining_abode` допустим только если `afterlife_return_guard.json` отсутствует или semantic-valid (`reason = post_life_return`) и неактивен; повреждённый guard или parsed guard с неверным `reason` блокирует re-entry fail-closed до runtime-normalization
- `reenter_shining_abode` возвращает игрока в активную Сияющую Обитель с текущим stored остатком `Искр Света` и без нового ascension reset
- `AscensionTrigger` остаётся отдельным переходом maximum-Enlightenment ascension и не подменяет этот ordinary re-entry route
- и `reenter_shining_abode`, и `return_to_chaos_sea` выполняются как client-owned local lifecycle commands, а не как GM-authored accepted turns

При `return_to_chaos_sea` сохраняются:

- все фракции
- все залы
- вся structural state фракций: `baseStrength`, резиденты и completed projects
- последнее вычисленное значение `FactionStrength` как исторический snapshot
- все completed projects
- весь `Radiance XP`
- текущий `Radiance Tier`

При `return_to_chaos_sea` меняются:

- `return_to_chaos_sea` допустим только из active `currentRealm = Shining Abode`, когда `prepared incarnation package = null`
- `shining_abode_state.availability = sealed_until_next_ascension`
- frozen gates candidate snapshot очищается
- `availableBlessingCards` очищается
- `selectedBlessingCardIds` очищается
- все pending discovery requests очищаются
- `game_state/meta/soul_state.json.enlightenment.currentTier = Новичок`
- `game_state/meta/soul_state.json.enlightenment.experience = 0`
- `game_state/meta/soul_state.json.enlightenment.level = 0`
- если `enlightenment.progressPercent` существует, он тоже сбрасывается в `0`
- остаток `Искр Света` перестаёт иметь gameplay-значение до следующего вознесения
- stored `FactionStrength` в sealed-state остаётся только read-only historical snapshot и не должен использоваться для live trade/forge/gates derivation

Когда игрок снова возносится:

- `availability = active`
- `currentRealm = Shining Abode`
- `Искры Света = 100`
- у каждой фракции `investCountThisAscension` сбрасывается в `0`
- у каждой фракции per-ascension XP tracker очищается
- `FactionStrength` пересчитывается заново из `baseStrength`, текущего resident roster и completed projects без старых инвестиций
- игрок возвращается в ту же Сияющую Обитель
- это re-entry route после `sealed_until_next_ascension`, а не generic ordinary return из `Chaos Sea` при сохранённом `availability = active`
- ordinary `reenter_shining_abode` при сохранённом `availability = active` продолжает тот же active-state и не делает этого ascension-local recompute

---

## 12. Сводка core-механик

| Механика | Стоимость | Что даёт | Прямой gameplay-эффект |
|---|---|---|---|
| `invest_in_faction` | `10 🪶 + 5 Искр Света` | `+8 factionStrength` | поднимает trade tier, card ceiling и forge multiplier |
| `discover_native_faction` | `25 🪶 + 20 Искр Света` | `1 pending discovery` | открывает новую сильную нативную фракцию |
| `complete_project:tier1..3` | по tier table | новый `completed` project | даёт strength, support eligibility и potential card source |
| `support_project` | нет | переводит `completed project` в `supported` | включает project-card и archetype-specific bonus этого project instance; project lookup идёт через `factionId + projectId` |
| `retire_project` | нет | переводит `completed project` в `retired` | убирает strength/support effect; project lookup идёт через `factionId + projectId` |
| `reshape` | `10 🪶 + 10 Искр Света` | новая `formTag` | меняет форму/совместимость реликвии |
| `retune_property` | `20 🪶 + 15 Искр Света` | новая property того же band | меняет build реликвии |
| `strengthen_band` | `30 🪶 + 20 Искр Света` | `+1 band` выбранного свойства | прямое усиление реликвии |
| `stabilize_echo` | `25 🪶 + 15 Искр Света` | `manifestationQuality` bonus | повышает качество воплощения companion-реликвии |
| `uplift_rarity` | `45 🪶 + 30 Искр Света` | `+1 rarity step` | повышает ceiling реликвии |
| `open_gates` | нет | новый draft | строит deterministic blessing pool текущего цикла; short candidate pool даёт short draft без ошибки |
| `reroll_gates_draft` | `1 gates reroll` | partial draft refresh | заменяет 2 weakest unselected cards только если доступны 2 unseen replacements, затем заново canonical-sort’ит draft |
| `enter_mortal_life_from_shining_abode` | нет | frozen `prepared incarnation package` | переносит выбранные карты в следующую жизнь без повторной реконструкции |
| `reenter_shining_abode` | нет | повторный вход в active Сияющую Обитель | возвращает игрока из `Chaos Sea` в Обитель с текущим stored запасом `Искр Света` без сброса ascension-local counters |
| `return_to_chaos_sea` | нет | seals Shining Abode | возвращает игрока в Море Хаоса, seal'ит Обитель, очищает transient Shining-state, сбрасывает Просветление и сохраняет радиантный/структурный прогресс |

---

## 13. Что не ограничивается

Никогда не ограничивать слотами:

- число вознесённых Хранителей
- число вознесённых резидентов
- число открытых нативных фракций

Ограничивать только:

- `Blessing picks`
- `Draft size`
- `Supported projects`
- `Faction Strength cap`

---

## 14. Проверочный кейс: Сареф

Фракция Сарефа должна быть валидной в этой системе без новых правил.

Пример:

- `displayName = Дом Нежной Тени`
- `favoredArchetype = subversion`
- `patronEffectFamily = social`
- authored project:
  - `displayName = Окутать Палаты Памяти мягкой тьмой`
  - `projectArchetype = subversion`
  - `outputEffectFamily = memory`
  - `tier = 2`
  - `targetFactionIds = [faction_memory_keepers]`

Это даёт:

- свободный тёмный нарратив
- понятную цену и reward
- memory-card output
- временное pressure-влияние на target faction

Если система держит такой кейс без ad-hoc исключений, значит модель достаточно универсальна.
