# Data Model: GM Worker CLI Runner

## Worker Runner Invocation

Represents a single script invocation launched by a `WorkerBridgeProfile.launchCommand`.

Fields:

- `AgentCommand` (string): CLI command to run in real mode. Default should be suitable for local Codex, but profiles may override it.
- `DryRun` (bool): When true, no external agent is launched.
- `PromptOutPath` (string, optional): File path for generated prompt in dry-run mode.
- `TimeoutSeconds` (int): Maximum wait time for nested agent command in real mode.

Validation:

- `TimeoutSeconds` must be at least 1.
- `AgentCommand` is not required in dry-run mode.
- In real mode, `AgentCommand` must be non-empty.

## Worker Runtime Environment

Existing bridge-provided environment variables consumed by the runner.

Fields:

- `BOE_WORKER_TASK_PATH`: absolute path to task packet JSON.
- `BOE_WORKER_PROPOSAL_PATH`: absolute path where the worker must write a proposal JSON.
- `BOE_WORKER_SESSION_PATH`: absolute path to the active `game_session` directory.

Validation:

- All three variables must be present.
- Task path must point to an existing file.
- Session path must point to an existing directory.
- Proposal directory is created by the runner if absent.

## Generated Worker Prompt

Prompt text handed to the configured agent.

Fields/sections:

- Role statement: subordinate GM worker.
- Authority rules: no direct canonical state edits.
- Task path, proposal path, session path.
- Output contract: exactly one `worker-proposal-v1` JSON object at proposal path.
- Changed-file guidance: content refs under `worker_proposals/<proposalId>/...` for file-changing proposals; empty `changedFiles` for proposal-only tasks.
- Raw task packet JSON.

Validation:

- Must contain `worker-proposal-v1`.
- Must contain the proposal path and task path.
- Must include the direct canonical-state-write ban.

## Proposal Handoff

The proposal file produced by the external agent.

Fields:

- File path from `BOE_WORKER_PROPOSAL_PATH`.
- Non-empty content after successful agent exit.

Validation:

- Runner checks existence and non-empty content only.
- Existing C# bridge validates schema, task id, worker id, permissions, proposal-only rules, and apply gate scope.
