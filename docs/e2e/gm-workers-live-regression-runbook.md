# GM Workers live regression runbook

Issue: #1189

Use this runbook to verify that hidden Codex GM workers can be used by the main
GM bridge during console-oriented play without exposing extra worker windows to
the player or letting workers write canonical state directly.

## Scope

- Console client / Agent Console only.
- Codex workers only, launched with `codex exec -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -`.
- Proposal-only `narrative-draft` or `analysis` dispatch through
  `dispatchworkertask`.
- Validation-repair dispatch through the existing validation repair loop.
- Browser UI, QTE rendering, Gemini CLI, and broad worker architecture changes
  are out of scope.

## Prerequisites

Run from the repository root.

```powershell
dotnet restore BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj
dotnet restore BookOfEternityGMBridge\BookOfEternityGMBridge.csproj
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore
dotnet build BookOfEternityGMBridge\BookOfEternityGMBridge.csproj --no-restore
```

## Prepare a disposable session

Keep all generated logs and copied session data outside the repository.

```powershell
$RunRoot = Join-Path $env:TEMP ("boe-gm-workers-live-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
Copy-Item -Recurse -Path "FileSystemExample\game_session" -Destination (Join-Path $RunRoot "game_session")
Copy-Item -Recurse -Path "BookOfEternityClient\system_guardians" -Destination (Join-Path $RunRoot "system_guardians")
$SessionPath = Join-Path $RunRoot "game_session"
```

The console client receives `$RunRoot` as its base path, not `$SessionPath`.
Keep `system_guardians` next to `game_session`; otherwise validators that resolve
system guardian presets from the base path can report false errors such as an
unknown Eternal Guardian preset during Chaos Sea tests.

For a named command-display save, extract the archive into the disposable
`game_session` and still copy `system_guardians` next to it:

```powershell
$RunRoot = Join-Path $env:TEMP ("boe-chaos-console-live-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$SessionPath = Join-Path $RunRoot "game_session"
New-Item -ItemType Directory -Force -Path $SessionPath | Out-Null
Expand-Archive -LiteralPath "FileSystemExample\game_session\saves\manual_saves\chaos_sea_command_display_fixture.zip" -DestinationPath $SessionPath
Copy-Item -Recurse -Path "BookOfEternityClient\system_guardians" -Destination (Join-Path $RunRoot "system_guardians")
```

Enable the three Codex worker templates in the copied `config.json`. The file
uses camelCase property names; do not write PascalCase settings.

```powershell
$ConfigPath = Join-Path $SessionPath "config.json"
$Config = if (Test-Path $ConfigPath) {
  Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
  [pscustomobject]@{}
}

$Runner = 'BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1'
$CodexMain = 'codex -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox'
$CodexWorker = 'codex exec -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -'
function New-WorkerProfile($id, $name, $role, $taskType, $timeout, $proposalOnly, $requiresValidation, $readPaths, $writePaths) {
  [ordered]@{
    workerId = $id
    displayName = $name
    launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$Runner`" -AgentCommand `"$CodexWorker`" -TimeoutSeconds $timeout"
    role = $role
    enabled = $true
    launchVisibility = "hidden"
    timeoutSeconds = ($timeout + 30)
    maxConcurrentTasks = 1
    permissions = [ordered]@{
      taskTypes = @($taskType)
      readPaths = $readPaths
      proposalWritePaths = $writePaths
      proposalOnly = $proposalOnly
      requiresValidation = $requiresValidation
    }
  }
}

$Config | Add-Member -Force -NotePropertyName "gmBridgeEnabled" -NotePropertyValue $true
$Config | Add-Member -Force -NotePropertyName "gmBridgeBackend" -NotePropertyValue "ConPTYBridge"
$Config | Add-Member -Force -NotePropertyName "gmCliLaunchCommand" -NotePropertyValue $CodexMain
$Config | Add-Member -Force -NotePropertyName "gmBridgeAutoStart" -NotePropertyValue $false
$Config | Add-Member -Force -NotePropertyName "gmWorkerBridgeProfiles" -NotePropertyValue @(
  (New-WorkerProfile "validation_repair_codex" "Codex validation repair" "validation-repair" "validation-repair" 180 $false $true @("game_state/**", "lore/**", "input/**", "ready/**") @("game_state/**", "lore/**", "ready/**")),
  (New-WorkerProfile "narrative_draft_codex" "Codex narrative drafter" "narrative-draft" "narrative-draft" 120 $true $false @("game_state/**", "lore/**", "Rules/**", "TaskGuides/**") @()),
  (New-WorkerProfile "analysis_codex" "Codex analysis worker" "analysis" "analysis" 120 $true $false @("game_state/**", "lore/**", "Rules/**", "TaskGuides/**") @())
)
$Config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $ConfigPath -Encoding UTF8
```

