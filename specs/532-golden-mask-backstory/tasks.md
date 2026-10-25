# Задачи: Золотая Маска, воспоминания и Новый Порядок

Вход: [spec](spec.md), [plan](plan.md), [research](research.md).
2026-10-26: исследование/проектирование; production-работа не начата.
T005 — обязательный gate: до него следующие задачи не исполняются.
После выбора D1–D4 каждый срез конкретизируется до exact schema/signatures,
определений и test assertions; этот список пока не является executable планом.
Источник задач: #532 / #1519–#1522 / #1514, ссылки и статус в plan/research.

## Phase 1 — исследование и проектирование

- [x] T001 Сверить код и GitHub-зависимости, зафиксировать реальные границы трёх систем в `specs/532-golden-mask-backstory/research.md`.
- [x] T002 Зафиксировать исходный запрос, US1–US3, FR-01–16 и AC-01–06 в `specs/532-golden-mask-backstory/spec.md`, отделив предложения от согласованного канона.
- [x] T003 Через Spec Kit подготовить проект `plan.md`, `data-model.md`, `contracts/gameplay.md`, `quickstart.md` и этот `tasks.md` в `specs/532-golden-mask-backstory/`; отметить открытые gates.
- [x] T004 Проверить согласованность и независимым контекстом проверить полный документальный diff; зафиксировать findings/ограничения в `specs/532-golden-mask-backstory/plan.md`.

## Phase 2 — условия реализации

- [ ] T005 Получить решения D1–D4 и утверждение точной версии `specs/532-golden-mask-backstory/spec.md`; закрепить tracker расширения, согласовать изменения #1514/#1522, конкретизировать schema/signatures/награду, повторить consistency check.
- [ ] T006 Свести зависимости #1520/#1521/#1522 с новым разделом каталога: `BookOfEternityClient/story_content/saref/catalog.json` (планируемый), `BookOfEternityClient/Services/ActorMaterializationContract.cs`, `BookOfEternityClient/Services/SarefMainStoryState.cs`; разрешить 9/10, mercy_wound и deep-victory расхождения, обновить точные bootstrap/fixtures без legacy fallback.

T006 не объявляет весь открытый epic уже выполненным. Перед работой проверить
его фактическую ветку/SHA и интегрировать нужный принятый baseline. Сначала
написать failing catalog/identity tests; только затем менять контракт.

## Phase 3 — US1: прошлое и раскрытие (P1)

Самостоятельная проверка: один точный trace и два порядка узнавания личности;
остальные акторы не узнают её автоматически, q1–q3 не становятся экспозицией.

- [ ] T007 [US1] Сначала добавить `BookOfEternityClient.Tests/GoldenMaskKnowledgeTests.cs` (новый): source provenance, no-spoiler projections, оба порядка встречи, q4 isolation; затем реализовать `BookOfEternityClient/Services/GoldenMaskStoryState.cs` (новый) и связанные Saref validator/normalizer/projection consumers, prompt/example parity.

## Phase 4 — US2: первая сцена и реальная сила (P1)

Самостоятельная проверка: из минимальной независимой fixture через оба клиента
начать/приостановить/продолжить сцену, завершить её и применить искусство.

- [ ] T008 [US2] Сначала добавить `BookOfEternityClient.Tests/MemorySceneSessionTests.cs` (новый): source-specific closure, один active owner, pause/reload/resume/failed retry, конфликтующие действия, неизменность личности/жизни; затем `Services/MemorySceneSessionService.cs`, `Services/Validation/ValidationService.MemoryScenes.cs` (новые внутри BookOfEternityClient), общие команды, console/browser и bootstrap/allowlists; сохранить правильный q4.
- [ ] T009 [US2] После точного D3 написать `BookOfEternityClient.Tests/GoldenMaskMemoryRewardTests.cs` и `BookOfEternityClient.IntegrationTests/GoldenMaskMemoryPublicationTests.cs` (новые): реальный effect/cost, rejected/recovery/replay, grant только один раз и отказ до него; затем `BookOfEternityClient/Services/GoldenMaskMemoryRewardService.cs` (новый), profile grant route и реальный spiritual-conflict consumer. Связать completion+art одной существующей публикацией, без фиктивного учителя.

Каждая T007–T009 включает RED→GREEN, relevant selected regressions и worked
example; T009 завершает первый вертикальный срез. Не отмечать T008/T009
по результату одного read-only меню или теста, лишь повторяющего реализацию.

## Phase 5 — US3: посещаемый Новый Порядок (P2)

Ниже вариант D1=realm. При выборе hall переписать эти задачи и связанные
критерии до исполнения, убрав ненужную поддержку третьего типа.

