# Привязка `game_master_daemon.ps1` к окну CLI

Старый способ через `-CliWindowTitle` оставлен как fallback, но основной рекомендуемый путь теперь другой:

- daemon работает через `gm_cli_window_binding.json`
- binding регистрируется один раз отдельным скриптом
- после этого изменение заголовка окна больше не ломает автопинг

## Основной рекомендуемый способ

### 1. Зарегистрируй окно

Открой окно PowerShell, которое будет использоваться под CLI, и выполни:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Register_GM_CLI_Window.ps1
```

Скрипт сохранит:

- PID окна
- HWND окна
- текущий заголовок
- время регистрации

в файл:

```text
E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session\game_state\control\gm_cli_window_binding.json
```

### 2. В этом же окне запусти CLI

```powershell
cd "E:\Games\The Book of Eternity Reborn"
codex -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox
```

### 3. Запусти daemon wrapper

В другом окне:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Start_GM_Daemon.ps1 -AutoPaste
```

Wrapper автоматически создаёт session-local `game_state\control\CLI_Launch_Script.generated.md` с актуальными путями этой машины перед стартом daemon.
По умолчанию автовставка использует `RightClick`.
Если вашей консоли нужен другой режим, можно попробовать:

```powershell
.\Start_GM_Daemon.ps1 -AutoPaste -PasteMode ShiftInsert
```

или

```powershell
.\Start_GM_Daemon.ps1 -AutoPaste -PasteMode CtrlV
```

## Почему это лучше, чем `CliWindowTitle`

Старый способ искал окно так:

```powershell
Get-Process | Where-Object { $_.MainWindowTitle -match $CliWindowTitle } | Select-Object -First 1
```

Это ломается, если CLI меняет заголовок окна на:

- `Processing`
- `Ready`
- другие временные состояния

Новый binding-based способ опирается не на текущий title, а на зарегистрированный PID/HWND окна.

## Когда нужно перерегистрировать окно

Повторно запускай `Register_GM_CLI_Window.ps1`, если:

- ты закрыл окно CLI и открыл новое
- окно было пересоздано
- daemon пишет, что binding stale/invalid

## Fallback по заголовку

Если binding временно не используется, можно запустить daemon с заголовком:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Start_GM_Daemon.ps1 -CliWindowTitle "GM Codex" -AutoPaste
```

Но это именно fallback, а не рекомендуемый основной режим.

## Clipboard-only режим

Если не хочешь автопасту:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Start_GM_Daemon.ps1
```

Тогда daemon:

- всё равно сможет использовать binding для диагностики
- будет копировать команды в clipboard
- не будет сам пытаться вставлять и нажимать `Enter`

## ConPTY bridge readiness

При использовании `GmBridgeBackend = ConPTYBridge` флаг `Ready` означает, что
bridge можно просить принять новый prompt. Перед фактической вставкой bridge
дополнительно очищает незавершённую строку ввода Codex CLI и проверяет видимый
экран PTY.

Если Codex CLI всё ещё выполняет старый запрос (`Working ... esc to interrupt`)
или ждёт подтверждения доверия к директории, bridge не вставляет ход игрока и
переводит статус в `DispatchFailed` / `Ready = false`. Это защита от ситуации,
когда новый ход попадает в активный экран Codex вместо новой задачи ГМа.

Диагностика:

```powershell
cd "E:\Games\The Book of Eternity Reborn"
.\BookOfEternityClient\Launcher\bookofeternity.ps1 diagnostics
```

После того как Codex CLI снова ждёт ввод, можно отметить bridge готовым:

```powershell
.\BookOfEternityClient\Launcher\bookofeternity.ps1 ready
```

## GM turn helper

Daemon создаёт для каждой активной сессии bootstrap-файл:

```text
game_state\control\gm_turn_helper.bootstrap.ps1
```

ГМ может dot-source-ить его в Codex CLI:

```powershell
. "...\game_state\control\gm_turn_helper.bootstrap.ps1"
```

После этого доступны функции:

