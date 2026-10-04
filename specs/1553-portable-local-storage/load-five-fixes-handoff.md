# Передача файлового контура загрузки — #1553

Обновлено: 2026-10-04. Задача [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), ветка **`codex/1553-load-filesystem`**. Файловые B1/B2/B3 и B5-FS приняты: native Linux proof, полный GitHub readback и финальное независимое gpt-6.1-sol / xhigh ревью `6133bfed117d1cbc0d28d5a7e0ea6146b081e8e2` прошли без открытых замечаний. Публичный B4 продолжается и пока не завершён; текущий public-entry checkpoint и отдельный worker blocker описаны в начале plan.md.

## Восстановление и текущие источники

Получить актуальный полный SHA ветки и новый чистый checkout, сравнить удалённый ref:

```sh
git clone --single-branch --branch codex/1553-load-filesystem https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git boe
cd boe
git rev-parse HEAD
git ls-remote origin refs/heads/codex/1553-load-filesystem
git status --short
```

Прочитать AGENTS.md, docs/development-workflow.md, docs/testing.md, [spec.md](spec.md), [ordinary-load-plan.md](ordinary-load-plan.md), актуальные [plan.md](plan.md) и [tasks.md](tasks.md). Владелец разрешил автономные рекомендуемые ревизии; обязательное независимое ревью — отдельный **gpt-6.1-sol / xhigh**. Не начинать от старых `bd5cb827`/`codex/1553-save-windows` и не применять архивные пакеты.

- Возвращённая Windows-передача: `07d354e16ce06796dee435003bd5fd2a023be62e`; её C# closure — `e593bcfa5e45502de9eb6d6b3949565f7c7881b2`
- Текущий production closure после причинного Linux case-pair исправления — `2da4d545c9398ab1db019609d14429f9480574c2`
- Текущий исправленный resource probe и его тесты — `eed0ca99e7ad8ef8306c6f4758ed55cce722fd46`
- Все runtime/resource/audit доказательства опубликованы в `f72830d5f66a3131ab0bb7c77c82280856439357`; [новый полный GitHub readback](recovery/load-filesystem-linux-github-readback.json) проверил 4 727 файлов, 123 изменения, 71 JSON и 37 TRX, чистоту и connectivity. Поздний документальный carrier сверяется отдельно

## Проверенные границы

[Windows proof](recovery/load-filesystem-windows-qualification.json) сохраняет свои точные исходники и отдельные результаты FIX, metadata, namespace, cold и 12 ресурсных случаев / 28 измеренных процессов. Повторные неизменённые Windows-прогоны не выполнялись. Ревью подтвердило сохранение Windows-поведения: новое различение исходных имён включается только для typed Linux load; дополнительные проверки Windows-словарей избыточны после прежнего отказа дубликатов. Старые результаты не выдаются за новое Windows-исполнение.

[Native Linux proof](recovery/load-filesystem-linux-qualification.json): Debian 13 x64, PowerShell 7.6.6, SDK 10.0.401, runtime 8.0.31, process-local telemetry opt-outs, DOTNET_PROCESSOR_COUNT=1 и XML-документация. Отдельные когорты:

- Первый baseline 53/53; девять неизменённых lease consumers сохраняются отдельно
- Исправленные names/aliases/admission/entry/outcomes 52/52, включая восемь новых проверок исходной идентичности; 44 строки перекрываются с baseline, суммы не складываются
- Metadata + transport 39/39; fresh path/batch 46/46; namespace/native/cold/v1/v2 consumers 128/128
- Native sampler 10/10 после причинного RED: Unix cached attributes=-1 больше не считаются реальной ссылкой; настоящие ссылки и stable/unknown missing остаются отказами
- Пять отдельных resource-команд: preparation 4/4 (1:35.334), bulk publication 3/3 (1:25.260), maximum inventory publication 1/1 (1:02.958), bulk recovery 3/3 (1:28.207), maximum inventory committed recovery 1/1 (1:06.617). Всего 12/12 и 28 измеренных детей, полные независимые state/source/library/generation проверки и cleanup
- Discovery-only аудит 221 категорий / 10 570 методов-файлов, ноль исполненных тестов; пять XML сборок читаются, в изменённых файлах нет XML compiler warnings

