# GM Worker Bridges

Tracked issues: #1141, #1143, #1145, #1147, #1149, #1231, #1232, #1233, #1500.

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
- Only status completed proposals can enter the apply gate. Status failed, timed-out, or rejected must use changedFiles: [].
- Status is mandatory; omission is invalid and must never default to completed.
- The apply gate checks scope, reads proposal `contentRef` files, applies
  allowed changes, runs validation when required, and rolls back failed repairs.
- The apply gate holds one canonical write lease from exact context/authority
  verification through all proposal writes, validation, read-only context
  revalidation, rollback when required, and the final accept/reject decision.
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

Every producer uses the collision-safe audit id shape `worker_audit_<UTC yyyyMMddHHmmssfff>_<32 lowercase hex GUID>`; do not hand-build timestamp-only event ids.

If a useful worker task cannot be expressed with these fields, record that as a
missing harness surface instead of giving a worker broad repository authority.

## Runtime Environment Contract

When the worker pool launches a worker task, it starts the configured
`launchCommand` hidden/background and provides these environment variables:

- `BOE_WORKER_TASK_PATH`: absolute path to the JSON `WorkerTaskPacket`.
- `BOE_WORKER_PROPOSAL_PATH`: absolute path where the worker must write one
  JSON `WorkerProposal`.
- `BOE_WORKER_SESSION_PATH`: absolute path to a detached execution snapshot
  under the client-owned `.worker_runtime` directory. The snapshot exposes only pinned task context,
  not the live canonical `game_session` directory.

The task, proposal inbox, and declared content files all live in that detached
execution snapshot. Direct worker writes to copied `game_state/...`, `lore/...`,
or any other snapshot path are discarded. After contract validation, the pool
imports only the validated proposal and its declared contentRef bytes into the
live proposal inbox; undeclared files are never imported. The detached
`.worker_runtime` directory is ephemeral and is removed after the task, so a
worker must not depend on it for durable state. This is a harness boundary for
the configured worker protocol, not an operating-system sandbox for a
deliberately malicious operator-supplied `launchCommand`.

The pool verifies every declared non-delete `contentRef` digest before importing any worker artifact.
One mismatched or missing artifact rejects the complete handoff, so a partially
published proposal cannot become review authority. Task and proposal identifiers are immutable:
reusing an existing identifier is a collision and never overwrites prior task,
proposal, or review evidence. If the process timeout and a malformed handoff
are both observed, timeout remains authoritative; the malformed proposal is
reported as additional diagnostic evidence rather than rewriting the outcome
as an ordinary failure.

The bridge atomically reserves every task and proposal identifier before the
corresponding durable handoff is published. Validation-repair task IDs are
globally unique per dispatch while retaining the repair-attempt number as a
readable prefix. `maxConcurrentTasks` is enforced at runtime for the same
worker and game session, including calls made through separate bridge-pool
instances; a queued task is not published until a worker slot is available.

Every validation-repair `contextFiles.sha256` and `changedFiles.beforeSha256`
must be the exact 64-character SHA-256 digest of the same canonical file bytes,
or the literal `missing` for an absent add target. Every non-delete
`afterSha256` must be the exact 64-character SHA-256 digest of its referenced
content bytes. A delete uses `afterSha256=missing` and no `contentRef`.
`contentRef` is proposal-bound and must be exactly
`worker_proposals/<proposalId>/<changedFiles.path>`.

For every changed entry, `changeKind is mandatory` and must be exactly `add`, `replace`, or `delete`.
Omission, zero/unspecified values, and unknown enum
values are invalid even when hashes and content otherwise look correct.

For an afterlife `validation-repair`,
`game_state/meta/soul_state.json` is hash-pinned read-only realm authority. Its
exact canonical bytes and SHA-256 must be present in `contextFiles`, its
`currentRealm` must agree with `afterlifeContract.realmGate` and
`afterlifeContract.currentRealm`, and it must not appear in `changedFiles` or
`allowedProposalPaths`. Missing, malformed, duplicate-key, unsupported,
changed-after-dispatch, or mismatched realm authority fails closed before any
canonical write.

