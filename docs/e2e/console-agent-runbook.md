# Console E2E agent runbook

Issue: #679

This runbook is the project-specific checklist for Hermes/Codex agents that need to drive the main console client without mutating a developer's live session. Start here, then use the lower-level sandbox reference in [`docs/console-e2e-sandbox.md`](../console-e2e-sandbox.md).

## Safety rules

- Do implementation work only when it is linked to a tracked GitHub issue/task. Put the issue number in the branch, commit, PR body, and any new doc/test header when practical.
- Never point E2E runs at `BookOfEternityClient/game_session` or another live player session.
- Use `FileSystemExample/game_session` only as the fixture source. Do not edit that fixture during a run.
- If a change touches Mortal World mechanics, update the GM-facing prompts/docs/examples/tests needed for the GM to understand the mechanic. Client implementation code is not the primary GM-facing source of truth.
- If a change touches an Afterlife contract, follow `AGENTS.md`: update the contract matrix, examples, validation manifest, and documentation coverage tests as required.

## Prepare a disposable sandbox session

Programmatic tests and harnesses should use `ConsoleE2ESandbox.CreateFromFixture`:

```csharp
using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
    fixtureGameSessionPath: Path.Combine(repoRoot, "FileSystemExample", "game_session"),
    artifactRoot: Path.Combine(repoRoot, "artifacts", "console-e2e"));

// Pass sandbox.BasePath to the console client. The client reads sandbox.BasePath/game_session.
```

By default, disposing the sandbox deletes `<artifact-root>/run-<guid>/`. Use `preserveArtifacts: true` for failing or diagnostic runs so copied state, logs, screen/state snapshots, and failure artifacts remain available.

## Launch the console client from a sandbox

Build first if dependencies changed:

```bash
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
```

Launch the main client with the sandbox base path, not the fixture path:

```bash
dotnet run --project BookOfEternityClient/BookOfEternityClient.csproj -- "<artifact-root>/run-<guid>"
```

A valid sandbox should initialize from `<artifact-root>/run-<guid>/game_session` and reach the normal console flow. An invalid `game_session` should fail before launch or surface a clear diagnostic instead of corrupting the source fixture.

## Scripted input and observations

The intended control path is the built-in scripted input mode from #676, not an external ConPTY/winpty harness. Until that mode is fully implemented, tests should stay at the stable API/helper layer and document any manual dry run honestly.

When scripted mode is available, keep scripts small and deterministic:

- one step per menu key, text submission, confirmation, or exit command;
- store the input script beside the run artifacts;
- after each step, read the deterministic screen/state snapshots rather than scraping ANSI stdout;
- write observations through `ConsoleE2EObservationArtifactWriter` using the format documented in [`docs/e2e/console-observation-artifacts.md`](console-observation-artifacts.md);
- assert only player-facing text/options and the current input mode;
- never assert hidden/internal-only state unless that state is explicitly player-visible.

Expected artifact shape for scripted runs:

```text
artifacts/console-e2e/run-<guid>/
  input-script.json
  stdout.txt
  stderr.txt
  screens/
    000-main-menu.txt
    000-main-menu.json
  state-diff/
  failure.txt
```

## Focused smoke command

Use the focused ConsoleE2E test filter before opening or updating a PR for console E2E infrastructure:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter ConsoleE2E
```

For this runbook itself, also run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter ConsoleE2ERunbookTests
```

Reference the exact commands and results in the PR body.

## Failure artifacts

For any failing E2E run, preserve enough evidence for the next agent to continue without re-running blindly:

- input script or manual key/text steps;
- stdout/stderr;
- screen/state snapshots;
- relevant copied `game_session` files or state diffs;
- exception text, timeout marker, and the last successful step;
- cleanup decision: whether the sandbox was deleted or preserved.

## Troubleshooting

### invalid `game_session`

Symptoms: missing `game_state/meta/soul_state.json`, missing `currentRealm`, startup exception, or menu never appears.

Actions:

1. Confirm the fixture source is exactly `FileSystemExample/game_session`.
2. Create a fresh sandbox with `ConsoleE2ESandbox.CreateFromFixture`.
3. Do not repair the source fixture as part of an unrelated E2E task. If the fixture itself is defective, create or link a tracked GitHub issue first.

### prompt/input hang

Symptoms: process keeps running, no next screen/state snapshot, or a text prompt waits forever.

Actions:

1. Check the last scripted step and the current input mode artifact.
2. If the client is in a text prompt, send a full text value plus submit/Enter, not only menu navigation keys.
3. If the client is in menu mode, use the documented key/choice format for #676 scripted input.
4. Preserve artifacts with `preserveArtifacts: true` before changing code.

### timeout

Symptoms: harness kills the client or a test exceeds its timeout.

Actions:

1. Save `failure.txt`, stdout/stderr, and the last screen/state snapshots.
2. Record the timeout duration and the last successful step.
3. Prefer shorter focused smoke scenarios over long story walkthroughs.

### ANSI / color / cursor-control noise

Symptoms: assertions fail because of color codes, console width, cursor movement, or screen clearing.

Actions:

1. Prefer deterministic JSON/text snapshots over stdout scraping.
2. Use `NO_COLOR=1` or a future `--plain-output` flag when launching the client.
3. Do not make tests depend on console width or Spectre.Console cursor-control sequences.

### cleanup

Default sandbox disposal deletes the copied run directory. For passing local runs this is expected. For failures, set `preserveArtifacts: true`, inspect the preserved run directory, and delete it manually only after the evidence is attached to the issue/PR or no longer needed.
