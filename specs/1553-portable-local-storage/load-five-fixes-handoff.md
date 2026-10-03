# Передача файлового контура обычной загрузки — #1553

Дата: 2026-10-04. Репозиторий: [The Book of Eternity Reborn](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn), задача [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).

Рабочая ветка: **`codex/1553-load-filesystem`**. База — `5d2aa2ceadd8f4424e3ccaf0249a8bb164f32fae`; принятые сохранения — `ddaade72ae44936f6cf61970afb2bc80225b7731`. Изменения не влиты в default branch. Для продолжения получить актуальный полный SHA ветки, сравнить удалённый ref и чистоту checkout; не начинать от старого `bd5cb827` или применять исторические пакеты.

## Прочитать и восстановить

```sh
git clone --single-branch --branch codex/1553-load-filesystem https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git boe
cd boe
git rev-parse HEAD
git ls-remote origin refs/heads/codex/1553-load-filesystem
git status --short
```

Исходники, каталог и доказательства — обычные Git blobs. Сверить SHA, затем прочитать AGENTS.md, docs/development-workflow.md, docs/testing.md, [spec.md](spec.md), [ordinary-load-plan.md](ordinary-load-plan.md), текущий checkpoint [plan.md](plan.md) и [tasks.md](tasks.md). Спецификация LOAD-FS-001…008 и v3 согласованы; владелец прямо разрешил автономные рекомендуемые ревизии и отменил дальнейшее ожидание утверждения. Независимое ревью остаётся обязательным: отдельный **gpt-6.1-sol / xhigh**, согласно актуальному правилу владельца.

## Что готово и чем подтверждено

| Блок | Код и native Windows | Сохранённые доказательства |
| --- | --- | --- |
| T032-B1-FIX | Пять исходных исправлений; корректная фикстура Shining Abode; фиксированные пути взяты из явных runtime-реестров, произвольные имена не приводятся к одному регистру | [План](plan.md), отдельные 24 alias / 9 entry / 7 admission / 4 outcome / 9 lease case результатов; независимый PASS через `60e539cf` |
| T032-B1-METADATA | Потоковый v2 codec без общего лимита 1 МиБ; строгие поля/области и проверка всего frame до записи; v1 сохранён | [39 metadata](recovery/evidence/load-metadata-green/summary.json), [41 compatibility](recovery/evidence/load-metadata-consumers/summary.json), [каталог/XML](recovery/evidence/load-metadata-catalog/summary.json); независимый PASS через `f822ef93` |
| T032-B2-NAMESPACE | Подключённый v3: файл ↔ каталог, точные защищённые границы, полный preflight, восстановление/удаление generation последним, cleanup без отката commit | [48 namespace](recovery/evidence/load-namespace-green/summary.json), [native/consumer](recovery/evidence/load-namespace-consumers/summary.json), [исправленный dispatch 4](recovery/evidence/load-namespace-dispatch-green/summary.json), [каталог/XML](recovery/evidence/load-namespace-catalog/summary.json); независимый PASS через `d229823a` |
| T032-B3-COLD | Реальные process cuts и journal-only restart без распаковки, обе конверсии, неизвестный поздний файл/пустой каталог, неполный scratch, повторное восстановление | [33 cases: new 29 + dispatch 4](recovery/evidence/load-cold-green/summary.json), [каталог/XML](recovery/evidence/load-cold-catalog/summary.json); независимый PASS через `3d20be0a` |
| T032-B3-RESOURCE | Подготовка отдельных preparation/publication/recovery qualification; до появления фактических результатов этот блок не принят | Текущий статус и source-bound результаты добавляются в [plan.md](plan.md) |

Сохранённые результаты относятся к указанным источникам и отдельным выбранным когортам, не к одному общему прогону. Не повторять успешные неизменённые Windows-когорты только для передачи. **Native Linux нового load-контура ещё не проверен.** Ранее принятые Linux-сохранения не заменяют эту проверку.

## Callable boundary для подключения клиентов

Подготовленный контур вызывается внутренним `SaveLoadService.LoadGameWithOutcomeAsync(string, CancellationToken)`, возвращающим `LoadReplacementResult`. Публичный старый `LoadGameAsync` ещё не переведён на него. Следующая работа Астры — согласованный B4 после необходимых filesystem gates: подключить публичную загрузку и её console/browser-потребителей к типизированному результату, сохранив существующие caller contracts.

