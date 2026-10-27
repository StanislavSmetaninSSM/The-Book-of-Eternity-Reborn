# Проверка проекта #1573

2026-10-27. Область: только проект документов v1. Production-код не менялся.
Исследование по `7b73b8c538436083e05def58da4970171f4614b5`; полный source map в research.

## Evidence и границы

- Прочитаны state/validation/resource/effect/lifecycle/UI/GM consumers; две
  отдельные read-only консультации Sol6.1 High. Audit ресурсов дополнительно
  сверил принятые четыре искусства Маски и выявил неэквивалентность Оплота барьеру.
- Вызваны Spec Kit setup-plan и setup-tasks с feature override1573; документы
  дополнены по actual code, шаблон плана заменён. Git-ветка не переименована.
- Writing-plans использован на plan; spec-kit-analyze — cross-artifact consistency,
  результаты/независимое review ниже после проверки. Это не сообщение о runtime pass.
- Test/build/gameplay/balance/clean-restore UNRUN. Новых raw diagnostics нет.
  Производительность исполнения документами не измерена; будущие owner/key/
  invalidation/fresh checks и cheap reuse tests описаны явно.

## Открытые обязательные решения

D1–D6 остаются блокирующими для production: HP/мощность/tuning/критические исходы,
ноль/лечение/раны/отдых, очередь/реакции, перенос arts/exchange/strain.
Особенно: Оплот ≠обычный HP-барьер; Исток сохраняет исходы; отмена matchup veto
явно меняет принятый Приговор. Это deliberate discussion gates, не defaults.
T001 блокирует код; последующее согласование требует обновить analysis.

## Consistency / independent review

Parent consistency по spec/plan/tasks/gameplay/data-model/commands/arts выполнена:

| Проверка | Результат |
|---|---|
| Требования и покрытие | AC1–AC9 имеют tasks/evidence scenarios; ни одна runtime task не отмечена complete |
| Область/authority | Общий resource/effect/publisher переиспользуется; independent actor/roles обязательны |
| Сохранённые arts | Оплот отделён от барьера; Исток и принятые цены сохранены; veto/ступени/clocks явно D6 |
| Открытые решения | D1–D6/T001 блокируют реализацию, документы не названы execution-ready |
| Проверяемость | Typed cheap tests + actual lifecycle/publication; выбранные категории, no full suite |
| Производительность | Whole-operation DATA keys/invalidations/fresh checks и named measurement предусмотрены |
| Шаблоны/ссылки | Нет неубранного plan template; 10 Markdown документов, локальные ссылки разрешаются |

`check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` успешно указал feature1573
и plan-related docs/tasks. Первая попытка с неподдерживаемым `-RequireSpec` не
выполнилась; параметры сверены с actual script, повтор успешен. Это tooling check,
не автоматическое доказательство смысловой корректности. `git diff --check` успешен.
Смысловой анализ выполнен чтением и таблицей выше, а не лишь наличием файлов.

Независимое review: `/root/review_spiritual_combat1573_v1`, явно выбранные
`gpt-6-astra/high`, отдельный контекст. Exact block
`874714c558ac250fb2984c471be205ab5b731ca9..a8c24ebc8ea3aad2731a821bd0779ed0d6e18613`.
Проверены docs и связанные C# через Serena; никаких builds/tests.
P1 нет; три P2 перед утверждением/реализацией:

1. Duplicate participant IDs одного actor могли дать две очереди — добавлена
   инъективность owner binding и сохранение spent slots при rejoin/смене стороны.
2. AoE собирала ответы до оплаты без durable commitment — добавлены канонические
   обязательства slot/AP, available=current−unpaid commitments, atomic consume/
   release, reload и запрет оплачивать будущим Gain, без второго resource ledger.
3. Прикрытие цели AoE уже включённым защитником не имело исхода — кандидат D5
   явно запрещает collision, хранит original/effective, отличает defense/dice
   и условие Приговора. Это предложение правил, не их утверждение за автора.

Quickstart/tasks дополнены причинными случаями. Родитель сверил findings и
изменил docs. Тот же отдельный reviewer проверил scoped diff
`a8c24ebc8..8d299d1d7`: все три P2 закрыты на уровне проекта, новых замечаний
в затронутых зависимостях нет; commands/gameplay/quickstart/tasks согласованы.
Draft пригоден для обсуждения, не является доказательством runtime readiness.

