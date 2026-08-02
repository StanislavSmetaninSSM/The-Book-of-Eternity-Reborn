# Contract: GM Worker Bridge

## Authority Rules

1. The main GM owns the player turn, narration, and final game-state decision.
2. Worker bridges are subordinate agents and do not directly write canonical `game_session` state.
3. Worker output enters a proposal inbox.
4. Canonical changes occur only through the apply gate.
5. The apply gate rejects out-of-scope changes and failed validation.
6. Worker processes launch hidden/background by default; the player should not see multiple worker console windows.
7. Production apply-gate construction requires `ValidationService`; no public
   empty-validator/fail-open constructor exists.

## Runtime Launch Protocol

The worker pool starts the configured `launchCommand` hidden/background. For
each dispatched task, the worker process receives:

- `BOE_WORKER_TASK_PATH`: absolute path to the JSON `WorkerTaskPacket`.
- `BOE_WORKER_PROPOSAL_PATH`: absolute path where the process must write one
  JSON `WorkerProposal`.
- `BOE_WORKER_SESSION_PATH`: absolute path to a detached execution snapshot
  under client-owned `.worker_runtime`; the snapshot exposes only pinned task context
  and is not the live canonical `game_session`.

Detached workspaces are stored outside the replaceable game session.
`BOE_WORKER_RUNTIME_BASE_PATH` may select an absolute runtime base. Without an
override, Windows sessions on a non-system volume use
`<session-volume>/BookOfEternityRuntime`; other sessions use local application
data, with the operating-system temporary directory only as a final fallback. A
canonical-session-path hash separates runtime roots. The override and derived
runtime root must not be equal to or nested under the canonical `game_session`;
the same containment check resolves filesystem identity and rejects a
reparse-point alias into the canonical session.

The protocol enforces fixed resource budgets: `proposal.json` is limited to
1 MiB, each declared `contentRef` to 4 MiB, all imported `contentRef` bytes
together to 16 MiB, and captured standard output plus captured standard error
individually to 65,536 characters. Output beyond 65,536 characters is discarded
with an explicit truncation marker. An oversized proposal or content artifact
rejects the handoff.

The proposal path and proposed content also live inside the detached execution
snapshot. Direct edits to copied state are discarded. After contract
validation, the pool imports only the validated proposal and its declared contentRef
bytes into the live proposal inbox; undeclared snapshot files never
cross that boundary. The client removes the ephemeral `.worker_runtime`
workspace after the task. This protocol does not claim operating-system
isolation from a deliberately malicious operator-supplied launch command.
The pool verifies every declared non-delete `contentRef` digest before importing any worker artifact.
Any mismatch rejects the complete handoff before proposal or artifact
publication. Task and proposal identifiers are immutable; an existing ID is a
collision and cannot overwrite earlier dispatch or review evidence. When a
process times out and leaves a malformed proposal, timeout remains authoritative
and the malformed handoff is retained only as an additional diagnostic.
The bridge reserves every task identifier with a create-only compare/exchange,
then publishes the complete proposal bundle through one create-only atomic directory rename.
The bundle contains `proposal.json` and every declared `contentRef`; no separate
proposal claim exists. While holding the canonical write lease, publication
verifies that the exact originating task bytes still belong to the current game
session generation. The durable bundle remains authority if derived inbox or
audit publication fails. Validation-repair task IDs are globally unique per
dispatch while retaining a readable attempt prefix. The proposal id `inbox` is
reserved for the derived inbox directory and is invalid for every proposal.
Staging and canonical-lease waiting observe caller cancellation and timeout.
Cancellation and publication use one atomic transition: cancellation before
the transition leaves no bundle, inbox, or applyable proposal; once publication
wins, the complete bundle rename remains authoritative and cannot be revoked by
late cancellation.
`maxConcurrentTasks` is
enforced at runtime for each worker and game session, including separate
bridge-pool instances. Worker slots use reference-counted gates that retire when idle;
an idle profile limit can change, while an active limit change fails as a worker
result. Cancellation kills and awaits the complete worker process tree before releasing its worker slot.
The durable session generation lives under `.boe_runtime/session-generation/current.json`
and rotates on load and New Game. Task reservation, proposal publication, and
apply bind to that exact generation, and save archives omit worker task/proposal
roots. Restoring byte-identical stale handoff evidence cannot revive an earlier
generation. A hidden client-owned process host ensures that the configured worker command cannot start until process-tree ownership is attached.

