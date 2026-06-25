# Shining Abode Player-Founded Guardian Design

## Статус документа

`V1 IMPLEMENTED / MINIMAL NEXT-ITERATION SCOPE ALSO IMPLEMENTED`

Этот документ описывает **отдельную post-Shining ветку прогрессии**, которая уже реализована для `v1` и может в будущем расширяться дополнительными системами.

Он нужен для будущей реализации механики, в которой вознесённая душа, вернувшаяся из Сияющей Обители в Море Хаоса, может **учредить собственного Хранителя**.

Текущее состояние реализации:

- ritual-authoring flow, pending foundation request и Chaos Sea command уже существуют в коде;
- accepted-turn validation, soul link, foundation history и inbox surfacing уже существуют;
- `former_patron` continuity для прежнего active guardian уже закреплена;
- `playerGuardianFoundationStatus` уже productized как soul-side completion marker;
- `soulbound` уже имеет минимальную `v1` gameplay-semantics;
- richer `/guardians` overview уже существует;
- minimal founder-specific bonus уже существует как extra guardian gacha charge per return;
- minimal founder-abode feature уже существует как founder-attraction resident roster path;
- `former_patron` narrative follow-up уже surfaced как GM-driven hook, а не как отдельная diplomacy subsystem;
- remaining items относятся уже не к обязательному `v1`, а только к более глубоким optional expansions поверх текущего post-`v1` состояния.

Этот документ:

- не переписывает текущий Shining stack;
- не отменяет текущий guardian-policy contract;
- не делает вид, что feature всё ещё полностью отсутствует в коде;
- фиксирует рекомендуемую форму реализации, чтобы потом не принимать те же архитектурные решения заново.

---

## Current V1 Implementation Status

В текущем коде уже реализованы:

- `/found_guardian_mantle` и ritual-authoring flow в `Chaos Sea`
- `pending_player_guardian_foundation.json`
- accepted-turn validation и resolution contract для founded guardian branch
- `guardians.json.playerGuardianFoundationHistory[]`
- `soul_state.playerFoundedGuardianId`
- `soul_state.playerGuardianFoundationStatus = founded`
- continuity прежнего guardian через `relationshipData.guardianRoleToPlayer = former_patron`
- `founderLoyaltyTier = soulbound` как special founder marker с минимальным legendary-tier floor
- `founderBonuses.extraGachaChargesPerReturn = 1` как minimal founder-specific perk
- `founderAbodeFeatures` и `founder_attraction` roster mode для первой founded-abode resident branch
- `/guardians` overview и afterlife inbox surfacing для foundation outcome
- GM-facing reminder text, `/guardians` summary и foundation notifications, которые явно трактуют `former_patron` как narrative follow-up hook

То есть для `v1` эта ветка уже считается реализованной, а её минимальный post-`v1` expansion layer тоже уже введён. Ниже документ продолжает служить источником правды для текущей формы механики и для optional future expansions beyond the current minimal scope.

---

## Summary

После восхождения и последующего возвращения в `Chaos Sea` игрок может открыть новый late-game branch:

- не просто снова быть душой под покровительством чужого Хранителя,
- а **учредить собственную Хранительскую сущность** и начать существовать в Море Хаоса уже как источник новой Обители, нового покровительства и новой afterlife-власти.

Ключевое решение этого документа:

- игрок **не переписывается из `player_soul` в `guardian`**;
- вместо этого игрок остаётся `player_soul`, но создаёт **player-founded guardian mantle**;
- этот mantle materialize-ится как **новый canonical guardian actor** внутри `guardians.json`.
- это **добавляет** нового Хранителя в `guardians[]`, а не уничтожает уже существующих.

Простая формулировка:

> Игрок не “исчезает и становится другим существом”.
>  
> Игрок оформляет вокруг себя собственную Хранительскую манифестацию и тем самым основывает собственный центр afterlife-власти.

---

## Почему игрок не может просто стать Хранителем напрямую

Это должно быть закреплено не только архитектурно, но и лорно.

### Лорное правило

Даже после вознесения игрок остаётся **духом иного порядка**, чем обычный Хранитель.

