# Implementation Plan: Daren Literary Scene Prose

**Branch**: `work/957-daren-literary-prose`  
**Spec**: `specs/957-daren-literary-prose/spec.md`  
**Source Issues**: [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)  
**Date**: 2026-06-11

## Summary

Turn the current Daren QTE showcase route from terse mechanical beat copy into concise interactive-book scene prose while preserving the landed QTE engine, route order, scoring, reward profile, and console/browser shared route contract.

## Technical Context

- Project: .NET 8 C# client; shared Daren QTE route authority lives in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Existing planning authority from #956: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`.
- Existing tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, `QteSceneServiceTests.cs`, `ValidationServiceQteTests.cs`, browser contract/workspace tests, and prompt/example documentation coverage tests.
- Current Daren route beat ids: `approach_manor`, `gadget_infiltration`, `stealth_crossing`, `lock_pick`, `rune_memory`, `physical_pressure`, `timed_rhythm`, `route_decision`, `staff_theft`, `pursuit`, `chase_chain`, `hideout_return`.
- Baseline before #957 spec artifact edits: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 291/291.

## Architecture and Files

### Product Route Content

- **Modify**: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Responsibility: shared Daren route/offer/chapter/action text consumed by both console and browser.
  - Expected changes:
    - Replace terse `DarenShowcaseBeat.PlayerText` strings with concise scene prose that explains location, stakes, and the immediate QTE goal.
    - Replace terse action `SuccessText`, `PartialText`, and `FailText` strings with short transition prose.
    - Reword `OfferText` and `IntroNarrative` to be player-facing/in-world while still communicating that this is a separate Daren showcase, not a normal GM turn.
  - Do not change route ids, beat order, QTE check types/configs, routing, score deltas, ending tiers, reward-profile writes, New Game grants, or browser/frontend runtime.

### Planning/Contract Artifact

- **Modify**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`
  - Responsibility: #956 durable scene map and handoff context.
  - Expected changes:
    - Add #957 to `sourceIssues` or a handoff/implementation note only if useful.
    - Preserve all existing #956 structural invariants and future links for #958-#961.
  - Do not add a new runtime schema requirement unless the implementation tests need objective alignment data.

### Tests

- **Modify**: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add focused tests over `QteSceneService.GetDarenShowcaseRoute()` that fail on bare/mechanical-only Daren chapter prose and terse/debug-like action result text.
  - Suggested objective guards:
    - Every chapter narrative is non-empty, has at least two sentences, contains Daren/player-facing story context, and is bounded for console (for example 140-520 characters unless a better project-local threshold is chosen).
    - Every action result text (`SuccessText`, `PartialText`, `FailText`) is non-empty, more than a terse label, player-facing, and bounded for console.
    - Offer/intro copy does not contain raw default-UI technical terms such as `GM`, `DTO`, `API`, `debug`, `Spec Kit`, `manual-grade`, `endpoint`, or `client-owned`.
    - Beat order and QTE types still match the #956 spine.
  - If implementation touches browser/frontend files unexpectedly, add the relevant frontend/source guard and run `npm run verify --prefix BookOfEternityClient.WebFrontend`.

### Spec Kit

- **Create/Modify**: `specs/957-daren-literary-prose/spec.md`, `plan.md`, `tasks.md`, `contracts/daren-literary-prose.md`, `checklists/requirements.md`.
  - Codex may update `tasks.md` with RED/GREEN and verification evidence for implementation tasks.
  - Hermes owns independent review, PR, merge, issue closure, evidence comment, and cleanup tasks.

## Implementation Strategy

Use TDD:

1. Write failing Daren route prose tests first in `DarenQteShowcaseTests.cs`.
2. Run a focused RED filter and verify failures are caused by existing terse/bare Daren copy.
3. Update shared C# Daren route text minimally to satisfy the prose guards while preserving mechanics.
4. Rerun the focused tests to GREEN.
5. Run the affected Daren/QTE/docs/browser contract slice to confirm no route/validation/browser contract regression.
6. Build client and test project.
7. Run Spec Kit prerequisite helper and `git diff --check`.
8. Update `tasks.md` with exact evidence for implementation tasks only.

## Verification Plan

Hermes pre-implementation baseline:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" \
  --logger "console;verbosity=minimal"
```

Codex should run after implementation:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "DarenQteShowcaseTests" \
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

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files change or a browser contract test indicates a frontend display issue.

Hermes post-Codex verification should include a focused rerun, `git diff --check`, added-line static scan outside specs/tests/docs as appropriate, independent review, PR/merge readback, and post-merge focused gate on `main`.

## Risk and Mitigation

- **Risk**: Tests become subjective style checks.
  **Mitigation**: Guard objective properties: presence, length bounds, sentence count, forbidden technical terms, shared route/spine alignment.

- **Risk**: Prose edits accidentally change QTE execution behavior.
  **Mitigation**: Keep code changes to route text fields and rerun Daren/QTE validation/browser contract tests.

- **Risk**: Browser and console diverge.
  **Mitigation**: Author prose in shared C# route data only; avoid React/static duplicate copy.

- **Risk**: #957 grows into #958-#961.
  **Mitigation**: Limit this slice to opening and transition prose. Dialogue choices, branch variants, endings/rewards, and broad quality gates remain follow-up issues.

- **Risk**: Player copy leaks technical boundary language.
  **Mitigation**: Source guards reject debug/API/DTO/Spec Kit/manual-grade/client-owned/GM wording in default Daren offer and chapter/action prose.

## Spec Kit Applicability

Applicable. #957 is a medium player-facing QTE/story UX task, changes shared console/browser route presentation, depends on the #956 durable narrative spine, and needs handoff evidence under parent #955. The active feature directory is `specs/957-daren-literary-prose/`.
