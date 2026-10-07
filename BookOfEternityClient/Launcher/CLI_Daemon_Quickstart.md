# Быстрый запуск игры с авто-пинками ГМа

## Linux: bounded M1 path

M1 qualifies the ordinary launcher with a persistent **neutral configured CLI** in
isolated roots. Real Codex readiness/VT/provider/game turns remain Q1/Q2; this is
not a live-GM acceptance. Select `GmBridgeBackend=OwnedTerminal` and explicitly
`GmMainOwnerBackend=NativeLineage` in the game profile; keep its original CLI command,
model, arguments and cwd. Helpers remain disabled. `Auto`/`SystemdUser` do not
silently downgrade. A user systemd manager is not installed or qualified by M1.

Publisher preparation (SDK/compiler needed only here):

```powershell
pwsh -NoProfile -File scripts/build-linux-supervisor.ps1 -OutputDirectory out/native/linux-x64
dotnet publish BookOfEternityClient/BookOfEternityClient.csproj -c Release -o out/ship/BookOfEternityClient -p:BoeNativePackageDirectory=$PWD/out/native/linux-x64 -p:BoeRequireNativePackage=true
dotnet publish BookOfEternityGMBridge/BookOfEternityGMBridge.csproj -c Release -o out/ship/BookOfEternityGMBridge -p:BoeNativePackageDirectory=$PWD/out/native/linux-x64 -p:BoeRequireNativePackage=true
```

Keep both published directories together. The client includes Launcher, daemon,
unchanged operational docs/rules and built-in guardians; native helper+manifest
are prebuilt at `runtimes/linux-x64/native`. Preserve executable mode and package
provenance. Player startup needs .NET8 runtime, PowerShell7 and the measured
linux-x64/glibc ABI; it does not compile/download the helper or require the source tree.
The qualification used Debug publications; the commands above describe publisher
layout and do not claim production Release gameplay qualification.

Use three separate caller-supplied terminals and an admitted disposable root:

```powershell
# Terminal A: ordinary client, with its own stdin
 dotnet out/ship/BookOfEternityClient/BookOfEternityClient.dll /path/to/disposable-root
# Terminal B: foreground main terminal, manual keyboard belongs to this host
 pwsh -NoProfile -File out/ship/BookOfEternityClient/Launcher/bookofeternity.ps1 start-bridge visible -SessionPath /path/to/disposable-root/game_session
# Terminal C: foreground daemon, independent stdin
 pwsh -NoProfile -File out/ship/BookOfEternityClient/Launcher/bookofeternity.ps1 start-daemon visible --no-autopaste -SessionPath /path/to/disposable-root/game_session
```

Status reports `native-lineage` / `ordinary-same-namespace-lineage`: original
process lineage in the same PID namespace. It does not control external services.
T042 automatic submission requires the configured reliable idle view and an empty
draft; unknown results pause without replay. Unsupported VT/TUI, trust/access or
TERM prompts require separate qualification; do not auto-confirm or substitute TERM.
Confirmed scoped stop/disposal/Stopped ACK must precede restart. Uncertain owner,
worker inventory or storage debt keeps refusal. Accepted bounded Load UX requires
confirmed original stop + successful Load + current-owner refresh before a fresh
configured launch; uncertain/failed decisions pause without old-session continuation.
Linux browser rollback/gacha/Daren portability is accepted in isolated automated
checks; native Windows and live GM remain separate unqualified environments.
Windows instructions below retain their existing route.


## legacy Windows/source recipe

Ниже сохранены отдельные Windows/source инструкции. Они не задают Linux packaged
startup или prerequisites команды prepare-turn; текущий M1 путь описан выше.
Старая window/clipboard автовставка не заменяет принятый T042 paste/observe/submit.

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

Это действующая auxiliary-команда существующего launcher. В установленном или
перенесённом package она запускает `BookOfEternityClient.dll`; нужны PowerShell 7
и прежние shared runtimes Microsoft.NETCore.App 8 / Microsoft.AspNetCore.App 8.
Launcher, `gm_main_operation.ps1`, DLL, deps и runtimeconfig поставляются издателем.
Нет необходимости в checkout, SDK или компиляторе у игрока. Отсутствующий ресурс
или runtime означает точный отказ до подготовки; автоматической установки нет.

Linux, из любого текущего каталога:

```powershell
pwsh -NoProfile -File /opt/boe/BookOfEternityClient/Launcher/bookofeternity.ps1 -SessionPath "/home/player/boe-game/game_session" prepare-turn --action "Надеть руническую перчатку и изучить письмо." --dice "14,8,17"
```

Windows, тот же поставляемый launcher:

```powershell
pwsh -NoProfile -File "C:\Games\BOE\BookOfEternityClient\Launcher\bookofeternity.ps1" -SessionPath "C:\Games\MyGame\game_session" prepare-turn --action "Надеть руническую перчатку и изучить письмо." --dice "14,8,17"
```

Корень игры должен быть существующим и подготовленным клиентом. `-SessionPath`
указывает его `game_session`; тексты/каталоги в `--action` не меняют этот корень.
Команда не запускает настроенный CLI, не меняет команду/модель/аргументы профиля,
не вставляет и не отправляет запрос ГМу. Linux не требует регистрации окна.
Без активного ГМа используется существующее локальное admission; при активном
ГМе нужен original participating pin. Cold nonterminal, worker или storage debt
не обходятся; отказ не является поводом очищать запись или повторять неизвестный ход.

Команда создаёт согласованные `input/turn_request.json`,
`game_state/control/pending_turn_snapshot.json` и
`game_state/control/pending_turn_snapshot.authority.json`. Подготовка сохраняет
существующую generation-bound транзакцию, canonical lease, no-follow snapshot
и исключения служебных bridge/daemon/harness артефактов. Load/New Game не получает
артефакты старой generation; не собирайте и не очищайте эти файлы вручную.

Действующие bounded Load/browser/clipboard/audio результаты и ограничения — в
[repository roadmap](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/codex/1553-load-filesystem/specs/1553-portable-local-storage/roadmap-after-daren.md) и
[repository auxiliary plan](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/codex/1553-load-filesystem/specs/1553-portable-local-storage/auxiliary-launcher-portable-plan.md).
Load UX принят отдельным срезом: подтверждённые stop + Load + refresh предшествуют
fresh launch; неопределённость не продолжает старую сессию. Эти helper-инструкции
не объявляют live GM, физический audio, desktop clipboard или native Windows проверенными.

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
