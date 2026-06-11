# Implementation Plan: Daren Scene 02 Full Literary Page

**Branch**: `work/970-daren-mira-whisper`
**Spec**: `specs/970-daren-mira-whisper/spec.md`
**Source Issues**: [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), completed prior scene [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969)
**Date**: 2026-06-12

## Summary

Rewrite only Daren scene `informant_parley` / “Шёпот Миры” into a substantial shared C# literary page with Mira interaction/dialogue, while preserving all QTE route mechanics and leaving the remaining per-scene children under #955 for later tasks.

## Technical Context

- Project: .NET 8 C# client with shared QTE route data in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Existing Daren planning artifact: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`.
- Existing focused tests and source guards: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`.
- Current #970 text is a short briefing about Mira waiting under the rear-road awning.
- The issue body includes Stanislav's target Mira example. Match its form/quality bar (scene action, subtext, dialogue), but do not copy it verbatim.
- Console and browser parity is preserved by keeping prose in shared route data.

## Architecture and Files

### Product Route Content

- **Modify**: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Responsibility: shared Daren route/offer/chapter/action text consumed by both console and browser.
  - Expected change: replace only the `DarenShowcaseBeat` `PlayerText` for beat id `informant_parley` with substantial Russian scene prose.
  - Do not change route ids, beat order, titles, QTE check types/configs, choice ids, routing, score deltas, ending tiers, reward-profile writes, New Game grants, endpoint/runtime behavior, or browser/frontend code.

### Tests

- **Modify**: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add a focused #970 guard for `informant_parley` that fails on the current one/two-sentence synopsis.
  - Suggested objective checks:
    - `informant_parley` narrative length is large enough for a scene page and has enough sentences/dialogue lines.
    - It mentions Daren and Mira repeatedly and includes required motifs: awning/rear road, night/wet/ribbon detail, old contact/subtext, body language/observation, guard/source exposure/pursuit stakes, and password/information pressure.
    - It preserves the existing action metadata and precision-choice ids/outcome labels.
    - It contains no player-facing technical terms from existing forbidden lists.
  - Keep broader existing Daren prose guards passing.

### Spec Kit

- **Create/Modify**: `specs/970-daren-mira-whisper/spec.md`, `plan.md`, `tasks.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`.
- Codex may update task checkboxes only when it has implementation/verification evidence.
- Hermes owns independent review, PR, merge, issue comment, closure, and cleanup tasks.

## Implementation Strategy

Use strict TDD:

1. Write the focused failing #970 test first in `DarenQteShowcaseTests.cs`.
2. Run `DarenQteShowcaseTests` and verify the new test fails against current `main` because `informant_parley` is still synopsis-length.
3. Replace only the shared C# `informant_parley` prose with a substantial Mira/Daren scene page that satisfies the test and issue quality bar.
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

Hermes post-Codex verification should include at least a focused rerun, `git diff --check`, static added-line scan excluding specs/tests/docs, independent review, PR/merge readback, and post-merge focused gate on `main`.

## Risk and Mitigation

- **Risk**: Codex copies the user example too literally.
  **Mitigation**: The prompt and spec require matching form/quality, not verbatim text; review checks for undue copy/paste.

- **Risk**: Automated tests overfit subjective literary taste.
  **Mitigation**: Use tests for objective proxies only; require independent review for the human quality bar.

- **Risk**: The scene rewrite changes QTE execution behavior.
  **Mitigation**: Keep code edits to the `informant_parley` narrative string and tests; run Daren/QTE validation/browser contract slice.

- **Risk**: Parent #955 is accidentally closed early.
  **Mitigation**: PR/issue evidence must state that #971-#983 remain open and #955 is not closed by #970 alone.

## Spec Kit Applicability

Applicable. #970 is player-facing story UX, changes shared console/browser presentation, and is one child of a larger tracked Daren interactive-book umbrella. Active feature directory: `specs/970-daren-mira-whisper/`.
