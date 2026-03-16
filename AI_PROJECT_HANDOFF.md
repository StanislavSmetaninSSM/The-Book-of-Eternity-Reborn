# AI Project Handoff

## Кратко

Это файловый клиент ролевой игры **The Book of Eternity Reborn** на `C# / .NET 8`, где GM работает через файловый протокол:

- клиент пишет `game_session/input/turn_request.json`
- GM пишет `output/*.json` и один terminal signal:
  - `ready/turn_complete.json`
  - или `ready/turn_error.json`

Источник истины по контракту:

1. [CLI_API_Specification.md](E:/Games/The%20Book%20of%20Eternity%20Reborn/CLI_API_Specification.md)
2. [Rules](E:/Games/The%20Book%20of%20Eternity%20Reborn/Rules)
3. runtime клиента в [BookOfEternityClient](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient)

На момент этого handoff:
- статический `Rules vs Code` аудит по проекту в целом считается закрытым на хорошем рабочем уровне
- основные системы уже синхронизированы между `runtime / validator / rules / examples`
- идёт следующий этап: **улучшение UX валидатора и дальнейшая полировка клиентского опыта**

## Что уже сделано

### 1. Основной lifecycle и realm model

Поддерживаются:
- `Chaos Sea`
- `Mortal World`
- `Shining Abode`

Ключевые lifecycle triggers:
- `TriggerLifeEnd`
- `TriggerIncarnation`
- `AscensionTrigger`

`AscensionTrigger` больше не doc-only marker:
- он реально переводит игрока в `Shining Abode`
- из `Shining Abode` доступен локальный `New Game+`
- `New Game+` сбрасывает `Enlightenment` и `Ink Feathers`
- `Soul Relics` и `Guardians` сохраняются по модели `Preserve And Rebind`

### 2. QTE

QTE v1 реализован как локальный cinematic subsystem:
- offer file: `output/qte_offer.json`
- accept/decline flow
- локальное выполнение сцен
- 5 node types:
  - `BranchChoice`
  - `TimingBar`
  - `PromptChain`
  - `BalanceMeter`
  - `ChargeRelease`
- паузы между сценами, промежуточные результаты, optional image prompt
- QTE не заменяет основную RPG-механику

### 3. Изображения

Сделана общая image-архитектура:
- entity images versioned
- `Показать изображение` / `Пересоздать изображение`
- scene/QTE images one-shot внутри клиента
- cleanup лишних изображений
- режим `генерировать без автопоказа`

### 4. Soul / life-evaluation / rewards

Жёстко провалидирован roguelike reward cycle:
- каждая завершённая жизнь должна дать минимум `10` Ink Feathers
- каждая завершённая жизнь должна дать минимум одну **новую** Soul Relic
- reward screen использует diff-based анализ
- `player_chronicle` обязателен

### 5. Factions, quests, items, guardians

Сильно усилены:
- faction core + sidecars
- guardian command contract
- quest / quest_history contract
- item sidecars:
  - `item_resources`
  - `item_bonds`
  - `item_text_updates`
  - `item_journals`

### 6. System mods / world setup

Старая активная модель `custom_rules` удалена.

Теперь архитектура такая:
- `game_session/mods/` — глобальные system mods
- `game_session/world_profiles/` — шаблоны миров
- `game_state/control/incarnation_world_setup.json` — pending setup следующего мира
- `lore/current_world/world_directives.json` — persistent dossier текущего мира
- canonical manifest для ГМа:
  - `game_state/core/system_mods.json`

### 7. Audio

Сделана базовая аудиосистема:
- музыка главного меню: `Main Theme` / `Main Theme (alt)`
- общий музыкальный пул в игре
- SFX:
  - `sound-notification.wav`
  - `menu_select.wav`
  - `qte_start`
  - `qte_success`
  - `qte_fail`
- настройки:
  - music on/off
  - music volume
  - sound on/off
  - sound volume

### 8. Главное меню и UX

Главное меню:
- переписано на кастомный renderer
- без `SelectionPrompt`
- есть `Продолжить` как вход в текущую `game_session`
- `Загрузить профиль` остаётся отдельно для save/load
- добавлена настройка размера шрифта
- service-меню опций и связанных экранов переведены на более удобный custom UI

### 9. GM daemon / launcher

Текущий daemon:
- умеет bootstrap
- реально пересылает `CLI_Launch_Script.md`
- поддерживает `AutoPaste`
- default paste mode сейчас `RightClick`
- default timeout = `0` (без лимита)

Но это всё ещё временная архитектура.

Есть design note для будущего:
- [BookOfEternityClient/Launcher/GM_Daemon_ConPTY_Proposal.md](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Launcher/GM_Daemon_ConPTY_Proposal.md)