At apply time, the exact durable reserved task is the sole apply authority. The
gate reloads it under the canonical lease and does not accept a mutable
caller-provided task as authority; task reservation returns a deep copy of the
exact persisted bytes. `sessionGeneration` is lowercase canonical GUID text in
`N` format. A generation rotation produces typed `SessionReplaced` and aborts
the old repair; no legacy fallback or rollback may write into the replacement
session. Repair trajectory evidence uses a generation-bound atomic append, and
the latest validation-repair task is ephemeral and excluded from saves.
Committed apply cleanup deletes the transaction directory before the active
journal so cleanup failure remains retryable without revoking accepted bytes.

Every complete logical GM interaction is an immutable session operation bound to
the generation captured at its outer entrypoint. Each canonical mutation
rechecks that generation after recovery and under the canonical write lease,
terminal polling checks it before consuming completion signals, and the outer
operation performs one final durable check before returning. Once replacement is
observed, nested catches cannot clear the replacement state. The client must not
hold the lifecycle lease while waiting for the GM; that short lease serializes
only load/New Game replacement. Typed `SessionReplaced` aborts every remaining
old-turn write, rollback, cleanup, ready publication, and telemetry path.

The parent creates private current-user named control/status pipe servers with
unique endpoint names and starts the hidden host with only the two endpoint
names and the per-launch nonce in its command line. The configured worker
payload never appears in the hidden-host command line, and no worker-accessible
marker files exist. After both channels connect, parent-side client PID
authentication authenticates both connected pipe clients as the exact
hidden-host PID. The parent only after authentication sends a typed `Launch`
frame containing the executable, arguments, working directory, and environment.
The hidden host retains both channels; the configured worker receives neither
channel nor any pipe handle.

Unknown, duplicate, or missing frame fields are rejected. The host emits typed
`Ready` after accepting the launch payload while the configured worker remains
stopped. The parent sends typed `Release`, and only then may the host start the
configured worker. Typed `Completed` requires a non-null exit code and is
published immediately after the direct worker exits, before output pipes are
drained. Typed `OutputDrained` follows bounded output capture and is awaited
before ordinary host teardown; this explicit `OutputDrained` acknowledgement is
diagnostic and cannot replace the authoritative completion. These are complete
typed frames bound to the per-launch nonce.

A proposal becomes applyable only after confirmed zero exit and confirmed process-tree termination.
A timeout, cancellation, nonzero exit, missing exit
code, incomplete process-tree cleanup, or uncertain host cleanup is
diagnostic-only: any bytes written by that execution must not be imported as a
worker proposal or passed to the apply gate.

Windows Job Object is the supported complete descendant boundary. Platforms
without an equivalent queryable kernel containment boundary fail closed before
worker release. Timeout and cancellation remain authoritative; cleanup
uncertainty quarantines the worker slot rather than changing the result or
releasing uncertain capacity. Complete-tree termination and unattached-host
cleanup is bounded. One fixed-capacity reaper entry retains the complete
process/workspace owner across bounded retries; it releases the slot and removes
the workspace exactly once only after death and process-tree authority disposal
are confirmed. Before release, the reaper durably records one stable-id
`process-tree-cleanup-confirmed` event. The generation-bound canonical append is
idempotent; after generation replacement, a create-only retained receipt is
written under `.worker_runtime/quarantine-audit/<eventId>.json`. An uncertain
audit write retains the same entry and slot for retry.
Validation-repair hashes bind exact bytes: `contextFiles.sha256` and
`changedFiles.beforeSha256` are the same exact 64-character SHA-256 digest (or
`missing` for an absent add target), while each non-delete `afterSha256` is the
exact 64-character SHA-256 digest of its `contentRef` bytes. A delete uses
`afterSha256=missing` and no `contentRef`. Every non-empty `contentRef` must be
exactly `worker_proposals/<proposalId>/<changedFiles.path>`.