Every `game_state/meta/` validation-repair target is afterlife-scoped, including
non-actor metadata. A mixed Mortal/afterlife issue batch fails task construction
closed instead of weakening either authority contract. Afterlife allowlists use
exact wildcard-free afterlife paths; `game_state/**`, `*`, `?`, and equivalent
patterns never grant repair authority. `lore/current_world/**` and
`game_state/core/player_status.json` are Mortal, including nested validation
coordinates beneath those files. Every exact afterlife validation-repair
allowlist entry must stay under `game_state/meta/`; merely failing to match a
known Mortal prefix is not repair authority. Typed afterlife content tasks may
still use exact task-provided control/report surfaces. The harness uses case-insensitive canonical path identity
for Windows session paths, rejects duplicate case aliases, and applies the same
identity to setting/Soul read-only authority and proposal scope. For
`npc_characteristics_empty`, `game_state/misc/characteristics.json` is likewise
read-only context and must never appear in `allowedProposalPaths` or
`changedFiles`.

The apply gate acquires one canonical write lease before its final exact-byte
context check. It retains that lease through every target compare/exchange,
full-state validation, read-only context recheck, rollback if necessary, and
decision linearization. A cooperating canonical writer waits; an external
non-cooperating mutation is detected by the final byte checks and rejects the
proposal without accepting mixed authority.

