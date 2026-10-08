# Windows: проверка merged PR #1555, 2026-10-08

Задача: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), проверка после [PR #1555](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1555).
Проверенная версия продукта: **`6381a9507baae6bb2531e22e9a0ace839f03985f`**.
Отчётная ветка: `codex/1553-windows-verification-20261008`.

Статус публикации: **отчётный пакет проверен независимым GPT-6 Astra XHigh, PASS без обязательных исправлений**. Проверенный содержательный commit: `26b3a0251ef0200ec224cfe4797648b4ec23f328`; текущая запись добавляет результат ревью и проверки восстановления. Это сохранение уже выполненной проверки, а не исправление продукта и не общий Windows PASS. В этой ветке добавлены только отчёт и ограниченный набор свидетельств. После принятия результатов пользователь отдельно одобрил дизайн Windows-relay и исправление HTTP 500 (подтверждение передано родителем 2026-10-08); эти ремонты в отчётную ветку не входят. Main, CI, runtime и игровые данные не меняются этим пакетом.

[Полный исходный handoff](HANDOFF.md) сохранён с заменой собственного временного корня на `<RUN_ROOT>`. Имена исходных локальных журналов в нём не означают, что все эти журналы опубликованы: публичный набор перечислен ниже. Полные CLI-транскрипты, личные профили, credentials, содержимое сохранений, node_modules, артефакты сборки и большие discovery-логи не включены.

## Итог и границы

Нативная среда HOME-PC: Windows 11 Pro x64 `10.0.26200`, PowerShell `7.6.6`, SDK `.NET 10.0.401`, runtime `.NET 8.0.31`, Git `2.55.0.windows.5`, Node `24.21.0`, npm `11.19.0`, Python `3.14.7`, Codex `0.154.0`, Chrome `154.0.8037.99`. WSL не использовался. C# build завершился без ошибок (22/29 предупреждений в integration/unit), frontend typecheck/Vite build прошёл. Зависимости восстановлены в отдельном рабочем каталоге.

**76 уникальных случаев: 67 реально выполненных PASS, 5 Windows no-op, 4 FAIL.** xUnit сообщает 72 PASS, поскольку пять FIFO theory rows сразу возвращаются на Windows. Всего было 80 выполнений: один четырёхтестовый набор повторён неизменённым для воспроизведения. Это не 80 уникальных тестов и не 72 реально выполненные Windows-проверки.

| Прогон | Запланировано | Выполнено | PASS xUnit | FAIL | Значение |
|---|---:|---:|---:|---:|---|
| foundation-plan | 54 | 0 | 0 | 0 | Только сборка/PlanOnly |
| foundation-run | 54 | 4 | 3 | 1 | Остановка после первого отказавшего descriptor |
| browser-repro | 4 | 4 | 3 | 1 | Повтор той же ошибки |
| storage-run | 50 | 44 | 42 | 2 | Остановка после handle descriptor |
| paths-run | 6 | 6 | 5 | 1 | Оставшиеся случаи первоначального отбора |
| workflow-plan | 22 | 0 | 0 | 0 | Только PlanOnly |
| workflow-run | 22 | 22 | 22 | 0 | Полностью завершён |

Первоначальные 54 случая в совокупности выполнены полностью, но не все прошли. Пять no-op — `PortableClientStorageTests.IntegratedLocalReaderRejectsFifoBeforeOpeningWithBoundedChildCleanup`; это отдельная оговорка поверх счётчиков runner. [Все случаи и исходные TRX-пути](evidence/test-case-index.json), [сводка с длительностями/cleanup/SHA](evidence/runner-summary-index.json), [уникальные итоги](evidence/unique-test-totals.json).

Обычная консоль была запущена через настоящий Windows PTY без e2e/agent флагов: QTE true→false, выход 0, полный перезапуск, QTE false, выход 0; config/projection hashes не изменились при перезапуске. Нативный GM Bridge подтвердил ConPTY input/output, новый terminal run при `restartshell`, завершение старого shell и его тестового потомка через исходный Windows Job, затем `shutdown` с `stopped-within-scope`, `cleanupComplete=true`, `authorityRetained=false`. Старые PID исчезли; host exit 0. Перенаправленный stdout-вариант не подтвердил bootstrap marker за 15 s; успешный результат относится именно к обычному PTY. Bridge запускает pwsh без `-NoProfile`, поэтому это не проверка profile-free shell.

Минимальный Codex-запрос через этот же терминал вернул [маркер](evidence/codex-native-live-answer.txt) и [exit 0](evidence/codex-native-live-exit.txt). Это **только связь с установленным CLI**, а не принятый игровой ход, новая игра или T042 readiness. Bridge оставался `OperatorNotReady`; поддержка автоматического профиля ввода не заявляется.

Public HTTP Save и Load на копии репозиторного fixture получили `Committed`, полный bundle и `NoActiveSession`. Новый синтетический ZIP имел 46 181 байт, SHA256 `11CF41AA9F3D9592AAFEBD28A489D2ED48EDB8496656229B336657045706B567`; сам ZIP не публикуется. После перезапуска сервера API и UI показывали масштаб 110%. Лишний диагностический `load-complete` после уже завершённого no-active-GM Load был отклонён HTTP 409; это не успешный follow-up и не дефект продукта. [Ограниченные наблюдения операций](evidence/operational-observations.json) не содержат payload сохранений/модельных сессий.

Полноценная сгенерированная новая игра → принятый ход → save/restart/load → продолжение, Load с активным GM, физическое воспроизведение звука, clipboard, видимый desktop-браузер и Linux/systemd S2/S3 **не квалифицированы**. Clipboard не читался и не перезаписывался. Аудио не проигрывалось; локальных assets в тестовом корне не было.

## Четыре FAIL в тестах

[Исходные сообщения/stack traces из пяти отказавших выполнений](evidence/failed-test-errors.json) сохранены отдельно от HTTP-дефекта. Ошибки ниже нельзя выдавать за stack traces HTTP 500.

1. `BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ConcurrentReplacementWaitsForCompleteTransaction`: дважды timeout на `BrowserLocalWriteCoordinatorTests.cs:2107`. Первая запись и ожидание `transactionStarted` на 2105 завершились. Fixture ждёт `CanonicalWriteLockContendedAsync`, но retained main admission задерживает replacement раньше, на `MainOwnerLockContendedAsync`. См. `BrowserLocalWriteCoordinator.cs:241–247`, `SessionOperationContext.cs:113–130`, `FileSystemManager.cs:3834–3840,5944`, `FileSystemManager.MainRunFence.cs:70–71`.
2. `FileSystemManagerTests.AcquireCanonicalWriteLease_RejectsOpenedExternalLockHandleAfterPathSwap` и `AcquireSessionLifecycleLease_RejectsOpenedExternalLockHandleAfterPathSwap`: fixture `Directory.Move` на `FileSystemManagerTests.cs:6029` выдаёт `IOException`/access denied, а тест ожидает `InvalidDataException`. Уже удерживаемый `gm-main-owner.lock` открыт с `FileShare.None` (`GmSessionRunPersistence.cs:123–129`); замена каталога ссылкой на 6030 не достигнута.
3. `PortableSessionRootKeyTests.WindowsAliasBoundWriterCannotMutateTheReplacementSessionBeforeFinalFence`: replacement на строке 110 исчерпывает ожидание того же main-owner guard. Тест удерживает исходную операцию на 93–96 и освобождает её только позже, на 116; ожидаемая замена/проверка stale writer не выполнена.

Независимый source-review `/root/review_windows_selection` подтвердил несовместимость этих предположений fixtures с новым порядком admission. Это объяснение отказов, **не** доказательство прохождения задуманных сценариев. Исправлений и повторов с изменённым продуктом не было.

## Отдельный дефект обычного браузера: HTTP 500

Chrome действительно отрисовал меню и основные настройки, но при загрузке страницы наблюдались ошибки API и сообщение об ошибке музыки. Отсутствие JavaScript exception и exit 0 диагностического скрипта не означают PASS интерфейса. [Оригинальные Network responses и наблюдения контролов](evidence/browser-observations.json).

После этого тот же сбой воспроизведён **без Chrome**, пятью одновременными GET к существовавшему loopback-серверу:

| Точный путь | HTTP | Время, ms | Полученное тело ошибки |
|---|---:|---:|---|
| `/api/main-menu` | 200 | 13241 | — |
| `/api/session` | **500** | 12268 | Пустая строка |
| `/api/game-screen` | **500** | 12265 | Пустая строка |
| `/api/audio/settings` | **500** | 12267 | Пустая строка |
| `/api/client/settings` | 200 | 13423 | — |

[Неизменённые значения воспроизведения](evidence/http-concurrent-read-results.json). Одиночный GET `/api/audio/settings` между UI-прогонами возвращал 200 и каталог без assets. Поэтому отсутствие звуковых файлов не объясняет записанные HTTP 500. Дополнительно наблюдался отдельный `/favicon.ico` 404.

**Исходный exception/stack trace для HTTP 500 не собран.** Сервер вернул пустые тела; `LocalWebUiHost.cs:42` очищает logging providers, отдельного server log в данном тестовом корне не оказалось. Нельзя приписывать этому дефекту исключения четырёх тестов. Причина пока не установлена. При публикации ничего не запускалось ради получения нового stack trace и не включался диагностический режим.

Точная последовательность состояния перед зафиксированным воспроизведением:

1. На проверенном SHA собраны клиент и frontend. В новый `<RUN_ROOT>/data/ordinary-web` скопированы **репозиторные** `FileSystemExample/game_session` и `BookOfEternityClient/system_guardians`.
2. В test-only config: русский язык, музыка/звук выключены, bridge/auto-start выключены, worker profiles выключены, scene images выключены, QTE false. Config перенесён из отдельного прошедшего console-settings теста, а не из пользовательского профиля.
3. Из корня checkout запущен `dotnet BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll <RUN_ROOT>/data/ordinary-web --web --web-url http://127.0.0.1:61719`.
4. Через HTTP установлен `browserFontScalePercent=110`; выполнены public Save и Load этого нового синтетического сохранения без активного GM. Уже завершённый Load дополнительно получил отклонённый `load-complete` (409).
5. Сервер остановлен Ctrl+C и запущен той же командой/корнем заново. Страница открыта в новом изолированном Chrome profile. Network responses дали три 500. После закрытия Chrome те же пять GET выполнены параллельно ещё раз и дали таблицу выше.

Этот пакет **не устанавливает**, воспроизводится ли ошибка до Save/Load на совершенно новом корне: такое сокращение сценария не проверялось. Генерации/PID/времена/имя ZIP при повторе естественно будут другими. Не использовать существующие сохранения пользователя для воспроизведения.

Существенная часть фактически выполненного read-only Node-пробника (команды здесь только документированы, **не повторялись при публикации**):

```javascript
const base = 'http://127.0.0.1:61719'; // original owned test listener, now stopped
const routes = ['/api/main-menu', '/api/session', '/api/game-screen',
                '/api/audio/settings', '/api/client/settings'];
const results = await Promise.all(routes.map(async route => {
  const started = Date.now();
  try {
    const response = await fetch(base + route, {signal: AbortSignal.timeout(45000)});
    const body = await response.text();
    return {route, status: response.status, elapsedMs: Date.now() - started,
            body: response.ok ? undefined : body};
  } catch (error) {
    return {route, error: String(error), elapsedMs: Date.now() - started};
  }
}));
```

## Relay и воспроизведение отбора

Исходный native Windows relay: `C:\Python314\python.exe -B tools/gm-relay/relay_cli.py --help` → exit 1, `ModuleNotFoundError: No module named 'termios'`, строка 6. [Исходный traceback](evidence/relay-native-help.stderr.txt). Это отдельная совместимость developer relay; успешный ConPTY-мост не исправляет Python relay. Ремонт в этом пакете не выполнялся.

Runner-команда из checkout: `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile <selection> -Parallelism 1`. Полный набор причин/контрактов сохранён в [selections](selections/):

- `selection-windows-foundation.json`: сначала `-PlanOnly` со сборкой, затем исполнение с `-NoBuild`.
- `selection-windows-browser-repro.json`: один неизменённый повтор с `-NoBuild`.
- `selection-windows-storage.json`, `selection-windows-paths.json`: ещё не завершённые части первоначального отбора, с `-NoBuild`.
- `selection-windows-workflow.json`: `-PlanOnly -NoBuild`, затем `-NoBuild`.

Свежие успешные builds всех выбранных проектов были получены до `-NoBuild`; product sources не менялись. Process-only среда: telemetry opt-outs, `DOTNET_PROCESSOR_COUNT=2`, отдельные temp/NuGet/XDG/DOTNET_CLI_HOME под `<RUN_ROOT>`, `core.longpaths=true`, `core.autocrlf=false` для Git. HOME/CODEX_HOME не переопределялись. `npm ci --prefix BookOfEternityClient.WebFrontend --cache <owned-cache> --no-audit --no-fund`, затем `npm run build --prefix BookOfEternityClient.WebFrontend`. Полные suites/Fast/PreMerge/Linux-fixture substitutes не запускались.

## Очистка, происхождение и продолжение

[Проверка очистки в 03:46:42 UTC](evidence/final-cleanup.json): собственного web listener, записанных native host/shell/child PID и Chrome с собственными profile roots больше нет. Runner сообщил полный owned-process cleanup во всех завершённых запусках. Две fixture-директории после browser timeout оставлены локально как свидетельства; это не живые процессы. Никто не завершался по имени процесса. Исходный detached checkout чист; report worktree отдельный.

[source-provenance.json](evidence/source-provenance.json) хранит SHA256 исходных локальных evidence-файлов. Опубликованные JSON являются ограниченными проекциями либо перезаписанным JSON с заменой собственного корня; их байты не выданы за исходные файлы. Сообщения/stack traces тестов извлечены из исходных TRX, рядом записаны их хэши. [manifest.json](manifest.json) хэширует опубликованный набор; он не доказывает повторное выполнение тестов.

Этот архивный блок не меняет runtime/test contracts, поэтому новые C#/frontend/игровые прогоны не нужны и прямо не разрешены текущим поручением. Проверки публикации ограничены чтением/сверкой свидетельств, diff, хэшей, безопасного состава, независимым review и восстановлением отчётных файлов из GitHub. Commit SHA отчёта отличается от указанного SHA проверенного продукта.

Независимый ревьюер `/root/review_windows_selection` (GPT-6 Astra, XHigh) сверил 20 файлов архивного diff, все 19 payload-хэшей manifest, 26 исходных хэшей, 80 записей выполнения с 11 исходными TRX и семь runner summaries. Отдельно проверены границы HTTP 500, Windows no-op, Codex connectivity и неподтверждённые игровые сценарии. Вердикт для `26b3a0251ef0200ec224cfe4797648b4ec23f328`: PASS, обязательных исправлений нет; новых runtime-прогонов в ревью не было.

Восстановление этого же commit проверено прямым `git clone --filter=blob:none --no-checkout --depth 1 --single-branch --branch codex/1553-windows-verification-20261008` из GitHub в новый пустой каталог, затем sparse checkout инструкций и данной отчётной директории. Локальные Git-объекты и старые исходники не копировались. HEAD совпал с удалённым ref, checkout чист, все 19 payload-хэшей и размеров manifest совпали. Это проверка восстановления **отчётных файлов**, а не полной сборки или повторного запуска продукта. SHA итоговой записи метаданных и результат её отдельной удалённой сверки передаются в handoff после push.

Следующий шаг после публикации — **отдельный одобренный Windows-relay этап, затем отдельный HTTP 500 bugfix**, каждый со своей проверкой и checkpoint. Исходный отчёт не заменяет доказательств исправления. Merge, изменение main, CI, security/auth settings, удаление веток и закрытие issue не разрешены.
