# Проект данных и владения

Статус: предложение 2026-10-26, зависит от D1–D4 в spec.md.
Это схема ответственности, не готовый JSON-schema. Точные поля/allowlists
фиксируются после утверждения поведения, до первого runtime-изменения.

## Каталог и знание

Один версионированный каталог #1520 хранит утверждённую истину и определения.
Существующий список 10 Хранителей/40 квестов не расширяется личными сценами:
они находятся в отдельной коллекции `goldenMaskMemories` того же story package.
Definition содержит exact ID, revision, ссылки на prerequisites, роль,
fixed historical events, allowed choices, nodes, success/failure и reward ID.
Полные определения приватны для GM; UI получает разрешённую проекцию.

`main_story_saref_state.json.goldenMask` — предлагаемый owner личной линии:
- `identityKnowledge`: что открыто игроку о связи его души с Маской;
- `evidence`: точные ID источника/события/свидетеля и открытые утверждения;
- `memories`: доступность и результат каждой личной сцены;
- `rewardGrants`: принятие точной награды этой души из этой сцены.

Знание конкретного NPC хранится/проектируется через существующую actor-owned
memory/relationship authority; `identityKnowledge` игрока не обновляет всех
акторов. Приватная истина каталога не копируется в открытый player evidence.
После установления личности текущие имя/пол/облик/отношения не меняются сами.

## Общий envelope активной памяти

Существующий `memoryScene` остаётся единственным слотом активной/последней сцены.
Новый обязательный `source` различает `guardian_quest` и `golden_mask`.
Общие поля: ID сцены и попытки, revision определения, role, boundaries,
abilities, requiredStoryNodes, successCondition, status, accepted turn evidence.

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
Если replay включён, он не перезаписывает исходное completion/grant.
Одна активная сцена обоих источников, приостановленная не владеет текущим ходом.
Правила конкуренции нескольких paused-сцен нужно закрыть в D2 до реализации.

## Награда

Постоянный эффект принадлежит текущему `player_soul` в
`game_state/meta/afterlife_entity_profiles.json`, не историческому «Маске».
Grant ссылается на catalog reward definition, proof и момент принятия.
Уникальность soul+memory+reward; attempt/revision не позволяют фармить повтор.
Профиль и story completion публикуются атомарно через существующий accepted-turn.
Receipt/proof не выдаёт само себя из одного произвольного GM-поля.

Для первого искусства после D3 определить: artId, baseOperation, tier,
costMultiplierPercent, допустимый механический эффект, trigger, предел,
resource/roll audit и видимость. Его настоящий consumer обязан применять
эти значения, а не только выводить описание. Исторические способности
сценической роли остаются отдельными от текущих приобретённых способностей.

## Пространство (только если D1=realm)

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
результат разбора; knowledge/availability добавляют точные зависимости от story,
profile, source и релевантного контекста. Retained mutable JSON наружу не выдаётся.
Каждая mutation/retry/rollback/replacement/generation смена инвалидирует её
зависимые результаты. Scope закрывается и при ошибке; свежие права на действие
и публикацию не становятся кешируемой истиной. TTL/path/mtime недостаточны.
