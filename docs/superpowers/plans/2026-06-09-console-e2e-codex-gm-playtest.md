# Console E2E Codex GM Playtest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve and execute a short live player-readiness E2E playtest for the console client where one Codex instance acts only as the player through the Agent Console API and a separate Codex CLI instance acts as the live GM through the GM bridge.

**Architecture:** Run the real console client against a disposable copied game session. Start the live GM bridge in that sandbox and configure it to launch Codex CLI as the GM. Drive the game only through player-facing Agent Console snapshots and actions during play. Inspect JSON/state files only for setup, teardown, or post-failure evidence.

**Tech Stack:** PowerShell, .NET 8, `BookOfEternityClient`, Agent Console loopback API, ConPTY GM bridge, Codex CLI, GitHub issue #909.

---

## Tracking

- [ ] Source task: GitHub issue #909, `[Plan] Live console E2E player-readiness playtest with Codex GM`
- [ ] Scope: planning only. Do not change game code, tests, prompts, examples, or contracts while executing this plan unless a separate implementation issue exists.
- [ ] Durable plan file: `docs/superpowers/plans/2026-06-09-console-e2e-codex-gm-playtest.md`
- [ ] Primary runbooks:
  - `docs/e2e/agent-console-runbook.md`
  - `docs/e2e/console-agent-runbook.md`
  - `docs/agent-console/snapshot-event-model.md`

## Readiness Definition

- [ ] A real player can complete one short adventure without editing JSON or asking for developer knowledge.
- [ ] The player can understand what is happening from the console UI alone.
- [ ] Short entity summaries have discoverable detailed views where appropriate.
- [ ] Inventory, status, books/documents, effects, map, quests, skills, combat, death, afterlife rewards, and reincarnation surfaces are usable without malformed markup, broken links, or misleading messages.
- [ ] GM-Codex can continue the adventure through the bridge without receiving this test plan or any hidden test instructions.
- [ ] Failures produce enough artifacts to open precise GitHub issues.

## Agent Separation Rules

- [ ] Player agent may read only Agent Console snapshots, Agent Console events, command responses, and final artifact logs during the play segment.
- [ ] Player agent must not inspect `game_session` JSON during the play segment unless the game is already failed or blocked.
- [ ] GM-Codex must be launched as an independent CLI process through the bridge and must not receive this test plan.
- [ ] GM-Codex prompt context should be the same project GM prompt context normal play would use.
- [ ] Any required manual JSON edit is a test failure, not a valid recovery.
- [ ] Any reliance on code knowledge, file paths, or hidden schemas to proceed is at least P1 severity.

## Artifact Contract

- [ ] Create a disposable run root outside the repo, for example `%TEMP%\boe-live-e2e-20260609-HHmmss`.
- [ ] Copy a known-good seed session into that root.
- [ ] Store all runtime logs, snapshots, event dumps, screenshots, bridge logs, and notes under the run root.
- [ ] Preserve artifacts for failed runs.
- [ ] For passing runs, preserve a compact final archive or run summary with exact commit SHA, build command, launch commands, and route transcript.

## Severity Rules

- [ ] P0: The client crashes, hangs permanently, corrupts the session, or cannot continue the adventure.
- [ ] P1: The adventure can continue only through manual file edits, developer knowledge, hidden commands, schema inspection, or bridge surgery.
- [ ] P2: The feature works but is confusing, misleading, incomplete, visually broken, or makes a normal player likely to abandon the flow.
- [ ] P3: Polish issue with low gameplay risk.

## Actor Agency And Brain Protocol Checks

- [ ] Treat nearby significant actors as living entities, not scene props. If the player is in one location with a Mortal NPC, Guardian, resident, Shining political actor, faction head, merchant, trainer, enemy, or other decision-making entity, the run must check whether the GM gives that actor a mind, memory, and strategy.
- [ ] For every accepted turn where such an actor speaks, reacts, negotiates, trains, trades, attacks, refuses, grants a reward, updates a quest, changes relationship, moves location, changes activity, or makes any material decision, verify `output/debug_logs.json.gm_thoughts_markdown` contains:
  - `## NPC Scope` / `## Охват NPC-анализа` with the actor listed as relevant, or an explicit reason why the actor is outside scope;
  - an `Actor Brain 2.0`, `Размышления акторов`, `Размышления NPC`, `Guardian Thoughts`, or equivalent reasoning block for each relevant actor;
  - situation, thoughts/constraints, considered strategies, rejected alternatives, final chosen strategy/action, and state-change summary when a strategy or decision is made.
