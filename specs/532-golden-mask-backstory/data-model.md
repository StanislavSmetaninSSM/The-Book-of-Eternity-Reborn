# Проект данных и владения

Статус: предложение 2026-10-26 после принятия отдельного realm и исследуемой
памяти. Точная версия spec, детали D2 и оставшиеся D3/D4 требуют рассмотрения.
Это схема ответственности, не готовый JSON-schema. Точные поля/allowlists
фиксируются после утверждения поведения, до первого runtime-изменения.

## Каталог и знание

Один версионированный каталог #1520 хранит утверждённую истину и определения.
Существующий список 10 Хранителей/40 квестов не расширяется личными сценами:
они находятся в отдельной коллекции `goldenMaskMemories` того же story package.
Оба источника используют общий формат scene definition: exact ID, revision/digest,
prerequisites, эпоха, historical POV owner, профиль/возможности роли, места/связи,
объекты, cast с мотивами и знанием того периода, fixed historical events,
allowed choices, discoveries/nodes, success/failure и exact closure/reward ID.
Границы сцены определяют допустимые дополнения GM; ключевые события/персонажи
и награда не создаются заново на каждом ходе. Проверка способности использует
роль и локальные ресурсы, а не present player profile.
Полные определения приватны для GM; UI получает разрешённую проекцию.

`main_story_saref_state.json.goldenMask` — предлагаемый owner личной линии:
- `identityKnowledge`: что открыто игроку о связи его души с Маской;
- `evidence`: точные ID источника/события/свидетеля и открытые утверждения;
- `memories`: открытие и принятый итог каждой личной памяти, без копии runtime;
- `rewardGrants`: принятие точной награды этой души из этой сцены.

Знание конкретного NPC хранится/проектируется через существующую actor-owned
memory/relationship authority; `identityKnowledge` игрока не обновляет всех
акторов. Приватная истина каталога не копируется в открытый player evidence.
После установления личности текущие имя/пол/облик/отношения не меняются сами.

## Один владелец сценического состояния

Предлагаемый новый owner — `game_state/meta/memory_scene_state.json`:
`activeSessionId` и коллекция sessions с принятым прогрессом. Не `currentRealm=Memory`
и не копия всего сохранения. Реальная душа остаётся в исходном месте; сценический
resolver выбирает контекст до обычного realm dispatch. Повреждённая active-ссылка
означает ошибку, а не молчаливое возвращение к настоящему.

Минимальные данные session:
- instance/attempt ID, source и exact reference на владельца closure;
- definition ID/revision/digest, state revision, status;
- POV/local actor ID, current scene location, локальное время/ordinal;
- открытые места/объекты/факты, принятые узлы и ссылки на действия-доказательства;
- изменения local cast, память принятых диалогов, локальные ресурсы/конфликт;
- связь с scene transcript, принятым завершением и grant receipt.

Определение неизменно; session хранит изменяющиеся данные/ссылки. После completion
в активном runtime не нужны полные копии истории: итог и transcript принадлежат
своим владельцам, сохраняются компактный результат и ссылки. Точный retention
закрытых попыток закрепить в схеме; не путать с ограничением диагностических логов.

Старый `SarefMainStoryState.memoryScene` перестаёт быть независимо изменяемым
слотом. Его consumers переводятся на reference/projection общего owner; q4 proof
ссылается на точный принятый instance и definition. Bootstrap/examples/fixtures
обновляются вместе. Два авторитетных mutable экземпляра одной сцены запрещены;
не требуется fallback старых development saves.

| Source | Closure authority | Что запрещено |
|---|---|---|
| guardian_quest | guardianId + exact questId + ordinal 4 + revelation/advantage + memorySceneProof | Personal reward вместо q4 proof |
| golden_mask | soul identity + catalog memoryId/revision + завершённые узлы + exact rewardId | Фиктивный Guardian, автоматическое q4, новый route fragment |

Добавить source требует обновить текущие bootstrap/examples/fixtures;
молчаливого угадывания source по наличию полей не предусматривается.

Предлагаемые переходы:
- available → active: явное действие игрока, prerequisites, нет конфликтующего owner;
- active → paused: добровольный выход с сохранением принятых узлов;
- paused → active: повторная проверка доступности/текущей среды;
- active → failed: утверждённое условие неудачи попытки;
- failed → active: новая попытка с её прогрессом, старые grants не удаляются;
- active → completed: валидное closure и награда в одной публикации.

«Доступно» относится к записи каталога/progress, active/paused/failed/completed —
к попытке; не подменять исторически завершённую память статусом её replay.
Replay разрешён для добора пропущенных целей; он не перезаписывает исходное
completion/grant. Учёт впервые награждённых целей принадлежит долговременному
прогрессу памяти, а не новой попытке. Впервые принятая пропущенная цель получает
свою награду только при успешном завершении этой попытки; уже награждённая не
получает её снова. Смена attempt/revision не обнуляет этот учёт. Момент выдачи
утверждён автором; exact schema/identity цели уточняются в #1561.
Предлагается разделить provisional proof попытки и durable earned goal:
пауза сохраняет первый, успех атомарно публикует второй с completion/grant;
неудача/окончательный отказ не переносят предварительные цели в новую попытку.
Ранее заработанные цели и ожидающие ранга повышения не теряются при неудачном
replay. Это не второй экземпляр выдачи основной награды за память.
Одна активная сцена обоих источников, приостановленная не владеет текущим ходом.
Предложение: несколько paused-сцен могут храниться одновременно; единственный
activeSessionId выбирает владельца действий. Resume повторно проверяет актуальные
ограничения. Политика один paused instance на definition не даёт создавать
неограниченные дубликаты одной попытки; новый retry только после её закрытия.
Definition mismatch не исправляется догадкой/новым сюжетом GM: нужен явный отказ
и разрешённый restart, сохраняющий уже принятый итог/награду.

