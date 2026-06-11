# Implementation Plan: Daren Branch Consequences

**Branch**: `work/959-daren-branch-consequences`
**Spec**: `specs/959-daren-branch-consequences/spec.md`
**Source Issues**: [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), prerequisite [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), prerequisite [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)
**Date**: 2026-06-11

## Summary

Deepen the existing Daren QTE route so choices and QTE grades produce branch-specific consequence prose and later echoes. Keep consequences inside the existing shared route/action/result/score data consumed by console and browser; do not add a campaign-state branch engine or change reward/profile semantics.

## Technical Context

- Project: .NET 8 C# client; shared Daren QTE route authority lives in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Current Daren route starts from #919, uses #956 scene-map handoff, #957 shared literary prose, and #958 NPC/dialogue/social-choice additions.
- Planning authority: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`, especially beat consequence hooks, carry-forward notes, cast slots, and future issue links.
- Existing tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, `QteSceneServiceTests.cs`, `ValidationServiceQteTests.cs`, browser QTE/API contract tests, prompt/example documentation coverage tests.
- Current Daren original heist beats from #957 remain an ordered subsequence even after #958 inserted dialogue beats: `approach_manor`, `gadget_infiltration`, `stealth_crossing`, `lock_pick`, `rune_memory`, `physical_pressure`, `timed_rhythm`, `route_decision`, `staff_theft`, `pursuit`, `chase_chain`, `hideout_return`.
- Current #958 dialogue/social-choice beats should be preserved and are a likely source for decision-dependent consequence echoes.

## Architecture and Files

### Product Route Content

- **Modify**: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Responsibility: shared Daren route/offer/chapter/action text, result branches, score deltas, routing, and QTE action definitions consumed by console and browser.
  - Expected changes:
    - Add or refine success/partial/fail result prose for key QTE actions so each branch names a specific consequence: clean progress, delay/noise, suspicion/evidence, ward pressure, pursuit control, hideout safety, or improvised detour.
    - Deepen existing #958 dialogue/planning actions so their choices echo later through route copy or result text.
    - Keep poor non-terminal outcomes playable where the existing route continues; use branch text and score/risk deltas rather than new state.
    - Use existing `QteScoreDelta`, `ScoreDeltas`, `QteRouting`, chapter narrative, action result text, and supported check config fields.
  - Do not add a new branch-memory service, campaign-state file, endpoint, React-only copy, or QTE check type.

### Narrative Spine / Handoff Artifact

- **Modify**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`
  - Responsibility: durable #956 scene map and future-task handoff context.
  - Expected changes:
    - Add #959 to `sourceIssues` or equivalent provenance.
    - Record branch-consequence hooks and carry-forward references for key beats if the current JSON schema accepts additive fields safely.
    - Preserve #956/#957/#958 truths and #960/#961 handoff links.
    - Keep the route/spine beat sequence aligned with actual route data.

### Tests

- **Modify**: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add focused RED tests before production changes.
  - Suggested objective guards:
    - Key action branch text coverage: selected stealth/security/dialogue/pursuit actions have distinct success/partial/fail consequence prose beyond generic pass/fail.
    - Carry-forward coverage: later route prose/result text references at least several earlier decisions/results, including at least one #958 dialogue/planning decision.
    - Playable bad-outcome coverage: non-terminal poor outcomes route or narratively continue with increased pressure rather than collapsing into generic failure.
    - Standard-contract coverage: branch consequences live in shared route/action/result/score/spine data and no new consequence runtime/state/endpoint/frontend-only fork/check type appears.
    - Boundary coverage: route id/reward profile/New Game grant semantics remain unchanged, and original heist beats stay ordered.
  - Keep #956/#957/#958 tests meaningful; update only when route/spine truth changes.

### Browser/Frontend

- **Normally unchanged**: `BookOfEternityClient.WebFrontend/`
  - Browser should receive the same route/actions/result/config content through existing QTE DTOs.
  - Only change frontend code if a failing shared-route/browser contract test proves the existing browser renderer cannot present already-supported consequence content.
  - If frontend changes happen, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and document why the change was required.

### GM-Facing Docs / Examples

- **Normally unchanged**: `Rules/`, `Examples/`, `CLI_API_Specification.md`
  - #959 changes client-owned Daren showcase content, not the GM-authored campaign QTE contract.
  - If Codex changes QTE contract fields, validation rules, or GM-authored example requirements, it must update GM-facing docs/examples/source guards in the same change and report the scope expansion.

### Spec Kit

- **Create/Modify**: `specs/959-daren-branch-consequences/spec.md`, `plan.md`, `tasks.md`, `contracts/daren-branch-consequences.md`, `checklists/requirements.md`.
  - Codex may update `tasks.md` with RED/GREEN and verification evidence for implementation tasks.
  - Hermes owns independent review, PR, merge, issue closure, evidence comment, and cleanup tasks.

## Implementation Strategy

Use TDD:

1. Write failing Daren branch-consequence tests first in `DarenQteShowcaseTests.cs`.
2. Run a focused RED filter and verify failures are caused by missing branch/carry-forward consequence evidence, not typos or harness issues.
3. Implement the minimum shared-route content and existing-QTE result/score data needed to satisfy #959.
4. Update `DarenQteNarrativeSpine.json` only to keep route/consequence/handoff truth aligned.
5. Rerun focused Daren tests to GREEN.
6. Run the affected Daren/QTE/docs/browser contract slice.
7. Build client and test project.
8. Run Spec Kit prerequisite helper and `git diff --check`.
9. Update `tasks.md` with exact evidence for implementation tasks only.

## Verification Plan

Hermes pre-implementation baseline recorded for this branch:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" \
  --logger "console;verbosity=minimal"
```

Observed baseline before #959 code changes: `299/299 passed`.

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

- **Risk**: #959 turns into a new branch-memory or campaign-state engine.
  **Mitigation**: Keep branch consequences in existing route/action/result/score text and spine handoff notes. Add a guard against new consequence services/state/endpoints if practical.

- **Risk**: Consequence text breaks console/browser parity.
  **Mitigation**: Author data in shared C# route only. Run browser/API contract tests and avoid React copy forks.

- **Risk**: Bad outcomes become generic immediate failures.
  **Mitigation**: Add tests for non-terminal failure branches that keep route play moving with specific pressure/detour text where the current route continues.

- **Risk**: #959 broadens into endings/rewards or broad quality gates.
  **Mitigation**: Limit this slice to in-route consequences and carry-forward echoes. Ending/epilogue/reward presentation remains #960; content-quality metrics/gates remain #961.

- **Risk**: Tests judge subjective prose quality.
  **Mitigation**: Guard objective properties: branch distinction, consequence terms, carry-forward references, score/risk deltas, route/spine alignment, and absence of technical/default-UI leaks.

## Spec Kit Applicability

Applicable. #959 is player-facing QTE/story UX work across shared console/browser route presentation, depends on #956/#957/#958 durable Daren artifacts, deepens choice/QTE outcome semantics, and requires handoff evidence under parent #955. The active feature directory is `specs/959-daren-branch-consequences/`.
