# Agent Console snapshot and event model

Issue: #750

The Agent Console model describes what the main console client is showing without scraping the Windows console buffer. It is a reusable in-memory contract for tests and loopback API hosting; the model itself is file-independent and does not write observation files.

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

## Live input queue

Issue: #751

`AgentConsoleLiveInputSource` is an in-process `IConsoleInputSource` implementation for Agent Console control. It accepts queued key input as `ConsoleKeyInfo` values and queued text line input as complete `ReadLine()` responses. It does not use OS-level keyboard automation or console-buffer scraping.

`AgentConsoleActionRequest` is the semantic action request shape. A request names an `actionId` and may include the caller's expected `screenId` and `inputKind`. The live input source accepts the request only when the current `AgentConsoleStateStore` snapshot exists, is awaiting input, exposes a matching enabled action, and can resolve that action to safe existing console input. Resolution prefers the action shortcut; when there is no shortcut, the selected/default action resolves to Enter, and menu actions with a safe one-digit index may resolve to that menu digit.

`TryQueueDefaultAction()` is a race-safe live-test helper for the current screen's default action. Unlike `AgentConsoleActionRequest`, it does not accept a caller-provided `screenId`; it reads the current snapshot at call time and queues only an enabled action marked `isDefault`. It rejects snapshots with no enabled default action, which keeps the helper deterministic and prevents accidental selection of arbitrary visible actions.

Accepted key, text line, and action requests append `InputAccepted` events. Missing, disabled, stale-screen, mismatched-input-kind, unsupported, closed-queue, and full-queue requests append `InputRejected` events and do not consume unrelated queued input. Shutdown or cancellation unblocks waiting synchronous reads with a typed live-input exception instead of deadlocking the console flow.

`TryQueueReturnToGameLoopStep()` is a live-test driver helper for local command screens only. It rejects `game-loop`, non-local screens, and non-awaiting snapshots. On `explorer-command-*` key-continuation screens it queues the safe continuation action; on `explorer-selection-*` menu screens it queues an exposed back/close/return action. It performs one step at a time so callers can poll the next snapshot and stop as soon as `screenId` becomes `game-loop`.

## Loopback API host

Issue: #752

The normal console client can expose the Agent Console store and live input queue through a loopback-only API:

```bash
dotnet run --project BookOfEternityClient -- --agent-console --agent-url http://127.0.0.1:8790 --agent-token auto
```

`--agent-token auto` generates a per-run token and prints it for the local operator. Explicit non-empty tokens are also accepted. Agent Console mode is for the real console game loop, so it cannot be combined with `--web` or `--e2e-script`.

The API binds only to HTTP(S) loopback URLs such as `127.0.0.1`, `localhost`, or `[::1]`. Wildcard, `0.0.0.0`, and non-loopback addresses are rejected before the host starts.

The read endpoints expose only the bounded, file-independent observation surfaces:

- `GET /api/agent-console/snapshot`
- `GET /api/agent-console/events`

The control endpoints require a Bearer token in the Authorization header (for example, `Authorization: Bearer <token>`) and feed the existing live input source:

- `POST /api/agent-console/key`
- `POST /api/agent-console/text`
- `POST /api/agent-console/action`
- `POST /api/agent-console/default-action`
- `POST /api/agent-console/return-to-game-loop-step`

The key endpoint accepts a small key-name request, the text endpoint accepts a bounded text line, the action endpoint uses `AgentConsoleActionRequest`, `default-action` queues the latest enabled default action without a caller-provided screen id, and `return-to-game-loop-step` exposes the local-command unwind helper for autonomous live tests. The API does not accept filesystem paths, shell commands, or gameplay authority changes.

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
