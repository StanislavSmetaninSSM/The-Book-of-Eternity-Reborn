# GM Worker Bridges

Tracked issue: #1141.

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

## Supported MVP Tasks

### validation-repair

Use this when validation reports state errors that are safe to repair through a
worker. The task packet includes validation issues, context file hashes, and
`allowedProposalPaths`. The worker returns changed files as proposal content
references. The apply gate accepts the proposal only if every path is allowed
and validation passes after applying the change.

### narrative-draft

Use this when the main GM wants help drafting prose, analysis, or scene options
without delegating game authority. The task is proposal-only: the worker returns
`draftText` and optional findings. It must not include `changedFiles`.

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
