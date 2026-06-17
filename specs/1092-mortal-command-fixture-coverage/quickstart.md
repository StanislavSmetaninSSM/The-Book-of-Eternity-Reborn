# Quickstart: Mortal Command Fixture Coverage

## Prerequisites

- Use the real local preview session at `E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session`.
- Keep the repository worktree clean enough to distinguish tracked Spec Kit/test changes from ignored local fixture changes.

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