For each file entry, `changeKind is mandatory` and must be exactly `add`, `replace`, or `delete`.
Missing, zero/unspecified, and undefined enum values are
invalid and cannot be inferred from hashes, file existence, or `contentRef`.

For afterlife `validation-repair`, `game_state/meta/soul_state.json` is the
hash-pinned read-only realm authority. The task must carry its exact canonical
SHA-256 in `contextFiles`; `currentRealm` must match both
`afterlifeContract.realmGate` and `afterlifeContract.currentRealm`; and the
authority path must not appear in `changedFiles` or `allowedProposalPaths`.
Missing, malformed, duplicate-key, unsupported, changed-after-dispatch, or
mismatched authority fails closed before canonical writes.

Every `game_state/meta/` validation-repair target is afterlife-scoped,
including non-actor metadata. Mixed Mortal/afterlife issue batches fail task
construction closed. Allowlists contain exact wildcard-free afterlife paths;
glob patterns such as `game_state/**`, `*`, and `?` never grant authority. The
paths `lore/current_world/**` and `game_state/core/player_status.json` are Mortal,
including nested validation coordinates. Every afterlife validation-repair
allowlist entry must stay under `game_state/meta/`; an unknown non-Mortal path
is not afterlife repair authority. Typed afterlife content tasks may use exact
task-provided control/report surfaces. The
bridge uses case-insensitive canonical path identity for Windows session paths,
rejects duplicate case aliases, and applies that identity to context, proposal
scope, and setting/Soul read-only authority. `game_state/misc/characteristics.json` is read-only
context for `npc_characteristics_empty` and cannot be writable even in a mixed
Mortal repair.

The apply gate holds one canonical write lease from the final exact-byte
context and authority check through every target compare/exchange, full-state
validation, read-only context revalidation, rollback when required, and final
decision linearization. Cooperating canonical writers wait for that decision;
detected external authority changes reject the complete proposal.
The apply gate has no replaceable lock inside `game_session`; the external lease
is its sole exclusion authority. Built-in backup, restore, game-state clear, and
current-world lore clear use the same lease. Save and load operations use the
same canonical write lease so a save is one coherent snapshot and load cannot
replace live state during apply. Load uses an external durable journal under
`.boe_runtime/load-transactions`: startup restores an interrupted swap before
creating session directories, and failed rollback preserves the backup and
journal for a later recovery attempt. State refresh and client-owned mirror
repair are one lease-scoped read/modify/write. Every canonical writer must
recover an interrupted load transaction immediately after acquiring the canonical write lease
or fail closed before it touches live state. Worker apply uses an external
durable journal under `.boe_runtime/worker-apply-transactions`; intent,
before-images, and expected applied hashes are durable before the first canonical
write. Every canonical writer recovers an interrupted worker apply transaction
after lease acquisition or fails closed before mutation. Once committed, accepted
canonical bytes remain authoritative: committed journal cleanup cannot roll back
accepted bytes and may be retried.
Detached workspace deletion never follows reparse points; a cleanup failure is an audit diagnostic
and cannot replace the worker result.

## Worker Profile Contract

Validation repair worker:

```json
{
  "workerId": "validation_repair_codex",
  "displayName": "Codex validation repair",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 180",
  "role": "validation-repair",
  "enabled": true,
  "launchVisibility": "hidden",
  "timeoutSeconds": 180,
  "maxConcurrentTasks": 1,
  "permissions": {
    "taskTypes": ["validation-repair"],
    "readPaths": [
      "game_state/**",
      "lore/**",
      "input/**",
      "ready/**"
    ],
    "proposalWritePaths": [
      "game_state/**",
      "lore/**",
      "ready/**"
    ],
    "proposalOnly": false,
    "requiresValidation": true
  }
}
```

Narrative drafting worker:

