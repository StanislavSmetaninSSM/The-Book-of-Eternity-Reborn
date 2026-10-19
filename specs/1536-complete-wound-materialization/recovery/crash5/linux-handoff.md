# CRASH5 — передача Linux NativeLineage и результаты Windows

Репозиторий: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn
Ветка: `1536-crash5-linux-windows-spec-20261019`.

Активная feature: [spec3](../../spec.md), [plan](../../plan.md), [tasks](../../tasks.md), [validation guide](../../quickstart.md).
Исходная версия: `1253328971a27ffafe3820ad59a2515ebbd35628`; согласованная spec3: `f22e51730e090e5cdc86531f589bdff05193158d`; согласованный plan1: `6f2d4d48615db910168d6c1a32841a1ffb037147`.
Implementation source после независимого исправления: `5f0c08ae5b9303bca7c0a44b1509edcf89fa974c`.

## Что изменено

Оба обычных Linux child запускаются прямо через новый `NativeLineageOwner.StartFinitePipeAsync` и существующий неизменённый C supervisor в режиме host-v2. Parent читает identity до A2; конечный child может закончить до Started. При остановке проверяются настоящие root/supervisor/status/EOF observations, а незавершённая control write сохраняет неопределённость. Полные stdout/stderr и child environment включены только для finite pipe; прежние worker/terminal defaults сохранены. Исходный owner удерживается до подтверждённого Dispose, в том числе при частичном запуске без PID.

Windows использует оригинальный owned Process, dotnet.exe, child-only TEMP/TMP и независимо прочитанные UTC creation ticks с явным видом токена. Оба публичных entrypoint отвергают неподдерживаемый mode до fixture. Ordinary stages ограничены64 JSONL records/2048 UTF8 bytes each/131072 total; их ошибки вспомогательные. Diagnostic/integrity Linux пути, G01 delivery/capture и игровые assertions не переписаны.

Пять исходных cut0…4, настоящий Environment.Exit73, новый resumer0, current owner, механика, en-GB/en-US contrast и инертный повтор сохранены. Замена launcher не доказывает устранение исторического Linux timeout. Его причина остаётся открытой до фактического облачного запуска; ограничения среды также не установлены как доказанная причина.

## Статус evidence

- Independent SOURCE: gpt-6-astra/high выявил один P2 на51f и принял его исправление на5f: реальный natural-stop/S race теперь координируется в Linux fixture, не обходится ожиданием HostExited. Production после51f не менялся.
- Managed helpers на51f: **37/37 PASS,0skip**,43.624s,4 descriptors, cleanup complete. [Summary](T213-T218-green-summary.json), [plan](T213-T218-green-plan.json), TRX/logs рядом. Это13 finite policy +2 generic source guards +9 Windows/common +6 stage +7 existing managed consumer cases.
- Final PREP на5f: обе XML builds exit0; changed-source XML warnings0. Catalog813/11831 valid, ordinary PlanOnly1descriptor/5cases, contracts4descriptors/37cases; все NoBuild,0executed,cleanup complete. [Source/DLL binding](T217-prep.json). Independent SOURCE/PREP accepted; [review](independent-review.md).
- Windows ordinary5: **FAIL / INCOMPLETE**. Первый run достиг category15m; четыре producer timeout220s до Exit73, пятый interrupted, TRX отсутствует. Runner cleanup complete; fixture пятого сохранён. [Outcome](T219-outcome.json), [summary](T219-summary.json), [raw log](T219-dotnet-test.log). Непроверенные игровые критерии не объявлены выполненными; неизменный прогон не повторялся. Причина исследуется.
- Linux native controls и ordinary5: **UNRUN**, ни одна локальная сборка/managed проверка не заменяет Linux qualification. T222…T225 остаются открытыми.
- Historical52/57 остаётся на своём прежнем source/run. Новые результаты не увеличивают этот исторический счётчик автоматически.

## Linux: подготовка

Нужны отдельный checkout выбранного переданного commit, PowerShell7, SDK10/.NET8, Linux x64/glibc, cc/readelf и штатные возможности NativeLineage. Использовать обычные разрешённые возможности среды. Отказ prerequisite сохранять как результат подготовки, без смены backend или настройки обхода ограничений.

Все команды ниже из корня checkout. Source и чистый diff сохранить до build. Package собирается один раз до ordinary5. Изменения environment ограничены текущим процессом и восстанавливаются в finally. Каждая внешняя команда проверяет exit code.

