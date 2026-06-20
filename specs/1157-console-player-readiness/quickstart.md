# Quickstart: Console Client Live Player-Readiness Pass

Source issue: #1157

## 1. Preflight

```powershell
git status --short
git rev-parse HEAD
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsole" -p:BaseOutputPath=TestResults\1157-agent-console\
```

## 2. Create Disposable Run Root

```powershell
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$RunRoot = Join-Path $env:TEMP "boe-live-e2e-1157-$Stamp"
$SeedSession = Resolve-Path "FileSystemExample\game_session"
$SandboxSession = Join-Path $RunRoot "game_session"
New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
Copy-Item -Recurse -Force -Path $SeedSession -Destination $SandboxSession
```

## 3. Configure GM Bridge

```powershell
$ConfigPath = Join-Path $SandboxSession "config.json"
$Config = if (Test-Path $ConfigPath) { Get-Content $ConfigPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
$Config | Add-Member -Force NoteProperty gmBridgeEnabled $true
$Config | Add-Member -Force NoteProperty gmBridgeBackend "ConPTYBridge"
$Config | Add-Member -Force NoteProperty gmCliLaunchCommand "codex --dangerously-bypass-approvals-and-sandbox"
$Config | Add-Member -Force NoteProperty gmBridgeAutoStart $true
$Config | ConvertTo-Json -Depth 30 | Set-Content -Encoding UTF8 $ConfigPath
```

## 4. Start Console Client With Agent Console

Use `docs/e2e/agent-console-runbook.md` for current argument names. Preserve stdout/stderr under `$RunRoot`.

## 5. Observe And Act

Before each action, save a snapshot. Send text and structured actions only through Agent Console. After each action, save events. Record any player-facing defect with the triggering command/action and artifact path.

## 6. Completion Evidence

Post a #1157 issue comment with run result, surfaces covered, findings, fixes, follow-up issues, verification commands, and residual risk.
