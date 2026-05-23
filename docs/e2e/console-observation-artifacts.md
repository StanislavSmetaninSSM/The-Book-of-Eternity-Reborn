# Console E2E observation artifacts

Issue: #677

Console E2E runs need deterministic player-facing observations that agents can read without scraping ANSI stdout. `ConsoleE2EObservationArtifactWriter` provides the shared artifact format for the built-in scripted input mode from #676 and for focused smoke tests.

## Paths

Create one writer per E2E run:

```csharp
var writer = new ConsoleE2EObservationArtifactWriter(
    artifactRoot: Path.Combine(repoRoot, "artifacts", "console-e2e", "run-<guid>"),
    runId: "run-<guid>");
```

Each snapshot is written under the run artifact root:

```text
screens/<step>-<slug>.json
screens/<step>-<slug>.txt
```

Example: `screens/000-main-menu.json` and `screens/000-main-menu.txt`.

## Snapshot fields

The JSON snapshot is the machine-readable source for agents. The text snapshot is for quick human review and diffs.

Required fields:

- `schemaVersion`: integer format version, currently `1`.
- `runId`: stable id for this E2E run.
- `stepIndex`: zero-based scripted step index.
- `capturedAtUtc`: ISO-8601 capture timestamp.
- `inputMode`: one of `menu`, `textPrompt`, `confirmation`, `loading`, `error`, `exit`.
- `screenTitle`: short player-facing screen/menu title.
- `playerFacingText`: deterministic plain text visible to the player.
- `options`: player-visible options/actions for menu-like screens.
- `selectedOption`: selected player-visible option when the UI is in menu mode.
- `artifactRoot`: path to the run artifact directory.
- `logPath`: optional stdout/stderr or combined log path.

Error snapshots additionally include:

- `errorType`: exception/type label, for example `InvalidOperationException`.
- `errorMessage`: diagnostic message suitable for artifacts and issue comments.

## Player-facing boundary

Snapshots must include only the screen/options/prompt state that is visible to the player. Do not add hidden/internal-only state, GM private reasoning, secret flags, full game state dumps, or implementation-only diagnostics to `playerFacingText`, `options`, or `selectedOption`.

If a test needs internal state, assert it through a separate test-only fixture or state diff artifact with explicit scope. The player-facing observation artifact remains safe to attach to issues and PRs.

## Usage pattern

After each scripted input step from #676:

1. Build a `ConsoleE2EObservationSnapshot` from the same console/menu state that rendered the player-facing UI.
2. Set `inputMode` to the current mode: `menu`, `textPrompt`, `confirmation`, `loading`, `error`, or `exit`.
3. Write the snapshot with `ConsoleE2EObservationArtifactWriter.WriteSnapshot`.
4. Let assertions read the `.json` file first; use `.txt` for review.
5. On exceptions/timeouts, call `WriteExceptionSnapshot` so failure artifacts still include the last known player-facing screen and error metadata.

## Main menu example

```json
{
  "schemaVersion": 1,
  "runId": "run-main-menu",
  "stepIndex": 0,
  "capturedAtUtc": "2026-05-23T12:00:00+00:00",
  "inputMode": "menu",
  "screenTitle": "Главное меню",
  "playerFacingText": "Добро пожаловать в Книгу Вечности",
  "options": ["Продолжить", "Об игре", "Выход"],
  "selectedOption": "Продолжить",
  "artifactRoot": "artifacts/console-e2e/run-main-menu",
  "logPath": "artifacts/console-e2e/run-main-menu/stdout.txt"
}
```

## Failure example

```json
{
  "schemaVersion": 1,
  "runId": "run-error",
  "stepIndex": 2,
  "capturedAtUtc": "2026-05-23T12:00:00+00:00",
  "inputMode": "error",
  "screenTitle": "Timeout",
  "playerFacingText": "The console E2E run timed out before the next prompt.",
  "options": [],
  "artifactRoot": "artifacts/console-e2e/run-error",
  "errorType": "InvalidOperationException",
  "errorMessage": "Scripted input timed out waiting for prompt."
}
```

See also: [`docs/e2e/console-agent-runbook.md`](console-agent-runbook.md).