Built-in backup, restore, game-state clear, and current-world lore clear
operations acquire the same canonical write lease. Save and load operations use the same canonical write lease:
saves read one coherent snapshot, and a loaded
session cannot replace live state during worker apply. Detached runtime cleanup
never follows reparse points. A cleanup failure is an audit diagnostic and does
not replace an already completed, timed-out, or rejected worker result.

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
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 180",
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
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
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
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "analysis",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 150,
  "maxConcurrentTasks": 1
}
```

Codex inventory content-authoring example:

```json
{
  "workerId": "inventory_content_codex",
  "displayName": "Codex inventory content author",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "inventory-content",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 150,
  "maxConcurrentTasks": 1
}
```

Codex skill content-authoring example:

```json
{
  "workerId": "skill_content_codex",
  "displayName": "Codex skill content author",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "skill-content",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 150,
  "maxConcurrentTasks": 1
}
```

Codex NPC content-authoring example:

```json
{
  "workerId": "npc_content_codex",
  "displayName": "Codex NPC content author",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "npc-content",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 150,
  "maxConcurrentTasks": 1
}
```

Codex Guardian/Abode content-authoring example:

```json
{
  "workerId": "guardian_abode_content_codex",
  "displayName": "Codex Guardian/Abode content author",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "guardian-abode-content",
  "enabled": false,
  "launchVisibility": "hidden",
  "timeoutSeconds": 150,
  "maxConcurrentTasks": 1
}
```

Codex soul content-authoring example:

```json
{
  "workerId": "soul_content_codex",
  "displayName": "Codex soul content author",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "soul-content",
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
- content-authoring task types such as `inventory-content`, `skill-content`,
  `npc-content`, `guardian-abode-content`, and `soul-content`: require
  `authoringGoal`, optional `authoringDomain`, `entityHints`,
  `requiredLinks`, `outputNotes`, and optional read-only context paths. The
  worker returns a structured `authoringProposal`; it does not return
  `changedFiles`.

Dispatch uses existing worker routing and the hidden/background launch path.
Context paths are sanitized, filtered through the worker profile read scope,
hashed, and sent as read-only references. Proposal-only workers must return
findings and/or `draftText` without `changedFiles`; if they return changed
files, the proposal is rejected by the worker proposal contract and canonical
state is not modified.

Content-authoring proposals use the same `worker-proposal-v1` envelope as
other workers, but `authoringProposal` is mandatory. It must include:

- `domain`: the content domain, such as `inventory`;
- `goal`: the authoring goal copied or refined from the task packet;
- `createdEntities` / `updatedEntities`: proposed entities with ids, display
  names, summaries, required fields, and relationships;
- `requiredLinks`: links the main GM must create or verify before accepting the
  proposal;
- `validatorRisks`: likely validation risks and how the main GM can avoid them;
- `gmReviewNotes`: explicit notes for the main GM before using the proposal.

These proposals are review-only. The main GM can rewrite them into normal game
state updates, but the worker cannot apply them directly.

## Afterlife Realm-Aware Worker Contract

Afterlife work is not allowed to reuse Mortal World state shortcuts. If a
worker task touches Chaos Sea, Shining Abode, pending-bootstrap handoff,
guardian state, soul state, afterlife chronicles, or afterlife control files,
the `WorkerTaskPacket` must include `afterlifeContract`.

`afterlifeContract` must tell the worker:

- `realmGate`: `ChaosSea`, `ShiningAbode`, or `ShiningAbodePendingBootstrap`;
- `currentRealm`: the player-visible realm context;
- `progressionControlPaths`: relevant deterministic control files, usually
  under `game_state/control/`;
- `pendingControlFiles`: pending afterlife control files the main GM must
  respect;
- `allowedAfterlifeSurfaces`: exact afterlife state surfaces the worker may
  discuss in the proposal;
- `requiredReceipts` and `requiredReports`: receipts/reports the main GM must
  preserve if it accepts the proposal;
- `forbiddenMortalSubstitutes`: explicit forbidden shortcuts.

For proposal-only tasks with `afterlifeContract`, the worker proposal must
include `afterlifeProposal`. It must repeat the same `realmGate`, list only
target surfaces allowed by `allowedAfterlifeSurfaces`, include required receipts
and reports, provide a player-visible summary, and give `gmReviewNotes` plus
`validatorRisks` for the main GM. For `validation-repair`, `afterlifeProposal`
is optional: the authoritative repair payload is the bounded, hashed
`changedFiles` list, and every changed path must also be allowed by
`allowedAfterlifeSurfaces`.

Every `game_state/meta/` validation-repair target, including a non-actor state
file, binds the contract to the exact
hash-pinned read-only realm authority in `game_state/meta/soul_state.json`.
That authority is context only and must not appear in `changedFiles`; the apply
gate holds one canonical write lease while it checks the same bytes and realm,
applies all targets, validates the result, rechecks every read-only context, and
records the final decision. Mixed Mortal/afterlife issue batches fail task
construction closed.

The validator rejects afterlife proposals that try to use Mortal World
substitutes such as `worldStateFlags`, `worldEventsLog`, Mortal NPC
relationships, Mortal combat HP/status, Mortal factions, or Mortal map files.
It also rejects realm mismatches between `afterlifeContract.realmGate` and
`afterlifeProposal.realmGate`.

Before accepting any afterlife worker proposal, the main GM must review
`OtherGuides/Afterlife_Contract_Matrix.md`. Worker proposals are still
review-only: the main GM must rewrite accepted ideas through the normal
afterlife response surfaces and validation remains authoritative.

Additional inventory-content requirements:

- every proposed item must include a player-facing description field, not only
  an internal key or slot;
- every proposed item must be linked to an owner, inventory, storage, or
  container that the main GM will review before accepting it;
- every proposed item must include balance details such as value, price,
  quality, rarity, or a balance note;
- book/document items must link to readable content or explicitly flag the
  missing readable content as a GM review gap.

Additional skill-content requirements:

- every proposed skill/effect must include a detailed player-facing
  description, not only a short `+1` label;
- scaling from a characteristic must include localized display text and a
  readable scaling explanation; if there is no scaling, include
  `noScalingReason`;
- every proposed structured/mechanical bonus must include a player-facing
  explanation of when it applies;
- proposed skills/effects must link to the effect, status, combat,
  characteristic-check, or progression surface the main GM should update.

Additional npc-content requirements:

- every proposed NPC must include public knowledge, private knowledge, thought
  journal entries, relationship hooks, personal quest hooks, dialogue seeds,
  and detail menu/command surfaces as separate fields;
- NPC details must link to a current location/scene and to the faction, quest,
  relationship, thought, or dialogue surfaces that should reveal the data;
- the worker must not collapse thoughts, quests, relationships, and secrets
  into one summary paragraph.

Additional guardian-abode-content requirements:

- the task must include both `afterlifeContract` and `guardianAbodeRequest`;
- `guardianAbodeRequest` must name the realm, Guardian/Abode ids, pending
  afterlife control files, focus areas, and exact read scope;
- the proposal must include `authoringProposal`, `afterlifeProposal`, and
  `guardianAbodeProposal`;
- `guardianAbodeProposal` must split `guardianUpdates`, `abodeUpdates`,
  `projectSuggestions`, `powerReputationConsequences`, `tradeFavorHooks`,
  `dossierNotes`, required receipts/reports, validator risks, and main-GM
  review notes;
- hidden Guardian facts stay in GM-only fields. The worker must not place
  hidden dossier facts in `playerVisibleSummary` or visible items;
- Guardians, Abodes, Guardian projects, Guardian politics, and trade/favor hooks
  must use exact afterlife surfaces such as `game_state/meta/guardians.json`,
  `game_state/meta/guardian_projects.json`,
  `game_state/meta/abode_power_journal.json`, and
  `game_state/meta/chaos_sea_guardian_politics.json`;
- the worker must not model Guardians as Mortal NPCs and must not model Abodes
  or Guardian politics as Mortal factions.

Additional soul-content requirements:

- the task must include both `afterlifeContract` and `soulContentRequest`;
- `soulContentRequest` must name the realm, current soul context, requested
  scope, progression constraints, exact read scope, and player-owned identity
  fields;
- `soulName` and `soulFormDescription` are player-owned readonly identity. The
  worker may reference them as context or list them in
  `forbiddenReadonlyFields`, but it must not propose overwriting either field;
- the proposal must include `authoringProposal`, `afterlifeProposal`, and
  `soulContentProposal`;
- `soulContentProposal` must split `safeSoulSummaries`,
  `progressionSuggestions`, `rewardNotes`, `nextLifePreparationHooks`,
  `forbiddenReadonlyFields`, required receipts/reports, validator risks, and
  main-GM review notes;
- soul progression, rewards, archive notes, and next-life preparation must use
  exact afterlife surfaces such as `game_state/meta/soul_state.json`,
  `game_state/meta/afterlife_chronicles.json`, and relevant
  `game_state/control/` progression files;
- the worker must not model the player soul as an ordinary Mortal character,
  Mortal inventory, item, NPC, faction, world flag, or map state.

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
- The client removes stale request/ready/stall artifacts and attempts worker
  dispatch before the legacy `validation_repair_request.json` fallback is
  exposed. There is never a worker and legacy GM writing the same repair in
  parallel.
- If no enabled validation-repair profile exists, behavior is unchanged: no
  worker task, worker inbox, or audit file is created.
- If a worker is configured, the client launches it hidden/background through
  `GmWorkerBridgePool`, reads the proposal, and applies it only through the
  apply gate.
- If the apply gate accepts the proposal, the client creates
  `game_state/control/validation_repair_ready.json` with the active
  session/request/turn metadata so the existing repair loop can revalidate.
- If canonical apply succeeds but ready signal publication fails, the accepted
  worker remains the sole repair owner. The client records
  `worker_apply_gate_accepted` and revalidates directly instead of creating a
  legacy request or asking the main GM to repeat the repair.
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
- `Examples/E_CLI_GM_Worker_Content_Authoring.txt`
- `Examples/E_CLI_GM_Worker_Skill_Content.txt`
- `Examples/E_CLI_GM_Worker_Npc_Content.txt`
- `Examples/E_CLI_GM_Worker_Afterlife_Contract.txt`
- `Examples/E_CLI_GM_Worker_Guardian_Abode_Content.txt`
- `Examples/E_CLI_GM_Worker_Soul_Content.txt`
