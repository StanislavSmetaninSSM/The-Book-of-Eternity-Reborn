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

## 4. Start GM Bridge And Turn Daemon

Start the bridge first, then start the daemon that watches `input/turn_request.json` and dispatches real player turns to the bridge. Without the daemon, slash commands still work but natural-language turns appear to hang.

Do not redirect stdout or stderr from a ConPTY bridge process that launches `codex`. Codex requires a terminal; redirecting its output can make the GM process exit with `Error: stdout is not a terminal` while the bridge shell remains alive. Capture bridge evidence through launcher diagnostics/status files instead.

```powershell
powershell -ExecutionPolicy Bypass -File BookOfEternityClient\Launcher\bookofeternity.ps1 start-bridge -SessionPath $SandboxSession

powershell -ExecutionPolicy Bypass -File BookOfEternityClient\Launcher\bookofeternity.ps1 diagnostics -SessionPath $SandboxSession | Set-Content -Encoding UTF8 (Join-Path $RunRoot "bridge-diagnostics-before-ready.json")

$DaemonOut = Join-Path $RunRoot "daemon.stdout.log"
$DaemonErr = Join-Path $RunRoot "daemon.stderr.log"
$DaemonArgs = @(
  "-NoLogo",
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-File", (Resolve-Path "BookOfEternityClient\game_master_daemon.ps1"),
  "-GameSessionPath", $SandboxSession,
  "-TurnTimeout", "900",
  "-PollingInterval", "500"
)
$DaemonProcess = Start-Process -FilePath "powershell" `
  -ArgumentList $DaemonArgs `
  -WorkingDirectory (Get-Location) `
  -RedirectStandardOutput $DaemonOut `
  -RedirectStandardError $DaemonErr `
  -WindowStyle Hidden `
  -PassThru
```

Mark the bridge ready only after diagnostics show the live Codex UI, for example `OpenAI Codex` and the expected model/status line. A sleep followed by `ready` is not enough; if Codex failed or PowerShell is still at a prompt, the daemon can send the GM request into the wrong shell.

Use an argument array as shown for the daemon. When launching `dotnet <dll>` directly from PowerShell, prefer `System.Diagnostics.ProcessStartInfo.ArgumentList` or explicitly quoted DLL paths; `Start-Process -ArgumentList @($dllPath, ...)` does not reliably quote paths with spaces.

## 5. Start Console Client With Agent Console

Use `docs/e2e/agent-console-runbook.md` for current argument names. Preserve stdout/stderr under `$RunRoot`.

## 6. Observe And Act

Before each action, save a snapshot. Send text and structured actions only through Agent Console. After each action, save events. Record any player-facing defect with the triggering command/action and artifact path.

## 7. Completion Evidence

Post a #1157 issue comment with run result, surfaces covered, findings, fixes, follow-up issues, verification commands, and residual risk.