```json
{
  "workerId": "narrative_draft_codex",
  "displayName": "Codex narrative drafter",
  "launchCommand": "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1\" -AgentCommand \"codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -\" -TimeoutSeconds 120",
  "role": "narrative-draft",
  "enabled": true,
  "launchVisibility": "hidden",
  "timeoutSeconds": 120,
  "maxConcurrentTasks": 1,
  "permissions": {
    "taskTypes": ["narrative-draft"],
    "readPaths": [
      "game_state/**",
      "lore/**",
      "Rules/**",
      "TaskGuides/**"
    ],
    "proposalWritePaths": [],
    "proposalOnly": true,
    "requiresValidation": false
  }
}
```

## Task Packet Contract

Validation repair task:

```json
{
  "schemaVersion": 1,
  "taskId": "worker_task_20260620_0001",
  "sessionGeneration": "11111111111111111111111111111111",
  "workerId": "validation_repair_codex",
  "taskType": "validation-repair",
  "createdAtUtc": "2026-06-20T00:00:00Z",
  "sourceTurn": {
    "sessionId": "test-session",
    "requestId": "test-request",
    "turnNumber": 12
  },
  "validationIssues": [
    {
      "code": "normalized_weather_missing_description",
      "path": "game_state/world/weather.json",
      "message": "normalizedWeatherState.description is required."
    }
  ],
  "contextFiles": [
    {
      "path": "game_state/world/weather.json",
      "sha256": "6cc0139810cee57e761b4f0549d4300004989eaceaf72e880be9265e5517947b"
    }
  ],
  "allowedProposalPaths": [
    "game_state/world/weather.json"
  ],
  "responseContract": "worker-proposal-v1",
  "instructions": "Return a minimal repair proposal. Do not change files outside allowedProposalPaths."
}
```

Narrative draft task:

```json
{
  "schemaVersion": 1,
  "taskId": "worker_task_20260620_0002",
  "sessionGeneration": "11111111111111111111111111111111",
  "workerId": "narrative_draft_codex",
  "taskType": "narrative-draft",
  "createdAtUtc": "2026-06-20T00:05:00Z",
  "sourceTurn": {
    "sessionId": "test-session",
    "requestId": "test-request",
    "turnNumber": 12
  },
  "validationIssues": [],
  "draftRequest": {
    "sceneGoal": "Draft a tense description of the locked manor corridor before the player chooses how to proceed.",
    "tone": "dark fantasy, concise, natural Russian prose",
    "continuityNotes": [
      "The player is currently inside the mortal world.",
      "Do not resolve the player's action.",
      "Do not introduce canonical state changes."
    ],
    "targetLength": "120-180 words"
  },
  "contextFiles": [
    {
      "path": "game_state/world/current_location.json",
      "sha256": "example"
    }
  ],
  "allowedProposalPaths": [],
  "responseContract": "worker-proposal-v1",
  "instructions": "Return draftText and optional findings only. Do not include changedFiles."
}
```

Afterlife realm-aware analysis task:

```json
{
  "schemaVersion": 1,
  "taskId": "worker_task_afterlife_contract_0001",
  "sessionGeneration": "11111111111111111111111111111111",
  "workerId": "analysis_codex",
  "taskType": "analysis",
  "createdAtUtc": "2026-06-20T02:15:00Z",
  "sourceTurn": {
    "sessionId": "test-session",
    "requestId": "test-request",
    "turnNumber": 17
  },
  "contextFiles": [
    {
      "path": "game_state/meta/soul_state.json",
      "sha256": "example"
    },
    {
      "path": "OtherGuides/Afterlife_Contract_Matrix.md",
      "sha256": "example"
    }
  ],
  "afterlifeContract": {
    "realmGate": "ChaosSea",
    "currentRealm": "Chaos Sea",
    "progressionControlPaths": ["game_state/control/progression_schedule.json"],
    "pendingControlFiles": ["game_state/control/pending_dice_state.json"],
    "allowedAfterlifeSurfaces": [
      "game_state/meta/guardians.json",
      "game_state/meta/afterlife_chronicles.json",
      "game_state/meta/afterlife_global_flags.json"
    ],
    "requiredReceipts": ["afterlifeChronicleUpdates"],
    "requiredReports": ["progressionProcessingReport"],
    "forbiddenMortalSubstitutes": [
      "worldStateFlags",
      "worldEventsLog",
      "Mortal NPC relationships",
      "Mortal combat HP/status",
      "Mortal factions or map files"
    ]
  },
  "allowedProposalPaths": [],
  "responseContract": "worker-proposal-v1",
  "instructions": "Return findings and afterlifeProposal only. Use Afterlife_Contract_Matrix.md for exact afterlife state surfaces."
}
```

