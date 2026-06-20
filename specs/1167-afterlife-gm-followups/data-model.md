# Data Model: Afterlife and GM Bridge Follow-ups

## Player Summary View

- **Purpose**: Default console output for normal gameplay.
- **Fields**: localized title, realm, resources, pending decisions, blockers, useful next actions, short description.
- **Rules**: Must not include raw JSON, file paths, request IDs, canonical field labels, or receipt/update field names.

## Audit View

- **Purpose**: Explicit diagnostic output for developers, GM contract verification, and repair flows.
- **Fields**: raw canonical state, pending/control payloads, receipts, request IDs, validation/repair diagnostics.
- **Rules**: Must be explicitly selected or labeled as audit/diagnostics.

## Afterlife Action Preview

- **Purpose**: Player confirmation view for afterlife actions.
- **Fields**: action name, target, cost, risk, expected result, blockers, confirm action, cancel/back action.
- **Rules**: Raw pending payloads and GM authoring fields stay in audit view.

## GM Bridge Launch Profile

- **Purpose**: Defines how hidden GM agents are launched.
- **Fields**: command, arguments, working directory, prompt visibility timeout, diagnostics, isolation mode.
- **Rules**: Default Codex profile should not run inside repository worktree context unless explicitly configured.

## Daemon Log Entry

- **Purpose**: Human-readable local diagnostics.
- **Fields**: timestamp, category, message, elapsed time where applicable.
- **Rules**: Russian text must be preserved as UTF-8 in stdout and log files.
