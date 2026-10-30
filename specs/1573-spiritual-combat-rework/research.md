# Исследование текущего кода для #1573

## T092–T093: качество обнаружения при фиксированном мире

2026-10-30 baseline d9bb88034. Parent прочёл актуальные crit/find/supply договоры;
Sol6.1/high consult_favor_stone_search1573 независимо сравнил A: обнаружение
настоящей лучшей возможности и B: ещё не канонизированная конечная находка.
Выбран A как рекомендация: сохраняет T091 без нового генератора качества.

- [Принятый crit](damage-and-innate-resistance-proposal.md): h(F), T=20−floor5h,
  Fperm для частоты, margin≥10 и предварительный допуск. Старые гарантированные
  криты приF380 в историческом favor-приложении не использовать.
- [Find-кандидат](favor-critical-and-find-proposal.md): одна проверка, Чуткость
  обычно основная; +1/+2 реликтовой rarity не принято и не распространяется
  автоматически на фиксированные четыре сорта камней.
- [Источники](living-world-events-proposal.md): скрытый канонический предмет уже
  существует независимо от знания игрока; fixed-grade жилы/монстры/склады не
  перебрасываются. Проценты появления типов мест не равны шансу конкретного поиска.

Прямой расчёт целых граней по `(d−15)F ≥100(20−d)` дляd16…20 подтвердил
F0→{20},25→{19,20},100→{18…20},150→{17…20},400/1000→{16…20}:
5/10/15/20/25/25%. Это натуральная часть, без margin и advantage; не оценка
частоты редких камней на кампанию. Пример сохраняет материю:2обычных+1великий
существуют до поиска; успех меняет обнаружение/доступ, не число созданных вещей.
Профиль B потребовал бы нового явного договора источника и не включён молча.
Новая игра/симуляция/tests не запускались; source-аудит не повторял весь проект.

## T090–T091: качество находок и реальные поставки

2026-10-30 baseline78a886c95. Parent сверил принятые камни/обычный бюджет и
границы находок; отдельный Sol6.1/high audit_stone_supply1573 проверил торговые
контракты и Favor. Чтение/арифметика, не runtime или численная игровая проба.

- [Find-quality](favor-critical-and-find-proposal.md): +1/+2 к редкости — ещё
  кандидат, не автоматическая новая шкала насыщения камней. Одна проверка
  получения, существующее скрытое имущество не меняется от неизвестности игроку.
  Исторический guaranteed crit F380 устарел; принятый [crit](damage-and-innate-resistance-proposal.md)
  использует h(F), T=20−floor(5h), не повод создавать новый quality-roll.
