# Быстрый запуск игры с авто-пинками ГМа

## Шаг 1. Открой окно ГМа и зарегистрируй его

Открой отдельное окно PowerShell и выполни:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Register_GM_CLI_Window.ps1
```

Это создаст файл:

```text
E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session\game_state\control\gm_cli_window_binding.json
```

## Шаг 2. В этом же окне запусти Codex

В том же окне PowerShell выполни:

```powershell
cd "E:\Games\The Book of Eternity Reborn"
codex -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox
```

Daemon будет работать с этим окном по binding-файлу, а не по меняющемуся заголовку.

## Шаг 3. Запусти daemon

Открой второе окно PowerShell и выполни:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Start_GM_Daemon.ps1 -AutoPaste
```

Это включит:

- слежение за `BookOfEternityClient\game_session\input\turn_request.json`
- авто-вставку и авто-Enter в зарегистрированное окно ГМа
- автоматическую генерацию session-local `game_state\control\CLI_Launch_Script.generated.md` под текущие пути этой машины
- bootstrap message с содержимым сгенерированного launch script
- авто-пинги ГМа при:
  - новом ходе
  - `validation_repair_request.json`
  - `terminal_protocol_failure_request.json`

По умолчанию автовставка использует `RightClick`.
Если вашей консоли нужен другой режим, можно явно выбрать:

```powershell
.\Start_GM_Daemon.ps1 -AutoPaste -PasteMode ShiftInsert
```

или

```powershell
.\Start_GM_Daemon.ps1 -AutoPaste -PasteMode CtrlV
```

Если автовставка всё равно срабатывает плохо, запускайте daemon без `-AutoPaste`: он будет копировать команды в буфер, а вы вставите их вручную.

## Шаг 4. Запусти игру

Открой третье окно PowerShell и выполни:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient"
dotnet run
```

## Подготовка следующего live-test хода без ручного JSON

Если нужно поставить следующий ход в очередь для живого теста, не собирайте `turn_request.json` и pending snapshot руками. Используйте launcher-команду:

```powershell
cd "E:\Games\The Book of Eternity Reborn"
.\BookOfEternityClient\Launcher\bookofeternity.ps1 -SessionPath "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session" prepare-turn --action "Надеть руническую перчатку и изучить письмо." --dice "14,8,17"
```

Команда создаёт согласованные `input\turn_request.json`, `game_state\control\pending_turn_snapshot.json` и `game_state\control\pending_turn_snapshot.authority.json`, нормализуя пути и исключая служебные bridge/daemon/harness артефакты.

Подготовка выполняется одной generation-bound транзакцией: клиент привязывает
операцию к текущей сессии до первого чтения, затем под одной канонической
блокировкой очищает прежние артефакты, снимает no-follow snapshot и публикует
manifest, authority и запрос хода. Параллельный Load или New Game либо ждёт
завершения этой транзакции, либо останавливает старую операцию через
`SessionReplaced`; артефакты старой сессии не попадут в новую. Поэтому не
собирайте и не очищайте эти файлы вручную.

## Самая короткая версия

Окно 1:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Register_GM_CLI_Window.ps1
cd "E:\Games\The Book of Eternity Reborn"
codex -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox
```

Окно 2:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Start_GM_Daemon.ps1 -AutoPaste
```

Окно 3:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient"
dotnet run
```

## Fallback-режим

Если binding почему-то не работает, daemon всё ещё можно запустить через заголовок окна:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Start_GM_Daemon.ps1 -CliWindowTitle "GM Codex" -AutoPaste
```
