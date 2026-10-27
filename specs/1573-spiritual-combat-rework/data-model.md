# Предлагаемая модель данных #1573

Draft v1, зависит от [spec](spec.md) и [gameplay](gameplay.md). Имена новых
типов/полей — предложения. Существующие механизмы перечислены в research.

## Владение

| Сущность | Хранит | Не хранит/не заменяет |
|---|---|---|
| CombatInstance | client instanceId, displayId, scope, ruleRevision, revision, dangerMode, objective | Ресурсы общей стороны |
| Participant | participantId, exact actor binding, sideId, controlMode, membership/status, resource/effect/wound owner refs | Вторую изменяемую копию HP/ОД |
| Actor profile projection | development, learned arts, focus, equipped relics, derived stats, authority fingerprints | Глобальный player profile для любого NPC |
| Resource ledger | spiritual_health и spiritual_action_points по exact coordinate | Состояние инициативы/порядка ходов |
| Effect ledger | конкретный source/target, barrier/control/modifiers, stacking/lifetime | Дубликат mutable combatConditions/controlState |
| Schedule | round, ordered participant IDs, current slot, spent slots, reaction budgets, phase | Разрешение на любой будущий ход |
| Battlefield | bounded zones/edges, actor locations, exact tactical relations | Всю карту мира |
| ActionRecord | actionId, executor/targets, type/mode, trigger, slots, admitted costs, dice refs, result event refs | Право GM переписать accepted HP |
| PendingCombatDecision | instance/action/revision, responder, admitted options, immutable attacker intent, private dice reservation reference | Произвольный следующий player turn/GM repair |
| CombatOutcome | individual exits, remaining contenders, objective evidence, aggregate outcome, reward refs | «Упал lead — проиграли все» |

Новые идентичности клиентские и уникальные в scope/instance. Display name/id
не является ключом. При смене стороны/лидера participant не получает новый
resource owner. Повторный конфликт может использовать тот же displayId,
но instance/action IDs и доказательства относятся к конкретному экземпляру.

Внутри scope/combat отображение participant→exact actor binding инъективно:
одна душа не может получить две записи/очереди под разными participantId.
Binding учитывает realm/scope и зарегистрированного владельца, не display name.
Выход и повторное вступление не создают новую боевую личность; сохраняются
прежние потраченные слоты/реакции этого раунда. Смена стороны/роли также не
обновляет бюджет. Duplicate owner/binding — ошибка всего ещё не принятого состава.

Постоянный actor использует свой существующий зарегистрированный владелец.
Historical role получает отдельный зарегистрированный scope с собственными
профилями/ресурсами/эффектами/ранами/экипировкой. References на героя прошлого
служат происхождению, не разрешают запись в нынешний actor. Tombstone owners
нельзя переиспользовать как роли. Выбор файлов для scope — отдельный согласуемый
adapter design в #1563–#1565; никакого hidden fallback на player_soul.

## Состояния и порядок

Участник: active → incapacitated/surrendered/withdrawn/dissipated по разрешённому
событию; восстановление incapacitated требует HP>0 и правила re-entry.
Оковы — effects, не единый статус «нельзя ничего». HP=0 и active несовместимы
на стабильной границе публикации; промежуточные события одной цепочки имеют
точную последовательность и не выдаются UI как завершённое состояние.

Schedule: start → round_open → actor_turn_open → awaiting_action →
action_declared → awaiting_reaction (если нужно) → resolving → action_complete →
actor_turn_close → next_actor/round_close → next_round/resolved.
Movement phase допускается один раз до/после main по gameplay; нельзя использовать
его повторно через два accepted сегмента одного personal turn.

На round_open обновить reaction budgets ровно один раз, записать eligible roster.
На actor_turn_open закрыть эффекты «до начала этого хода», затем проверить
возможность участия. На actor_turn_close сработать due actor-turn эффекты,
завершить слот. На round_close сработать due round эффекты один раз.
Эффект с N полными раундами получает точный первый due boundary: неполный раунд
применения по умолчанию не считается полным. Эффект «до конца текущего раунда»
указывает текущую границу явно. Не подменять один срок другим.
Смена clock/version не меняет прошлые события, duplicate boundary ничего не тикает.

## Последовательность разрешения действия

1. Проверить актуальные instance/revision, действующего, слот, цель/дальность,
   способности, ограничения, ресурсы и control/effect sources.
2. Зафиксировать намерение, targets и назначение клиентских костей до раскрытия.
3. Принять допустимую реакцию/явный pass; не менять атакующего/приём по новым костям.
   Допустимое прикрытие меняет effective target по отдельному правилу ниже.
   Принятие declaration/response сохраняет обязательство соответствующего слота
   и точной цены. Следующий decision admission учитывает его уже сейчас.
4. На общей границе оплаты проверить сумму законных обязательств каждого владельца
   и оплатить их из его current, атомарно преобразовав reserved slots в spent.
   Только затем развернуть causal effects оплаты через общий planner в стабильном
   порядке. Они могут прекратить исполнение, но не отменяют уже законную оплату.
   После этой границы новые modifiers не пересчитывают прошлую цену.
5. Рассчитать проверку и защиту на разрешённой границе из актуальных производных
   DATA. Изменившиеся источники требуют новой проекции, не переноса stale кэша.
