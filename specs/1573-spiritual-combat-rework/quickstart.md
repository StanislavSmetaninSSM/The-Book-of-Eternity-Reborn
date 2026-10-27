# Проверочные сценарии #1573

План будущей проверки, **не отчёт о пройденных тестах**. Ожидания условны до
утверждения [spec](spec.md)/[gameplay](gameplay.md). Cheap typed fixtures, production APIs.
Поправка v1.1: нет карты/зон/пространственных слотов. Reaction-specific строки
условны до решения D4; новый рекомендуемый кандидат — [адаптация смертного боя](mortal-combat-comparison.md).

| Сценарий | Доказательство |
|---|---|
| 1v1 hit/miss/barrier | Assigned roll, exact HP/ОД/event order; preview и accepted совпали |
| 1v2 control A | Запрет A; B атакует/помогает за собственные ресурсы; попадание в B не ранит A |
| 2v2 несколько Оков | Source/target effects не перетираются, exact remove не снимает соседний |
| Чужой actor/unknown target | Отказ без частичной оплаты/публикации/тика; helper не подставляет lead |
| 0 HP A, B продолжает | A incapacitated, B сохраняет слот, общего финала ещё нет |
| Heal A в том же раунде | HP вырос, spent/skipped слот не вернулся, wound не стёрта |
| Сдача/отход | Вышел только конкретный участник; маршрут/условия доказаны |
| Failed attempt | Законный промах оплачен один раз, недопустимый ничего не тратит |
| ОД0/recover | Будущий Gain не оплачивает текущую wound surcharge; без surcharge допустим за слот |
| Barrier/max HP | Изменение max не лечит; барьер не permanent health |
| AoE | Base cost один раз, targets до костей, личные defense/reaction/results |
| Mid-round join/revive | Нет лишнего main/reaction, стабильный порядок/clocks |
| Duplicate actor / leave-rejoin | Два participantId одного owner отвергнуты; rejoin не сбрасывает прежние слоты |
| Несколько reaction decisions | Первый response уже занимает слот/AP; второй не резервирует его повторно, reload/отмена не теряют commitment |
| AoE+cover collision | Уже включённый в AoE protector не принимает второй прямой hit; original/effective targets и контроль Приговора различаются |
| NPC intent/dice | Нельзя выбрать action/target после просмотра бросков; repair не перебрасывает |
| Pending reaction | GM segment закончен, урон не применён, обычная команда заблокирована |
| Duplicate submit | Exact repeat без двойного расхода; changed/stale response отвергнут |
| Reload reaction | Тот же action/dice, актуальный UI token, нет extra round/timeout pass |
| GM batching | Те же intents/dice с разным числом ответов дают те же clocks/resources |
| Boundary effect убил актора | Сначала boundary, затем eligibility; выбывший не получает действие |
| Publication failure/replay | Не остаётся половина HP/effects/schedule; accepted history не переписана |
| Side/encounter change | Actor сохраняет ресурсы, нет refill от нового participant/lead |
| Historical role | Исторические HP/раны/снаряжение изолированы, настоящее меняют лишь разрешённые награды |
| Relic | Бонус только надетого на этого owner предмета; смена invalidates projection |
| Hidden actor/effect | Нет утечки через DTO/count/tooltip/preview; parity обоих клиентов |
| Ранги | Сияние IX допустимо; art VI недопустимо; Просветление/Средоточие отдельно |
| Повествовательная сцена | Действие/выбор цели допустимы без координат/zoneId; target-count/owner/effect restrictions при этом проверяются |

Сначала RED на старом нарушении либо failing contract test отсутствующей функции;
затем GREEN через production reducer/kernel, не тестовую копию формул.
Симметричные player/NPC/ally cases не сводить к единственной атаке игрока.
Отдельно actual accepted-plan/normalizer/repair/recovery и оба input пути.
Границы publication/historical scopes не доказываются одним чистым unit-test.

Баланс: несколько уровней/комплектов, 1v1/1v2/2v2, разные воспроизводимые кости,
стратегии attack/guard/control/recover/heal. Распределение раундов/побед/полезности,
не один удачный seed. Исторический Маска с artsV/комплектом — отдельный профиль;
Люциан/Сареф побеждаемы с трудом, сюжетная ловушка после настоящего результата.
Точные профили пока не утверждены. Никакие эти проверки сейчас не запускались.
