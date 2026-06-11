# Implementation Plan: Daren Scene 07 Full Literary Page

## Technical Context

- Repository: `E:/Games/worktrees/boe-975-daren-rune-memory`
- Branch: `work/975-daren-rune-memory`
- Runtime authority: shared C# Daren QTE route data in `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
- Parent umbrella: #955
- Scene issue: #975

## Constraints

- Do not add browser-only or console-only prose.
- Do not add a new dialogue runtime/state file/endpoint.
- Do not change QTE mechanics, route ids, beat order, action ids, check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, or frontend files.
- Preserve already-merged scene pages #969-#974 unless a neutral shared test helper must change.
- Do not close #955 from this task.
- Do not broaden into #976-#983 or result/aftermath children #988-#1014.

## TDD Strategy

1. Add a focused `DarenQteShowcaseTests` guard for `rune_memory` before changing the scene prose.
2. The guard should assert:
   - title and shared route data parity;
   - existing action/check/routing/scoring invariants for the beat;
   - substantial page length and sentence count;
   - Daren active POV/protagonist presence;
   - grouped motif coverage for case door/glass/runes, magical-security/ward/watchful-house pressure, Daren's eyes/breath/memory/body control, alarm/trace/guard stakes, and the natural lead-in to repeating the rune pattern;
   - absence of default player-facing technical terms including `QTE`.
3. Run focused Daren tests and record RED evidence against the existing compact synopsis.
4. Replace only the `rune_memory` narrative / player text in `QteSceneService.Daren.cs` with a full Russian dark-fantasy magical-security scene page.
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

- `rune_memory` literary quality against the user's target bar;
- Daren is the active protagonist in a magical-security/memory scene rather than a dry description of runes;
- case door/glass/rune details, cold blue magical pressure, Daren's eyes/breath/body control/memory craft, and alarm/trace/guard stakes are vivid and grouped;
- the scene naturally leads into the existing `PatternMemory` action without adding a new social/dialogue runtime;
- no QTE/reward/runtime drift;
- tests are meaningful grouped guards, not a weak single-token bucket;
- Spec Kit artifacts do not stale-reference completed #969-#974 as current source issues.

## Local Implementation Evidence

- Focused RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed after the test-only change with 47 passed / 1 failed / 0 skipped / 48 total. Failure reason: `rune_memory` was still a compact synopsis.
- Focused GREEN: the same focused command passed after the shared prose replacement with 48 passed / 0 failed / 0 skipped / 48 total.
- Affected slice GREEN: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed with 317 passed / 0 failed / 0 skipped / 317 total.
- Build evidence: both `BookOfEternityClient` and `BookOfEternityClient.Tests` built with 0 warnings / 0 errors.
- Static guard: added production C# lines in `QteSceneService.Daren.cs` scanned for forbidden default-player technical terms and returned `NO_MATCHES`.
- Diff scope: changed shared Daren route prose, focused Daren tests, and #975 Spec Kit artifacts only; no frontend verification was required because no frontend/React files changed and no browser rendering bug was found.
