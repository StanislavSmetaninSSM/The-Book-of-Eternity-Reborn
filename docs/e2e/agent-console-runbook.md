# Agent Console live-control runbook

Issue: #753
Parent task: #749

This runbook is for agents and developers who need to launch the real main console client in a disposable sandbox, observe it through the Agent Console API, submit live input, and shut it down. It complements the scripted E2E runbook in [`docs/e2e/console-agent-runbook.md`](console-agent-runbook.md); scripted E2E remains the deterministic regression harness, while live Agent Console control is for interactive diagnosis and first-practical workflow smoke checks.

## Prerequisites

- Run from the repository root.
- Build the console client before a no-build smoke run:

```bash
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
```

- Use `FileSystemExample/game_session` only as the fixture source.
- Copy the fixture into a disposable run root outside the repo before launch.
- Bind only to loopback URLs such as `http://127.0.0.1:<port>`, `http://localhost:<port>`, or `http://[::1]:<port>`.
- Keep generated stdout, stderr, snapshots, event captures, and scratch scripts under the run root. Do not commit generated run output such as `stdout.txt`, `stderr.txt`, `events.jsonl`, `exit-code.txt`, `prompt.md`, `final.md`, `.hermes/`, `bin/`, or `obj/`.

## Launch from PowerShell

This starts the real console client in the background with an explicit local token. The token is kept in a shell variable and is not written into repo files.

```powershell
$RunRoot = Join-Path $env:TEMP ("boe-agent-console-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
Copy-Item -Recurse -Path "FileSystemExample\game_session" -Destination (Join-Path $RunRoot "game_session")

$Port = 8790
$Base = "http://127.0.0.1:$Port"
$Token = ([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))).ToLowerInvariant()

$Args = @(
  "run",
  "--project", "BookOfEternityClient\BookOfEternityClient.csproj",
  "--no-restore",
  "--",
  $RunRoot,
  "--agent-console",
  "--agent-url", $Base,
  "--agent-token", $Token,
  "--plain-output"
)

$Process = Start-Process -FilePath "dotnet" `
  -ArgumentList $Args `
  -WorkingDirectory (Get-Location) `
  -RedirectStandardOutput (Join-Path $RunRoot "stdout.txt") `
  -RedirectStandardError (Join-Path $RunRoot "stderr.txt") `
  -WindowStyle Hidden `
  -PassThru
```

To let the client generate a per-run token instead, use `--agent-token auto` and discover it from stderr after startup:

```powershell
Select-String -Path (Join-Path $RunRoot "stderr.txt") -Pattern "Agent Console token:" |
  Select-Object -Last 1
```

Copy the token into `$Token` for the current shell only. This workflow does not store secrets in repo files; do not paste real tokens into committed docs, scripts, issues, or PR comments.

## Launch from Hermes bash

Use this shape in Hermes/git-bash style shells. It keeps the run root outside the repository and stores logs there.

```bash
RUN_ROOT="${TMPDIR:-/tmp}/boe-agent-console-$(date +%s)-$$"
mkdir -p "$RUN_ROOT"
cp -R FileSystemExample/game_session "$RUN_ROOT/game_session"

PORT=8790
BASE="http://127.0.0.1:$PORT"
TOKEN="$(openssl rand -hex 32 2>/dev/null || uuidgen | tr -d '-')"

dotnet run --project BookOfEternityClient/BookOfEternityClient.csproj --no-restore -- \
  "$RUN_ROOT" \
  --agent-console \
  --agent-url "$BASE" \
  --agent-token "$TOKEN" \
  --plain-output \
  > "$RUN_ROOT/stdout.txt" \
  2> "$RUN_ROOT/stderr.txt" &
CLIENT_PID=$!
```

For generated-token mode, replace `--agent-token "$TOKEN"` with `--agent-token auto`, wait for startup, then read the token from the bounded run log:

```bash
TOKEN="$(sed -n 's/^Agent Console token: //p' "$RUN_ROOT/stderr.txt" | tail -1)"
test -n "$TOKEN"
```

## Ordinary foreground launch

When you want to watch stderr directly in a normal developer shell, launch in one terminal and run `curl` from another. Replace `<run-root>`, `<port>`, and `<token>` with local values:

```bash
dotnet run --project BookOfEternityClient/BookOfEternityClient.csproj --no-restore -- \
  "<run-root>" \
  --agent-console \
  --agent-url http://127.0.0.1:<port> \
  --agent-token <token> \
  --plain-output
```

`--agent-token auto` is also valid here; copy the printed `Agent Console token:` value into a local shell variable only.

## Observe the client

Read endpoints do not require a token. During early startup, the snapshot endpoint can return an empty/no-content response or `null`; wait and retry until a screen appears.

