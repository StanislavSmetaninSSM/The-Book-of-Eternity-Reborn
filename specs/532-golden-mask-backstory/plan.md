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
Tracker зарегистрирован: [#1560](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1560)
и дети #1561–#1570; mapping в [tasks](tasks.md#github--spec-kit). Условие наличия
задач выполнено; авторское утверждение точной spec и предметные gates сохраняются.

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
quickstart. Ревизия исследования на `518be413b`: 3 user stories, 21 FR, 10 AC,
21 задача. Следующий блок регистрации Issues добавляет T022, не игровые требования.
Consistency: 21 FR и 10 AC покрыты задачами, IDs уникальны; семь документов и
13 относительных ссылок проверены, `git diff --check` без ошибок. G1/G2 явно
остаются открыты; это готовность исследовательского результата, не исполнения.
Независимое ревью этой ревизии завершено, T018 выполнена.
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

Ревью текущего блока: отдельный GPT-6 Astra High проверил точный diff
`43205fcb7..ef9b05707`, семь документов и связанные исходники, включая все десять
q4. Существенных findings нет; новые положения о времени и paused sessions
проверены именно как предложения, а не приписанные автору решения. Parent
сверил отчёт с diff/требованиями. Оптимизация оценена: operation-scoped detached
DATA для definition/context/projections, полные зависимости/invalidation,
fresh authority и настоящая публикация сохраняются. Runtime-ускорения не заявлены.
Game tests/builds не запускались: блок меняет только планирование.

WIP `ef9b057071a67d6c572eef6e135bbf094c18524d` опубликован; exact remote SHA
совпал с локальным. Финальный commit добавляет только итог проверки и статус
T018. Проверка clean restore в новую директорию не выполнялась, поэтому
проверенная публикация не обозначается как испытанное восстановление checkout.
Маршрутизация агентов соответствует политике: два bounded Sol High аудита и
отдельное Astra High ревью взаимосвязанных контрактов; данных о стоимости или
выигрыше времени для изменения этой политики в данном блоке нет.

## Регистрация GitHub-задач — 2026-10-26

Запрос автора: до параллельной разработки или передачи Астре создать чёткие
самодостаточные задачи, отметить сюжетную важность и связать с текущим сюжетом;
Астра продолжает #1536. База блока `518be413b29ea67bdb7b9a8c8125eb1bbbc01dfd`
сверена с remote перед изменениями. Runtime не меняется.

Созданы эпик #1560, десять дочерних задач #1561–#1570 и отдельная #1571.
Каждая содержит scope/non-goals, критерии, источники на immutable SHA, Spec Kit
mapping, зависимости и правила тестов/performance. Метка `story: important`
обозначает сюжетную важность, не срочность и не разрешение начать production.
Существующие story issues прочитаны через GitHub; их статусы не меняются.

Bounded Sol 6.1 High audit параллельности: #1536 — related mechanics baseline,
не blanket blocker. Безопасны уточнение spec/содержания/трекеры. Turn/profile/
effect/realm/repair изменения требуют согласованного принятого SHA нужного среза
и одного владельца общих файлов; неопубликованный WIP Астры не известен.
В частности, #1565 затрагивает staging/finalizer/snapshot/scheduler и GM helper,
#1566 — profile/arts/effects, #1568 — realm/resource/repair consumers.

Проверен существующий SOURCE-аудит #1536/T148: historical defeat audit persistence
не доказывает runtime memory_suppression. Его отдельный подготовленный follow-up
зарегистрирован как #1571; это не закрытие T148/E01/#1536 и не смешение с playback
или mercy_wound. Документы/статусы текущей работы по ранам не правятся.

Remote readback выполнен: 12/12 bodies/titles/open states/меток совпали с
подготовленным пакетом, placeholders отсутствуют; GitHub native hierarchy
содержит ровно 10 children #1561–#1570 и 34 exact blocking edges. Граф ацикличен,
#1571 standalone, #1536 не blocking parent. 22 уникальных T IDs и 12 issue mappings
проверены; `git diff --check` прошёл. Все девять pinned source/evidence ссылок
доступны, локальные draft-файлы исполнителю не требуются.

Независимый GPT-6.1 Sol XHigh в отдельном контексте проверил 12 issue bodies,
трёхфайловый mapping diff и native readback; существенных findings не осталось.
Уточнён источник в #1571: более позднее принятое M01 evidence подтверждает
только historical audit persistence; ранний SOURCE-аудит предшествует этому
прогону, E01/runtime suppression остаётся blocked. Это исправление цитирования,
не новый тест или изменение поведения. Parent проверил источники и remote body.
Performance/test obligations не ослаблены; повторной обработки файлов/нового
runtime в блоке нет, ускорение не заявляется. Игровые тесты/builds UNRUN намеренно.

WIP mapping сохранён на `9a2cbc461f4e185b2365f94478e5361493b121b2`, полный SHA
совпал с remote, tasks.md прочитан обратно через GitHub. Финальная фиксация
содержит только этот итог и статус T022. Clean checkout restore не выполнялся;
проверена публикация, не отдельная процедура восстановления.
Создание Issues выполнено по прямому запросу автора, сообщения Астре не отправлялись,
назначения исполнителей не сделаны. Дальнейший первый шаг — #1561, не запуск
неутверждённого runtime. Рабочие исходники, тесты и диагностика в этом блоке не меняются.

## Обсуждение наград Хранителей — #1561, 2026-10-26

В spec зафиксировано новое предложение автора: доступ к изучению/первому уровню
духовного искусства за память и повышение его уровней за дополнительные цели.
Первичная трактовка выдачи, уже известное искусство, повтор/добор целей и числа
открыты. Это продолжающийся T005/D3, не утверждение exact reward contract и не
смена q4 closure. После согласования обновить применимые data-model/contracts/
tasks и GitHub scopes #1566/#1567 перед production. Сейчас изменены только
spec и этот checkpoint; код/тесты/существующие правила выдачи не меняются.

Предложение сохранено на `7276d09ef568b83f8f772f157c3971fca8c018f0`; remote SHA
проверен. Отдельный Sol 6.1 XHigh проверил 29 строк diff относительно `81398c5e9`:
материальных замечаний нет, открытые решения не выданы за утверждённые.
Отдельный Sol 6.1 High выполнил read-only аудит: learning receipt сейчас выдаёт
tier 0, явного q4 gate в learning validator/application нет; обычное улучшение
ограничено tier 5 и собственными rank/cost gates. Это ограничения для обсуждения,
не разрешение менять их. Проверка методов не является gameplay/replay тестом.
Стабильные production DATA и cache path в этом документальном блоке не менялись;
runtime/измерения UNRUN, ускорение не заявляется. T005/#1561 остаётся открытой.

Уточнение автора 2026-10-26: повторное прохождение разрешено для добора
пропущенных целей, награда за каждую цель только один раз. Это решение внесено
в FR-20/AC-09/D2, data model, gameplay contract, research, quickstart и задачи
T008/T009/T014/T020. Учёт выдачи не сбрасывается новой attempt; первое получение
за новую цель отличается от запрещённого повторного grant. Exact goal identity,
момент принятия и численные награды остаются в обсуждении #1561. Сценарные идеи
в разговоре не становятся утверждённым каноном автоматически. Production UNSTARTED.

Решение о replay сохранено на `afa36f44349e8a81b1256b461274dabc825bad35`,
remote SHA проверен. Отдельный Sol 6.1 XHigh проверил полный семидокументный diff
от `d9ec7307d`: материальных замечаний нет; прежняя формулировка «replay optional»
удалена, первая награда за новую цель и повторный grant различены. Diff check
прошёл; runtime/tests UNRUN, production caching/performance не меняются.

Текущее продолжение #1561: автор подтвердил личный спор Илариона с Сарефом
как кульминацию его памяти. Решение отражено в spec; роли в разговоре, точные
действия/реплики/развилки ещё обсуждаются. Это не утверждение полного сценария
или разрешение на runtime. Содержание текущих bibles/prompts и код не меняются.
Решение сохранено на `9b60c87f18aecb066052f64a8cdbbe3b671c8a34`, remote SHA
проверен; отдельный Sol 6.1 XHigh проверил 15 строк diff от `9b112cfa9`,
замечаний нет. Diff check прошёл; runtime/tests UNRUN, performance не меняется.

Текущий блок #1561, 2026-10-26, база `bbeaa054dde0895d6388fe3ae685926a26716262`:
в spec сохранено подтверждение права свидетеля отказаться от восстановления
памяти, предложение автора отклонять не соответствующие памяти/роли/сценарию
ходы с понятным объяснением, и уточнения к проверке целей/исторического канона.
Новая механика остаётся обсуждением T005, не одобрением реализации.
Следующий шаг — согласовать критерии целей и допустимые исходы конкретной памяти.
Начальные 40 строк сохранены на `56d02b3d772e7efc2c84f8220a649b464102a08e`;
remote SHA/spec readback проверены, отдельный Sol 6.1 XHigh замечаний не нашёл.
После нового предложения автора уточнена политика отклонения до изменения сцены;
это заменяет прежнюю идею завершать попытку при недопустимом действии.
Дополнение сохранено на `a1add0873004120009c32183d3c88f70552db794`;
remote SHA/spec readback проверены. Тот же отдельный Sol 6.1 XHigh проверил
дополнение и затронутые зависимости, замечаний нет. Parent проверил фактический
diff; `git diff --check` прошёл. Статус: запись обсуждения проверена,
полная спецификация реализации ещё не утверждена. Проверена согласованность
с gameplay contract; игровые tests/builds UNRUN, production/prompts не меняются.
Обработка файлов и кэширование runtime не затронуты, измерения не требуются.

Продолжение того же обсуждения, база `3b053f0025f81b413363fe475e887f7ec0dba65a`:
автор согласовал механизм допуска/отказа и пояснения из ответа. Принятое правило
отражено в FR-06, gameplay contract и проверках T014/T019–T021; точные цели,
границы каждой роли и полная спецификация реализации остаются открытыми T005.
Следующий предмет обсуждения — цели, ограничения и исход памяти Илариона,
затем уровни наград. Синхронизация сохранена на
`01147a275d7016e64610a5e0c029ea75db9c043e`; remote SHA и spec readback проверены.
Отдельный GPT-6.1 Sol XHigh проверил четырёхфайловый diff относительно базы;
замечаний нет. Parent проверил diff, `git diff --check` прошёл. Это проверенная
запись принятого правила, не подтверждение работающей механики или закрытие T005.
Код/prompts/runtime не меняются; игровые тесты и замеры UNRUN, кэш не затронут.

Следующее уточнение #1561, база `7fcd8f78100666848590f4f6ce74b6cced933d57`:
автор одобрил центральную подмену Илариона — просьбу о защите Сареф превратил
в разрешение удалить показания. Свидетель благодарен за исчезновение страха;
Иларион сохраняет доказательство отсутствия согласия, не навязывая восстановление.
Решение записано в spec; конкретные цели/источники/награды ещё обсуждаются.
Следующий шаг — дополнительные цели и уровни искусства. Запись сохранена на
`e61ffea23a34b818fc438c7ad358003623947d8d`; remote SHA и spec readback проверены.
Отдельный GPT-6.1 Sol XHigh проверил 19 добавленных строк и соседние решения;
замечаний нет. Parent проверил diff. Одобрена подмена, полная спецификация открыта.
Проверка: `git diff --check`; runtime/prompts/кэш не изменены, tests/builds/замеры UNRUN.

Продолжение #1561, база `ef4913d475845f27de0c34704ad0c96d5e1fd228`: автор
выбрал обе дополнительные цели Илариона и запретил первое изучение искусства
Хранителя до прохождения его памяти. Правило внесено в spec/gameplay/tasks,
включая все пути выдачи и отделение исторической роли от нынешнего игрока.
Числа уровней/награды для уже развитого искусства ещё обсуждаются, T005 открыт.
В spec помечен неутверждённый вариант распределения 1 + 2 обучением + 2 целями;
он сохраняет награды при любом порядке, но требует целей для достижения максимума.
Ограничения ранга и альтернатива с компенсацией остаются явными открытыми решениями.
Read-only аудит отдельного Sol 6.1 High и просмотр parent подтвердили: learning
receipt сейчас создаёт tier 0 (`AfterlifeEntityProfileState:2175`), UI показывает
число буквально и разрешает обычную прокачку до 5 (`ExplorerMode.Afterlife.SpiritualConflict:1089`).
Учитель — отдельный путь (`TrainingService:1335`); обучение сохраняет фактические
затраты в receipts (`TrainingService:1982`), но полнота истории каждого save не доказана.
Нельзя просто повторить learning receipt как награду уже развитому искусству:
он клонирует исходное искусство с tier 0 и Upsert заменяет прежнюю запись.
Это выводы по исходникам, не выполненный игровой тест. Serena C# symbolic access ready.
Запись сохранена на `4f4d877fb99d0420bc06fb0e7979cc7b110e97e9`; remote SHA
и spec readback проверены. Отдельный GPT-6.1 Sol XHigh проверил четырёхфайловый
diff и связанные источники: найдено только форматирование формулы как списка.
Исправление и границы списка целей повторно проверены тем же ревьюером,
остаточных замечаний нет. Parent проверил diff; `git diff --check` прошёл.
Приняты цели/gate; схема повышения остаётся предложением, T005 не закрыт.
Код/prompts/кэш не менялись, tests/builds/замеры UNRUN.

Последнее решение #1561, база `b079cae549abeb9d8853ad10dfb5e40de9e47daa`:
автор выбрал резервирование двух повышений за целями Илариона. Схема 1 + 2
обучением + 2 целями принята; обычное обучение не заменяет награды и максимум
требует обеих целей. Уточнены spec/gameplay/tasks и проверки разных порядков.
Правило не распространено молча на остальных Хранителей. Следующее решение —
момент принятия наград и недостаточный ранг; T005 открыт. Запись сохранена на
`abc8e8b727db20a6b85d86b9e531277a46dc713d`; remote SHA и spec readback проверены.
Отдельный GPT-6.1 Sol XHigh проверил четырёхфайловый diff: замечаний нет.
Parent проверил diff; `git diff --check` прошёл. Принята схема Илариона,
полная спецификация реализации открыта. Код/prompts/кэш не меняются,
tests/builds/замеры UNRUN.

Текущий блок #1561/D3, база `860537bf95f3969fa92046c5990a87c889b1d83a`:
автор выбрал общий ранговый допуск для наград. Spec/gameplay/tasks обновлены;
пять конкретных уровней «Якоря» и цена 2 ОД записаны как неутверждённое предложение.
Read-only аудит Sol 6.1 High и parent отделил существующие roll modes/ward/guard
от отсутствующего точного memory-target/consent/consumption consumer. Связь с
#1571 и предел M01 зафиксированы в research; исходники Астры не меняются.
Следующий шаг — рассмотрение таблицы уровней, затем точный D3 и его проверки.
WIP записи ожидает ревью. Production/prompts/кэш не изменены; tests/builds/
измерения UNRUN, новые повторные вычисления не введены. Для будущего runtime
описаны reuse definitions через существующий сервис и свежие authority checks.