Guardian/Abode content-authoring task:

```json
{
  "schemaVersion": 1,
  "taskId": "worker_task_guardian_abode_content_0001",
  "sessionGeneration": "11111111111111111111111111111111",
  "workerId": "guardian_abode_content_codex",
  "role": "guardian-abode-content",
  "taskType": "guardian-abode-content",
  "createdAtUtc": "2026-06-20T03:15:00Z",
  "sourceTurn": {
    "sessionId": "test-session",
    "requestId": "test-request",
    "turnNumber": 18
  },
  "authoringRequest": {
    "domain": "guardian-abode",
    "goal": "Prepare Guardian and Abode project suggestions.",
    "entityHints": ["guardian_azalia", "abode_azalia_memory_silk_001"],
    "requiredLinks": ["active Guardian", "current Abode", "guardian project tracker"],
    "outputNotes": ["Keep hidden Guardian facts GM-only."]
  },
  "guardianAbodeRequest": {
    "realm": "Chaos Sea",
    "guardianIds": ["guardian_azalia"],
    "abodeIds": ["abode_azalia_memory_silk_001"],
    "pendingControlFiles": [
      "game_state/control/system_guardian_attraction.json",
      "game_state/control/afterlife_return_guard.json"
    ],
    "focusAreas": ["guardian dossier", "abode project", "abode power", "trade favor", "guardian politics"],
    "readScope": [
      "game_state/meta/guardians.json",
      "game_state/meta/guardian_projects.json",
      "game_state/meta/abode_power_journal.json",
      "game_state/meta/chaos_sea_guardian_politics.json"
    ]
  },
  "afterlifeContract": {
    "realmGate": "ChaosSea",
    "currentRealm": "Chaos Sea",
    "progressionControlPaths": ["game_state/control/progression_schedule.json"],
    "pendingControlFiles": [
      "game_state/control/system_guardian_attraction.json",
      "game_state/control/afterlife_return_guard.json"
    ],
    "allowedAfterlifeSurfaces": [
      "game_state/meta/guardians.json",
      "game_state/meta/guardian_projects.json",
      "game_state/meta/abode_power_journal.json",
      "game_state/meta/chaos_sea_guardian_politics.json",
      "game_state/meta/afterlife_chronicles.json"
    ],
    "requiredReceipts": ["guardianProjectUpdates", "guardianPowerEvents"],
    "requiredReports": ["progressionProcessingReport"],
    "forbiddenMortalSubstitutes": ["UpdateNPCs", "NPCRelationshipChanges", "factionDataChanges", "worldMapUpdates"]
  },
  "allowedProposalPaths": [],
  "responseContract": "worker-proposal-v1",
  "instructions": "Return authoringProposal, afterlifeProposal, and guardianAbodeProposal. Do not model Guardians as Mortal NPCs or Abodes as Mortal factions."
}
```

For proposal-only tasks, an `afterlifeContract` requires a matching
`afterlifeProposal`. For `validation-repair`, `afterlifeProposal` is optional:
the repair authority is the bounded `changedFiles` list, and every changed path
must be included in both `allowedProposalPaths` and
`afterlifeContract.allowedAfterlifeSurfaces`.

The afterlife repair contract is derived from the hash-pinned read-only realm
authority in `game_state/meta/soul_state.json`, not from caller-supplied realm
text. That file must not appear in `changedFiles`; the apply gate verifies its
same exact bytes and realm again immediately before applying the proposal.

## Proposal Contract

Only status completed proposals can enter the apply gate. Status failed, timed-out, or rejected must use changedFiles: [].
Status is mandatory; omission is invalid and must never default to completed.

Validation repair proposal:

