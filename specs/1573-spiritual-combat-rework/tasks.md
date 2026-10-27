# Задачи #1573 — полный духовный бой

**Checkpoint v1.2:** обсуждается [полная модель v2](combat-system-proposal.md),
включая новые D7–D9. Ниже сохранён прежний implementation inventory, не готовая
декомпозиция нового кандидата. T001 требует утверждения всей текущей spec и
пересборки plan/tasks/contracts; ни одно старое задание не разрешено исполнять.

[Issue](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1573),
[spec](spec.md), [plan](plan.md), [gameplay](gameplay.md), [review](review.md).
Draft v1, 2026-10-27. Spec Kit setup-tasks выполнен для feature1573.
**Ни одна задача реализации не начата. T001 блокирует все T002–T028.**
Поправка v1.1: spatial model снята автором; [mortal comparison](mortal-combat-comparison.md)
содержит новую рекомендацию для выбора. T009–T015/T020–T022 описывают прежний
неутверждённый reaction candidate и требуют пересборки по решению D4; не исполнять
их как уже выбранную архитектуру. Formal movement budgets/zone engine удалены.
Новые filenames ниже — предлагаемые; существующие paths приведены явно.
US1=личное здоровье, US2=самостоятельные бойцы, US3=частичные исходы,
US4=единая работающая модель/интерфейсы.

## Gate и фундамент

- [ ] T001 Утвердить точную spec/актуальную gameplay-модель/arts-migration D1–D9 с автором; записать version/решения, актуальный опубликованный baseline #1535/#1536, владение общими файлами; пересобрать plan/tasks/contracts и обновить analysis в specs/1573-spiritual-combat-rework/review.md. Без этого нет production кода/балансного probe.
- [ ] T002 Зафиксировать исполняемый контракт в specs/1573-spiritual-combat-rework/data-model.md и contracts/commands.md: exact owners/instances, scopes, schedule/reaction/cost clocks, tuning, mutation order, schema cutover. Согласовать interfaces с #1572/#1563–#1565.
- [ ] T003 Определить категории и tests/selection.json по контрактам; узкие новые owners health/scheduler/decision в tests/categories.json; -PlanOnly и discovery -ValidateCatalog без suite. Подготовить BookOfEternityClient.TestSupport/SpiritualCombatFixture.cs: typed immutable templates/per-test copies, internal seams, без reflection/генерации мира.

## US1 — личное здоровье и настоящая дуэль

- [ ] T004 [US1] RED tests в BookOfEternityClient.Tests/SpiritualCombatRulesTests.cs и BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.cs: HP/ОД exact actor, hit/miss/barrier/max changes, invalid actor/target, расходы/recover surcharge, ranks. Negative builders не чинят malformed input.
- [ ] T005 [US1] Ввести typed contracts/profile resolver в BookOfEternityClient/Services/SpiritualCombat/; обновить AfterlifeSpiritualConflictState.cs и Validation/ValidationService.AfterlifeEntityProfiles.cs. Definitions/ранги, actor authority/dependency fingerprint, без file I/O в kernel.
- [ ] T006 [US1] ResourceDefinitionCatalog.cs, AfterlifeResourceOwnerComposer.cs, ResourceMutationSourceCatalog.cs, AfterlifeConflictActionPointProjection.cs: personal spiritual_health/ОД, capabilities/max/init, no refill между encounters, no side fallback. Не расширять Mortal health.
- [ ] T007 [US1] SpiritualCombatRules.cs и AfterlifeActionCostRules.cs: accepted math/target/cost/damage/guard/recover из согласованного tuning; ValidationService.AfterlifeSpiritualConflict.cs делегирует расчёт. Preview и accepted используют одно ядро.
- [ ] T008 [US1] AcceptedMechanicsPlanner.SpiritualMechanics.cs и общий CanonicalStateNormalizer: включить state/resources/action receipts в одну существующую публикацию. GREEN T004 и actual accepted/replay/rollback integration. Новый отдельный publisher запрещён.

