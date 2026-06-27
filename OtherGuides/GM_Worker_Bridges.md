# GM Worker Bridges

Tracked issues: #1141, #1143, #1145, #1147, #1149.

GM worker bridges are subordinate helpers for the main GM. They can be Codex
or another supported CLI profile configured by the user. The main GM remains the
only authority for the player turn, narration shown to the player, and canonical
game state.

## Visibility Contract

- Worker processes are launched hidden/background by default.
- The player should see only the main GM/client window.
- Worker status, failures, and proposal results must be surfaced through the
  main GM/daemon diagnostics and `game_state/control/gm_worker_audit.jsonl`.
- Do not ask the player to manage multiple worker console windows.

## Authority Contract

- Workers receive scoped task packets.
- Workers return `worker-proposal-v1` proposals.
- Workers must not edit canonical `game_session` files directly.
- Canonical writes happen only through the apply gate.
- The apply gate checks scope, reads proposal `contentRef` files, applies
  allowed changes, runs validation when required, and rolls back failed repairs.
- Stored proposals are inspectable through GM worker proposal inbox diagnostics.
  The inbox is read-only and does not apply proposal-only drafts.

## Recursive Delegation Cycle

Use workers as bounded RLM-like subcalls, not as alternate GMs.

1. Decide whether delegation is useful: use it for validation repair, narrative
   drafting, lore consistency checks, NPC/faction/QTE analysis, or afterlife
   content review when the main GM would otherwise block on a narrow subtask.
2. Send a scoped `WorkerTaskPacket`. The packet must include `role`,
   `taskType`, `timeoutSeconds`, read-only `contextFiles`,
   `allowedProposalPaths`, `responseContract`, `acceptanceCriteria`, and
   `forbiddenActions`.
3. Treat the result as a proposal. The main GM may quote, rewrite, reject, or
   ignore `draftText`/findings. The worker never decides the player turn.
4. For changed files, use the apply gate. Never copy a worker's proposed file
   content into canonical state by hand.
5. Record what happened. Worker dispatch/proposal/apply events are written to
   `game_state/control/gm_worker_audit.jsonl`, and compact summaries are copied
   into `game_state/control/gm_trajectory_ledger.jsonl` as `workerEvents[]` for
   live-test and harness review.

If a useful worker task cannot be expressed with these fields, record that as a
missing harness surface instead of giving a worker broad repository authority.

## Runtime Environment Contract

When the worker pool launches a worker task, it starts the configured
`launchCommand` hidden/background and provides these environment variables:

- `BOE_WORKER_TASK_PATH`: absolute path to the JSON `WorkerTaskPacket`.
- `BOE_WORKER_PROPOSAL_PATH`: absolute path where the worker must write one
  JSON `WorkerProposal`.
- `BOE_WORKER_SESSION_PATH`: absolute path to the current `game_session`
  directory.

The proposal file written to `BOE_WORKER_PROPOSAL_PATH` is an inbox response.
After validation, the main GM/daemon stores it under
`worker_proposals/<proposalId>/proposal.json`. Repair workers that include
`changedFiles` must write referenced content under `worker_proposals/<proposalId>/...`
and use safe relative `contentRef` paths. The worker must not overwrite
canonical files such as `game_state/...` directly.

If a worker CLI writes a valid proposal and only then times out or exits with a
nonzero code, the worker pool preserves the proposal and records it as
`proposal-received`. The abnormal exit remains diagnostic evidence, but the
proposal is still proposal-only: the main GM must review it and any canonical
change must still pass the apply gate.

## Local CLI Worker Runner

Worker profiles should prefer the repo-owned runner entrypoint instead of a
bare raw agent command. The runner reads the same `BOE_WORKER_*`
environment variables, builds a strict prompt with the task packet and proposal
contract, feeds that prompt to the configured agent command through UTF-8
stdin, and requires the agent to write a non-empty proposal handoff file.
The runner prompt must include a self-contained `worker-proposal-v1` JSON
skeleton with required fields such as `summary`, `status`, `changedFiles`,
`findings`, `selfCheck`, and `createdAtUtc`; workers must not need to read
implementation source to discover the proposal schema.
Hidden/background worker commands must be non-interactive; Codex workers should
use `codex exec ... -`, while the interactive `codex ...` command remains for
the visible main GM console.

Default settings expose disabled worker profile templates for common local
agents. They are safe to keep because `enabled` is `false`; the main GM cannot
route work to them until the user decides to enable one template explicitly and
confirms the local agent command works.

Runner path:

```text
BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1
```

Codex validation-repair example:

