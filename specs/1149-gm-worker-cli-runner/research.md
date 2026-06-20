# Research: GM Worker CLI Runner

## Decision 1: Use a repo-owned PowerShell runner

**Decision**: Add `BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1`.

**Rationale**: Existing local bridge/daemon entrypoints are PowerShell scripts, and current C# tests already use PowerShell fake workers. A PowerShell runner is easy to invoke from a worker `launchCommand`, can read environment variables directly, can launch nested CLI commands with redirected stdin, and does not require adding a new compiled tool.

**Alternatives considered**:

- C# console subcommand: stronger type safety, but requires packaging and launch-path decisions not needed for v1.
- Inline prompt in every worker profile: simpler short-term, but duplicates the protocol and makes profiles fragile.
- Agent-specific scripts per Codex/Gemini: too much branching before real CLI incompatibilities are proven.

## Decision 2: Dry-run mode is a first-class contract

**Decision**: Runner supports `-DryRun` and optional `-PromptOutPath`.

**Rationale**: CI and local tests should validate the prompt/proposal protocol without requiring Codex, Gemini, network access, or user credentials. Dry-run also lets a GM maintainer inspect the prompt before enabling a worker profile.

**Alternatives considered**:

- Only source-guard tests: weaker because they do not execute environment validation or prompt generation.
- Mock Codex/Gemini CLI binaries: more setup and still less clear than a dry-run prompt artifact.

## Decision 3: Runner validates handoff, bridge validates proposal content

**Decision**: The runner requires a non-empty proposal file after a successful agent exit, but does not parse or validate proposal JSON.

**Rationale**: Existing `GmWorkerBridgePool` already validates JSON, worker id, task id, task type permissions, changed-file scope, and proposal-only restrictions. Duplicating that validation in a PowerShell script would create drift.

**Alternatives considered**:

- Parse proposal JSON in the runner: catches errors earlier, but duplicates C# authority and is harder to keep synchronized.
- Let missing proposal be handled only by C#: possible, but the runner can report a clearer boundary error before the bridge reads the inbox.

## Decision 4: Feed the generated prompt through stdin

**Decision**: In real mode, the runner launches the configured agent command and writes the generated prompt to standard input.

**Rationale**: Stdin avoids command-line length limits and avoids embedding task JSON in arguments. It matches common CLI-agent workflows better than writing temporary prompt files that every agent command must know how to read.

**Alternatives considered**:

- `-PromptFile` handoff: useful later for CLIs that require file input, but not necessary for v1.
- Command-line argument prompt: fragile for long JSON and quoting.

## Decision 5: No new canonical state authority

**Decision**: Runner prompt tells workers to write only the proposal handoff and optional proposal content refs, never canonical `game_session` files.

**Rationale**: This is the main safety property of the multi-agent architecture. The main GM and apply gate remain the only authorities that can accept changes.

**Alternatives considered**:

- Allow validation-repair workers to edit canonical files directly and let validation catch issues: rejected because it breaks auditability and rollback.