- [ ] For Mortal NPCs, do not accept a shallow generic Actor Brain block as enough when the turn is social, strategic, or emotional. Mortal `NPC Brain 2.0` depth must still account for knowledge limits, personality, culture, relationship pressure, motives, risk, and the chosen communication strategy.
- [ ] For afterlife actors, use the actor packs from `OtherGuides/Actor_Brain_2_0.md`: Guardian, resident, and Shining political/faction actors need the same strategy-choice discipline when they make decisions or receive state changes.
- [ ] Verify persistent state follows the reasoning. Mortal NPCs should have useful diary/thought/journal or equivalent NPC state updates when they materially react. Afterlife actors should update profile ledger, goals, current activity, personal quests, relationship state, or `gmThoughtsSummary` where the contract expects it.
- [ ] Fail as P1 if a significant actor changes state, speaks for a major scene, trains/trades/fights, or drives a quest while remaining only prose with no persistent actor surface.
- [ ] Fail as P2 if the actor has persistence but no useful thoughts/diary/strategy record, or if the reasoning is purely mechanical and does not explain the actor's in-world choice.
- [ ] Record harness/RLM follow-ups when this fails. Prefer validators, repair packets, command snapshots, actor-state templates, or live-test rubrics that make missing actor agency hard to accept, rather than prompt-only reminders.

## Task 1: Preflight And Sandbox Setup

- [ ] Confirm the working tree state before running:

```powershell
git status --short
git rev-parse HEAD
```

