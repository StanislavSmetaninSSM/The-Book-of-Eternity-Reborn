# GM Workers Live Regression Audit (#1189)

Date: 2026-06-21

Worktree: `E:/Games/worktrees/boe-1189-gm-workers-live`

Branch: `work/1189-gm-workers-live-regression`

Primary successful run root: `C:/Temp/boe-gm-workers-live-20260621-075955`

## Scope

This audit verified console-oriented GM worker delegation with Codex workers:

- proposal-only `narrative-draft`
- proposal-only `analysis`
- scoped `validation-repair` with apply gate
- hidden/background worker launch
- durable task, proposal, and audit artifacts

Browser UI, QTE rendering, Gemini CLI, and broader worker architecture changes
were intentionally out of scope.

## Readiness

Status: conditionally ready for regular live E2E worker use.

Readiness score: 8/10.

The worker path is usable after the fixes in this branch. Hidden Codex workers
can be launched, can receive UTF-8 prompts, can write proposal files, and can be
audited through `gm_worker_audit.jsonl`. Validation repair also completed the
apply-gate path and produced `validation_repair_ready.json`.

The remaining condition is operational: the main GM bridge is still an
interactive Codex session and should be run as the visible main GM console. The
hidden/non-interactive mode is for workers through `codex exec`.

## Successful Evidence

Run root:

`C:/Temp/boe-gm-workers-live-20260621-075955`

Agent Console:

`http://127.0.0.1:63511`

Bridge pipe:

`boe-gmbridge-live-7fa09637ea9e45f7bf4833f96de0d196`

Proposal-only narrative dispatch:

- response file: `dispatch.narrative.exec.json`
- outcome: `completed`
- task: `worker_task_narrative_draft_19bec5ee7a92458da65b1999d96c0452`
- proposal: `worker_proposal_narrative_draft_19bec5ee7a92458da65b1999d96c0452`
- result: review-only draft text, zero changed files

Proposal-only analysis dispatch:

- response file: `dispatch.analysis.exec.json`
- outcome: `completed`
- task: `worker_task_analysis_de59187585e642a8ad3c43c33ed0bcad`
- proposal: `worker_proposal_analysis_de59187585e642a8ad3c43c33ed0bcad`
- result: three findings, zero changed files

Validation-repair dispatch:

- harness report: `validation-repair.result.json`
- outcome enum: `6`
- apply result: `Accepted`
- task: `worker_task_validation_repair_0001`
- proposal: `worker_proposal_validation_repair_0001`
- ready signal: `game_state/control/validation_repair_ready.json`
- final canonical file: `game_state/world/weather.json`
- final content: `{"condition":"clear","description":"Тестовый ветер над поместьем."}`

Durable artifacts:

- `game_session/game_state/control/gm_worker_audit.jsonl`
- `game_session/worker_tasks/**/task.json`
- `game_session/worker_proposals/**/proposal.json`
- `game_session/worker_proposals/worker_proposal_validation_repair_0001/game_state/world/weather.json`

Process visibility:

- worker profiles used `launchVisibility = hidden`
- no extra user-facing worker console windows were required
- post-run process scan found no remaining run-root worker/client processes

## Bugs Found And Fixed

### 1. Relative runner path broke hidden workers

Finding:

The worker profile launch command used `-File "BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1"`,
but the worker process working directory is the copied `game_session`. PowerShell
therefore could not find the runner script during the first live run.

Fix:

`GmWorkerBridgePool.CreateWorkerStartInfo` now resolves a relative PowerShell
`-File` argument against the configured working directory, current repo root,
and app-context ancestors before launch.

Regression test:

`GmWorkerBridgeContractTests.CreateWorkerStartInfo_DefaultRelativeRunner_ResolvesRunnerOutsideGameSession`

### 2. Bare `codex` command did not resolve to `codex.cmd`

Finding:

`ProcessStartInfo` with `UseShellExecute=false` did not resolve the Windows
shim command. The worker failed with `Start-Process`/file-not-found behavior
when the profile used `-AgentCommand "codex ..."`.

Fix:

`gm_worker_cli_runner.ps1` now resolves bare commands with `where.exe` and
prefers executable Windows command extensions such as `.exe`, `.cmd`, `.bat`,
and `.com`.

Regression test:

`GmWorkerCliRunnerTests.RunnerRealMode_BareAgentCommand_ResolvesCmdFromPath`

### 3. Worker prompt stdin was not UTF-8 safe

Finding:

The real validation-repair worker failed with:

`Failed to read prompt from stdin: input is not valid UTF-8`

The prompt contains Cyrillic JSON, and the runner wrote stdin through the
default stream writer encoding.

Fix:

`gm_worker_cli_runner.ps1` now writes the prompt as UTF-8 bytes directly through
`StandardInput.BaseStream`. This avoids Windows code page behavior and works
under both Windows PowerShell and PowerShell 7.

Regression test:

`GmWorkerCliRunnerTests.RunnerRealMode_FeedsPromptToAgentAsUtf8`

### 4. Interactive Codex is not a hidden worker command

Finding:

Using `codex --dangerously-bypass-approvals-and-sandbox` as a hidden worker
command fails because the interactive Codex CLI expects a terminal.

Fix:

Worker templates, examples, and contracts now use:

`codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -`

The main GM bridge keeps the interactive command:

`codex --dangerously-bypass-approvals-and-sandbox`

Regression tests:

- `GmWorkerProfileTemplateTests.DefaultProfileTemplates_ExposeDisabledCodexWorkers`
- `GmWorkerProfileTemplateTests.DefaultProfileTemplates_AreDocumentedByDefaultConfigScripts`

## Controlled Failures Preserved

Earlier failed run roots were kept for diagnosis:

- `C:/Temp/boe-gm-workers-live-20260621-073559`
- `C:/Temp/boe-gm-workers-live-20260621-074649`
- `C:/Temp/boe-gm-workers-live-20260621-075955`

They document the path-resolution, command-resolution, interactive-worker, and
stdin-encoding failures fixed by this branch.

## Follow-Up Issues

No separate blocker or major GitHub issue is required from this audit. The
blockers discovered during the live run were narrowly scoped and fixed under
#1189 with regression tests.

Potential future hardening, not a blocker for #1189:

- Add a first-class non-interactive main-GM test harness if future E2E work
  requires running the main GM itself headless. Today, the visible interactive
  main GM console remains the expected operator surface.

## Verification Commands

Final verification should include:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore
dotnet build BookOfEternityGMBridge\BookOfEternityGMBridge.csproj --no-restore
git diff --check
```
