# Исследование текущего кода для #1573

Дата 2026-10-27. Production baseline `7b73b8c538436083e05def58da4970171f4614b5`;
WIP874714c5 меняет только документы. Проверенный remote main `d0241e71e349fbe2020e4a41b6be7c627c81cbb4`
датирован 2026-10-08 и существенно расходится с рабочей веткой (в ней есть
последующая работа над ранами/оптимизацией). Main не объединялся с веткой.
Перед реализацией выбрать актуальный опубликованный baseline общих механик.
Ни сборки, ни игровые/автоматические тесты в этом исследовании не запускались.

Serena: активирован правильный проект, local config C#/TypeScript/Python.
Семантическое чтение реального C# и TS ShellProvider подтверждено; Python
в этой задаче не изменяется и отдельной символической проверкой не заявлен.

Все ссылки ниже относительно repository root через ../../. Номера строк относятся
к baseline, не к будущему коду. Предлагаемые решения не выдаются за готовые функции.

| Наблюдение | Источник | Следствие для проекта |
|---|---|---|
| Один lead и supporters, strain на сторону | [Матрица](../../OtherGuides/Afterlife_Contract_Matrix.md#L393) | Новый participant model вместо имитации группы помощниками |
| Snapshot контроля — rank/controllerSide | [ControlStateRules](../../BookOfEternityClient/Services/AfterlifeControlStateRules.cs#L19) | Требуется exact target/controller и независимые записи |
| Запрет операции не получает актора | [Validation](../../BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs#L5099) | Проверять обе стороны по действующему участнику; массовый bind не объявлен воспроизведённым |
| Общий контроль копируется в active | [State](../../BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs#L797) | Меняются канонический контракт, apply, snapshots и history |
| Resolve проверяет ведущего | [State](../../BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs#L1012) | Нужны индивидуальные исходы и отдельный общий результат |
| Matrix безусловно отклоняет успех pressure против guard/counter | [Validation](../../BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs#L4348), [pairs](../../BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs#L5152) | Заменить veto расчётом активной защиты/результата |
| Клиентские d20 уже существуют | [GameLoop](../../BookOfEternityClient/Core/GameLoop.cs#L37) | Переиспользовать источник случайности; новая привязка reservations к action/target |
| Есть проверка сумм/выбранных костей/критических bands | [Validation](../../BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs#L5857), [bands](../../BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs#L6641) | Выделить общую actor-aware арифметику, не бросать кубики внутри GM |
| Стоимости стандартных/особых искусств вынесены | [ActionCostRules](../../BookOfEternityClient/Services/AfterlifeActionCostRules.cs#L5) | Сохранить каталог/округления по назначению, добавить цены новых action/reaction modes |
| Сияние доIX, profile tier общий0..5 | [ранги](../../BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs#L242), [validator](../../BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs#L3353) | Проверять разные домены рангов раздельно; не повысить max всех искусств до9 |
| Ресурс имеет coordinate/current/maximum/capacity/history | [ResourceState](../../BookOfEternityClient/Services/ResourceStateContract.cs#L11) | Хранить HP/ОД в общем resource ledger, не в параллельном store |
| health — Mortal percent_point; spiritual AP допускает actor/side | [ResourceDefinitionCatalog](../../BookOfEternityClient/Services/ResourceDefinitionCatalog.cs#L1574) | Новый spiritual_health; убрать side-owner AP в новом боевом контракте |
| Реальная capability AP есть у player_soul/стороны | [OwnerComposer](../../BookOfEternityClient/Services/AfterlifeResourceOwnerComposer.cs#L1770) | Прописать capability/maximum/capacity каждого NPC, не лишь новую строку JSON |
| Projection/AP planner жёстко используют две стороны | [AP projection](../../BookOfEternityClient/Services/AfterlifeConflictActionPointProjection.cs#L42), [planner](../../BookOfEternityClient/Services/AcceptedMechanicsPlanner.SpiritualMechanics.cs#L192) | Actor-keyed ресурсы и оплаты во всех consumers |
| Resource reducer различает Damage/Restore/Spend/Gain и exact replay | [Reducer](../../BookOfEternityClient/Services/ResourceMutationReducer.cs#L18), [replay](../../BookOfEternityClient/Services/ResourceMutationReducer.cs#L305) | Новый combat source экспортирует точные mutations в прежний reducer |
| Исторические owners — tombstone, не акторы воспоминаний | [OwnerComposer](../../BookOfEternityClient/Services/AfterlifeResourceOwnerComposer.cs#L417) | Изолированный зарегистрированный role scope/owner, не оживление tombstone |
| Wound conflict instance отделён от display conflictId | [InstanceState](../../BookOfEternityClient/Services/SpiritualWoundConflictInstanceState.cs#L48) | Использовать клиентский instance для participant/action identity |
| Stack/remove эффектов уже exact target/source/effectId | [Effect planner](../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs#L4970), [remove](../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs#L8897) | Сохранить общий effect lifecycle, не отдельный словарь статусов |
| Есть explicit effect→resource owner override | [Effect planner](../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs#L8071) | Подключить точный participant/role owner |
| Раны имеют личные carriers; conflict не carrier раны | [WoundCarrierCatalog](../../BookOfEternityClient/Services/WoundCarrierCatalog.cs#L554) | Раны остаются в зарегистрированном owner; historical adapter обязателен |
| Wound projector знает actor, затем даёт side-based axes | [Projector](../../BookOfEternityClient/Services/SpiritualWoundConflictContributionProjector.cs#L159) | Перенести проекции на личный action/target без потери provenance |
| owner_turn_end сейчас связан с accepted GM turn | [InputComposer](../../BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs#L418) | Ввести tactical clocks; batching не меняет сроки |
| Нет registered round-end; scheduler умеет advancePhase | [EventCatalog](../../BookOfEternityClient/Services/EffectEventTypeCatalog.cs#L17), [Scheduler](../../BookOfEternityClient/Services/EffectLifecycleScheduler.cs#L660) | Расширить каталоги и composer согласованно |
| Публикация уже имеет capability/common normalizer/rollback | [SpiritualPublication](../../BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SpiritualPublication.cs#L32) | Один существующий accepted publisher, не второй combat writer |
| Accepted-plan cache проверяет fingerprint/admission | [PlanCache](../../BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs#L316) | Расширить ключи actor/clock/action/health; fresh authority сохраняется |
| Обе команды прямо требуют side-v-side/no health | [console](../../BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs#L1535), [browser](../../BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs#L1946) | Переписать prompt/authoring, а не только карточку |
| Preview читает игрока и одного lead, первую пару костей | [Preview](../../BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs#L32) | Коллекция участников/выбранное действие; не молчаливый default lead |
| Обычный browser submit запрещён при active GM | [BrowserPlayerActionService](../../BookOfEternityClient/WebUi/BrowserPlayerActionService.cs#L82) | Reaction decision отдельный admission/сегмент, не разблокировка composer всем |
| Spiritual continuation — ответ GM, не игрока | [Protocol](../../BookOfEternityClient/Services/SpiritualWoundContinuationProtocol.cs#L39), [Engine](../../BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs#L118) | Заимствовать resume/freshness, не смешивать каналы |
| QTE имеет проверку token/fingerprint/generation | [QteTokenAuthority](../../BookOfEternityClient/WebUi/QteInteractionTokenAuthority.cs#L24) | Образец fencing; не импортировать смертные injury/таймеры/последствия QTE |
| ResourceMeter разбирает процент из строки | [BlockRenderer](../../BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx#L1532) | Typed current/max, не authoritative parsing подписи |

## Выбор подхода и отклонённые альтернативы

- Добавить HP к прежнему side-state недостаточно: сохраняет ошибочные actor/clock/
  resolution assumptions. Предложена замена предметной модели с reuse инфраструктуры.
- Новый независимый resource/effect publisher отклонён: создаёт две истины,
  ломает текущую атомарность/историю. Предложены новые exporters/adapters общего plan.
- Полный deterministic автоматический GM/AI не нужен: GM выбирает намерения,
  предметные правила проверяемо разрешают последствия и очередность.
- Ждать ввода в обычном activeGM без нового протокола нельзя. Предложен
  принятый префикс+pending decision, окончание GM request и отдельный связанный submit.
- Не вводить tactical hex/grid только ради HP: предложены небольшие зоны/отношения.
- Не считать тестовый AfterlifeSpiritualCombatScenarioHarness production engine:
  он содержит собственные цены/контест/награды; баланс проверять новым production kernel.

## Неустановленное

Числа развития/HP/урона, critical/healing/wound thresholds и все решения D1–D6
остаются обсуждаемыми. Наличие модуля не доказывает корректность будущего переноса.
Code reading доказывает ограничения текущего контракта, не результат будущих тестов.
