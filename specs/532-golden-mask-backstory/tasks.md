# Задачи: Золотая Маска, воспоминания и Новый Порядок

Вход: [spec](spec.md), [plan](plan.md), [research](research.md).
2026-10-26: исследование/проектирование; production-работа не начата.
T005 — обязательный gate: до него следующие задачи не исполняются.
После рассмотрения уточнённой spec, деталей D2 и D3/D4 каждый срез конкретизируется до exact schema/signatures,
определений и test assertions; этот список пока не является executable планом.
Источник задач: #532 / #1519–#1522 / #1514, ссылки и статус в plan/research.
GitHub execution epic: [#1560](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1560).
Номера ниже — каноническое соответствие Spec Kit и Issues, не второй backlog.

## Phase 1 — исследование и проектирование

- [x] T001 Сверить код и GitHub-зависимости, зафиксировать реальные границы трёх систем в `specs/532-golden-mask-backstory/research.md`.
- [x] T002 Зафиксировать исходный запрос, US1–US3 и первичные FR/AC в `specs/532-golden-mask-backstory/spec.md`, отделив предложения от согласованного канона; новое расширение учитывается в T018.
- [x] T003 Через Spec Kit подготовить проект `plan.md`, `data-model.md`, `contracts/gameplay.md`, `quickstart.md` и этот `tasks.md` в `specs/532-golden-mask-backstory/`; отметить открытые gates.
- [x] T004 Проверить согласованность и независимым контекстом проверить первоначальный diff `cbd417223..e2b42e268`; findings/ограничения в `specs/532-golden-mask-backstory/plan.md`. Не заменяет ревью T018.
- [x] T018 Исследовать уточнённые автором explorable memories для обоих источников и POV владельца: navigation/cast/GM/turn/scheduler/history/recovery и все десять q4. Обновить семь документов этой feature до FR-01–21/AC-01–10; провести consistency и независимое ревью нового блока, опубликовать проверенный remote checkpoint. Evidence: `43205fcb7..ef9b05707`, review/checkpoint в plan; только investigation/documentation, без runtime.
- [x] T022 По запросу автора оформить полный пакет GitHub-задач #1560–#1570 с сюжетной важностью, native hierarchy/dependencies и связями с прежними story issues; зарегистрировать отдельный follow-up #1571 из #1536/T148. Evidence: 12/12 remote body/label readbacks, 10 native children, 34 exact dependencies, Sol 6.1 XHigh review и checkpoint в plan. Никакой runtime-реализации или смены статусов чужих задач.

## Phase 2 — условия реализации

- [ ] T005 Получить рассмотрение точной версии `specs/532-golden-mask-backstory/spec.md`, деталей D2 и решений D3/D4 (D1 и основной формат D2 приняты); закрепить tracker расширения, согласовать изменения #1514/#1522, конкретизировать schema/signatures/награду/первую пару сцен и повторить consistency check.
- [ ] T006 Свести зависимости #1520/#1521/#1522 с новым разделом каталога: `BookOfEternityClient/story_content/saref/catalog.json` (планируемый), `BookOfEternityClient/Services/ActorMaterializationContract.cs`, `BookOfEternityClient/Services/SarefMainStoryState.cs`; разрешить 9/10, mercy_wound и deep-victory расхождения, обновить точные bootstrap/fixtures без legacy fallback.

T006 не объявляет весь открытый epic уже выполненным. Перед работой проверить
его фактическую ветку/SHA и интегрировать нужный принятый baseline. Сначала
написать failing catalog/identity tests; только затем менять контракт.

В T005/D3–D4 включить авторское предложение о стартовой непродаваемой
«Расколотой маске», бонусах интеллекта/убеждения в смертных мирах и открытии
личного раздела после первого успешного воспоминания любого Хранителя.
Автор уточнил: бонусы только при экипировке, stored и снятие их не дают;
не переносить это условие автоматически на открытие/запуск воспоминаний.
До реализации уточнить owner/lifetime, числа эффектов, первую личную сцену
и дальнейший доступ; не закрывать T007/T009 от появления предмета.
Принято последовательное открытие личных эпизодов. Для T007/T008/T009 проверить:
пробуждение→первый; успех текущего→следующий; optional цели не блокируют;
пауза/неудача не открывают следующий; replay не пропускает непройденный эпизод,
не отменяет прежний доступ и не удваивает награду; после последнего новых нет.
Первым выбран эпизод «Убежище для потерянных» до призыва Сарефа. Его полный
сценарий/награда и оставшийся состав цепочки ещё не утверждены; T013/T021
используют будущий согласованный definition, а не одно название.
Для T005/T013/T021 обновлён [проект первого эпизода](scenarios/golden-mask-01-refuge.md).
Подача ремонта убежища отклонена автором; прежние E1–E5/M1–M3/O1–O2 и развязки
архивные, не требования новой сцены. Новое авторское направление — Маска со
спутниками защищает беглецов от банды в Море Хаоса, показывает превосходящую
силу. Автор выбрал сильные боевые оковы с затруднённым сопротивлением; согласовать
точные baseOperation/особый эффект (предложены force_binding и Помеха разрыву
конкретной связи; универсальная «Длань» отозвана), затем конкретные
цели, исход, цену/уровни/награду и её потребители. Сохранить раннее стремление
к контролю, самостоятельные действия игрока, finite power и прежние общие
правила воспоминаний. Эти design-решения ещё не закрывают runtime-задачи.

## Phase 3 — US1: прошлое и раскрытие (P1)

Самостоятельная проверка: один точный trace и два порядка узнавания личности;
остальные акторы не узнают её автоматически, q1–q3 не становятся экспозицией.

- [ ] T007 [US1] Сначала добавить `BookOfEternityClient.Tests/GoldenMaskKnowledgeTests.cs` (новый): source provenance, no-spoiler projections, оба порядка встречи, q4 isolation; затем реализовать `BookOfEternityClient/Services/GoldenMaskStoryState.cs` (новый) и связанные Saref validator/normalizer/projection consumers, prompt/example parity.

## Phase 4 — US2: первая сцена и реальная сила (P1)

Самостоятельная проверка: из минимальных независимых fixtures через оба клиента
исследовать память Маски и один q4: два места, осмотр, разговор с памятью,
историческая способность, pause/resume и source-specific завершение/награда.

- [ ] T008 [US2] Сначала добавить `BookOfEternityClient.Tests/MemorySceneSessionTests.cs` (новый): один active owner, несколько paused, A→B→A, revision/definition mismatch, failed retry, конфликтующие действия и source-specific closure; затем `Services/MemorySceneSessionService.cs`, `Services/Validation/ValidationService.MemoryScenes.cs` (новые внутри BookOfEternityClient) и новый `game_state/meta/memory_scene_state.json` owner. Перевести story.memoryScene в reference/projection, обновить bootstrap/allowlists/save/restore без legacy fallback.
- [ ] T019 [US2] Сначала `BookOfEternityClient.Tests/MemorySceneExplorationTests.cs` (новый) и targeted frontend fixtures: карта/место/cast/role, move/inspect/talk, no future imagery, malformed context и stale location/actor refusal. Затем новые `Services/PlayerSceneContextResolver.cs`, `Services/MemoryScenePlayerProjection.cs`, `UI/ExplorerMemorySceneCommandResultBuilder.cs`; подключить map/scope, console/browser routing/menu/GameScreen до ordinary refresh. Исправить mutating start alias, сохранить read-only dossier и map selection; movement — отдельное принятое действие. Имена внутри BookOfEternityClient предложены.
- [ ] T020 [US2] Сначала `BookOfEternityClient.IntegrationTests/MemorySceneTurnIsolationTests.cs` (новый) на настоящий staging/accepted publication/recovery: snapshot binding, scene transcript, сохранённый backlog, отсутствие current-world ticks/изменений, rejected forbidden writes, pause/retry/stale recovery. Затем memory policy в `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`, `Services/ProgressionScheduleService.cs`, `Services/StoryService*.cs` и validators/snapshot inventory. В том же срезе `game_master_daemon.ps1` context-pack/normal/repair, `Launcher/GM_Turn_Helper.ps1` и examples/source guards: exact allowed effects, не один дополнительный prompt.
- [ ] T021 [US2] Сначала `BookOfEternityClient.Tests/MemorySceneActorContextTests.cs` (новый): POV владельца, historical knowledge/actor journals, разговор→перемещение→возврат, способность роли с локальным ресурсом и отсутствие present profile mutation. Затем scene-owned actor context и typed mechanics input/adapters; подготовить одну утверждённую память Маски и один Guardian q4 в story catalog. Полный бой включать по принятому definition, не заявлять готовым от наличия описания abilities; не материализовать исторических акторов в настоящих registries.
- [ ] T009 [US2] После точного D3 написать `BookOfEternityClient.Tests/GoldenMaskMemoryRewardTests.cs` и `BookOfEternityClient.IntegrationTests/GoldenMaskMemoryPublicationTests.cs` (новые): реальный effect/cost, rejected/recovery/replay, grant только один раз и отказ до него; затем `BookOfEternityClient/Services/GoldenMaskMemoryRewardService.cs` (новый), profile grant route и реальный spiritual-conflict consumer. Связать completion+art одной существующей публикацией, без фиктивного учителя.

Каждая T007–T009/T019–T021 включает RED→GREEN, relevant selected regressions и worked
example; T009 завершает первый вертикальный срез. Не отмечать T008/T009
по результату одного read-only меню или теста, лишь повторяющего реализацию.

## Phase 5 — US3: посещаемый Новый Порядок (P2)

Отдельный realm выбран автором; точный location/travel contract зависит от D4.

- [ ] T010 [US3] Сначала `BookOfEternityClient.Tests/NewOrderRealmTests.cs` (новый) на все затронутые классификаторы/menus/trade/repair и отказ Mortal fallback; затем `RealmSemantics.cs`, `Models/GameState/AggregatedGameState.cs`, профиль/ресурсные владельцы и точные worker/segregation consumers в BookOfEternityClient, без broad write whitelist.
- [ ] T011 [US3] Сначала `BookOfEternityClient.IntegrationTests/NewOrderTravelTests.cs` (новый): entry/return, suspended/resolved constraints, crash/replay, сохранность incarnation/progression/history и недействительный return context; затем `BookOfEternityClient/Services/NewOrderState.cs` и `BookOfEternityClient/Core/GameEngine/GameEngine.NewOrder.cs` (новые) через прежний publication owner без reset.
- [ ] T012 [US3] Сначала минимальные presence/purchase tests в `BookOfEternityClient.Tests/LocalInteractionScopeServiceTests.cs` и новом `BookOfEternityClient.Tests/NewOrderTradeTests.cs`; затем расширить `Services/LocalInteractionScopeService.cs`, `Services/ShiningTradeService.cs`, map/command/browser consumers внутри BookOfEternityClient: один Saref/Wings, exact location, fresh stock/cost/locality, одна атомарная покупка и отказ stale offer. Согласовать location authority с #1514.

## Phase 6 — полное содержание и последствия

- [ ] T013 [US1] После рабочего среза внести утверждённые свидетельства/индивидуальные реакции в `OtherGuides/Saref_Character_Bible.md`, десять `OtherGuides/Saref_Guardian_Questlines/*.md` и exact catalog: сохранить 40 историй, агентность Хранителей, последовательность переворот→сердце и необязательность личной линии; проверить source/content guards.
- [ ] T014 [US2] Наполнить утверждённые definitions/rewards в планируемом `BookOfEternityClient/story_content/saref/`, переработать все десять `OtherGuides/Saref_Guardian_Questlines/*.md` под POV самого Хранителя (цели, abilities, узлы, места/cast), синхронизировать `OtherGuides/Saref_Memory_System_Boundaries.md` и GM Examples. Проверить fixed events, разрешённый выбор, неудачу, source-specific reward/prerequisites; отдельно исключить неявную цену нынешнего игрока у Илариона/Вейры. Полное содержание не подменяет рабочий срез T021.
- [ ] T015 [US3] В `BookOfEternityClient/Services/NewOrderState.cs`, Saref ending validator и `OtherGuides/Afterlife_Contract_Matrix.md` реализовать утверждённые последствия каждого финала/связанной души: судьба убежища отдельно от Крыльев, правила доступа и возвращения, сохранение доступа к уже открытым личным воспоминаниям; сначала negative/round-trip fixtures.

## Phase 7 — общая проверка срезов

- [ ] T016 Проверить фактический DATA reuse и invalidation через существующий `BookOfEternityClient/Services/OperationValidationService.cs`; независимые счётчики источника/копии/failure/dispose в новых `BookOfEternityClient.Tests/GoldenMaskOperationReuseTests.cs`, измерения настоящих операций, краткие результаты в `specs/532-golden-mask-backstory/plan.md`. Выполняется также внутри каждого среза, не откладывает performance до финала.
- [ ] T017 Сверить весь утверждённый diff с FR/AC; обновить `tests/categories.json`, `tests/selection.json`, worked manifest, prompt/source guards, выполнить выбранные проверки и browser UI evidence, независимое ревью и checkpoint в `specs/532-golden-mask-backstory/plan.md`; публиковать только исходники/документацию.

## Зависимости и параллельность

T001–T004 + T018 → T005 → T006 → T007 → T008 → T019/T021 → T020 → T009.
T019/T021 можно делать независимо после фиксации общего scene contract T008;
T020 объединяет их в настоящий ход. T009 завершает награду и первый полный путь.
Уточнение автора для T008/T009/T014/T020: replay разрешает добор пропущенных
целей; каждая цель награждается однократно за все прохождения. Сохранить учёт
при retry/reload/новой attempt; включить этот случай в source-specific grant tests.
Уточнение автора для T014/T019–T021: реализовать проверку допуска каждого ввода
и отказ целиком без игровых эффектов с понятной причиной. Проверить неизменность
сцены/времени/ресурсов/NPC/целей/наград после отказа, повторный ввод и выход,
допустимую импровизацию, попытку убеждения без гарантии исхода, ошибки GM и
отсутствие спойлеров в отказе. Смысловые случаи требуют сценарных примеров;
структурные проверки не доказывают произвольную прозу. Exact contract — через T005.
Уточнение автора для T009/T014: первое изучение искусства Хранителя доступно
только после его памяти. Проверить все пути learning/grant/upgrade, обход через
технический tier 0, отсутствие повторного начального grant/понижения на replay
и независимость сценической способности роли. Для Илариона выбраны цели
«Вернуть свидетельству имя» и «Оставить выбор свидетелю»; отказ свидетеля допустим.
Для Илариона принята схема: 1 за завершение + до 2 повышений обучением + по 1
за две цели, максимум 5. Проверить обучение до/между/после наград целей, общий
лимит двух обычных повышений, отсутствие потери награды и повторного grant.
Ранговый допуск принят общим: проверить сохранение заработанного повышения
при недостаточном ранге и применение после его достижения без повторной оплаты/
прохождения; порядок обучения/целей не даёт более раннего обхода ранга.
Автор утвердил выдачу наград только после успешного завершения памяти, включая
replay. В T009/T014/T020 проверить отсутствие ранней выдачи, pause/resume и
неудачный replay без потери старых rewards; семантика provisional целей после
окончательного отказа принята для Илариона вместе со сценарием v1.
Exact activation contract и машинное представление proof целей согласовать в T005.
Утверждённый [сценарий Илариона v1](scenarios/ilarion-memory.md) покрывает исследование,
E1–E6, M1–M3/O1–O2, четыре успешные развязки, отказ/повтор и план проверки.
Автор подтвердил весь сценарий, отдельно отказ Нерина и финальное столкновение
с Сарефом. Это принятый вход для T005/T014/T021, не выполненная runtime-задача;
полная спецификация feature и её технические контракты ещё не утверждены.
Пять эффектов «Якоря» и цена 2 ОД приняты в spec. В T009/T014 предусмотреть
проверки каждого отличия, нецелевой атаки, согласия/замены якоря и невозможности
восстановить страховку уровня 5 повторным наложением/reload/retry. Реализация
требует рассмотрения полной спецификации и точных source/target/lifetime/consumer
контрактов; описательные combatEffect/effectNote не являются исполнением.
T010–T012 после решения D4 и нужного catalog/actor/location baseline;
минимальный memory срез не зависит от наличия нового realm.
T013/T014 после T009; T015 после T012 и утверждения outcomes.
T016 сопровождает изменяемые срезы; T017 замыкает принятый объём.

Сейчас независимые read-only аудиты realm и memories шли параллельно.
После фиксации контракта возможно параллельно писать отдельные определения
сцен и UI-проекции, если нет общих изменяемых файлов. Архитектурные изменения
state/validator/publication должны иметь одного владельца; ревью — отдельный.

## Покрытие требований

### GitHub ↔ Spec Kit

| Issue | Объём | Задачи |
|---|---|---|
| [#1560](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1560) | Эпик: Маска, память, Новый Порядок | Полный принятый объём |
| [#1561](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1561) | Точные решения/контракты, авторское рассмотрение | T005, согласование T006 |
| [#1562](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1562) | Прошлая личность, свидетельства, реакции | T006 (интеграция), T007, T013 |
| [#1563](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1563) | Единый scene owner, pause/save/resume | T008 |
| [#1564](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1564) | Исследование, оба клиента, historical actor/role | T019, T021 (контекст) |
| [#1565](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1565) | Настоящий GM turn, границы настоящего, pilot integration | T020, интеграция T021 |
| [#1566](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1566) | Постоянное искусство и атомарный grant | T009 |
| [#1567](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1567) | Десять q4 и личные сценарии | T014, содержание T013/T021 |
| [#1568](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1568) | Отдельный realm и visit round-trip | T010, T011 |
| [#1569](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1569) | Presence, торговля, последствия финалов | T012, T015 |
| [#1570](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1570) | Сквозная приёмка | T016, T017; проверки обязательны и внутри каждого среза |

Отдельная [#1571](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1571)
фиксирует runtime memory_suppression из SOURCE-аудита #1536/T148. Она связана
с эпиком, но не дочерняя задача playback и не реализация Элиариного mercy_wound.
Её регистрация не закрывает #1536/T148/E01 и не меняет план текущего исполнителя.
Старые #532/#579–#584 и последующие display/audit задачи сохраняют свои статусы;
новые issues описывают обнаруженные пробелы и согласованное расширение.

### FR и задачи

| Требования | Задачи |
|---|---|
| FR-01, FR-04 | T007, T013 |
| FR-02 | T006, T007, T008, T013 |
| FR-03 | T007, T008, T014 |
| FR-05, FR-06, FR-07 | T008, T019–T021, T014 |
| FR-08, FR-09, FR-10 | T009, T014 |
| FR-11 | T010, T011 |
| FR-12, FR-13 | T012, T015 |
| FR-14 | T006–T015, T019–T021 (в каждом срезе), T017 |
| FR-15 | T016 и проверки каждого среза |
| FR-16 | T013, T015 |
| FR-17 | T019, T021 |
| FR-18 | T019, T020, T021 |
| FR-19 | T020, T021 |
| FR-20 | T008, T019, T020 |
| FR-21 | T006, T014, T021 |

AC-01→T007/T013; AC-02→T008/T019–T021; AC-03→T009;
AC-04→T008/T009/T020; AC-05→T010–T012; AC-06→T013–T017;
AC-07→T019/T021; AC-08→T020/T021; AC-09→T008/T019/T020;
AC-10→T014/T021. Полная проверка принятого объёма — T017.

Все implementation boxes остаются пустыми. Покрытие задачами не является
ни подтверждением поведения, ни заменой G1/G2 в plan.
