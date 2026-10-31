# Выбор момента прерывания лечения — T124

2026-11-01. [Spec](spec.md), [план](plan.md), [задачи](tasks.md), [review](review.md).
**WIP: полные измерения выполняются, итогового вывода и финального review нет.**
Автор отменил отдельное ожидание согласования документов на текущем этапе
планирования/численных исследований. Сила, цены и фазы Heal сохранены.
Это numerical investigation; игровой runtime, AI, промпты и UI не реализуются.

## Фиксированные условия

Source bb2048a260fefe9e052e5ba548e728f1dfea7fcb, Windows/Node24.21.0/PowerShell7.6.6.
[Общая модель](probes/healing-tactics-model.mjs),
[runner](probes/healing-interruption-probe.mjs),
[policy guards](probes/healing-interruption-policy.test.mjs),
[runner guards](probes/healing-interruption-probe.test.mjs).

Дуэли D130, HP1030, MaxDE204, полные стартовые ресурсы. Целитель только лечит
и защищает плетение guard, без ответных атак/самоочищения. У него +50% лечения
и +25п.п. дополнительного сопротивления; это заданный усиленный профиль,
не доказанная равная экономическая стоимость сборок. У атакующего обычные
показатели. В healer28 атакующий восстанавливает26.4ДЭ, целитель100.8ДЭ за
личный ход; в both36 оба129.6ДЭ. Recover дополнительно57.6ДЭ за Main.

Обе очереди, instant/long/прежний adaptive; fixed_large либо suppress_reserve.
Последняя сохраняет прежний attack-reserve16P с учётом своего end-regen, включая
Never. Новые политики меняют только решение о срыве. Пять вариантов:

| Политика | Условие попытки |
|---|---|
|Never|Нет попытки|
|Always|Доступна личная реакция и текущая оплата8P|
|Reserve strong|После8P остаётся16P|
|Reserve large|После8P остаётся24P|
|Release + reserve large|Тот же24P; у long только finish, instant допускается|

ДляD130 срыв стоит28.8ДЭ, резервы57.6/86.4ДЭ. Порог текущей энергии86.4/115.2ДЭ,
будущий regen не прибавляется. Это эвристика, а не новый обязательный резерв.
Отказ не тратитДЭ/реакцию/кости. Попытка оплачивается и может провалиться.
Реальная возможность платить перепроверяется в общем applyHeal; решение о
попытке не заменяет текущий допуск. Данные противника и будущие кости не читаются.

## Измерения и воспроизводимость

120cases =2harmony×2orders×3modes×2attack×5interrupt. Main по1000 и независимое
confirmation по10000 для **каждого** case; план1 320 000траекторий. Horizon2000 —
граница наблюдения. Null median/p95 означает недостижение квантили; незавершённый
бой не объявляется победой за2000раундов. Разные policies не являются парными
траекториями. Полный набор параметров и seed находятся в каждой raw cell.

```powershell
node --test specs/1573-spiritual-combat-rework/probes/*.test.mjs
node specs/1573-spiritual-combat-rework/probes/healing-interruption-probe.mjs --matrix=main --samples=1000 --horizon=2000
node specs/1573-spiritual-combat-rework/probes/healing-interruption-probe.mjs --matrix=confirm --samples=10000 --horizon=2000 --shard-index=0 --shard-count=3
# Повторить confirmation с shard-index=1 и2; вместе исходные indices0..119.
```

Основная матрица и три confirmation workers выполняются параллельно. SeedSuffix
T124-main-v1/T124-confirm-v1 передаётся в прежний T123-v1 pipeline и включает
полный cell/interruptPolicy/n/horizon; shard/index/time в seed не входят.
Не суммировать wall time параллельных процессов как время ожидания пользователя.

Raw без квот только `D:\BoeDiagnostics\healing-interruption-t124\20261101-v1`.
Прежний полный T123raw пока сохранён. В Git публикуются source и краткий отчёт.
Расходы `*Paid` в raw — сотыеДЭ, для показаДЭ делить на100. Reasons — первая
применимая причина: inactive/no_reaction/no_energy/policy_never/long_start/reserve/
attempt. В штатном прогоне нет hooks, меняющих бюджет между выбором и оплатой.
Все ресурсы, состояния и часы принадлежат отдельному участнику.

## Проверки перед замером

99/99 numerical guards прошли (84прежних+15новых),exit0,stderr0,
3269.2947мс Node/3506.0386мс shell. Observed RED→GREEN для порогов, отказа без
расхода/костей, оплаты до dice, long start/finish, метрик и shards. Отдельно
исправлен лишний Main-only запрет реакции после revival; потерянный Main при
этом не возвращается. Прежние12control cases совпали до/после byte-for-byte.

Smoke main/confirm n1,horizon3:120cells каждый; main shards60+60 точно совпали
с unsharded без timings. Parent проверил raw/indices/count/outcome/endpoint/
decision sums иexit/stderr. Source опубликован и прочитан с GitHub до замера.
Прежние utility guards сохраняют пользу instant/long/возврата союзника; новые
дуэли не подменяют исследование союзного лечения. C# build и игровые прогоны UNRUN.

## Выводы

Пока не сформированы: требуется завершить все ячейки и независимое подтверждение.
Отчёт отдельно ответит, лучше ли selective-политики обоих Always/Never,
уменьшают ли потерянные атакующие Main и помогает ли ожидание finish long.
Неудачная эвристика останется отрицательным результатом; Heal не ослабляется.
Выводы не переносятся на нерешённый1×2, произвольные искусства или весь баланс.
