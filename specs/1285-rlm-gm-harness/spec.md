# Feature Specification: RLM-Inspired GM Harness

**Feature Branch**: `1266-universal-command-audit`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User request to adopt the useful parts of the RLM approach for the game master before the next harness live test.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1249 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1249
  - #1285 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1285
  - #1281 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1281
  - #1282 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1282
  - #1283 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1283
  - #1286 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1286
  - #1287 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1287
  - #1288 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1288
  - #1289 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1289
  - #1290 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1290
  - #1316 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1316
  - #1340 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1340
  - #1341 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1341
  - #1342 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1342
  - #1343 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1343
  - #1344 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1344
  - #1345 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1345
  - #1349 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1349
  - #1350 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1350
  - #1351 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1351
  - #1352 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1352
  - #1353 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1353
  - #1354 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1354
  - #1356 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1356
  - #1396 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1396
- **Issue type**: epic plus implementation tasks.
- **Spec Kit justification**: This work changes GM harness runtime behavior, worker orchestration, validation repair feedback, GM-facing prompts/docs, and live-test evidence across multiple sessions.
- **Contract scope**: GM-facing prompts, runtime-state, validation, docs, examples, agent-console, e2e.
- **Out of scope**:
  - Training a custom RL model.
  - Adding the upstream `rlms` Python package as a runtime dependency.
  - Giving the GM arbitrary REPL, PowerShell, Python, or repo-root file access as normal gameplay flow.
  - Replacing existing validators, repair packets, worker proposal gates, or rollback authority.

## User Scenarios & Testing

### User Story 1 - Live Turn Trajectory Ledger (Priority: P1)

As the developer running live GM tests, I need every GM turn and repair loop to emit a compact structured trajectory so repeated failures become measurable harness feedback instead of scattered temp logs.

**Why this priority**: Without normalized trajectories, the project cannot know whether RLM-style context, delegation, or memory actually improves the GM.

**Independent Test**: Run focused tests that simulate a successful turn and a validation-repair turn, then assert a session-owned ledger record exists with outcome, validation, timing, repair, and containment fields.

**Acceptance Scenarios**:

1. **Given** a live GM turn completes without validation errors, **When** the harness records the result, **Then** the ledger records the turn as valid with duration, context pack, output files, and no repair attempts.
2. **Given** a live GM turn enters validation repair, **When** the repair completes or fails, **Then** the ledger records the issue kinds, repair packet references, attempts, final status, and rollback/worker events.
3. **Given** the GM reads or is instructed toward implementation source during ordinary play, **When** the turn is audited, **Then** the ledger can record a containment failure or missing harness-surface finding.

---

### User Story 2 - Compact Experience Memory (Priority: P2)

As the GM harness, I need to retrieve relevant lessons from prior trajectories into the session context pack so the GM is guided by proven local fixes without reading giant historical logs.

**Why this priority**: RLM-style improvement depends on reusable experience, but raw logs would bloat prompts and stale lessons can mislead the GM.

**Independent Test**: Feed several ledger records with different realms and validation issue kinds, then assert only relevant compact lessons are selected and bounded in size.

**Acceptance Scenarios**:

1. **Given** a new repair request matches prior issue kind and realm, **When** the context pack is generated, **Then** it includes a small lesson describing the prior bad action, accepted fix, and preferred harness tool.
2. **Given** a prior lesson belongs to an obsolete template/contract version, **When** lessons are selected, **Then** it is excluded or marked stale.
3. **Given** there are many matching lessons, **When** lessons are selected, **Then** the output stays under a configured cap and prioritizes recent successful repairs.

---

### User Story 3 - Safe GM Context-Probing Surface (Priority: P2)

As the GM, I need safe harness-owned context probes and summaries instead of repo-root spelunking so I can inspect game state and validation needs without reading implementation code or damaging files.

**Why this priority**: This is the practical replacement for the RLM REPL idea. The GM gets programmatic context access, but only through bounded game/harness functions.

**Independent Test**: Generate a session context pack and verify it exposes safe probes/templates while ordinary play prompts do not direct the GM to `BookOfEternityClient/**/*.cs`.

**Acceptance Scenarios**:

1. **Given** an ordinary live turn starts, **When** the GM opens the context pack, **Then** it can find safe summaries for realm, pending contracts, actors, validation issues, and output templates.
2. **Given** a validation issue requires a specific output shape, **When** the GM asks for repair guidance, **Then** the harness points to a compact template or repair packet instead of implementation source.
3. **Given** the GM needs a fact that is only available in code, **When** the issue is detected, **Then** the result is a harness gap follow-up rather than a normal instruction to read source.

