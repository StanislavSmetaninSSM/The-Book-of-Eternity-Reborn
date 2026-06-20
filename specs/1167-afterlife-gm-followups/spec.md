# Feature Specification: Afterlife and GM Bridge Follow-ups

**Feature Branch**: `feature/1167-afterlife-gm-followups`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User request to continue autonomous work on all open non-browser tasks after console readiness pass.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1167: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1167
  - #1168: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1168
  - #1169: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1169
  - #1170: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1170
  - #1171: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1171
- **Issue type**: bug, task, console UX hardening, GM bridge hardening.
- **Spec Kit justification**: The work spans afterlife player-facing console UX, explicit audit/diagnostic boundaries, GM bridge launch isolation, daemon logging, tests, and documentation.
- **Contract scope**: player-facing console, GM bridge runtime, afterlife pending/control display surfaces, docs, tests.
- **Out of scope**: Browser client, browser parity, frontend styling, new afterlife mechanics, changes to canonical state shape unless required by an existing validation failure.

## User Scenarios & Testing

### User Story 1 - Readable afterlife status (Priority: P1)

A player in Chaos Sea or Shining Abode enters `/status` and sees a compact, localized gameplay summary instead of raw JSON, file names, contract fields, or GM/debug terminology.

**Why this priority**: `/status` is a core player command and currently undermines the goal of a polished console client.

**Independent Test**: Run existing afterlife command-display fixtures and focused tests for Chaos Sea and Shining Abode `/status`; default output must not contain `Полный JSON`, raw file paths, or internal contract field names, while explicit audit output remains available.

**Acceptance Scenarios**:

1. **Given** a Chaos Sea save, **When** the player runs `/status`, **Then** the output describes the realm, resources, pending decisions, blockers, and useful next actions in Russian without raw JSON.
2. **Given** a Shining Abode save, **When** the player runs `/status`, **Then** the output gives a readable abode summary without raw receipts, closure hints, or update field names.
3. **Given** the player requests explicit audit diagnostics, **When** audit mode is used, **Then** raw contract/state details remain available for debugging and tests.

---

### User Story 2 - Readable Shining Abode details (Priority: P2)

A player opens `/shining_abode` detail flows and sees localized summaries, costs, blockers, expected outcomes, and back navigation instead of raw payloads and receipt arrays.

**Why this priority**: Shining Abode has rich data, but player utility is lost when details are shown as contract dumps.

**Independent Test**: Run focused Shining Abode detail tests for gates, prepared packages, core receipts, and pending core actions; default detail views must avoid raw JSON and internal update field names.

**Acceptance Scenarios**:

1. **Given** Shining Abode has gate data, **When** a player chooses gate details, **Then** the view explains the gate, state, costs, outcomes, and how to return.
2. **Given** Shining Abode has prepared package or core action receipt data, **When** a player opens details, **Then** the view summarizes the result and player impact without exposing raw receipt arrays by default.
3. **Given** audit mode is explicitly requested, **When** the player or developer opens audit diagnostics, **Then** the contract payload is still available.

---

### User Story 3 - Readable afterlife action previews (Priority: P3)

A player previews afterlife actions such as guardian trade, resident interaction, resident transfer, abode offering, or archive candidate mutation and sees a clear confirmation screen instead of GM authoring payloads.

**Why this priority**: These flows are action gates; players need to understand cost, risk, expected result, and how to cancel.

**Independent Test**: Add focused tests covering at least one trade flow, one resident flow, and one archive or offering flow; default previews must not expose pending file names, request IDs, receipt/update fields, or raw JSON.

**Acceptance Scenarios**:

1. **Given** a guardian trade preview, **When** the player opens it, **Then** the output explains what will be sold or bought back, cost, risk, and confirmation/cancel options.
2. **Given** a resident interaction or transfer preview, **When** the player opens it, **Then** the output explains the resident, intended action, requirements, expected outcome, and back path.
3. **Given** an archive/offering preview, **When** the player opens it, **Then** the output explains the change in player terms and reserves raw payloads for audit mode.

---

### User Story 4 - GM bridge runs as hidden GM, not coding agent (Priority: P1)

During live console E2E, the GM bridge launches Codex in a GM-only context so it does not load repository coding-agent instructions, Spec Kit work, or developer worktree state.

**Why this priority**: The live GM turn observed in #1166 took 430.3 seconds and showed coding-agent behavior, making autonomous live playtests impractical.

**Independent Test**: Run bridge profile and launcher tests that prove Codex defaults use a dedicated GM working directory/prompt isolation strategy and produce diagnostics for elapsed turn time.

**Acceptance Scenarios**:

