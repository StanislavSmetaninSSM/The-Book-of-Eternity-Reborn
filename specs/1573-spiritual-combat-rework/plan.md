# План перехода к личной модели духовного боя — #1573

## T134 — план численного предложения о мастерстве

Baseline/origin847bc7f922276289087e3088e822d9691e1bd807; [scope](spec.md).
Применяются brainstorming/Spec Kit bridge/writing-plans к существующей feature.
T133 принимается явно; новый T134 остаётся кандидатом до обсуждения чисел.

1. Два read-only Sol6.1/high audit: mortal scaling/outcomes/AoE и реальное
   обучение/clock/costs. Parent сверяет принятый B/мастерство/исторические границы.
2. Записать формулы, пороги и начисления в существующий combat-system-proposal;
   отдельным bounded arithmetic check проверить таблицы и остатки без RNG/боевой
   матрицы. Результат арифметики — локально D:, краткий итог в документах.
3. Independent Astra/high review всего doc-only блока: отсутствие двойного
   scaling/awards, честный baseline, crit/Favor, границы времени/цены/памяти/NPC;
   ссылки/diff, публикация/readback/новое clean restore, предъявление кандидата.

Consistency: запрос→T134spec→T134.1–3. Уровень героя/формулы HP/ДЭ не меняются.
Сохранённый B нельзя молча трактовать как исходную базу без mastery: указать
опорный уровень при будущей нормализации. Mastery начисляется после действия.
Оптимизация: doc-only и малая арифметика, нового runtime/дорогого пути нет;
production reuse и свежая проверка authority остаются по existing service policy.

## T133 — план подготовки договора искусств, 2026-11-01

