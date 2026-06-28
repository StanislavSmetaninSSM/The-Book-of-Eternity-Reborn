# Contract: GM Worker Bridge

## Authority Rules

1. The main GM owns the player turn, narration, and final game-state decision.
2. Worker bridges are subordinate agents and do not directly write canonical `game_session` state.
3. Worker output enters a proposal inbox.
4. Canonical changes occur only through the apply gate.
5. The apply gate rejects out-of-scope changes and failed validation.
6. Worker processes launch hidden/background by default; the player should not see multiple worker console windows.

## Runtime Launch Protocol

The worker pool starts the configured `launchCommand` hidden/background. For
each dispatched task, the worker process receives:

- `BOE_WORKER_TASK_PATH`: absolute path to the JSON `WorkerTaskPacket`.
- `BOE_WORKER_PROPOSAL_PATH`: absolute path where the process must write one
  JSON `WorkerProposal`.
- `BOE_WORKER_SESSION_PATH`: absolute path to the current `game_session`.

The proposal path is an inbox handoff path. After the proposal validates, the
main GM/daemon stores it under `worker_proposals/<proposalId>/proposal.json`.
Workers that propose file changes write content under
`worker_proposals/<proposalId>/...` and reference it with safe relative
`contentRef` values. Workers do not write canonical `game_state/...` files.

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
      "sha256": "example"
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

## Proposal Contract

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
      "beforeSha256": "example",
      "afterSha256": "example-after",
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

## Apply Gate Decision Contract

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

Every worker task must produce durable audit events for dispatch and terminal result. Apply decisions must include the proposal id and the decision result.

```json
{
  "schemaVersion": 1,
  "eventId": "worker_audit_20260620_0001",
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