Игрокская душа:

- проходит смертные жизни;
- сохраняет continuity через `livesHistory`, memory legacies и reincarnation loop;
- может входить в `Shining Abode`, выходить из неё и снова воплощаться;
- остаётся именно `player_soul`, даже когда достигает предельно высокого afterlife-статуса.

Обычный Хранитель:

- не является reincarnating soul-stream;
- существует как стабильный покровительский центр в `Chaos Sea`;
- удерживает собственную Обитель и выступает объектом resident-bond и guardian-policy systems;
- по смыслу игры относится к другому afterlife-порядку, чем душа игрока.

Именно поэтому вознесённая душа:

- может приблизиться к уровню, на котором она способна **основать** Хранителя;
- но не должна просто “переписываться” в обычного guardian actor как будто различия никогда не было.

Правильный лорный смысл такой:

> Вознесённая душа не становится обычным Хранителем напрямую,
> потому что её природа остаётся природой души, прошедшей через воплощения, память и вознесение.
>  
> Она может только породить из себя собственную Хранительскую манифестацию и сделать её устойчивой.

### Архитектурное следствие

Будущая механика должна моделировать не:

- `player_soul -> guardian`

а:

- `player_soul -> founds guardian mantle`

---

## Почему нельзя делать простое `player_soul -> guardian`

Текущая архитектура уже жёстко различает:

- `player_soul`
- `guardian`
- `resident`
- `radiant_actor`

См.:

- `docs/audits/afterlife/shining-abode/Shining_Abode_Faction_Politics_Addendum.md`
- `docs/audits/afterlife/shining-abode/Abode_Resident_Personality_And_Devotion_Design.md`

Причины не делать прямой type-rewrite:

- это бы стёрло лорное различие между reincarnating soul и stable guardian-order entity;
- resident `bond` уже определён как личная связь именно с Хранителем;
- `guardians.json` уже владеет отдельной canonical identity Хранителя;
- `activeGuardian`, guardian trade, guardian gacha, guardian projects, navigation и abode-state уже строятся вокруг отдельного guardian actor;
- если просто переписать `player_soul` в `guardian`, это сломает current ownership model, relationship model и часть guardian-policy assumptions.

Поэтому будущая механика должна говорить не:

- `игрок теперь больше не душа, а Хранитель`,

а:

- `игрок как душа основал собственную guardian-identity`.

---

## Лорный смысл механики

До этой ветки игрок:

- находится под покровительством чужого Хранителя,
- может взаимодействовать с Обителью, резидентами, afterlife systems, но не является их автономным источником.

После Shining branch игрок становится достаточно сильным, чтобы:

- удерживать собственную afterlife-форму,
- стабилизировать собственную Обитель,
- быть не только получателем покровительства, но и его источником.

По лору это означает:

- игрок **не нанимает нового Хранителя**;
- игрок **не клонирует себя в NPC**;
- игрок **основывает собственный трон / mantle / принцип присутствия в Море Хаоса**.

Именно это в документе и называется:

- `player-founded guardian`
- `guardian mantle`
- `founding a guardian throne`

---

## Core Decision

### Финальная рекомендуемая модель

Будущая реализация должна использовать следующую модель:

1. игрок остаётся `player_soul`;
2. создаётся новый `guardian` actor с origin, указывающим на игрока как основателя;
3. новый guardian добавляется в `guardians[]` рядом с уже существующими;
4. по умолчанию этот новый guardian становится текущим `activeGuardian`;
5. прежний active guardian **не удаляется** и не стирается из истории;
6. старая Shining state не уничтожается только из-за этого перехода.

### Чего делать не надо

Не надо:

- переписывать `player_soul.currentRealm logic` так, будто игрок больше не существует как душа;
- трактовать foundation так, будто у игрока теперь может существовать только один guardian actor;
- silently заменять старого Хранителя без следа;
- force-convert-ить старого Хранителя или игрока в resident;
- автоматически переносить старых resident records под нового guardian;
- пытаться reuse стартовый new-game flow как будто это просто ещё один обычный `pendingGuardianCreation`.