```powershell
$ErrorActionPreference = 'Stop'
$crash5PreviousDotnet = $env:DOTNET_ROOT
$crash5PreviousPackage = $env:BOE_CRASH5_NATIVE_PACKAGE
try {
    git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source.' }
    git status --porcelain
    $crash5DotnetCommand = (Get-Command dotnet -CommandType Application).Source
    $crash5ResolvedDotnet = [IO.File]::ResolveLinkTarget($crash5DotnetCommand, $true)
    $env:DOTNET_ROOT = Split-Path $(if ($null -eq $crash5ResolvedDotnet) { $crash5DotnetCommand } else { $crash5ResolvedDotnet.FullName })
    $crash5PreparedDotnet = $env:DOTNET_ROOT
    $crash5Package = Join-Path (Get-Location) ('TestResults/crash5-native-' + [Guid]::NewGuid().ToString('N'))
    & pwsh -NoProfile -File ./scripts/build-linux-supervisor.ps1 -OutputDirectory $crash5Package -IncludeHostGuardian -IncludeTerminalFixture
    if ($LASTEXITCODE -ne 0) { throw 'Native package preparation failed.' }
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5Package
    foreach ($crash5Project in @('BookOfEternityClient.Tests', 'BookOfEternityClient.IntegrationTests')) {
        dotnet build "$crash5Project/$crash5Project.csproj" --verbosity minimal -p:GenerateDocumentationFile=true -p:NoWarn=1591 "-p:BoeNativePackageDirectory=$crash5Package" -p:BoeRequireNativePackage=true
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $crash5Project" }
    }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -ValidateCatalog -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Catalog validation failed.' }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -SelectionFile tests/selections/1536-crash5-linux-controls.json -PlanOnly -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Linux controls planning failed.' }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-joint-interface-crash-current -PlanOnly -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Ordinary-five planning failed.' }
    # Save package-manifest.json, source/native/DLL hashes and exact build/discovery artifacts.
    # Review Linux PREP before running the separate runtime block below.
} finally {
    $env:DOTNET_ROOT = $crash5PreviousDotnet
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5PreviousPackage
}
```

## Linux: фактические проверки после PREP

В той же PowerShell session после принятого PREP (сохранённые переменные указывают проверенные пути):

```powershell
$crash5RuntimePreviousDotnet = $env:DOTNET_ROOT
$crash5RuntimePreviousPackage = $env:BOE_CRASH5_NATIVE_PACKAGE
try {
    if (!(Test-Path -LiteralPath $crash5PreparedDotnet -PathType Container) -or
        !(Test-Path -LiteralPath $crash5Package -PathType Container)) { throw 'Restore reviewed PREP paths before runtime.' }
    $env:DOTNET_ROOT = $crash5PreparedDotnet
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5Package
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -SelectionFile tests/selections/1536-crash5-linux-controls.json -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Controls failed; investigate before ordinary-five.' }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-joint-interface-crash-current -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Preserve ordinary-five outcome and investigate; do not retry blindly.' }
} finally {
    $env:DOTNET_ROOT = $crash5RuntimePreviousDotnet
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5RuntimePreviousPackage
}
```

Controls selection: new finite12 cases и существующие host/bootstrap-output/owner-retention/native-terminal/held-Main controls. Общее число брать из фактического PlanOnly после deduplication, не складывать пересекающиеся категории. Negative controls проверяют удержание Uncertain; их PASS не является положительным cleanup/game outcome. Existing guardian относится только к isolated native control driver. Обычные пять не запускают guardian и не компилируют package внутри cases.

Ordinary обязательно1 Theory/5planned=5completed=5passed,0skip, original root73/resumer root0, независимая identity/nonce, полные исходные assertions, EOF/join/cleanup. Supervisor0 не подменяет root exit. Лимиты220s child +10s stop/join +10s EOF и15m ordinary category сохранены.

Сохранить исходные stdout/stderr, TRX, plan/summary, durations, source/package/DLL hashes, root/supervisor IDs и статус владения. При Uncertain удерживать original owner и каталоги, не объявлять cleanup успешным. При разрыве связи сначала установить исход прежнего запуска, не повторять неизвестный прогон. Не запускать diagnostic G01/G02, integrity4, full suite или CI как часть этой передачи. После облачного выполнения нужен отдельный независимый разбор T225.
