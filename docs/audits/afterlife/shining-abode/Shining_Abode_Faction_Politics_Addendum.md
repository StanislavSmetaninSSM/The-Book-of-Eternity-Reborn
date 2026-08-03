# Сияющая Обитель: faction politics addendum

## Статус документа

Это **дополнение** к:

- `docs/audits/afterlife/shining-abode/Shining_Abode_Endgame_Design_Plan.md`
- `docs/audits/afterlife/shining-abode/Shining_Abode_Implementation_Plan.md`
- `docs/audits/afterlife/shining-abode/Shining_Abode_Implementation_Plan_Rebased.md`

Если старые Shining docs конфликтуют с этим файлом в части:

- устройства фракций,
- смены власти,
- player-founded фракций,
- переходов резидентов между фракциями,
- судьбы бывших лидеров,

то **главнее этот addendum**.

---

## 1. Главное переопределение модели

Старые Shining docs местами предполагают, что у фракции почти всё жёстко привязано к её текущему главе:

- identity,
- patron-source,
- favored archetype,
- политическая легитимность.

Для сменяемой власти это плохой контракт.

Новая модель:

- у фракции есть **charter**
- у фракции есть **current leadership**
- у фракции есть **membership**

Это три разные вещи.

### 1.1. Charter

`charter` отвечает на вопрос:

- что это за фракция вообще,
- какой у неё устойчивый механический профиль,
- почему она существует как отдельная политическая сила.

Именно `charter`, а не текущий глава, теперь владеет:

- `factionName`
- `favoredArchetype`
- `patronEffectFamily`
- базовой thematic identity фракции

Следствие:

- смена главы **не должна автоматически переписывать** `favoredArchetype` и `patronEffectFamily`;
- player, ставший главой существующей фракции, **не перезаписывает** её charter одним фактом захвата власти;
- фракция, основанная игроком, получает charter при основании, а не каждый раз при смене лидера.

Это сознательно переопределяет старое допущение из прежних Shining docs, где глава фракции почти целиком определял patron/favored identity.

### 1.2. Leadership

`leadership` отвечает на вопрос:

- кто руководит сейчас,
- насколько его власть устойчива,
- может ли произойти мирная передача власти или бунт.

Для этого у фракции должны быть:

- `headActorType`
- `headActorId`
- `leadershipState = secure | contested | vacant`

### 1.3. Membership

`membership` отвечает на вопрос:

- кто входит во фракцию,
- насколько члены ей верны,
- готовы ли они перейти в другую фракцию,
- готовы ли они поддержать нового лидера.

Membership не должен храниться в `faction.residents[]`.

Как и в старом Shining плане, authoritative membership остаётся derived from resident state:

- `resident.shiningFactionId`

---

## 2. Новые типы политических акторов

Чтобы корректно переживать смену власти, текущего `headActorType = guardian` недостаточно.

Разрешённые типы главы фракции:

- `guardian`
- `player_soul`
- `resident`
- `radiant_actor`

Смысл:

- `guardian`
  - существующий Хранитель из `guardians.json`
- `player_soul`
  - сам игрок в post-ascension форме
- `resident`
  - уже существующий резидент из `guardian_abode_residents.json`
- `radiant_actor`
  - Shining-only политический актор, не являющийся ни guardian, ни resident

### 2.1. Зачем нужен `radiant_actor`

Он нужен для native/non-guardian глав, которые не должны:

- исчезать после смены власти,
- force-convert-иться в ordinary resident,
- терять собственную identity только потому, что больше не являются главой.

Для этого в Shining state нужен отдельный registry:

- `shiningPoliticalActors[]`

Минимальный shape:

```json
{
  "actorId": "radiant_actor_memory_keepers_head",
  "actorType": "radiant_actor",
  "displayName": "Архонтка Немирия",
  "summary": "Старая хранительница памяти и ритуала.",
  "originFactionId": "faction_memory_keepers",
  "currentFactionId": "faction_memory_keepers",
  "politicalStatus": "head"
}
```

`politicalStatus` допустим в таких значениях:

- `head`
- `former_head`
- `claimant`
- `elder`
- `retired`

---

## 3. Что происходит с бывшими лидерами

Это ключевое решение.

### 3.1. Бывший глава-Хранитель

**Хранитель не превращается в резидента.**

Причина:

- у Хранителя уже есть отдельная canonical identity и большой объём данных в `guardians.json`;
- переводить его в resident record означало бы потерять или дублировать значимые guardian surfaces;
- это сломало бы current guardian-policy ownership model.

Поэтому:

- бывший глава-Хранитель остаётся `guardian`;
- фракция просто перестаёт ссылаться на него как на `headActorType=headActorId`;
- факт потери власти фиксируется в:
  - `leadershipReceipts[]`
  - `leadershipHistory[]`
  - optional `politicalStatus = former_head | claimant | elder` relation in faction-political layer

Если позже понадобится, такой guardian может:

- остаться духовным патроном,
- стать claimant,
- поддержать другого лидера,
- окончательно утратить политическую связь с фракцией.

Но он **не** становится resident.

