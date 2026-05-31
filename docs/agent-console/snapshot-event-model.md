# Agent Console snapshot and event model

Issue: #750

The Agent Console model describes what the main console client is showing without scraping the Windows console buffer. It is a reusable in-memory contract for tests and future loopback API hosting; it does not expose HTTP endpoints and does not write observation files.

## Snapshot DTO

`AgentConsoleSnapshot` is the current player-visible console screen:

- `schemaVersion`: integer format version, currently `1`.
- `screenId`: stable id for the observed screen.
- `mode`: one of `menu`, `textPrompt`, `confirmation`, `loading`, `error`, or `exit`.
- `title`: short screen title.
- `plainText`: deterministic player-facing text.
- `ansiText`: optional ANSI-rendered text when a caller can provide it safely.
- `awaitingInput`: whether the console is waiting for user input.
- `inputKind`: one of `none`, `key`, `text`, `menuSelection`, or `confirmation`.
- `selectedIndex`: selected action index for menu-like screens.
- `actions`: available player-visible actions.
- `prompt`: active prompt metadata when a prompt is open.
- `renderedAtUtc` and `updatedAtUtc`: snapshot timestamps.
- `diagnostics`: bounded diagnostic/error entries suitable for observation, not private state dumps.

`AgentConsoleEvent` records a bounded history with monotonic `sequenceId` values. Event kinds are `screenRendered`, `promptStarted`, `inputAccepted`, `inputRejected`, `stateChanged`, and `failure`.

`AgentConsoleStateStore` keeps the current snapshot nullable for the no-screen state and retains only the configured number of most recent events. It is file-independent and host-independent, so tests or a future API endpoint can consume it without requiring normal console startup to use Agent Console services.

## E2E compatibility mapping

`AgentConsoleE2EObservationMapper.ToAgentConsoleSnapshot` maps the existing `ConsoleE2EObservationSnapshot` artifact model into the live snapshot model without reading or writing artifact files.

| ConsoleE2EObservationSnapshot | AgentConsoleSnapshot |
| --- | --- |
| `runId` + `stepIndex` | `screenId` as `e2e:{runId}:{stepIndex}` |
| `capturedAtUtc` | `renderedAtUtc` and `updatedAtUtc` |
| `inputMode -> mode` | `mode` |
| `inputMode -> inputKind` | `inputKind` and `awaitingInput` |
| `screenTitle -> title` | `title` |
| `playerFacingText -> plainText` | `plainText` |
| `options -> actions` | `actions` with `option-{index}` ids |
| `selectedOption -> selectedIndex` | selected action index when the option is present |
| `errorType` and `errorMessage` | bounded `diagnostics` entry |
| `artifactRoot` and artifact paths | not mapped; live observation remains file-independent |

The mapping preserves the existing E2E player-facing boundary: snapshots should include only screen text, options, prompts, and bounded diagnostics suitable for agents to observe.