---

## Unlock Contract

Рекомендуемые условия открытия ветки:

- `currentRealm = Chaos Sea`
- у игрока уже был хотя бы один успешный вход в `Shining Abode`
- игрок вышел в `Chaos Sea` через осмысленный post-Shining route, а не находится в активной ordinary Shining-сессии
- `preparedIncarnationPackage = null`
- нет active `afterlife_return_guard`
- нет незавершённого foundation request
- в текущем save ещё не существует уже основанного `player-founded guardian`, если в `v1` путь одноразовый

### Recommended v1 gate

Для простоты и архитектурной чистоты в `v1` рекомендовано открывать механику только если:

- `currentRealm = Chaos Sea`
- stored `shining_abode_state.availability = sealed_until_next_ascension`

Это означает:

- игрок сознательно покинул активную Сияющую Обитель;
- branch не конфликтует с ordinary `reenter_shining_abode`;
- игрок не пытается одновременно жить в active Shining loop и основывать свой guardian mantle.

Это не единственно возможный вариант, но это лучший стартовый контракт.

---

## Player-Facing Fantasy

Игрокский смысл этой ветки должен звучать так:

- `Я больше не просто гость в afterlife systems.`
- `Я могу основать собственную Обитель и стать новым узлом силы в Море Хаоса.`

UI/UX язык должен подавать это как:

- `Учредить собственного Хранителя`
- `Основать Хранительскую мантию`
- `Воздвигнуть свой трон в Море Хаоса`

Но не как:

- `создать ещё одного NPC`
- `сменить текущего покровителя`
- `переключиться на другой скин Хранителя`

---

## How The Player Founds Their Guardian

Эта механика должна ощущаться не как мгновенный unlock и не как автоконверсия после Shining.

Её правильная форма:

- **осознанное решение**
- **ритуал учреждения**
- **accepted-turn materialization**

### Recommended v1 player flow

1. игрок находится в `Chaos Sea`;
2. Shining уже consciously sealed через `return_to_chaos_sea`;
3. в afterlife menu появляется late-game action:
   - `Учредить собственного Хранителя`
   - или `/found_guardian_mantle`
4. игрок проходит короткий authoring flow;
5. клиент пишет pending foundation request;
6. следующий accepted turn materialize-ит нового guardian.

### Почему это не должно происходить автоматически

Потому что это не passive reward, а **онтологический акт учреждения новой afterlife-сущности**.

Если сделать это автоматическим эффектом возвращения из Shining:

- потеряется ощущение выбора;
- исчезнет вес решения;
- игрок не успеет задать identity новой мантии;
- механика будет читаться как “апгрейд формы”, а не как основание нового guardian center.

---

## Authoring Ritual

В `v1` игрок не должен писать гигантский свободный текст.

Нужен короткий, но содержательный ritual-authoring flow.

### Recommended inputs

Игрок задаёт:

- `proposedDisplayName`
  - как зовут нового Хранителя
- `mantleSummary`
  - краткое определение сущности
- `mantleCreed`
  - во что верит новая мантия / какой закон она несёт
- `appearanceMotifs[]`
  - ключевые образные мотивы
- optional `dominantAspect`
  - память / кузня / знание / покровительство / власть / путь / другой high-level theme

### Recommended UX framing

Игроку должно быть ясно, что он:

- не вызывает стороннее существо;
- не “выбирает нового хозяина”;
- а **оформляет из собственной души устойчивую guardian-manifestation**.

Правильный текст подсказки должен звучать примерно так:

> Ты не становишься обычным Хранителем напрямую.
>  
> Но ты можешь вынести из своей вознесённой души собственную Хранительскую мантию и закрепить её в Море Хаоса.

---

## Confirmation Step

Перед созданием pending request игрок должен увидеть явное подтверждение.

### Confirmation must explain

- новый guardian будет создан как отдельная canonical сущность;
- он станет `activeGuardian` по умолчанию;
- старые guardians не исчезнут;
- это крупный late-game progression шаг;
- это не отменяет player-soul identity.

