# Implementation Plan: Daren Scene 04 Full Literary Page

## Technical Context

- Repository: `E:/Games/worktrees/boe-972-daren-stealth-crossing`
- Branch: `work/972-daren-stealth-crossing`
- Runtime authority: shared C# Daren QTE route data in `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
- Parent umbrella: #955
- Scene issue: #972

## Constraints

- Do not add browser-only or console-only prose.
- Do not change QTE mechanics, route ids, beat order, action ids, check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, or frontend files.
- Preserve already-merged scene pages #969-#971 unless a neutral shared test helper must change.
- Do not close #955 from this task.
- Do not broaden into #973-#983.

## TDD Strategy

1. Add a focused `DarenQteShowcaseTests` guard for `stealth_crossing` before changing the scene prose.
2. The guard should assert:
   - title and shared route data parity;
   - existing action/check/routing/scoring invariants for the beat;
   - substantial page length and sentence count;
   - Daren active POV/protagonist presence;
   - grouped gallery/portrait/light/guard/breath/noise/movement motif coverage;
   - absence of default player-facing technical terms.
3. Run focused Daren tests and record RED evidence against the existing compact synopsis.
4. Replace only the `stealth_crossing` narrative / player text in `QteSceneService.Daren.cs` with a full Russian dark-fantasy stealth scene page.
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

- `stealth_crossing` literary quality against the user's target bar;
- no forced unrelated dialogue if the scene is physical/stealth rather than social;
- Daren remains active protagonist;
- no QTE/reward/runtime drift;
- tests are meaningful grouped guards, not a weak single-token bucket;
- Spec Kit artifacts do not stale-reference #969/#970/#971 as source issues for this task.