```json
{
  "schemaVersion": 1,
  "proposalId": "worker_proposal_20260620_0001",
  "taskId": "worker_task_20260620_0001",
  "workerId": "validation_repair_codex",
  "status": "completed",
  "summary": "Added the missing normalized weather description.",
  "changedFiles": [
    {
      "path": "game_state/world/weather.json",
      "changeKind": "replace",
      "beforeSha256": "6cc0139810cee57e761b4f0549d4300004989eaceaf72e880be9265e5517947b",
      "afterSha256": "6691bcf05a185d27168baad72602bf3a4faea235646948b7d84c5d9eede4b8be",
      "contentRef": "worker_proposals/worker_proposal_20260620_0001/game_state/world/weather.json"
    }
  ],
  "findings": [],
  "selfCheck": {
    "scopeReviewed": true,
    "validationExpectedToPass": true,
    "notes": []
  },
  "createdAtUtc": "2026-06-20T00:00:15Z"
}
```

Before dispatching a validation-repair worker, the client removes stale legacy
request, ready, and stall artifacts. It exposes
`validation_repair_request.json` only if worker dispatch or apply does not
succeed. Once the apply gate accepts canonical bytes, worker ownership is
final: a later ready signal publication failure triggers direct revalidation
and a `worker_apply_gate_accepted` trajectory record, not a second legacy GM
repair.

Narrative draft proposal:

```json
{
  "schemaVersion": 1,
  "proposalId": "worker_proposal_20260620_0002",
  "taskId": "worker_task_20260620_0002",
  "workerId": "narrative_draft_codex",
  "status": "completed",
  "summary": "Drafted corridor narration for main-GM review.",
  "changedFiles": [],
  "findings": [
    {
      "kind": "continuity-note",
      "message": "Draft avoids resolving the player's next action."
    }
  ],
  "draftText": "Черновик сцены для главного ГМа. Этот текст не показывается игроку автоматически.",
  "selfCheck": {
    "scopeReviewed": true,
    "validationExpectedToPass": true,
    "notes": [
      "Proposal-only task; no file changes included."
    ]
  },
  "createdAtUtc": "2026-06-20T00:05:20Z"
}
```

Afterlife realm-aware proposal:

```json
{
  "schemaVersion": 1,
  "proposalId": "worker_proposal_afterlife_contract_0001",
  "taskId": "worker_task_afterlife_contract_0001",
  "workerId": "analysis_codex",
  "status": "completed",
  "summary": "Prepared an afterlife realm-aware proposal for main-GM review.",
  "changedFiles": [],
  "findings": [
    {
      "kind": "afterlife-contract-note",
      "message": "Use afterlife chronicles and guardian state surfaces named in the task packet."
    }
  ],
  "draftText": null,
  "authoringProposal": null,
  "afterlifeProposal": {
    "realmGate": "ChaosSea",
    "targetSurfaces": [
      "game_state/meta/guardians.json",
      "game_state/meta/afterlife_chronicles.json"
    ],
    "requiredReceipts": ["afterlifeChronicleUpdates"],
    "requiredReports": ["progressionProcessingReport"],
    "playerVisibleSummary": "В Море Хаоса нужно обновить хронику и реакцию хранителя через поверхности посмертия.",
    "gmReviewNotes": [
      "Review Afterlife_Contract_Matrix.md before accepting.",
      "Keep hidden guardian motives out of player-visible output."
    ],
    "validatorRisks": [
      {
        "code": "afterlife_surface_receipt_required",
        "message": "Afterlife updates need exact receipts and reports.",
        "mitigation": "Use afterlifeChronicleUpdates and progressionProcessingReport surfaces only."
      }
    ]
  },
  "selfCheck": {
    "scopeReviewed": true,
    "validationExpectedToPass": true,
    "notes": [
      "Proposal-only afterlife contract task; no file changes included."
    ]
  },
  "createdAtUtc": "2026-06-20T02:15:20Z"
}
```

Guardian/Abode content proposal:

```json
{
  "schemaVersion": 1,
  "proposalId": "worker_proposal_guardian_abode_content_0001",
  "taskId": "worker_task_guardian_abode_content_0001",
  "workerId": "guardian_abode_content_codex",
  "status": "completed",
  "summary": "Prepared Guardian and Abode proposal for main-GM review.",
  "changedFiles": [],
  "findings": [],
  "draftText": null,
  "authoringProposal": {
    "domain": "guardian-abode",
    "goal": "Prepare Guardian and Abode project suggestions.",
    "createdEntities": [
      {
        "entityType": "guardian-project",
        "entityId": "project_azalia_memory_silk",
        "displayName": "Шёлковая память Азалии",
        "summary": "Проект Обители, который укрепляет память и долг перед Азалией.",
        "requiredFields": [
          {
            "name": "playerFacingSummary",
            "value": "Азалия предлагает укрепить Обитель через нити памяти."
          },
          {
            "name": "gmOnlyHiddenFacts",
            "value": "GM-only: проект также проверяет долг памяти."
          },
          {
            "name": "exactAfterlifeSurfaces",
            "value": "game_state/meta/guardian_projects.json; game_state/meta/abode_power_journal.json"
          }
        ],
        "relationships": ["guardian_azalia", "abode_azalia_memory_silk_001"]
      }
    ],
    "updatedEntities": [],
    "requiredLinks": [
      {
        "source": "project_azalia_memory_silk",
        "target": "game_state/meta/guardian_projects.json",
        "reason": "The main GM must accept the proposal through Guardian project surfaces."
      }
    ],
    "validatorRisks": [
      {
        "code": "guardian_abode_surface_required",
        "message": "Guardian/Abode proposals are invalid if rewritten as Mortal NPC or Mortal faction updates.",
        "mitigation": "Use guardianAbodeProposal plus exact afterlife surfaces."
      }
    ],
    "gmReviewNotes": ["Keep hidden Guardian motives GM-only."]
  },
  "afterlifeProposal": {
    "realmGate": "ChaosSea",
    "targetSurfaces": [
      "game_state/meta/guardians.json",
      "game_state/meta/guardian_projects.json",
      "game_state/meta/abode_power_journal.json"
    ],
    "requiredReceipts": ["guardianProjectUpdates", "guardianPowerEvents"],
    "requiredReports": ["progressionProcessingReport"],
    "playerVisibleSummary": "Азалия предлагает укрепить Обитель через проект памяти.",
    "gmReviewNotes": ["Review Afterlife_Contract_Matrix.md before accepting."],
    "validatorRisks": [
      {
        "code": "guardian_project_receipt_required",
        "message": "Guardian project and Abode Power changes need matching receipts.",
        "mitigation": "Use guardianProjectUpdates and guardianPowerEvents only if accepted."
      }
    ]
  },
  "guardianAbodeProposal": {
    "playerVisibleSummary": "Азалия предлагает укрепить Обитель через проект памяти.",
    "guardianUpdates": [
      {
        "itemId": "guardian_update_azalia_focus",
        "targetId": "guardian_azalia",
        "title": "Позиция Азалии",
        "summary": "Азалия открыто поддерживает укрепление Обители через память.",
        "visibility": "visible",
        "targetSurfaces": ["game_state/meta/guardians.json"],
        "fields": [
          {
            "name": "relationshipCue",
            "value": "visible Guardian attitude on afterlife Guardian surfaces"
          }
        ]
      }
    ],
    "abodeUpdates": [
      {
        "itemId": "abode_update_memory_silk",
        "targetId": "abode_azalia_memory_silk_001",
        "title": "Нити памяти",
        "summary": "Обитель получает проект, связанный с памятью и долгом.",
        "visibility": "visible",
        "targetSurfaces": ["game_state/meta/guardians.json"],
        "fields": [
          {
            "name": "abodeCue",
            "value": "current Abode project context"
          }
        ]
      }
    ],
    "projectSuggestions": [
      {
        "itemId": "project_suggestion_memory_silk",
        "targetId": "project_azalia_memory_silk",
        "title": "Шёлковая память",
        "summary": "Проект можно начать как медленное укрепление Обители.",
        "visibility": "visible",
        "targetSurfaces": ["game_state/meta/guardian_projects.json"],
        "fields": [
          {
            "name": "projectType",
            "value": "abode_memory_fortification"
          }
        ]
      }
    ],
    "powerReputationConsequences": [
      {
        "itemId": "power_consequence_memory_silk",
        "targetId": "abode_azalia_memory_silk_001",
        "title": "Резонанс Обители",
        "summary": "Принятие проекта может дать малый рост силы Обители и доверия Азалии.",
        "visibility": "visible",
        "targetSurfaces": ["game_state/meta/abode_power_journal.json", "game_state/meta/guardians.json"],
        "fields": [
          {
            "name": "powerDelta",
            "value": "small positive if main GM accepts"
          }
        ]
      }
    ],
    "tradeFavorHooks": [
      {
        "itemId": "trade_favor_azalia_thread",
        "targetId": "guardian_azalia",
        "title": "Услуга Азалии",
        "summary": "Азалия может предложить услугу обмена памятью, если проект принят.",
        "visibility": "visible",
        "targetSurfaces": ["game_state/meta/guardians.json"],
        "fields": [
          {
            "name": "favorHook",
            "value": "trade/favor hook for Guardian review"
          }
        ]
      }
    ],
    "dossierNotes": [
      {
        "itemId": "dossier_hidden_dependency",
        "targetId": "guardian_azalia",
        "title": "Скрытый долг Азалии",
        "summary": "GM-only: Азалия проверяет долг памяти.",
        "visibility": "gm-only",
        "targetSurfaces": ["game_state/meta/chaos_sea_guardian_politics.json"],
        "fields": [
          {
            "name": "hiddenFact",
            "value": "hidden_dependency politics; never show in player-visible summary"
          }
        ]
      }
    ],
    "requiredReceipts": ["guardianProjectUpdates", "guardianPowerEvents"],
    "requiredReports": ["progressionProcessingReport"],
    "validatorRisks": [
      {
        "code": "guardian_abode_hidden_leak",
        "message": "Hidden Guardian politics must stay GM-only.",
        "mitigation": "Keep hidden dossier notes out of playerVisibleSummary."
      }
    ],
    "gmReviewNotes": ["Use Afterlife_Contract_Matrix.md examples 16, 20, and 26E before accepting."]
  },
  "selfCheck": {
    "scopeReviewed": true,
    "validationExpectedToPass": true,
    "notes": ["Proposal-only Guardian/Abode task; no file changes included."]
  },
  "createdAtUtc": "2026-06-20T03:15:20Z"
}
```