- [Guardian inventory](../../Rules/Block_32_Guardians.txt#L664) —4–8 реликвий;
  [GuardianTradeService](../../BookOfEternityClient/Services/GuardianTradeService.cs#L668)
  использует return_incarnation и при совпадении переоценивает существующий товар.
  Это не таймер добычи/подвоза камней. [Продажа](../../BookOfEternityClient/Services/GuardianTradeService.cs#L498)
  не даёт готового отдельного кошелька NPC-покупателя.
- [ShiningTradeService](../../BookOfEternityClient/Services/ShiningTradeService.cs#L420)
  использует shining_return_incarnation. [Профиль торговли](../../BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs#L58)
  и strength tiers25/50/75 влияют на4/6/8слотов и доступность реликвий;
  дополнительные provisioning slots не являются нормой производства.
- [Принятая таблица](out-of-combat-recovery-proposal.md)20/40/160/640 и цены
  фиксируются T090. Старые замечания аудита о непринятости этих цифр до нового
  ответа автора не переносятся в текущий статус. У source-level запасов остаётся
  единое владение; торговая передача не является генерацией наград.

Parent выбрал источники6ч/12ч/4сут/20сут с caps40/20/3/1. Потенциал за20сут
при своевременном освобождении80/40/5/1предметов,1600/1600/800/640ДЭ. Весовые
доли минеральных мест30/60/9/1 суммируются до100; пакеты сильных75/25 и
особых90/10 альтернативны, не суммируются с отдельной наградой камнями.
Общий пример:2обычные жилы×20за10сут=40,10реальных вылазок×4=40.
Начальные1на каждой жиле остаются1к концу; изъятие1.5/3.5/5.5/7.5/9.5,
доставка2/4/6/8/10 с полусуточным обратным путём, торговая приёмка на общей базе.
40−14ремонта−20экспорта=6, стартовый резерв14 становится20. Труд/потери/спрос
и деньги предполагаются явно; это не подтверждение безусловной прибыли.

## T088–T089: скорость отдыха и насыщение камней

2026-10-30 baseline61bf0c02a. Parent сверил актуальные формулы/временной договор;
Sol6.1/high audit_rest_stone_tiers1573 — экономические ориентиры и источники.
Это адресное чтение и арифметика, без новой симуляции или runtime-проверки.

- [S/H-примеры](reserve-and-harmony-proposal.md): приD0 S=H5; D50 —20;
  D140 —65; D1000 —495. Максимум40P+S и регенерация4P+H/5 приняты безC дляS/H.
- [Актуальный ручной тариф и цены культивации, §5.10](stat-growth-and-development-proposal.md):
  22→37 за96, первый R-интервал100. Старый ручной5n в историческом §5.9 не
  использовать. Высший расходник240 конкурирует с постоянным развитием,
  его покупка должна оставаться факультативной при бесплатном отдыхе.
- [Жилы/торговля](living-world-events-proposal.md): accepted10/6 относится
  к обычному камню.1обычный/12ч/cap20 и4изъятых/вылазку — ещё кандидаты;
 4за вылазку не означает производство4/сутки. Нельзя заменить единицу выпуска
  произвольным крупным камнем. Запас и кошелёк покупателя остаются настоящими.
- [GuardianTradeService](../../BookOfEternityClient/Services/GuardianTradeService.cs#L1424)
  имеет отдельные таблицы реликтовой редкости; камни не наследуют их автоматически.
  Степень насыщения и качественная доступность не равны готовому relic rarity.
- [Исходный аудит часов/ресурса](out-of-combat-recovery-proposal.md): progression
  и guarded ledger есть, готовый accepted-rest Gain не доказан. Связь с1536
  требует отдельной интеграции safe-cycle; минутная скорость ДЭ не меняет ран.

Parent выбрал ёмкости20/40/160/640 и цены5/10/50/240 (sell3/6/30/144).
Консультация предлагала20/40/200/1000: более широкий шаг, но дороже верхний
расходник350. Выбран шаг×4 после обычного для первого обсуждения, не как
доказанный глобальный баланс. Обе конечные шкалы уступят16P при достаточно
большомD; нельзя обещать вечную актуальность без потолка развития.

Арифметика: отдыхP+H/20 из нуля даёт36мин при0/5/5,20мин при0/5/25,
52мин при0/25/5,33мин20с при50/20/20,30мин47с при140/65/65,
29мин11с при1000/495/495 (секунды вверх).40ДЭ за минуту уже хуже45.75
естественного отдыха последнего профиля; это сохранённая польза развития.
Равенство с16P приD12.5/75/450/1950; стоимость40ДЭ10/10/12.5/15Перьев.
Ремонт5/10/40/160 сохраняет отношение4:1. Конверсия не вводится; простые
гипотетические котировки40→30,160→144,200→144 не создают прибыль.

## T086–T087: камень, личный ресурс и действие

2026-10-30 baseline04283bae. Parent сверил принятые формулы и прежние результаты
подавления; Sol6.1/high audit_stone_energy_use1573 проверил связанные исходники.
Serena ready; ResourceMutationSourceCatalog доступен символически. Это чтение
источников и арифметика, без исполнения игры/тестов/новой симуляции.

- [Формулы запаса и Гармонии](reserve-and-harmony-proposal.md):40P+S и4P+H/5.
  Активное16P — текущий кандидат Основного действия, не утверждение всего
  Main/Fast-протокола. Ёмкость предметов прежде оставалась открытой.
- [Мortal предметы](../../Rules/Block_7.txt#L80) задают actionCost; зелье обычно
  занимает Main, некоторые предметы допускают Fast по профилю. Это прецедент,
  а не уже существующий духовный договор камня.
- [Действия](spiritual-action-economy-proposal.md): обычный урон сам по себе
  не прерывает, применённые ограничения/стоимости проверяются до Gain;
  повторное отклонение не создаёт завершённый ход с регенерацией.
- [Ресурс](../../BookOfEternityClient/Services/ResourceDefinitionCatalog.cs#L1633)
  имеет Spend/Gain, запрет ухода ниже минимума и clamp максимума.
  [Spiritual outcome](../../BookOfEternityClient/Services/AfterlifeSpiritualConflictResourceOutcome.cs#L679)
  проверяет owner/координаты ledger/before и применяет Spend перед Gain.
  Сам clamp не решает, следует ли тратить предмет при исходном полном запасе.
- [Mortal planner](../../BookOfEternityClient/Services/MortalItemConsumptionPlanner.cs#L182)
  ограничен mortal_world; его проверки carrier/receipt/count/history полезны,
  но перенос готового planner на духовный инвентарь не доказан.
  [Публикация](../../BookOfEternityClient/Services/MortalItemTransitionWriter.Stacks.cs#L666)
  координирует предмет/индекс/ресурс под canonical lease — использовать существующий
  механизм целостности, не второй независимый store.
- [Source catalog](../../BookOfEternityClient/Services/ResourceMutationSourceCatalog.cs#L79)
  привязывает local_item_outcome к Item-owner. Для Gain души нужны согласованные
  источник и полномочия; #1572 остаётся зависимостью, а не готовой поддержкой.
- [Прежняя проба](healing-suppression-proposal.md) не включала камни. ПриH=120P
  регенерация28P покрывала лечениеV28P; новое Основное действие камня не отменяет
  этот вывод и не гарантирует устранение затяжного самоисцеления.

Проверены кандидаты:16P=40 приD75; приD0/50/60/130/1000 активное восстановление
равно16/32/35,2/57,6/336. Фиксированные40 теряют относительную боевую ценность
с ростомD — это явный компромисс; насыщенные разновидности пока без чисел.
При20/180 четыре камня дают180/180 за4минуты и40Перьев покупки; два —100/180.
Это арифметика кандидата, не измерение баланса отдыха/лечения/торговли.

## T084–T085: цены, торговля и производящий источник

2026-10-30 baseline6c22589af. Parent проверил §5.9/5.10 принятой экономики,
реальные таблицы GuardianTradeService; Sol6.1/high audit_stone_trading1573
сверил trade/restock/Favor/NPC hooks. Это адресное чтение, не runtime-проверка.

- [Guardian buy/sell](../../BookOfEternityClient/Services/GuardianTradeService.cs#L1424)
  имеет базовые Common30/10 и разные таблицы других редкостей, затем репутацию.
  Единого процента перепродажи нет; не копировать эти цены на новый расходник.
- [Stock rule](../../Rules/Block_32_Guardians.txt#L664),
  [обновление trade](../../BookOfEternityClient/Services/GuardianTradeService.cs#L668):
  authored4–8 реликвий, identity return_incarnation; открытие переоценивает,
  не создаёт новую добычу. Новый day-based источник камней не возникает из restock.
- [Продажа](../../BookOfEternityClient/Services/GuardianTradeService.cs#L451)
  удаляет stored-реликвию и начисляет Перья, отдельную кассу Хранителя не списывает.
  Новый договор камней должен определить настоящий кошелёк покупателя явно.
  [Shining trade](../../BookOfEternityClient/Services/ShiningTradeService.cs#L620)
  опирается на authored price/экземпляр/receipt; supply modifiers меняют слоты,
  не дают тариф генерации материалов.
- [SR04/#1572](../532-golden-mask-backstory/spec.md#L1824) различает магазин и
  личную экипировку; новые законные поступления не воскрешают проданный ID.
  Favor исключает продажи в текущем spec; бонус допустимой Feather-награды
  отдельно от передачи существующего имущества, см. [правила](favor-reward-proposal.md).
- [Хозяйственные hooks](../../Rules/Block_32_extension_2.txt#L220) допускают пути
  торговли, ремесла/материалов, исследования и помощи; зарплаты/камней в сутки
  там нет. Жители/проекты создают возможности, не готовый бюджет содержания.
  Казна имеет свой вклад/цикл/cap5, не доход каждого убежища за10суток.
- [Принятые цены/источники](stat-growth-and-development-proposal.md#59-реальные-источники-перьев-и-пересмотр-темпа-после-возражения-о37-жизнях):
  первый E-прорыв40, R-интервал100. Старые боевые5–120 — опора масштаба,
  не тариф нового боя; нет измеренного общего «дохода Перьев в сутки».

Кандидат10/6, жила2/сутки/cap20, вылазка4/сутки и новые loot-диапазоны предложены
для обсуждения. Сверены затраты20/40/80 за10суток и160/320/640 за80, ремонт60/120/240;
общая добыча16 покрывает14, одна сорванная вылазка даёт12 и дефицит2.
Моменты2/4/6/8 при росте2/сутки дают допустимые изъятия4 из первоначально пустой
жилы, к10суткам остаются4. Это простая арифметика, не игровой прогон/баланс.
Неопределённые частоты опасностей, доступ и труд не выдаются за доказанную прибыль.

## T082–T083: убежища и предметные границы, 2026-10-30

Baseline7281cbcca. Parent выполнил ограниченный поиск новых камней/износа и
символический просмотр AbodePowerRules; Sol6.1/high audit_shelter_foundations1573
проверил смежные поверхности. Готовых чисел/исполняемого договора для убежищ и
spiritual stones в проверенных Services/Models/Rules не найдено. Это граница
аудита, не утверждение об отсутствии любого слова shelter во всём репозитории.

- [AbodePowerRules](../../BookOfEternityClient/Core/AbodePowerRules.cs#L8) — отдельная
  растущая сила Обители0–100, tiers/торговля/награды. [Power events](../../BookOfEternityClient/Services/GuardianPowerEventState.cs#L68)
  не предоставляют готовый reason для износа убежища. Не применять их рост к D.
- [Chaos politics](../../BookOfEternityClient/Services/ChaosSeaGuardianPoliticsState.cs#L51)
  нормализует отношения/проекты/зоны/хроники/конфликты; новый запас/прочность нельзя
  считать поддержанными просто добавлением произвольных JSON-полей.
- [Contract Matrix](../../OtherGuides/Afterlife_Contract_Matrix.md#L43) ограничивает
  common-resource quartet существующими ресурсами; [Shining ledger](../../OtherGuides/Afterlife_Contract_Matrix.md#L111)
  принадлежит фракции. Ни один не является готовым складом камней убежища.
- [Общий предметный договор #1572](../532-golden-mask-backstory/spec.md#L1868)
  расширяет Soul Relics/materialization/accepted publication, но NPC ownership
  ещё не готов. [InventoryManagementService](../../BookOfEternityClient/Services/InventoryManagementService.cs#L224)
  использует mortal transitions. Нужен согласованный владелец нового запаса,
  однократные acquisition/transfer/spend; не отдельные копии камней для ДЭ/ремонта.

Кандидат2%M/сутки и10D/камень — новые числа для обсуждения. Арифметика50суток
без ухода, расходов2/4/8 за10суток и16/32/64 за80суток, ремонта6/12/24 от40%
проверяется отдельно. Реальная добыча/цена/работа не измерены и не утверждены;
игровой тест/симуляция не выполняются. Raw-диагностики и нового кэша нет.

## T080–T081: источники времени, 2026-10-30

Baseline d352a3867. Parent проверил BuildControlForNextTurnAsync, символы
ApplyVerifiedAfterlifeProgression/ComputeElapsedCycles и правила минут;
Sol6.1/high audit_mortal_clock1573 проверил mortal input, incarnation и frozen
пути. Ограниченный source-аудит без прогона, не доказательство runtime-дефектов.

- [Block17.3](../../Rules/Block_17.txt#L566): ГМ уже назначает логические минуты
  по действию и публикует целый timeChange. [Block23A](../../Rules/Block_23.A.txt#L20)
  допускает абсолютный календарный override; [resolver](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L1577)
  принимает root/nested currentTimeInMinutes, иначе incremental fallback;
  date-only override даёт unresolved. Готовой конвертации произвольного календаря
  audit не нашёл; [validator времени суток](../../BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs#L22)
  использует24h. Нельзя выводить прожитую длительность только из надписи года.
- [BuildControl](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L207)
  выдаёт минимум1afterlife cycle на ход. [Guardian rule](../../Rules/Block_32_Guardians.txt#L248)
  называет это24equivalent hours. Это старый договор, требующий пересмотра для
  физических длительностей; нельзя запускать суточный износ за каждую реплику.
- [Catchup](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L502)
  берёт max прошедших mortal/realm циклов, не сумму. [Подтверждение](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L471)
  переносит marker на current; [целочисленное деление](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L1632)
  при таком переносе не сохраняет неделимый остаток. Новому1:365 нужен явный остаток.
- [Incarnation bootstrap](../../BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs#L3325)
  меняет incarnation и строит control до свежих mortal files; [ClearCurrentWorldLore](../../BookOfEternityClient/Core/FileSystemManager.cs#L7014)
  сам не очищает world_time/schedule. Нельзя вычитать абсолютные часы разных миров;
  новый источник длительности требует исходной точки своего воплощения.
- Sanitize/control могут повторно читать incremental timeChange; [frozen basis](../../BookOfEternityClient/Services/ProgressionScheduleService.MortalJointBasis.cs#L49)
  имеет собственный resolution. Это риск интеграции, а не проверенный прогонами
  симптом: использовать подтверждённые интервалы однократно в общем принятом
  результате, не присоединять накопление к чтению. Оба пути требуют согласования.
- [MortalWoundGameTimeAuthority](../../BookOfEternityClient/Services/MortalWoundGameTimeAuthority.cs#L13)
  использует локальные authoritative минуты. Сжатие добавляет духовный интервал,
  не меняет смертные сроки ран/лечения. Существующие mutable markers/authority
  должны проверяться свежими; отдельный кэш арифметики не нужен.

Арифметические примеры1/30/365/3650/29200/365000суток разделены на365;
таблица приложения соответствует результату. Это проверка арифметики, не игровой
баланс выживания убежищ, отдыха или проектов. Новых raw-артефактов нет.

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
| Ресурс имеет coordinate/current/maximum/capacity/history | [ResourceState](../../BookOfEternityClient/Services/ResourceStateContract.cs#L11) | Хранить HP/ДЭ в общем resource ledger, не в параллельном store |
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
- Зоны/граф/формальная пространственная модель, предложенные в v1, отвергнуты
  автором. В v1.1 они сняты; mortal Block13.2.2 уже даёт повествовательное движение.
- Не считать тестовый AfterlifeSpiritualCombatScenarioHarness production engine:
  он содержит собственные цены/контест/награды; баланс проверять новым production kernel.

## Неустановленное

Числа развития/HP/урона, critical/healing/wound thresholds и все решения D1–D6
остаются обсуждаемыми. Наличие модуля не доказывает корректность будущего переноса.
Code reading доказывает ограничения текущего контракта, не результат будущих тестов.

## Исправление исходной исследовательской базы

Перед v1 не был изучен полный смертный combat workflow. Проверка общих ресурсов/
QTE не заменяла этого исследования; это ограничение предыдущего предложения.
После замечания автора исследованы Blocks6/12/13/14/15/15.A, примеры, combat
identity/owner/validation/UI/TurnRequest. [Сравнение](mortal-combat-comparison.md)
отделяет существующие GM-правила от проверяемых клиентом контрактов и предлагает
адаптацию смертного боя без карты. Отсутствие полноценного client scheduler в
проверенном пути не означает отсутствия описанной системы боя.
## Уточнение 2026-10-30: население, занятия и ДЭ вне боя

T075–T077, baseline df38466287dfe28ae8d41b1e7567957da3b8f1f7.
Узкое чтение исходников, без запуска игры/тестов. Два read-only Sol6.1/high
аудита (audit_chaos_population1573, audit_shining_activities1573), parent проверил
ключевые места приёма, ресурсов и расписания. Это опоры обсуждения, не полный
аудит всех путей и не утверждение, что новые механики уже реализованы.

| Проверенная опора | Что она означает для запроса |
|---|---|
| [Поиск Обители](../../BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs#L337) | Текущая исследовательская команда ориентирована на Хранителей; результат поиска материализует ГМ. Новые встречи требуют своего содержания и поддерживаемого состояния. |
| [Типы духовных личностей](../../BookOfEternityClient/Services/AfterlifeEntityProfileState.cs#L46), [отношения](../../BookOfEternityClient/Services/AfterlifeEntityProfileState.cs#L1392) | Есть resident/guardian/custom_afterlife_actor и сохраняемые отношения. Это основа отдельных душ; группа/убежище не возникает автоматически из профиля. |
| [Состояние резидента](../../BookOfEternityClient/Services/GuardianAbodeResidentState.cs#L224), [переход](../../BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs#L1039) | Есть личность, привязанности/беспокойство и перенос того же residentId с receipts. Нынешний transfer начинается с уже существующего резидента, не решает приём встреченной свободной души. |
| [Политические участники](../../BookOfEternityClient/Services/Validation/ValidationService.ChaosSeaGuardianPolitics.cs#L81) | Текущие relations/projects/influenceZones рассчитаны на известные guardian IDs. Союз/рейд лидера убежища нельзя просто объявить существующей политикой Хранителей. |
| [Циклы](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L233) | Есть контуры Моря, проектов и resident agency, а также переходы/catchup. Износ убежищ и движение групп нуждаются в своём явном договоре; реальное время компьютера не заменяет игровые циклы. |
| [Описание Обители](../../Rules/Block_32_extension.txt#L24) | Стабильный карман реальности Хранителя отличается от предлагаемого распадающегося убежища. Автоматическое преимущество над любой численностью убежища не найдено в целевом runtime-аудите; это новый требуемый договор. |
| [Духовный ресурс](../../BookOfEternityClient/Services/ResourceDefinitionCatalog.cs#L1634) | Уже есть spiritual_action_points с Spend/Gain и границами; текущий owner допускает actor/side, #1573 ещё меняет это. Наличие Gain не означает готовое расходование духовного камня. |
| [Текущий инвентарь](../../BookOfEternityClient/Services/InventoryManagementService.cs#L64), [общие духовные реликвии #1572](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1572) | ReadContextCoreAsync опирается на MortalItemMaterializationContract/EquipmentAuthority; перенос духовных камней сюда без определения scope был бы ошибкой. #1572 задаёт общий ownership/trade/loot; отдельной feature1572 в этой ветке нет, документы находятся в feature532. Новый расходник следует связать с общей духовной предметной системой, а не создать ещё одно хранилище. |
| [Начальное состояние Сияющей Обители](../../BookOfEternityClient/Services/ShiningAbodeState.cs#L389), [местность](../../OtherGuides/Shining_Abode_Contract.md#L73) | halls/factions изначально пусты и материализуются ГМ. currentHallId ограничивает услуги наставников/магазины; новая библиотека или мастерская должна стать реальным доступным местом, не только словом в ответе. |
| [Завершение проектов](../../BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs#L206), [кузня/торговля](../../BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs#L367) | Есть оплата/результат проекта, усиление фракции, развитие, улучшение реликвий. Само наличие завершения не создаёт последовательность сцен исследования или ремесленного заказа. |
| [Обучение](../../BookOfEternityClient/Services/TrainingService.cs#L386), [личные взаимодействия](../../BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs#L3144), [городская жизнь](../../Rules/Block_CLI_Operations.txt#L1105) | Наставники и запросы жителей имеют реальные пути, civic progression поддерживает события ГМ. Доступность каждого резидента в каждом зале и отдельные циклы занятий этим не доказаны. |

Направление обсуждения для Сияющей Обители: исследование находок/прошлого,
восстановление произведений и реликвий, личные просьбы/дружба, совместные заказы,
публичные состязания и праздники. Содержание должно давать выбор в сценах и
заметное последствие. Участие фракции допустимо без обязательной вражды; должны
быть полностью местные занятия. Не представлять эти предложения готовыми runtime
лабораториями/доской заказов/турнирами — такие общие циклы в этом аудите не найдены.

Бесплатный отдых и камни приняты автором; скорость и боевой расход камня открыты.
Числа прежнего подавления действительны для его исходных условий; новые предметы
их не проверяли и не исправляют доказанную проблему сильного самоисцеления.
Оптимизация: адресное чтение вместо генерации миров; новый runtime/кэш сейчас
не создаётся. Будущие постоянные данные и состояния используют существующие
сервисы, одноразовые источники/публикацию и точную инвалидизацию, по правилам проекта.

## T078–T079: автономность, известия и удаление «Нового цикла»

Baseline db64e5a84a625f2f8929a81aeabaf9baeecc67bb. Parent прочёл BuildAfterlifeCatchupContext/
Contours через Serena, фактический reset/reentry и foundation check; отдельные
Sol6.1/high read-only audit_world_news1573 и audit_realm_return1573 проверили
контуры сведений и зависимости переходов. Это ограниченный source-аудит, не
проверка исполнения; no tests/build. История до этого среза сохраняется выше.

| Источник | Вывод для кандидата |
|---|---|
| [ProgressionScheduleService:502](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L502), [контуры:557](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L557) | Catchup использует прошедшее mortal world time и realm ordinals; для смертной жизни может включать все активные afterlife-контуры. Обычный набор зависит от realm. Новые самостоятельные поселения и постоянная жизнь другого пространства требуют явного покрытия, не возникают автоматически. |
| [Константы расписания:20](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L20), [контроль сводки:1291](../../BookOfEternityClient/Services/ProgressionScheduleService.cs#L1291) | Есть эквивалент24h/1440мин и bounded summary count до5 по tier. Это нынешний отчётный контракт, не согласованный баланс частоты событий/износа, не лимит полной истории и не квота диагностики. |
| [AfterlifeChronicleState:15](../../BookOfEternityClient/Services/AfterlifeChronicleState.cs#L15), [архив:237](../../BookOfEternityClient/Services/AfterlifeChronicleState.cs#L237) | Хроники поддерживают области Моря/Сияющей Обители и сохраняют persistentConsequences/openThreads; eventDescriptions — дедуплицированные строки, не полноценные события с occurrence/acquisition и источником знания. |
| [Видимость хроник](../../BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs#L3791) | Есть фильтрация скрытых хроник, но видимый объект сам по себе не означает допустимость каждого нового скрытого факта внутри. |
| [AfterlifeNotificationState:209](../../BookOfEternityClient/Services/AfterlifeNotificationState.cs#L209), [upsert:1980](../../BookOfEternityClient/Services/AfterlifeNotificationState.cs#L1980), [журнал actor](../../BookOfEternityClient/Services/ActorJournalState.cs#L116) | Есть typed inbox, устойчивые notification/request IDs/read state и записи разговоров. Произвольное событие убежища ещё не имеет автоматического канала доставки, знания рассказчика или задержки. |
| [Первое вознесение](../../BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs#L4294) | Сейчас требуется максимумE и явный Ascension. Не переиспользовать это начисление/инициализацию для каждого посещения после вознесения. |
| [Выход/New Cycle](../../BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs#L2754) | Нынешний return_to_chaos_sea/new_game_plus запечатывает Обитель и заменяет enlightenment/soulProgression на начальные. Противоречит новому принятому договору; изменить при реализации, а не только переименовать кнопку. |
| [Reentry](../../BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs#L2283), [preview](../../BookOfEternityClient/Models/GameState/AggregatedGameState.cs#L77) | Reentry уже не требуетE, но требует active/отсутствия package/return-guard/активного боя. Preview не включает весь conflict blocker исполнения. Согласовать оба клиента и новые условия постоянного доступа. |
| [Browser dispatch](../../BookOfEternityClient/WebUi/ExplorerWebCommandService.cs#L37) | Browser идёт через command catalog; наличие canReenterShiningAbode в DTO не означает исполняемые browser-команды перехода. Аудит не нашёл их в каталоге — это отдельный пункт паритета. |
| [Ритуал основания](../../BookOfEternityClient/Services/PlayerGuardianFoundationState.cs#L385), [его validator](../../BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs#L5037) | Оба требуют sealed_until_next_ascension. При удалении New Cycle нужен заменяющий допуск, с сохранением прочих требований и однократности; нельзя оставить ритуал навсегда недоступным. |
| [Help](../../BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs#L254), [GM guidance](../../BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs#L4713) | Подсказки/алиас и GM явно обучены сбросу и запечатыванию. Будущая реализация обязана удалить именно этот продуктовый договор во всех входах. |
| [Return cycle](../../BookOfEternityClient/Services/ShiningAbodeState.Gacha.cs#L164), [incarnation package](../../BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs#L4673) | currentIncarnation/currentReturnCycleId и frozen bootstrap относятся к смертным воплощениям/одноразовой экономике. Они не равны отменённому New Cycle; посещение не создаёт нового return-cycle и не удаляет подготовленный пакет. |

Устаревший sealed mode встречается также в нормализации, Shining validation,
боевом frame и статусе UI. Удалять режим согласованно, сохраняя другие проверки
активного режима/pending пакета. Старые development saves не требуют отдельной
совместимости; обновить актуальные fixtures/bootstrap/schema по новой модели.
Оптимизация обсуждения: адресное чтение и повторное использование прежних аудитов;
полный мир/боевая симуляция ради записи этих решений не создавались.