### 3.2. Бывший глава-резидент

Если глава фракции был `resident`, то:

- он остаётся тем же resident record;
- не создаётся отдельная копия;
- он просто перестаёт быть `headActorType=resident/headActorId=that residentId`.

После смещения такой actor:

- всё ещё существует как resident,
- может остаться во фракции,
- может перейти в `former_head`,
- может стать claimant,
- позже может покинуть фракцию обычным faction-realignment flow.

### 3.3. Бывший глава-`radiant_actor`

Если глава не guardian и не resident, он остаётся в `shiningPoliticalActors[]`.

После смещения:

- его `politicalStatus` меняется на `former_head`, `claimant` или `elder`;
- он не исчезает;
- он не обязан автоматически становиться resident;
- при желании отдельным future pass его можно будет materialize-ить как resident, но это **не default**.

### 3.4. Бывший глава-игрок

Если игрок теряет власть:

- игрок не перестаёт быть `player_soul`;
- теряется только political headship над конкретной фракцией;
- это не уничтожает player-founded faction и не стирает faction charter;
- игрок может оставаться claimant, ally, or external rival in future political flows.

---

## 4. Внутрифракционная лояльность резидентов

Текущая resident model уже умеет:

- `bond` к Хранителю,
- `abode devotion` к Обители,
- migration between Abodes.

Для Shining faction politics нужен ещё один слой:

- отношение резидента **к конкретной фракции**.

Новые additive resident fields:

- `factionLoyaltyLevel`
- `factionLoyaltyTier`
- `factionRestlessness`
- `factionRealignmentState`

### 4.1. Canonical meaning

- `factionLoyaltyLevel`
  - насколько резидент хочет оставаться именно в этой фракции
- `factionLoyaltyTier`
  - derived bucket of the same value
- `factionRestlessness`
  - насколько сильно его тянет к внутрисияющей перемене
- `factionRealignmentState`
  - staged readiness to leave the current faction

Recommended `factionRealignmentState` values:

- `settled`
- `wavering`
- `restless`
- `considering_realignment`
- `ready_to_realign`

### 4.2. Relationship to current resident model

Эти поля **не заменяют**:

- `bondLevel`
- `abodeDevotionLevel`
- `restlessness`
- `migrationState`

Они отвечают на другой вопрос:

- обычная resident system: “хочет ли резидент оставаться в этой Обители вообще?”
- Shining faction politics: “хочет ли он оставаться именно в этой фракции внутри уже принятой Сияющей Обители?”

### 4.3. What affects faction loyalty

На `factionLoyaltyLevel` должны влиять:

- `factionStrength`
- leadership stability
- соответствие роли резидента профилю фракции
- наличие поддержанных проектов
- resident personality
- отношение к текущему главе
- привлекательность других фракций

Никаких ambient random drifts every turn не нужно.

Как и в текущей resident system, shifts должны происходить:

- по сильным accepted-turn событиям,
- bounded steps only,
- с journals/history consequences.

---

## 5. Переходы резидентов между фракциями

Резиденты могут переходить от фракции к фракции **по той же философии**, что и между обычными Обителями:

- staged pressure,
- explicit request,
- canonical resolution,
- receipts/history.

Но это должен быть **отдельный flow**, не reuse межобительной transfer schema один-в-один.

### 5.1. Core rule

Менять `resident.shiningFactionId` напрямую без political flow нельзя.

Нужен отдельный request layer, например:

- `pending_shining_faction_realignments.json`

### 5.2. Когда резидент может realign-иться

Минимальные условия:

- `ascensionState = ascended`
- resident currently belongs to a faction
- `factionRealignmentState = ready_to_realign`
- нет conflicting pending transfer/leadership/founding lock on the same resident

### 5.3. Resolution modes

Recommended modes:

- `accepted_transfer`
- `refused_transfer`
- `departure_to_neutral`

Обычный happy path:

- resident stays in Shining Abode
- old `shiningFactionId` replaced with new one
- `factionLoyaltyLevel/restlessness/state` recomputed relative to new faction
- receipts/history are written on both sides

---

## 6. Основание игроком собственной фракции

Это не должно быть “нажал кнопку и создал клан”.

Это отдельный **founding flow**.

### 6.1. Default founding rule

Игрок может основать фракцию, только если:

- находится в active `Shining Abode`
- `preparedIncarnationPackage = null`
- нет другой уже возглавляемой player faction
- есть минимум `N = 3` ascended residents с явным preliminary consent
- хватает ресурсов на founding

`N = 3` — рекомендуемый canonical default.

### 6.2. Founding support

Согласие должно быть не implied prose, а явным temporary political support surface.

Recommended shape at request time:

- `supportingResidentIds[]`

Validator for founding request должен проверять, что каждый supporter:

- существует,
- `ascensionState = ascended`,
- может сменить фракцию,
- не locked by conflicting pending flow,
- действительно даёт support this turn.

### 6.3. Founding result

Accepted founding:

- materializes new `faction`
- materializes new `hall`
- sets:
  - `originType = player_founded`
  - `headActorType = player_soul`
  - `headActorId = player_soul`
  - `leadershipState = secure`
