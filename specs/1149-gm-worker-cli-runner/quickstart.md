# Quickstart: GM Worker CLI Runner

## 1. Create a Dry-Run Prompt

Set the same environment variables the bridge provides to live worker tasks:

```powershell
$env:BOE_WORKER_TASK_PATH = "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session\worker_tasks\sample\task.json"
$env:BOE_WORKER_PROPOSAL_PATH = "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session\worker_proposals\inbox\sample\proposal.json"
$env:BOE_WORKER_SESSION_PATH = "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session"

.\BookOfEternityClient\Launcher\gm_worker_cli_runner.ps1 `
  -DryRun `
  -PromptOutPath "$env:TEMP\boe-worker-prompt.txt"
```

Open the generated prompt and confirm it includes:

- `worker-proposal-v1`
- the proposal path
- the raw task JSON
- the instruction not to edit canonical `game_session` files directly

## 2. Configure a Codex Worker Profile

Use the runner as the profile `launchCommand` and pass Codex as the nested command:

```json
{
  "workerId": "validation_repair_codex",
  "displayName": "Codex validation repair",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex --dangerously-bypass-approvals-and-sandbox\" -TimeoutSeconds 180",
  "role": "validation-repair",
  "enabled": true,
  "launchVisibility": "hidden",
  "timeoutSeconds": 210,
  "maxConcurrentTasks": 1
}
```

The bridge launches this hidden/background. The runner then feeds the generated prompt to Codex through stdin and requires Codex to write the proposal handoff file.

## 3. Verify

Run focused tests:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerCliRunner|GmWorkerBridgeDocumentation|WorkerBridge" -p:BaseOutputPath=TestResults/bin/1149-runner/
```

Run the full C# suite before merging:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:BaseOutputPath=TestResults/bin/1149-full/
```
