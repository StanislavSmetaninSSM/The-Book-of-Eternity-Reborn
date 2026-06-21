# Implementation Plan: Browser Console Command Parity Audit

**Branch**: `work/1119-browser-console-audit` | **Date**: 2026-06-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/1119-browser-console-command-parity-audit/spec.md`

## Summary

Create a durable audit for #1119 that classifies every browser command coverage entry against console semantic parity, links follow-up issues for gaps, and adds a source guard so the audit cannot silently omit future commands.

## Technical Context

**Language/Version**: C#/.NET 8 for coverage/source guard tests; Markdown for audit artifact

**Primary Dependencies**: Existing browser command coverage service and xUnit test project

**Storage**: Repository documentation under `docs/audits/`

**Testing**: Focused `dotnet test` filter `BrowserCommandCoverage`

**Target Platform**: Local Windows development environment

**Project Type**: Local game client with console and browser UI

**Performance Goals**: Audit guard should run within the existing focused test filter without noticeable delay.

**Constraints**: Do not change player runtime behavior, command contracts, GM prompts, or browser UI in this audit PR.

**Scale/Scope**: All commands exposed by `BrowserCommandCoverageService` and `/api/explorer/command-coverage`.

**Source Issue(s)**: [#1119](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1119), parent [#1118](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1118)

**Contract Scope**: player-facing, console, browser, frontend documentation

**Verification Commands**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter BrowserCommandCoverage --logger "console;verbosity=minimal"
```

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **GitHub traceability**: Pass. Source issues #1119 and #1118 are linked.
- **Spec Kit fit**: Pass. This is browser/console parity work across many command families.
- **Player-facing integrity**: Pass. The audit classifies player-facing data loss and debug/API leakage; no runtime UI changes happen here.
- **Contract/state authority**: Pass. No command contract or state authority changes are planned.
- **Test-first path**: Pass. Add a failing audit source guard before creating the audit document.
- **Verification evidence**: Pass. Focused C# verification is listed.
- **Agent orchestration**: Pass. Work stays in this Codex branch and will be verified before merge.

## Project Structure

### Documentation (this feature)

```text
specs/1119-browser-console-command-parity-audit/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs
BookOfEternityClient/WebUi/ExplorerWebCommandService.cs
BookOfEternityClient/CommandProtocol/
BookOfEternityClient/UI/ExplorerMode/
BookOfEternityClient.Tests/
docs/audits/
```

**Structure Decision**: Keep the deliverable as a docs audit plus a focused C# source guard. No frontend or runtime files should change unless verification reveals the existing coverage test cannot support the guard.

## Phase 0: Research

- **Decision**: Use `BrowserCommandCoverageService` as the command inventory authority.
  **Rationale**: It is the service behind `/api/explorer/command-coverage` and already includes browser migration metadata.
  **Alternatives considered**: Scraping command help output was rejected because it would miss browser-only status metadata.

- **Decision**: Treat semantic parity, not pixel-perfect rendering, as the audit standard.
  **Rationale**: The parent issue explicitly allows browser-native layout but forbids loss of player-facing information.
  **Alternatives considered**: Exact console layout cloning was rejected as out of scope and bad browser UX.

- **Decision**: Add a source guard test for command IDs.
  **Rationale**: The audit should fail when coverage changes without documentation.
  **Alternatives considered**: Manual review only was rejected because #1119 is meant to protect future browser parity work.

## Phase 1: Design

### Data Model

- **Coverage entry**: command ID, aliases, group/realm, browser status, handler kind, form mode, audit status, follow-up issue, gap summary, notes.
- **Audit row**: command ID, aliases, realm, browser surface, console sections, browser sections, missing details, raw JSON dependency, drill-down status, priority, follow-up issue, notes.
- **Priority**: P0 blocker, P1 major player-facing loss, P2 notable quality gap, P3 adequate/minor/no-fix/advanced-only.

### Contracts

- The audit document contract is `docs/audits/browser-console-command-parity-audit.md`.
- Command IDs are referenced as backticked literals so the source guard can match them deterministically.

### Quickstart

1. Run the focused browser command coverage test filter.
2. Open `docs/audits/browser-console-command-parity-audit.md`.
3. Confirm the summary includes #1121-#1126 status/order.
4. Confirm non-adequate rows include severity and linked issue or no-fix reason.

## Constitution Check

- **GitHub traceability**: Pass.
- **Spec Kit fit**: Pass.
- **Player-facing integrity**: Pass.
- **Contract/state authority**: Pass; no GM docs/examples required because this is documentation and test guard only.
- **Test-first path**: Pass.
- **Verification evidence**: Pass.
- **Agent orchestration**: Pass.

## Complexity Tracking

No constitution violations.
