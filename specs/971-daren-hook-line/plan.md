# Implementation Plan: Daren Scene 03 Full Literary Page

**Branch**: `work/971-daren-hook-line`
**Spec**: `specs/971-daren-hook-line/spec.md`
**Source Issues**: [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), completed scenes [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970)
**Date**: 2026-06-12

## Summary

Rewrite only Daren scene `gadget_infiltration` / “Крюк и леска” into a substantial shared C# literary page centered on the folding hook, line, tower wall, balcony, and courtyard sound/light risk, while preserving all QTE route mechanics and leaving remaining per-scene children under #955 for later tasks.

## Technical Context

- Project: .NET 8 C# client with shared QTE route data in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Existing Daren planning artifact: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`.
- Existing focused tests/source guards: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`.
- Current #971 text is a short briefing about Daren at a tower wall launching a folding hook.
- Console/browser parity is preserved by keeping prose in shared route data.

## Architecture and Files

### Product Route Content

- **Modify**: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Replace only the `DarenShowcaseBeat` `PlayerText` for beat id `gadget_infiltration` with substantial Russian scene prose.
  - Do not change route ids, beat order, titles, QTE check types/configs, routing, score deltas, ending tiers, reward-profile writes, New Game grants, endpoint/runtime behavior, or browser/frontend code.

### Tests

- **Modify**: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add a focused #971 guard for `gadget_infiltration` that fails on the current one/two-sentence synopsis.
  - Suggested objective checks:
    - length/sentence count for a scene page;
    - Daren as protagonist;
    - required motifs: tower/cold stone, balcony/courtyard, folding hook/line/metal, hand/body/ascent movement, guard/sound/light stakes, first shout or clatter risk, lead-in to launch/anchor/climb;
    - action metadata preservation for `gadget_infiltration_action`, `ChargeRelease`, Dexterity, difficulty/config/routing;
    - forbidden technical terms.

### Spec Kit

- **Create/Modify**: `specs/971-daren-hook-line/spec.md`, `plan.md`, `tasks.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`.
- Codex may update task checkboxes only when it has implementation/verification evidence.
- Hermes owns independent review, PR, merge, issue comment, closure, and cleanup tasks.

## Implementation Strategy

Use strict TDD:

1. Write the focused failing #971 test first in `DarenQteShowcaseTests.cs`.
2. Run `DarenQteShowcaseTests` and verify the new test fails against current `main` because `gadget_infiltration` is still synopsis-length.
3. Replace only the shared C# `gadget_infiltration` prose with a substantial hook-and-line infiltration page.
4. Rerun focused `DarenQteShowcaseTests` to GREEN.
5. Rerun the affected Daren/QTE/docs/browser slice.
6. Run build/diff/static hygiene as time permits.
7. Leave parent #955 open and do not touch other scene tasks unless tests require neutral shared-helper adjustments.

## Verification Plan

Hermes launch baseline before Codex:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj   -p:IsTestProject=true   --filter "FullyQualifiedName~DarenQteShowcaseTests"   --logger "console;verbosity=minimal"
```

Codex should run after implementation:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj   -p:IsTestProject=true   --filter "FullyQualifiedName~DarenQteShowcaseTests"   --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj   -p:IsTestProject=true   --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests"   --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files change or a browser display bug is found.

## Spec Kit Applicability

Applicable. #971 is player-facing story UX over shared console/browser QTE route data and one child of the Daren interactive-book umbrella. Active feature directory: `specs/971-daren-hook-line/`.
