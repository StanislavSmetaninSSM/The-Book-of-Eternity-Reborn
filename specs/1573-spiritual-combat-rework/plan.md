# План перехода к личной модели духовного боя — #1573

## Текущий срез: формулы Средоточия и Гармонии

T051→T052→T053, baseline42bd2b82efcb760024fc96795b1e06d75fcaa984.
Brainstorming/Spec Kit bridge продолжаются в feature1573, только дизайн.
1. Зафиксировать принятие двух характеристик; сверить предыдущее развитие/ОД,
   отдельно обозначить перенос стартовых значений/+3/+5/ручных правил. Согласовать
   scope/spec/plan/tasks до расчёта. Узкая Sol6.1/high консультация альтернатив;
   parent выбирает модель и проверяет арифметику, independent review отдельно.
2. Сравнить фиксированные цены с растущими ресурсами и совместный масштаб стоимости/
   запаса/регенерации; проверить профиль5/7, автонаграды, ручную специализацию,
   D далеко за100, дроби/остатки и платёж-before-gain. Дешёвая модель последовательностей
   и равной дуэли с принятым уроном; никаких реальных сборок/игры/600s.
3. Написать воспроизводимый кандидат и пределы, синхронизировать текущие статусы,
   независимый Astra/high review interacting contracts/математики, исправления,
   verification/remote checkpoint/issue1573 и предъявление автору.

Bridge consistency перед probe: сохраняются принятые формулы/семантика источников,
нет реализации; новые численные предложения открыты. Простой арифметике новый
кэш не нужен, стабильные определения используют existing operation service;
current/ownership/authority/revision остаются свежими.

Результат T051–T053: reserve-and-harmony-proposal.mdv1, specv1.43/modelv2.33.
Raw S/H без C,Max40P+S,Regen4P+H/5,шкала8P/16P/24P и active16P предложены
вместе со стартом5/общими10очками/переносомceil(n/5)/+3/+5. Старые пять неизменны.
Parent и независимый Astra/high подтвердили107 профилей/321 дуэль,12006core,
4500границ и таблицы цен. Запрошенная сетка воспроизведения добавлена; blocking
findings нет.187 links/diff PASS, WIP remoteSHA/8blobs совпали. Кандидат готов
к предъявлению, full balance/runtime/поздние доходы не объявлены проверенными.

## Текущий срез: принятые воздействия, запас ОД и восстановление

T048→T049→T050, baseline56f8e4146b0db34e66146410f0b7613bd4f0eaa4. Продолжение
brainstorming/Spec Kit bridge в feature1573, без нового setup или production.
1. Записать согласие на damage/innate/crit, прочитать источники OD/max/recovery и
   mortal action economy, разделить code/GM/design. Bounded Sol6.1/high source audit;
   parent проверяет ключевые свидетельства и сохраняет границы spec→plan→tasks.
2. Сравнить2–3 подхода к запасу/росту/cost/regen; конкретизировать основной ход,
   быстрые действия и ответы настолько, насколько нужно для ресурса. Проверить
   дешёвые последовательности5–8 ходов, burst/sustain/recovery/дефицит/late/контроль,
   реальные антициклические ограничения и отсутствие refill от reentry.
3. Записать новый кандидат с решениями/альтернативами/открытым, independent
   Astra/high review, targeted correction/verification, remote checkpoint и issue.
   Предъявить автору новый пакет; T001 не снимается.

Bridge consistency до исполнения численной модели: старые arts/prices не обязательны,
принятые формулы/валютные контракты не меняются. Кэш не нужен для простой арифметики;
stable definitions через existing operation service, current OD/ownership/revision
и разрешение действия свежие. Нет новых обязательных cooldown/таймеров без обоснования.