- [ ] Build the console client before any live run:

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.csproj
```

- [ ] Create a disposable run root:

```powershell
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$RunRoot = Join-Path $env:TEMP "boe-live-e2e-$Stamp"
New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
```

- [ ] Copy the seed session into the disposable run root. Prefer `FileSystemExample\game_session` for a clean run:

```powershell
$SeedSession = Resolve-Path "FileSystemExample\game_session"
$SandboxSession = Join-Path $RunRoot "game_session"
Copy-Item -Recurse -Force -Path $SeedSession -Destination $SandboxSession
```

- [ ] Record run metadata:

```powershell
@{
  commit = (git rev-parse HEAD)
  runRoot = $RunRoot
  seedSession = "$SeedSession"
  startedAt = (Get-Date).ToString("o")
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 (Join-Path $RunRoot "run-metadata.json")
```

## Task 2: Configure The GM-Codex Bridge

- [ ] Locate Codex CLI. Known expected path on this machine is `C:\Users\Ёж\AppData\Roaming\npm\codex.ps1`; verify before use:

```powershell
Get-Command codex -ErrorAction SilentlyContinue
Test-Path "C:\Users\Ёж\AppData\Roaming\npm\codex.ps1"
```

- [ ] Configure only the sandbox session for bridge play:

```powershell
$ConfigPath = Join-Path $SandboxSession "config.json"
$Config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$Config.gmBridgeEnabled = $true
$Config.gmBridgeBackend = "ConPTYBridge"
$Config.gmCliLaunchCommand = "codex --dangerously-bypass-approvals-and-sandbox"
$Config.gmBridgeAutoStart = $true
$Config | ConvertTo-Json -Depth 30 | Set-Content -Encoding UTF8 $ConfigPath
```

- [ ] Start the bridge from the launcher in the sandbox session:

```powershell
powershell -ExecutionPolicy Bypass -File BookOfEternityClient\Launcher\bookofeternity.ps1 start-bridge -SessionPath $SandboxSession
```

- [ ] Do not redirect stdout or stderr from a ConPTY bridge process that launches `codex`. Codex requires a terminal; redirecting its output can make it exit with `Error: stdout is not a terminal` while the bridge shell remains alive.

- [ ] Confirm bridge diagnostics without sending test instructions to the GM. Mark the bridge ready only after diagnostics show the live Codex UI, for example `OpenAI Codex` and the expected model/status line:

```powershell
$BridgeStatus = Join-Path $SandboxSession "game_state\control\gm_bridge_status.json"
Get-Content $BridgeStatus -Raw
powershell -ExecutionPolicy Bypass -File BookOfEternityClient\Launcher\bookofeternity.ps1 diagnostics -SessionPath $SandboxSession
```

- [ ] Start the game master daemon in the same sandbox. The bridge only hosts the GM process; the daemon is what detects `input/turn_request.json` and dispatches natural player turns to the bridge:

```powershell
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

- [ ] Use an argument array for the daemon launch; a string-built command is easy to break when the repository or session path contains spaces. When launching `dotnet <dll>` directly from PowerShell, prefer `System.Diagnostics.ProcessStartInfo.ArgumentList` or explicitly quoted DLL paths; `Start-Process -ArgumentList @($dllPath, ...)` does not reliably quote paths with spaces.

## Task 3: Launch The Real Console Client With Agent Console

- [ ] Pick a local port and token:

```powershell
$AgentPort = 55731
$AgentUrl = "http://127.0.0.1:$AgentPort/"
$AgentToken = [Convert]::ToBase64String([Guid]::NewGuid().ToByteArray()).TrimEnd("=")
```

- [ ] Start the real client against the sandbox session with Agent Console enabled. If the client has a different session-path argument in the current build, use the argument documented in `docs/e2e/agent-console-runbook.md`:

```powershell
$ClientExe = Resolve-Path "BookOfEternityClient\bin\Debug\net8.0\BookOfEternityClient.exe"
$StdOut = Join-Path $RunRoot "client.stdout.log"
$StdErr = Join-Path $RunRoot "client.stderr.log"
$ClientArgs = @(
  "--agent-console",
  "--agent-url", $AgentUrl,
  "--agent-token", $AgentToken,
  "--plain-output",
  "--session-path", $SandboxSession
)
$ClientProcess = Start-Process -FilePath $ClientExe -ArgumentList $ClientArgs -WorkingDirectory (Split-Path $ClientExe) -RedirectStandardOutput $StdOut -RedirectStandardError $StdErr -WindowStyle Hidden -PassThru
```

- [ ] Confirm the Agent Console snapshot endpoint responds:

```powershell
Invoke-RestMethod -Method Get -Uri "$AgentUrl/api/agent-console/snapshot" | ConvertTo-Json -Depth 30
```

## Task 4: Observation Loop

- [ ] Before each player action, save a snapshot:

```powershell
$SnapshotPath = Join-Path $RunRoot ("snapshot-{0:0000}.json" -f $Step)
Invoke-RestMethod -Method Get -Uri "$AgentUrl/api/agent-console/snapshot" | ConvertTo-Json -Depth 50 | Set-Content -Encoding UTF8 $SnapshotPath
```

- [ ] Send text commands only through the Agent Console write endpoint:

```powershell
$Headers = @{ Authorization = "Bearer $AgentToken" }
$Body = @{ text = "/статус" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$AgentUrl/api/agent-console/text" -Headers $Headers -ContentType "application/json" -Body $Body
```

- [ ] Prefer structured actions when the snapshot offers an action that represents the intended player choice:

```powershell
$Body = @{ actionId = "open-status" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$AgentUrl/api/agent-console/action" -Headers $Headers -ContentType "application/json" -Body $Body
```

- [ ] After each action, save Agent Console events:

```powershell
$EventsPath = Join-Path $RunRoot ("events-{0:0000}.json" -f $Step)
Invoke-RestMethod -Method Get -Uri "$AgentUrl/api/agent-console/events" | ConvertTo-Json -Depth 50 | Set-Content -Encoding UTF8 $EventsPath
```

## Task 5: Afterlife Opening Route

- [ ] Start from the first visible player-facing screen and record whether the player is in afterlife, mortal world, or a recovery screen.
- [ ] If the session starts in afterlife, talk to the guardian using visible commands or actions.
- [ ] During Guardian interaction, apply Actor Agency checks: the Guardian must have relevant actor scope, a reasoning/brain block, and persistent profile/ledger/thought/activity changes when the Guardian reacts, judges, teaches, trades, grants, refuses, or changes relationship.
- [ ] Open all visible afterlife command surfaces that exist in the current build:
  - `/статус`
  - `/квесты`
  - `/обучение`
  - afterlife archive/books/relics/feathers/soul commands shown by the UI
  - any visible help command
- [ ] Verify each visible afterlife command either works or clearly explains why it is unavailable.
- [ ] For `/обучение` in afterlife, verify the screen distinguishes mentor vitrines from expensive self-training fallback. If a mentor profile exists without a fresh `mentorTrainingShowcase`, record whether a pending GM refresh request is created and whether the next GM turn can materialize it without manual JSON editing.
- [ ] If an afterlife mentor offer is available, buy one low-risk Spiritual Art upgrade and verify the client, not the GM, spends currency, writes a receipt, and raises only a legal tier. If no mentor offer exists, inspect self-training costs and confirm they are clearly presented as expensive fallback.
- [ ] Fail as P2 or worse if a command mentions the wrong lifecycle area, for example a mortal map command returning a guardian-only explanation.
- [ ] Fail as P1 if progress requires knowing an internal file name, pending control file, or schema field.

## Task 6: Start A Mortal Life

- [ ] Use the visible UI route to start or continue a mortal incarnation.
- [ ] Confirm the player can understand race, class, level, money, active states, health, energy, balance, quests, and location from the console UI.
- [ ] Confirm named Mortal NPCs in the first scene are materialized when they are actionable. A named teacher, merchant, quest-giver, witness, enemy, or dialogue partner must not exist only in prose.
- [ ] Run the baseline mortal commands:
  - `/статус`
  - `/инв`
  - `/карта`
  - `/квесты`
  - `/эффекты`
  - `/книги`
  - `/навыки`
  - `/обучение`
  - `/help`
- [ ] For every command, capture whether the response is complete, useful, and free of malformed markup.
- [ ] For `/обучение` in a mortal life, verify NPC teachers are selected through a player-readable menu, each offer shows target skill, current/next mastery, teacher cap, money cost, current-level XP cost, availability reason, and a back action.
- [ ] If a teacher exists without a fresh `trainingShowcase`, record whether a pending GM refresh request is created and whether the next GM turn can fill it through normal response surfaces without manually editing files.
- [ ] If a training offer is available, buy one cheap offer and verify the client does not delevel the player, spends only current-level XP progress plus money, and writes a visible/resulting mastery increase.
- [ ] Check short-detail linking:
  - status active effects must have a discoverable detailed view in `/эффекты` or equivalent
  - inventory documents must have a discoverable readable view in `/книги` or equivalent
  - quest summaries must have enough details in `/квесты`
  - skills shown in combat or status must have details in `/навыки`
- [ ] Fail as P2 if the player sees a named entity but cannot discover what it means.
- [ ] Fail as P1 if the validator accepts the state while the client cannot render or navigate the entity.

## Task 7: Mortal Exploration Route

- [ ] Perform three to five simple natural-language mortal actions:
  - look around the location
  - talk to a nearby NPC
  - inspect or take one object
  - read a document or book if available
  - attempt one simple skill or perception check
- [ ] Do not guide GM-Codex with test goals. The player agent may phrase actions naturally, as a player would.
- [ ] Verify the GM response is reflected in the client without raw protocol, JSON, debug paths, or markup errors.
- [ ] Verify newly introduced entities become discoverable through the relevant command surface.
- [ ] For every NPC the player talks to or directly affects, apply Actor Agency checks. The NPC should have persistent identity, diary/thought/journal or equivalent memory, and a brain/strategy block if they answer, refuse, bargain, reveal a clue, move, or change attitude.
- [ ] If the NPC makes a communication choice, check that the GM considered multiple strategies and selected the final approach based on profile, situation, relationship, knowledge, and risk.

## Task 8: Combat Route

- [ ] Start or accept one simple low-risk combat encounter through normal play.
- [ ] Use at least two different available combat actions or skills.
- [ ] Check that action point costs, health/energy/balance changes, enemy state, and result text are understandable.
- [ ] Check repeated-use behavior for a strong skill. If spam is possible, record whether it is actually harmful to pacing or balance.
- [ ] Apply Actor Agency checks to enemies and allies when they choose tactics, retreat, negotiate, use an ability, or change target. Combat decisions should not be unexplained random actions when the actor has a profile or state.
- [ ] End combat with a clear win, escape, or loss state.
- [ ] Fail as P1 if combat gets stuck in a pending state the player cannot resolve.
- [ ] Fail as P2 if combat works mechanically but the player cannot infer legal actions or consequences.

## Task 9: End Life And Verify Rewards

- [ ] End the mortal life through a natural story outcome, voluntary ending, or controlled death route available in the current build.
- [ ] Confirm the console transitions back to afterlife without manual state repair.
- [ ] If the Guardian, afterlife resident, or evaluating actor comments on the life, apply Actor Agency checks: evaluation should include relevant actor scope, reasoning/brain, and persistent afterlife profile/ledger/reward state where applicable.
- [ ] Verify life results are visible and understandable:
  - death or ending reason
  - rewards
  - ink feathers or equivalent progression currency
  - archive/memory entries
  - relics or persistent unlocks if any
  - quests or achievements updated by the life
- [ ] Open afterlife command surfaces again and confirm the rewards are connected to visible afterlife systems.
- [ ] Fail as P2 if rewards are granted but cannot be found from the UI.
- [ ] Fail as P1 if reward state is written but causes the client to report corruption.

## Task 10: Failure Triage And GitHub Issues

- [ ] For every P0, P1, or repeated P2 failure, create a GitHub issue before continuing to unrelated fixes.
- [ ] Each issue must include:
  - player-facing symptom
  - exact command or action that triggered it
  - expected behavior
  - actual behavior
  - severity
  - run root artifact path
  - relevant snapshot/event filenames
  - whether manual JSON repair was needed
  - whether validator accepted the broken data
- [ ] Use labels matching the subsystem where possible, for example `subsystem: console-client`, `subsystem: validation`, `subsystem: docs`, `ux`, `bug`.
- [ ] If the failure is a cross-entity discoverability problem, link it to the broader tracked issue for entity-detail consistency instead of creating an isolated cosmetic bug.

## Task 11: Cleanup

- [ ] Stop the client process if it is still running:

```powershell
if ($ClientProcess -and -not $ClientProcess.HasExited) {
  Stop-Process -Id $ClientProcess.Id -Force
}
```

- [ ] Stop the daemon process if it is still running:

```powershell
if ($DaemonProcess -and -not $DaemonProcess.HasExited) {
  Stop-Process -Id $DaemonProcess.Id -Force
}
```

- [ ] Stop the bridge helper process only if it belongs to the disposable sandbox:

```powershell
$BridgeStatus = Join-Path $SandboxSession "game_state\control\gm_bridge_status.json"
if (Test-Path $BridgeStatus) {
  $Status = Get-Content $BridgeStatus -Raw | ConvertFrom-Json
  if ($Status.helperPid) {
    Stop-Process -Id ([int]$Status.helperPid) -Force -ErrorAction SilentlyContinue
  }
}
```

- [ ] Write final run notes to `$RunRoot\run-summary.md`.
- [ ] Keep failed-run artifacts intact.
- [ ] For a clean pass, record the run root and concise result in GitHub issue #909.

## Verification Commands

- [ ] Baseline build:

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.csproj
```

- [ ] Agent Console focused tests if present in the current branch:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsole"
```

- [ ] Documentation-sensitive afterlife tests if the playtest uncovers or triggers an afterlife contract change in a later implementation issue:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

## Self-Review Checklist

- [ ] The GM was not told this was a test.
- [ ] The player agent used only player-visible information during the play segment.
- [ ] Every failure has an artifact trail that another developer can reproduce.
- [ ] No production or developer session was modified.
- [ ] No code, prompt, example, or contract change was made without a separate implementation issue.
- [ ] The final conclusion distinguishes playable, playable with UX issues, blocked by bugs, and blocked by test harness setup.

## Completion Criteria

- [ ] One short adventure has been attempted end to end with a live Codex GM through the bridge.
- [ ] The route covered afterlife, guardian interaction, afterlife training, mortal life start, baseline commands, inventory, effects, books/documents, map, quests, mortal training, exploration, combat, life ending, and afterlife rewards.
- [ ] All P0/P1 issues found during the run have GitHub issues.
- [ ] Repeated P2 patterns have GitHub issues or are linked to an existing broader issue.
- [ ] GitHub issue #909 has a final comment with result, artifacts, blocking issues, and recommendation for the next run.