### Recommended v1 confirmation tone

Не нужен overly dramatic irreversible warning, но нужно явное подтверждение масштаба:

- `Вы собираетесь учредить собственного Хранителя.`
- `Старые Хранители сохранятся, но новая мантия станет вашим основным activeGuardian.`
- `Продолжить?`

---

## Multi-Guardian Model

Эта ветка должна явно исходить из того, что у игрока **может быть больше одного Хранителя**.

### Базовое правило

- `guardians[]` хранит всех значимых Хранителей, связанных с игроком;
- `activeGuardian` — это только текущий главный afterlife-фокус;
- основание собственного Хранителя добавляет нового guardian actor, но не запрещает существование старых и будущих guardian relationships.

### Recommended v1 semantics

- player-founded guardian создаётся как новый entry в `guardians[]`;
- он становится `activeGuardian` по умолчанию;
- прежние guardians остаются в `guardians[]`;
- ordinary guardian systems продолжают исходить из правила “один activeGuardian в данный момент”, но не “только один guardian вообще”.

Это должно быть отражено и в кодовой модели, и в лоре.

---

## Relationship To Existing Guardian

Это один из самых важных слоёв дизайна.

### Что происходит со старым Хранителем

Старый `activeGuardian`:

- остаётся canonical guardian actor в `guardians[]`;
- теряет статус `activeGuardian`;
- не удаляется;
- не превращается в resident;
- не теряет свою identity, abode history, gacha/trade/project history.

### Recommended v1 semantics

При успешном foundation:

- новый player-founded guardian становится `activeGuardian`;
- прежний active guardian остаётся в `guardians[]` как отдельный значимый actor;
- в `v1` прежний active guardian получает canonical relation-status:
  - `former_patron`

Более сложные labels вроде `ally`, `rival` или `estranged_patron` относятся уже к optional later expansions.

### Почему это важно

Если старый guardian просто исчезнет:

- сломается continuity;
- потеряется часть сильного эмоционального payoff;
- часть resident/bond/history semantics станет непонятной.

Правильный лорный эффект здесь в том, что игрок **перерастает покровительство**, а не стирает его историю.

### Что это означает practically

Основание собственного Хранителя:

- не отменяет старую guardian history;
- не делает старого Хранителя “несуществующим”;
- не мешает игроку в дальнейшем иметь сложную сеть из нескольких guardian relations;
- лишь меняет то, кто сейчас является `activeGuardian` по умолчанию.

---

## Founder Loyalty

Player-founded guardian должен быть особым не только по origin, но и по лояльности.

### Лорный смысл

Этот guardian не просто “хорошо относится к игроку”.

Он:

- порождён из души игрока;
- обязан своим существованием его afterlife-силе и его воле;
- структурно ближе к игроку, чем любой обычный guardian relation.

Поэтому его начальная лояльность должна быть:

- максимальной;
- устойчивой;
- качественно отличной от обычной guardian reputation.

### Recommended v1 semantics

Новый player-founded guardian должен получать special founder relation, например:

- `founderLoyaltyTier = soulbound`
- или другой отдельный canonical label, отличающийся от обычных guardian-reputation tiers.

Минимальный смысл этого relation:

- player-founded guardian стартует на максимальной лояльности к игроку;
- ordinary guardian drift не должен легко понижать эту связь;
- в `v1` это выражается минимумом через special founder marker и requirement держаться не ниже legendary guardian tier;
- эта связь объясняется происхождением, а не просто высокой симпатией.

Это не означает, что player-founded guardian лишён характера или agency.
Это означает, что **его базовая преданность игроку — онтологическая, а не случайно нажитая**.

---

## Relationship To Shining Abode

Player-founded guardian branch не должен разрушать или дублировать Shining subsystem.

### Что сохраняется

Если player-founded guardian route запускается после `return_to_chaos_sea`, то:

- stored `shining_abode_state.json` сохраняется как есть;
- `availability = sealed_until_next_ascension` остаётся корректным canonical состоянием;
- structural Shining progress не должен стираться;
- future re-ascension path остаётся возможным, если более поздний дизайн не решит иначе.

