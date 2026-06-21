# Feature Specification: Browser Console Command Parity Audit

**Feature Branch**: `work/1119-browser-console-audit`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User requested autonomous browser work after console polish; source issue #1119 requires a complete browser-vs-console command output parity audit.

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1119](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1119), parent [#1118](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1118)
- **Issue type**: audit / hardening
- **Spec Kit justification**: The work governs browser/console semantic parity across many player-facing commands and will direct several follow-up implementation issues, so it needs durable traceability.
- **Contract scope**: player-facing, console, browser, frontend documentation. No GM-authored runtime contract changes are in scope.
- **Out of scope**: Fixing individual browser command renderers, changing game state contracts, changing GM prompts/examples, and redesigning the browser UI. Those belong to linked follow-up issues.

## User Scenarios & Testing

### User Story 1 - Every Command Is Classified (Priority: P1)

As a maintainer, I need every browser command from the command coverage contract to appear in the audit with a clear status, so implementation agents cannot miss player-facing gaps.

**Why this priority**: This is the baseline acceptance criterion for #1119 and prevents future parity work from starting from an incomplete inventory.

**Independent Test**: Run the browser command coverage tests and a source-guard test that confirms the audit document references every command ID from `BrowserCommandCoverageService`.

**Acceptance Scenarios**:

1. **Given** the current browser command coverage, **When** the audit is checked, **Then** every command ID has a row or explicit classification.
2. **Given** a future command is added to coverage, **When** the audit guard runs without updating the document, **Then** verification fails.

---

### User Story 2 - Gaps Have Severity And Owners (Priority: P2)

As a maintainer, I need every non-adequate command group to have severity and a linked follow-up issue or explicit no-fix reason, so the backlog is actionable.

**Why this priority**: The audit is only useful if it turns gaps into prioritized work rather than a passive list.

**Independent Test**: Inspect `docs/audits/browser-console-command-parity-audit.md` and confirm non-adequate rows use P0/P1/P2/P3 and link or name the governing issue.

**Acceptance Scenarios**:

1. **Given** a command has missing browser details, **When** it is listed in the audit, **Then** the row includes severity and follow-up issue.
2. **Given** a command is intentionally advanced-only or blocked, **When** it is listed in the audit, **Then** the row explains why it is not a player-default renderer gap.

---

### User Story 3 - Execution Order Is Clear (Priority: P3)

As a maintainer, I need the audit to state which #1121-#1126 tasks should be done first or whether they are already complete, so future agents can continue without re-triage.

**Why this priority**: #1118 depends on the audit to guide execution sequencing across follow-up issues.

**Independent Test**: Inspect the audit summary and confirm it names #1121 through #1126 with current status or priority guidance.

**Acceptance Scenarios**:

1. **Given** a follow-up issue from #1121-#1126, **When** a maintainer reads the audit, **Then** they can see its relative order and whether it still needs implementation.

### Edge Cases

- Coverage includes blocked, advanced-only, local-turn, and write commands that are not ordinary player-default read screens.
- Some follow-up issues may already be closed; the audit must reflect current state instead of reopening completed work.
- Pixel-perfect console rendering is not required; semantic parity is the requirement.
- Raw JSON may remain available only as an explicit advanced/debug surface, not as the default player explanation.

## Requirements

### Functional Requirements

- **FR-001**: The audit MUST enumerate every command ID exposed by the browser command coverage contract.
- **FR-002**: The audit MUST record aliases, realm/surface, browser status, console-visible sections, browser-visible sections, missing browser details, raw JSON dependency, drill-down status, priority, follow-up issue, and notes for each command or command family.
- **FR-003**: The audit MUST assign severity P0/P1/P2/P3 to every gap or state an explicit reason that no fix is required.
- **FR-004**: The audit MUST distinguish semantic parity from pixel-perfect console rendering.
- **FR-005**: The audit MUST state the recommended execution order/current status for #1121, #1122, #1123, #1124, #1125, and #1126.
- **FR-006**: Verification MUST fail if a browser coverage command is absent from the audit.

### Key Entities

- **Browser command coverage entry**: A command descriptor with command ID, aliases, realm/group, browser status, handler kind, audit status, and follow-up metadata.
- **Audit row**: A durable classification of one command or command family against console semantic parity.
- **Follow-up issue**: A GitHub issue that owns a concrete implementation gap.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of command IDs in browser command coverage appear in the audit document.
- **SC-002**: 100% of non-adequate command groups have severity plus a linked issue or explicit no-fix reason.
- **SC-003**: A future missing command in the audit is caught by automated verification before merge.
- **SC-004**: Maintainers can identify the next browser parity implementation group from the audit summary in under 2 minutes.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter BrowserCommandCoverage`
- **Documentation/contract verification**: Audit source guard that checks command IDs in `docs/audits/browser-console-command-parity-audit.md`.
- **Frontend verification**: N/A for this audit-only task.
- **Manual/player-facing verification**: Spot check `/api/explorer/command-coverage` metadata against the audit and existing console/browser drill-down audits.

## Assumptions

- The browser command coverage contract is the source of truth for command inventory.
- Existing mortal and afterlife drill-down audits are trusted context for console-visible sections and known browser gaps.
- This change does not alter player-visible runtime behavior and therefore does not require GM prompt/example updates.
