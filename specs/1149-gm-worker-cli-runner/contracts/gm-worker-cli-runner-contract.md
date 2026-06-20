# Contract: GM Worker CLI Runner

## Script Location

`BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1`

## Parameters

```powershell
.\gm_worker_cli_runner.ps1 `
  -AgentCommand "codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -" `
  -TimeoutSeconds 180
```

Dry-run prompt inspection:

```powershell
.\gm_worker_cli_runner.ps1 `
  -DryRun `
  -PromptOutPath "C:\Temp\worker-prompt.txt"
```

## Required Environment

- `BOE_WORKER_TASK_PATH`: existing task packet JSON file.
- `BOE_WORKER_PROPOSAL_PATH`: proposal handoff file to create.
- `BOE_WORKER_SESSION_PATH`: existing `game_session` directory.

## Exit Codes

- `0`: prompt generated in dry-run mode, or real agent exited successfully and wrote a non-empty proposal file.
- `2`: required environment variable, task file, session directory, or agent command is missing/invalid.
- `3`: nested agent command timed out.
- `4`: nested agent command exited non-zero or failed to start.
- `5`: nested agent command exited successfully but did not write a non-empty proposal file.

## Prompt Contract

The runner feeds the generated prompt to the nested agent command through
UTF-8 stdin. Agent commands used in hidden/background profiles must therefore
support non-interactive stdin prompts; Codex workers should use `codex exec ... -`
rather than the interactive `codex ...` command.

The prompt must instruct the worker to:

- act as a subordinate GM worker;
- read the embedded task packet;
- write exactly one `worker-proposal-v1` JSON object to `BOE_WORKER_PROPOSAL_PATH`;
- never edit canonical `game_session` files directly;
- write repair content refs under proposal-owned paths when `changedFiles` are needed;
- keep proposal-only tasks free of `changedFiles`;
- leave final validation, scope checks, and canonical application to the main GM/apply gate.

## Authority Boundary

The runner is not an apply gate. It does not parse or accept proposals. It only prepares the prompt, runs the agent, and checks that the handoff file exists and is not empty. Existing C# bridge code remains responsible for JSON/schema validation, permission checks, proposal storage, and canonical state application.
