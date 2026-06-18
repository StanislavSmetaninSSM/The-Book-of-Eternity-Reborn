# Quickstart: Mortal Command Fixture Coverage

## Prerequisites

- For #1092 local preview, use the real local preview session at `E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session`.
- For #1095 clean-checkout QA, use the tracked reusable save source at `FileSystemExample\game_session\saves\manual_saves\mortal_world_command_display_fixture.zip`.
- Keep the repository worktree clean enough to distinguish tracked Spec Kit/test changes from ignored local fixture changes.

## Reusable #1095 Save

Tracked source archive:

```text
FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture.zip
```

Sidecar metadata:

```text
FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture_metadata.json
```

Internal save name:

```text
Mortal World Command Display Fixture (#1095)
```

To make the save visible to the console or browser main-menu load flow from a clean checkout, copy the tracked source archive into the live session's normal manual-save directory:

```powershell
$source = "FileSystemExample\game_session\saves\manual_saves\mortal_world_command_display_fixture.zip"
$targetDir = "BookOfEternityClient\game_session\saves\manual_saves"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $targetDir "mortal_world_command_display_fixture.zip") -Force
```

Then start the console or browser client and load `Mortal World Command Display Fixture (#1095)` from manual saves.

The copied live-session archive is disposable. `SaveLoadService.LoadGameAsync()` replaces the active `game_session` with the archive contents, so repeated manual QA should recopy from the tracked `FileSystemExample/.../mortal_world_command_display_fixture.zip` source before each main-menu load. Automated tests load the tracked archive directly into disposable roots and verify the source archive hash remains unchanged.

Expected visible data includes the #1092 Mortal World command fixture surfaces: inventory/equipment and readable books, NPCs and NPC trade/talk prompts, quests, map/location/weather/world news, rival threads, guardian corrections, factions/directives, skills/stats/distribution, combat, storage/transport item movement, interactions, craft prompts, ink-feather fate previews, and practical universal Mortal previews such as `/статус`, `/душа`, `/хроника`, `/кодекс`, `/галерея`, `/моды`, and `/валидация`.

## Reusable #1096 Chaos Sea Save

Tracked source archive:

```text
FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture.zip
```

Sidecar metadata:

```text
FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture_metadata.json
```

Internal save name:

```text
Chaos Sea Command Display Fixture (#1096)
```

To make the save visible to the console or browser main-menu load flow from a clean checkout, copy the tracked source archive into the live session's normal manual-save directory:

```powershell
$source = "FileSystemExample\game_session\saves\manual_saves\chaos_sea_command_display_fixture.zip"
$targetDir = "BookOfEternityClient\game_session\saves\manual_saves"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $targetDir "chaos_sea_command_display_fixture.zip") -Force
```

Then start the console or browser client and load `Chaos Sea Command Display Fixture (#1096)` from manual saves.

The #1096 save is an at-rest Chaos Sea manual save. It intentionally does not preserve `game_state/control/pending_turn_snapshot/` or live `input/` artifacts because `SaveLoadService` strips transient turn state from saves. Guardian project display uses the validated `game_state/meta/guardian_project_journal.json` entry `project_archive_lighthouse_display_001`; `game_state/meta/guardian_projects.json` remains empty of active tracker authority, so `/archive_project_fuel` renders the project context plus a clear in-world unavailable reason instead of opening a mutation prompt.

Expected visible data includes Chaos Sea navigation, Azalia and Seret guardian context, Azalia's abode and power history, journal-backed guardian project display, guardian politics, the player soul profile, Azalia's afterlife profile, visible active threat and chronicle examples, an afterlife inbox notification, spiritual conflict/combat log details, spiritual arts and a special art, soul relics, archive entries, archive candidates, and practical universal afterlife previews such as `/статус`, `/душа`, `/архив_души`, `/квесты_души`, `/хроника`, `/перья`, `/кодекс`, `/галерея`, and `/валидация`.

## Command Inventory

From the repository root:

```powershell
$rows = foreach($line in Get-Content BookOfEternityClient\CommandProtocol\ExplorerCommandCatalog.cs) {
  if($line -match 'D\("([^"]+)",\s*ExplorerCommandGroup\.MortalWorld,\s*ExplorerCommandMutationMode\.([^,]+).*?\[([^\]]+)\]') {
    $aliases = [regex]::Matches($matches[3], '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    [pscustomobject]@{Id=$matches[1]; Mode=$matches[2]; Primary=$aliases[0]; Aliases=($aliases -join ', ')}
  }
}
$rows | Format-Table -AutoSize
```

## Manual Smoke Commands

Run these in the console client or browser command panel while the fixture is in Mortal World:

```text
/статус
/инв
/нпс
/квесты
/карта
/где_я
/фракции
/навыки
/характеристики
/новости_мира
/чужие_нити
/коррективы_хранителя
/локации
/транспорт
/эффекты
/бой
/погода
/книги
/доступ_к_хранилищам
/взаимодействия
/поговорить_с_нпс
/открыть_судьбу
/переписать_судьбу
/распределить
/директива_компаньону
/директива_фракции
/экипировать
/снять
/выбросить_предмет
/разделить_стопку
/объединить_стопки
/хранилище_предметы
/транспорт_предметы
/торговля_нпс
/ремесло
```

Also smoke-test these universal Mortal World preview commands:

```text
/help
/душа
/достижения
/хроника
/story
/поведение
/жизни
/перья
/кодекс
/правила_мира
/галерея
/моды
/валидация
```

## Automated Verification

Reusable #1095 save load, validation, browser command smoke, and console renderer smoke:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalCommandDisplaySaveTests" --logger "console;verbosity=minimal"
```

Reusable #1096 Chaos Sea save load, validation, browser command smoke, and console renderer smoke:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ChaosSeaCommandDisplaySaveTests" --logger "console;verbosity=minimal"
```

Broader afterlife/fixture/Explorer command gate:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~FileSystemExampleAfterlifeStateExamplesTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~Validation" --logger "console;verbosity=minimal"
```

Local fixture smoke, including JSON/JSONL syntax, `ValidationService`, Mortal World command aliases, practical universal preview commands, and console renderer markup safety:

```powershell
powershell -ExecutionPolicy Bypass -File specs\1092-mortal-command-fixture-coverage\verify-local-fixture.ps1
```

The helper copies `BookOfEternityClient/game_session` into a temporary directory for each command. This is intentional: several local-turn commands create prompt-session control files, and command coverage should not leave those transient locks in the real fixture.

Latest local result:

```text
JSON and JSONL syntax OK: 81 json, 1 jsonl
VALIDATION ISSUES 0
MORTAL ALIASES 77
MORTAL PROBLEMS 0
UNIVERSAL COMMANDS 14
UNIVERSAL PROBLEMS 0
CONSOLE RENDER COMMANDS 91
CONSOLE RENDER PROBLEMS 0
```

Focused C# regression suite for related tracked code:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Mortal|BrowserMortalWorld|Inventory|Trade|Storage|ExplorerWebCommandService"
```

If a local helper is added for the ignored session, run it against:

```powershell
$env:BOOK_OF_ETERNITY_SAMPLE_SESSION='E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session'
```