- `NotLoaded`: замена не начата; приватная ошибка подготовки/очистки может требовать follow-up, сама по себе не блокирует каноническую работу.
- `Committed`: новый generation подтверждён. Поздняя ошибка refresh/log/release/cleanup не отменяет состоявшуюся загрузку.
- `RolledBack`: точные старые bytes/absence и прежний generation подтверждены.
- `Uncertain`: established generation отсутствует, follow-up обязателен, продолжение канонической работы заблокировано до разрешения evidence.

Runtime обновляется только после подтверждённого commit. Связанный с тем же root вызов запрещён до подготовки; detached settings/profile preparation не публикует runtime заранее. Полная библиотека, выбранный исходный ZIP и его предки защищены, в том числе source вне saves. Отсутствующий config сохраняет согласованную семантику. Не подменять результат исключением/boolean, которое стирает установленное решение.

`TrustedLocalFilePublication` читает прежние v1/v2 file-image и новые v3 namespace frames; старые handler/authority boundaries сохранены. Новый typed load пишет v3 в существующий private checkpoint. Нормальный `FileSystemManager.AcquireCanonicalWriteLeaseAsync` завершает pending rollback или committed cleanup по самодостаточному журналу. Не удалять неизвестный blocker/journal и не расширять grants, чтобы восстановление «прошло». Для нового UI-кода обычно не требуется править ZIP/parser/reconcile/recovery-модуль; подтверждённый дефект в нём оформляется отдельным узким изменением.

## Linux qualification и пределы

Использовать native Linux, PowerShell 7, SDK 10 и runtime 8. Инструкции среды — [quickstart.md](quickstart.md). До запуска инструментов выставить process-local telemetry opt-outs; `DOTNET_PROCESSOR_COUNT=1` — записанная настройка сборки. Никаких полных прогонов или Fast/PreMerge. Свежая сборка нужна перед `-NoBuild`.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:POWERSHELL_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_PROCESSOR_COUNT = '1'
./scripts/test-csharp.ps1 -SelectionFile tests/selections/1553-load-linux.json -Parallelism 1 -TimeoutMinutes 15
```

Это первая существующая когорта допуска/исходов/затронутых lease consumers. Следующие когорты выбирать по изменённой границе и каталогу: metadata + затронутые v1/v2 consumers, namespace/topology/native/cleanup, cold decision/topology. Это ещё не выполненная Linux-проверка и не инструкция последовательно обходить весь каталог. Ресурсные фазы запускаются **отдельными командами**, по четыре независимые фикстуры каждой: 64/128/почти 512 МиБ и 8 192 записи с почти 2 МиБ UTF-8 имён / 9 216 старыми файлами. Точные команды и бюджеты — [ordinary-load-plan.md](ordinary-load-plan.md#t032-b3-resource--actual-envelope-by-phase).

Load-only envelope: 768 МиБ heap, 1 ГиБ RSS, 5 ГиБ owned disk, 180 секунд на child; категория 10 минут, команда 15. Save-only лимиты и архивные ceilings не изменены. Guard stop, неполная фаза, OS early return, setup/cleanup failure не являются PASS. Сохранять ОС/SHA/toolchain/actual counts/cleanup и probe reports.

Bounded Linux workflow и selection уже опубликованы. Repository Actions разрешены только для трёх необходимых actions; прежний general workflow остался disabled. Новая workflow не зарегистрировалась и actual run отсутствует; причина не установлена. Не делать merge solely для активации CI. Если обычный Linux executor недоступен, native gate остаётся открытым; не выдавать исходники или Windows execution за Linux PASS.

## Что остаётся

1. Закончить resource qualification и независимое ревью локального блока; сохранить доказательства и проверить восстановление GitHub.
2. Native Linux filesystem qualification и закрытие соответствующей части T032-B5-FS.
3. B4: public/console/browser load integration и affected caller tests, затем T033/live/full B5 и остальные открытые задачи #1553.

Материализация ран #1536 не возобновлялась. Client-owned filesystem changes не добавляют GM-authored поле/команду/механику: игровые prompts/examples не менялись, no-update rationale записан в плане. Вся #1553 и вся загрузка пока не объявляются завершёнными.

## Историческая точка остановки

`bd5cb827318e849681a848a57016d62740a6e5ea` содержал первые 9 GREEN, пять ещё не внесённых исправлений и поправленную, но не проверенную Shining Abode fixture. Это исторический baseline. Сообщение платформы «possible cybersecurity risk» не назвало конкретную операцию; архивная работа остаётся гипотезой, причинной связи не установлено. Текущий модуль разработан и проверяется обычными локальными средствами.
