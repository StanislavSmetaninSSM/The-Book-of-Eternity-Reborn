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
codex --dangerously-bypass-approvals-and-sandbox
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
