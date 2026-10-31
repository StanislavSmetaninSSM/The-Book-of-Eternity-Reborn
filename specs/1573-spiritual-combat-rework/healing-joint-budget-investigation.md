# Совместный бюджет атаки и срыва — T125

2026-11-01. [Spec](spec.md), [план](plan.md), [задачи](tasks.md), [review](review.md).
**WIP: main108000 завершён и проверен; confirmation1080000 выполняется.**
Автор поручил автономную проверку без дополнительных согласований. Игровые
формулы/цены/фазы Heal, production, GM-промпты и UI не меняются.

## Что проверяем

[T124](healing-interruption-investigation.md) показал конфликт независимых
резервов атаки/реакции и частичный эффект сохранения энергии. Новый
`joint_pressure_v1` одновременно оценивает ordinary/strong/large/suppress/Recover
и skip/attempt8P на горизонте трёх собственных Main. Цель: ожидаемый урон минус
прогноз лечения плюс оценка конечного остаткаДЭ. λ=ordinaryMean/
(ordinaryCost+RecoverGain); приD130/res25 это85.7825/86.4=0.9928530093HP/ДЭ.
Внутри единицы — сотыеДЭ, поэтому λ нацент в100раз меньше.

Горизонт/вес зафиксированы до результатов. Текущая оплата не использует будущую
регенерацию; прогноз учитывает её после своего Main. При равенстве оценок
реакция пропускается, Main выбирает меньшую цену и закреплённый порядок.

Прогноз знает объявленный публичный профиль benchmark и текущие видимые marks/
pending/Heal intent; не читает энергию врага, будущие dice/chooseMode/guard.
Будущий режим — последний увиденный (включая отменённый intent), initial instant;
видимый pending означает finish. Будущий guard считается доступным: принятый−5,
76/400=19% срыва в измеренном профиле. Это приближение, реальный guard может
быть недоступен. Будущие HP-эффекты не ограничены текущимHP; только нынешняя
реакция учитывает реальный дефицитHP. Такое приближение может переоценивать
лечение и упускать добивание. Поддержана дуэль с одним источником подавления.

Общая машина исполняет реальные действия, расходы, RNG, срок эффектов, распад/
возврат и конечный исход. Таблица оценок не является разрешением действовать.

## Фиксированные условия

Frozen source: `0da23b58d9a2c27a52b520c8f52c2d5a1a32bb2d`.
Windows, Node24.21.0, PowerShell7.6.6; D130/HP1030/MaxDE204, полные стартовые ресурсы.
Три профиля: fortified_healer28 (+50%Heal/+25п.п.res; regen26.4/100.8ДЭ),
fortified_both36 (те же усиления; regen129.6/129.6ДЭ), unboosted_normal
(0%/0п.п.; обычные26.4/26.4ДЭ). Целитель пассивен, guard, без самоочищения.
Последний профиль имеет одинаковые базовые ресурсы, не доказанную равную
экономическую стоимость ролей. Обе очереди, instant/long/adaptive.

Шесть тактик: legacy_large_never, legacy_large_always, legacy_suppress_reserve
(прежний attack reserve16P + reaction reserve24P), planner_never, planner_always,
planner_joint. Все три planner-варианта используют одинаковый Main-selector с
совместным прогнозом; Never/Always меняют только реальные реакции. Это абляции
с намеренным расхождением прогноза/исполнения, не отдельные оптимальные AI.

108ячеек main×1000 и108 confirm×10000 =1 188 000плановых траекторий. Horizon2000
— граница наблюдения, не фиктивная длительность победы. Null median/p95 честно
сохраняются; очереди и main/confirm не объединяются в одну медиану.
SeedSuffix T125-main-v1/T125-confirm-v1, прежний T123-v1 pipeline включает все
параметры/tactic/profile/n/horizon; shard/index/time на seed не влияют.

```powershell
node --test specs/1573-spiritual-combat-rework/probes/*.test.mjs
node specs/1573-spiritual-combat-rework/probes/healing-joint-budget-probe.mjs --matrix=main --samples=1000 --horizon=2000
node specs/1573-spiritual-combat-rework/probes/healing-joint-budget-probe.mjs --matrix=confirm --samples=10000 --horizon=2000 --shard-index=0 --shard-count=3
# Повторить confirmation с индексами1 и2; вместе исходные108ячеек.
```

## Проверки и оптимизация до замера

120/120numerical guards (99прежних+21новых),0fail/cancel/skip,exit0/stderr0;
3681.5834мс Node/3837.3791мс shell. RED→GREEN policy/runner/integration, ручные
численные oracles прогноза/λ/фаз. Legacy42cases checksum сохранён. Прежние
burst/ally/revival guards продолжают проверять пользу лечения. C# build UNRUN.