- [ ] T010 [US3] Сначала `BookOfEternityClient.Tests/NewOrderRealmTests.cs` (новый) на все затронутые классификаторы/menus/trade/repair и отказ Mortal fallback; затем `RealmSemantics.cs`, `Models/GameState/AggregatedGameState.cs`, профиль/ресурсные владельцы и точные worker/segregation consumers в BookOfEternityClient, без broad write whitelist.
- [ ] T011 [US3] Сначала `BookOfEternityClient.IntegrationTests/NewOrderTravelTests.cs` (новый): entry/return, suspended/resolved constraints, crash/replay, сохранность incarnation/progression/history и недействительный return context; затем `BookOfEternityClient/Services/NewOrderState.cs` и `BookOfEternityClient/Core/GameEngine/GameEngine.NewOrder.cs` (новые) через прежний publication owner без reset.
- [ ] T012 [US3] Сначала минимальные presence/purchase tests в `BookOfEternityClient.Tests/LocalInteractionScopeServiceTests.cs` и новом `BookOfEternityClient.Tests/NewOrderTradeTests.cs`; затем расширить `Services/LocalInteractionScopeService.cs`, `Services/ShiningTradeService.cs`, map/command/browser consumers внутри BookOfEternityClient: один Saref/Wings, exact location, fresh stock/cost/locality, одна атомарная покупка и отказ stale offer. Согласовать location authority с #1514.

## Phase 6 — полное содержание и последствия

- [ ] T013 [US1] После рабочего среза внести утверждённые свидетельства/индивидуальные реакции в `OtherGuides/Saref_Character_Bible.md`, десять `OtherGuides/Saref_Guardian_Questlines/*.md` и exact catalog: сохранить 40 историй, агентность Хранителей, последовательность переворот→сердце и необязательность личной линии; проверить source/content guards.
- [ ] T014 [US2] Наполнить утверждённые определения/награды в планируемом `BookOfEternityClient/story_content/saref/`, синхронизировать `OtherGuides/Saref_Memory_System_Boundaries.md` и GM Examples; для каждой сцены проверить fixed nodes, разрешённый выбор, допустимую неудачу, reward consumer и отсутствие морально обязательного одобрения прошлой тирании.
- [ ] T015 [US3] В `BookOfEternityClient/Services/NewOrderState.cs` (при D1=realm), Saref ending validator и `OtherGuides/Afterlife_Contract_Matrix.md` реализовать утверждённые последствия каждого финала/связанной души: судьба убежища отдельно от Крыльев, правила доступа и возвращения, сохранение доступа к уже открытым личным воспоминаниям; сначала negative/round-trip fixtures.

## Phase 7 — общая проверка срезов

- [ ] T016 Проверить фактический DATA reuse и invalidation через существующий `BookOfEternityClient/Services/OperationValidationService.cs`; независимые счётчики источника/копии/failure/dispose в новых `BookOfEternityClient.Tests/GoldenMaskOperationReuseTests.cs`, измерения настоящих операций, краткие результаты в `specs/532-golden-mask-backstory/plan.md`. Выполняется также внутри каждого среза, не откладывает performance до финала.
- [ ] T017 Сверить весь утверждённый diff с FR/AC; обновить `tests/categories.json`, `tests/selection.json`, worked manifest, prompt/source guards, выполнить выбранные проверки и browser UI evidence, независимое ревью и checkpoint в `specs/532-golden-mask-backstory/plan.md`; публиковать только исходники/документацию.

## Зависимости и параллельность

T001–T004 → T005 → T006 → T007 → T008 → T009.
T010–T012 после решения D1/D4 и нужного catalog/actor/location baseline;
минимальный memory срез не зависит от наличия нового realm.
T013/T014 после T009; T015 после T012 и утверждения outcomes.
T016 сопровождает изменяемые срезы; T017 замыкает принятый объём.

Сейчас независимые read-only аудиты realm и memories шли параллельно.
После фиксации контракта возможно параллельно писать отдельные определения
сцен и UI-проекции, если нет общих изменяемых файлов. Архитектурные изменения
state/validator/publication должны иметь одного владельца; ревью — отдельный.

## Покрытие требований

| Требования | Задачи |
|---|---|
| FR-01, FR-04 | T007, T013 |
| FR-02 | T006, T007, T008, T013 |
| FR-03 | T007, T008, T014 |
| FR-05, FR-06, FR-07 | T008, T014 |
| FR-08, FR-09, FR-10 | T009, T014 |
| FR-11 | T010, T011 |
| FR-12, FR-13 | T012, T015 |
| FR-14 | T006–T015 (в каждом срезе), T017 |
| FR-15 | T016 и проверки каждого среза |
| FR-16 | T013, T015 |

Все implementation boxes остаются пустыми. Покрытие задачами не является
ни подтверждением поведения, ни заменой закрытия D1–D4.