### Что не происходит автоматически

Не происходит автоматически:

- перенос сияющих фракций в guardian abode;
- перенос сияющих residents в новый guardian abode;
- перенос Shining trade/forge/gates в guardian systems;
- автоматическая политическая конверсия Shining state в Chaos Sea guardian politics.

Это должен быть **новый guardian branch**, а не физический перенос Shining state в другой subsystem.

---

## Canonical Ownership Model

### `guardians.json`

Остаётся canonical owner для:

- нового guardian actor;
- нового `activeGuardian`;
- новой guardian abode navigation binding;
- guardian-side origin metadata;
- foundation history / receipt.

### `soul_state.json`

Может получить additive поля вроде:

- `playerFoundedGuardianId`
- `playerGuardianFoundationStatus`

Но не должен становиться owner самого guardian actor.

### `guardian_abode_residents.json`

Не должен получать silent auto-migration старых residents при foundation.

Если позже появится отдельная система привлечения резидентов к player-founded guardian, это должен быть отдельный explicit flow.

### `shining_abode_state.json`

Не должен владеть новым guardian actor.

Допустимы только:

- optional history link
- optional unlock marker

но не canonical guardian data.

---

## Request / Resolution Model

Эту механику не надо делать client-local raw rewrite.

### Recommended shape

Добавить новый client-owned control file:

- `game_state/control/pending_player_guardian_foundation.json`

Внутри хранить один pending request.

### Recommended request payload

Минимальный shape:

```json
{
  "requestId": "player_guardian_foundation_x",
  "mode": "player_founded_guardian",
  "founderSoulName": "Имя души",
  "previousGuardianId": "guardian_old",
  "previousGuardianName": "Старый Хранитель",
  "sourceShiningAvailability": "sealed_until_next_ascension",
  "proposedDisplayName": "Имя новой мантии",
  "mantleSummary": "Краткое описание сущности",
  "mantleCreed": "Во что верит новый Хранитель",
  "appearanceMotifs": [
    "..."
  ],
  "createdAtTurn": 123,
  "createdAtUtc": "..."
}
```

### Recommended authoring semantics

Этот request должен означать не “сгенерировать guardian-а из воздуха”, а:

- игрок уже прошёл ritual-authoring flow;
- игрок зафиксировал, какой именно guardian mantle он учреждает;
- accepted turn должен признать и materialize-ить эту мантия в canonical afterlife state.

### Почему отдельный pending file лучше, чем reuse `pendingGuardianCreation`

Потому что это не new-game bootstrap.

У этой ветки уже есть особые условия:

- игрок уже существует;
- старый guardian уже существует;
- Shining history уже существует;
- foundation должен валидироваться against current afterlife state;
- здесь нужен proof не просто создания guardian-а, а **переучреждения afterlife роли игрока**.

Поэтому reuse стартового `pendingGuardianCreation` не рекомендуется.

---

## Accepted-Turn Resolution Contract

Успешный accepted turn должен materialize-ить:

1. новый guardian entry в `guardians[]`
2. `activeGuardian`, зеркалящий этого guardian
3. `chaosSeaNavigation.currentAbodeId`, указывающий на его abode
4. foundation receipt / history entry
5. additive soul link к founded guardian
6. cleanup pending foundation request

### What the accepted turn is actually doing in fiction

В лоре этот turn означает:

- Море Хаоса признаёт новую мантия устойчивой;
- из души игрока оформляется новый guardian actor;
- новая Обитель получает право существовать как самостоятельный afterlife center;
- эта сущность становится основным current patron focus игрока.

### Новый guardian actor должен содержать как минимум

- `guardianId`
- `displayName` / `canonicalName`
- `originType = player_founded_ascended_soul`
- `founderSoulName`
- `founderLoyaltyTier`
- `formerPatronGuardianId`
- `foundationSource = shining_return`
- `foundationRequestId`
- свой `abode`
- свою обычную guardian canonical shape

### Recommended v1 result

Новый guardian становится:

- текущим `activeGuardian`
- владельцем нового текущего `currentAbodeId`

