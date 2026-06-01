# Console E2E agent runbook

Issue: #679

This runbook is the project-specific checklist for Hermes/Codex agents that need to drive the main console client without mutating a developer's live session. Start here, then use the lower-level sandbox reference in [`docs/console-e2e-sandbox.md`](../console-e2e-sandbox.md).

For the live Agent Console API workflow, use [`docs/e2e/agent-console-runbook.md`](agent-console-runbook.md). That runbook launches the real console client with `--agent-console`, reads snapshots/events over loopback HTTP, submits token-gated key/text/action requests, and exits through the player-visible menu. The live Agent Console workflow complements this scripted E2E harness; it does not replace scripted E2E for deterministic regression coverage.

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

The implemented control path is the built-in scripted input mode from #676. It replaces only the console input source; the production menu/prompt logic still handles the key/text events.

### Script format

Store the script as JSON. Each step has a `kind`:

- `kind: "key"` with `key`: `Up`, `Down`, `Left`, `Right`, `W`, `S`, `Enter`, `Escape`, `Space`, digits, or a `ConsoleKey` name.
- `kind: "text"` with `text`: complete text for a `ReadLine()`-style prompt.

Example `input-script.json` for a safe main-menu exit smoke path:

```json
{
  "steps": [
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Enter" }
  ]
}
```

### Copy-pasteable local smoke run

From the repository root in Hermes/git-bash:

```bash
RUN_ROOT="${TMPDIR:-/tmp}/boe-console-e2e-$(date +%s)"
mkdir -p "$RUN_ROOT/artifacts"
cp -R FileSystemExample/game_session "$RUN_ROOT/game_session"
cat > "$RUN_ROOT/input-script.json" <<'JSON'
{
  "steps": [
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Down" },
    { "kind": "key", "key": "Enter" }
  ]
}
JSON

dotnet run --project BookOfEternityClient/BookOfEternityClient.csproj --no-restore -- \
  "$RUN_ROOT" \
  --e2e-script "$RUN_ROOT/input-script.json" \
  --e2e-artifacts "$RUN_ROOT/artifacts" \
  --plain-output \
  > "$RUN_ROOT/stdout.txt" \
  2> "$RUN_ROOT/stderr.txt"

echo "Artifacts: $RUN_ROOT"
find "$RUN_ROOT/artifacts" -maxdepth 3 -type f | sort
```

Expected result: exit code `0`, no `$RUN_ROOT/artifacts/failure.txt`, and deterministic observation snapshots under `$RUN_ROOT/artifacts/screens/`.

To verify the real runner error path, use an intentionally exhausted script:

```bash
cat > "$RUN_ROOT/input-script.json" <<'JSON'
{ "steps": [] }
JSON

if dotnet run --project BookOfEternityClient/BookOfEternityClient.csproj --no-restore -- \
  "$RUN_ROOT" \
  --e2e-script "$RUN_ROOT/input-script.json" \
  --e2e-artifacts "$RUN_ROOT/artifacts" \
  --plain-output \
  > "$RUN_ROOT/stdout.txt" \
  2> "$RUN_ROOT/stderr.txt"; then
  echo "Expected scripted input failure, but the run succeeded" >&2
  exit 1
fi

grep -F "Console E2E scripted input failed at step 0" "$RUN_ROOT/stderr.txt"
test -f "$RUN_ROOT/artifacts/failure.txt"
find "$RUN_ROOT/artifacts/screens" -name '*error*.json'
```

Expected result: exit code `2`, `$RUN_ROOT/artifacts/failure.txt`, stderr containing `Console E2E scripted input failed at step 0`, and at least one `screens/*error*.json` snapshot.

When scripted mode is used, keep scripts small and deterministic:

- one step per menu key, text submission, confirmation, or exit command;
- store the input script beside the run artifacts;
- after each relevant player-visible step, read the deterministic `screens/*.json` and `screens/*.txt` snapshots rather than scraping ANSI stdout;
- write observations through `ConsoleE2EObservationArtifactWriter` using the format documented in [`docs/e2e/console-observation-artifacts.md`](console-observation-artifacts.md);
- assert only player-facing text/options and the current input mode;
- never assert hidden/internal-only state unless that state is explicitly player-visible.

Expected artifact shape for scripted runs:

```text
$RUN_ROOT/
  game_session/
  input-script.json
  stdout.txt
  stderr.txt
  artifacts/
    screens/
      000-main-menu.txt
      000-main-menu.json
      006-exit.txt
      006-exit.json
      000-error.json         # only on exception/failure paths
    failure.txt              # only on script/input/client failures
```

Failure evidence uses the same `$RUN_ROOT/artifacts/screens/*error*.json` glob documented above.

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

### missing or invalid script file

Symptoms: startup exits before the host starts, stderr says `Console E2E scripted input failed at step 0`, and `$RUN_ROOT/artifacts/failure.txt` exists.

Actions:

1. Confirm `--e2e-script` points at the copied sandbox script path, not the source repo or a deleted temp file.
2. Inspect `$RUN_ROOT/artifacts/failure.txt` for `operation: script-load` and the normalized `scriptPath`.
3. For malformed JSON, fix the script and rerun; for a missing file, recreate the sandbox/script rather than changing game code.

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
2. Use `NO_COLOR=1` or `--plain-output` when launching the client.
3. Do not make tests depend on console width or Spectre.Console cursor-control sequences.

### cleanup

Default sandbox disposal deletes the copied run directory. For passing local runs this is expected. For failures, set `preserveArtifacts: true`, inspect the preserved run directory, and delete it manually only after the evidence is attached to the issue/PR or no longer needed.
