# Implementation Plan: Daren Scene 08 Full Literary Page

## Technical Context

- Repository: `E:/Games/worktrees/boe-976-daren-renara-voice`
- Branch: `work/976-daren-renara-voice`
- Runtime authority: shared C# Daren QTE route data in `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
- Parent umbrella: #955
- Scene issue: #976

## Constraints

- Do not add browser-only or console-only prose.
- Do not add a new dialogue runtime/state file/endpoint.
- Do not change QTE mechanics, route ids, beat order, action ids, check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, or frontend files.
- Preserve already-merged scene pages #969-#975 unless a neutral shared test helper must change.
- Do not close #955 from this task.
- Do not broaden into #977-#983 or result/aftermath children.

## TDD Strategy

1. Add a focused `DarenQteShowcaseTests` guard for `ward_steward_parley` before changing the scene prose.
2. The guard should assert:
   - title and shared route data parity;
   - existing action/check/routing/scoring invariants for the beat;
   - substantial page length and sentence count;
   - Daren active POV/protagonist presence;
   - grouped motif coverage for Renara as named ward authority, voice/reflection/glass/seal presence, dialogue/question/answer pressure, false-seal/house-silencing stakes, and carry-forward from the rune-memory beat into the physical-pressure beat;
   - absence of default player-facing technical terms including `QTE`.
3. Run focused Daren tests and record RED evidence against the existing compact synopsis.
4. Replace only the `ward_steward_parley` narrative / player text in `QteSceneService.Daren.cs` with a full Russian dark-fantasy magical-security dialogue scene page.
5. Run focused tests and affected slice to GREEN.

## Verification Commands

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

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

git diff --check origin/main...HEAD
```

Run frontend verification only if frontend/React/browser files change or a browser rendering bug is found.

## Local Implementation Evidence

- Added #976 focused guard in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before production prose changes.
- RED focused Daren run: 48 passed / 1 failed / 0 skipped / 49 total; the new guard failed for the expected compact-synopsis reason.
- GREEN focused Daren run: 49 passed / 0 failed / 0 skipped / 49 total.
- Affected Daren/QTE/docs/browser C# slice: 318 passed / 0 failed / 0 skipped / 318 total.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: 0 warnings / 0 errors.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`: 0 warnings / 0 errors.
- Working-tree `git diff --check`: no whitespace errors.
- Added-line production C# forbidden-term scan: `NO_MATCHES`.
- No frontend/React files changed, so frontend verification was not run.

## Review Requirements

Independent review must check:

- `ward_steward_parley` literary quality against the user's target bar;
- Daren is the active protagonist in a magical-security dialogue scene rather than a dry description of Renara;
- Renara Wardova is personified through voice/reflection/ward authority, asks or pressures Daren, and receives a visible answer strategy;
- the scene naturally leads into the existing `PrecisionChoice` action without adding a new dialogue runtime;
- the prose preserves carry-forward from `rune_memory` and leads toward `physical_pressure` without rewriting either scene;
- no QTE/reward/runtime drift;
- tests are meaningful grouped guards, not a weak single-token bucket;
- Spec Kit artifacts do not stale-reference completed #969-#975 as current source issues.
