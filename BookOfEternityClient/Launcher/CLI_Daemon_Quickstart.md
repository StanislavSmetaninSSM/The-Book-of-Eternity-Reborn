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

## Шаг 2. В этом же окне запусти Gemini

В том же окне PowerShell выполни:

```powershell
cd "E:\Games\The Book of Eternity Reborn"
gemini
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
- автоматическую генерацию `Launcher\CLI_Launch_Script.md` под текущие пути этой машины
- bootstrap message с указанием прочитать `Launcher\CLI_Launch_Script.md`
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

## Самая короткая версия

Окно 1:

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\Launcher"
.\Register_GM_CLI_Window.ps1
cd "E:\Games\The Book of Eternity Reborn"
gemini
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
.\Start_GM_Daemon.ps1 -CliWindowTitle "GM Gemini" -AutoPaste
```
