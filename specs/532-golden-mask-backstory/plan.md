# Золотая Маска — исследование и проект плана реализации

> Для исполнителя: после утверждения спецификации применять
> `superpowers:executing-plans` либо согласованный subagent-driven workflow;
> TDD, независимое ревью и verification-before-completion сохраняются.

**Дата:** 2026-10-26. **Ветка:** `story/golden-mask-backstory-20261025`.
**Spec:** [spec.md](spec.md). **Цель:** включить прошлое Маски, игровые
исследуемые воспоминания Маски/Хранителей с реальными наградами и Новый Порядок.
**Статус:** Phase 0 дополнена по уточнению автора; Phase 1 — обновлённый проект.
D1 (отдельное посмертие) и основной D2 (исследуемая сцена/POV владельца) приняты.
Это последовательность реализации и проверки, не разрешение её начать.
Открытые контракты не считаются решёнными из-за наличия plan/tasks.

## Технический контекст

C#/.NET 8, System.Text.Json, файловое canonical state, Spectre.Console,
общий CommandProtocol, локальный browser host, React/TypeScript.
Целевые платформы Windows/Linux. Новые облачные сервисы не нужны.
Публикация через существующий accepted-turn/owned-resource механизм;
общие Service/Repository/OperationValidation кэши. Новая транзакционная система
или универсальный язык сценариев не требуются; общий memory runtime нужен.