```json
{
  "workerId": "validation_repair_codex",
  "displayName": "Codex validation repair",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 180",
  "role": "validation-repair",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 210,
  "maxConcurrentTasks": 1
}
```

Codex narrative-draft example:

```json
{
  "workerId": "narrative_draft_codex",
  "displayName": "Codex narrative drafter",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "narrative-draft",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 150,
  "maxConcurrentTasks": 1
}
```

Codex analysis example:

```json
{
  "workerId": "analysis_codex",
  "displayName": "Codex analysis worker",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "analysis",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 150,
  "maxConcurrentTasks": 1
}
```

Use dry-run mode to inspect the generated prompt without launching an external
agent:

```powershell
.\BookOfEternityClient\Launcher\gm_worker_cli_runner.ps1 `
  -DryRun `
  -PromptOutPath "$env:TEMP\boe-worker-prompt.txt"
```

The runner is not an apply gate. It does not validate or apply proposal JSON;
`GmWorkerBridgePool`, the proposal store, and the apply gate remain responsible
for schema validation, scope checks, audit records, and canonical state writes.

## Proposal Inbox Diagnostics

The main GM/daemon can inspect saved proposals through worker proposal inbox
diagnostics. This surface is for GM review and troubleshooting, not a normal
player-facing command.

Each readable inbox entry exposes proposal id, worker id, task id, task type,
status, summary, creation time, draft-text presence, finding count, changed-file
count, changed file paths, self-check notes, related audit event types, and
known apply state. Malformed proposal JSON must not crash diagnostics; it
appears as an unreadable inbox entry with a reason.

## Proposal-Only Dispatch

The main GM/daemon can dispatch proposal-only worker tasks for narrative drafts
and analysis. This is a GM-facing bridge/diagnostic capability, not a normal
player command.

Supported proposal-only dispatch task types:

- `narrative-draft`: requires scene goal, tone, target length, optional
  continuity notes, and optional read-only context paths.
- `analysis`: requires analysis goal, optional questions, and optional
  read-only context paths.

Dispatch uses existing worker routing and the hidden/background launch path.
Context paths are sanitized, filtered through the worker profile read scope,
hashed, and sent as read-only references. Proposal-only workers must return
findings and/or `draftText` without `changedFiles`; if they return changed
files, the proposal is rejected by the worker proposal contract and canonical
state is not modified.

## Supported MVP Tasks

### validation-repair

Use this when validation reports state errors that are safe to repair through a
worker. The task packet includes validation issues, context file hashes, and
`allowedProposalPaths`. The worker returns changed files as proposal content
references. The apply gate accepts the proposal only if every path is allowed
and validation passes after applying the change.

Live dispatch status:

- As of #1143, validation-repair is the first task type wired into the live
  repair loop.
- The client still writes `game_state/control/validation_repair_request.json`
  first. This legacy request remains the fallback channel.
- If no enabled validation-repair profile exists, behavior is unchanged: no
  worker task, worker inbox, or audit file is created.
- If a worker is configured, the client launches it hidden/background through
  `GmWorkerBridgePool`, reads the proposal, and applies it only through the
  apply gate.
- If the apply gate accepts the proposal, the client creates
  `game_state/control/validation_repair_ready.json` with the active
  session/request/turn metadata so the existing repair loop can revalidate.
- If the worker times out, exits with an error before writing a valid proposal,
  writes malformed JSON, returns a rejected proposal, or fails validation, the
  ready file is not created and the legacy repair loop remains active for the
  main GM/manual repair path.

### narrative-draft

Use this when the main GM wants help drafting prose, analysis, or scene options
without delegating game authority. The task is proposal-only: the worker returns
`draftText` and optional findings. It must not include `changedFiles`.

Narrative-draft and analysis packets are defined by the bridge contract, but
they are not automatically dispatched from ordinary gameplay. They can be
requested through the GM-facing proposal-only dispatch surface.
When proposal-only entries exist in the inbox, diagnostics mark them as
`review-only`; they are suggestions for the main GM and are not applied to
canonical files.

## Main GM Checklist

1. Decide whether a worker is useful; do not delegate trivial work.
2. Pick an enabled worker profile whose `permissions.taskTypes` include the
   requested task type.
3. Send a scoped task packet, not broad freeform authority.
4. Read the proposal before using it.
5. For file changes, use the apply gate. Do not copy worker output into
   canonical files manually.
6. Record dispatch, proposal receipt, and apply decision in the worker audit log.

## Worked Examples

- `Examples/E_CLI_GM_Worker_Validation_Repair.txt`
- `Examples/E_CLI_GM_Worker_Narrative_Draft.txt`
