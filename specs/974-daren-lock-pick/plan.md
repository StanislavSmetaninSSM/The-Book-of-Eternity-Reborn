# Implementation Plan: Daren Scene 06 Full Literary Page

## Technical Context

- Repository: `E:/Games/worktrees/boe-974-daren-lock-pick`
- Branch: `work/974-daren-lock-pick`
- Runtime authority: shared C# Daren QTE route data in `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
- Parent umbrella: #955
- Scene issue: #974

## Constraints

- Do not add browser-only or console-only prose.
- Do not add a new dialogue runtime/state file/endpoint.
- Do not change QTE mechanics, route ids, beat order, action ids, check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, or frontend files.
- Preserve already-merged scene pages #969-#973 unless a neutral shared test helper must change.
- Do not close #955 from this task.
- Do not broaden into #975-#983 or result/aftermath children #988-#1014.

## TDD Strategy

1. Add a focused `DarenQteShowcaseTests` guard for `lock_pick` before changing the scene prose.
2. The guard should assert:
   - title and shared route data parity;
   - existing action/check/routing/scoring invariants for the beat;
   - substantial page length and sentence count;
   - Daren active POV/protagonist presence;
   - grouped motif coverage for cabinet/door/old lock, lock-picking craft/pins/picks/plate, Daren's hands/breath/listening/intent, stealth/evidence/noise/scratch stakes, and the natural lead-in to setting the pins;
   - absence of default player-facing technical terms including `QTE`.
3. Run focused Daren tests and record RED evidence against the existing compact synopsis.
4. Replace only the `lock_pick` narrative / player text in `QteSceneService.Daren.cs` with a full Russian dark-fantasy tactile lock-picking scene page.
5. Run focused tests and affected slice to GREEN.

## Verification Commands

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "FullyQualifiedName~DarenQteShowcaseTests" \
  --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" \
  --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

git diff --check origin/main...HEAD
```

Run frontend verification only if frontend/React/browser files change or a browser rendering bug is found.

## Review Requirements

Independent review must check:

- `lock_pick` literary quality against the user's target bar;
- Daren is the active protagonist in a tactile burglary scene rather than a dry description of a lock;
- the old lock/cabinet door mechanics, pins/picks, breath/hands/body control, and evidence/noise/scratch stakes are vivid and grouped;
- the scene naturally leads into the existing `LockPinSet` action without adding a new social/dialogue runtime;
- no QTE/reward/runtime drift;
- tests are meaningful grouped guards, not a weak single-token bucket;
- Spec Kit artifacts do not stale-reference completed #969-#973 as current source issues.