1. **Given** default Codex GM bridge settings, **When** the launcher command is generated, **Then** it does not use the repository worktree as the child process working directory unless explicitly configured.
2. **Given** a bridge turn is executed, **When** diagnostics are recorded, **Then** elapsed time and waiting/working state are visible enough to diagnose slow turns.
3. **Given** a prompt template is generated, **When** Codex sees the GM prompt, **Then** it receives game-session and GM instructions without coding-agent task context.

---

### User Story 5 - Russian daemon logs stay readable (Priority: P1)

When the daemon logs a Russian player action, stdout and log files preserve Cyrillic text.

**Why this priority**: Russian is the normal language for this project; mojibake breaks live diagnostics and playtest logs.

**Independent Test**: Add a regression test or script smoke check that writes a representative Cyrillic action through the daemon logging path and verifies UTF-8 output.

**Acceptance Scenarios**:

1. **Given** the player action contains Cyrillic text, **When** the daemon writes stdout or log text, **Then** the text remains readable UTF-8.
2. **Given** PowerShell launches the daemon, **When** output encoding is initialized, **Then** console code page and output encoding do not corrupt Russian diagnostics.

### Edge Cases

- Explicit audit/debug views may contain raw JSON, canonical fields, request IDs, and file paths, but must be labeled as diagnostics.
- Malformed afterlife state may still show repair diagnostics, but those diagnostics must be clearly separated from normal gameplay output.
- Default player output must stay useful when optional fields are absent: show an in-world unavailable reason instead of leaking nulls or schema terms.
- GM bridge isolation must remain configurable for advanced users who intentionally want a custom working directory.
- Encoding fixes must not corrupt ASCII logs or JSON files.

## Requirements

### Functional Requirements

- **FR-001**: Default afterlife `/status` MUST render player-readable Russian summaries for Chaos Sea and Shining Abode without raw JSON or internal contract terms.
- **FR-002**: The system MUST preserve explicit audit/diagnostic access for raw afterlife state and contract payloads.
- **FR-003**: Shining Abode details MUST distinguish player detail views from audit views for gates, packages, receipts, and pending actions.
- **FR-004**: Afterlife action previews MUST show player confirmations with action, cost, risk, expected result, confirm/cancel affordances, and back navigation.
- **FR-005**: Default player-facing afterlife screens MUST NOT expose `Полный JSON`, raw file paths, pending file names, raw request IDs, receipt/update field names, or canonical field names unless audit mode is explicit.
- **FR-006**: Codex GM bridge launch defaults MUST isolate the child GM process from repository coding-agent context and must document how advanced users override that isolation.
- **FR-007**: GM bridge diagnostics MUST record elapsed turn time and enough state to distinguish slow work from waiting for input.
- **FR-008**: GM daemon stdout/log diagnostics MUST preserve Cyrillic player actions as UTF-8.
- **FR-009**: Documentation/audit notes MUST link these changes back to #1164 where the original afterlife output sweep was started.
- **FR-010**: Browser client behavior MUST remain untouched by this feature.

### Key Entities

- **Player Summary View**: The default localized console view shown during normal play.
- **Audit View**: An explicit diagnostic view that may expose raw canonical state or GM contract payloads.
- **Afterlife Action Preview**: A confirmation screen for trades, residents, archive candidates, offerings, and related afterlife actions.
- **GM Bridge Launch Profile**: User-configurable command, working directory, prompt, and diagnostic settings for hidden GM agents.
- **Daemon Log Entry**: A timestamped diagnostic line that must preserve Russian text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Focused afterlife command tests cover default `/status`, Shining Abode details, and at least three action-preview categories.
- **SC-002**: Default afterlife player output tests fail if raw JSON, file paths, raw request IDs, receipt/update fields, or canonical debug labels reappear.
- **SC-003**: GM bridge tests prove the default Codex child process is launched outside the repository worktree unless explicitly configured.
- **SC-004**: A regression check proves representative Cyrillic daemon log text survives without mojibake.
- **SC-005**: Documentation-sensitive afterlife verification tests pass after the change.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridge|GmWorkerBridge|GmWorker|Daemon|Encoding|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|ExplorerModeCommandTests.Afterlife"`
- **Documentation/contract verification**:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`
- **Frontend verification**: N/A; browser client is explicitly out of scope.
- **Manual/player-facing verification**: Run representative console commands in Chaos Sea and Shining Abode saves and inspect that default screens are player-readable while audit screens still expose diagnostics.

## Assumptions

- Existing afterlife fixture saves contain enough data to cover the targeted player surfaces; if a fixture is missing a required case, tests may add fixture-only state tied to these issues.
- Explicit audit access may be implemented as command arguments, menu actions, or existing diagnostics mode, following nearby command patterns.
- GM bridge isolation can default to a session-local working directory while still allowing advanced configuration.
- Encoding fixes should be script/runtime-level and should not require changing stored game state encoding.