## US2/US3 — группа, часы, контроль и исходы

- [ ] T009 [US2] После решения D4 определить RED BookOfEternityClient.Tests/SpiritualCombatSchedulerTests.cs, SpiritualCombatControlTests.cs: 1v2/2v2, exact controls, re-entry/join, принятая экономика действий/защиты, wrong-side/owner restriction, multi-target costs/roll assignments; без spatial budgets.
- [ ] T010 [US2] SpiritualCombatScheduler.cs/TargetRules.cs по принятому D4: очередь, точные цели/возможности, clocks, multi-target ordering; повествовательная обстановка, без zones/coordinates/pathfinding; actor-aware ограничения вместо side snapshot.
- [ ] T011 [US2] EffectAcceptedTurnInputComposer.cs/EventTypeCatalog/LifecycleScheduler/Planner и SpiritualWoundConflictContributionProjector.cs: personal effects/barriers/control, source/stack/remove, actor/action/round due boundaries; не tick всем на GM segment. Сохранить provenance/общую причинную обработку.
- [ ] T012 [US3] RED BookOfEternityClient.Tests/SpiritualCombatOutcomeTests.cs: HP0/revive, сдача/выход/смерть, B продолжает после A, смена стороны/чемпиона, lawful dissipation/incarnation и distinct objective.
- [ ] T013 [US3] Implement personal outcomes/aggregate objective в kernel/state/validation; HP vs lasting wounds/healing/rest; точные defeat proofs/reward sources. GREEN T009/T012 + общий accepted plan.

## US4 — реальные реакции, не дополнительный ход

- [ ] T014 [US4] RED BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualCombatDecisions.cs: intent до reveal, pending prefix, action identity/dice, fresh/stale/duplicate submit, no lease wait, reload, no duplicate cost/clock, batch invariance.
- [ ] T015 [US4] SpiritualCombatDecisionService.cs; GameEngine.TurnLifecycle/ValidationAndRepair и console/WebUi lifecycle: planning input без будущих костей, frozen intent, canonical pending, ended GM request, dedicated response admission, session fencing/continuation и durable narration obligation; обычный player-action guard не снимать. GREEN T014 через actual routes.

## Искусства, исторические роли и equipment

- [ ] T016 [US2] Поимённо завершить/утвердить матрицу arts-migration.md для всех базовых направлений и Хранителей; RED по accepted tables/costs/conditions MaskI–V, no accidental maintenance/HP-step rewrite; добавить BookOfEternityClient.Tests/SpiritualCombatArtContractTests.cs к узкой категории.
- [ ] T017 [US2] Definitions/adapters базовых/особых arts и SoulRelicEquipmentService/exporters: equipment exact owner/slot/applicability, effect/source clocks, control/guard/heal/incarnation/dissipation. GREEN T016; синхронизировать общий контракт #1572 без реализации его торговли/полного loot внутри этой feature.
- [ ] T018 [US4] RED historical actor isolation в BookOfEternityClient.IntegrationTests/SpiritualCombatHistoricalScopeTests.cs: independent profiles/HP/ОД/effects/wounds/equipment, reward-only export, no tombstone/live player fallback.
- [ ] T019 [US4] Role adapters/registry в AfterlifeResourceOwnerComposer/WoundCarrierCatalog и profile resolver совместно с #1563–#1565; GREEN T018; определить исторический МаскаV, Люциан/Сареф только после согласованного баланса. Настоящий герой не получает historical kit.

## Preview, оба интерфейса и GM