Goal: [scope T133](spec.md), результат в [существующем проекте](combat-system-proposal.md#t133-arts).
Brainstorming/Spec Kit bridge и writing-plans применены к doc-only разработке
предложения, не к реализации runtime. Baseline/origin fe5245392231e7ee6596180d322c14bb6240d855.

1. Read-only Sol6.1/high: mortal freeform/skill/mastery. Второй Sol6.1/high:
   духовные договоры/память/миграция. Parent проверяет ключевые Rules и
   TrainingService через Serena; без повторения всего аудита агентов.
2. Parent дополняет текущий combat-system-proposal.md: варианты и рекомендация,
   видимая польза изученного искусства, примеры Оков, личное развитие, память,
   открытые числа и будущие точки интеграции. Старые формулы не переписываются.
3. Independent Astra/high review взаимодействия свободных действий, владения,
   наград и source authority; проверить ссылки/статусы/scope, опубликовать WIP
   до длинного review, затем итог и новое чистое восстановление.

Consistency: запрос→T133spec→T133.1–3; новый код/численный эксперимент не разрешён.
Оценка оптимизации: сейчас только документы, новой вычислительной операции нет.
Будущий reuse неизменных определений/профилей — через существующий сервис с
owner/definition revision/mastery/зависимостями; живые ресурсы/эффекты/допуск свежие.
Конкретный cache/API/тестовый контракт выбирается при реализации, не этим проектом.

## T132 — выбор следующего вопроса, 2026-11-01

Brainstorming + Spec Kit bridge: существующая feature, [scope](spec.md).
Прочитать актуальные статусы и открытые договоры; отдельный Sol6.1/medium
read-only audit проверяет выбор следующего вопроса. Записать рекомендацию
в spec без нового игрового дизайна; независимый review краткого doc-only diff,
проверка ссылки и публикации. Реализация, пробы и изменение T127 исключены.

## T131 — аудит исходных правил, 2026-11-01

Запрос и границы: [spec](spec.md); deliverable: [существующее сравнение](mortal-combat-comparison.md).
Применяется systematic-debugging: источники и различия до предложений исправлений.
Это продолжение исследования существующей feature, без нового brainstorming
и без разрешения реализации. Baseline: e8e01f6465eaf57c51c536140743421595d4bb3c.

1. Два независимых read-only Sol6.1/high аудита: смертные Rules и духовные
   договоры/probes. Parent проверяет ключевые места и границу исполнения C#.
2. Свести формулы, единицы, цены, восстановление, очередь и статусы решений
   в T131-раздел сравнения; явно отозвать T130 grace/no-refresh, не менять баланс.
3. Independent Astra/high review источников и всего diff; исправить находки,
   проверить ссылки/объём правок, опубликовать и проверить свежим восстановлением.

Consistency: запрос→T131spec→T131.1–3; итог — доказательный отчёт, не новая модель.
Production/Rules/probes и сторонний1536/obj не менять. C# тесты, сборки и новые
численные матрицы не нужны. Кэша/горячего пути нет; прежняя диагностика T129
остаётся на D:, поскольку новый полный замер её не заменяет.

## T130 — план проектирования контроля, 2026-11-01

Goal: проверяемый doc-only проект видов контроля, личных сроков и освобождения.
Spec: [T130](spec.md), детали: [проект](control-effects-proposal.md).
Brainstorming/Spec Kit bridge и writing-plans используют существующую feature;
нового runtime, симулятора или плана его реализации сейчас нет. Baseline/origin
c8236e1049892ceb6adfc152b20da861397c9882 совпали; unrelated1536/obj не трогать.

- [x] T130.1 Read-only Sol6.1/high audit принятых T113/T115 и экспериментальных
  T129 контрактов; Serena csharp/typescript/python ready, реальные C#/JS symbols.
- [x] T130.2 Parent подготовил control-effects-proposal.md; обновлены spec/plan/tasks,
  сохраняет источники и статус кандидатов. Разобрать сроки/ответы на четырёх
  последовательностях, без RNG-матрицы и изменения старых probes.
- [x] T130.3 Independent GPT-6 Astra/high review взаимодействия запретов,
  срока/очищения/реакции/HP0/передышки; закрыть findings, проверить ссылки,
  diff/scope и воспроизводимость опубликованного документа. Показать автору
  конкретные предложения; не назвать новые правила уже принятыми.

Consistency до записи проекта: request→T130spec→T130.1–3; сохранённые договоры
отдельны от новых кандидатов. Встроенное право обычного разрыва не равно
автоматическому снятию; очищение подавления не становится универсальным.
Полные запреты Main/Fast/реакции нельзя обходить сменой имени/источника эффекта.
Оптимизация: doc-only, нового кэша/горячего пути нет. Будущие неизменные профили
и расчёты используют existing operation/cache; действующие ограничения,
ресурсы/фазы и право действия проверяются свежими. В эту задачу не входят
дорогие игровые фикстуры, тестовые запуски, новые цены лечения или внедрение.

## T129 — численная проверка контроля, 2026-11-01

Goal: дополнить ограниченный T128 контрольными искусствами, не ослабляя Heal.
Brainstorming + Spec Kit bridge/writing-plans/plan/tasks/analyze-equivalent;
автор отменил дополнительные согласования. Existing отдельный D:checkout,
baseline/origin a8bb390e46c412b08bb3d3877dcaf08e974224a8, unrelated1536/obj не трогать.
Один coherent Sol6.1/high source-owner, parent документы/измерения/интеграция,
независимый Astra/high reviewer. Новый runtime/GM/C# не входит.

- [x] T129.1 Sol6.1/high read-only audit принятых control/clock/reaction договоров;
  записать scope/параметры/матрицу/границы, опубликовать до source. Серверы
  Serena csharp/typescript/python ready; реальные JS/C#/Python symbols проверены.
- [x] T129.2 RED→GREEN узкий opt-in поверх periodic-v1 и общей encounter-машины.
  Создать probes/healing-control-model.mjs/.test.mjs и healing-control-probe.mjs/
  .test.mjs; узкие изменения healing-tactics-model.mjs, healing-tactics-probe.mjs,
  при необходимости periodic-model.mjs для fresh admission. Не копировать HP/
  ДЭ/clock/endpoints и не возвращать Heal interruption. API закрепить доGREEN.
- [x] T129.2a Проверить связанные numerical guards (144прежних+новые), legacy42
  и bounded periodic baseline; smoke504main/confirm +3shard union. Fixtures:
  source/art/target, forbiddencast/allowedbreak, stunMain/reaction vs freecheck,
  dueHoT/regen/refresh, applysave/crit/partial, цены доRNG, escape consumesMain,
  allyrelease/no extraMain, independentcontrols/expiry/exit/finaltick.
- [x] T129.2b Stable quality/damage/healing reuse через существующую operation;
  полные key/D/B/guard/resistance/profile зависимости. Живые ограничения не
  кэшировать. Измерить подготовку/lookup; no newparallelcache/дорогихgamefixtures.
- [x] T129.3 Frozen source commit/push/readback; main504×100/confirm504×1000,
  horizon2000,3shards при необходимости. Rawбезквот D:\BoeDiagnostics\healing-control-t129\20261101-v1.
  Сверить весьcoverage/seeds/endpoints/actorcounts/costs/controls/HP и сохранить
  плохие случаи. Не менять параметры после старта полной матрицы.
- [x] T129.4 healing-control-investigation.md: сравнение control/none, эффект
  в дуэли/2v2, полезность Heal, прерывание действий vs подавление чужогоHoT,
  расходы/время/отрицательныехвосты. Independent review, исправления/verification,
  publication/readback/newrestore/issue1573; старыйraw удалить после fullreview.

Preflight: request→T129spec→T129.1–4; accepted personalstate/ordinaryregen/
autonomousHoT/ownclock/freecheck сохраняются. Цены/сроки/save/разрыв обозначены
экспериментальными, не принятыми правилами игры. 336+56+112=504cases,
504×1100=554400. Не импортировать старый side-widecontrol и фиксированные
действия. Ledger вplan/review, бездублирующего Superpowers плана.

API доGREEN: `controlRules:'control-v1'`, `controlProfile:none|seal|stun|stun_long`,
`helperControlRelease:boolean`; periodic-v1/combined/cleanse-first и общий
periodicAttack:suppress_needed. Exact actor.controls; exports controlProfiles,
controlFamily, controlsFor, blocksControl, applyControl, expireControls,
breakControl, chooseControlMain. Existing group roster [primary,striker,healer,
helper]/[0,0,1,1] сохраняется; specочереди исправлены по actualcreateEncounter
до source. Helper с seal использует selfbreak, allyrelease только безCC.
Дополнительно controlPolicy:pressure|window; условияwindow зафиксированы вspec
до полнойматрицы, не выбраны по результатам. Тестировать ровно65%/полныйHP/
HoTactive, безскрытойинформации. Baseline none идёт одинраз сpressure.
Control snapshot хранит исходный M_Воли; breakdelta — свежийM освобождающего
минус snapshot. Третийаргумент checkedQuality есть Благоволение, не Воля;
не менять критпорог при ином Will. Полные фикстуры имеют равныеX/D.
Counters: controlCasts/Paid/Hits/SaveAttempts/Saves/controlsApplied,
controlBlockedMains/GuardDenials,
controlBreakAttempts/Succeeded/Paid/allyControlBreaks, aggregate/byActor.
Stable таблицы через existingoperation, fresh ограничения/платежи/rolls.
controlPaid входит вattackPaid и не складываетсяс ним повторно;
controlBreakPaid — отдельныйрасход. allyControlBreaks считаетоплаченные
попыткисоюзнойпомощи; успехи отражаютсяcontrolBreakSucceeded поactor.

## T128 — выполнение численной проверки, 2026-11-01

Goal: проверить точныйT127 в бою и сохранить полезность Heal. Spec — T128/T127
выше; автор разрешил проверку и отменил лишние согласования. Baseline30084bbe6.
Writing-plans/Spec Kit plan/tasks/analyze-equivalent + subagent-driven-development:
один Sol6.1/high source/test owner, parent измеряет/интегрирует, отдельный
Astra/high review целого блока. Existing D:checkout на story-ветке сохраняется;
unrelated1536/obj не трогать. Нового runtime/GM/C# в задаче нет.

- [x] T128.1 Перед source опубликовать spec/plan/tasks с684cases/правилами,
  выполнить consistency/preflight. Закрепить API численной реализации.
- [x] T128.2 RED→GREEN primitives+integration+runner. Предпочесть opt-in в
  `healing-tactics-model.mjs` с новой моделью `healing-periodic-model.mjs`,
  `healing-periodic-model.test.mjs`; runner `healing-periodic-probe.mjs` и
  `healing-periodic-probe.test.mjs`. Узко изменить tactics-probe для новой
  metadata/потока только если нужно; не копировать общую HP/ДЭ/очередь/endpoint
  машину. Defaults/T123–125 checksum остаются прежними. Новые функции явно
  экспортируются для дешёвых fixtures, без reflection/дорогих игровых объектов.
  При необходимости узко расширить `healing-modes-model.mjs`: optional divisor
  для общего исходящегоHoTbudget и точное дробное подавление через прежний
  healingCalculator/cache, сохранив default1 и integer результаты. Не создавать
  отдельную конкурирующую формулу/кэш. API source-owner фиксирует до GREEN.
- [x] T128.2a Проверить весь связанный numerical набор (120прежних+новые),
  smoke684main/684confirm n1h3,3shard union; exact profile/дробь22.5, payment до
  RNG, miss=>noSave,crit=>save,guard beforedice, source snapshot/flat×1,
  next3targetends/expiry/skip,weakforeign source/refresh/no duplicate ticks,
  revival без Main/regen, battle-end cleanup и отдельные utility-примеры.
- [x] T128.2b Existing operation cache владеет immutable damage/formula data.
  Key включает все параметры, в том числе guard delta, art level, fractional
  suppression, outgoing snapshot/incoming modifiers. Не кэшировать liveHP/
  допуска/остатоктиков/save. Измерить подготовку и повторное использование.
- [x] T128.3 Frozen source commit/push/readback перед main684×100 и
  confirm684×1000 (оба допускают3shards), horizon2000. Raw без квот
  D:\BoeDiagnostics\healing-periodic-t128\20261101-v1. Сверить всё coverage/
  sums/расходы/endpoints/union и fixtures; не менять параметры по исходам.
- [x] T128.4 `healing-periodic-investigation.md` с полным результатом и
  ограничениями; независимый whole-block Astra/high review, исправления/
  verification, GitHub/readback/freshrestore/issue1573. Старый полный raw
  удалить только после нового полного замера/review штатным способом;
  ранее отклонённые T123 пути не трогать. Прямо различать probe и production.

Preflight: primitive→integration (личные clock/payment/HP0); integration→runner
(counts/исходы); runner→report (684×1100,seed/shard); report→review/retention.
ScopesT127 неизменны; openHoT/R закрыты только явными T128 исследовательскими
допущениями. Это не окончательная продуктовая спецификация stack/развитияR.
Ledger остаётся plan/review, без дублирующего Superpowers плана/журнала.

API до GREEN: `healingRules:'periodic-v1'`, `periodicMode:direct|hot|combined`,
`periodicAttack:ordinary|suppress_always|suppress_needed`, `level:1..5`,
`effectResistance:0..100`, `cleansePolicy:never|first`, `activeGuard:boolean`.
Матрица использует R0/25/50/90. Exported primitives: periodicProfiles,
applyPeriodicHeal, tickPeriodic, applyPeriodicMark, choosePeriodicMain,
choosePeriodicAttack. Общий healingCalculator получает optional divisor=1 и
immutable outgoing snapshot; точный ключ включает divisor/snapshot/incoming/
дробное подавление. operation.table расширяется guardDelta=0, прежние defaults
не меняются. Runner periodicScenarios/ simulatePeriodic использует T128-v1 seed.
Дополнительные counters: direct/hotCasts/hotTicks/hotSuppressedTicks/markHits/
saveAttempts/saves/marksApplied/partialMarks/fullMarks/guardPaid и actualHP/
overheal/расходы по участникам. Живые HP/платёж/save/допуск не кэшируются.

Checkpoint до реализации: spec/plan/tasks опубликованы в5c810288705fb4cb87379ae446f5e5c971b59a81,
remote ref совпал, spec прочитан с GitHub по точному SHA. Sol6.1/high owner
выполнил RED14/14: прежний BigInt не принимает22.5%, новый opt-in/runner ещё
отсутствуют. GREEN/полная матрица/review на том checkpoint ещё не выполнены.

Source freeze T128:7source/test файлов, общий opt-in вместо копии машины.
`node --test specs/1573-spiritual-combat-rework/probes/*.test.mjs`:
143/143 (120прежних+23новых),0fail/skip/cancel,4061.435мс Node24.21.0.
Legacy42cases n3h20 deepEqual и SHA256
1e11934e9e7e958e11fed7fd8331c8f9117fad88640767cc0777180603756d7c сохранены.
Main/confirm684+684 n1h3 и3shard union точны; весь набор только numerical,
не project-wide suite. Parent прочитал actualdiff/guards/evidence. Исправлены
выявленные до замера несовпадения expires<=started+1/metadata/idle skip;
две utility-фикстуры поправлены по явному226HPудару и изоляции helper.
684encounter preparation44.769мс/16damagecomputations,20000lookup12.130мс,
новыхcomputations0. Пять n1/h2000 cheapcases0.631–15.140мс не являются матрицей.
Следующий шаг: сохранить sourceWIP, запустить main и independent review,
затем confirm всех684cells. Параметры/политики во время замера не менять.

Review поправка перед confirm: Astra/high нашёл отменённый dueHoT конца
победного хода; T112 требует его разрешить. Parent подтвердил договор,
source-owner RED→GREEN,144/144/4101мс,legacy42checksum сохранён. Первый
main39f317f7e (684×100,61.830с) помеченsuperseded для корректныхHP/counters.
Баланс/политики не изменены; повторmain и весьconfirm на исправленномSHA.

Frozen c36c9ee6e6462d9a4cd43f498f6ace628fc38a02 опубликован/readback;
reviewer подтвердил закрытиеP2. Main684×100 завершён62.015с,3exit0/stderr0;
coverage/params/seeds/outcomes/costs/HP/counters проверены,18726horizon.
Winner/endpoint результаты совпали с первымmain; correctedHP учтены новым
прогоном. Confirm684×1000 выполняется с теми же правилами. Подготовлен
[отчёт](healing-periodic-investigation.md), пока с явно незавершённымconfirm.

Полныйconfirm завершён:684000траекторий/583.483с,3exit0/stderr0;
full-verification.json проверяет1368cells/752400траекторий. Main/confirm
отдельны, все684случая подтверждены,186954confirmhorizon. Отчёт теперь
содержит полные срезы/отрицательныйрезультат/пользулечения/actualHP/costs.
Astra/high адресно закрылP2 и не нашёлfindings вmain-отчёте; итоговый review
полногоconfirm/отчёта впереди. Следующийкандидат сильныхатак не реализуется.

T128 завершён: whole-block GPT-6 Astra/high review до1290b773b — pass,
ревьюер независимо сверил1368cells. Parent171ссылок/diff/scope12files;
source unchangedc36. Все12файлов1290b773b прочитаны сGitHub и проверены по
blob вновомremote-restore-reviewed-20261101. После review штатно удалены
9rawT125+3supersededmainT128,8778472байта; текущийполныйзамер/компактные
свидетельства сохранены. ЗаблокированныеранееT123 пути не тронуты.
Финальный commit/readback/newrestore — issue1573; результат исследования
отрицательный дляустраненияstall, не изменяет принятыеусловия честнойзащиты.

## T127 — профили лечения/подавления и сопротивление, 2026-11-01

Writing-plans + Spec Kit bridge: active spec/plan/tasks; baseline/origin
4785a71c500b8fa071edebdef79bebd5a9f8a4c6 совпали. Goal — конкретный проект
силы/стоимости с честной защитой/save/очищением. Это bounded расчёт/документы,
не runtime/полная симуляция. Автор принял основуT126/шкалу; новые числа — кандидат.
Architecture — общие HP/damage/ДЭ и effect pipeline, отдельный точный gate метки.
Tech — существующий Node/PowerShell и точная арифметика без зависимостей.

- [x] T127.1 Read-only Sol6.1/high audit: damageR не effectR, обычная защита
  бесплатна, активная+5/4P; универсальный post-hit proc отсутствует. Parent
  прочёл фактические договоры/исходящие формулы. Сохранить различие в spec.
- [x] T127.2 Зафиксировать таблицу до расчёта; проверить15профилей наD130 и
  масштабированиеD0/50/130/1000/10000 (арифметика не даёт рангового допуска),
  flat/округление/послебонусное подавление, вероятность при R0/25/50/90.
  Малый одноразовый helper/raw толькоD:\BoeDiagnostics\healing-profiles-t127;
  использовать прежние formula exports для cross-check, не изменять runner.
- [x] T127.3 Записать проверенные значения/ограничения, согласовать статусы
  T126/исторических профилей. Independent Astra/high review точного doc diff
  и расчётов; scope/link/diff check, GitHub persistence/readback/новый restore.

Review focus: hit vs absorbedHP, naturalcrit+save,partial22.5%,flat×1,HoT не
channel; R источники открыты, no invented innate. Optimization:15профилей и
несколько точных подстановок не требуют нового кэша. Прежние immutable formula
таблицы использовать повторно; живые targetHP/метки/сохранённые кости не кэшировать
как разрешение. Полных боёв/таймерной оптимизации в этом блоке нет.
Bridge consistency: три требования автора→T127spec→T127.1–3; старые Heal
interruption/probe не переносятся, accepted damage/HP/ДЭ сохранены; blockers0
для арифметики, открытые R/HoTконтракты блокируют объявление полного баланса.

Checkpoint T127: WIP d097fcc07935fe72d95606b982aa50b8534cd18b опубликован,
originSHA совпал. Independent GPT-6 Astra/high review4785a71c..d097fcc0:
без замечаний; независимо воспроизведены числа/flat/22.5%/R/риск высокогоregen.
Parent188ссылок/doc-only/diff проверены. Финальные SHA/readback/новый sparse
restore отмечаются в issue1573. Цена/сила — проект; полноценный баланс не доказан.
Одноразовый арифметический helper хранится наD:, формулы/профили/полезные результаты
записаны в spec и воспроизводимы без этого helper. Предыдущий полный T125raw
сохраняется: арифметикаT127 не является новым полным боевым замером.

## T126 — концептуальная замена Heal, 2026-11-01

Baseline/origin d9c4b6b877247ce8a7a70da6bb5aa72026d19540 проверены; issue1573.
Brainstorming + Spec Kit bridge используют active spec/plan/tasks. Scope этого
блока — source-аудит/проект, без поведения/runner/прогонов. Автор отменил лишние
согласования процесса; собственные числа исполнителя остаются предложениями.
Сравнены сохранение двухфазного Heal (не отвечает новому запросу), два вида
доставки HP с атакующим подавлением (направление автора) и общая усталость от
лечения (лишняя глобальная санкция). Развивается второй вариант.

- [x] T126.1 Проверить прежние Heal/suppression, личные часы/HP0 и существующий
  periodic_restore/resource accepted-путь; записать реальное переиспользование.
- [x] T126.2 Записать в spec пожелания/кандидаты/неопределённости и приёмку;
  сравнить scope с T119/T114, сохранить исторические измерения.
- [x] T126.3 Независимое review документа, адресные исправления, проверки diff/
  ссылок/статусов, публикация/readback. Не выдавать review за численный баланс.

Consistency bridge: запрос→T126spec→три задачи, немедленный/HoT и no-interrupt
явные,90% после бонусов, индивидуальное состояние/HP0 и динамические искусства
сохранены. Силы/цены/stack/lifecycle открыты; новая реализация не запускается.
Optimization: вычислительных изменений нет. Для будущего HoT использовать
общий effect/resource pipeline и существующий operation-сервис; допускается
reuse неизменных DATA по полному ключу, живые HP/метки/тик/authority проверять
свежими. Численной экономии/производительности этот блок не измеряет.

Checkpoint T126: WIP aa0ed445a опубликован/remoteSHA совпал. Независимое
GPT-6 Astra/high review точного d9c4b6b..aa0ed445: без замечаний. Parent сверил
7doc-only diff,185файловых ссылок и отсутствие source/probe изменений.
Финальные publication/readback/новый sparse restore — issue1573. C#/frontend/
численная матрица UNRUN: результатом является проект для дальнейшего обсуждения.

## Исполнение T125 — 2026-11-01

**Goal:** проверить совместный выбор Main/реакции без ослабления Heal.
**Spec:** [T125](spec.md), автор разрешил автономное исполнение.
**Architecture:** opt-in planner над общей машиной; immutable прогнозы в operation,
live admission свежий. **Tech:** Windows/Node24.21.0/PowerShell7.6.6, без зависимостей.
Writing-plans + Spec Kit plan/tasks через bridge; ledger — plan/review.md.
Один Sol6.1/high владеет source/tests, parent измеряет/интегрирует; независимый
Astra/high проверяет interacting clocks/ресурсы/целый блок. Existing D:checkout
на story-ветке, не новый worktree; unrelated1536/obj не трогается.

### T125.1–2 — source и RED→GREEN

- [x] Source-аудит chooseAttack/doAttack/chooseInterrupter/stepEncounter;
  baseline/origin5fae43131 совпали, issue1573 открыт; h3/λ закреплены до source.
- [x] Создать `probes/healing-joint-budget-model.mjs`: immutable forecast curves,
  pure Main/reaction selectors. Сигнатуры закреплены ниже до GREEN. Внутренний
  key(h,energy,mark amount/countdown,mode/phase); outer key — version/h/λ,
  MaxDE/regen/Recover/цены, damage-quality distributions/resistance, Heal values
  при0/25/50, reaction odds/guard/forecast. Не кэшировать live permission.
- [x] В `healing-tactics-model.mjs` добавить duel-only `jointPolicy`
  ('never','always','joint'), observation/seams в doAttack/chooseInterrupter/doHeal,
  operation-owned подготовку. Reject совместно с interruptPolicy. Legacy schema/
  defaults unchanged. Новая metadata-compatible attackPolicy=fixed_large задаёт
  прежнему chooseMode максимальный возможный удар44, не будущий выбор planner.
  Opt-in roster явно сообщает planner; узкая правка healing-tactics-probe.mjs
  разрешена для этого поля. Mode записать до applyHeal, в том числе при срыве.
- [x] Создать `probes/healing-joint-budget.test.mjs`: RED λ=85.7825/86.4HP/ДЭ,
  разные terminal energy при одинаковом Recover; ручные бюджетные примеры пяти
  Main, skip/attempt/ties, равенство цены и нехватка1цента, отсутствие RNG/hidden
  energy/future guard, pure input. Fresh payment/reaction после мутации состояния,
  revival reaction без восстановления Main; обе очереди и current-HP cap.
- [x] Проверить forecast transitions против общей модели: stronger-mark renewal,
  weak-under-strong, expiry до/после target.begin/end, long start success/cancel/
  finish, initial instant/last observed mode. Фактические действия не заменять
  прогнозом. Legacy checksum до/после и прежние utility guards сохранить.
- [x] Создать `healing-joint-budget-probe.mjs`/`.test.mjs`, переиспользовать
  simulateTactics.108cases из spec, seedSuffix T125-main-v1/T125-confirm-v1,
  прежний seed pipeline; shards index%count, metadata/cell/complete/timers/reuse.
- [x] GREEN `node --test specs/1573-spiritual-combat-rework/probes/*.test.mjs`
  (99прежних + новые связанные guards), без C# build/fullsuite. Smoke108main/
  108confirm n1,h3 + shard union. Parent читает actual diff/output/evidence.
- [x] Измерить cold preparation и repeated lookup, complete-key separation/
  operation ownership. Не вычислять глубокий прогноз повторно каждый ход;
  при дорогой подготовке сначала named timings, не повышение лимита.

API до GREEN: `jointPolicies`; `normalizeJointMarks(marks,started,active)` и
`forecastJointMark(mark,quality)` возвращают `{amount,remaining}`;
`forecastJointPhase(phase,cancelled)` для instant/cast/finish;
`createJointBudgetCache()->{stats,prepare(profile)}`;
`chooseJointMain(curve,view)->{kind,score}`;
`chooseJointReaction(curve,view,policy)->{attempt,reason,skipScore?,attemptScore?}`.
Profile использует сотыеДЭ: maxEnergy/regen/recoverGain/costs,4сжатых массива
quality/probability/conditional damage, healing.instant/long при0/25/50,
interruptSuccess. Curve даёт lambda вHP/цент,value(view,h),immutable profile.
Новые счётчики jointDecisions/jointDecision_reason/jointMainDecisions только opt-in.
Будущие value не зависят от current missingHp; root reaction cap свежий.
Поддержанная пассивная дуэль имеет один источник подавления; многoисточниковую
форму нельзя молча сжать, если слабая метка переживает сильную: её явно отклонять
либо сохранять полный прогноз. Legacy работа marks остаётся прежней.

### T125.3–4 — измерения и завершение

- [x] Scoped WIP/source commit+push/readback до полного прогона/review.
- [x] На frozen SHA main108×1000 и confirm108×10000, horizon2000;3confirm shards
  разрешены. Raw без квот `D:\BoeDiagnostics\healing-joint-t125\20261101-v1`.
  После исходов не менять h/λ/политику. Parent проверяет coverage/seeds/sums/union.
- [x] `healing-joint-budget-investigation.md`: обе очереди и main/confirm отдельно,
  сравнение6тактик/профилей, actions/Recover/расходы/reasons/actualHeal/overheal,
  стоимость подготовки/reuse, ограничения прогноза и плохие хвосты.
- [x] Whole-block Astra/high review actual diff/contracts/raw, исправления и
  адресные проверки. Final publication/readback/restore/issue1573; production
  не реализуется. Retention после полного нового замера штатным способом;
  ранее не выполненная очистка T124.4r остаётся отдельно открытой.

Checkpoint: frozen source0da23b58d9a2c27a52b520c8f52c2d5a1a32bb2d,
main108000 завершён/проверен,3770horizon,176525.9676мс process,exit0/stderr0.
Confirmation1080000 завершена:38009horizon; wall960622мс для3shards,
всеexit0/stderr0. Parent проверил216cells/1188000trials/41779horizon и union/readback.
120/120guards; source-stage Astra/high review без замечаний.
[Отчёт](healing-joint-budget-investigation.md) сохраняет обе выборки по очередям
и отрицательный результат Joint. Whole-block GPT-6 Astra/high review
5fae43131..00972610a без оставшихся замечаний; P3 об области отсутствия large
исправлен.120/120 actual evidence,72endpoint и24accounting rows проверены.
Parent сверил54confirm строки/159ссылок. Удалены9старых T124JSONL/4581983байта
после нового полного замера/review; ранее отклонённые T123 не трогались.
Финальные SHA/readback/restore фиксируются в issue1573. Улучшение long/adaptive
не достигнуто; никаких новых Heal/production правил не принято.

### Consistency/preflight

| Производитель → потребитель | Проверяемый договор | Итог |
|---|---|---|
|T125.1→2|h3/λ/visible state → guards|Literal tests, без будущей оплаты|
|T125.2 внутри|curve→selector→общая машина|DATA не заменяют fresh admission|
|T125.2→3|runner108/seeds/shards→raw|108×11000=1188000, контролы неизменны|
|T125.3→4|frozen source/raw→report/review|Все очереди, negative/censor сохранены|
|T125.4 внутри|verification→publication/retention|No raw inGit, штатная очистка|

Bridge-equivalent speckit-analyze:6критериев покрыты (1/2→2,3→2–3,4/5→3,6→4),
unmapped0/неразрешённых противоречий0. Spec/plan/tasks обновлены без перезаписи
истории шаблоном. Optional plan/tasks/analyze hooks прочитаны; scoped commits
выполняются явно. Конституция2.1.0/категорийные проверки сохранены.

## Исполнение T124 — 2026-11-01

**Spec:** [v1.76/T124](spec.md); автор отменил отдельное ожидание review документа
после предъявления ebcfb6a21. **Цель:** выяснить пользу избирательного срыва при
дефицитеДЭ без ослабления Heal. **Метод:** subagent-driven-development/TDD:
один Sol6.1/high владеет связным source/tests, parent — интеграцией/измерениями/
отчётом, отдельный Astra/high — whole-block review взаимодействующих фаз/ресурсов.
**Среда:** существующий отдельный checkout на D:, story-ветка, Node24.21.0/
PowerShell7.6.6/Windows; новых зависимостей нет. Canonical plan/review — ledger,
новой копии в .superpowers не создаётся. Unrelated1536/obj не трогать.

### API, файлы и общие ограничения

- Изменить только `probes/healing-tactics-model.mjs`: exported
  `interruptPolicies`, `chooseInterrupt(view, policy) -> {attempt, reason}` и
  optional `interruptPolicy` encounter-option. View содержит только canAct,
  hasReaction, energy, interruptCost, strongCost, largeCost, mode, phase.
  Цены берутся из прежней `price` для самого участника; нет future regen/RNG/
  скрытых данных врага. Возвращаемое решение само не мутирует состояние.
- Политики: `never`, `always`, `reserve_strong`, `reserve_large`,
  `release_reserve_large`. Порядок причин: inactive, no_reaction, no_energy,
  policy_never, long_start, reserve, attempt; записывается первая применимая.
  Фаза long_start проверяется только у release_reserve_large; instant и finish
  допускаются при выполненном резерве. Сравнение остатка включает равенство.
- Без новой option сохранить T123 on/off/выбор кандидата и numeric output.
  Новая option используется только в duel; неподдерживаемую group-комбинацию
  отклонять явно, чтобы не выдумывать многосубъектную политику. `interrupt:false`
  сохраняет прежнее выключение срыва. Оплата и кости по-прежнему в applyHeal.
  Новые причины/числа решений собирать только для opt-in T124, не менять прежний
  schema/counters default T123. Одна причина на каждую допустимую попытку Heal.
- Создать `probes/healing-interruption-policy.test.mjs`,
  `probes/healing-interruption-probe.mjs` и `.test.mjs`. Runner переиспользует
  simulateTactics/createOperation, не копирует actor/Heal state machine.
  Exports `interruptionScenarios(matrix)`, CLI `--matrix=main|confirm --samples=N`
  плюс детерминированные `--shard-index=I --shard-count=N` для распределения
  исходных индексов без смены cell/seed. По умолчанию shard-count1.
- Полные matrix params из spec120cells; собственный seedSuffix T124-main-v1 /
  T124-confirm-v1 передаётся в существующий seed pipeline, дополняя policy.
  Seed не зависит от shard/index/time; параметры и seeds сохранять в JSONL.
  Каждая shard пишет metadata с исходными индексами, cell и complete/counts/
  elapsedMs/formulaReuse. Точные имена допустимо уточнить в ledger до замера.

Сохранены все пять игровых договоров, source-based Heal/личный расход, фазы,
смерть/возврат, существующий attack reserve16P даже при Never. Нет новогоAI,
урезания Heal, новых цен, проигнорированных исходов или предсказания костей.

### T124.2 — дешёвые проверки и реализация

- [x] Записать RED guards pure selector: приD130 срыв28.8ДЭ, strong57.6,
  large86.4; energy86.39/86.40 дают отказ/допуск reserve_strong,
  115.19/115.20 — reserve_large. Низкая фактическая energy не исправляется
  будущим regen. Проверить equality, pure view, недееспособность/noReaction.
- [x] RED real runner: Never/недостаточный резерв не списываютДЭ/реакцию и
  не вызывают reaction RNG; разрешённая попытка оплачивается до dice.
  Release пропускает start long, оставляет pending/его предоплату и допускает
  finish при достаточнойДЭ, с личным refresh между ними; instant не блокируется
  проверкой фазы. Проверить причины пропуска и единичность учёта.
- [x] Зафиксировать дешёвый default T123 контроль до изменения (обе очереди,
  instant/long/adaptive, on/off): без option результаты сохраняются точно;
  новые counters не меняют прежние summaries. Старые84tests не переписывать.
- [x] Реализовать selector/seam/runner минимально; fresh budget check остаётся
  в applyHeal, Main/час/оплата не дублируются. Проверить matrix120unique/cross
  product, main-confirm suffix, shard union/без дублей/те же seeds, validation
  некорректного shard. Отдельное mutable состояние на каждый trial.
- [x] GREEN `node --test specs/1573-spiritual-combat-rework/probes/*.test.mjs`.
  Это связанный numerical набор, не C# suite. Smoke main/confirm n1,horizon3
  и небольшие shards; counts/endpoints/metadata/stderr/exit проверить.

### T124.3–4 — измерение, review, сохранение

T124.2 evidence:99/99passed,3269.2947мс Node/3506.0386мс shell,exit0/stderr0.
Main/confirm smoke120cells каждый и2main-shards по60 прошли; default checksum
12cases неизменен. Новые counts: interruptDecisions/interruptDecision_<reason>
в aggregate/byActor, только opt-in. Ruling: collapsedThisTurn запрещает Main,
но не сохранённую реакцию после revival; лишний conjunct удалён по RED→GREEN.
Source frozen для дальнейшего замера; подробности/хеши в review и raw наD:.

- [x] Опубликовать frozen source/WIP перед дорогим прогоном. Основная матрица
  120×1000; confirmation120×10000, horizon2000. Можно запускать shards параллельно
  и перераспределять ещё не начатые indices, сохраняя seed/полную выборку.
  Неполные cells не использовать. Raw `D:\BoeDiagnostics\healing-interruption-t124\20261101-v1`,
  без byte/record quotas. СтарыйT123raw оставить до полного нового замера.
- [x] Parent проверить coverage/params/seeds/sums/exit/stderr и объединение;
  отчёт `healing-interruption-investigation.md`: median/p95/null/horizon по
  обоим порядкам, attacks/Recover/Heal, расходы/попытки/успехи/причины отказа,
  actualHeal/overheal. Сравнить пять политик внутри одинаковых условий;
  main и confirmation отдельно, не подбирать новый порог по удачному результату.
- [x] Independent Astra/high review точного source/docs diff и raw; проверить
  отрицательные выводы, сохранение пользы Heal и отсутствие переноса на1×2.
  Исправить подтверждённые находки и адресно проверить. Scoped publication/
  GitHub readback/restore, issue1573. Retention после полного verified замера
  выделен в T124.4r: удаление21старого файла отклонено automatic policy, файлы сохранены.

**Оптимизация:** reuse operation.table/healingCalculator, полный immutable key
уже существует; новые predicates дешёвые, им отдельный кэш не нужен. Fresh self
state/admission, никакого удержания изменяемого результата решения. Посчитать
actual formula reuse и wall time. Это probe, productionService–Repository не меняется.
**Review focus/guards:** стоимость до dice; отсутствие future regen; long phase
против reaction refresh; default T123 неизменность; shard/seed идентичность.

### Полный замер — checkpoint

Sourcebb2048a26 неизменен;240cells/1320000траекторий,62589horizon,
4процесса exit0/stderr0. Main wall299.673с, confirmation3shards wall873.063с.
Проверены все seeds/params/indices/sums/оплата/reasons/union; full-verification.json
наD:. Полный [отчёт](healing-interruption-investigation.md) готов к итоговому
независимому review; оно завершено без существенных замечаний. Две P3 ошибки
округления исправлены/пересчитаны parent. Замедление в трудных случаях осталось;
Heal не менялся.156ссылок/accepted contracts/frozen blobs проверены;9файлов
76f84b9e восстановлены из GitHub в существующем чистом sparse checkout.
Очистка21старого raw/17425474байта заблокирована automatic review (без подробной
причины), не выполнена. Финальная публикация/readback/restore — issue1573.

### Consistency/preflight до реализации

| Задачи/граница | Производитель → потребитель | Проверка |
|---|---|---|
|T124.1→2|Spec/plan → selector/guards|Все5политик, literal thresholds, original Heal сохранены|
|T124.2 внутри|pure view → applyHeal/метрики|Решение до RNG, свежая оплата; opt-in counters, default unchanged|
|T124.2→3|runner/matrix/seeds → shards/report|120cases, samples/horizon/suffix, union проверяемы|
|T124.3→4|full raw/frozen SHA → reviewer/publication|Отрицательные результаты не закрываются условием «стало быстрее»|
|T124.4 внутри|review/verification → retention|Удалять старый raw лишь после полного нового; не game saves|

Bridge equivalent speckit-analyze: все6критериев spec имеют задачи (1/4→2,
2/3→3,5→2–4,6→4), unmapped0, противоречий/неразрешённых контрактов0.
Конституция2.1.0, category-only verification, #1573 и no-production соблюдены.
Plan/tasks phases применены к действующим артефактам; setup не перезаписывает
исторический план. Optional hooks plan/tasks/analyze прочитаны, автошаблонизация
не требуется; scoped commits выполняются явно. Разрешение автора снимает вопросы
о повторном согласовании документа, но не engineering/verification/review.

## T124 — подготовка scope, исторический checkpoint

[Spec v1.76](spec.md) предлагает исследовать выбор момента срыва, сохранив Heal.
Автор согласился с направлением; точный документ ещё не показан/не утверждён.
Сейчас только source-аудит, запись scope и независимое review документации.
Не запускать новую пробу и не менять numerical source/tests до T124.0a.
После согласия — superpowers:writing-plans и соответствующие Spec Kit plan/tasks,
затем bridge consistency, TDD, один владелец связного блока и отдельный reviewer.
Текущий раздел не выдаётся за завершённый execution plan.

Source seams проверены через Serena: chooseInterrupter, doHeal, applyHeal,
stepEncounter. Свежий выбор реакции должен видеть текущуюДЭ и фазу до броска;
фактический расход остаётся в applyHeal/interruption. End-регенерацию нельзя
добавлять к резерву для начала следующего личного Main. Default T123 не меняется.
Reuse DATA — существующие operation.table/healingCalculator; новый кэш простых
предикатов не нужен. Runtime Service–Repository не затронут.

Bridge consistency scope: пять политик/личная оплата/фазы→T124.1–2;
120cells main+confirmation/честные хвосты/отрицательный результат→T124.3;
сохранённые Heal/utility→T124.2–4; independent review/публикация/retention→T124.4.
T124.0–0a закрывают gate до кода. Старые production-задачи не получают разрешения.

Scope review завершён: отдельный GPT-6.1 Sol/xhigh проверил diff741944f6..0a168c8b
и source seams, замечаний нет. Parent подтвердил144файловые ссылки (без anchors),
4doc-only changes и неизменность исходников/пяти принятых договоров/T123report.
WIP0a168c8b9 опубликован, remoteSHA и specblob19a3d6e3c78e76e80c9fa51527c61a7a945bbfe2
сверены. T124.0a остаётся открытым: нужен review точного документа автором.
Новые тесты/симуляции/production UNRUN; полный T123raw сохранён.

## Исполнение T123 — согласовано 2026-10-31

**Spec:** [v1.75, T123](spec.md), одобрена после показа commit11dbb105f.
**Цель:** измерить пользу выбора атаки/цели, не ослабляя лечение.
**Архитектура:** отдельный standalone runner поверх существующих numerical
actor/Heal/damage функций. Старые модели неизменны. Один исполнитель владеет
новыми source/tests; parent интегрирует, измеряет и пишет выводы; отдельное
Astra/high review проверяет полный блок. **Среда:** Windows/Node24.21.0/PowerShell7.6.6.
Применяются writing-plans, subagent-driven-development/TDD и Spec Kit bridge.
Этот plan/review — единый ledger; старый .superpowers/sdd/plan принадлежит #1536.

### Зафиксированные настройки пробы

Все actor D130, равные исходные характеристики/HP/MaxDE, кроме явно названных
бонусов. Начальные HP/ДЭ полные. LevelV instant/long из T119, цена источника.
`normal` — обычная регенерация всех; `healer28` —28P только у healer;
`both36` —36P у каждого. Heal%/extraRpp получает только healer; у остальных0/0.
В зеркальном составе усиления одинаковы у обоих healer. Это чувствительность,
не утверждённый бюджет экипировки. У каждого своя реакция и эффект.

Новые файлы в probes/: `healing-tactics-model.mjs`, `healing-tactics-model.test.mjs`,
`healing-tactics-probe.mjs`, `healing-tactics-probe.test.mjs`.
Model exports: `chooseAttack(view, policy)`, `chooseTarget(actors, actorId, policy)`,
`choosePatient(actors, healerId)`, `teamDefeated(actors, team)`,
`createEncounter(options)`, `stepEncounter(encounter, dice)`, `runTrial(options)`.
Probe exports: `tacticsScenarios(matrix)`, `simulateTactics(options)`;
CLI `--matrix=duel|support|group|confirm --samples=N` пишет JSONL metadata/cell/complete.
Названия/интерфейсы можно уточнить без смены scope, записав фактические в ledger.

Политики атак:
1. `fixed_ordinary/strong/large/suppress`: только названный профиль, иначе Recover.
2. `greedy`: доступная large, затем strong, ordinary, иначе Recover.
3. `reserve`: тот же порядок, но после атаки и своего end-regen остаётся16P
   (8P для возможного срыва и8P для следующей ordinary); иначе Recover.
4. `suppress_reserve`: сначала suppress при выполнении того же бюджета, если
   сила подавления меньше50 либо все50marks истекут до начала следующего
   личного места цели (`expires <= target.started`); иначе физический `reserve`.
   Повторное50подавление с прежним источником не продлевается без нужды.
Каждая оплата сейчас; планируемый regen не разрешает занимать ДЭ. Решения
не получают RNG/будущий выбор/скрытую энергию врага. Профили фикстуры известны.
Срыв включён отдельным флагом. На видимый Heal один способный враг с реакцией
и8P выбирается по наибольшей текущей своей ДЭ, tie — положение в очереди;
остальные сознательно не вмешиваются. Reply guard/counter платит именно caster.

Цели `patient`: healer, затем остальные; `helper`: не-healer, затем healer;
`switch`: минимальная текущая доляHP, tie — healer, затем очередь. Только доступные
неcollapsed враги. Это видимые тактики, без знания будущего урона.
Triage healer: сохранить цель pending; иначе collapsed ally, затем наименьшая
доляHP среди раненых союзников, tie — сам healer, затем очередь. При fullHP всех
long/adaptive разрешено начать на себе, как T120; instant без дефицита — Recover.
`cleanse_safe` замещает своё лечение при50на себе, remaining>1 иHP>25%, как T120.
`mix50` атакует ordinary, когда у всех союзниковHP>50%, никто неcollapsed и нет
pending; иначе triage/Heal. Helper очищает союзника с наиболее сильным эффектом
(tie: collapsed, меньшая доляHP, healer, очередь), иначе в passive Recover,
в offensive ordinary/recover. Adaptive использует прежний chooseMode по casterHP
и известному максимальному одиночному урону БЕЗ дополнительного сопротивления,
как T120: B44 для primary с выбором атак, фиксированныйB для fixed; в группе
наибольший известный профиль фактических врагов, у healer/helper/striker только
ordinary. Не считает будущие броски.
Это прежняя однопороговая эвристика, не оценка суммы всех групповых угроз.

Составы: duel A(attacker)–B(healer); support A–B(healer),C(helper);
group A(primary),B(ordinary striker)–C(healer),D(helper).
Mirror: A(healer),B(helper)–C(healer),D(helper), одинаковые capabilities/bonus.
В группах все дееспособные имеют базовый срыв, но в одном root выбран только один.
Healer лечит союзников, helper сам не получает искусства лечения. Второй striker
не получает лечебных искусств. Перед действиями старых cleanse/recover/attack
проверяются collapsed/Main lock; HP>0 ниже return threshold не запрещает живого
actor вне распада. Личные пропущенные места сохраняются; team defeat только при
collapsed всех членов. Не добавлять новые эффекты/барьеры/карту/продуктовыйAI.

### Матрица до измерения

Все перечисленные комбинации, D130, horizon2000; samples1000 в первых трёх
матрицах. На каждый независимый trial отдельное mutable состояние. Seeds записаны,
сравнения разных policies не объявляются парными траекториями. Сначала выбор/оплата,
потом RNG; damage/reaction потоки разделены.

- **duel core:**50/25 ×3harmony ×3mode ×2order ×2reply(guard,counter)
  ×2defender(heal,cleanse_safe) ×7attackPolicy =504cells.
  Controls:(0/0,25/25) ×(healer28,both36) ×3mode ×2order ×guard ×heal ×7=168.
  No-interrupt:50/25 ×(healer28,both36) ×3mode ×2order ×guard ×heal ×7=84.
  Итого756cells.
- **support:**50/25 ×(normal,both36) ×3mode ×6order ×3target
  ×2helperAccess =216. Controls:(0/0,25/25) ×both36 ×instant ×6order
  ×3target ×2access =72. Primary suppress_reserve, healer heal/guard,
  helper passive. Итого288cells. В обеих access-ветках одинаковый новый runner;
  недоступность исключает helper только из attack candidates, не из лечения.
- **group:**50/25 ×(normal,both36) ×3mode ×24order ×3target
  ×2defender(passive,offensive/mix50)=864. Controls:(0/0,25/25) ×both36
  ×instant ×24order ×3target ×offensive=144. Primary suppress_reserve.
  Mirror:50/25 ×(normal,both36) ×3mode ×24order ×switch ×offensive=144.
  Во всех reply guard; все permutations. Итого1152cells.
- **confirm:** независимый seedSuffix, samples10000, заранее выбранные случаи:
  duel50/25 ×(healer28,both36) ×(instant,adaptive) ×2order ×guard ×heal
  ×(fixed_suppress,greedy,reserve,suppress_reserve)=32;
  support50/25 ×both36 ×instant ×6order ×3target ×2access=36;
  group50/25 ×(normal,both36) ×instant ×24order ×switch ×offensive=48.
  Итого116cells/1 160 000траекторий; общая плановая выборка3 356 000.

Общий endpoint total — конец всей схватки, censor еслиhorizon. FirstPatientFall
и firstAnyFall отдельные endpoints; в access-control сравниваются именно они.
Если patient не падал до победы своей стороны, это competing outcome/no-event,
не победа атакующего и не факт длительности2000. Отчёт хранит долю достижений,
условные квантили наблюдённых событий и unconditional rank с null, когда событие
не достигнуто достаточной долей выборки; отдельно horizon и no-event при конце.
Для total median/p95=null при недостижении квантили до horizon. Персональные
действия, расходы, damage/heal/overheal, срывы, смены целей, collapse/revival.

### TDD, проверки и исполнение

- [x] T123.2 RED: guards на реальную оплату/бюджет/решение до RNG; GREEDY vs reserve
  дают разные действия на фиксированной низкой ДЭ. Два контрольных состояния
  сHP72: collapsed запрещён, живой допускается. Смена цели меняет resistance;
  падение healer не завершает группу; allyHeal возвращает helper без extra Main;
  pending не переназначается; expiry на skipped slot; один выбранный interrupter
  тратит свой ресурс; остальные не теряют реакцию. Guards всех endpoint/censor.
- [x] GREEN: новый runner поверх принятых функций; существующие файлы probes
  не менять без воспроизводимого дефекта. Ранние tests проверяют числа fixtures,
  а не копируют вычисления implementation. Matrix guard проверяет уникальность/
  полное покрытие/подтверждающую выборку. Дешёвый smoke до длинных измерений.
- [x] Запустить `node --test specs/1573-spiritual-combat-rework/probes/*.test.mjs`:
  ожидается0fail, старые66tests и новые. Это связанный numerical набор, не C# suite.
  Сохранить WIP/source/checkpoint в GitHub до полной матрицы.
- [x] T123.3 Выполнить4матрицы с raw в `D:\BoeDiagnostics\healing-tactics-t123\20261031-v1`;
  проверить metadata/count/complete/outcomes/exit0/stderr. Сохранить источник и
  краткий `healing-tactics-investigation.md`; utility T122 сохранить в проверках.
- [x] T123.4 Независимый Astra/high whole-block review с фактическим diff/raw;
  исправить подтверждённые замечания, затем scoped verification/push/readback.
  Не считать более короткий бой достаточным без сохранённой полезности лечения.

**Оптимизация:** таблицы создаются один раз на measurement operation по полному
D/B/targetResistance ключу (остальные характеристики равны в явных fixtures),
healCalculator переиспользуется с полным formula identity. Fresh actors/marks/
channel/допуск, никакой общей mutable стороны. Raw без квот, не в Git. Старый T122
raw удалить только после полного проверенного T123 и сохранения краткого сравнения.
Production/ГМ contracts и категории тестов не меняются; C# tests/build UNRUN.
**Review focus:** предсказание костей; повторный Main/reaction после revival;
target-specific protection иpending; сравнимость outcome; достаточно явные
ограничения слабых heuristics/неравных ролей. Все связаны с guards выше.

Bridge consistency перед кодом:spec A→политики/duel; B→support/group/endpoint;
сохранённые договоры→reuse+guards; utility→старые guards; honesty→metrics/report;
публикация/retention/review→T123.3–4. Материального изменения согласованного scope нет.

Ruling T123.2: равные cleanse-кандидаты разрешаются в пользу healer после
collapsed/долиHP, а не самого helper; это уточняет описанное triage. Adaptive
сохраняет прежний unresisted threshold и использует реальные профили врагов:
mirror не получает воображаемую large-угрозу. Оба случая закрепляются guards.
Ruling оптимизации: в passive support после распада healer, при недоступном helper,
полнойДЭ живых, отсутствии marks/pending и на границе раунда дальнейшие события
детерминированно состоят только из Recover/skipped. Можно точно агрегировать
оставшиеся личные часы и счётчики до horizon; это censor, никогда не победа.
Эквивалентность полному циклу проверяется на actors/counts/endpoints и трёх очередях;
при trace-hook shortcut выключается, чтобы не пропускать диагностические события.
Квоты записей/байтов нет. Вне доказанного absorbing-state расчёт идёт полностью.
Unique(actorId,started) используется один раз за пробу, retry отсутствует; поэтому
результат applyHeal можно убрать из probe actor.receipts после чтения. Старый
идемпотентный API/его tests не меняются; это не политика runtime-истории игры.

Ruling T123.3 scheduling: после завершения group/support подтверждение разделено
по неизменным индексам исходной матрицы. Полные cells0–14 сохранены; один собственный
worker остановлен после проверки PID/parent/exact command. Незавершённыйcell15
не включается в анализ. Оставшиеся15–115 считаются тремя workers по `(index-15)%3`.
Каждый вызывает тот же simulateTactics с тем же cell/n10000/horizon2000, поэтому
seed и численный результат не зависят от shard. Это распределение вычислений,
не изменение выборки или правил. Перед объединением требуются exit0 каждого
worker, exact index coverage без дублей, metadata/params/seed/count/outcomes.
Исходный process exit-1 — намеренная остановка для распределения, не сбой теста;
финальный отчёт не должен утверждать, что он завершился exit0.

T123.3 завершён: source c5bd379b6,84/84guards,2312cells/3 356 000траекторий.
Все три confirmation workers exit0; исходные0–14 и результаты15–115 объединены,
точные параметры/seeds/coverage/outcomes/endpoints проверены по генератору.
Отчёт [healing-tactics-investigation.md](healing-tactics-investigation.md)
сохраняет неудачу reserve при дефицитеДЭ и затяжной helper-focus. T123.4: whole-block
GPT-6 Astra/high review завершён; уточнены3формулировки без изменения модели.
Удалены9старых T122raw-файлов (1579245байт), currentraw сохранён. Проверены143
относительные файловые ссылки (без anchors),13неизменённых старых probe-файлов,
5сохранённых игровых договоров и scoped diff9файлов. Source c5bd379b6, полный
отчёт опубликован84602b700; итоговый checkpoint, remote readback и restore —
в журнале ниже/issue1573. Production-gates не менялись.

## T123 — подготовка следующего исследования, ожидается review scope

2026-10-31, baseline 493cce3df289da5bc0c5a5f0ae29b055fad319e9,
ветка story/golden-mask-backstory-20261025; origin SHA и issue1573 сверены.
[Спецификация T123](spec.md), [задачи](tasks.md), [проверка](review.md).
Применены brainstorming и spec-kit-superpowers-bridge к существующей feature1573.
В этом блоке только source-аудит и письменная спецификация. Новая модель/замер
не выполняются до рассмотрения именно записанного scope по AGENTS владельца.

После принятия: этап A — выбор атаки по ДЭ с прежними контролями; этап B —
доступный помощник, групповой исход и сравнение 2×2. Перед кодом дополнить этот
план через writing-plans/Spec Kit phase конкретными политиками, матрицей и guards,
выполнить bridge consistency; затем TDD, bounded execution и независимое review.
Пока это порядок дальнейшей работы, не готовый план исполнения и не результат.

Повторное использование: неизменные таблицы урона/формулы существующей numerical
модели; ключи включают все влияющие характеристики источника/цели/профиля, scope
одна операция измерения. HP/ДЭ/marks/channel/реакции и допуск всегда свежие.
Не создавать игровые объекты или второй production cache; расширение частных
таблиц требуется только при действительно новых входах групповой фикстуры.
Production/ГМ/UX/1536 не меняются, поэтому игровые сборки и категории не запускаются.
Проверки этого документального блока: source evidence, ссылки, diff, согласованность
spec/plan/tasks, независимое review и удалённая сверка. Численные tests UNRUN:
их исходники не меняются. T122 raw сохраняется до полного следующего измерения.

Read-only консультация GPT-6.1 Sol/high audit_t123_tactics подтвердила ограничение
simulateModes, предложила три обозримые эвристики и указала риски групп: нельзя
заканчивать бой по HP0 пациента, игнорировать возврат помощника или допускать Main
после распада при HP ниже порога возврата. Предложение учитывает эти границы,
личные ресурсы и возможность ответного урона. Полный групповой AI не обещается.

Проверен документальный блок:131 файловая ссылка (без anchors), неизменность пяти
принятых договоров и diff-check. WIP0570f37525fa98110a9e7a21e342c1dab2758c12
опубликован; origin SHA и spec через GitHub прочитаны обратно до review.
Независимый GPT-6 Astra/high review нашёл два P2: неоднозначность запрета действий
ниже порога возврата и несопоставимый конец боя с недоступным помощником.
Исправлено: запрет только при сохраняющемся распаде; общий endpoint сравнения —
первое падение пациента/участника, итог всей группы отдельно. Тот же reviewer
адресно проверил исправления, дополнительных замечаний нет. Новые пробы UNRUN.
Следующий шаг — показать фактическую spec v1.75 автору; только после одобрения
выполнить T123.1. Финальный commit с этим checkpoint публикуется и сверяется
через remote SHA/четыре blob и issue1573; историческая часть issue сохраняется.

## Завершённый план T122 — проверка усиленной сборки без ослабления лечения

Baseline aa2f7e5aae59adc92624dc2a144991223bc8b305 подтверждён origin/#1573.
Продолжается ранее согласованное исследование feature1573: автор прямо поручил
соответствующие расчёты и проверки, особо сохраняя полезность лечения. Новых
правил не проектируем; это применение принятых формул к следующим случаям.
Spec Kit bridge/writing-plans/executing-plans/TDD используются в этих canonical
артефактах. [Spec](spec.md), [tasks](tasks.md). Старые документы остаются историей.

1. Сопоставить T119/T114 и damage/energy; сохранить все согласованные формулы.
   Main28/8P, strong36/16P, large44/24P — существующие уровни атаки, не новые
   бонусы к подавлению. Suppress остаётся B24/12P. Никаких penetration/dispel
   с выдуманными ценами, лимитов Гармонии или уменьшения B лечения.
2. Добавить только параметр силы обычной атаки в standalone runner. При default
   не менять seed/options/исходы T120. TDD: точные стоимость/урон/отказ за нехваткой
   ДЭ, запрет бесплатно совместить strong с suppress, прежняя таблица0extra.
   Полезность отдельными fixtures: немедленное спасение от382HP при HP320;
   long против2instant на двух ударах382 при HP600; союзникHP0/порог103,
   +25/+50% и подавление50%, цена/Main/отсутствие бесплатного возврата хода.
3. Основная матрица D130: instant/long/adaptive, обе очередности,
   normal/healer28/both36, пары heal%/extraResistancePP=(0,0),(25,25),(50,25).
   Тактики: ordinary/strong/large без срыва; ordinary+срыв с none/guard/counter;
   strong+срыв с guard/counter; suppress с/без самоочищения; suppress+срыв
   с guard/counter и с/без самоочищения.1000траекторий/cell, horizon2000.
4. Support отдельно: D130/both36, усиления25/25 и50/25,3режима,6очередностей,
   suppress без срыва и suppress+срыв guard/counter; помощник только очищает.
   Scaling: D0/1000,both36,50/25,3режима/2очередности, ordinary/strong/large,
   strong+срыв guard, suppress+срыв guard+самоочищение.1000/cell.
   Confirmation: D130/both36,25/25 и50/25,3режима/2очередности,
   strong+срыв guard/counter и suppress+срыв guard+самоочищение;10000/cell
   с независимым seed. Без подбора новых чисел по результату.
5. Отдельный отчёт полезности: фактические HP/стоимость/Main/ожидание/возврат
   союзника; никакого вывода о ненужности лечения из одной длительности дуэли.
   20/60/2000 сохраняются как ориентир базового равного stress; усиленная
   сборка и support не объявляются равной базой. Для них показываем фактические
   хвосты/цензурирование/ресурс и пользу контрмер, не обещаем всеобщую победу.
6. Tests только numerical feature1573; immutable damage profiles готовятся
   один раз на полный набор входов, fresh actor/effects каждый бой. Существующий
   игровой service/cache не меняется; дорогих игровых объектов нет.
7. WIP/push до длинных матриц/review. Raw D:\BoeDiagnostics\healing-amplification-t122,
   без квот; старый T120 raw удаляется лишь после полного нового замера и сохранения
   нужного краткого сравнения. Итоги вGit, независимое Astra/high review целого
   блока, final remoteSHA/blob/issue readback. Production/UX/1536 не меняются.

Приёмка — воспроизводимые расчёты/guards, честный вывод об усилениях и полезности,
не автоматически успешный баланс и не изменение согласованных игровых чисел.
Результат:66/66guards,0.283с, default parity324cells×20;1 284 000траекторий,
960cells,4complete/exit0/пустые stderr и outcome sums проверены.
[Итоги](healing-amplification-investigation.md) показывают и полезность лечения,
и оставшиеся случаи затяжного боя. Матрицы42335ea5d, utility-only fix077bced26
опубликованы/remoteSHA/source readback подтверждены. Числа T119 неизменны.
Независимый GPT-6 Astra/high review aa2f7e5aa..ab62c6c71 всего8файлов/raw:
существенных замечаний нет. Повторно66/66guards;960raw cells/utility,
3confirmation cases×10000 и27damage tables/10800исходов проверены reviewer.
Единственная неточность prose p951967 исправлена по исходным данным; код не менялся.
129относительных файловых ссылок (не anchors), четыре accepted contract blobs
неизменны. Clean remote restore ab62c6c71 совпал по8изменённым файлам.
После нового полного замера удалены только8точных файлов старого T120 raw,
2735392байта; текущая диагностика наD:, прежняя проверочная clone-копия не удаляется.
T122 выполнен как исследование. Финал: scoped commit/push, remoteSHA/blob и
exact #1573 readback, сохранение исторического tail; production gates открыты.


## Активный план T120/T121: проверка принятых двух режимов

Baseline b704855c9164834c50ed11143b3d6ed7b3f61020 подтверждён на origin и в #1573.
Автор прямо одобрил точный [T119](healing-modes-proposal.md), включая два Main,
цены, окна срыва и критерии численной проверки. T121 фиксирует принятие.
Применяются writing-plans/executing-plans/TDD и Spec Kit bridge к этой feature;
существующая отдельная копия/story-ветка сохраняется. Это исследование, не runtime.

1. Обновить spec/tasks/статус T119; PathsOnly1573 и bridge consistency: требования
   T119 §§2–7 → T120.1–4, сохранённые T107/T112/T114/внебой/раны → неизменные
   исходные договоры, исследование → численные выводы, не готовность производства.
2. RED: новые дешёвые tests на независимых actor fixtures. GREEN: отдельные
   healing-modes-model/probe, переиспользующие неизменённые распределения урона,
   цену, подавление, реакции T116. Проверить start/finish оплату/Main/expiry,
   прерывание до/после принятия, свежие силу/подавление/надбавки, HP0/порог,
   неизменность цели/уровня, повтор ID, отмену/Ready; тестировать фактический runner.
3. Запустить только локальную категорию численных проб feature1573:
   `node --test specs/1573-spiritual-combat-rework/probes/*.test.mjs`.
   Она покрывает изменённый standalone-модуль и импортируемые старые контракты;
   C#/frontend/GameState не меняются и не требуют дорогой подготовки/общего build.
4. WIP commit/push и remote/readback перед полной матрицей. База: D0/130/1000,
   обе очередности, Harmony normal/healer28/both36; новые instant/long/adaptive,
   ordinary без вмешательства, suppression, reserve interruption с none/guard/counter,
   suppression+reserve с самоочищением. Старый V — отдельный контроль без изменения.
   Адаптивная политика выбирает по видимому HP/ДЭ/плетению, не будущим костям:
   готовое плетение завершает; новый long предпочитает при HP выше максимального
   следующего обычного попадания, иначе instant. Ограниченность этой эвристики явна.
5. Отдельные матрицы: помощник очищает (все шесть очередностей; это support-stress,
   не полноценный 2v2); смешанная атака/лечение; Воля±5; оплаченные, но принудительно
   неуспешные срывы; повышенное лечение +25/+50% и дополнительное сопротивление25%
   пациента как чувствительность. База и такие усиления не смешиваются в выводе.
   Основные cells по2000 траекторий, confirmation по10000 с независимым seed;
   горизонт2000 раундов — граница наблюдения, не квота диагностики.
6. Raw JSONL без квот под D:\BoeDiagnostics\healing-modes-t120; хранить все cells,
   счётчики действий/платежей/срывов/завершений/отмен, metadata и complete.
   После полной проверки нового замера удалить только старые принадлежащие
   исследованию raw, проверив абсолютные пути; в Git исходники и написанные итоги.
7. Показать median/p95/max/censored, обе очередности и худшие базовые cases.
   Принятые пороги equal/stable-energy: median≤20, p95≤60, censored=0 при2000;
   если не пройдены — честно отметить, не менять значения или критерии.
8. WIP с результатами → независимый Astra/high review diff от baseline вместе
   с guards/runner/raw/выводами → исправления → final diff/remote/blob/issue readback.

Оптимизация: immutable numerical profiles и damage/heal tables строятся один раз
для полного набора входов данной пробы; это исследовательские fixtures без игровых
объектов/файлового I/O в траектории, не новая production cache-архитектура.
Каждая траектория владеет свежим actor/channel/marks/reactions. Фазы проверяют
актуальные HP/energy/access/допуск, лечение пересчитывается по текущим модификаторам;
кэш нельзя использовать как разрешение продолжить истёкшее плетение.
Ledger данного блока — этот раздел и review.md; прежний .superpowers/sdd/plan
принадлежит #1536 и не используется. Reviewer проверяет timing/policy bias,
округление/энергию, достаточность cases и границы заключения, не только зелёные tests.

Исполнение:59/59 numerical guards;4 224 000 актуальных траекторий, четыре
complete/exit0/пустые stderr;134файловые ссылки без anchors; T119 §§2–5
байтово неизменны, старые пробы не правились. Основной WIP69a824895 и исправление
sensitivity ea99d08fd опубликованы/remoteSHA/GitHub blob подтверждены.
[Результаты](healing-modes-investigation.md) сохраняют непрошедшие усиленные cases
и границы модели; базовые324 stableenergy cells прошли20/60/2000.
Независимый GPT-6 Astra/high review0b204257b всего11файлов/raw нашёл двеP2
в Ready: не закрывалась подготовка при отказе и Main-lock мешал сохранённой
реакции после возврата. Обе воспроизведены RED и исправлены;59/59GREEN0.287с,
адресный follow-up подтвердил отсутствие оставшихся замечаний.324cells×20
идентичны старому runner; матрицы не содержат затронутых Ready/revival ветвей.
Восстановление0b204257b в новый clean sparse clone с GitHub:11файлов совпали.
После нового полного замера удалены только729839байт старого raw T116.
T120.4/T120 завершены по исследовательскому результату, не по production gates.
Финальная публикация: commit/push, remoteSHA,11blob readback, update/readback
#1573 с сохранённой историей. Модель/итог доступны для продолжения без нового
согласования уже принятого T119.

## Исторический срез T118–T119: перед одобрением нового профиля

Baseline1411f4074f5c83e24f24c9d14b012cc8b724c193 сверён с origin/#1573;
workflow1.7/конституция/performance/PathsOnly1573 подтверждены. Продолжаются
brainstorming и Spec Kit bridge в этой feature: [spec](spec.md), [tasks](tasks.md).
T118 — прямое одобрение показанного T117. T119 — новый профиль по измеренному T116;
автор выбрал два режима через async-вопрос. Это не одобрение ещё не показанных чисел.
Parent отвечает за полный договор; Sol6.1/high консультирует по throughput/рискам.
Обычные source/fixture чтения и алгебра разрешены; новая симуляция/код не исполняются.

Порядок: принятые статусы → сравнение вариантов/алгебра → healing-modes-proposal.md
и spec/tasks → consistency/ссылки/сохранность формул → WIP/push → independent
Astra/high review целого блока → исправления → финальный remoteSHA/blob/issue
readback → фактический профиль автору. T120 остаётся открытым: после одобрения
новой спецификации подробный план/guards/TDD/модель/проба; старые результаты не
объявляются доказательством нового баланса. Никаких production/UX/wounds1536 правок.

Review focus: throughput без помощи срывов; два оплаченных Main при успешном
длительном лечении; никакого автоматического payout+нового Main; отмена/расходы
по реально принятым этапам; collapse/возврат/target/price/актуальная сила и suppression;
реакции/Ready/HoT не создают второе полное лечение бесплатно. Для будущего runtime
сохраняется существующий Service–Repository/операционный слой: стабильные
definitions переиспользуются, состояние двух разных личных ходов не считается
одним неизменным snapshot. Pending stage/authority/price/target проверяются свежими.

T118–T119:129файловых ссылок (не anchors), старый блокHeal, шесть путей принятых
договоров/проб и diff/consistency проверены. WIP62674bead044b8e592f8af8d1a61f98da1102e76
опубликован, remoteSHA/GitHub readback сверены. Отдельный GPT-6 Astra/high
review_healing_modes_t119 проверил все7документов и соседниеT107/T112/T114/ДЭ:
замечаний нет. Новые числа не объявляются принятыми или проверенными в бою.
Финальная публикация: commit/push/remoteHEAD/7blob и exact #1573 с сохранённой
историей. После показанного профиля ждём решения автора; T120 остаётся открытым.

## Текущий срез T115–T117: проверка принятого профиля

Goal: проверить принятый T114 против самоисцеления и предложить понятное объяснение.
Spec: [spec](spec.md), [принятые реакции](reactions-and-interruptions-proposal.md),
[границы исследования](reaction-balance-investigation.md), [tasks](tasks.md).
Baseline3c1c895b610a98ba6f797d4c77822ec61a1f4d2f совпал с origin; отдельная копия
проекта/веткаstory остаётся рабочей, main/1536 не затрагиваются. PathsOnly1573,
workflow1.7/конституция и Serena csharp/typescript/python проверены; JS symbols доступны.
Пользователь прямо одобрил показанный T114 и поручил численную проверку; новых
правил до пробы не вводим. Используются writing-plans/executing-plans/TDD и Spec Kit
bridge, журнал — этот checkpoint/tasks; duplicate Superpowers plans не создаём.

Архитектура: Node24 standalone probe поверх прежних чистых numerical fixtures,
без production imports. Создать probes/reaction-model.mjs (личный budget/Heal/sрыв),
reaction-model.test.mjs (инварианты), reaction-probe.mjs (политики/матрица/метрики).
Не менять старые пробы, чтобы сохранить историческое воспроизведение. Основные
interfaces: createActor/beginTurn/endTurn, attemptHeal, react, checkedQuality;
probe simulate(options) возвращает метрики, решения получают только текущее состояние.
Временный кэш неизменяемых damage/проверочных таблиц на профиль/пробу, мутабельные
HP/ДЭ/метки/остатки всегда личные; не создавать дорогие game objects в траекториях.

- [x] T115: статусы/spec/crossrefs; показаны фактические spec/plan/tasks.
- [x] T116a: failing guards shared spend/reset/no-refund/no-regen/HP0, full/partial
  interrupt, counter-interruption/defense, cleanse и длительность; запустить RED,
  реализовать минимальную модель, GREEN всего probes/*.test.mjs.
- [x] T116b: политики без foresight, матрица/профили/seed/censoring, дешёвый smoke;
  WIP/push/readback; полный прогон с raw D:/BoeDiagnostics/spiritual-reactions-t116/;
  отдельно10000на значимые выводы и support/Will sensitivity. Динамических лимитов
  диагностики нет; горизонт2000 — цензурирование, не доказанная бесконечность.
- [x] T116c/T117: прочитаны полные результаты, сведён краткий отчёт/объяснение,
  independent Astra/high review всего coherent diff, исправления/адресная проверка,
  финальный commit/push/blob/issue readback. Только численная модель; нет game build.

Review focus: 1) reset личный, не общийround; 2) цена и место потрачены при срыве;
3) противник вправе ответить на срыв тем же общим бюджетом, без повторного ответа
исходного actor; 4) HP0 не регенерирует/не действует; 5) защита/политики не видят
будущие dice, контроль имеет те же stats/horizon, censored не выдаются за победу.
Каждый пункт покрывается дешёвыми guards/сверкой матрицы. Консистентность до
выполнения: T115↔одобрение, T116↔запрошенная проба, T117↔UX-предложение;
нет нового production-контракта/невыбранных цен, требования покрыты задачами.

T116a:13новых guards (вместе с16прежними —29) проходят на Node24.21.0.
Первый RED на API scaffold:10ожидаемых assertion failures/2pass; после реализации
12/12GREEN, затем добавлена полная сверка400равных встречных исходов и guard5.
Smoke30траекторийD130/counter: модель завершила все, игровые выводы не делаются.
Затем WIP/push перед основным прогоном396ячеек×2000; результаты ещё не получены.

WIPd0dd9d5ca31d320ab9dccab95389635956766454 опубликован до долгой пробы;
GitHub source readback подтверждён. Последующие confirm/support/sensitivity
завершены;1 854 000траекторий,31/31guards. Raw4части/пустыеstderr/complete/cells/
win+censor проверены. [Результаты](reaction-balance-investigation.md) и
[UX-предложение](combat-explanation-proposal.md) готовы к independent review;
доказано улучшение выбранных сценариев, но не решение heal stalemate.
Parent не менял принятые числа. Sol6.1/high отдельно проверил actual UI/help/preview,
SerenaC#/TS symbols, старыеoperation_type/position/AP обозначены как будущая замена.
Далее checkpoint/push, сложный Astra/high review всего diff и финальная публикация.

Checkpoint59d918af4401077c236c306d9bedff8def29cdb6 опубликован/readback и remoteSHA
сверены перед review.191файловая ссылка/3DEfences/неизменность старых проб/diffcheck
проверены. Независимый GPT-6 Astra/high review_reaction_balance_t116 проверил17files,
модель/исходники/31tests/raw/отчёт/UX: существенных замечаний нет. По его замечанию
для ясности order назван самостоятельным входом, не новым правилом инициативы.
Модель/цены/результаты после проверки не менялись; повтор миллионов не требуется.
Завершение: финальный commit/push, remoteHEAD/17blob и exact #1573 readback с
сохранением истории. UX-профиль остаётся на рассмотрении, production gates открыты.

## Текущий срез T113–T114: принятие хода и активные реакции

2026-10-30 baseline617b63b0d78f0f7cec1cc8b31f0f769d0429f26c совпал с origin,
#1573/workflow1.7 сверены, PathsOnly1573 выполнен. Продолжаются brainstorming/
Spec Kit bridge: [spec](spec.md), [tasks](tasks.md). T113 записывает одобрение T112;
T114 — новый проект реакции/цены/счётчика, а не готовый runtime или баланс.
Parent сводит принятые ДЭ/HP0/очередь/барьеры и пишет профиль; Sol6.1/high отдельно
проверяет mortal Block/Parry/Ready, pending dice/preview и реальные effect reactions.
Порядок: источник → проект/статусы → consistency/ссылки/ручные примеры/diff →
WIP/push → независимый Astra/high review → исправления/адресная проверка →
финальный SHA/blob/issue readback → автору. Никаких code/tests/build/симуляций;
полный баланс и T001/T009–T015 остаются открытыми. Цена реакции не доказывает
устранение старого healing stalemate. Стабильные профили/источники — через existing
operation service; pending identity/права/цена/остаток/фаза проверяются свежими.

T113–T114: WIP3a985faf86d646e4762d5ca51a28e590f8e4902a опубликован до ревью,
remoteSHA и профиль GitHub сверены. Parent проверил165файловых ссылок (не anchors),
три неизменённых блока ДЭ, семь ручных примеров и consistency/diff. Независимый
GPT-6 Astra/high review12документов без блокирующих замечаний; единственная P3
устранена: прикрытие сохраняет identity/атакующий пул/защитную кость удара,
подставляя модификатор фактической цели без переброса. Parent проверил точную
правку и зависимости assigned dice/обычных проверок; полного нового ревью не нужно.
Финальный checkpoint: commit/push, remoteHEAD и12blob, точное тело #1573 с неизменной
историей. T114 остаётся предложением; этот результат не разрешает production.

## Текущий срез T112: инициатива и состав личного хода

2026-10-30 baseline9600203d787de07bf8e22e8bcfc3f3b89bd5e7ea совпал с origin;
#1573 прочитан, workflow1.7 сохранён, PathsOnly1573 выполнен. Продолжаются
brainstorming/Spec Kit bridge в активной feature: [spec](spec.md), [tasks](tasks.md).
Parent сверяет принятые игровые договоры; Sol6.1/high отдельно исследует смертные
правила и реальный источник turn events. Результат — предложение личной очереди,
Main/Fast/свободного намерения, границ/пропуска/возврата без side clocks.
Полные реакции/искусства/засада/численный баланс отдельно; новая механика не
считается принятой запросом темы. Порядок: источники → проект → consistency/
ссылки/примеры/diff → WIP/push → независимый Astra/high review → исправления
→ финальный SHA/blob/issue readback → рассмотрение автором. Код/tests/build/
симуляции не запускаются. T001/T009–T015 и прочие технические gates открыты.
Повторный parse профилей/модификаторов выполняется через существующий operation
service: cache DATA по actor/revision/source/round; свежий допуск действия и
единственность хода проверяются отдельно. Не кэшировать тривиальную арифметику.

T112: WIPc8c4e30883909dfed8ef582c975f26c112e9e6a8 опубликован до review,
remoteSHA и GitHub readback профиля сверены.134относительные файловые ссылки
(без anchors), неизменность трёх блоков ДЭ, пять ручных последовательностей,
diff/consistency проверены. Независимый GPT-6 Astra/high review_initiative_turns
проверил весь coherent diff9документов и связанные исходники/HP0/ДЭ; замечаний нет.
Документальный срез завершён, профиль ожидает рассмотрения автора. Финальная
публикация сверяет9blob/remoteHEAD и #1573 с сохранением предыдущей истории.

## История T109–T111: обыск и сохранность Маски приняты

T111/2026-10-30: автор ответил «принимаем» на полный профиль T110.
Baselineae63cd16c4c635db2a6eaa5550f45d3d2b123d33 совпал с origin; текущие
#1573/#1572 и workflow1.7 сверены, PathsOnly1573 выполнен. Это только фиксация
одобрения без новой механики: синхронизировать статусы1573/532, проверить
неизменность игровых разделов/ссылки/diff/consistency, WIP/push, независимый
Sol6.1/xhigh review, финальный SHA/blob/issues readback. Runtime/tests/build/
симуляции не запускаются; технические gates и решение о реализации открыты.

T111: WIP34aad01e0da7ba62db674ab8f27370df801db3c5 опубликован до review,
remoteSHA/readback подтверждены.161относительная файловая ссылка проверена
(без anchors); игровые разделы1–8 неизменны после нормализации трёх фраз
принятия, diff/consistency проверены. Независимый Sol6.1/xhigh review нашёл
одну P3: старая подпись активной ссылки532 называла T110 предлагаемым.
Подпись исправлена и адресно проверена parent; других замечаний нет.
Финальная публикация T111 сверяет9blob/remoteHEAD и два issue с сохранением истории.

2026-10-30 baseline6c5f43bed5099206974c41c2df70bbf11ea3375d совпал с origin;
#1573/#1572 прочитаны, workflow1.7 сохранён, PathsOnly1573 выполнен.
Продолжаются brainstorming/Spec Kit bridge в активной feature. T109 записывает
принятие T108 и прямое требование неутрачиваемой Маски. T110 — новый проект
обыска/изъятия, не готовая реализация. Canonical: [spec](spec.md), [tasks](tasks.md).
Parent исследует существующие SR-01–08/принятые профили и пишет документы;
Sol6.1/high отдельно проверяет реальные inventory/equipment/trade/loot источники.
Порядок: источники → требования/проект → consistency/ссылки/diff → WIP/push →
независимый GPT-6 Astra/high review → исправления/адресные проверки → финальный
SHA/blob/issues readback → точный документ автору. Новые цены, schema, код,
tests/build/симуляции не входят; T001/T077/T012–T013 и532/T005/T023 открыты.
Будущая оптимизация использует общие стабильные снимки инвентаря/экипировки,
с полной инвалидацией владения/места/эффектов/защиты; свежий допуск передачи обязателен.
Сейчас измерений runtime нет, новое кэширование не реализуется.

WIPf60e7e125f9f2f8c3466f260e881f3a8b2742e81 опубликован до review; remoteSHA
и GitHub readback нового профиля подтверждены.184относительные файловые ссылки
(без anchors), три неизменённых блока формул Масок, diff/consistency проверены.
Независимый GPT-6 Astra/high review_spiritual_loot проверил все11документов
и связанные исходники/договоры: замечаний нет. Документальный блок завершён;
правила обыска T110 впоследствии приняты T111. Финальная публикация этого проекта сверяет
11blob/remoteHEAD и точные тела #1573/#1572, сохраняя историю и open-статусы.

## История T107–T108: принятие HP0; проект капитуляции и плена

2026-10-30 baselinecae83c77e175da68e80b266141439aaa676151a0 совпал с origin.
Прочитаны #1573/#1572, PathsOnly1573 выполнен, workflow1.7 сохранён.
Продолжаются brainstorming/Spec Kit bridge в активной feature. T107 записывает
прямое одобрение T106 без изменения формул. T108 предлагает следующий игровой
договор; новые правила ещё не одобрены. [Спецификация](spec.md),
[профиль](surrender-and-captivity-proposal.md), [задачи](tasks.md).

Sol6.1/high audit_surrender_captivity отдельно проверил реальные добровольность,
side-wide control (не переносить), lifetime/source ownership, combat vow,
клятву Сарефу, terminal-proof и obsolete reset старого перехода. Serena csharp/
typescript/python сохранены, C# символы прочитаны. Parent сводит проект и
принятые HP0/rest/free travel; схема пленения/новые цены/обыск/runtime не делаются.
Порядок: источники → проект/статусы → consistency/ссылки/diff → WIP/push →
независимый GPT-6 Astra/high review coherent diff → исправления/адресная проверка
→ финальный SHA/blob/issues readback → документ автору для рассмотрения.
Никаких игровых tests/build/симуляций; T001/T077/T012–T013 остаются открытыми.

WIP0741f78f50ce08b8a3ef1df1dc788140e229723c опубликован до review; remote SHA
и GitHub readback нового профиля сверены. Проверены144относительные файловые
ссылки (без anchors), неизменность блока формулы HP0, diff/consistency и открытые
production-gates. Независимый GPT-6 Astra/high review_surrender_captivity проверил
все11документов actual diff и связанные исходники; замечаний нет. Документальный
проект завершён, правила T108 ожидают рассмотрения автора. Финальная публикация
проверяет11blob/remoteHEAD и точное readback #1573/#1572 с сохранением истории.

## Текущий срез T106: HP0 и возвращение формы

2026-10-30 baseline832fd4051731add252752ab872b1c091d8910907 совпал с origin,
#1573/#1572 прочитаны, PathsOnly1573 выполнен, workflow1.7 сохранён.
Продолжаются brainstorming/Spec Kit bridge в той же feature. Parent исследует
принятые/предложенные HP/ДЭ/лечение/предметы и готовит [кандидат](zero-health-and-form-recovery-proposal.md).
Sol6.1/high audit_zero_spiritual_health отдельно проверил реальные source:
mortal HP0 не готовая модель, ресурсная инфраструктура пригодна при явной
поддержке AfterlifeActor, развеивание/forced incarnation имеют отдельный proof,
рановые helpers не являются работающим HP-revive. Serena ready, C# symbols прочитаны.
Scope: личный распад, порог10%, помощь/очередь/часы, естественный покой и явный
кандидат1час за периодHP. Не определять вместо этого весь плен/loot/гибель,
не утверждать цены боевого лечения и не менять1536. Порядок: источники/варианты →
полный кандидат → cross-doc consistency/арифметика/ссылки/diff → WIP/push →
независимый GPT-6 Astra/high review HP/resource/effect/terminal contracts →
исправления и адресная проверка → финальный SHA/blob/issue readback → точный
документ автору. Только docs; никакого runtime/tests/build/новой симуляции.
T001/T077/T012–T013 и технические планы остаются открытыми до своих стадий.

WIP81e517aa2d0a9b8c0a76d183cdc995deb9dfbd54 опубликован до review; remoteSHA
и файл с GitHub сверены. Parent проверил порог/накопление/отдыхMax1…4,124файловые
ссылки (без anchors), consistency/diff; две исходные ссылки Validation исправлены
до WIP. Независимый GPT-6 Astra/high review_zero_spiritual_health нашёл P2:
в старом абзаце combat-healing оставалось снятие недееспособности при любомHP>0.
Абзац заменён точным T106-порогом/очередью; адресная проверка рабочего дерева
подтвердила устранение, других замечаний нет. Финальный шаг —9blob/HEAD и
#1573/#1572 с историей, затем точная спецификация автору. Баланс/код не проверялись.

## Текущий срез T105: принятое усиление

2026-10-30 baseline6bec82a1206a6c3a3cf169682d3fff972c44f0fc совпал с origin;
два issue прочитаны, PathsOnly1573 выполнен, workflow1.7 сохранён. Прямая
фиксация одобрения точного T104, без нового brainstorm/чисел/пробы. Обновить
активные статусы1573/532 и resource-приложения, не менять формулы/пять свойств,
условия экипировки/восстановления/current. Следующий вопрос выбран из открытых
T012–T013/#1572: последствия поражения, капитуляция и переход добычи; решения
ещё не принимаются. Проверка ссылок/diff/consistency → WIP/push → независимое
Sol6.1/xhigh review → исправления при наличии → remoteSHA/blob/issue readback.
Код/tests/build/пробы не входят, production-gates не закрывать.

WIP8bbf0b25d4b6838aade09ec69045d50323d67e57 опубликован, remoteSHA/readback
подтверждены. Проверены127файловых ссылок (без anchors), неизменность двух
блоков формул и consistency/diff. Отдельный GPT-6.1 Sol/xhigh
review_golden_mask_acceptance проверил весь diff девяти файлов без замечаний;
полный баланс повторно не исследовался. Финальная публикация сверяет9blob/HEAD
и #1573/#1572 с прежней историей. Профиль принят; новая тема ещё обсуждается.

## Текущий срез T103–T104: усиление Золотой Маски

2026-10-30 baseline c85fbd08a5aab352bc4f0be355f3042df7d13611 совпал с origin;
#1573/#1572 прочитаны, PathsOnly1573 выполнен, workflow1.7 неизменён.
Продолжается brainstorming/Spec Kit bridge в прежней feature. T103 фиксирует
прямое одобрение +5/+5,+3/+3,head; parent проектирует восстановленный профиль,
Sol6.1/high audit_golden_mask_contracts отдельно сверяет ресурс/историю/примеры.
Продукт — [профиль](golden-mask-bonuses-proposal.md): +12/+12,+8/+8 и20% CoreMaxDE.
Порядок: source-аудит и варианты → явная новая добавка/замена/примеры →
consistency spec/plan/tasks/532 и арифметика/ссылки/diff → WIP/push → отдельный
GPT-6 Astra/high review взаимодействующих resource/equipment/story contracts →
поправки/проверка → публикация с remoteSHA/blob/issue readback → показать автору.
Никаких runtime/tests/build/новых игровых проб; процент не выдавать за принятое
ядро. Реализация требует согласованной точной спецификации и своих технических
фаз; T001/T077 и gates532 остаются открытыми.

WIP3e9905050e0f677fb33d3d9649d94ba666e47f25 опубликован до review, remote SHA
и файл с GitHub прочитаны/сверены. Parent проверил5ресурсных и4M-примера,
остатки смертных ресурсов,126относительных файловых ссылок (без anchors),
consistency/diff. Отдельный GPT-6 Astra/high review_golden_mask_bonuses проверил
весь diff девяти файлов и связанные источники без существенных замечаний.
Числа/новый maximum-компонент ещё на рассмотрении; баланс боя не доказан.
Финальная публикация сверяет9blob/HEAD и #1573/#1572 с сохранённой историей;
после неё предъявляется точная спецификация, реализация не начинается.

## Текущий срез T102: бонусы Расколотой Маски

2026-10-30 baseline eca81a823c36b2962abe5f56d0cc48c353914afc совпал с origin.
Продолжены brainstorming/Spec Kit bridge; PathsOnly1573 выполнился, #1573/#1572
прочитаны, workflow1.7 неизменён. Результат — [профиль](cracked-mask-bonuses-proposal.md),
не production. Parent проверяет духовные формулы и межсистемный договор;
Sol6.1/high audit_cracked_mask_mortal отдельно проверяет mortal-опоры.
Последовательность: source-аудит → четыре числа/границы/примеры → consistency
spec/plan/tasks/feature532 и арифметика/ссылки/diff → WIP commit/push → независимый
GPT-6 Astra/high review взаимодействующих realm/equipment/formula contracts →
исправления и адресная проверка → финальная публикация/remote SHA/blob и issue
readback → предъявить точную спецификацию автору. Новых tests/build/проб нет;
не переносить одобрение прежнего общего замысла на эти числа. Реализация после
согласования точной версии и отдельных технических фаз; T001/T077 не закрывать.

Итог исследования: WIP ddcda6a557fbc83ff96fc4ed58544eca479b721d опубликован,
remote SHA и файл с GitHub сверены. Sol6.1/high подтвердил mortal-примеры и
производный вклад интеллекта в ресурсы. Parent проверил6примеров M, проценты,
остатки округления ресурсов,108файловых ссылок (без anchors), согласованность
Spec Kit и diff. GPT-6 Astra/high review_cracked_mask_bonuses проверил весь
diff и рабочую поправку truncate вместо floor для ресурсов; существенных
замечаний нет. Числа остаются предложением. Финальный шаг — сверить7blob/HEAD
и обновить #1573/#1572 с сохранением истории, затем предъявить документ автору.

## Текущий срез T101: поправка стартового инвентаря

2026-10-30 baseline8df5be4550006d299bfa71a6a8be689ad8f7dd77 совпал с origin,
#1573/#1572 прочитаны, PathsOnly1573 выполнен. Это прямое уточнение принятого
документа без нового brainstorm/баланса/пробы. Удалить нить из таблицы, записать
принятие остальных9товаров/названия; разделить ассортимент и стартовую реликвию.
Parent сверил feature532spec1714+: непродаваемая Маска, интеллект/убеждение,
экипировка, память/восстановление, чисел нет. Духовные боевые бонусы добавлены
как требование без выдуманного профиля. Согласовать spec/tasks532 и1573,
приложение; ссылки/diff → WIP/push → отдельный Sol6.1/xhigh review → SHA/blob
и #1573/#1572 с сохранением истории. Никакой runtime/schema/GM/tests/build.
Поведение кэша/владения не меняется, измерений не требуется. T077/T001/532gates
открыты; принятые товарные эффекты не равны реализованной игре.

WIP1b26162f5546db32ba91d3bcf1b7cddfd367cf26 опубликован/remote SHA совпал,
приложение прочитано с GitHub. Независимый GPT-6.1 Sol/xhigh
review_starting_mask_correction1573 нашёл один P2: в текущих crossref532 оставался
непринятый статус T100. Исправлены spec/tasks532; адресное повторное ревью без
новых замечаний.103файловые ссылки (без anchors),9строк без нити и diff проверены.
Финальный шаг —7blob/HEAD и #1573/#1572 с прежним текстом/историей. T101 завершён
документально; чисел Маски/production/пробы нет.

## Текущий срез T099–T100: название и начальный ассортимент

2026-10-30 baselinec256491d6471eb0a6106bb5f352d8b6206df9950 совпал с origin;
#1573/#1572 прочитаны. Продолжены brainstorming/bridge/clarify, PathsOnly1573
выполнен; сохранены workflow1.7/optional hooks и существующая ветка документов.
T099 фиксирует принятые частоты/возможности и Main/noDE. Parent владеет названием
и десятью профилями; Sol6.1/high audit_starter_item_balance1573 отдельно сверяет
принятые числа, экономику и границы непринятого healing/suppression.

Результат T100 в существующем предметном приложении: «Вещи» как рекомендация,
конечные24/72HP,36барьер/урон, ситуативные+2/+1, текст/квест без скрытой магии,
действия/цены/источники/сроки/повторы. Ни стартовая выдача, ни закрытый каталог,
ни автоматический restock. Точные recipes/рост/stock и поздние усиленные вещи
не придумываются как готовая экономика. Сохраняются source/owner/realm/память,
общая публикация, камни/раны/цены развития; clearing-suppression не вводится.

Проверить HP121/332/1030, доли лечения, floor60% продаж, концентрационную цену
бальзама, базовый урон; consistency spec/plan/tasks/связь532, ссылки/diff.
WIP/push → независимый GPT-6 Astra/high review → исправления → remote SHA/blob
и #1573/#1572 с прежней историей. Без runtime/GM-пакета/build/tests/симуляции;
T077/T001 и532T005/T023 открыты. Баланс поздней игры/серийного лечения не доказан.
Existing service для immutable definitions/projections; fresh owner/count/target/
effect-time/publish. Простой расчёт цены не кэшировать; source revision/realm/role
и invalidation расхода/передачи сохраняются. Новая схема/тесты только в будущем плане.

WIP071c4e540afbf716e816692f20f4938eadb75537 опубликован до независимого review,
remote SHA совпал, каталог прочитан через GitHub. GPT-6 Astra/high
review_starter_catalog1573 проверил9документов и связанные HP/урон/барьеры/
лечение/экономику без материальных P1/P2.272относительных файловых ссылки
(без anchors), арифметика цен/долей/HP и diff проверены. Финальный шаг —9blob/
HEAD и #1573/#1572 с сохранением истории. T100 завершён как проект, новый
каталог/название ещё требуют авторского рассмотрения; игровой баланс не доказан.

## Текущий срез T096–T098: частоты и предметы приключений

2026-10-30 baseline9b0c0b4d743c2e9f6b7a9f34e8d1a4576a71f266 совпал с origin;
#1573/#1572 прочитаны. Продолжены brainstorming/bridge/clarify, PathsOnly1573
выполнен; optional commit-hook не заменяет scoped publication. Serena готова,
локальные csharp/typescript/python сохранены; ResolveDocumentsCore прочитан
семантически. T096 фиксирует только принятый цикл/время, новые частоты/контракт
custom ещё предложения. Редактируется документация, не runtime/GM-пакет.

Parent отвечает за общую таблицу/дизайн; Sol6.1/high audit_mortal_items1573 и
audit_spirit_items1573 раздельно исследуют смертные возможности и духовные границы.
Цель — §1.5 living-world и новое предметное приложение, ссылки #1572/feature532
без дублирования executable плана. Проверить суммы100%, conditional30%×60%=18%,
знаменатели, actual-time/repeat/canonical sources, item/owner/realm/effect/text.
Согласовать spec/plan/tasks/исследование, относительные ссылки/diff → WIP/push →
отдельный GPT-6 Astra/high review связанного блока → исправления → SHA/blob и
обновление #1573/#1572 с сохранением прежней истории. Код/tests/build/симуляция
не входят; T077/T001 и feature532 T005/T023 открыты.

Оптимизация: чтение определений/текста/стабильных проекций через существующий
service/repository/cache с полной source revision/item/owner/realm/role зависимостью,
инвалидация передачи/расхода/редактирования; fresh mutable state/публикация.
Проценты не требуют кэша, выбранное событие — канон. Точные новые схемы,
effect-бюджеты и поведенческая приёмка — будущий executable plan после рассмотрения.

WIPc7762c0d735d5f9d5bff22287bf2f66e46922120 опубликован до независимого ревью,
remote SHA совпал, новое приложение прочитано через GitHub. GPT-6 Astra/high
review_encounters_custom1573 проверил девять документов и ключевые item/text/
actor/root/consumption исходники без материальных P1/P2.264относительные
файловые ссылки (не anchors), суммы100/100/100, пример18% и diff проверены.
Финальная публикация сверяет SHA/blob и #1573/#1572; T097/T098 завершены как
исследование/предложение, не согласование таблицы/custom-договора или runtime.

## Текущий срез T094–T095: исследование Моря Хаоса

2026-10-30 baseline814dcdc5cf9c5844e29f05e9b30ed1855e242784 совпал с origin.
Продолжены brainstorming/Spec Kit bridge/clarify; PathsOnly1573 выполнен,
workflow1.7 и optional clarify hooks сохранены. T094 записывает принятие
поискового Благоволения. Parent разрабатывает игровой цикл; Sol6.1/high
audit_chaos_encounters1573 проверяет существующие actor/resident/conflict
контракты без редактирования. Serena готова; ShowSearchAbodePrompt прочитан
семантически, navigation/local-scope проверены по исходникам.

Результат — §1.4 [живого мира](living-world-events-proposal.md): места/маршруты,
этап4ч/дальний8ч и фактические интервалы, реальные встречи/опасности, добровольное
приглашение и приём, отдельное знание. Сохраняются камни/снабжение, один clock,
свободный доступ после вознесения, личности/покровительство и общая публикация.
Явно предложена замена старого raw-d20 поиска/мгновенного прибытия и расширение
abode-only scope/transfer-only приёма; конкретные новые поля/миграция ещё впереди.

Приёмка: игровой выбор до/во время встречи, отсутствие обязательного боя,
содержательные угрозы/разница районов, реальное время без повторного списания,
неизвестный мир не становится всеведущим UI. Проверить согласованность, пример
90+20+130+60=300мин, ссылки/diff; WIP/push → отдельное Astra/high review →
исправления → финальные SHA/blob и #1573 с сохранённой историей. Без кода,
GM-пакета/tests/build/симуляции; T077/T001 открыты. Не материализовать бесконечный
мир, не опрашивать ГМ поминутно; существующий сервис снимков/стабильных правил,
свежие изменяемые источники/владельцы/положение/время/receipt. Новый store не нужен.

WIP0ba7c146a003353863b9f21d3e8b886ac0fb1207 опубликован до ревью; удалённый SHA
совпал, §1.4 прочитан через GitHub. Независимый GPT-6 Astra/high
review_chaos_exploration1573 проверил точный diff семи документов и смежный код
без материальных P1/P2. Исправлена только терминология статуса принятого Favor.
218 относительных файловых ссылок (без проверки anchors), арифметика300мин и
diff проверены. Финальный шаг — SHA/blob и #1573; T095 ожидает авторского
согласования, частота встреч/полный баланс не доказаны, production не начат.

## Текущий срез T092–T093: Благоволение в поиске

2026-10-30 baseline d9bb88034ed0dc397a541b6f49f81354946d2fd4 совпал с origin.
Продолжены brainstorming/Spec Kit bridge/clarify, PathsOnly1573 и workflow1.7
проверены; прежние clarify hooks optional. T092 записывает принятое снабжение.
Parent владеет поисковым профилем; Sol6.1/high consult_favor_stone_search1573
сравнивает два способа влияния на качество без редактирования/повторного аудита.

Результат — T093 в существующем favor-приложении: одна проверка/принятый crit,
предварительная область/ветки и настоящие находки, сохранение полного успеха,
пример двух обычных и великого, ограничения отсутствующего предмета/доступа/
владения/повторов/группы. Альтернативное создание ещё не канонизированной находки
и +1/+2 реликтовой редкости остаются открытыми. Частоты производства не меняются.

Проверить статусы spec/plan/tasks/приложений, натуральные грани, ссылки/diff;
WIP/push → отдельное Astra/high ревью цельного блока → исправления → remote
SHA/blob и обновление #1573 с историей. Без кода/GM-пакета/tests/build/новой
симуляции, T077/T001 открыты. Каноническое открытие не временный кэш удачи;
существующий сервис для стабильных определений/снимка, свежие source/owner/
доступ/receipt. Не генерировать весь мир заранее и не вводить новый store.

WIPe75f07bcf12aea29131c0bb1c95d7084a553a4a9 опубликован до ревью; remote SHA
совпал, приложение прочитано с GitHub. Независимый GPT-6 Astra/high review
девяти документов без существенных замечаний. Проверены грани5/10/15/20/25%,
213относительных файловых ссылок и diff. Финальный шаг — SHA/blob и #1573;
поисковый профиль ещё не утверждён, кампания/production не проверены.

## Текущий срез T090–T091: источники и снабжение

2026-10-30 baseline78a886c95569f697703015d10148515631a9529f совпал с origin.
Продолжены brainstorming/Spec Kit bridge/clarify; PathsOnly1573, workflow1.7
и прежние optional clarify hooks сохранены. T090 фиксирует отдых и таблицу
разновидностей; частоты ещё открыты. Parent владеет предложением источников/
труда/торговли; Sol6.1/high audit_stone_supply1573 отдельно проверяет Favor и
текущие restock-контракты, без редактирования тех же файлов.

Результат — §1.3 прежнего living-world приложения: четыре fixed-grade источника,
периоды/caps, доли минеральных мест, конечные находки и монстры, целевые запасы
и реальные поставки торговцев. Приёмка требует различать образование/изъятие/
доставку, поддержание общин/экспорт, частоту источников/вероятность находки;
сохранять10/6 обычного и всю принятую таблицу. Не утверждать open quality-roll.

Проверки: согласованность spec/plan/tasks/приложений, арифметика общего бюджета,
ссылки/diff → WIP/push → отдельное Astra/high review → исправления → SHA/blob
и обновление #1573 с историей. Код/GM-пакет/tests/build/симуляция не входят;
T077/T001 открыты. Оптимизация — принятые интервалы и причинные границы вместо
поминутного GM; существующий сервис стабильных правил, свежие stock/time/owner/
receipt. Новый cache арифметики или параллельный инвентарь не нужен.

WIPd13ff504d2dc7f525ab499f65ed454c059043600 опубликован и сверен с origin;
приложение прочитано с GitHub. Независимый GPT-6 Astra/high review нашёл два P2:
пограничное смешение изъятия/доставки на10сутках и старый статус T006. График
с начальным запасом/обратным путём и T006 исправлены; адресное повторное ревью
без замечаний. Арифметика/211файловых ссылок/diff проходят. Финальный шаг —
SHA/blob и #1573; новые частоты и production остаются открыты.

## Текущий срез T088–T089: отдых и разновидности

2026-10-30 baseline61bf0c02ab7cf4612eec061f10d67e228266dc60 совпал с origin.
Продолжены brainstorming/Spec Kit bridge/clarify; PathsOnly подтвердил1573,
workflow1.7 прежний, hooks clarify только optional commit. Приняты ровно
40ДЭ/Основное/1минута обычного. Parent предлагает отдых и сводит договор;
Sol6.1/high audit_rest_stone_tiers1573 отдельно проверяет экономику/жилы/цены.

Результат в существующем приложении: абсолютная скорость P+H/20/минуту против
процентной/фиксированного времени; реальные S/H-примеры; единый accepted-rest
интервал, остатки/прерывание/границы состояния; четыре ёмкости20/40/160/640,
цены/доступность/ремонт и границы полезности. Все новые числа — кандидат.
Production, GM-пакет, тесты/сборки/симуляция не входят. T077/T001 открыты.

Проверить требования/статусы между spec/plan/tasks и приложениями, арифметику,
ссылки/diff; опубликовать WIP и отдать цельный блок независимому Astra/high.
После исправлений сверить remote SHA/blob и обновить #1573 без потери истории.
Не запускать поминутные запросы ГМ: интегрировать принятый интервал до ближайшей
причинной границы. Стабильные определения/снимки — через существующий сервис;
реальные время/остаток/предметы/максимум/допуск/публикация — свежие. Нового
таймера/инвентаря/кэша простой арифметики не требуется.

WIP9c28dd401326111450280219917d0919bcdee92a опубликован и сверен с origin.
Независимый GPT-6 Astra/high review нового блока без существенных замечаний;
старая противоречащая фраза о запрете отдыха ДЭ исправлена и проверена адресно.
Арифметика,201 файловая ссылка/diff проходят. Финальный шаг — SHA/blob и #1573;
согласование нового профиля и production остаются открыты.

## Текущий срез T086–T087: ёмкость и применение камня

2026-10-30 baseline04283bae6996b7df6ff3d8e86bdbd1e1a241d8d4 сверен с origin.
Продолжены brainstorming/Spec Kit bridge/clarify в feature1573; автор принял
возобновляемые жилы как основной источник и котировку10/6. Производительность,
запас жилы и ёмкость ДЭ этим не утверждены. Parent ведёт предложение;
Sol6.1/high audit_stone_energy_use1573 отдельно проверил предметный расход,
владельца ДЭ, стоимость действия и существующие правила восстановления.

Результат — профиль обычного камня в прежнем приложении восстановления:
40ДЭ фиксированно; Основное действие в бою,1минута вне боя; цена активации0ДЭ,
предел фактического запаса, отказ при полном ресурсе, целый расход с потерей
излишка, последовательное пакетное применение. Нужны сравнение с16P и указание
нерешённого самоисцеления. Простая арифметика не заменяет новый боевой прогон.
Runtime/GM-пакет, сборки, тесты и симуляция в этот срез не входят. T077/T001
остаются открыты; перед реализацией требуется согласование самого профиля.

Проверка: исходные требования → согласованность spec/plan/tasks и приложений →
арифметика и файловые ссылки → публикация WIP → независимый GPT-6 Astra/high
review цельного блока → исправления → финальные SHA/blob и обновление #1573
с сохранением истории. Предметный Gain требует договора #1572: существующий
local_item_outcome привязан к Item-owner, а mortal planner — к mortal_world.
Оптимизация: переиспользовать существующие сервисы для стабильных правил и
снимков операции; запас, максимум, время, owner и право принятия свежие.
Тривиальное сложение не кэшировать, новый инвентарь/публикатор не создавать.

WIP0ae8942c82aaa607e168889f8bbe31cf7c7d6e2b опубликован, remote SHA совпал,
приложение прочитано с GitHub. Независимый GPT-6 Astra/high review нашёл P2:
формула числа камней не учитывала постоянный burden. Уточнение и остановка
пакетного применения проверены адресно без новых замечаний. Арифметика,
195 относительных файловых ссылок и diff check проходят; финальная публикация
сверяет SHA/blob и сохраняет историю существующего #1573. Профиль не принят.

## Текущий срез T084–T085: добыча и цена камней

2026-10-30, baseline 6c22589af7703242b54a2072ae82291715fbd1c0 сверен с origin.
Продолжены brainstorming/bridge/clarify; PathsOnly подтверждает1573, hooks и
workflow1.7 не менялись. Автор принял2%M/сутки и10D/камень. Parent сверяет
принятые Feather-ориентиры и создаёт модель; bounded Sol6.1/high исследует
торговлю/запасы/Favor/хозяйственные активности. Без runtime, тестов или симуляции.

Результат — раздел1.2 прежнего приложения: реальные источники против переноса,
котировка10/6, действующая жила2/сутки с запасом20, рабочий пример4/вылазку,
запас/труд/перебои и совместное содержание. Арифметика доказывает только
выполнимость примера при явных условиях, не баланс произвольной кампании.
Часовой отдых, ёмкость ДЭ и полная реализация не утверждены, T077/T001 открыты.
Следующий этап — предъявление и согласование, не запуск production.

WIP опубликован; независимый GPT-6 Astra/high review цельного блока,
168 ссылок/diff и арифметика прошли без замечаний. Финал — remote SHA/blob
и обновление существующего #1573 без потери истории. Будущий
runtime потребует согласования предметного owner с #1572, кошелька торговца,
общего accepted-time production и GM-примеров. В этом срезе GM-пакет не меняется.
Оптимизация: существующий сервис для стабильных правил/снимков операции, свежие
время/запас/кошелёк/ownership/publication; повтор чтения не производит камни.
События роста и расхода обрабатываются по причинным границам, без поминутного
GM-запроса и без кэша простой арифметики.

## Текущий срез T082–T083: износ и камни убежищ

2026-10-30, baseline 7281cbcca8480be1f53581c61478d2783773dd68 сверен с origin.
Продолжены brainstorming/bridge/clarify в feature1573. Принят только коэффициент
365:1; часовой отдых HP/ДЭ/новый баланс убежищ не утверждены. Parent владеет
численным предложением; ограниченный Sol6.1/high аудит проверяет отсутствие
готовых shelter-правил и границы Abode Power/инвентаря/общих ресурсов.

Результат — раздел1.1 существующего приложения: фиксированный M, износ от M,
один курс камень→ремонт, плановое поддержание без двойного платежа, запасы/масштаб,
остатки/границы распада, самостоятельные закупки и открытая экономика добычи.
Примеры проверяются арифметикой, не игровой симуляцией. Самостоятельная жизнь
сохраняется; не выдавать расчёт запасов за доказанный баланс доступности камней.
Нет runtime/GM-контрактов/проб; перед реализацией потребуются владельцы состояния,
согласование #1572, GM-примеры, категории тестов. T077/T001 открыты.

WIP опубликован; независимый GPT-6 Astra/high review, арифметика и163ссылки/diff
прошли без замечаний. Финал — remote SHA/blob и статус существующего #1573.
Оптимизация: применять изменения по причинным
границам, стабильные правила/снимки переиспользовать существующим сервисом;
фактическое D/предметы/время/владение/право принятия проверять свежими. Кэшировать
тривиальную формулу или создавать новый framework не нужно.

## Текущий срез T080–T081: масштаб духовного времени

2026-10-30, baseline d352a3867bcc8e66fe9d18ed47d042153fab9db3 сверен с origin.
Продолжены brainstorming/Spec Kit bridge и clarify в существующей feature;
PathsOnly подтвердил1573, before_clarify имеет только необязательный commit.
Посторонний obj не включать. Механизм событий/сведений одобрен; длительности/
коэффициент не выбраны. Parent проверяет afterlife scheduler и готовит кандидата;
отдельный Sol6.1/high проверяет смертные часы/смену воплощения/скачки времени.

Результат — раздел2.1 существующего приложения, без ещё одного design-файла:
общие часы, примеры сцен, варианты сжатия, рекомендация1:365, дроби/однократность,
календарь/исходная точка, связь с отдыхом/проектами. Согласованность: принят только
механизм, все новые цифры помечены предложениями; T077/T001 открыты. Не запускать
runtime, игровую/численную пробу и не менять GM-пакет. При будущей реализации
потребуется единый договор progression и его live/frozen путей, UI/GM-примеры.

Проверка: арифметика примеров, scoped diff/155ссылок и независимый GPT-6 Astra/high
review границ часов/остатков/принятых решений пройдены без замечаний. WIP
опубликован до review; финал — remote
SHA/содержимое и текущий статус #1573. Тривиальной формуле отдельный кэш не нужен;
стабильные правила/снимки одной операции через существующий сервис, актуальные
duration identity/владение/право публикации проверяются свежими. Накопление только
принятых интервалов, не чтением файла при каждом UI/validation-проходе.

## Текущий срез T078–T079: события, сведения и постоянный доступ

2026-10-30, baseline db64e5a84a625f2f8929a81aeabaf9baeecc67bb сверен с origin.
Продолжение brainstorming/bridge и уточнения существующего spec, без новой
ветки/feature. Автор принял самостоятельность мира и постоянный доступ после
вознесения, уточнил бой/отступление и отменил «Новый цикл» целиком. Ответы
занесены в Clarifications; T077 остаётся согласованием полного кандидата.

Parent читает progression/catchup и проверяет ключевые переходы; отдельные
Sol6.1/high read-only аудиты — осведомлённость/уведомления и доступ/New Cycle.
Результат: living-world-events-proposal.md с причиной/намерением/исходом,
игровыми часами, разделением факта и известия, долгим отсутствием, переходами
и зависимостями удаления. Никакого запуска/изменения старой симуляции или игры.
Отдельный complex GPT-6 Astra/high review проверил причинность, публикацию, знание,
часы двух пространств и сохранение отдельного mortal lifecycle. Единственный P2
старого AC17/статуса доступа исправлен и повторно проверен. Полный diff check и
159 относительных ссылок проходят; публикация включает сверку remote SHA/blob.
Одобрение принципов не закрывает T077.

Согласованность: T078 отражает ровно решения автора, T079 предлагает ещё не
выбранный механизм; цены/частоты не назначены. Runtime и GM-пакет не меняются.
При будущей реализации потребуется синхронное обновление клиентских маршрутов,
preview/допуска, GM-подсказок/примеров и tests по согласованным tasks. Производство
состояния отделено от чтения новости; повтор не исполняет событие заново.
Оптимизация: готовые определения/стабильные проекции через существующий сервис;
актуальные ресурсы/время/право публикации свежие. Агрегация спокойного интервала
только до причинных границ; новый кэш и повторное чтение всей истории не нужны.

## Текущий срез T075–T077: уточнение ДЭ и активностей, 2026-10-30

Продолжение brainstorming/Spec Kit bridge, уточнение существующего spec1573;
новая feature/ветка и реализация не создаются. Baseline df38466287dfe28ae8d41b1e7567957da3b8f1f7
сверен с origin. `check-prerequisites.ps1 -Json -PathsOnly` с явным
SPECIFY_FEATURE_DIRECTORY подтвердил существующие spec/plan/tasks. Ответ автора
об отдыхе/камнях фиксируется в Clarifications и заменяет текущие refill-формулировки;
история и исходники прежних измерений сохраняются как свидетельства того варианта.

T075 — записать авторские требования/границы/открытые решения. T076 — read-only
аудит: parent — ресурс/предметы/#1572, Sol6.1/high — население/убежища и отдельно
Сияющая Обитель. Свидетельства в research, краткий independent Sol6.1/xhigh review
документального блока, ссылки/diff/remote SHA. Это не исполняемый план расширения
всего мира. На T077 предъявить написанную основу автору и продолжить обсуждение.
Production/новые пробы начинаются только после согласованного spec и отдельных
фаз плана/задач/consistency; T001 остаётся открытым.

Оптимизация сейчас: только адресное чтение; нет нужды в сборке, мире-фикстуре,
симуляции или новом кэше. В будущей системе стабильные описания/проекции должны
переиспользоваться через существующий операционный сервис; фактическое владение,
остаток предметов, прочность, время и право изменения проверяются свежими.
Модель всего бесконечного Моря и периодический опрос файлов не предполагаются.

Итог T075/T076: требования/ответ автора и source-опоры записаны. Независимый
Sol6.1/xhigh обнаружил устаревший combat-end Gain в §6 recovery; исправление
проверено отдельно, существенных замечаний не осталось.127links/diff PASS,
WIP опубликован и сверён с origin; окончательный spec остаётся на обсуждении T077.

## Исполнение T071–T074: ДЭ и подавление лечения

Автор одобрил написанный профиль и запуск. Используется существующая ветка
story/golden-mask-backstory-20261025/изолированная копия проекта; unrelated obj
не трогать. Применён executing-plans с canonical Spec Kit checkpoint/review.md,
без дублирующего ledger. Согласованный численный spike не production feature.

T072 отдельный Sol6.1/high меняет только md в feature кроме spec/plan/tasks/review/
healing-suppression-proposal; parent владеет этими файлами и probe. T073 parent
сравнивает мгновенный combat-end, автоматическое тиканье и явный отдых; смена
названия не считается причиной молча отменить прежнее решение.

T071: переиспользовать profile/damage/rng из healing-counters.mjs через импорты
без исполнения старой матрицы. Отдельный suppression-probe.mjs моделирует этот
согласованный компонент; helper guards с node:test сначала RED→GREEN на часах/
refresh/очищении/единственном округлении. Baseline старого probe сохраняется;
проверить его10000-траекторные значения и контрольные дуэли, не повторять422
старых ячейки без новой причины. Никаких C# сборок или полноценных игровых тестов.

Профили D0/50/130/210/1000,10000 траекторий на ячейку, две очередности, horizon2000
с явным censoring. Контроль: ordinary28/8P и placebo24/12P без эффекта; лечениеV.
Атаки suppression24/12P всегда либо только когда метка отсутствует (иначеordinary).
Защитник: heal-only; очищать каждую полную метку; очищаться только когда метка
ещё переживёт текущую возможность и HP выше25%; при подавлении recover до истечения;
mixed50 (heal≤50%, иначеordinary). Все политики используют текущее состояние,
не будущие броски. Подавление частичного/полного результата связано с тем же
исходом проверки урона; лечение не получает второй бросок. При нехватке ресурса
выбранного действия — recover16P+passiveR; это явное ограничение политик, не оптимум.
ВысокаяH=120P у целителя отдельно от исходнойS, не равные бюджеты сборок; H больше
этого порога также не делает more-than-one Main. Проверить обе очередности.

Союзное очищение: ограниченная модель1v2 с целью/помощником, помощник очищает
при метке либо recover; все6 порядков, собственные ДЭ/регенерация/цена помощника.
Атакующий намеренно фокусирует пациента; помощник неуязвим в этом опыте, значит
это верхняя оценка надёжности поддержки, не полный групповой баланс.

Точные boundary cases: expiry после событий конца именно следующих target
opportunities; controlled skip расходует срок, дополнительныйMain —нет;
same-source weak не продлеваетstrong; multisource strongest, очистить всюсемью;
hit при полном поглощенииHP-урона всё ещё накладывает; crit не усиливаетrider;
source death не удаляет, terminaltarget/conflict удаляет; round226.512→113/170;
оплата до regen, отдельные ресурсы помощника. Definitions/outcome таблицы создаются
раз на профиль; mutable HP/ДЭ/effects независимы, I/O в цикле отсутствует.
Raw JSON только D: вне Git, без квот диагностики; агрегированные результаты и
исходник в Git. Удалять прежний сырой замер только после полного нового и cleanup.
Bridge consistency до исполнения: T070 закрыт точным решением автора; scope/spec,
T071+T072+T073→T074, T001 production не разрешён. Independent Astra/high на весь
coherent block, исправления и повтор только затронутого.

Исполнение: 16/16 guard tests PASS, прежние 15×10000 baseline сохранены.
Дуэли 280×10000, группы D130/1000 и все шесть порядков 48×10000 выполнены;
результаты/ограничения в разделе 7 healing-suppression-proposal.md. T074 —
независимый review точного опубликованного diff и подтверждение публикации.

## Текущий срез T068–T071: профиль подавления лечения

Продолжаются brainstorming/Spec Kit bridge; written profile до новой пробы.
Source audit общего effect/attack/clock/removal договора, затем один явно
предлагаемый профиль и scoped очищение в healing-suppression-proposal.md.
Независимый complex Astra/high review договоров/краевых случаев; предъявление
актуального файла автору. В этом блоке только документы и простые примеры,
без новой симуляции, правок probes/игры/GM-контрактов1535/1536.
После утверждения конкретного профиля отдельно исполнить T071: обновить
существующую численную пробу, обе очередности, обычная/высокаяH, heal-only,
смешанная тактика, самолечение/очищение/союзная помощь, точные expiry/refresh/
barrier/rounding границы и результаты обеих сторон. Проверить цену давления,
перманентное удержание и выбор очищения; не объявлять заданную политику оптимумом.
Если эффект недостаточен, сначала показать измерения и предложить изменение
параметров, не выдавать подогнанные числа за уже согласованные. Перед T071
конкретизировать seed/сценарии/горизонт в плане и пройти consistency повторно.
Оптимизация: общие стабильные определения через существующий сервис1535/кэш;
факт текущего подавления, срок, цель/источник и authority проверяются свежими.
Простой процент не кэшировать; множитель рассчитывать один раз на healing event.
Bridge consistency: требования spec, дизайн T068→review T069→approval T070→
probe T071; T001 остаётся закрытым для production.

Результат T068/T069: healing-suppression-proposal.md v1, отдельный Astra/high
review без существенных замечаний. Уточнена половина healing strength до cap
дефицитаHP, арифметические примеры подтверждены.62links/diff PASS, WIP опубликован;
профиль предъявляется на T070. Проба T071 и production не выполнялись.

## Текущий срез T065–T067: проверка противодействия

Прямое разрешение автора на probe продолжает показанную spec v1.48. Сначала
source audit цен/действий/прерывания; затем source-only исследовательский скрипт
`probes/healing-counters.mjs`, без импорта игры и без нового production-кэша.
Точные BigInt-таблицы400 исходов создаются один раз для неизменного профиля/B;
независимое HP/ДЭ каждой траектории, сотые ДЭ, одно финальное округление урона.
Профили D0/50/130/210/1000, по10000 траекторий на ячейку, обе очередности.
Сначала воспроизвести прежние heal-only числа; затем B28/8P,36/16P,44/24P,
чередование ordinary/large и выбор large только при возможности добить полным
успешным ударом (без знания кубиков). Смешанные тактики лечатся приHP≤25/50/75%,
иначе атакуют; проверять обе стороны, исход/длительность и действия/затраты.
Дефицит ДЭ означает Main-recover16P, passiveR только после собственного хода.
Условные прерывания — отдельная чувствительность цены4/8/16P и вероятности
25/50/75/100%. Основной вариант: чередование ordinary-атаки и Main-подготовки
одного ответа до следующего собственного хода; неиспользованный ответ сгорает,
подготовка не наносит урона. Отдельный оптимистичный вариант — немедленная
платная реакция сверх Main, максимум одна за раунд: наличие такого слота не принято.
Попытка лечения оплачена до реакции, успех отменяет только эффект; recover и
passiveR не подавляются. Это предположение, не полный reaction-контракт.
Сравнить обе очередности. Baseline считать до выбывания; новые сценарии с
заранее объявленным горизонтом2000 раундов, незавершённые случаи публиковать
отдельно и не считать победой/точным средним. Это горизонт опыта, не лимит
диагностических записей. Сырые результаты только на D: вне Git; в Git источник
пробы и компактные выводы. Конкретные доступ/история/публикация не моделируются.
Независимый Astra/high проверяет связанный блок и ключевые результаты.
Bridge consistency: scope/приёмка в spec, T065→T066→T067 ниже; T001 не снят.

Результат: раздел6 combat-healing-proposal.md и источник пробы;150000 baseline,
4220000 новых траекторий,100000 обычных дуэлей. Все незавершённые учтены отдельно.
Сильные атаки недостаточны, эффект прерывания зависит от ещё открытого договора
слота/цены, при высокойH остаются длинные бои. Independent Astra/high повторил
результаты и проверил отдельной рациональной/DP моделью; существенных замечаний
нет, pooled win rates уточнены. Публикация/issue по Git-истории, runtime не менялся.

## Текущий срез: боевое духовное исцеление

T062→T063→T064, baseline0ae7f46ab90474ae6397776aadad4627d40e0f23. Brainstorming/
Spec Kit bridge продолжаются. Автор принял outside40/55/70/85/100% и тариф20;
combat HP healing/цены остаются новыми. Использовать существующие damage/HP/OD
источники, bounded Sol6.1/high консультацию альтернатив; не подменять её review.
Сравнить надёжное source-scaled лечение, вариант с проверкой и распределённый
эффект; выбрать одну базовую процедуру и не превращать её в закрытый каталог.
Перед пробой записать сохраняемые договоры/цели/риски в spec и выбранные параметры
в приложении. Дешёвая арифметика: все прежние107 auto-профилей, степени исцеления,
ДЭ/пассивная/активная регенерация, damage expectation по400 кубикам, границы
поддержания серии и разный source/targetD; отдельные сценарии выбора heal/attack
если нужны. Проверить cap, flat/percent, roundHalfUp и отсутствие двойного mastery.
Не обещать завершение боя5–8 при лечении/любой экипировке или ограниченную серию
при неограниченном rawH. Независимый Astra/high review взаимодействующих combat/
healing/ДЭ договоров; публикация с результатами и открытым. Никакого production,
нового runtime-теста или вмешательства в1536. Простая формула не требует кэша;
stable definitions — existing service, current HP/ДЭ/max/цель/authority свежие.

Результат T062–T064: формула/уровни в combat-healing-proposal.md; parent и
независимый GPT-6 Astra/high подтвердили535 сочетаний,42800 исходов атак,
252000 граничных переходов,3210 случаев ДЭ и150000 стресс-траекторий. Долгое
самоисцеление явно раскрыто, контр-искусства не объявлены сбалансированными.
Существенных замечаний нет; номер строки source-ссылки исправлен.74links/diff
PASS, WIP опубликован/remote SHA совпал; итоговая публикация и issue по Git-истории.
Предложение предъявляется автору; production T001 сохраняется.

## Текущий срез: ускоренное восстановление HP и услуги

T059→T060→T061, baseline0090ea3552b3f5871a0f00990ee40f86a04abcf8. Продолжаются
brainstorming/Spec Kit bridge. Автор принял25%MaxHP за период отдыха и одну команду
до полного; новые искусства/цены пока открыты. Sol6.1/high read-only аудит1536 и
экономических источников, parent проверяет ключевой договор. Затем сравнить
фиксированный HP/зависимость от силы целителя/ускорение доли естественного
восстановления; конкретизировать один outside-сеанс, tiers, цену и интеграцию ран.
Проверить до предъявления целочисленную модель новых процентов, сохранение прежних
четвертей, cap/carry, неизменность total за повторные начисления, частичные тарифы,
высокий max и примеры кошелька. Это арифметический probe, не runtime-исполнение.
Новый общий остаток сотых HP предлагается явно только для отдыха/нового outside-
лечения; перенос прежнего четвертного остатка точный. Общие damage/battle правила
округления не меняются. Затем независимый Astra/high review взаимодействующих
time/carry/billing/wound контрактов, исправления/публикация. HP0 и прочие ещё
не утверждённые детали не выдавать за согласованные. Код1536 не менять.
Bridge consistency до probe: scope/приёмка в spec, задачи выше; простой арифметике
новый кэш не нужен, определения через existing service, current/owner/кошелёк/
provider/clock/authority проверяются заново. Полная реализация T001 закрыта.

Результат T059–T061: раздел8 приложенияv4 описывает40/55/70/85/100% за один
внебоевой сеанс и базовый тариф20 за полную полезную шкалу с частичной ценой.
Самолечение/помощь/новая услуга Элиары/сотый carry/границы ран предложены явно.
Parent и независимый GPT-6 Astra/high подтвердили91830 переходов/18366 курсов/
10770 сравнений старого и нового остатка. Source договор1536 проверен, замечаний
нет.67links/diff PASS, WIP remoteSHA/6blobs совпали. Финальная публикация и issue
следуют по Git-истории; новый пакет предъявляется автору, runtime не начинался.

## Текущий срез: принят ДЭ, явный отдых для HP

T058, baseline6045b5f82ec220fbb2109e39152c5f15c04206aa. Продолжается brainstorming/
Spec Kit bridge в существующей feature. Зафиксировать явное принятие combat-end
заполнения ДЭ и предъявить отдельный HP-кандидат: отдых запрашивается явно, не
выводится из каждого диалога. Сравнить полный reset/обязательное активное лечение/
доступный всем отдых; сохранить уже проверенный четвертной расчёт, раны1536 и
границы HP0. Новых численных формул нет: результаты HP-проверки T055 остаются
применимы, повторный probe не нужен. Scope/приёмка записаны в spec; проверить
согласованность текущих статусов, независимый review и публикацию. Не проектировать
заодно исчерпывающий healing catalog, не менять код1536 или production/T001.
Кэш не нужен для простой HP-арифметики; условия/текущее состояние проверяются
при подтверждённом отдыхе, stable definitions используют существующий сервис.

Результат T058: принятие OD-core записано, HP-кандидатv3 конкретизирует явный
отдых, сравнение трёх подходов и открытое активное лечение. Независимый GPT-6.1
Sol/xhigh проверил6045b5f8..a3a3ed91 без замечаний, parent diff/69links PASS.
WIP remoteSHA совпал; финальная публикация/issue по Git-истории. Новые runtime/
численные probes не исполнялись, HP-кандидат предъявляется автору отдельно.

## Текущий срез: ДЭ автоматически после боя

T057, baselinef401f6fd7caca3596ff2b59042ae8bec708ac090. Продолжение brainstorming/
Spec Kit bridge: автор возражает против оценки спокойной деятельности ГМ для ДЭ
и предлагает обычное мгновенное восстановление после боя. Уточнить spec и текущий
кандидат, отделить OD combat-end event от safe-rest HP/ран, проверить связность
статусов/границ и передать весь малый документационный блок независимому reviewer.
Новых численных probes, production, build/game нет; T001 не снимается. Имеющийся
source audit достаточен: завершение конфликта сейчас само ДЭ не заполняет, значит
автоматическое заполнение описывается как новая функция клиента. Простой assignment
не требует кэша; текущее terminal-состояние/owner/max/повтор события проверяются заново.
Приёмка: без оценки диалогов и ожидания; боевые формулы сохранены; refill строго
по завершению боя, HP/раны не исцеляются; условия/открытое не выданы за согласие.

Результат T057: кандидат v2 отделяет combat-end ДЭ от отдыха HP/ран. Независимый
GPT-6.1 Sol/xhigh проверил f401f6fd7..a6a899088 без замечаний; parent diff и65 links
PASS, WIP remoteSHA/6blobs совпали. Только уточнение spec, без новых probes/runtime;
кандидат предъявляется автору, финальный checkpoint/issue следуют по Git-истории.

## Текущий срез: восстановление ДЭ/духовного HP вне боя

T054→T055→T056, baseline5d7d8b867d6fbcd7b6bb0063e92c972fe04837a6.
Продолжение brainstorming/Spec Kit bridge, дизайн без реализации.
1. Зафиксировать принятие формул/+3/+5/rawS/H, проверить existing source contracts
   для духовного времени/отдыха/ДЭ/лечения ран. Sol6.1/high read-only audit;
   parent проверяет основные ссылки/символы. Scope/spec/plan/tasks до численных probe.
2. Сравнить2–3 игровых подхода к отдыху, выбрать описанный кандидат с явным временем,
   условиями/прерыванием/HP0 и границами постоянных ран. Дешёвые примеры ранних,
   поздних,разныхS/H,частичногоотдыха; no refill/double tick через reentry/retry.
3. Сохранить кандидат/ограничения, независимый Astra/high review interacting
   time/resource/wound договоров, corrections/verification/remote checkpoint/issue,
   предъявить автору точную spec. Production gate T001 остаётся.

Bridge consistency до probe: только формулы/ограниченная арифметика, старые источники
ран сохраняются, новую шкалу времени не выдавать за runtime. Stable definitions
через existing operation service; current/owner/clock/authority/revision свежие,
для простых формул отдельный кэш не требуется.

Результат T054–T056: out-of-combat-recovery-proposal.md v1, specv1.44/modelv2.34.
Принятый OD-пакет отделён от нового предложения отдыха/HP0/current-max. Parent и
независимый GPT-6 Astra/high подтвердили10770 HP/5376 OD расчётов, пять профилей,
две специализации и отдельное естественное лечение ран. Уточнена безопасность
всего периода; P2 устаревших текущих статусов исправлен и закрыт targeted review.
Открытых замечаний нет. WIP0df7f46acc77b5367a3f09e8e96defb0a6fae0e2 сохранён:
remoteSHA/8 exact-ref blobs совпали,192 relative links/diff PASS. Финальный checkpoint
и issue header следуют по Git-истории. Новая spec предъявляется для согласования;
runtime/clock/realm и safe recovery1536 не объявлены реализованными, T001 сохранён.

## Текущий срез: формулы Средоточия и Гармонии

T051→T052→T053, baseline42bd2b82efcb760024fc96795b1e06d75fcaa984.
Brainstorming/Spec Kit bridge продолжаются в feature1573, только дизайн.
1. Зафиксировать принятие двух характеристик; сверить предыдущее развитие/ДЭ,
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

## Текущий срез: принятые воздействия, запас ДЭ и восстановление

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
12 ДЭ, Main/малый Fast, цены2/4/6,passive2/MainGain4 на рассмотрение. Parent и
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
согласование написанного пакета автором, затем стоимость/ДЭ/бюджеты искусств.
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
Следующий шаг — обсуждение личного HP/ДЭ и оставшейся цены ручных вложений;
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
личные HP/ДЭ/эффекты, очередь, реакции, адресные действия и индивидуальные исходы.
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
| ДЭ | AfterlifeConflictActionPointProjection; AcceptedMechanicsPlanner.SpiritualMechanics; AfterlifeActionCostRules | Личные владельцы, main/reaction modes, оплата exact action, постоянные ресурсы между encounters |
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

**1. Честная дуэль через production.** Typed actors/owner profiles, HP/личные ДЭ,
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
AcceptedPlan.Validate, Publication и фактические source reads. После HP/ДЭ/effect/
equipment mutation зависимая проекция недействительна. Fresh authority нужна,
повторный parse стабильного файла обычно нет. Не кэшировать тривиальную арифметику.
Raw диагностика без квот на D: вне Git; в Git только компактные выводы.

## Статус

Подготовлена архитектура и исходная карта. Реализация/баланс/runtime tests/builds/
clean-restore не выполнены. [Review](review.md) относится к проекту документов.
