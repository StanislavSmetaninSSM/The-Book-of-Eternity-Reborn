# GM Worker Bridges

Tracked issues: #1141, #1143, #1145, #1147.

GM worker bridges are subordinate helpers for the main GM. They can be Codex,
Gemini, or another CLI profile configured by the user. The main GM remains the
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
- If the worker times out, exits with an error, writes malformed JSON, returns a
  rejected proposal, or fails validation, the ready file is not created and the
  legacy repair loop remains active for the main GM/manual repair path.

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
