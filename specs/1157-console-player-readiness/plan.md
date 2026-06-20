# Implementation Plan: Console Client Live Player-Readiness Pass

**Branch**: `1157-console-player-readiness` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/1157-console-player-readiness/spec.md`

## Summary

Run a live, player-bound console E2E pass against a disposable session, using Agent Console for player observations and a Codex-backed GM bridge for normal GM responses. Treat the console UI as the primary product surface: record command-output quality, fix narrow blocking/high-impact defects with tests, and file follow-up issues for broad or out-of-scope work.

## Technical Context

**Language/Version**: C# / .NET 8, PowerShell 7-compatible scripts

**Primary Dependencies**: Spectre.Console, file-backed JSON session state, Agent Console loopback API, ConPTY GM bridge, Codex CLI

**Storage**: Disposable copied `game_session` directory and temporary run artifacts under `%TEMP%`

**Testing**: `dotnet test` for C# regression/source-guard coverage, live Agent Console playtest for player-facing behavior

**Target Platform**: Local Windows console client

**Project Type**: Local game client and test harness workflow

**Performance Goals**: Player-visible command responses should complete without hangs; live run artifacts should be captured before and after each action

**Constraints**: Do not mutate production/developer sessions; do not use browser/frontend GLM task scope; player-side live test must not inspect JSON during active play

**Scale/Scope**: One short live adventure attempt plus covered command-output audit; in-scope fixes limited to narrow console/player-facing blockers or repeated high-impact defects

**Source Issue(s)**: #1157 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1157

**Contract Scope**: player-facing console output, Agent Console observation boundary, GM bridge launch/failure behavior, verification artifacts

**Verification Commands**:

- `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsole"`
- Focused tests for any fixed defect
- Full `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore` before merging code changes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **GitHub traceability**: Pass. Source issue #1157 exists and is linked in spec and plan.
- **Spec Kit fit**: Pass. The work is player-facing console UX/E2E, may span code/tests/docs, and is expected to run across multiple steps.
- **Player-facing integrity**: Pass. Requirements explicitly prohibit debug/API leakage in normal console output and require Russian in-world terminology for touched surfaces.
- **Contract/state authority**: Pass. Summary/detail discoverability is in scope for audit and narrow fixes; GM-authored contract changes require docs/examples or follow-up issues.
- **Test-first path**: Pass. Narrow fixes require failing regression/source-guard tests before implementation unless explicitly documented as live-only.
- **Verification evidence**: Pass. Focused Agent Console tests, focused defect tests, full C# suite, and live-run artifacts are required.
- **Agent orchestration**: Pass. This feature uses Spec Kit artifacts plus Superpowers TDD/debugging/verification; no Hermes delegation is required yet.

## Project Structure

### Documentation (this feature)

```text
specs/1157-console-player-readiness/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── console-live-playtest-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/                 # Console client, launcher scripts, Agent Console integration
BookOfEternityClient.Tests/           # C# tests, source guards, live-smoke helpers
BookOfEternityGMBridge/               # GM bridge host
docs/e2e/                             # Existing Agent Console runbooks
docs/superpowers/plans/               # Existing #909 live playtest plan
```

**Structure Decision**: Use existing console, bridge, and Agent Console harnesses. Add or modify source only after the live pass identifies a tracked, in-scope defect and a failing test has been captured.

## Phase 0: Research

See [research.md](research.md).

## Phase 1: Design & Contracts

- Artifact/data model: [data-model.md](data-model.md)
- Player/agent boundary contract: [contracts/console-live-playtest-contract.md](contracts/console-live-playtest-contract.md)
- Validation quickstart: [quickstart.md](quickstart.md)

## Constitution Check (Post-Design)

- **GitHub traceability**: Pass. All design artifacts link #1157.
- **Spec Kit fit**: Pass. The artifacts decompose live E2E, audit, repair, and verification.
- **Player-facing integrity**: Pass. Contract preserves Agent Console as player boundary and marks debug leakage as a defect.
- **Contract/state authority**: Pass. Data model includes defects and repair evidence; summary/detail authority is explicitly audited.
- **Test-first path**: Pass. Tasks require red tests for in-scope fixes.
- **Verification evidence**: Pass. Quickstart and tasks name focused and broad verification.
- **Agent orchestration**: Pass. No external delegation; local Codex handles execution and verification.

## Complexity Tracking

No constitution violations.