- [ ] T020 [US4] RED selected preview/command/UI tests для actor/target/phase/current-max/hidden state/decision/reload. AfterlifeSpiritualConflictTurnPreviewService, console/browser/form consumers, Frontend BlockRenderer/ShellContext; проверять semantics parity, не только строки.
- [ ] T021 [US4] Обновить Preview/LiveTurnPreparation/TurnRequest, UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs, ExplorerLifecycleLocalTurnCommandResultBuilder/ExplorerAfterlifeCombatCommandResultBuilder; actor cards, target selection, reactions, ordered log.
- [ ] T022 [US4] WebUi/BrowserAfterlifeWriteService/BrowserGameScreenService/ExplorerWebPromptSessionService/LocalWebUiHost; BookOfEternityClient.WebFrontend/src/context/ShellContext.tsx и components/BlockRenderer.tsx/DTO: typed meters/decision state/routes/fencing. GREEN T020 и визуальная проверка выбранных экранов.
- [ ] T023 [US4] GameResponse/schema, GameEngine runtime reminders/repair packets; TaskGuides/CLI_Step_Main.txt, Rules/Block_CLI_Operations.txt, OtherGuides contracts, CLI API/Daemon specs, Examples/E_CLI_Afterlife_Turns.txt/manifest/bootstrap. Worked 1v2/control/reaction/partial outcome/historical examples; убрать obsolete nohealth/side fallback в реальном GM пакете. Docs/source guard RED→GREEN.

## Производительность, баланс и приёмка

- [ ] T024 [US4] RED→GREEN cache tests: ProfileResolver/AcceptedMechanicsPlanCache/EffectAcceptedTurnPlanCache whole-operation stable DATA reuse и invalidation owner/HP/ОД/effects/wounds/equipment/clocks. Считать actual source reads, сохранять fresh admission; не static cache/TTL-only.
- [ ] T025 [US4] Измерить production kernel/accepted путь cheap scenarios и реальные выбранные интеграции: named sections, distributions of balance/rounds/outcomes; оценить multi-actor scaling/численность, healing loops, historical fight. Raw output на D: вне Git, без квот. Не использовать старый harness с дублированной арифметикой как доказательство баланса.
- [ ] T026 [US4] Проверить весь diff vs AC1–AC9/сохранённые arts; actual selected category results/build/visual/manual evidence; актуализировать selection/catalog. Пять entrypoints GM, cross-domain negatives общих систем; никакой full suite и произвольного 600s recovery.
- [ ] T027 Independent complex review отдельным Astra/high контекстом: original requirements/exact diff/spec/plan/tasks/evidence, authority/clocks/costs/roles, performance/selection. Исправить подтверждённое, проверить затронутое; не считать отчёт агента приёмкой.
- [ ] T028 Финальный scoped verification/checkpoint: WIP/final commits/push, remote SHA/readback, compact evidence; clean restore перед заявлением recoverability. Обновить issue/handoff, не закрывать #1573 до всех AC и согласованной приёмки.

## Зависимости и параллельность

T001→T002→T003→T004–T008→T009–T013→T014–T015.
T016 можно уточнять после T002 параллельно foundation без правки его файлов;
T017 после T011/T013/T016, T018–T019 после согласованных scopes и T008/T011.
T020–T023 после contracts/lifecycle; console/frontend работа параллельна только
при закреплённом DTO/владении файлами. T024 закладывается с resolver, доказательства
до T025. T025–T028 после полного coherent implementation. Один разработчик
владеет связным блоком; отдельный reviewer, не многократные перекрывающиеся edits.

## Coverage map

Уточнения независимого review (обязательны в соответствующих задачах):
T009/T010 — duplicate exact actor binding и leave/rejoin без сброса budgets;
T014/T015 — durable slot/AP commitments, multi-decision/reload/cancellation,
однократный consume/release без второго ledger;
T009/T010/T016/T017 — AoE+cover collision, original/effective targets,
детерминированные attack/defense reservations и контроль Приговора по фактической цели.

AC1 T004–T008/T020–T022; AC2 T009–T011/T016–T017; AC3 T004/T008/T014–T015/T018;
AC4 T006–T011/T014–T015/T024; AC5 T012–T013/T017–T019;
AC6 T008/T014–T015/T020–T025; AC7 T020–T023/T026;
AC8 T010/T016–T019/T025; AC9 T003/T004/T009/T012/T014/T018/T020/T024–T028.
Подготовка этого списка не отмечает перечисленные проверки выполненными.
