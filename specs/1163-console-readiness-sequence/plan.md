# Implementation Plan: Console Readiness Sequence 2

**Branch**: `feature/1163-console-readiness` | **Date**: 2026-06-20 | **Spec**: `specs/1163-console-readiness-sequence/spec.md`

## Source Issues

- #1158, #1160, #1161, #1162, #1163, #1164, #1165, #1166

## Summary

Implement the non-browser readiness sequence in dependency order: stabilize GM worker status/profile configuration, expose live QTE frame state through Agent Console, sweep afterlife output for raw/debug leakage, run a broader console polish pass, then perform a second live console playtest focused on player friction.

## Technical Context

- **Language/runtime**: C# / .NET 8, xUnit tests, Spectre.Console, file-backed JSON session state.
- **Primary source areas**:
  - `BookOfEternityClient/AgentConsole/` for snapshot/event publication.
  - `BookOfEternityClient/Services/QteSceneService.cs` and QTE support services for live frame state.
  - `BookOfEternityClient/Services/GmWorkers/` and state/profile services for worker status and profile cleanup.
  - `BookOfEternityClient/UI/ExplorerMode*` and command-result builders for console output.
  - `BookOfEternityClient.Tests/` for focused regression and E2E tests.
  - `Examples/`, `OtherGuides/`, `TaskGuides/`, and docs only if GM-facing contracts or prompts change.
- **Non-goal source area**: `BookOfEternityClient.WebFrontend/` unless shared compilation requires it.

## Constitution Check

- **Issue traceability**: All work is tied to #1158/#1160-#1166 and those issues are listed in spec, plan, and tasks.
- **Player-facing integrity**: Console and afterlife output changes must avoid debug/API/DTO/raw JSON leakage in normal player flow.
- **Contract/state authority**: QTE Agent Console snapshot is a client-owned observation surface. If state contracts or GM-authored afterlife contracts change, docs/examples/tests must be updated in the same PR.
- **Test-first path**: Behavior changes require RED tests before production code.
- **Verification evidence**: Focused tests, broad tests, build, diff hygiene, and live playtest notes are required before completion.
- **Agent orchestration**: Use Superpowers TDD/debugging/verification. Do not close issues from implementation reports without diff/test evidence.

## Implementation Approach

1. Establish a clean worktree and run baseline build/focused tests.
2. Audit current GM worker code and tests, then implement #1158/#1162 with RED/GREEN coverage.
3. Add the smallest production-safe QTE frame state model needed by Agent Console, with tests for stale/inactive and active timed/non-timed frames.
4. Audit afterlife command outputs with prepared saves. Fix narrow raw/default leakage with tests; create follow-up issues for broad gaps.
5. Run ordinary command-output polish audit. Fix narrow defects with tests and record remaining findings.
6. Run the second live Agent Console playtest after the above gates, using a disposable session and preserving artifacts outside the repo unless a lightweight summary is intentionally committed.
7. Verify, review, commit, PR, merge only when acceptance criteria are met.

## Risk Controls

- Do not mutate the user's live `BookOfEternityClient/game_session`; use copied disposable sessions.
- Do not change browser UI files.
- Do not leave generated logs, screenshots, or temporary sessions committed accidentally.
- Do not retain raw JSON in normal player-facing output unless it is explicitly diagnostic and documented.
- Do not mark Spec Kit tasks complete until there is implementation and verification evidence.

## Verification Commands

Baseline and final verification will include:

```powershell
$env:SPECIFY_FEATURE_DIRECTORY='specs/1163-console-readiness-sequence'
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore

dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsole|GmWorker|Qte|Afterlife"

dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore

git diff --check origin/main...HEAD
```

If afterlife runtime/GM docs change:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

## Expected Closure Evidence

- Issue/PR notes list which issues are fully fixed, which are live-tested, and which require follow-up.
- #1166 report includes route, artifacts, command coverage, findings, and playability score.
- PR body includes local verification evidence and states that browser/frontend work was not touched.