Результат T048–T050: spiritual-action-economy-proposal.md v1, specv1.42/modelv2.32;
12 ОД, Main/малый Fast, цены2/4/6,passive2/MainGain4 на рассмотрение. Parent и
независимый Astra/high подтвердили15 распределений,1944 перехода и сертификат
чистого урона. Review устранил старые пометки принятия, blocking findings нет.
182 ссылки/diff PASS; WIP remote SHA и9 blobs совпали. Полная реализация T001
закрыта до согласования; текущий результат — написанный дизайн и честные пределы.

## Текущий срез: принятие HP и совместная настройка урона/врождённой защиты

T045→T046→T047, baselinefdbfb83f85da15753ca5b8c298982225136b74bb. Только
исследование в feature1573; T001 закрывает production. Brainstorming/bridge
продолжаются в канонических артефактах, без нового setup и дублирующего feature.
1. Зафиксировать согласие на HP/5–8; отделить неутверждённый current/max lifecycle.
   Сверить existing mortal/proposal damage/resistance/crit контракты. Scope→plan→tasks.
2. Сравнить2–3 подхода: прежние растущие проценты, ослабление/насыщение innate,
   совместный масштаб урона/защиты. Bounded Sol6.1/high консультация по альтернативам,
   parent — математика/выбор. Проверить равные и специализированные профили,
   D/F далеко за100, состояние F380, предметы/уязвимость и шкалу силы разных рангов.
   При необходимости предложить коррекцию неутверждённого crit как явную зависимость.
3. Записать написанный кандидат, независимый Astra/high review, targeted corrections,
   проверки/remote checkpoint/issue1573 и предъявление. Реальную игру не запускать.

Bridge consistency до арифметического исследования: запрос не разрешает менять
принятые HP/Q/D/M/экономику/слои защиты; новый урон/innate/crit — только proposal.
Простым формулам отдельный кэш не нужен; стабильные определения через existing
operation service, текущие ресурсы/authority/revision остаются свежими.

Результат T045–T047: specv1.41, модельv2.31, новое приложение урона/innatev1.
HP/темп отмечены принятыми; B28/насыщаемое innate/новые h-криты предъявляются
вместе как кандидат. Parent и independent Astra/high подтвердили266 профилей,
798 точных значений урона и200 проверок порогов. Замечание о старой повестке
исправлено и закрыто targeted review. WIPccf3a5b2 опубликован и прочитан по
полному SHA/восьми blobs; финальный checkpoint по Git-истории. Следующий шаг —
согласование написанного пакета автором, затем стоимость/ОД/бюджеты искусств.
T001 сохраняется, производство/игровые прогоны/clean restore UNRUN.

## Текущий срез: принятая ручная цена, личное HP и длительность дуэли

T042→T043→T044, прямой запрос автора, только источники/арифметика/документы.
1. Зафиксировать ceil(n/5); сверить mortal HP/damage и existing current/max
   семантику. Read-only Sol6.1/high аудит узких источников; parent проверяет
   релевантные выводы, не повторяет весь audit. Scope spec→plan→tasks согласован.
2. Сравнить варианты HP от Цельности/D, проверить cap/auto/equipment и ранние/
   поздние профили, калибровочный равный1v1 с явными damage/outcome допущениями.
   Базовая арифметика и вероятностная модель не являются реальным игровым прогоном.
3. Записать предложение в combat-system-proposal.md, independent review,
   согласованность/публикация и предъявление автору. T001 не снимается.

Bridge equivalent consistency выполняется до численной модели. Простые формулы
не требуют нового кэша; stable profiles/definitions переиспользуются существующим
сервисом, current resources/authority/revision свежие. Никаких game/build/600s runs.

Результат T042–T044: specv1.40, приложениеv1.17, модельv2.30. Ручная цена
принята; HP/5–8 раундов/current-max поведение предъявляются как новый кандидат.
Три варианта HP,10 профилей,4 бюджета, изолированные и12 совместных сценариев
бросков/crit/Rnatural; выявлена нерешённая зависимость позднего темпа от защиты.
Независимый Astra/high подтвердил расчёты и границы без замечаний. WIP07a2a213f
проверен по remote SHA/шести exact-ref blobs; финальный checkpoint по Git-истории.
Дальше — согласование написанного HP/темпа, затем совместный баланс воздействий,
защиты и ресурсов. T001 не снимается; production/clean restore UNRUN.