Parent прочёл фактический diff/source и проверил108main/108confirm smoke n1h3,
54+54shard union exact без timings, params/seeds/stderr. Прогнозные DATA immutable
на существующую operation; новые живые ресурсы/сроки/допуск не кэшируются.
Cold preparation88.3536мс/3096states;1000warm profiles5.3831мс;
20000selector lookups61.9278мс, новых state computations нет. Smoke использует
три профиля/9288states. Это цена подготовки/поиска, не измеренный выигрыш боя.

Raw без квот только `D:\BoeDiagnostics\healing-joint-t125\20261101-v1`, не Git.
Все Paid-счётчики — сотыеДЭ; делить на100 при показе расходов. Предыдущий полный
T124raw сохраняется до полного проверенного нового замера. Ранее отклонённая
очистка T123raw остаётся отдельным незавершённым T124.4r.

## Основная выборка (не confirmation)

Main108ячеек/108000траекторий завершёнexit0/stderr0:176525.9676мс process,
176358.8876мс runner;3770незавершённых к2000. Parent сверил все параметры,
seeds/indices/outcomes/endpoints/личные и общие счётчики/цены/нормализацию.
Все завершённые исходы — победы атакующего, поскольку целитель не атакует;
это не сравнение win-rate соревновательных сборок.

Ниже fortified_healer28, по1000 на очередь; A/B — атакующий первым/целитель
первым. Квантили раздельны; незавершённые — из1000 для каждой очереди.

| Режим | Тактика | Median A / B | p95 A / B | Незавершённые A / B |
|---|---|---:|---:|---:|
|instant|legacy_large_never|221 / 206|924 / 893|3 / 4|
|instant|legacy_large_always|861 / 751|— / —|145 / 174|
|instant|legacy_suppress_reserve|289 / 312|1175 / 1258|7 / 5|
|instant|planner_never|188 / 188|763 / 696|0 / 0|
|instant|planner_always|— / —|— / —|532 / 561|
|instant|planner_joint|168 / 176|718 / 715|0 / 0|
|long|legacy_large_never|478 / 504|— / —|61 / 69|
|long|legacy_large_always|450 / 437|1841 / 1812|33 / 39|
|long|legacy_suppress_reserve|396 / 426|1612 / 1758|25 / 29|
|long|planner_never|320 / 351|1296 / 1445|8 / 15|
|long|planner_always|1163 / 1112|— / —|302 / 309|
|long|planner_joint|439 / 387|1806 / 1941|40 / 48|
|adaptive|legacy_large_never|415 / 394|1610 / 1649|18 / 31|
|adaptive|legacy_large_always|681 / 732|— / —|122 / 113|
|adaptive|legacy_suppress_reserve|373 / 383|1522 / 1576|16 / 26|
|adaptive|planner_never|261 / 253|1022 / 1051|4 / 3|
|adaptive|planner_always|1948 / —|— / —|491 / 516|
|adaptive|planner_joint|325 / 326|1324 / 1282|8 / 13|

Первый эффект неоднозначен. Против instant main median planner_joint168–176,
planner_never188 и large_never206–221. Однако **оба planner-варианта не сделали
ни одной попытки срыва в обеих очередях**: на наблюдавшихся состояниях это одно
и то же поведение, разницу их медиан нельзя приписывать совместному бюджету.
Улучшение относительно large_never относится к Main-selector. Против long
planner_joint387–439 хуже planner_never320–351. Прежний T124 fixed_large+reserve_large на той же
усиленной дуэли давал независимую confirmation median290–298. Этот контроль
не включён в шесть новых тактик: его исторический результат нужно учитывать,
не объявляя улучшение относительно Always победой над всеми прежними решениями.
Сравнение с T124 не является парной выборкой; общие формулы/defaults сохранены.

У всех planner-тактик в healer28 main нет ни strong, ни large: фактический набор
состоит из ordinary/suppress/Recover. Наличие дорогих кандидатов в алгоритме
не доказывает, что он использует их. Уже завершённые отдельные confirmation
ячейки instant тоже имеют0срывов: planner_never179/180, planner_joint181/181;
это не итоговый verdict ещё не завершённой полной confirmation матрицы.

В unboosted_normal main median всех тактик14–19, без незавершённых; совместное
планирование не показало перехода к5–8раундам. В fortified_both36 результат
planner близок прежним large+Always. Полный вывод пока ожидает confirmation.

Main reuse:8damage computations/26160000hits,12Heal/28425039hits;
3planner profiles/9288states,54051profile hits,16236199lookups,
21767145value hits,240.8382мс подготовки внутри основного прогона.
Это измеренная цена вычисления, не доказательство улучшения игрового баланса.

Source-stage GPT-6 Astra/high review точного5fae43131..0da23b58d завершён без
замечаний. Проверены actual120test evidence,21новыйguard, общие действия/фазы,
legacy checksum/seed/shards, immutable keys и свежий допуск. Итоговый verdict
полных данных/отчёта открыт. Source после freeze не менялся; Heal не ослаблен.
