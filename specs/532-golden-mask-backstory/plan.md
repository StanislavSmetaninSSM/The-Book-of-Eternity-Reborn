# Золотая Маска — исследование и проект плана реализации

> Для исполнителя: после утверждения спецификации применять
> `superpowers:executing-plans` либо согласованный subagent-driven workflow;
> TDD, независимое ревью и verification-before-completion сохраняются.

**Дата:** 2026-10-26. **Ветка:** `story/golden-mask-backstory-20261025`.
**Spec:** [spec.md](spec.md). **Цель:** включить прошлое Маски, игровые
воспоминания с реальными наградами и выбранную форму Нового Порядка.
**Статус:** Phase 0 исследована; Phase 1 — проект для выбора автором D1–D4.
Это последовательность реализации и проверки, не разрешение её начать.
Открытые контракты не считаются решёнными из-за наличия plan/tasks.

## Технический контекст

C#/.NET 8, System.Text.Json, файловое canonical state, Spectre.Console,
общий CommandProtocol, локальный browser host, React/TypeScript.
Целевые платформы Windows/Linux. Новые облачные сервисы не нужны.
Публикация через существующий accepted-turn/owned-resource механизм;
общие Service/Repository/OperationValidation кэши. Новая транзакционная система
или движок сценариев не требуются.

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
| D1 | Отдельный New Order realm, компактное наполнение | Подтвердить realm вместо Shining hall; расширение #1514/#1522 |
| D2 | Диалоговые исторические сцены через обычный GM turn | Подтвердить отсутствие отдельного боевого профиля в первом срезе |
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
2. **Lifecycle сцены.** Новый `Services/MemorySceneSessionService.cs` владеет
   переходами source/attempt/status. Совместная проверка envelope в
   `Services/Validation/ValidationService.MemoryScenes.cs`, source-specific
   q4 остаётся в Saref validator. Расширить top-level allowlists и bootstrap.
   Команды в `CommandProtocol/ExplorerCommandCatalog.cs`; общий command result,
   `UI/ExplorerMode/ExplorerMode.Afterlife.MemoryScene.cs`, browser write/menu
   через существующие command/session pipelines.
3. **Награда.** Новый `Services/GoldenMaskMemoryRewardService.cs` готовит
   exact detached after-images story/profile для существующей публикации.
   `AfterlifeEntityProfileState.cs` получает честный memory restoration route;
   `ValidationService.AfterlifeSpiritualConflict.cs` и связанный расчёт проверяют
   конкретный эффект зарегистрированного искусства. Reward consumer и tests
   входят в тот же срез, что выдача.
4. **Пространство, если D1=realm.** Новый `Services/NewOrderState.cs` хранит
   только собственное состояние места/сердца/посещения; actors/factions не
   дублируются. Расширить `RealmSemantics.cs`, `Models/GameState/AggregatedGameState.cs`,
   `AfterlifeEntityProfileState.cs`, resource owner, `LocalInteractionScopeService.cs`,
   `LocalMapViewService.cs`. Выделить visitor transition в partial
   `Core/GameEngine/GameEngine.NewOrder.cs`, сохраняя прежние death/incarnation
   и New Cycle эффекты только у их собственных операций.
5. **Доступ и торговля.** Узкий resolver присутствия единственных Saref/Wings,
   общая покупка с detached offer DATA и fresh locality/price/stock checks;
   не копировать целиком `ShiningTradeService`. Расширить
   `ExplorerRealmTradeCommandResolver.cs`, browser menus, story/realm validators,
   repair realm models/authority/apply gate. Отключённые Shining-возможности
   должны иметь явную причину недоступности, а не падать в Mortal route.
6. **Содержание и договорённости GM.** `OtherGuides/Saref_Character_Bible.md`,
   десять `Saref_Guardian_Questlines/*.md`, `Saref_Memory_System_Boundaries.md`,
   `Afterlife_Contract_Matrix.md`, релевантные Rules/TaskGuides/Examples,
   prompt construction в `GameEngine.TurnLifecycle.cs`, manifest/source guards.