Все resource-пределы сохранены: heap 768 МиБ, RSS 1 ГиБ, owned disk 5 ГиБ, 180 секунд на child; 600 секунд только для maximum-inventory publication/cut. Category 10 минут для preparation/bulk и 12 для inventory, команда 15 минут. Polling delay 100 мс не обещает такой частоты при синхронном disk scan. Forced closed-boundary disk samples и финальный OS RSS peak сохранены. Guard stop, partial phase и cleanup failure — не PASS. Первые source-specific resource-проходы, реальный inventory non-pass и причинные RED сохранены отдельно.

Это actual native cloud Linux qualification через обычный runner; GitHub Actions execution не используется как доказательство. Ограниченная workflow/selection установлена, никаких merge ради CI не сделано. Не повторять весь набор или неизменённые resource-фазы при последующих B4 UI-правках. Команды и среда — [quickstart.md](quickstart.md); ответственность и бюджеты — tests/categories.json. Полные/Fast/PreMerge прогоны запрещены.

## Callable boundary для B4

`SaveLoadService.LoadGameWithOutcomeAsync(string, CancellationToken)` и `LoadReplacementResult` теперь публичны. Старый `LoadGameAsync` использует этот же portable loader и возвращает только факт commit: true сохраняется даже при поздней ошибке и blocked continuation. Его bool не разрешает продолжение или безопасный повтор. Console/browser ещё должны перейти на полный typed result; это остаётся открытым B4. Public-entry проверки: отдельные 6/6 и шесть прямых consumers в сохранённом partial run; весь 13-case selection не прошёл из-за pre-load worker blocker.

- `NotLoaded`: замена не начата; приватная cleanup-проблема может требовать follow-up, сама по себе не блокирует каноническое продолжение
- `Committed`: новый generation подтверждён; поздние refresh/log/release/cleanup ошибки не отменяют commit
- `RolledBack`: подтверждены точные прежние bytes/absence и прежний generation
- `Uncertain`: established generation отсутствует, follow-up обязателен, каноническое продолжение блокируется до разрешения evidence

Runtime обновляется только после подтверждённого commit. Связанный с тем же root вызов запрещён до подготовки; detached settings/profile не публикует runtime заранее. Сохраняются полная библиотека и выбранный исходный ZIP, включая source вне saves и его предков; отсутствующий config имеет согласованную семантику. Не стирать установленное решение boolean или исключением при поздней ошибке.

Новая Linux original-name проверка хранит точные исходные ключи, проверяет схему/hash до finite fixed-path materialization, выбирает точный original entry или единственный case alias и требует one-to-one manifest claims. Одновременные произвольные `Pair.bin`/`pair.bin` сохраняются и точно откатываются; fixed/manifest collisions, ambiguous aliases и double claims отвергаются.

`TrustedLocalFilePublication` читает прежние v1/v2 и новые v3 namespace frames в одном private checkpoint. Нормальный `FileSystemManager.AcquireCanonicalWriteLeaseAsync` завершает pending rollback или committed cleanup только по самодостаточному журналу. Не удалять неизвестный blocker/journal и не расширять grants. Повторная recovery отдельно доказана cold controls; один resource restart сам по себе не доказывает повторную acquisition. Обещается process-crash recovery, не дополнительная power-loss durability.

## Следующая работа и явные открытые границы

1. Файловые B5-FS и точно сопоставленные B1/B2/B3 закрыты. Отдельный T031-WORKER-PORTABLE отслеживает фактический Linux kernel32.dll отказ в старой worker-transaction cleanup до вызова Load; worker/full-game qualification остаётся заблокированной. Продолжить **B4 public/console/browser integration** на этой же ветке: typed outcome/committed identity, generation rebind, pending/UI owner guards, required refresh failure и blocked uncertainty, affected caller tests
2. **T032-A4-NATIVE-NAMES остаётся открытым:** существующий save producer по исходникам отвергает произвольную Linux case-distinct пару. Loader проверен на корректном независимо дополненном manifested archive; полный producer → load → save round trip такой пары пока не принят. Исправить producer узким отдельным блоком, сохранив Windows collision policy и все manifest/format/budget контракты
3. T033/live console/browser/real GM, полный B5/T032, остальные platform-helper/interactive-GM задачи и вся #1553 остаются открытыми. Материализация ран #1536 не возобновлялась

Файловые изменения принадлежат клиенту и не добавляют GM-authored команду/поле/механику; no-update rationale для prompts/examples записан в плане. [Запись фактических операций и ошибок](recovery/evidence/load-linux-final-audit-20261004/operation-incidents.json) отделяет approval/network cancellations, git authentication и обычные test failures. Generic cybersecurity flag в этих запусках не наблюдался; причинная связь с историческим неопределённым сообщением не установлена, специальных screening-проб не выполнялось.