Старый guardian:

- остаётся в массиве guardians
- больше не active

Игрок при этом:

- остаётся `player_soul`
- не теряет soul continuity
- не теряет Shining continuity

---

## Foundation Receipt / History

Для этой ветки нужна явная audit surface.

Рекомендовано добавить в `guardians.json`:

- `playerGuardianFoundationHistory[]`

Минимальный receipt:

```json
{
  "requestId": "player_guardian_foundation_x",
  "guardianId": "guardian_player_founded_x",
  "guardianDisplayName": "Новый Хранитель",
  "founderSoulName": "Имя души",
  "formerPatronGuardianId": "guardian_old",
  "formerPatronGuardianName": "Старый Хранитель",
  "foundationSource": "shining_return",
  "resolvedAtTurn": 123,
  "resolvedAtUtc": "..."
}
```

Это важно для:

- UX clarity
- validator parity
- future journal/history/narrative hooks

При желании отдельным additive history можно позже добавить:

- `formerPatronOutcome`
- `becameActiveGuardian = true`
- `founderLoyaltyTier`

---

## Guardian Abode Scope

### Recommended v1 scope

Player-founded guardian получает:

- собственный guardian abode
- базовую guardian identity
- обычный access к guardian-afterlife systems

Но в `v1` не получает автоматически:

- готовый перенесённый resident roster
- авто-перенос старого guardian trade state
- авто-перенос старых guardian projects
- авто-перенос старой guardian gacha history

Причина:

- это было бы уже не foundation, а миграция целой чужой meta-структуры.

Лучший v1-смысл:

- игрок основывает новое начало,
- а не отбирает готовую систему бывшего покровителя.

Этот scope уже является фактическим `v1` поведением в коде: founded guardian branch не переносит автоматически residents, projects, trade, gacha и не импортирует Shining polity в новую guardian abode.

---

## Progression Semantics

### Single-use or repeatable?

Recommended `v1`:

- **one founded guardian per save**

Причины:

- это не противоречит тому, что у игрока может быть много guardians вообще;
- проще validation;
- проще identity continuity;
- не возникает вопроса, какой из нескольких player-founded guardians truly canonical;
- это сильнее ощущается как late-game milestone.

### Reversibility

Recommended `v1`:

- foundation является **сильным и почти необратимым** progression шагом;
- обычного “стереть нового guardian и как будто ничего не было” path быть не должно.

Это не означает:

- что старые guardians исчезают;
- что игрок навсегда лишается любых отношений с прежним patron guardian;
- что multi-guardian continuity запрещена.

Дальнейшее развитие этой ветки должно идти не через отдельную diplomacy-system, а через:

- GM-driven narrative follow-up для `former_patron`
- founder-specific bonuses
- resident attraction / foundation-specific abode features

Но это уже относится к следующей итерации, а не к стартовой реализации `v1`.

---

## Validation / Authority Notes

Эта ветка должна подчиняться тем же strict rules, что текущие Shining/Guardian systems.

Validator должен требовать:

- существующий validated pre-turn foundation request
- корректный current Chaos Sea context
- отсутствие конфликта с pending bootstrap / invalid return guards
- exact materialization нового guardian actor
- exact `activeGuardian` mirror parity
- exact `currentAbodeId` binding
- exact foundation receipt/history entry
- cleanup request после success

Validator должен fail-ить:

- client-local guardian rewrite без accepted-turn foundation
- overwrite старого guardian вместо materialize новой сущности
- missing old guardian continuity
- reuse foundation path, если player-founded guardian уже существует в `v1` single-use mode
- foundation из active Shining или Mortal World

---

## UX / Command Surface

Рекомендуется не прятать механику в сырой freeform prompt.

### Recommended entry surface

В Chaos Sea добавить явный command / menu action вроде:

- `/found_guardian_mantle`
- `Учредить собственного Хранителя`

### Player authoring flow

Игрок должен задать:

- имя новой мантии
- краткий символический образ
- credo / principle
- appearance motifs

Это создаёт pending foundation request.

### Resolution surfacing

