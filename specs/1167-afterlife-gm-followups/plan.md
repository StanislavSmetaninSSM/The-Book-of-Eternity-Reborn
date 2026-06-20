# Implementation Plan: Afterlife and GM Bridge Follow-ups

**Branch**: `feature/1167-afterlife-gm-followups` | **Date**: 2026-06-20 | **Spec**: `specs/1167-afterlife-gm-followups/spec.md`

**Input**: Feature specification from `specs/1167-afterlife-gm-followups/spec.md`

## Summary

Close the remaining non-browser issues #1167-#1171 by separating player-facing afterlife output from audit diagnostics, isolating the hidden Codex GM bridge from repository coding-agent context, and preserving Cyrillic daemon logs. Implementation will use focused regression tests before behavior changes, update afterlife audit documentation, and avoid any browser client edits.

## Technical Context

**Language/Version**: C#/.NET 8, PowerShell launcher scripts, Spectre.Console rendering.

**Primary Dependencies**: Existing `BookOfEternityClient` console/runtime services, `BookOfEternityGMBridge`, file-backed JSON state, xUnit tests.

**Storage**: Local files under `game_session`, `game_state/control`, logs, and profile/config JSON.

**Testing**: `dotnet test` focused xUnit filters; script/source-guard tests where existing patterns exist.

**Target Platform**: Local Windows console play and loopback GM bridge execution.

**Project Type**: Local game client and local GM bridge runtime.

**Performance Goals**: Ordinary hidden-GM live turns must be diagnosable and should avoid 7+ minute coding-agent context stalls.

**Constraints**: No browser/frontend changes. No cloud dependencies. No new afterlife mechanics. Preserve explicit audit/debug access.

**Scale/Scope**: Five open non-browser GitHub issues, primarily console UX and GM bridge reliability.

**Source Issue(s)**: #1167, #1168, #1169, #1170, #1171.

**Contract Scope**: Player-facing console, afterlife audit/player split, GM bridge launch profile, daemon diagnostics, docs/tests.

**Verification Commands**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridge|GmWorkerBridge|GmWorker|Daemon|Encoding|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|ExplorerModeCommandTests.Afterlife"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests"
```

## Constitution Check

- **GitHub traceability**: Pass. Source issues are linked in `spec.md`, this plan, and `tasks.md`.
- **Spec Kit fit**: Pass. The work is multi-file, player-facing, afterlife-related, and touches GM bridge runtime behavior.
- **Player-facing integrity**: Pass. Default console surfaces must use Russian player terms and hide debug/API fields unless audit mode is explicit.
- **Contract/state authority**: Pass. Existing audit access is preserved; afterlife docs/examples coverage must be checked when player-visible contract behavior changes.
- **Test-first path**: Pass. Each behavior change has a regression test task before implementation.
- **Verification evidence**: Pass. Focused runtime/docs tests and broad non-browser C# tests are listed.
- **Agent orchestration**: Pass. Active spec/plan/tasks and issue list define the context for any delegated worker.

## Project Structure

### Documentation (this feature)

```text
specs/1167-afterlife-gm-followups/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/                 # Console client, afterlife commands, launcher scripts
BookOfEternityClient.Tests/           # Regression/source-guard/docs tests
BookOfEternityGMBridge/               # Hidden GM bridge launch/runtime settings
OtherGuides/                          # Afterlife contract guidance if touched
Examples/                             # Worked afterlife examples if contract guidance changes
docs/                                 # Audit notes and implementation reports
```

**Structure Decision**: Keep changes in existing command/result builders, bridge settings, launcher scripts, and tests. Do not add new cross-cutting frameworks.

## Complexity Tracking

No constitution violations planned.