---

### User Story 4 - Recursive Worker Delegation Flow (Priority: P3)

As the main GM, I need to delegate bounded subtasks to hidden worker agents and receive proposal-only outputs so narrative, validation, lore, or entity authoring can happen in parallel without giving workers canonical authority.

**Why this priority**: Existing worker roles need a first-class orchestration loop before they can behave like RLM subcalls.

**Independent Test**: Simulate one proposal-only worker task and one validation-repair worker task, then assert the proposal is recorded in the trajectory and cannot directly mutate canonical state.

**Acceptance Scenarios**:

1. **Given** a worker profile supports a task type, **When** the main GM delegates a bounded task, **Then** the worker receives a narrow packet with allowed context, schema, timeout, and acceptance criteria.
2. **Given** the worker writes a malformed or overbroad proposal, **When** the apply gate checks it, **Then** canonical files are unchanged and the trajectory records the rejection.
3. **Given** a worker proposal passes validation, **When** the main GM applies it through the gate, **Then** the ledger records proposal receipt, apply decision, and validation result.

---

### User Story 5 - RLM-Inspired Live-Test Rubric (Priority: P3)

As the developer, I need the next live GM test to measure harness quality, not just whether one turn eventually completes.

**Why this priority**: The live test must drive harness engineering: repeated GM difficulty should become a tool, validator, rollback, context, or worker issue.

**Independent Test**: Run the live-test checklist against a short scenario and confirm it produces structured notes tied to ledger entries and follow-up issues/comments.

**Acceptance Scenarios**:

1. **Given** a live turn succeeds slowly after multiple repairs, **When** the rubric is applied, **Then** it records friction separately from final success.
2. **Given** the GM gets stuck or needs manual reasoning, **When** the rubric is applied, **Then** it asks whether a harness tool, validator, rollback, template, or worker packet should take over.
3. **Given** a repeated failure is found, **When** the test ends, **Then** a GitHub issue or issue comment captures the harness fix or explicit no-change rationale.

### Edge Cases

- A live test is interrupted: partial ledger records must remain readable and mark status as interrupted or abandoned.
- Validation fails before a GM output exists: the ledger must still record pre-turn context, issue kind, and missing output status.
- A worker times out: the trajectory records timeout and fallback path without blocking legacy repair forever.
- Experience memory finds contradictory lessons: newer contract/template version wins; stale lessons are excluded or marked non-authoritative.
- Context pack generation fails: the GM prompt must fail closed or fall back to existing compact templates without exposing repo-root source as the normal route.
- Player-facing output is unchanged by this feature; if player-visible command behavior changes during implementation, a follow-up issue/spec update is required.

## Requirements

### Functional Requirements

