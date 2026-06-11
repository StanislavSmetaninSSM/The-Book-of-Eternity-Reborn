# Implementation Plan: Daren NPC Dialogue Cast

**Branch**: `work/958-daren-dialogue-cast`
**Spec**: `specs/958-daren-dialogue-cast/spec.md`
**Source Issues**: [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), prerequisite [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)
**Date**: 2026-06-11

## Summary

Add people-driven interaction to the Daren QTE heist by defining a small NPC cast and adding multiple dialogue/social-choice moments inside the existing shared QTE route. Preserve the existing QTE engine, console/browser shared route contract, reward profile behavior, and ordinary campaign boundaries.

## Technical Context

- Project: .NET 8 C# client; shared Daren QTE route authority lives in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Current Daren route starts from #919, uses #956 scene-map handoff, and received #957 shared prose in `QteSceneService.Daren.cs`.
- Planning authority: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`, especially `castSlots` and `futureIssueLinks` for #958.
- Existing tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, `QteSceneServiceTests.cs`, `ValidationServiceQteTests.cs`, browser QTE/API contract tests, prompt/example documentation coverage tests.
- Current Daren heist beats from #957: `approach_manor`, `gadget_infiltration`, `stealth_crossing`, `lock_pick`, `rune_memory`, `physical_pressure`, `timed_rhythm`, `route_decision`, `staff_theft`, `pursuit`, `chase_chain`, `hideout_return`.
- #958 may add dialogue/social-choice chapters or update choice-like actions, but must keep the original heist beats present in their original relative order and must not add a new dialogue runtime.

## Architecture and Files

### Product Route Content

- **Modify**: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Responsibility: shared Daren route/offer/chapter/action text and QTE action definitions consumed by console and browser.
  - Expected changes:
    - Define a small named/personified cast in code-local data or helper methods, keeping the public route model unchanged unless a tiny test-backed extension is required.
    - Add at least three dialogue/social-choice moments through existing QTE route chapters/actions and existing check types.
    - Prefer `PrecisionChoice` for player-visible answer options when choices must be interactive in console/browser.
    - Keep `BranchChoice` only when the current route intentionally resolves a predetermined branch or when Codex finds an existing supported player-facing pattern.
    - Author success/partial/fail result text as NPC responses.
    - Apply score/risk deltas through existing `QteScoreDelta`/`ScoreDeltas` data.
  - Do not add a separate dialogue service, endpoint, state file, React-only copy, or new QTE check type.

### Narrative Spine / Handoff Artifact

- **Modify**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`
  - Responsibility: durable #956 scene map and handoff context.
  - Expected changes:
    - Add #958 to `sourceIssues`.
    - Expand `castSlots` with concrete names/personas and dialogue/response responsibilities if the current schema can accept additive fields safely.
    - If route beats are inserted or QTE types change, update `beats` and tests so route/spine alignment remains truthful.
    - Preserve #959-#961 handoff links and #919/#956/#957 context.

### Tests

- **Modify**: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add focused RED tests before production changes.
  - Suggested objective guards:
    - Cast coverage: four required cast slots have concrete display names/personas and are visible in route copy or choice text.
    - Dialogue coverage: at least three dialogue/social-choice actions or chapters exist and use supported QTE check types.
    - Choice-option coverage: interactive dialogue choices expose answer labels/descriptions/hints through `PrecisionChoice` config where player selection is expected.
    - Response coverage: dialogue/social-choice result text differs for success/partial/fail and names/reflects NPC reactions.
    - Consequence coverage: at least one dialogue/social-choice action changes existing score/risk metrics; later route prose/result text references an earlier NPC/social interaction.
    - Boundary coverage: no new dialogue runtime, no React-only story fork, route id/reward semantics remain unchanged, and original heist beats remain ordered.
  - Keep #956/#957 tests meaningful; update only when the route/spine truth changes.

### Browser/Frontend

- **Normally unchanged**: `BookOfEternityClient.WebFrontend/`
  - Browser should receive the same route/actions/config through existing QTE DTOs.
  - Only change frontend code if a failing shared-route/browser contract test proves the existing browser renderer cannot present already-supported `PrecisionChoice` data.
  - If frontend changes happen, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and document why the change was required.

### Spec Kit

- **Create/Modify**: `specs/958-daren-dialogue-cast/spec.md`, `plan.md`, `tasks.md`, `contracts/daren-dialogue-cast.md`, `checklists/requirements.md`.
  - Codex may update `tasks.md` with RED/GREEN and verification evidence for implementation tasks.
  - Hermes owns independent review, PR, merge, issue closure, evidence comment, and cleanup tasks.

## Implementation Strategy

Use TDD:

1. Write failing Daren route/cast/dialogue tests first in `DarenQteShowcaseTests.cs`.
2. Run a focused RED filter and verify failures are caused by missing cast/dialogue/choice evidence, not typos or harness issues.
3. Implement the minimum shared-route content and existing-QTE choice data needed to satisfy #958.
4. Update `DarenQteNarrativeSpine.json` only to keep route/cast/handoff truth aligned.
5. Rerun focused Daren tests to GREEN.
6. Run the affected Daren/QTE/docs/browser contract slice.
7. Build client and test project.
8. Run Spec Kit prerequisite helper and `git diff --check`.
9. Update `tasks.md` with exact evidence for implementation tasks only.

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

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files change or a browser contract/display bug is found.

Hermes post-Codex verification should include a focused rerun, `git diff --check`, added-line static scan, independent review, PR/merge readback, and post-merge focused gate on `main`.

## Risk and Mitigation

- **Risk**: #958 turns into a separate dialogue engine.
  **Mitigation**: Keep all dialogue inside existing QTE route chapters/actions/check config/result text/score deltas. Add a guard against new dialogue service/state/endpoint if practical.

- **Risk**: Dialogue changes break console/browser parity.
  **Mitigation**: Author data in shared C# route only. Prefer existing `PrecisionChoice` rendering. Run browser/API contract tests.

- **Risk**: Route/spine alignment tests fail after adding dialogue beats.
  **Mitigation**: Update `DarenQteNarrativeSpine.json` and tests to treat original #957 heist beats as an ordered subsequence while including inserted dialogue beats truthfully.

- **Risk**: #958 broadens into #959/#960.
  **Mitigation**: Limit consequences to immediate responses, existing score/risk deltas, and modest later prose references. Expanded branch consequence variants and ending/reward presentation remain separate issues.

- **Risk**: Subjective writing tests become brittle.
  **Mitigation**: Guard objective properties: cast slot presence, names/personas, choice labels/descriptions, outcome response diversity, score deltas, route/spine alignment, and absence of technical/default-UI leaks.

## Spec Kit Applicability

Applicable. #958 is player-facing QTE/story UX work across shared console/browser route presentation, depends on #956/#957 durable Daren artifacts, changes route choice/content semantics, and requires handoff evidence under parent #955. The active feature directory is `specs/958-daren-dialogue-cast/`.