## Персонажи, роль и проекции

Исторический актор адресуется `(sceneInstanceId, localActorId)`. Provenance
может ссылаться на `saref_001` или exact Guardian, но не даёт прав менять
canonical actor, его нынешний brain или relationship. POV owner управляется
игроком; не создавать рядом второй автономный экземпляр того же Хранителя.
Scene-owned память хранит принятые события этой попытки и использует знания
периода из definition. Pause/reload не подмешивают будущее из настоящих журналов.

Один detached `PlayerSceneContext` (предлагаемое имя) содержит role/location,
разрешённые map/cast/props и action capabilities; private GM definition отдельно.
Общие DTO карты/диалога переиспользуются. Картинка — точная scene-qualified
ссылка, не поиск по совпавшему названию. Не материализовать исторических NPC
в настоящих Mortal/Guardian registries. Сценические предметы нельзя экспортировать.

## Запрос, время и публикация

Memory action содержит exact instance/source, definition identity, state revision,
исходное место/цель, request/generation binding. Доверенный pending snapshot
фиксирует контекст; GM не выбирает режим проверки. Stale action после перехода,
pause/resume/retry/rollback отклоняется. Fresh ownership/eligibility сохраняются.

Рекомендуется отдельное scene time: протокольный номер accepted turn остаётся
монотонным, но настоящие world/guardian/faction/trade/resource cycles не идут
от исторических действий. Staging не вызывает мутирующий обычный scheduler,
finalization не запускает внешние последствия. Накопленный ранее backlog не
теряется и не увеличивается из-за scene turns; выход не создаёт catch-up.
Это проектная политика D2, не уже существующее поведение.

История памяти имеет instance/POV/period context и отдельную ownership-проверку.
Нынешний story stream может получить только явный итог посещения/полученное знание.
Разрешённые after-images: сцена + её transcript + точное source-specific closure;
protocol/save bookkeeping перечисляются отдельно. Обычные game roots защищены
проверкой diff и существующим rollback. Новый owner/transcript/closure включаются
в save/backup/restore и validation allowlists; публикация и grant атомарны.
Не вводится новая транзакционная система или обещание запретить физическую запись
GM средствами одной подсказки. Непринятые данные не становятся canonical authority.

## Награда

Постоянный эффект принадлежит текущему `player_soul` в
`game_state/meta/afterlife_entity_profiles.json`, не историческому «Маске».
Grant ссылается на catalog reward definition, proof и момент принятия.
Для основной личной награды уникальность soul+memory+reward;
attempt/revision не позволяют фармить повтор. Дополнительные цели требуют
отдельного однократного учёта по цели: одно искусство может получать разные
согласованные повышения за разные цели, но artId сам по себе не различает их.
Профиль и story completion публикуются атомарно через существующий accepted-turn.
Receipt/proof не выдаёт само себя из одного произвольного GM-поля.

Для первого искусства после D3 определить: artId, baseOperation, tier,
costMultiplierPercent, допустимый механический эффект, trigger, предел,
resource/roll audit и видимость. Его настоящий consumer обязан применять
эти значения, а не только выводить описание. Исторические способности
сценической роли остаются отдельными от текущих приобретённых способностей.

## Пространство Нового Порядка (D1 принят)

`soul_state.currentRealm = New Order` — предлагаемый canonical token;
русское название отображения «Новый Порядок». Единственная общая классификация
должна согласоваться с оставшимися самостоятельными realm-потребителями.
`game_state/meta/new_order_state.json` — предлагаемый owner состояния места:
существование/состояние сердца, currentLocationId, ссылки на materialized locations,
посетительский return context, независимые от фракции состояние убежища/жителей.
Полный topology/ownership/materialization contract согласуется с #1514.

Wings сохраняет одну canonical запись/ID в текущем Shining owner; присутствие
в New Order — ссылка, не вторая faction. При смене canonical owner это решение
пересматривается явно с #1522. То же относится к Saref actor.
Return context содержит подтверждённый source realm/location и состояние визита;
не разрешает безусловную запись в произвольный указанный игроком путь.
Travel receipt не является новым воплощением, отдыхом или New Cycle.

## Кэширование данных

Immutable catalog/definition DATA — через существующий RepositoryService;
повторные зависимые проекции — OperationValidationService в scope операции.
Definition ID + полная content identity/revision и версия алгоритма определяют
результат разбора; scene projections добавляют instance/state revision и visibility
policy. Knowledge/availability добавляют точные зависимости от story/profile/source
и generation/request, где они влияют на результат. Retained mutable JSON наружу
не выдаётся; одни detached данные передаются карте, месту, спискам и validation.
Каждая mutation/retry/rollback/replacement/generation смена инвалидирует её
зависимые результаты. Scope закрывается и при ошибке; свежие права на действие
и публикацию не становятся кешируемой истиной. TTL/path/mtime недостаточны.