## Apply Gate Decision Contract

An apply decision represents one atomic authority interval. The gate has held
the canonical write lease across context verification, all target writes,
validation, read-only context revalidation, any rollback, and this decision.
No accepted decision may be based on authority bytes that changed during that
interval.

```json
{
  "schemaVersion": 1,
  "decisionId": "apply_decision_20260620_0001",
  "proposalId": "worker_proposal_20260620_0001",
  "result": "accepted",
  "scopeCheck": {
    "passed": true,
    "checkedPaths": ["game_state/world/weather.json"],
    "violations": []
  },
  "validationCheck": {
    "required": true,
    "passed": true,
    "command": "ValidateGameStateAsync",
    "issueCount": 0
  },
  "appliedFiles": [
    "game_state/world/weather.json"
  ],
  "rejectionReasons": [],
  "decidedAtUtc": "2026-06-20T00:00:30Z"
}
```

## Audit Event Contract

Every worker task must produce durable audit events for dispatch and terminal result. Apply decisions must include the proposal id and the decision result. Every producer uses `worker_audit_<UTC yyyyMMddHHmmssfff>_<32 lowercase hex GUID>` so concurrent events retain readable UTC ordering without timestamp-only collisions.

```json
{
  "schemaVersion": 1,
  "eventId": "worker_audit_20260620000030000_00112233445566778899aabbccddeeff",
  "eventType": "proposal-applied",
  "workerId": "validation_repair_codex",
  "taskId": "worker_task_20260620_0001",
  "proposalId": "worker_proposal_20260620_0001",
  "timestampUtc": "2026-06-20T00:00:30Z",
  "summary": "Worker repair proposal accepted after validation.",
  "details": {
    "appliedFiles": ["game_state/world/weather.json"]
  }
}
```