## Launch the console client with Agent Console

```powershell
$Port = 8790
$Base = "http://127.0.0.1:$Port"
$Token = ([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))).ToLowerInvariant()
$ClientExe = Resolve-Path "BookOfEternityClient\bin\Debug\net8.0\BookOfEternityClient.exe"

$Client = Start-Process -FilePath $ClientExe `
  -ArgumentList @(
    $RunRoot,
    "--agent-console", "--agent-url", $Base, "--agent-token", $Token, "--plain-output"
  ) `
  -WorkingDirectory (Get-Location) `
  -RedirectStandardOutput (Join-Path $RunRoot "client.stdout.txt") `
  -RedirectStandardError (Join-Path $RunRoot "client.stderr.txt") `
  -WindowStyle Hidden `
  -PassThru
```

Use the prebuilt executable for live runs after the prerequisite build. `dotnet run`
rebuilds the client and can fail if another live client is still holding
`BookOfEternityClient.exe`.

Poll until the snapshot is non-empty:

```powershell
curl.exe -sS "$Base/api/agent-console/snapshot" > (Join-Path $RunRoot "agent-console.snapshot.json")
```

## Launch the main GM bridge

The bridge host uses a visible console window for the main GM. Worker windows
must remain hidden.

```powershell
.\BookOfEternityClient\Launcher\bookofeternity.ps1 start-bridge -SessionPath $SessionPath
Start-Sleep -Seconds 8
.\BookOfEternityClient\Launcher\bookofeternity.ps1 ready -SessionPath $SessionPath
.\BookOfEternityClient\Launcher\bookofeternity.ps1 diagnostics -SessionPath $SessionPath |
  Set-Content -LiteralPath (Join-Path $RunRoot "bridge.diagnostics.before.json") -Encoding UTF8
```

## Launch the GM daemon

The console client writes `input/turn_request.json`, but the daemon is the
component that watches that request and dispatches it to the bridge. A ready
bridge without a running daemon leaves the client waiting for a GM turn forever.

```powershell
$DaemonLogPath = Join-Path $RunRoot "daemon.log"
$Daemon = .\BookOfEternityClient\Launcher\bookofeternity.ps1 start-daemon -SessionPath $SessionPath --timeout 900 --log $DaemonLogPath |
  ConvertFrom-Json
$Daemon | ConvertTo-Json -Depth 8 |
  Set-Content -LiteralPath (Join-Path $RunRoot "daemon.start.json") -Encoding UTF8
$Daemon.daemonPid
```

The launcher action uses an encoded PowerShell host command internally so paths
with spaces, such as the repository root, do not break daemon startup.

Before submitting a live player turn, run the read-only runtime preflight:

```powershell
.\scripts\agent-console-gm-runtime-preflight.ps1 `
  -SessionPath $SessionPath `
  -RequireBridge `
  -RequireReadyBridge `
  -WaitSeconds 30 |
  Tee-Object -FilePath (Join-Path $RunRoot "gm-runtime-preflight.json")
```

Script path: `scripts/agent-console-gm-runtime-preflight.ps1`. The script checks
`gm_daemon_status.json`, `gm_bridge_status.json`, `pid`, `helperPid`, and
`Get-Process` liveness. `-WaitSeconds` makes the check poll briefly while a
freshly started daemon or bridge is still writing its first status file. A
non-zero exit after that wait means the live test must stop and fix
bridge/daemon startup before sending player input.

During live play, check the daemon control status after every completed turn or
repair. `game_state/control/gm_daemon_status.json` should remain
`status=running`; if it contains `lastLoopError`, preserve the run root because
the watcher recovered from a transient harness error. If the daemon exits with
`status=failed`, inspect `game_state/control/gm_daemon_fatal_error.json` before
restarting. A hidden daemon that silently stops after validation repair is a
harness bug, not a GM/player failure.

The client also protects live tests from silent `gm-waiting` deadlocks. If a
present `gm_daemon_status.json` / `gm_bridge_status.json` is a stale status file
with a dead pid, the wait loop writes a correlated `ready/turn_error.json` with
`harnessSource = "gm_runtime_unavailable"` and rolls the player action back
through the normal terminal-error path. If no terminal response arrives within
`GmTimeoutSeconds`, the same path uses
`harnessSource = "gm_terminal_wait_timeout"`. Treat both as harness/RLM feedback:
preserve the run root, inspect the status files and daemon/bridge logs, then
restart through `bookofeternity.ps1 start-bridge` and
`bookofeternity.ps1 start-daemon` instead of editing game data or changing GM
prompts first.