## Текущий срез: фиксация принятого допуска ручной покупки

T041, прямое согласие автора, только документы: синхронизировать spec/приложение/
общую модель и критерии с правилом base<C, исключением Favor и полными auto.
Проверить целые/половинные пороги и >100 без изменения цены, затем отдельное
Sol6.1/xhigh review, remote checkpoint и предъявление. Scope spec→plan→T041
согласован до проверки примеров (bridge equivalent consistency). Нового source
audit, runtime/build/probe нет; T001 сохраняется. Существующие DATA/fresh checks
не меняются, простому предикату кэш не нужен.

Результат: specv1.39, приложениеv1.16/5.11, модельv2.29. Пять граничных примеров
подтверждены parent и отдельным Sol6.1/xhigh; review без замечаний, diff check PASS.
WIPfa104cf1c опубликован и проверен exact-ref; final checkpoint по Git-истории.
Следующий шаг — обсуждение личного HP/ОД и оставшейся цены ручных вложений;
production не начинать, clean restore UNRUN.

## Завершённое исследование: принятие культивации и цена ручной специализации

Прямой запрос автора после принятия5.9, T038→T039→T040, только исследование.
1. Зафиксировать E5+2r/R100+25r; сверить start/+3/+5/D/M/Favor и границы scope
   в spec/plan/tasks (bridge equivalent consistency до арифметического probe).
2. Parent сравнивает2–3 тарифа на одинаковом бюджете: ранние/поздние ранги,
   partial cultivation, насыщение, >100, before/after auto, Favor/окупаемость;
   отдельная Sol6.1/high консультация ищет математические ограничения и альтернативы.
3. Записать результат в существующее приложение; independent review всего среза,
   проверка арифметики/diff/remote checkpoint, предъявление нового тарифа автору.

Код/игровые тесты/новая система валют/изменение D и автонаград не разрешены.
Для дешёвых формул отдельный кэш не нужен; fresh wallet/profile/revision/phase
проверки сохраняются, stable DATA через существующий whole-operation service.

Проверенный результат T038–T040: [spec v1.38](spec.md), приложение5.10/v1.15 и модельv2.28.
Принятие культивации записано; ручные ceil(n/5) рекомендуются на согласование.
Same-budget M/partial/Favor/order/saturation проверены parent и отдельным Sol6.1/high
консультантом. Independent Astra/high review завершён: одно P2 — старые E-цены
в действующей приёмке — исправлено на5.9, targeted review закрыл замечание.
WIP0a68ba0ff опубликован, remote SHA/шесть blob readbacks совпали. Следующий шаг —
предъявить новый ручной тариф автору для согласования, не production. Clean restore UNRUN.

## Завершённый исследовательский срез: источники Перьев и новый темп E/R

Разрешён прямым запросом автора2026-10-29 после возражения о37 жизнях.
Сначала сверить spec/plan/tasks (bridge consistency), затем T035→T036→T037:
1. Read-only аудит mortal settlement и spiritual reward/quest/trade/combat paths:
   размеры, условия, повторяемость, источники истины и разрывы prompt/runtime.
   Независимый Sol6.1/high исследует духовные источники без изменения файлов.
2. Parent сопоставляет реальные тарифы покупок и наград с2–3 кандидатами E/R;
   точная арифметика сценариев, Favor, частичных покупок и остатков. Условные
   профили не выдавать за телеметрию и месяцы не конвертировать в заданный доход.
3. Записать выводы в существующее приложение; независимое review целого среза,
   проверить diff/арифметику/ссылки, опубликовать и предъявить цены автору.

