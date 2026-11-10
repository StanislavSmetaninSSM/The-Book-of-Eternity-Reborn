# #1536: граница dependency cut и тестовое наблюдение

Дата: 2026-11-08. Составное доказательство принято для узкой реализации T081-D-RECON12.
Точный прежний OwnApSpent oracle и четыре старых direct-cut случая остаются отдельными HOLD.

## Решение

Отдельная операция cut не нужна продукту. Все найденные продуктовые вызовы уже
выполняются внутри существующих Mortal/Spiritual materialization или combat
healing owners. Именно затронутые Spiritual materialization и combat healing
владеют combined allocation speculation. Mortal использует свой owner-путь;
общий Spiritual replay scope ему здесь не приписывается. Не следует добавлять
новый отдельно коммитящийся путь изменения состояния ради тестов.

Четыре положительных direct-cut случая можно перевести на существующую
MaterializeWoundAsync и сохранённые detached before/receipt данные. Но проверку
того, что старый mechanics context устарел именно от cut, нужно сохранить явно.
OwnApSpent дополнительно требует настоящего cold/warm запроса между cut и wound
insertion. Существующая транзакционная граница этого не допускает.

Для T081-D-RECON12 принят небольшой тестовый механизм наблюдения, а не новый
продуктовый cut API. Составное доказательство реализуется отдельно и не объявляется
эквивалентом точного прежнего OwnApSpent oracle. Все пять старых HOLD сохраняются,
пока их собственные требования не будут отдельно покрыты.

## Варианты

1. **Standalone capture-owned cut. Не рекомендуется.** Он сохранил бы прежний
   промежуточный тестовый шаг, но создал бы новую отдельно фиксируемую границу
   мутации без продуктового потребителя. Понадобятся новые правила повторов,
   revocation и передачи before-authority. Это лишнее расширение.
2. **Только существующая materialization. Недостаточно для полного закрытия.**
   Она проверяет настоящие эффекты, историю и продолжение, но её selection заново
   проверяет opportunity и заранее инвалидирует ordinary DATA. Такой тест нельзя
   выдавать за независимую проверку invalidation от одного cut.
3. **Существующая materialization плюс ограниченное наблюдение. Рекомендуется
   только после согласования изменения oracle.** Отдельно доказать invalidation
   на настоящем cut, затем сохранить полноценный cold/warm lifecycle-тест. Это
   составное доказательство, а не тот же самый изолированный post-cut запрос.

## Почему простой callback не подходит

MaterializeWoundAsync удерживает capture gate и combined replay speculation на
всём участке cut → prepare → apply → register → commit. Существующие внешние точки входа comparison и
probe повторно берут capture gate, поэтому callback с их вызовом зависнет.

Private under-gate core решает только повторный gate. Настоящий cold query
открывает BeginConflictValidationProbe, который начинает speculation в том же
journal. Вложенный scope окончательно отзывает journal. Полный closed snapshot
также читает committed cursor/export, запрещённый до commit.

Нельзя ради теста разрешать вложенные scopes, снимать committed-export guard,
подставлять другой clock или коммитить materialization посередине.

## Узкий предлагаемый механизм

Использовать существующий образец одноразового запроса диагностического
наблюдения и сохранённого исторического отчёта. По умолчанию он выключен.

- Запрос связан с текущим capture, canonical lease и точным admission. Он не
  создаёт нового права на cut или публикацию.
- После selection, но до allocation speculation, при необходимости выполнить
  cold/warm ordinary comparison через общий private under-gate implementation,
  сохранив все текущие fresh input/dependency checks.
- Непосредственно перед и после настоящего AdvanceForWoundInsertion сохранить
  только detached факты: current/stale состояние старого mechanics context,
  точную stale-read ошибку, версии wound/cut, неизменность source ordinal и
  routing identity. Не выполнять внутри scope новый cold query или полный probe.
- Продолжить прежнюю materialization без раннего commit. После её завершения
  проверить сохранённые before DATA, trigger/reaction receipts, history,
  allocation counts и продолжение через существующие owner-пути.