## Dispatch proposal-only worker tasks

Use the bridge pipe directly for `dispatchworkertask`. This avoids relying on
manual UI clicks while still exercising the same live bridge and hidden worker
path.

```powershell
function Invoke-BoeBridgeRequest($SessionPath, $Payload) {
  $StatusPath = Join-Path $SessionPath "game_state\control\gm_bridge_status.json"
  $Status = Get-Content -LiteralPath $StatusPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $Pipe = [System.IO.Pipes.NamedPipeClientStream]::new(".", [string]$Status.pipeName, [System.IO.Pipes.PipeDirection]::InOut)
  try {
    $Pipe.Connect(5000)
    $Writer = [System.IO.StreamWriter]::new($Pipe, [System.Text.Encoding]::UTF8, 1024, $true)
    $Writer.AutoFlush = $true
    $Writer.WriteLine(($Payload | ConvertTo-Json -Depth 20 -Compress))
    $Reader = [System.IO.StreamReader]::new($Pipe, [System.Text.Encoding]::UTF8, $false, 4096, $true)
    $Reader.ReadLine() | ConvertFrom-Json
  } finally {
    $Pipe.Dispose()
  }
}

$Narrative = Invoke-BoeBridgeRequest $SessionPath @{
  command = "dispatchworkertask"
  workerTaskType = "narrative-draft"
  sessionId = "gm-workers-live-regression"
  requestId = "narrative-1"
  turnNumber = 1
  sceneGoal = "Draft a short optional room-description variant for the main GM to review."
  tone = "dark fantasy, concise, natural Russian prose"
  continuityNotes = @("Do not resolve the player action.", "Do not edit canonical files.")
  targetLength = "80-120 words"
  contextPaths = @("game_state/world/current_location.json", "output/narrative_response.json")
}
$Narrative | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $RunRoot "dispatch.narrative.json") -Encoding UTF8

$Analysis = Invoke-BoeBridgeRequest $SessionPath @{
  command = "dispatchworkertask"
  workerTaskType = "analysis"
  sessionId = "gm-workers-live-regression"
  requestId = "analysis-1"
  turnNumber = 1
  analysisGoal = "Review whether the current copied session has enough visible mortal-world data for a short console smoke adventure."
  questions = @("Which commands should the tester run first?", "Are any context files missing?")
  contextPaths = @("game_state/world/current_location.json", "game_state/core/player_status.json")
}
$Analysis | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $RunRoot "dispatch.analysis.json") -Encoding UTF8
```

Expected result: both responses have `ok=true`, `workerDispatch.outcome=Completed`,
and non-empty `proposalId`. The proposals are stored under
`game_session/worker_proposals/`.

If a Codex worker writes a valid proposal but the CLI runner exits late or times
out, the dispatch should still return `Completed`; preserve `dispatch.*.json`
and `gm_worker_audit.jsonl` so the abnormal exit can be inspected separately.

## Exercise validation-repair

Create a controlled validation fault in the copied session, then start a normal
turn or validation path that writes `game_state/control/validation_repair_request.json`.
The expected worker path is:

1. `game_state/control/validation_repair_request.json` exists.
2. `game_state/control/gm_worker_latest_validation_repair_task.json` exists.
3. `game_session/worker_tasks/<task>/task.json` exists.
4. `game_session/worker_proposals/<proposal>/proposal.json` exists.
5. `game_state/control/gm_worker_audit.jsonl` contains dispatch and terminal events.
6. If the apply gate accepts the proposal, `validation_repair_ready.json` is
   created by the client, not by the worker.

If the live Codex worker cannot produce a valid repair proposal, preserve the
failure as a controlled finding rather than editing canonical files manually.

## Evidence to preserve

Copy or keep these files under `$RunRoot`:

- `client.stdout.txt`
- `client.stderr.txt`
- `agent-console.snapshot.json`
- `bridge.diagnostics.before.json`
- `dispatch.narrative.json`
- `dispatch.analysis.json`
- `game_session/game_state/control/gm_worker_audit.jsonl`
- `game_session/worker_tasks/**/task.json`
- `game_session/worker_proposals/**/proposal.json`
- `game_session/worker_proposals/**/apply_decision.json` when present

## Teardown

```powershell
try { .\BookOfEternityClient\Launcher\bookofeternity.ps1 shutdown-bridge -SessionPath $SessionPath } catch { }
if ($Client -and -not $Client.HasExited) {
  Stop-Process -Id $Client.Id -Force
}
```

Do not remove `$RunRoot` until the audit report has copied or summarized the
needed evidence.