## Порядок законченных срезов

**A. Утвердить контракты и зависимости.** D1–D4, catalog baseline #1520,
точные actor/faction #1521/#1522, afterlife locations #1514; разрешить
9/10, mercy_wound и deep-victory расхождения. Не ждать независящих GM-workers.

**B. Первый рабочий путь US1+US2.** Один проверяемый источник знания, одна
личная сцена, роль/узлы, start/pause/resume, одно exact искусство с реальным
действием. RED → минимальная реализация → GREEN → отдельный accepted-turn
и recovery test. Сначала доказать весь путь, затем наполнять оставшиеся сцены.

**C. US3: посещаемое пространство.** Классификация и explicit visitor transition,
одно место, единые actors/faction, одна покупка, возврат без reset, отказ
старого предложения и сохранение состояния при прерывании. Shining hall
альтернатива меняет этот срез после D1, а не сохраняет ненужные realm-задачи.

**D. Полное содержание и последствия.** Остальные утверждённые сцены,
индивидуальные реакции Хранителей, разные порядки раскрытия, состояние
убежища/населения/Крыльев после разных финалов. Не переписывать q1–q3 в
прямую экспозицию. Отдельно доказать отсутствие обязательной личной линии.

Каждый срез включает его prompt/doc/example parity, targeted tests, performance
assessment, независимое ревью, commit/push/checkpoint. Не откладывать реальные
механические эффекты и валидацию до последнего этапа.

## Проверка и внимание ревью

Пять наиболее опасных случаев:
1. Секретная команда/карта/карточка награды открывает ещё неизвестную личность.
2. Personal scene закрывает q4 или даёт route fragment без линии Хранителя.
3. Retry/recovery выдаёт награду дважды либо завершение остаётся без искусства.
4. Временная роль меняет настоящее имя/облик/профиль, а New Order return сбрасывает прогресс.
5. Старая карточка торговли/доступа действует после смены realm, владения или финала.
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
definition/story/profile/generation; failure/dispose не оставляют пригодного hit.
Массовый кэш на всю игровую сессию и отдельный Dictionary запрещены.
Runtime-замеры в этом блоке UNRUN: меняются документы, ускорение не заявляется.

## Точка продолжения

2026-10-26: source/GitHub исследование завершено; D1–D4 представлены как
выборы, не готовые решения. Spec Kit setup-plan выполнен, документы проектирования
подготовлены. Следующий шаг — авторское рассмотрение spec, затем конкретизация
контрактов/задач и повторный consistency gate перед implementation.
Независимое ревью и результаты проверки этого документального блока записываются
здесь после выполнения; их наличие не означает утверждения игровых решений.

Первичная проверка согласованности: 3 user stories, 16 требований,
17 задач; все 16 требований имеют задачи, implementation остаётся UNSTARTED.
G1 блокирует исполнение: D1–D4 не утверждены и точные contracts не завершены.
G2 блокирует соответствующие срезы: открытые #1520–#1522/#1514 и найденные
расхождения каталога должны быть разрешены. Для исследовательского результата
это явно обозначенные решения, не скрытые допущения.
Проверены существование spec/plan/tasks, уникальность IDs и относительные ссылки
семи документов. В установленном check-prerequisites нет -RequireSpec;
spec проверен отдельно. Его branch-name check, в отличие от setup-plan/tasks,
не учитывает закреплённую feature: проверка запускается с документированным
process-only `SPECIFY_FEATURE=532-golden-mask-backstory`, реальная Git-ветка
остаётся `story/golden-mask-backstory-20261025`. Скрипты проекта не изменялись.
Необязательные git/agent-context hooks не выполнялись автоматически;
публикация только точных feature-файлов выполняется обычным checkpoint-путём.