- `Write-BoeJson -RelativePath "output/narrative_response.json" -Data <object>` — безопасная UTF-8 запись JSON внутри текущей `game_session`.
- `Complete-BoeTurn -FilesModified @("output/narrative_response.json")` — пишет `ready/turn_complete.json` с точными `sessionId/requestId/turnNumber` из текущего `input/turn_request.json`.
- `Complete-BoeTurn` и `Fail-BoeTurn` требуют, чтобы текущие `game_state/control/pending_turn_snapshot.json` и `game_state/control/pending_turn_snapshot.authority.json` всё ещё существовали и совпадали с `input/turn_request.json`. Если pending authority уже исчезла, helper падает и не пишет stale terminal signal.
- `Fail-BoeTurn -ErrorMessage "..."` — пишет `ready/turn_error.json` с той же корреляцией.
- `Complete-BoeValidationRepair` — пишет `game_state/control/validation_repair_ready.json` из текущего `validation_repair_request.json`.

Если активный `turn_request.json`, pending snapshot authority или repair request уже исчезли, helper падает с понятной ошибкой и не пишет stale terminal signal.
Helper также отклоняет запись и `filesModified` для client-owned runtime-файлов: `input/turn_request.json`, `game_state/history/chat_log.json`, pending-turn snapshots, `validation_repair_request.json`, `terminal_protocol_failure_request.json`, `gm_bridge_status.json`, `stories/*.jsonl`.
Когда `game_state/meta/soul_state.json.currentRealm` равен `Chaos Sea` или `Shining Abode`, helper дополнительно отклоняет wrong-realm записи и `filesModified` для Mortal World profile путей: `game_state/world/`, `game_state/npcs/`, `game_state/factions/`, `game_state/player/`, `game_state/inventory/`, `game_state/combat/`, `game_state/quests/`.
Rollback backup artifacts с фрагментом `.rollback.` в имени файла не считаются canonical profile mutation и не должны указываться в `filesModified`; настоящие JSON-файлы профиля Mortal World по-прежнему сравниваются с pending snapshot и блокируются при semantic mutation.
`input/turn_request.json` нельзя удалять или переписывать из daemon/GM-скрипта: клиент использует его как authority до принятия, отклонения или отмены хода.

## GM context pack

Daemon также создаёт session-local пакет контекста:

```text
game_state\control\gm_context_pack\
  README.md
  context_pack_manifest.json
  TaskGuides\...
  Examples\...
  OtherGuides\...
```

Пакет содержит только GM-facing документы и примеры, нужные для текущей live-сессии. Prompt daemon указывает ГМу начинать с `context_pack_manifest.json` и не читать implementation code вроде `BookOfEternityClient/**/*.cs` во время обычного хода или repair.

Bootstrap-сообщение daemon больше не вставляет полный `CLI_Launch_Script.md` в Codex. Оно только указывает на session-local context pack, `gm_turn_helper.bootstrap.ps1` и режим ожидания настоящего per-turn/repair prompt. Это снижает шанс, что ГМ уйдет читать repo-root документы и планы вместо текущего игрового состояния.

Если ГМу не хватает правила, правильный путь такой:

- текущий `input\turn_request.json`;
- `game_state\control\validation_repair_request.json` и `harnessRepairPackets[]`, если это repair;
- session-local `gm_turn_helper.bootstrap.ps1`;
- документы и примеры из `gm_context_pack`.

## Daemon status and live-test cleanup

Daemon пишет session-local диагностический файл:

```text
game_state\control\gm_daemon_status.json
```

В нём есть `pid`, `sessionPath`, `startedAtUtc`, `heartbeatAtUtc`, `turnCount` и `errorCount`.
При старте daemon проверяет этот файл и не запускает второй активный daemon для той же `game_session`.
Если запуск остановился с сообщением `GM daemon already running for this game_session`, сначала останови процесс с указанным `pid` или убедись, что процесс уже умер, и только затем запускай daemon снова.

Daemon-owned timeout для live-test помечается как `harnessSource = "gm_daemon_timeout"`.
Если после такого timeout появляется настоящий `ready/turn_complete.json` с той же `sessionId/requestId/turnNumber`, daemon удаляет свой stale timeout artifact и принимает успешный ответ, чтобы harness timeout не превращался в ложный terminal protocol conflict.