Источники задач: [#532](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/532)
(историческая линия), [#1519](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1519)
и дочерние #1520–#1522; afterlife locations —
[#1514](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1514).
План не меняет их lifecycle. До runtime-работ нужен явный tracker для принятого
расширения Маски и согласованные изменения пересекающихся критериев.

## Проверка конституции

- Исследование/планирование разрешены текущим запросом; production gate закрыт.
- Spec Kit используется фактически: активная feature, setup-plan, research,
  проект данных/контрактов, tasks и проверка их согласованности.
- Русский интерфейс, отделение GM/private от player projections, console/browser parity.
- Один owner прогресса, точные ссылки и атомарный эффект; no-normalizer-invention.
- Новые схемы обновляют bootstrap, fixtures и примеры; старые saves не создают
  требования migration/compatibility.
- Поведение проверяется test-first на typed независимых фикстурах; реальная
  публикация/восстановление проверяются отдельно от чистых функций.
- Категории по затронутым потребителям, без full suite/Fast/PreMerge.
- Performance assessment обязателен для каждого принимаемого среза.

## Решения перед исполнением

| Gate | Предложение | Что требуется зафиксировать |
|---|---|---|
| D1, принят | Отдельный New Order realm, компактное наполнение | Согласовать расширение #1514/#1522, topology/visitor contract |
| D2, основной формат принят | Исследуемая память с POV владельца, locations/cast/props | Рассмотреть runtime, отдельное scene time/paused sessions; точные действия роли и объём боя в definitions |
| D3 | Пять тем; начать с одного постоянного afterlife special art | Утвердить сцены/силы; определить точный effect/cost/tier/limit и применимость в Mortal |
| D4 | Два порядка раскрытия; вход из Обители; возврат туда же | Точный unlock graph, доказательство личности, правила выхода связанной души и состояния после финалов |

Числа наград выбираются по действующим формулам и небольшим balance-fixtures
после выбора искусства, до его реализации. До этого T009 заблокирована.
Полная спецификация сценария должна назвать неизменные события, допускаемые
выборы, необходимые узлы и результат; «GM решит всё сам» не контракт.

## Архитектура и файлы

Подробности: [data-model.md](data-model.md), [contracts/gameplay.md](contracts/gameplay.md).
Ниже новые имена — предложения для реализации, не существующие сервисы.

1. **Каталог и раскрытие.** Расширить будущий
   `BookOfEternityClient/story_content/saref/catalog.json` из #1520 разделом
   personal memories. Guardian inventory остаётся 10/40. Изменить
   `Services/SarefMainStoryState.cs`, `Services/Validation/ValidationService.SarefMainStory.cs`,
   `Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SarefMainStory.cs`.
   Добавить изолированную логику `Services/GoldenMaskStoryState.cs` для evidence,
   knowledge projection и personal progress; не раздувать общий файл сценариями.
2. **Runtime сцены.** Новый `Services/MemorySceneSessionService.cs` владеет
   `game_state/meta/memory_scene_state.json`: один active и сохраняемые paused
   instances, revision, роль, место, открытия, local cast/memory/resources.
   Старый story.memoryScene заменить reference/projection, не второй копией.
   `Services/Validation/ValidationService.MemoryScenes.cs` проверяет общий контракт;
   source-specific q4 остаётся в Saref validator. Расширить save/backup/restore,
   top-level allowlists и bootstrap. Никакого `currentRealm=Memory` или whole-save clone.
   Новый `PlayerSceneContextResolver` выбирает scene до ordinary realm dispatch;
   malformed active state — ошибка, не fallback.
3. **Исследование и оба клиента.** Общая `MemoryScenePlayerProjection` и
   `UI/ExplorerMemorySceneCommandResultBuilder.cs` (предлагаемые) обслуживают
   карту/место/cast/роль и scene actions. Расширить `LocalMapViewService`,
   `LocalInteractionScopeService`, `ExplorerCommandCatalog`, console/browser
   routers и `BrowserGameScreenService`. Сохранить общие MapViewDto/MapAtlas и
   dialogue UI, добавить реальный move/inspect/talk route с fresh target binding.
   Перевести ложный read-only `/воспоминание_начать` в явный mutating launch.
   Разрешать контекст до ordinary StateManager refresh с profile repair.
4. **Ход и исторические акторы.** В `GameEngine.TurnLifecycle.cs` выбрать
   memory policy до staging: scene profile/period knowledge/reminder вместо
   current characteristics/ActorMemory; не запускать мутирующий ordinary scheduler.
   Global request/accepted sequence сохраняется, scene time отдельно. Finalizer
   публикует scene state/history и exact closure; не тикает настоящее, сохраняет
   backlog, autosave и прежнее владение/rollback. `StoryService` и strict append
   contract получают scene owner; historical local actor journals не становятся
   нынешними journals. Расчёт способностей использует typed role/profile input;
   полноценный бой — только с локальным conflict/resources adapter.
5. **Награда.** Новый `Services/GoldenMaskMemoryRewardService.cs` готовит
   exact detached after-images story/profile для существующей публикации.
   `AfterlifeEntityProfileState.cs` получает честный memory restoration route;
   `ValidationService.AfterlifeSpiritualConflict.cs` и связанный расчёт проверяют
   конкретный эффект зарегистрированного искусства. Reward consumer и tests
   входят в тот же срез, что выдача.
6. **Пространство.** Новый `Services/NewOrderState.cs` хранит
   только собственное состояние места/сердца/посещения; actors/factions не
   дублируются. Расширить `RealmSemantics.cs`, `Models/GameState/AggregatedGameState.cs`,
   `AfterlifeEntityProfileState.cs`, resource owner, `LocalInteractionScopeService.cs`,
   `LocalMapViewService.cs`. Выделить visitor transition в partial
   `Core/GameEngine/GameEngine.NewOrder.cs`, сохраняя прежние death/incarnation
   и New Cycle эффекты только у их собственных операций.
7. **Доступ и торговля.** Узкий resolver присутствия единственных Saref/Wings,
   общая покупка с detached offer DATA и fresh locality/price/stock checks;
   не копировать целиком `ShiningTradeService`. Расширить
   `ExplorerRealmTradeCommandResolver.cs`, browser menus, story/realm validators,
   repair realm models/authority/apply gate. Отключённые Shining-возможности
   должны иметь явную причину недоступности, а не падать в Mortal route.
8. **Содержание и договорённости GM.** `OtherGuides/Saref_Character_Bible.md`,
   десять `Saref_Guardian_Questlines/*.md`, `Saref_Memory_System_Boundaries.md`,
   `Afterlife_Contract_Matrix.md`, релевантные Rules/TaskGuides/Examples,
   prompt construction в `GameEngine.TurnLifecycle.cs`, manifest/source guards.
   Все десять q4 переработать под POV Хранителя, включая способности/цели/узлы;
   сохранить rewards/prerequisites, отдельно разобрать нынешнюю цену Илариона/Вейры.
   `BookOfEternityClient/game_master_daemon.ps1` context-pack и normal/repair
   prompts получают exact scenario; `BookOfEternityClient/Launcher/GM_Turn_Helper.ps1`
   и accepted validator проверяют разрешённые effects/roots. Prompt не заменяет
   проверку сохранности настоящего. Не переносить next-life ScenarioCore owner.

## Порядок законченных срезов

**A. Утвердить контракты и зависимости.** Точная обновлённая spec, детали D2,
D3/D4, catalog baseline #1520,
точные actor/faction #1521/#1522, afterlife locations #1514; разрешить
9/10, mercy_wound и deep-victory расхождения. Не ждать независящих GM-workers.

**B. Первый рабочий путь US1+US2.** Один источник знания, одна память Маски
и один Guardian q4 с общим runtime: два места, объект, исторический собеседник,
move/inspect/talk, одна действующая способность роли, start/pause/resume и
source-specific completion. Одна личная награда с реальным consumer.
RED → минимальная реализация → GREEN → настоящий accepted-turn/recovery.
Проверить оба клиента и memory isolation, затем наполнять остальные сцены.

**C. US3: посещаемое пространство.** Классификация и explicit visitor transition,
одно место, единые actors/faction, одна покупка, возврат без reset, отказ
старого предложения и сохранение состояния при прерывании. D1 уже выбран:
этот срез реализует отдельное посмертие, а не Shining hall.

**D. Полное содержание и последствия.** Остальные утверждённые сцены,
индивидуальные реакции Хранителей, разные порядки раскрытия, состояние
убежища/населения/Крыльев после разных финалов. Не переписывать q1–q3 в
прямую экспозицию. Отдельно доказать отсутствие обязательной личной линии.

Каждый срез включает его prompt/doc/example parity, targeted tests, performance
assessment, независимое ревью, commit/push/checkpoint. Не откладывать реальные
механические эффекты и валидацию до последнего этапа.

## Проверка и внимание ревью

Основные случаи для ревью:
1. Секретная команда/карта/карточка награды открывает ещё неизвестную личность.
2. Personal scene закрывает q4 или даёт route fragment без линии Хранителя.
3. Retry/recovery выдаёт награду дважды либо завершение остаётся без искусства.
4. Временная роль меняет настоящее имя/облик/профиль, а New Order return сбрасывает прогресс.
5. Старая карточка торговли/доступа действует после смены realm, владения или финала.
6. Исторический разговор использует нынешние знания или изменяет настоящее
   через refresh/ActorMemory/scheduler/finalizer, хотя сам prompt выглядит правильным.
7. Смена сцены уничтожает paused progress; old location action принимается новым actor.
8. Guardian role переименована, но узлы/цена/способности по-прежнему рассчитаны на свидетеля.
Эти случаи включены в задачи B–D и [quickstart](quickstart.md).

Существующие владельцы для выбора/разделения будущих проверок:
`narrative-world`, `soul-realm`, `turn-lifecycle-afterlife`,
`canonical-normalization-afterlife`, `afterlife-resource-cutover`,
`shining-trade-politics`, `console-explorer-afterlife`, `browser-command-afterlife`,
`browser-command-parity`, `gm-worker-repair`, `gm-worker-proposals`,
`documentation-afterlife`. Это список затронутых контрактов, не приказ
запустить все категории. Создать узкие memory/reward/realm владельцы по
фактическим новым тестам; сохранить соседние регрессии и selection reasons.

Команды: `pwsh -File scripts/test-csharp.ps1 -ListCategories`, затем
`-Category <выбранные-ID> -PlanOnly`, после утверждения состава — тот же
выбор без `-PlanOnly`. Проверка каталога `-ValidateCatalog` только discovery.
Новые C# APIs документировать читаемо; при кодовых изменениях проверить XML
references. Никакой генерации полного игрового мира ради проверки двух JSON.

## Производительность

Объект reuse — immutable scene definition и зависимые detached проекции внутри
одной команды/хода. Owner — сервис операции; key/dependencies/invalidation
описаны в research/data-model. Fresh permissions/ownership/absence-of-reward
не кэшируются как доступ. Все значения выдаются отделёнными.
Тесты считают actual reads/computations на стабильном повторе и после смены
definition/instance/state revision/visibility/story/profile/generation;
failure/dispose не оставляют пригодного hit. Один detached context передаётся
карте, текущему месту, cast и validation; ordinary whole-world refresh не
повторяется ради каждой сценической проекции. Snapshot narrowing не предлагается.
Массовый кэш на всю игровую сессию и отдельный Dictionary запрещены.
Runtime-замеры в этом блоке UNRUN: меняются документы, ускорение не заявляется.

## Точка продолжения

2026-10-26, уточнение после `43205fcb7`: автор выбрал отдельный Новый Порядок
и исследуемые воспоминания обоих источников от лица владельца. Source-аудит
дополнен navigation/actors/GM/turn lifecycle/scheduler/history/save и десятью q4.
Два независимых read-only исследования GPT-6.1 Sol High проверили navigation
и GM/content границы; родитель проверил staging/finalizer/scheduler/snapshot.
Native Serena: проект ready, symbolic C#/TS доступ; конфигурация не менялась.
Новые главы заменяют прежнее предложение только повествовательных узлов.

Spec Kit setup-plan/setup-tasks повторно выполнены для той же feature без
перезаписи шаблонами. Обновлены spec, research, plan, tasks, data-model, contracts,
quickstart. Текущая ревизия: 3 user stories, 21 FR, 10 AC, 21 задача.
Consistency: 21 FR и 10 AC покрыты задачами, IDs уникальны; семь документов и
13 относительных ссылок проверены, `git diff --check` без ошибок. G1/G2 явно
остаются открыты; это готовность исследовательского результата, не исполнения.
Независимое ревью этой ревизии пока PENDING; T018 отражает это.
До реализации G1: рассмотреть точную spec, детали D2 и D3/D4; G2: разрешить
нужные catalog/identity/location зависимости #1520–#1522/#1514. Исследование
не делает ни эти задачи, ни игровой код выполненными. Runtime/tests UNRUN.
Независимое ревью документации не означает утверждения игровых решений.

В установленном check-prerequisites нет -RequireSpec; spec проверяется отдельно.
Его branch-name check, в отличие от setup-plan/tasks, не учитывает закреплённую
feature: проверка запускается с process-only `SPECIFY_FEATURE=532-golden-mask-backstory`,
реальная Git-ветка остаётся `story/golden-mask-backstory-20261025`.
Скрипты проекта не изменяются. Необязательные git/agent-context hooks не
выполнялись автоматически; checkpoint включает только точные feature-файлы.

Предыдущий checkpoint (не ревью текущего расширения): отдельный GPT-6 Astra High
проверил `cbd417223..e2b42e268` и связанные исходники, существенных findings нет.
Последняя уточняющая фиксация — `43205fcb7f2836390a2bb344be5eb9ed1903f54b`;
её remote SHA проверен перед новым блоком. Новое расширение проходит отдельное
ревью полного документального diff. Raw diagnostics и посторонний obj исключены.
