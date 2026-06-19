# Feature Specification: Explicit GM Worker Bridges

**Feature Branch**: `1113-gm-worker-bridges`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "Add explicit multi-agent architecture where the main GM can launch additional configured bridges, delegate validation repair, narrative drafting, lore/NPC/QTE analysis, console-output review, and other scoped work to Codex/Gemini/other agents, receive proposals, and keep the main GM as the single owner of turn and canonical state."

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1141 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1141
- **Issue type**: epic / enhancement / architecture
- **Spec Kit justification**: This changes GM bridge orchestration, general worker task dispatch, validation repair flow, proposal-only creative/analysis flow, runtime state authority, diagnostics, docs, examples, and future agent handoff contracts. It is cross-cutting and expected to span multiple sessions.
- **Contract scope**: GM-facing prompts, runtime-state, validation, docs, examples, console diagnostics, agent-console/e2e.
- **Out of scope**: Browser UI redesign, direct worker writes to canonical `game_session`, autonomous co-GM control of player turns, remote/cloud orchestration, and automatic acceptance of worker-authored narrative without main-GM review. Proposal-only narrative drafting and analysis are in scope for the first implementation wave.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Delegate Validation Repair Safely (Priority: P1)

When a live GM turn fails validation, the main GM can delegate the validation errors and scoped state snapshot to a configured worker bridge. The worker returns a repair proposal, and the main GM/daemon applies it only after scope and validation checks pass.

**Why this priority**: Validation repair is the most concrete and safest use case. It provides immediate value for live play without allowing multiple agents to mutate canonical state directly.

**Independent Test**: A fixture with a failing accepted-turn validation dispatches a worker task, records the worker proposal, applies it through the gate, and ends with validation passing while the audit log attributes the repair to the worker.

**Acceptance Scenarios**:

1. **Given** a validation failure and one configured worker bridge, **When** the main GM delegates repair, **Then** the worker receives a task packet containing validation issues, allowed files, and read-only context.
2. **Given** a worker proposal that only changes allowed files, **When** the apply gate validates the proposal, **Then** the proposal is accepted and canonical state changes are attributed in audit output.
3. **Given** a worker proposal that changes a forbidden file, **When** the apply gate validates the proposal, **Then** the proposal is rejected and no canonical state changes are applied.

---

### User Story 2 - Configure and Observe Worker Bridges (Priority: P2)

The user can define worker bridge profiles with command, role, permissions, and lifecycle policy. Runtime diagnostics show whether each worker is stopped, starting, ready, busy, failed, or disabled.

**Why this priority**: Explicit profiles make the architecture user-controlled and auditable. Workers must still run hidden/background so the player does not get multiple visible agent windows.

**Independent Test**: A settings fixture defines two workers, starts one, disables another, and verifies status files and console diagnostics show the correct state without starting unauthorized commands.

**Acceptance Scenarios**:

1. **Given** a configured worker profile, **When** the daemon starts the worker pool, **Then** the worker launches hidden/background with its configured CLI command and role label.
2. **Given** a worker launch failure, **When** diagnostics are read, **Then** the failure reason is visible and no task is dispatched to that worker.
3. **Given** a disabled worker profile, **When** the worker pool starts, **Then** the disabled worker is listed but not launched.

---

### User Story 3 - Route Proposal-Only Narrative and Analytical Tasks (Priority: P2)

The main GM can delegate non-authoritative creative and analytical tasks, such as drafting scene narration while the GM checks state, NPC consistency review, lore suggestions, QTE scene suggestions, or player-facing console output audits. Workers return suggestions only; the main GM chooses what to use, edits it if needed, and remains responsible for final narration.

**Why this priority**: This is the core value of multi-agent orchestration beyond validation repair. It proves that the worker system is a general delegation mechanism while preserving main-GM authority.

**Independent Test**: A narrative-draft task is sent to a worker with read-only context, the worker returns a draft scene, and the daemon records it in the proposal inbox without changing canonical state.

**Acceptance Scenarios**:

1. **Given** the main GM is preparing a scene, **When** it delegates a narrative-draft task, **Then** the worker returns draft narration that is visible to the main GM but not automatically sent to the player.
2. **Given** a proposal-only task type, **When** a worker returns suggestions, **Then** the suggestions appear in the proposal inbox and no files are applied automatically.
3. **Given** a worker response that includes direct file changes for a proposal-only task, **When** the apply gate evaluates it, **Then** file changes are rejected and the textual suggestions remain available.
4. **Given** no suitable worker for a task role, **When** the main GM tries to delegate, **Then** the daemon reports a player-safe/GM-safe diagnostic and continues without delegation.

### Edge Cases