- moves supporting residents into the new faction
- establishes charter of the faction

### 6.4. Charter of player-founded faction

Для player-founded faction charter выбирается при основании.

Игрок должен учредить:

- `factionName`
- `favoredArchetype`
- `patronEffectFamily`
- tone/identity summary

Это создаёт устойчивую identity фракции.

Позже смена лидера не переписывает charter автоматически.

---

## 7. Смена главы фракции

Смена власти допускается, но не должна быть freeform или мгновенной.

Нужен отдельный leadership flow.

Recommended request layer:

- `pending_shining_faction_leadership_transitions.json`

### 7.1. Три режима перехода

Допустимые canonical leadership modes:

- `abdication`
- `peaceful_succession`
- `revolt`

Все эти режимы требуют текущего non-vacant incumbent head. Если фракция уже имеет `leadershipState = vacant` и пустые `headActorType/headActorId`, не создавай `pending_shining_faction_leadership_transitions.json`: явный vacancy-fill режим пока не реализован.

### 7.2. `abdication`

Глава добровольно уходит.

Требования:

- текущий глава согласен
- указан валидный successor или фракция становится `vacant`

Это самый мягкий сценарий.

### 7.3. `peaceful_succession`

Мирная передача власти.

Default requirements:

- valid candidate
- incumbent consent
- explicit resident support

Recommended support threshold:

- `max(2, ceil(ascendedFactionResidents / 3))`

Это не majority capture, а признанная передача власти.

### 7.4. `revolt`

Силовой захват или бунт.

Default requirements:

- `leadershipState = contested`
- valid challenger
- explicit resident support
- higher support threshold than peaceful succession

Recommended threshold:

- `max(3, ceil(ascendedFactionResidents / 2))`

Consequences of successful revolt:

- faction enters political aftermath
- open Gates become stale
- faction may take temporary strength or stability penalty
- some residents may become more restless or realign away

`revolt` не должен быть тем же самым, что peaceful succession, только с другим текстом.

---

## 8. Когда власть считается contested

`leadershipState = contested` не должен случаться “по настроению”.

Его нужно materialize-ить из political pressure.

Recommended causes:

- заметная resident support loss for incumbent
- много residents in `considering_realignment` or `ready_to_realign`
- sharp faction decline
- explicit leadership challenge that gathered enough preliminary support

На первом проходе достаточно такого правила:

фракция становится `contested`, если accepted turn materializes challenge pressure and one of the following is true:

- at least `2` ascended residents explicitly support a challenger
- at least `1/3` ascended residents are already in severe faction pressure
- faction suffered a major recent setback and the turn explicitly records legitimacy crisis

То есть contested — это не silent formula, а validated political state with visible narrative consequences.

---

## 9. Игрок как глава существующей фракции

Игрок может стать главой уже существующей фракции двумя путями:

- мирный переход
- бунт

### 9.1. Мирный переход

Игрок становится главой существующей фракции, если:

- current head consents
- support threshold met
- no blocking lifecycle/state conflict

В этом случае:

- `headActorType = player_soul`
- `headActorId = player_soul`
- charter фракции остаётся прежним
- бывший глава получает `former_head` status

### 9.2. Бунт

Игрок может захватить лидерство, если:

- faction already `contested`
- revolt support threshold met
- accepted turn resolves the confrontation canonically

В этом случае:

- player becomes head
- old head loses current leadership
- faction takes aftermath consequences
- some residents may refuse the new order and start realignment pressure

---

## 10. Что НЕ надо делать

На этом слое не нужно:

- мгновенных prose-only смен `headActorId`
- автоматических резидентских прыжков между фракциями без request/receipt flow
- превращения guardian в resident после потери власти
- автоматического стирания бывших native heads
- переписывания `favoredArchetype`/`patronEffectFamily` при каждой смене главы
- endless automatic coup loop every turn

---

## 11. Главные explicit defaults

Чтобы implementer не принимал решения заново:

- faction charter и current leadership — разные слои
- `originType` не равен current leadership
- `headActorType` может быть `guardian | player_soul | resident | radiant_actor`
- guardian при потере власти **не** становится resident
- former native/non-guardian head по умолчанию остаётся в `shiningPoliticalActors[]`, а не force-convert-ится в resident
- resident leader при смещении остаётся resident
- player-founded faction требует минимум `3` предварительно согласных ascended residents
- resident faction switching внутри Shining Abode uses explicit realignment flow
- player capture of an existing faction can be `peaceful_succession` or `revolt`
- old rule “current head fully defines faction patron identity” считается устаревшим; стабильную mechanical identity теперь хранит `charter`

## Authority boundary

Faction Materialization requires an exact native discovery, player-founding, or story-authority route. Merely making a Guardian active does not authorize a charter: an active Guardian does not cause the client to invent a faction charter. If the story requires a Guardian-sponsored Shining faction, the GM must author that faction through a supported route and separately bind the head or other significant actors through Actor Materialization.

Chaos Sea Guardian Politics is Actor Materialization/living-world authority under #1500/#1368, not a Mortal or Shining faction.