`ConPTY`-bridge пока **не реализован**.

## Текущая задача

Главный активный трек сейчас:

### Глобальная доработка валидатора как GM-facing слоя

Уже сделан **первый проход**:
- `ValidationIssue` получил совместимую категоризацию:
  - `ProtocolViolation`
  - `StateConsistency`
  - `ClientOwnedSurface`
- daemon теперь показывает:
  - summary groups
  - category / section / code
  - expected / actual
  - repairHint
- клиентский `/validate` тоже показывает summary + human-readable hints
- добавлены validator fixtures в:
  - [FileSystemExample/validator_fixtures](E:/Games/The%20Book%20of%20Eternity%20Reborn/FileSystemExample/validator_fixtures)

Это **не финал**. Следующий логичный шаг:
- пакетно улучшать сами `message / repairHint` в `ValidationService`
- в первую очередь для:
  - lifecycle
  - terminal protocol
  - realm segregation
  - factions
  - guardians
  - item journals / sidecars

## Важные решения, которые нельзя потерять

### Realm segregation

`Chaos Sea` и `Shining Abode`:
- afterlife realms
- работают с guardian/soul/meta systems
- не используют mortal-world NPC/faction/location/combat systems

`Mortal World`:
- не может мутировать guardian/afterlife-only state

### Narrative contract

Для обычного accepted GM turn свежий:
- `output/narrative_response.json.response`

является обязательным.

### QTE

QTE:
- только Mortal World
- только ordinary player-driven turns
- отдельный инструмент, не замена общей RPG-механике

### Mods/world setup

ГМ должен читать:
- `game_state/core/system_mods.json`
- `game_state/control/incarnation_world_setup.json` при воплощении
- `lore/current_world/world_directives.json` в Mortal World

### Client-owned files

ГМ не должен мутировать:
- `system_mods.json`
- `incarnation_world_setup.json`
- `world_directives.json`
- `progression_schedule.json`
- pending snapshot files
- repair/protocol files

## Где смотреть в первую очередь

Если новый агент открывается без памяти, ему лучше начать отсюда:

1. [BookOfEternityClient/Core/GameEngine.cs](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine.cs)
2. [BookOfEternityClient/Services/ValidationService.cs](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ValidationService.cs)
3. [CLI_API_Specification.md](E:/Games/The%20Book%20of%20Eternity%20Reborn/CLI_API_Specification.md)
4. [Rules/Block_CLI_Operations.txt](E:/Games/The%20Book%20of%20Eternity%20Reborn/Rules/Block_CLI_Operations.txt)
5. [TaskGuides/CLI_Step_Main.txt](E:/Games/The%20Book%20of%20Eternity%20Reborn/TaskGuides/CLI_Step_Main.txt)
6. [Examples/E_CLI_Step_Main.txt](E:/Games/The%20Book%20of%20Eternity%20Reborn/Examples/E_CLI_Step_Main.txt)

По конкретным подсистемам:
- mods/world setup:
  - [SystemModService.cs](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SystemModService.cs)
  - [WorldDirectiveService.cs](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/WorldDirectiveService.cs)
- UI:
  - [ExplorerMode.cs](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode.cs)
  - [GameInterface.cs](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/GameInterface.cs)
- daemon:
  - [game_master_daemon.ps1](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/game_master_daemon.ps1)
  - [BookOfEternityClient/Launcher](E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Launcher)

## Что ещё не проверено живьём

Статический аудит в целом уже очень сильный, но live smoke по этим маршрутам остаётся желательным:
- `Ascension -> Shining Abode -> New Game+`
- same-turn сложные faction/item сценарии
- `/world_setup -> /incarnate -> world_directives`
- daemon transport / bootstrap / autopaste
- validator repair loop на реальном rejected turn

## Build

Текущее состояние проекта собирается:

```powershell
dotnet build BookOfEternityClient/BookOfEternityClient.csproj
```

На момент создания handoff:
- build проходит без ошибок
- build проходит без предупреждений проекта

## Резюме для следующего агента

Проект уже в хорошем состоянии.  
Сейчас **не нужно** снова делать полный `Rules vs Code` аудит с нуля.

Рациональный следующий шаг:
- продолжать **validator UX overhaul**
- либо переключаться на конкретный gameplay/UX feature
- либо делать controlled runtime smoke по редким lifecycle routes

Если вскрываются новые ошибки валидатора на “системных файлах”, сначала проверяй:
- не мутирует ли сам клиент `client-owned` файлы до validation pass
- не пишет ли клиент что-то после snapshot, что потом валидатор считает GM mutation