Endpoint summary:

- `GET /api/agent-console/snapshot`
- `GET /api/agent-console/events`
- `POST /api/agent-console/key`
- `POST /api/agent-console/text`
- `POST /api/agent-console/action`
- `POST /api/agent-console/default-action`
- `POST /api/agent-console/return-to-game-loop-step`

```bash
curl -sS "$BASE/api/agent-console/snapshot"
curl -sS "$BASE/api/agent-console/events"
```

Optional bounded polling capture:

```bash
curl -sS "$BASE/api/agent-console/events" >> "$RUN_ROOT/events.jsonl"
```

Expected first useful snapshot:

- `screenId`: usually `main-menu`
- `mode`: `menu`
- `awaitingInput`: `true`
- `inputKind`: `menuSelection`
- `actions`: player-visible options with ids such as `option-0`

## Submit live input

Control endpoints require `Authorization: Bearer <token>`. Use the snapshot's `inputKind` before choosing an input endpoint.

Send a key:

```bash
curl -sS -X POST "$BASE/api/agent-console/key" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  --data-raw '{"key":"Down"}'
```

Send text only when the current snapshot is waiting for text, for example `inputKind: "text"`:

```bash
curl -sS -X POST "$BASE/api/agent-console/text" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  --data-raw '{"text":"look north"}'
```

Submit a semantic action from the current snapshot:

```bash
curl -sS -X POST "$BASE/api/agent-console/action" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  --data-raw '{"actionId":"option-0","screenId":"main-menu","inputKind":"menuSelection"}'
```

For menu actions without shortcuts, the API resolves the action to a safe existing console input: a digit selection when possible, or Enter when the action is already selected/default. If the first action call only changes selection, read the next snapshot and submit the selected action again to activate it.

When an autonomous live-test driver only needs to accept the current default action, prefer the race-safe default endpoint. It reads the latest server-side snapshot at the moment of the call and does not require the caller to pass a `screenId`:

```bash
curl -sS -X POST "$BASE/api/agent-console/default-action" \
  -H "Authorization: Bearer $TOKEN"
```

This endpoint is intentionally narrow: it queues only an enabled action marked `isDefault`. It rejects screens that are not awaiting input or do not expose an enabled default action.

When an autonomous live test drills into a local command screen (`explorer-command-*` or `explorer-selection-*`), prefer the harness step endpoint instead of guessing Russian labels for “continue”, “back”, or “close”:

```bash
curl -sS -X POST "$BASE/api/agent-console/return-to-game-loop-step" \
  -H "Authorization: Bearer $TOKEN"
```

This endpoint queues exactly one safe step toward `game-loop`: Enter for local command key-continuations, or the exposed back/close/return action for local command menus. Poll the next snapshot and call it again until the current `screenId` is `game-loop`. It rejects non-local screens and does nothing when already at `game-loop`.

PowerShell uses the same endpoint shapes with `curl.exe`:

```powershell
curl.exe -sS "$Base/api/agent-console/snapshot"
curl.exe -sS -X POST "$Base/api/agent-console/key" -H "Authorization: Bearer $Token" -H "Content-Type: application/json" --data-raw '{"key":"Down"}'
curl.exe -sS -X POST "$Base/api/agent-console/text" -H "Authorization: Bearer $Token" -H "Content-Type: application/json" --data-raw '{"text":"look north"}'
curl.exe -sS -X POST "$Base/api/agent-console/action" -H "Authorization: Bearer $Token" -H "Content-Type: application/json" --data-raw '{"actionId":"option-0","screenId":"main-menu","inputKind":"menuSelection"}'
curl.exe -sS -X POST "$Base/api/agent-console/default-action" -H "Authorization: Bearer $Token"
curl.exe -sS -X POST "$Base/api/agent-console/return-to-game-loop-step" -H "Authorization: Bearer $Token"
```

## Safe read-only command sweep

Use `scripts/agent-console-readonly-sweep.ps1` for any read-only command sweep where the goal is to inspect command output and return to the player prompt without submitting a turn. This is the harness-owned path for command audits; avoid ad-hoc loops that sleep blindly or guess which menu label means "back".

Example:

```powershell
$Commands = @(
  "/перья",
  "/хроники_посмертия",
  "/архив_души",
  "/spiritual_arts",
  "/spiritual_combat_log"
)

.\scripts\agent-console-readonly-sweep.ps1 `
  -Base $Base `
  -Token $Token `
  -Commands $Commands `
  -ForbiddenPattern @("specialArtAudit", "baseOperation", "#[turn]") `
  -OutputPath (Join-Path $RunRoot "readonly-command-sweep.json")