После accepted turn игрок должен явно увидеть:

- что foundation удался;
- кто был прежним patron guardian;
- как называется новая guardian identity;
- какой новый abode стал текущим;
- что это теперь новый activeGuardian.

Это уже реализовано в `v1` через `/guardians` overview, pending/completed foundation summaries и afterlife inbox detail.

---

## Non-Goals For V1

В `v1` эта ветка не должна пытаться решить всё сразу.

Не включать:

- multi-guardian party management
- resident migration politics
- Shining faction import into guardian abode
- guardian-vs-guardian succession wars вокруг старого patron guardian
- full rewrite resident bond semantics
- превращение игрока в pure guardian-only POV without soul continuity

Это всё возможные future expansions, но не стартовая цель механики.

---

## Current Post-V1 Expansion Status

После `v1` foundation branch уже получила минимальную реализацию трёх направлений, которые раньше были вынесены в next-iteration scope:

- `former patron narrative follow-up (GM-driven, no dedicated subsystem)`
  - реализовано как GM-facing reminder language, `/guardians` surfacing, foundation notification summary и inbox detail;
  - прежний guardian с ролью `former_patron` может получать narrative follow-up через обычные GM-driven Guardian talks, Guardian journals, Guardian/Soul quests, `guardianPowerEvents`, foundation history/receipts, and derived afterlife notifications;
  - не использовать `worldEventsLog` для этого follow-up: это Mortal World surface и rejected в afterlife; `afterlife_notifications.json` тоже не primary GM proof surface, а client-derived inbox from canonical receipts/state;
  - отдельная diplomacy-state machine по-прежнему не нужна и не вводится.

- `founder-specific bonuses`
  - реализовано в минимальном объёме:
    - founded guardian получает `founderBonuses.extraGachaChargesPerReturn = 1`
    - этот bonus реально участвует в canonical guardian gacha charges derivation и validation;
  - более широкий founder perk set остаётся optional future expansion.

- `resident attraction / foundation-specific abode features`
  - реализовано в минимальном объёме:
    - founded guardian получает `founderAbodeFeatures`
    - новая founded abode использует `founder_attraction` roster mode в уже существующем resident request pipeline
    - это materialize-ит первых резидентов новой мантии без автоматической миграции старого roster;
  - более глубокая founder-abode ecosystem остаётся optional future expansion.

По-прежнему не включать:

- `inherited traits from Shining progress`
  - identity founded guardian уже достаточно определяется ritual-authoring flow и не требует отдельного inheritance из Shining progress.

---

## Recommended Implementation Order

### Phase 1. Design contract and state ownership

`V1: completed`

- зафиксировать request file
- зафиксировать guardian origin metadata
- зафиксировать single-use rule
- зафиксировать foundation receipt/history owner

### Phase 2. Accepted-turn creation path

`V1: completed`

- authoring UI / command
- pending request save-load
- accepted-turn validation
- new guardian materialization
- activeGuardian swap

### Phase 3. UX and continuity

`V1: completed`

- inbox/notification summary
- current guardian overview updates
- old guardian continuity surfacing

### Phase 4. Minimal post-v1 expansions

`Implemented in minimal form`

- former patron narrative follow-up (GM-driven, no dedicated subsystem)
- founder-specific bonuses
- resident attraction or foundation-specific abode features

Более глубокие версии этих трёх направлений остаются optional later expansions и не требуются для текущего document-parity.

---

## Final Recommendation

Если эту механику вводить, вводить её нужно именно так:

- как **новую late-game branch**
- как **foundation of a new guardian mantle**
- как **materialization новой guardian identity**
- без разрушения существующего split между `player_soul` и `guardian`

Главное решение документа:

> Игрок не заменяет собой Хранителя как тип сущности.
>  
> Игрок как вознесённая душа учреждает собственного Хранителя, который становится его новой canonical guardian-manifestation в Море Хаоса.

Это даёт:

- сильный лорный payoff;
- чистую архитектуру;
- минимальный конфликт с текущими Shining и Guardian contracts;
- хороший фундамент для будущей реализации.