T001 остаётся gate производства. Нет игры/build/600s probe/новых income механик.
Тривиальной арифметике кэш не нужен; существующие DATA/authority границы сохранены.
Ранее исследованные тарифы ниже остаются историей и не задают текущую рекомендацию.

Проверенный исследовательский результат: [spec v1.37](spec.md), приложение5.9/v1.14 и модельv2.27.
Аудит выполнен, E5+2r/R100+25r рассчитаны и приняты последующим сообщением автора.
Ключевые ограничения: нет доказанного постоянного Feather income в Обители,
мелкие победы дают длинный путь, ручные5n уступают культивации по общей силе.
Independent Astra/high review T037 завершён: P2 в нормированном примере цены
исправлен90.09, остальные расчёты подтверждены. Следующий шаг — согласовать с
автором новую спецификацию/цены и открытые вопросы экономики; production inventory
ниже не исполнять. WIP9b1afbae6 опубликован и проверен exact-ref readback;
финальный checkpoint читается по истории ветки, clean restore UNRUN.

## Разрешённое исследование цены Сияния — 2026-10-29

Автор принял E20/25/30/35/40 и0/1/2/3, заказал расчёт R с +5/Favor и выбрал
постепенное замедление по рангам. По spec v1.36 результат — раздел5.8 приложения.

1. Сверить сохранённые источники/пороги/5n и сравнить2–3 тарифа полного Q_R.
2. Точно вычислить накопительный индекс и примеры целых/частичных покупок,
   сравнить специализацию на одинаковом кошельке и условный темп с Favor.
3. Проверить монотонность/дробление заказа/бесплатный R/все награды/остатки;
   согласованность spec/plan/tasks, независимое review и предъявление автору.

Производственные T001–T028 не разрешены. Внешних действий/игры/билдов нет;
дешёвой арифметике кэш не нужен, будущие DATA/authority границы сохранены.

## Завершённое исследование цены Просветления — 2026-10-29

Результат5.7 принят автором:20/25/30/35/40 и0/1/2/3. Ниже сохранён план
выполненного исследования, не новое разрешение менять принятые суммы.

По прямому запросу автора после принятия +3/+5 выполнить только расчёт из
текущей spec v1.35, результат — раздел5.7 приложения развития, не production.

1. Сверить источники дохода и старую доплату ранга, отделить принятые правила
   от модельных допущений. Сохранить 5n и бессрочное D, учесть ручной рост выше100.
2. Точной целочисленной арифметикой сравнить тарифы20+5r/25+5r и бюджеты,
   растущий Favor, альтернативы естественного E и чувствительность к ретробонусу.
3. Сверить таблицы, доход/расход/остаток, частичные и полные пересечения порогов;
   записать рекомендацию/ограничения, независимое review и предъявить автору.

Входы — приложение1/2/5.1/5.6 и Rules/Block_31_The_Soul_System.txt; новые
числа жизни/тарифа остаются предложениями. Выполнить bridge consistency
spec/plan/tasks перед расчётом; проверка не снимает старый T001 и не делает
исторический план ниже исполняемым. Игры, сборок, файловых нагрузок нет;
дешёвой арифметике кэш не нужен, production cache/authority контракт сохранён.

**Уточнение v1.3:** четыре характеристики подтверждены, свободное действие не
требует изученного искусства. Старые I–V/цены/ограничения/награды не обязательны
для нового дизайна. Старые требования сохранения механик ниже не исполнять:
plan пересобирается по текущей spec после выбора генерации/развития искусств.

**Checkpoint v1.2:** [кандидат полной системы v2](combat-system-proposal.md)
добавляет неутверждённые D7–D9 (разные искусства, формулы, большие группы).
Этот plan остаётся инвентарём предыдущего дизайна; до кода пересобрать его
вместе с tasks/contracts после точного утверждения spec. Не исполнять как план v2.

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