```

The helper waits for `screenId: game-loop` with `inputKind: text`, submits each command through `/api/agent-console/text`, tolerates the short `command-processing` loading snapshot used for local slash commands, records the resulting command snapshot, scans for forbidden markers, and returns through `/api/agent-console/return-to-game-loop-step`. It fails closed if a read-only command reaches `turn-preparing`, if the screen is not awaiting input, or if returning from a command screen would require typing into another text prompt.

For read-only command sweeps, do not use `/default-action`. That endpoint is useful only when the explicit test intent is to accept the current player-visible default action. It is not a safe unwind primitive for command-output audits because a stale or unexpected screen can turn a data inspection into a player turn.

## Safe smoke path

1. Launch with a copied `FileSystemExample/game_session` under a disposable run root.
2. Poll `GET /api/agent-console/snapshot` until `screenId` is `main-menu`.
3. Read `actions` and find the player-visible Exit option.
4. Use `POST /api/agent-console/key` or `POST /api/agent-console/action` to move selection.
5. Read the next snapshot and confirm `selectedIndex` changed.
6. Use `POST /api/agent-console/action` for the selected Exit action.
7. Wait for the process to exit with code `0`.
8. Keep `stdout.txt`, `stderr.txt`, and optional `events.jsonl` only inside the run root.

The automated coverage for this path is `AgentConsoleLiveSmokeTests`, which launches the real console client, observes a main-menu snapshot, submits key/action control requests through the API, reads events, and exits through the menu in a disposable sandbox.

## Shutdown and cleanup

Preferred shutdown is player-visible: activate the main-menu Exit action through the API and wait for exit code `0`.

PowerShell fallback cleanup:

```powershell
if ($Process -and -not $Process.HasExited) {
  Stop-Process -Id $Process.Id -Force
}
Remove-Item -Recurse -Force $RunRoot
```

Bash fallback cleanup:

```bash
if kill -0 "$CLIENT_PID" 2>/dev/null; then
  kill "$CLIENT_PID"
  wait "$CLIENT_PID" 2>/dev/null || true
fi
rm -rf "$RUN_ROOT"
```

Use the fallback only after preserving enough bounded evidence to diagnose a failure.

## Troubleshooting

### port conflict

Symptoms: host startup fails, `curl` connects to the wrong process, or stderr says the address is already in use.

Actions:

1. Pick another loopback port and restart with the same run root or a fresh one.
2. Confirm `BASE`/`$Base` matches the `--agent-url` value exactly.
3. Do not switch to `0.0.0.0` or a LAN address; non-loopback bind rejection is intentional.

### missing token or invalid token

Symptoms: control endpoints return `401`, or startup exits with `Missing value for --agent-token`.

Actions:

1. Launch with `--agent-token auto` or `--agent-token <token>`.
2. For auto mode, read `Agent Console token:` from the run root `stderr.txt`.
3. Send `Authorization: Bearer $TOKEN` on `POST /key`, `POST /text`, and `POST /action`.
4. Keep tokens in shell variables or temporary secret stores. Do not store secrets in repo docs.

### non-loopback bind rejection

Symptoms: startup fails before the API host starts and mentions loopback.

Actions:

1. Use `http://127.0.0.1:<port>`, `http://localhost:<port>`, or `http://[::1]:<port>`.
2. Do not expose remote/non-loopback Agent Console control.
3. Keep control endpoints token-gated even on loopback.

### no snapshot yet

Symptoms: `GET /api/agent-console/snapshot` returns empty/no-content, `null`, or no `screenId`.

Actions:

1. Confirm the process is still running.
2. Check `stderr.txt` for startup errors.
3. Wait and retry; the API host can start before the first main-menu render.
4. If no screen appears, preserve `stdout.txt`, `stderr.txt`, and an events capture under the run root.

### blocked/waiting input

Symptoms: the snapshot does not change after input, events show `inputRejected`, or the process keeps waiting.

Actions:

1. Inspect the latest snapshot's `awaitingInput` and `inputKind`.
2. Use `/key` for `menuSelection`, `key`, or confirmation-style navigation.
3. Use `/text` only for `text` prompts, and send a complete line.
4. Use `/action` with the current `screenId` and matching `inputKind`; stale screen ids are rejected by design.
5. Use `/default-action` when the next intended input is simply the current enabled default action and a screen transition may have happened between polls.
6. If an action only selects a menu item, read the next snapshot and submit the now-selected action again.

### bounded artifacts

Symptoms: smoke output starts accumulating in the repo or artifacts are too large to review.

Actions:

1. Keep all generated logs under the disposable run root.
2. Capture only `stdout.txt`, `stderr.txt`, optional `events.jsonl`, and a short note with the last successful step.
3. Delete passing run roots after verification.
4. Do not commit generated run output.