- Worker process starts but never becomes ready.
- Worker returns malformed JSON or a partial patch.
- Worker task times out while the main GM is waiting for validation repair.
- Worker task times out while the main GM is waiting for a narrative draft.
- Two workers return proposals for the same validation failure.
- Two workers return competing narrative drafts for the same scene.
- A worker proposal passes file-scope checks but fails game validation.
- The main GM restarts while worker tasks are in progress.
- Worker CLI command contains unsafe shell metacharacters or unsupported paths.
- Worker CLI command would normally open an interactive console window.
- Worker tries to edit `game_state/control/` files outside its explicitly granted task scope.
- Validation repair succeeds but would hide a GM prompt/docs/example contract gap.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support multiple named worker bridge profiles configured by the user.
- **FR-002**: Each worker profile MUST include at least a stable id, display name, launch command, role, enabled flag, and permission scope.
- **FR-003**: The main GM MUST remain the sole authority for player turn narration, final state application, and canonical game-state ownership.
- **FR-004**: Worker bridges MUST receive scoped task packets rather than unrestricted live control of canonical state.
- **FR-005**: Worker bridges MUST return proposals, structured responses, or patch bundles; they MUST NOT directly mutate canonical `game_session` state as part of the supported contract.
- **FR-006**: The apply gate MUST reject worker proposals that change files outside the task's allowed scope.
- **FR-007**: The apply gate MUST run relevant validation before accepting a worker repair proposal.
- **FR-008**: Worker activity MUST be recorded in an audit trail that identifies worker id, role, task id, start/end time, result, and applied/rejected proposal summary.
- **FR-009**: Validation repair delegation MUST support dispatching validation errors, relevant state context, and allowed repair scope to a worker.
- **FR-010**: Proposal-only tasks MUST be recordable without applying any file changes.
- **FR-011**: The first implementation wave MUST support at least two worker task classes: validation repair and narrative drafting.
- **FR-012**: Narrative-draft worker output MUST remain invisible to the player until the main GM explicitly uses or rewrites it in the final response.
- **FR-013**: Worker task routing MUST be role-based so different configured agents can be selected for repair, narration, lore, NPC analysis, QTE design, or console-output audit tasks.
- **FR-014**: Worker lifecycle diagnostics MUST distinguish stopped, starting, ready, busy, failed, timed out, and disabled states.
- **FR-015**: Worker agent processes MUST launch hidden/background by default so the user sees only the main GM/client window and does not receive multiple visible agent consoles.
- **FR-016**: Worker diagnostics MUST be visible through main-GM/daemon diagnostics rather than separate player-facing worker windows.
- **FR-017**: The system MUST provide clear failure behavior when no worker is available, a worker fails, or a proposal is rejected.
- **FR-018**: GM-facing prompts/docs/examples MUST describe when the main GM may delegate, what workers may return, why worker windows are hidden, and why the main GM still owns final state.
- **FR-019**: Tests MUST cover safe acceptance and safe rejection of worker repair proposals.
- **FR-020**: Tests MUST cover proposal-only narrative tasks that return drafts without changing canonical state.
- **FR-021**: Tests MUST cover hidden/background worker launch options so future process-launch changes do not reintroduce visible worker windows.

### Key Entities *(include if feature involves data)*

- **WorkerBridgeProfile**: User-configured profile for a worker agent, including command, role, enabled state, and permissions.
- **WorkerTaskPacket**: Scoped unit of work sent to a worker, including task type, validation issues or analysis request, allowed files, snapshot references, and expected response shape.
- **WorkerProposal**: Worker response containing structured findings, patch bundle, changed-file list, rationale, and self-check result.
- **ApplyGateDecision**: Result of evaluating a proposal: accepted, rejected, needs main-GM review, or failed validation.
- **WorkerBridgeStatus**: Runtime lifecycle status for a worker profile.
- **WorkerAuditEvent**: Durable record of task dispatch, response, apply decision, and errors.
- **WorkerScopePolicy**: Rules defining which task types may read or propose changes to which files.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A validation repair worker can resolve a controlled failing fixture with one delegated proposal and final validation passes.
- **SC-002**: 100% of worker proposals that attempt to change forbidden files are rejected in automated tests.
- **SC-003**: Worker lifecycle status is observable within 2 seconds of launch, failure, timeout, or task completion in local diagnostics.
- **SC-004**: A live E2E repair scenario records an audit trail that identifies the worker, task, proposal, and apply decision.
- **SC-005**: Existing single-GM gameplay continues to work when no worker profiles are configured.
- **SC-006**: A narrative-draft worker task returns a draft visible to the main GM in automated or scripted evidence, and no canonical files change.
- **SC-007**: Worker launch tests prove configured workers are started with hidden/background launch settings and no extra user-visible console windows are required.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "WorkerBridge|GmBridge|ValidationRepair|ProposalOnly|AgentConsoleLiveSmokeTests"`
- **Documentation/contract verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|SourceGuard"`
- **Frontend verification**: N/A for MVP; browser UI is out of scope unless diagnostics are later surfaced there.
- **Manual/player-facing verification**: Run a console live E2E with no worker profiles, one validation-repair worker profile, and one narrative-draft worker profile. Confirm normal single-GM play remains unchanged, no extra worker windows are shown to the player, delegated repair is auditable, and narrative drafts are visible to the main GM only.

## Assumptions

- Worker profiles are local CLI commands; no remote orchestration is introduced.
- The first implementation wave proves both validation repair and proposal-only narrative drafting.
- Workers can read task packets and produce structured proposal files through local filesystem/bridge protocols.
- Main GM and daemon may ask for worker help, but only the main GM/daemon apply gate may accept changes.
- Existing validation and pending snapshot authority remain the source of truth for accepted-turn correctness.