- Оставить отдельный полный post-materialization cold/warm тест. В названии и
  документации явно указать, что cut-only predicate и whole-lifecycle
  recomputation проверяются разными частями доказательства.
- Отчёт не возвращает draft, scope, clock, before-authority, callable prepare/apply
  или произвольный callback. Повтор не выполняет cut и не выделяет новые identity.

## Обязательные проверки до принятия реализации

Сохранить содержательные проверки четырёх положительных сценариев: consuming и
non-consuming trigger, terminal expiration, relocated stacking, точные before
images, receipts, writer ranges, повтор без новой allocation и продолжение.

Для observation добавить отказ на foreign/stale lease или admission, повторный
запрос, выключенный механизм без изменения поведения, недоступное наблюдение,
исключение после мутации, revocation/disposal и отсутствие передаваемой authority.
Cold/warm проверки по-прежнему должны доказывать реальные computation counts,
нулевой actual-work delta у ordinary-запросов и свежие проверки прав/зависимостей.

В исходной theory есть также corruptBefore row с reflection. Она меняет только
candidate before DATA, сохраняя настоящий writer/arbiter. Подмена её обычной
порчей файлов проверяла бы другой случай. Миграцию наблюдений следует делать на
typed detached DATA; для отрицательного случая отдельно согласовать строго
ограниченную candidate-data fault injection либо оставить его миграцию открытой.
Универсальный setter или mutation callback не подходит.

## Границы и следующий шаг

Предлагаемый механизм нужен тестам, не игровому процессу. GM-facing контракт,
формат сохранений и механика ран не меняются. Никакой код не изменён и новые
build/test запуски не выполнялись: результаты прошлых проверок не переименованы.
31 556 исходных файлов совпадают с итоговыми pins предыдущего блока; HEAD/index
и обычный selection неизменны. GitHub не публиковался.

Вариант 3 принят как отдельный ограниченный тестовый дизайн и реализуется
по cut-observation-plan.md. Если требуется буквально прежний изолированный post-cut
cold/warm oracle, OwnApSpent следует оставить HOLD до отдельного более широкого
дизайна наблюдения за активной транзакцией. Ослаблять существующие guards нельзя.

Оптимизация: проверки скалярных версий/currentness дешевы и не требуют нового
кэша. Для cold/warm использовать существующий comparison/cache implementation,
а не параллельный тестовый вычислитель. Настоящие owner-fixtures должны оставаться
независимыми. Никакого ускорения пока не заявляется.

## Опорные места в исходниках

- ValidationService.SpiritualOriginalTurnMaterialization.cs: 98–109, 156–213:
  owning transaction и revocation после неуспешной мутации.
- ValidationService.SpiritualOriginalTurnWoundSelection.cs: 196–225;
  SpiritualOriginalTurnOpportunities.cs: 452–465;
  SpiritualWoundSourceTransaction.cs: 405: повторная проверка и invalidation.
- ValidationService.SpiritualExistingPressureOrdinary.cs: 68–112, 138–166,
  298–308, 830: существующий one-shot observation, gates и cold probe.
- SpiritualWoundProjectionClock.Probes.cs: 60;
  SpiritualWoundReplayJournal.Speculation.cs: 24–30;
  SpiritualWoundReplayJournal.cs: 207–228: общий journal и запрет вложенного scope
  или чтения committed export в незавершённой speculation.
- EffectAcceptedTurnPlanner.DraftWoundDiagnostics.cs: 155–183;
  DraftWounds.cs: 204–220: сохранённые before/receipt DATA.
- AcceptedMechanicsPlanner.SpiritualMechanics.cs: 98–102, 146–155:
  currentness и stale-read predicate.
- AfterlifeResourceCutoverTests.SpiritualWoundDependencyCut.cs: 171–202;
  SpiritualPressureGroupTransitions.cs: 52–123: исходные тестовые границы.
