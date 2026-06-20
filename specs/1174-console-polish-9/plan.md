# Implementation Plan: Console Client Polish Pass 2 to 9/10

**Branch**: `work/1174-console-polish-9` / follow-up `work/1181-console-followups` | **Date**: 2026-06-20 | **Spec**: `specs/1174-console-polish-9/spec.md`

## Source Issues

- #1174, #1175, #1176, #1177, #1178, #1179, #1180, #1181, #1182, #1183

## Summary

Implement the non-browser, non-QTE console 9/10 readiness pass in sequence: document command coverage, add a dry command sweep, repair narrow player-facing output/navigation defects, run a live Codex-GM console playtest without QTE, publish a final acceptance score, and harden the accepted-turn contract where live play revealed that narrative facts could bypass player-facing command surfaces.

## Technical Context

- **Language/runtime**: C# / .NET 8, xUnit tests, Spectre.Console, file-backed JSON game state.
- **Primary source areas**:
  - `BookOfEternityClient/UI/` and `BookOfEternityClient/Services/` for console command rendering and command-result builders.
  - `BookOfEternityClient.Tests/` for fixture integrity, command-display, dry sweep, and source-guard tests.
  - `FileSystemExample/game_session` and `BookOfEternityClient.Tests/Fixtures/` if reusable test data needs documented coverage.
  - `docs/audits/` for command matrix, dry sweep reports, and final playtest/audit reports.
  - `specs/1174-console-polish-9/` for durable feature requirements and task evidence.
- **Non-goal source areas**:
  - `BookOfEternityClient.WebFrontend/`
  - QTE services, QTE scene data, QTE test mode, and QTE balance code.

## Constitution Check

- **Issue traceability**: Pass. Source issues #1174-#1180 are listed in spec, plan, and tasks.
- **Spec Kit fit**: Pass. The work is an epic with player-facing console UX, afterlife surfaces, fixtures, tests, and live E2E evidence.
- **Player-facing integrity**: Pass. Requirements prohibit debug/API/raw JSON leakage in ordinary console output.
- **Contract/state authority**: Pass. Displayed summaries must resolve to details, explicit diagnostic mode, or follow-up issues. Contract changes require docs/examples/tests.
- **Test-first path**: Pass. Runtime behavior fixes require RED/GREEN tests before production edits.
- **Verification evidence**: Pass. Focused tests, build, dry sweep, live playtest, and final score are required.
- **Agent orchestration**: Pass. Use Superpowers TDD/debugging/verification. Browser and QTE work are excluded by direct user instruction.

## Implementation Approach

1. Create and verify the `1174` Spec Kit artifacts and mark source issues in progress only when implementation begins.
2. Build the command matrix from command registration, help text, command builders, tests, and reusable fixtures.
3. Add or document a dry sweep that exercises non-QTE console command output without mutating the user's live session.
4. Run the sweep and classify findings into narrow fixes, follow-up issues, or explicit diagnostic surfaces.
5. For each narrow fix, write a failing regression test first, implement the smallest display/navigation repair, and rerun focused tests.
6. Run a live Codex-GM console playtest without QTE after dry fixes and record friction findings.
7. For #1181 live-playtest fact persistence gaps, prefer validation/prompt contract hardening over guessing facts from prose; ensure the GM writes structured state that console commands can display.
8. Produce a final acceptance audit and score. Merge only with verification evidence.

## Risk Controls

- Do not touch browser/frontend files.
- Do not touch QTE engine, QTE scripts, or QTE balance files.
- Do not mutate `BookOfEternityClient/game_session` as a normal test path.
- Do not leave generated logs, screenshots, or copied sessions committed unless they are intentionally lightweight audit artifacts.
- Do not convert diagnostic/audit data into ordinary player output.
- Do not close issues from agent reports alone; verify diffs and test evidence first.

## Verification Commands

```powershell
$env:SPECIFY_FEATURE_DIRECTORY='specs/1174-console-polish-9'
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore

dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "<focused-filter>"

dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"

git diff --check origin/main...HEAD
```

If afterlife runtime contracts or GM-facing docs change:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

## Expected Closure Evidence

- `docs/audits/console-command-matrix-1174.md` covers all non-QTE console commands.
- A dry sweep command/report exists and is linked from #1176.
- Narrow Mortal World and afterlife display fixes have RED/GREEN tests.
- Broad remaining defects have linked GitHub issues.
- `docs/audits/console-live-playtest-1179.md` records the live route and score.
- `docs/audits/console-acceptance-score-1180.md` records the final 1-10 assessment.
