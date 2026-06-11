# Implementation Plan: Daren Scene 05 Full Literary Page

## Technical Context

- Repository: `E:/Games/worktrees/boe-973-daren-keykeeper-gallery`
- Branch: `work/973-daren-keykeeper-gallery`
- Runtime authority: shared C# Daren QTE route data in `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
- Parent umbrella: #955
- Scene issue: #973

## Constraints

- Do not add browser-only or console-only prose.
- Do not add a new dialogue runtime/state file/endpoint.
- Do not change QTE mechanics, route ids, beat order, action ids, check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, or frontend files.
- Preserve already-merged scene pages #969-#972 unless a neutral shared test helper must change.
- Do not close #955 from this task.
- Do not broaden into #974-#983.

## TDD Strategy

1. Add a focused `DarenQteShowcaseTests` guard for `guard_interrogation` before changing the scene prose.
2. The guard should assert:
   - title and shared route data parity;
   - existing action/check/routing/scoring invariants for the beat;
   - substantial page length and sentence count;
   - Daren active POV/protagonist presence;
   - grouped motif coverage for service door/gallery, Lукьян/keykeeper/lantern/keys, suspicion/question/social pressure, Daren's answer/improvisation, and the QTE lead-in;
   - absence of default player-facing technical terms.
3. Run focused Daren tests and record RED evidence against the existing compact synopsis.
4. Replace only the `guard_interrogation` narrative / player text in `QteSceneService.Daren.cs` with a full Russian dark-fantasy social-pressure scene page.
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

- `guard_interrogation` literary quality against the user's target bar;
- Lукьян is a personified NPC and the scene includes real dialogue/social pressure;
- Daren remains active protagonist;
- no QTE/reward/runtime drift;
- tests are meaningful grouped guards, not a weak single-token bucket;
- Spec Kit artifacts do not stale-reference #969-#972 as current source issues.
