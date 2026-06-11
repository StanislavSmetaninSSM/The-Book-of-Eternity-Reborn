# Implementation Plan: Daren Narrative Spine and Scene Map

**Branch**: `work/956-daren-narrative-spine`  
**Spec**: `specs/956-daren-narrative-spine/spec.md`  
**Source Issues**: [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), related [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)  
**Date**: 2026-06-11

## Summary

Create a durable Daren QTE narrative spine/scene-map authority and tests that keep it synchronized with the existing shared Daren QTE route. This slice defines story backbone, pacing, dramatic roles, branch/consequence hooks, and future implementation handoff; it does not yet write all literary prose, dialogue, endings, or content-quality gates from #957-#961.

## Technical Context

- Project: .NET 8 C# client with shared console/browser QTE route authority in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Existing Daren tests live in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`.
- Existing Daren browser route consumes `QteWebInteractionService` and React `DarenShowcaseView.tsx` over shared C# DTOs.
- Spec Kit governance exists in `.specify/memory/constitution.md`.
- Current Daren route beat ids: `approach_manor`, `gadget_infiltration`, `stealth_crossing`, `lock_pick`, `rune_memory`, `physical_pressure`, `timed_rhythm`, `route_decision`, `staff_theft`, `pursuit`, `chase_chain`, `hideout_return`.
- Required QTE types already in route: `BranchChoice`, `ChargeRelease`, `StealthNoise`, `LockPinSet`, `PatternMemory`, `MashInput`, `RhythmPulse`, `PrecisionChoice`, `BalanceMeter`, `TimingBar`, `PromptChain`.

## Architecture and Files

### New/Modified Product Artifact

- **Likely create**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`  
  Responsibility: durable machine-readable narrative scene-map authority for #956. JSON keeps structure easy to validate from tests and easy for future agents to read.

  Expected shape:
  - `schemaVersion`
  - `routeId`
  - `sourceIssues`
  - `targetPlaytimeMinutes`
  - `arcStages`
  - `castSlots`
  - `beats[]` with `beatId`, `phase`, `title`, `dramaticPurpose`, `playerGoal`, `qteType`, `sceneFraming`, `branchPoints`, `consequenceHooks`, `carryForward`, `futureIssueLinks`, and `pacingMinutes`.
  - `handoffNotes` for #957-#961.

- **Maybe modify**: `BookOfEternityClient/Services/QteSceneService.Daren.cs`  
  Only if a tiny source constant/path helper is useful. Do not move route authority or change reward logic unless required by tests.

### Tests

- **Modify**: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`  
  Add focused tests that load the scene-map JSON and compare it to `QteSceneService.GetDarenShowcaseRoute()`.

  Required assertions:
  - route id and source issue links are present;
  - each current Daren route beat appears exactly once in route order;
  - each scene-map beat has non-empty structural fields;
  - `qteType` matches the authoritative route action check type;
  - issue-required arc stages are represented;
  - every beat has branch/consequence/carry-forward notes;
  - map declares 20-30 minute target;
  - the map includes NPC/cast insertion points for future #958 work;
  - no separate runtime/system wording such as new dialogue engine or browser-only route is required.

### Spec Kit

- **Modify**: `specs/956-daren-narrative-spine/tasks.md`  
  Codex should record RED/GREEN evidence for completed implementation tasks but leave Hermes-owned review/PR/merge/issue closure tasks open.

- **Keep aligned**: `specs/956-daren-narrative-spine/spec.md`, `plan.md`, `contracts/daren-narrative-spine.md`.

## Implementation Strategy

Use TDD:

1. Write failing structure/coverage tests in `DarenQteShowcaseTests.cs` that reference the intended JSON artifact path and current route authority.
2. Run the focused Daren test filter and confirm the new tests fail because the artifact does not exist or required entries are absent.
3. Add the JSON scene-map artifact with complete entries for every existing beat.
4. Run the focused tests to green.
5. If tests reveal mismatched QTE type or missing arc coverage, fix the JSON artifact rather than weakening the test.
6. Run the affected QTE/docs/browser contract slice to ensure #919 reward/profile/shared-route behavior did not regress.
7. Update `tasks.md` with exact evidence.

## Verification Plan

Pre-implementation baseline from Hermes should run:

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

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files change.

## Risk and Mitigation

- **Risk**: JSON artifact becomes stale when route beats change.  
  **Mitigation**: tests compare artifact beat ids and QTE types to `GetDarenShowcaseRoute()`.

- **Risk**: This slice accidentally implements broad #957-#961 content.  
  **Mitigation**: tasks and tests focus on scene-map authority and structure; future issue links are handoff notes, not completed content.

- **Risk**: New documentation changes GM-authored QTE contract expectations.  
  **Mitigation**: keep Daren showcase explicitly client-owned and do not change `Rules/Block_CLI_QTE.txt`, examples, pending/control files, reward profile contract, or normal campaign QTE authoring unless a failing test shows existing docs must be clarified.

- **Risk**: Browser and console diverge.  
  **Mitigation**: scene-map is shared and tested against C# route authority; no browser-only story copy should be added in this slice.

## Spec Kit Applicability

Applicable. #956 is a medium player-facing QTE scenario planning task under #955, changes durable content expectations for future implementation work, and must preserve shared console/browser QTE route authority. This feature directory is `specs/956-daren-narrative-spine/` and should be discoverable through the repo-local Spec Kit prerequisite helper.