Checkpoint a8c24ebc8 опубликован; remote SHA и GitHub file readback подтверждены.
Исправленная редакция `8d299d1d7f4ac3c65fce5c0f9380da15378686d3` опубликована;
remote SHA и GitHub readback data-model подтверждены. Issue#1573 обновлена ссылками
на exact revision spec/gameplay/research/plan/tasks/контрактов. Clean restore не выполнялся.
Оценка маршрутизации: две профильные консультации + одно архитектурное review
выявили неэквивалентный перенос искусства и три конкретных пробела; данных о
денежной/токенной экономии нет, ослаблять independent review оснований нет.

## 2026-10-27 — замечание автора о смертном бое

Автор отверг формальные позиции/зоны и потребовал изучить смертный бой; затем
поручил сравнить варианты и рекомендовать наиболее интересный игрокам подход.
Первый анализ действительно не охватывал весь mortal combat workflow.
Приёмка предыдущего draft не означает авторского утверждения или полноты такого сравнения.

Применены receiving-code-review и brainstorming в продолжающейся feature1573,
Spec Kit/bridge остаётся canonical tracking. Parent исследовал identity/resource/
validation/UI/request paths через Serena, проверил ключевые правила; отдельный
Sol6.1 High прочитал Blocks6/12/13/14/15/15.A и примеры, затем оценил цену решений
Main/Fast и риски Fast recover/break/heal. Это read-only анализ, не симуляция.

В [mortal comparison](mortal-combat-comparison.md) записаны факты/точные источники,
граница GM guidance vs executable checks и три варианта. Рекомендация — адаптация
смертного боя к существующим духовным arts без карты и универсального interrupt.
Позиционные поля/задачи сняты; старые reaction stages явно не выбраны и требуют
пересборки после D4. Проект не объявлен новым согласованным implementation plan.

Runtime/builds/balance UNRUN; performance — без новых исполняемых затрат,
для будущего кода сохраняется operation cache/fresh authority. Диагностики нет.
Независимое review `/root/review_mortal_comparison1573`, явно выбранные
`gpt-6.1-sol/xhigh`, отдельный контекст, diff `a225d594..cb0232c2`:
P1/P2 нет. Reviewer проверил источники/выборочный C# через Serena, подтвердил
разделение GM guidance и runtime checks, отсутствие обязательной карты/зон,
сохранение принятых arts. Результат готов к обсуждению направления B, не к исполнению.

Consistency: актуальная spec фиксирует отказ от spatial model; старый reaction
candidate помечен в gameplay/data-model/commands/plan/tasks; выбор D4 и пересборка
единого решения обязательны. 11 Markdown файлов имеют разрешающиеся локальные
ссылки; diff-check успешен; Spec Kit prerequisites -RequireTasks/-IncludeTasks
указывает feature1573. Никакой implementation task не отмечен выполненным.
Checkpoint `cb0232c2caa85590627e33c4b7acdb51478f7546` опубликован; remote SHA
и exact GitHub readback comparison подтверждены. Issue#1573 обновлена текущими
ограничениями/ссылками; предыдущий v1 обозначен частично отозванным.
Clean restore UNRUN. Из review не следует доказательство игрового баланса.

## 2026-10-27 — кандидат v2: разнообразие и математика

Автор отверг ограничение несколькими одинаковыми действиями и запросил полный
бой с искусствами/эффектами/формулами и большими группами. Brainstorming/bridge
продолжены в feature1573; две read-only консультации Sol6.1 High отдельно
разобрали mortal формулы и exact members/effects. Parent проверил первичные
формулы сопротивления/порога, текущие spiritual ranks/ОД и определения effects.

Написан combat-system-proposal.md: факты отделены от предложений, четыре новых
стата/каналы/пробивание/формулы не объявлены утверждёнными, сложные смертные
контракты не выданы за готовый runtime. Найденное противоречие FinalResistance
вынесено по запросу автора в отдельную #1574. Историческая #1350 закрыта при
очистке backlog; будущий probe принадлежит #1573.

Spec расширена AC10–AC13/D7–D9; старые gameplay/plan/tasks явно не исполняемый v2.
Нет реализации или нового runtime поведения. Builds/tests/gameplay UNRUN.
Оптимизация: описаны reuse immutable definitions/профилей существующим сервисом,
полные зависимости/инвалидирование, exact action checks свежие; скорость не заявлена.
Проверка согласованности документационного среза и независимое review — pending.