- **FR-001**: The harness MUST record a compact session-owned trajectory for live GM turns and repair loops.
- **FR-002**: The trajectory MUST include turn identity, realm/mode, context pack reference, dispatch attempts, validation outcome, repair attempts, worker events, rollback events, duration, and final status.
- **FR-003**: The trajectory MUST include rubric fields that distinguish "valid eventually" from "pleasant/contained/easy for the GM to complete".
- **FR-004**: The harness MUST derive compact experience lessons from prior trajectories using realm, mode, issue kind, task type, and template/contract version as relevance signals.
- **FR-005**: Experience lessons MUST be bounded, versioned, and compact enough to be placed in the session-local context pack without dumping raw historical logs.
- **FR-006**: The GM context pack MUST expose safe context-probing guidance or generated summaries for common turn/repair needs.
- **FR-007**: Safe context probes MUST prefer harness-owned packets/templates/summaries over implementation source reads during ordinary play and repair.
- **FR-008**: Delegated worker tasks MUST remain hidden/background and proposal-only unless accepted through existing validation/apply gates.
- **FR-009**: Worker dispatch, proposal receipt, rejection, apply, timeout, and validation outcomes MUST be recorded in the trajectory ledger.
- **FR-010**: The next live-test checklist MUST record harness friction and convert repeated GM difficulty into harness follow-up issues or issue comments.
- **FR-011**: GM-facing prompts/docs/examples MUST explain any new trajectory, experience-memory, safe-probe, or delegation workflow added by the implementation.
- **FR-012**: The feature MUST NOT add normal gameplay reliance on arbitrary REPL execution, arbitrary shell execution, or direct worker writes to canonical game state.
- **FR-013**: During afterlife live turns and repairs, GM helper completion MUST detect raw Mortal World profile mutations against the pending-turn snapshot before writing completion signals, so wrong-realm writes are blocked before stable acceptance. JSON files MUST be compared semantically so formatting-only snapshot serialization differences are not treated as GM mutations.
- **FR-014**: Actor reasoning validation MUST tolerate harmless trailing punctuation differences between declared actor names and `###` reasoning block headings without weakening canonical actor identity checks.
- **FR-015**: Afterlife spiritual-conflict context packs MUST expose a compact `tempoAdvantage` template with required `advantageId`, `sourceId`, and `guard_tempo_window` consumption fields.
- **FR-016**: Diagnostic-only validation repair requests MUST fail closed in the client without waiting for GM-authored `validation_repair_ready.json`, preserve a machine-readable `validation_diagnostic_failure_report.json` after rollback, and expose any player/agent pause as Agent Console key input.
- **FR-017**: Authorized Chaos Sea abode travel MUST project the target discovered abode and its Guardian into guardian policy authority for the same turn, so the guardian-scope validator treats the accepted `[CHAOS_SEA_TRAVEL]` transition as canonical instead of stale pre-turn state.
- **FR-018**: Afterlife wrong-realm mutation guards MUST ignore helper/rollback backup artifacts such as `*.rollback.*` while continuing to reject real Mortal World profile file creations, deletions, or semantic mutations.
- **FR-019**: Worker dispatch MUST preserve and validate a proposal already written to the proposal inbox even if the worker CLI later times out or exits nonzero, because the proposal remains proposal-only and still has to pass the existing schema and apply gate before any canonical state changes.
- **FR-020**: Worker runner prompts MUST include a self-contained `worker-proposal-v1` JSON skeleton and required-field rules, so hidden workers can produce validator-compatible proposals without reading implementation source or guessing contract fields.
- **FR-021**: Mortal World turns that explicitly resolve open combat and also change XP, active-skill mastery, or combat resources MUST be validated as incomplete unless they materialize a player-inspectable `game_state/combat/combat_log.json` surface for `/бой`; repair guidance MUST route the GM to the compact Mortal combat state template and preserve the existing XP/mastery/status changes unless separately invalid.
- **FR-022**: Mortal World level-up stat-point awards MUST be idempotent across client restarts and validation repair loops; the client MUST persist a restart-safe awarded-through-level marker and MUST refresh computed characteristics after local stat allocation.
- **FR-023**: Mortal World accepted turns MUST NOT leave `game_state/player/experience.json.totalExperience` equal to or above `experienceForNextLevel` for the persisted `playerLevel`/`level`; validation MUST request repair that advances the level and next threshold while preserving client-owned stat-point marker authority.

### Key Entities

- **GM Trajectory Record**: Compact audit item for one GM turn, repair attempt, or terminal flow.
- **Experience Lesson**: Small derived hint from a prior trajectory: pattern, bad action, accepted fix, preferred harness surface, contract/template version.
- **Safe GM Probe**: Harness-owned read-only function, packet, or generated summary that exposes game/repair context.
- **Worker Delegation Record**: Structured record of a worker task packet, proposal, apply/reject decision, and validation result.
- **Live-Test Rubric Finding**: Structured note about success, containment, friction, delegation, memory usage, or missing harness support.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A focused test can produce and validate at least one successful-turn ledger record and one repair-turn ledger record.
- **SC-002**: Context pack generation can include relevant lessons while keeping the lesson artifact below a documented size cap.
- **SC-003**: Ordinary GM turn/repair prompts expose session-local safe surfaces and do not present repo implementation files as the default source of truth.
- **SC-004**: At least one worker proposal path is represented in the ledger without granting direct canonical write authority.
- **SC-005**: The next live test reports turn duration, repair count, containment status, missing-tool findings, and follow-up issue/comment links.
- **SC-006**: An afterlife helper completion attempt with raw Mortal World profile mutations fails before `ready/turn_complete.json` or `validation_repair_ready.json` is written.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests"`
- **Documentation/contract verification**:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`
- **Frontend verification**: N/A unless implementation touches browser surfaces.
- **Manual/player-facing verification**:
  - Run a short live GM bridge test with `codex -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox`.
  - Confirm ledger, experience lessons, context pack references, and rubric notes are produced.
  - Confirm the GM does not need to inspect implementation source for ordinary repair.

## Assumptions

- The first implementation is harness engineering, not model training.
- The upstream RLM repository is used as an architectural reference only.
- Existing worker bridge, validation repair, rollback, and context-pack mechanisms remain the authority path.
- The next live test may be narrower than a full adventure if needed to prove the new harness loop first.