6. Поглотить барьер/повредить HP выбранных целей, затем положенные post-hit,
   wound procedures и последствия. Одна цепочка имеет явный event order.
7. Вывести статусы/индивидуальный исход, закрыть действие и нужные clock events,
   затем проверить общее завершение. Числа snapshots — проекции этих событий.

Неуспешная валидация отвергает весь ещё не принятый сегмент. Принятый прежний
префикс не откатывается из-за следующей ошибки GM. Завершённые оплаты/броски
не пересчитываются при repair; сравнение exact receipt различает retry и подмену.

Обязательства — каноническая часть незавершённого action/response, не второй
resource ledger. Доступно для нового admission = current минус сумма ещё не
оплаченных обязательств owner/resource. Нельзя зарезервировать вторую реакцию
того же участника или оплатить обязательство будущим Gain. Пока игрок выбирает,
lease не удерживается; reload восстанавливает обязательства из принятого состояния.
Consume/release публикуются общим планом ровно один раз. Разрешённая отмена
освобождает только ещё не оплаченные обязательства action; не возвращает уже
законные расходы префикса. Exact повтор response не создаёт второй reservation.

## Кости и несколько целей

Reservation связывает instance/action/subcheck/actor/target/ruleRevision.
Использованный индекс не назначается второй раз; изменение порядка targets
не переставляет удачные кости. Скрытые результаты не входят в player DTO.
Ремонт неправильного audit не выдаёт новый пул. При нехватке зарезервированных
костей continuation получает клиентское расширение того же назначенного потока;
перезапуск не генерирует альтернативу уже назначенным значениям.
GM-планирование NPC должно предшествовать раскрытию предназначенных ему бросков:
нынешний first-pair preview не должен давать выбор из будущих удачных атак.

AoE фиксирует упорядоченный набор targets по стабильным participant IDs после
геометрического выбора области, до раскрытия костей. Несколько решений реакций
собираются в этом порядке, без показа уже известных серверу будущих результатов.
Для первоначальной версии предлагается одновременно применять результаты AoE
к целям после сбора реакций; causal effects получают стабильный порядок событий.
Применение эффекта к первой цели не меняет уже зафиксированную область атаки.
Цепные/перенаправляемые искусства обязаны явно задавать иную последовательность.

Уточнение кандидата D5: обычное прикрытие перенаправляет один sub-hit ровно один
раз; сохраняются originalTarget/effectiveTarget и источник изменения. Цепочка
«прикрыть прикрывающего» запрещена. Защитник должен быть в допустимом положении,
иметь реакцию/ОД и не присутствовать среди initial либо уже effective targets
этой AoE. Уже попавший в её область союзник может защищать себя; его отдельное
искусство союзного барьера не считается перенаправлением. Так один actor не
получает два прямых попадания одной обычной AoE через прикрытие.

Бросок атаки закреплён за исходным sub-hit. Защитная кость назначается окончательной
цели детерминированно после фиксации прикрытия, до раскрытия; исходная неиспользованная
защитная reservation не становится запасной костью. Условие Приговора проверяется
для effective target по состоянию ДО действия. Контроль original target не
усиливает Приговор против свободного защитника. Защита/сопротивления окончательной
цели рассчитываются на общей границе resolve, с законно выбранной реакцией
и уже обработанными cost effects, как в шагах4–5; snapshot условия искусства
не замораживает все параметры защиты в прошлом.
Выбор прикрытия фиксируется до результата, не даёт переключаться между бросками.

После action_declared игрок не может сменить экипировку/цель/стоимость через
параллельную обычную команду. AP до resolve ещё не списаны, но объявленный action
не отменяется добровольно для повторного выбора костей. External authority drift
требует typed invalidation/cancellation с причиной и сохранением истории; repair
не разрешает тихо заменить action или вернуть уже потраченные ресурсы префикса.

## Публикация и восстановление

Один Common AcceptedMechanics plan экспортирует изменения conflict/schedule,
resource state/history, effect state/history, wounds, outcome/reward и pending
decision вместе. Используются существующие signed snapshot, capability,
normalizer, before-images/rollback и replay, без отдельного combat writer.
Промежуточный pending decision — явное завершение принятого сегмента; action
ещё не resolved. Damage/final narration не публикуются до решения реакции.

Snapshot/read-set включает все authoritative actor/resource/effect/wound/
schedule/definition источники. Preview не даёт права на будущую публикацию.
При изменении не принятого response повтора сравнивается instance/action/version,
а не только display conflictId. История содержит ссылки на exact события,
получившие ресурсный/эффектный receipt, а не вторые изменяемые значения.

## Кэширование

Владелец — текущая planning/validation operation и существующий accepted-plan
cache. Стабильные DTO/profile/participant projections повторно используются
по fingerprint входа, версиям definitions/rules, scope/instance/membership,
actor/equipment/wound/effect/resource generations, action prefix и clocks.
После изменения любой зависимости — новый epoch/проекция. Новое действие
не использует старое разрешение на применение; свежие owner/admission/expected
version проверки остаются. Не создавать параллельный static/dictionary cache.
Чистый kernel получает уже собранные immutable DATA и ничего не читает с диска.
