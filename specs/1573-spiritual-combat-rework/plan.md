# План перехода к личной модели духовного боя — #1573

2026-10-27. **Draft v1: исполнение заблокировано D1–D6 и утверждением автора.**
Поправка v1.1: формальное позиционирование отвергнуто. [Сравнение со смертным
боем](mortal-combat-comparison.md) рекомендует адаптацию его темпа/действий;
предыдущие scheduler/reaction stages ниже — инвентарь кандидата v1, не выбранный
план. После решения автора D4 пересобрать plan/tasks; новые interrupt stages
не считать обязательными для личного здоровья. Карта/зоны исключены уже сейчас.
[Issue #1573](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1573),
[spec](spec.md), [правила](gameplay.md), [задачи](tasks.md), [исследование](research.md).
Ветка документов: `story/golden-mask-backstory-20261025`.
Production baseline исследования: `7b73b8c538436083e05def58da4970171f4614b5`.

## Summary / technical context

Перенести духовный бой с двух агрегированных сторон на самостоятельных участников:
личные HP/ОД/эффекты, очередь, реакции, адресные действия и индивидуальные исходы.
Сохранить общую инфраструктуру ресурсов/эффектов/ран и принятого хода.
HP в карточке поверх старого side-state не считается завершённым переходом.

Проверенные проекты: C#/.NET8 client, Spectre.Console, ASP.NET local web host;
React19/TypeScript6/Vite8 frontend. Канонические данные/история — текущие файловые
контракты с атомарной публикацией, не новая база данных. Windows/Linux.
Область: runtime, validation/publication/recovery, GM normal/repair, оба клиента,
шаблоны/примеры и исторические роли. Смертный бой не переделывается; общие
ресурсы/эффекты проверяются на отсутствие регрессий. Скорость без измерений не обещается.

## Constitution / workflow gates

- Traceability #1573, AC1–AC9; feature532 использует результат, не отдельную физику.
- Spec Kit обязателен по масштабу и пользовательскому правилу brainstorming.
  Реально вызван `setup-plan.ps1 -Json` с feature override1573; Git-ветка не переименована.
  Writing-plans применяется к этому plan, не к конкурирующему Superpowers-файлу.
- Точная редакция spec/gameplay предъявляется автору. D1–D6 не скрыты defaults.
  После утверждения обновить tasks/analysis для принятой версии, затем исполнение/TDD.
- Русские внутримировые подписи, parity console/browser, отсутствие утечки скрытого.
- Authority: immutable input → общий accepted plan → существующий publisher.
  GM не получает второй путь менять HP; Cached DATA не заменяет свежие полномочия.
- Pre-release: переносим текущие bootstrap/templates/tests, удаляем side-fallback.
  Старые saves не поддерживаем без отдельного утверждённого исключения.
- Тесты перед кодом, узкие категории, typed fixtures; independent review целого блока.
  Документационное review не заменяет будущий review реализации.
- Перед кодом сверить опубликованную работу #1535/#1536 и владение общими файлами.
  Не сливать автоматически старый remote main/не затирать параллельную работу Астры.

Governance exceptions не требуются. Открытые решения блокируют код, а не разрешают
исполнителю выбрать баланс самостоятельно. См. [review](review.md).

## Project structure / новые предлагаемые модули

Документы: spec/gameplay/research/data-model/plan/tasks/quickstart/review и contracts/commands.
Предлагаемые классы в `BookOfEternityClient/Services/SpiritualCombat/`:

| Модуль | Ответственность и граница |
|---|---|
| SpiritualCombatContracts | Typed instance/participant/intent/action/decision/outcome, без I/O |
| SpiritualCombatProfileResolver | Actor profile + arts/equipment/wounds/effects → immutable stats + dependencies |
| SpiritualCombatRules | Snapshot + admitted intent + assigned dice → events либо typed rejection |
| SpiritualCombatTargetRules | Actors/definition/принятые обстоятельства сцены → exact targets, без геометрического движка |
| SpiritualCombatScheduler | Очередь/слоты/clocks/eligibility → следующая возможность или итог |
| SpiritualCombatDecisionService | Current authority + response → continuation input; самостоятельно не пишет файлы |

Это предлагаемые имена, не готовые API. Kernel не читает JSON/каталоги, не вызывает GM
и не имеет собственного генератора случайности. Preview, planner и балансные
фикстуры вызывают ту же логику. Не копировать формулы в тестовый harness.
Сборка входа — через существующие Service–Repository/operation cache.

## Изменения существующих модулей

Все C# имена ниже внутри BookOfEternityClient; полные пути/строки в research.

| Область | Существующие потребители | Изменение |
|---|---|---|
| State/resolve | AfterlifeSpiritualConflictState; AfterlifeControlStateRules | Participant/schedule/action/outcome; убрать общий strain/control из текущего исполнения |
| Validation | ValidationService.AfterlifeSpiritualConflict | Exact actors/targets; расчёт общего kernel; personal control/cost/resolve; удалить hard matchup veto после согласования D6 |
| Ранги | ValidationService.AfterlifeEntityProfiles | Разные диапазоны Просветления/Сияния/arts; не max9 для всех |
| Ресурсы | ResourceDefinitionCatalog; AfterlifeResourceOwnerComposer; ResourceMutationSourceCatalog | spiritual_health, capabilities/max каждого actor, authorised combat source; side-owner не используется в новом бою |
| ОД | AfterlifeConflictActionPointProjection; AcceptedMechanicsPlanner.SpiritualMechanics; AfterlifeActionCostRules | Личные владельцы, main/reaction modes, оплата exact action, постоянные ресурсы между encounters |
| Effects/clocks | EffectAcceptedTurnInputComposer; EffectEventTypeCatalog; EffectLifecycleScheduler; EffectAcceptedTurnPlanner | Личные эффекты, actor/round/action events вместо тика всем на GM-ответ; registered stack/remove/durations |
| Раны | SpiritualWoundConflictContributionProjector; WoundCarrierCatalog | Personal contribution/owner; доказательства/история; historical carrier, conflict не owner раны |
| Publication/cache | AcceptedMechanicsPlanCache; EffectAcceptedTurnPlanCache; CanonicalStateNormalizer.SpiritualPublication | Расширить общий plan/read-set/receipt; прежние capability/rollback/recovery; invalidation по зависимости |
| Preview | AfterlifeSpiritualConflictTurnPreviewService; LiveTurnPreparationService; TurnRequest | Selected actor/action/targets, unknown ≠zero; общий resolver; не раскрывать будущие кости |
| GM/repair | GameResponse; GameEngine.TurnLifecycle; GameEngine.ValidationAndRepair | Versioned update, typed intents/events/audits; адресные repair packets; повествование по результату |
| Reaction | Новый decision route + lifecycle | Accepted prefix → оконченный GM request → связанный player response; fencing QTE как образец, без его таймера/последствий |
| Console | ExplorerMode.Afterlife.SpiritualConflict; ExplorerLifecycleLocalTurnCommandResultBuilder; ExplorerAfterlifeCombatCommandResultBuilder | Цели/эффекты/карточки/реакции/журнал; убрать no-health инструкции |
| Browser backend | BrowserAfterlifeWriteService; BrowserGameScreenService; ExplorerWebPromptSessionService; LocalWebUiHost | Тот же intent, pending state, freshness/route, доступный слот |
| Browser frontend | ShellContext.tsx; BlockRenderer.tsx и DTO | Typed current/max, участники/очередь/реакция; не парсить подпись как число |
| Arts/relics | Definitions/adapters; SoulRelicEquipmentService и exporters | Exact носитель/цель/слот/цена/уровень; только экипированные реликвии |

Также проверить ExplorerCommandCatalog, runtime reminders, FileMapping (имя
transport может остаться), effect→resource bindings и source-frontier validators.
Не создавать вторую изменяемую копию HP рядом с resource ledger.

## Порядок перехода

**0. Решения/baseline.** Утвердить D1–D6 и [перенос arts](arts-migration.md),
версии общих механик/ownership, scopes/identity, tuning/ruleRevision и clocks.
Analysis точной spec/plan/tasks. Текущая подготовка документов не разрешает этот код.

**1. Честная дуэль через production.** Typed actors/owner profiles, HP/личные ОД,
kernel hit/damage/guard/recover, индивидуальный ноль, state/validation/common
publication. Cheap RED до кода, затем actual accepted/replay. Это промежуточный
срез, не готовый групповой бой; старые UI/GM не включать на несовместимую schema.

**2. Группа.** Согласованный порядок действий без зон, контроль/heal/effects, personal clocks, 1v2/2v2,
вступление/выход/смена стороны/частичный итог, wounds и owner projections.
Доказать отсутствие скрытого lead fallback в accepted и preview.

**3. Реакции/lifecycle.** NPC intent до костей, pending decision, оконченный GM
segment, console/browser input, fresh submit, reload/replay/явная отмена.
Не обходить active-GM guard и не держать lease при ожидании. Несколько targets/
реакций обрабатываются в зафиксированном порядке, не по выгодным броскам.

**4. Arts/equipment/historical.** Матрица всех обычных направленностей и особых
arts, exporters реликвий совместно с #1572, role scope adapter с #1563–#1565.
Полная торговля/добыча остаются в #1572; бой даёт адресное доказательство исхода
и доступ к действительному inventory, не создаёт копии чужих предметов.

**5. Полный путь.** Формы/карточки/preview/журнал, GM normal/repair,
prompts/examples/bootstrap одной версии; удалить старый fallback/nohealth,
проверить оба клиента. Промежуточный WIP не объявлять рабочей версией.

**6. Баланс/приёмка.** Production kernel с воспроизводимыми костями, несколькими
профилями/тактиками; затем выбранные настоящие интеграции и согласованный игровой
прогон. Измерить раунды/исходы/защиту/контроль/лечение/численность/дорогие секции.
Independent review и доказательства AC1–AC9. Исторический Маска — отдельный профиль.

## GM/docs/examples

Обновить Afterlife_Contract_Matrix, Combat_Terminology_Glossary, wound/effect
materialization contracts; Examples/E_CLI_Afterlife_Turns.txt и manifest.
Worked examples: 1v2/control одного; прикрытие/реакция; частичный исход;
replay; historical actor без записи в нынешнюю душу.
Синхронизировать TaskGuides/CLI_Step_Main.txt, Rules/Block_CLI_Operations.txt,
CLI API/Daemon specs и five-entrypoint coverage. Launcher/daemon routing-only
менять лишь при устаревшем прямом правиле/маршруте, не косметически.
Mortal-specific rules не меняются; cross-domain negatives для shared resources/
effects/token обязательны. Устаревший nohealth удаляется из реально выдаваемого GM пакета.

## Verification / performance

Подбор на исполнении по фактическому diff, не автоматический запуск всего перечня:

- spiritual-conflict-rules/dice/conditions: actor/action/control/roll.
- Новые узкие категории personal-health/scheduler/decision зарегистрировать
  в tests/categories.json; названия до регистрации только предложения.
- turn-lifecycle-spiritual, spiritual-cutover-output/publication: atomicity/replay;
  соседние core/repair/snapshot/C1–C3/source-frontier по изменённым зависимостям.
- console-explorer-afterlife, browser-command-afterlife, frontend-command-results,
  frontend-visual-status, frontend-shell-input/types: forms/DTO/parity.
- documentation-afterlife-entrypoints-coverage, documentation-afterlife: GM/examples.
- QTE категории только при изменении общего QTE кода, не по сходству концепции.

Уточнить точные existing IDs через каталог; обновить tests/selection.json с причинами
и контрактами всего diff. Будущие ограниченные команды, сейчас **не запускались**:

```powershell
# В PowerShell 7, из корня репозитория:
.\scripts\test-csharp.ps1 -Category @('spiritual-conflict-rules', 'spiritual-conflict-dice') -PlanOnly
.\scripts\test-csharp.ps1 -Category @('spiritual-conflict-rules', 'spiritual-conflict-dice')
```

После изменения каталога -ValidateCatalog (discovery). Нет full suite/Fast/PreMerge;
-NoBuild только после свежей сборки выбранных проектов. Текущий docs-only блок:
diff/link/consistency/independent review; runtime tests/builds UNRUN.

Fixture: два-три exact actors, resources/effects, assigned dice; без генерации мира.
Internal/InternalsVisibleTo, без reflection. Malformed negative не ремонтировать
helper-ом перед validator. Чистый kernel не заменяет реальные publication/lifecycle tests.

Cache owner — whole planning/validation operation; keys/invalidations в data-model.
Измерять Profile.Resolve, ParticipantProjection, Action.Resolve, Effects.Plan,
AcceptedPlan.Validate, Publication и фактические source reads. После HP/ОД/effect/
equipment mutation зависимая проекция недействительна. Fresh authority нужна,
повторный parse стабильного файла обычно нет. Не кэшировать тривиальную арифметику.
Raw диагностика без квот на D: вне Git; в Git только компактные выводы.

## Статус

Подготовлена архитектура и исходная карта. Реализация/баланс/runtime tests/builds/
clean-restore не выполнены. [Review](review.md) относится к проекту документов.
