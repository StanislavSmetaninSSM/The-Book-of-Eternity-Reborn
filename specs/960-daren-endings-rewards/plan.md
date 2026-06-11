# Implementation Plan: Daren Endings and Reward Presentation

**Branch**: `work/960-daren-endings-rewards`
**Spec**: `specs/960-daren-endings-rewards/spec.md`
**Source Issues**: [#960](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/960), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisites [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)
**Date**: 2026-06-11

## Summary

Expand the Daren standalone QTE heist completion into authored Russian dark-fantasy ending pages and in-world reward presentation centered on Daren as the protagonist. Keep reward thresholds, profile writes, and New Game grants from #919 unchanged while adding shared ending epilogue data that console and browser consume through the same C# contract.

Revision note, 2026-06-11: user correction rejects short, dry Daren epilogues even when tests pass. #960 must now guard against terse summaries with objective structural proxies and fix the browser no-downgrade wording blocker where a lower replay tier was labeled as the future New Game reward despite a higher saved best tier.

## Technical Context

- Project: .NET 8 C# client; shared Daren QTE route authority lives in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Ending threshold/profile authority lives in `BookOfEternityClient/Services/DarenQteRewardProfileService.cs`.
- Browser Daren state is serialized by `BookOfEternityClient/WebUi/QteWebInteractionService.cs` through `DarenShowcaseEndingDto`.
- Planning authority: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`, especially ending/reward handoff notes and future #961 links.
- Existing tests: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, `BrowserApiContractTests.cs`, `QteSceneServiceTests.cs`, `ValidationServiceQteTests.cs`, prompt/example documentation coverage tests, and browser frontend workspace tests.
- Current required tiers from #919: `shadow_on_the_run` (+1), `broken_trail` (+2), `clean_heist` (+4), `perfect_shadow` (+6), plus no-reward `no_reward_failure`.

## Architecture and Files

### Ending/Reward Domain Data

- **Modify**: `BookOfEternityClient/Services/DarenQteRewardProfileService.cs`
  - Responsibility: ending threshold resolution, permanent reward profile writes, and New Game Ink Feather grant messages.
  - Expected changes:
    - Add or revise shared ending epilogue/reward-presentation fields on `DarenEndingTier` and `DarenEndingResult` if needed.
    - Author substantial distinct epilogue pages for `no_reward_failure`, `shadow_on_the_run`, `broken_trail`, `clean_heist`, and `perfect_shadow`.
    - Make reward messages explain the achievement and future New Game Ink Feather amount in-world while retaining exact tier ids, thresholds, bonuses, profile path, and one-time New Game marker semantics.
    - Avoid raw receipt wording such as `+N` and "future bonus" labels in ending/reward copy.
  - Do not add a new profile file, new threshold table source, reward stacking rule, or campaign-state side effect.

### Shared Daren Completion Flow

- **Modify**: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Responsibility: Daren showcase attempt resolution, completion summary, console rendering, and shared ending object captured in attempt state.
  - Expected changes:
    - Include the new epilogue/reward copy in `QteSceneCompletion`, `DarenShowcaseEnding`, `attempt.Feedback`, and console completion rendering as appropriate.
    - Avoid duplicating epilogue/reward text in the console panel when `completion.Response.Response` already includes those shared fields.
    - Preserve `ResolveDarenNormalizedScore`, `HadUnsafeRouteFailure`, and existing routing/score model behavior.
    - Keep result text from #959 branch consequences intact; endings should complement them rather than replace route-specific consequence text.

### Browser State Contract

- **Modify if needed**: `BookOfEternityClient/WebUi/QteWebInteractionService.cs`
  - Responsibility: serialize Daren ending data to browser state.
  - Expected changes:
    - Expose the same shared epilogue/reward fields that console completion uses through `DarenShowcaseEndingDto` when completion exists.
    - Avoid React-only ending copy or browser-local reward mapping.

- **Modify if needed**: `BookOfEternityClient.WebFrontend/src/`
  - Change React if a failing contract/player-facing test proves the current browser renderer cannot display the shared ending epilogue/reward fields already provided by C# or mislabels saved-best reward state.
  - Browser completion must not present `state.ending.inkFeatherBonus` as the future New Game grant when `state.bestReward` carries a higher saved tier.
  - If changed, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and explain the need in `tasks.md` evidence.

### Narrative Spine / Handoff Artifact

- **Modify**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`
  - Responsibility: durable #956 scene map and future-task handoff context.
  - Expected changes:
    - Add #960 source/provenance or ending/reward handoff notes if the schema already uses such arrays/notes.
    - Preserve #956 beat order/pacing, #957 prose shape, #958 cast/dialogue, #959 consequence hooks, and #961 future quality-gate handoff.

### Tests

- **Modify**: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add focused RED tests before production changes.
  - Suggested guards:
    - Every Daren outcome has non-empty distinct epilogue copy.
    - Every Daren outcome has substantial multi-sentence, Daren-centered epilogue copy.
    - Epilogue/reward text contains tier-appropriate consequence terms and not only mechanical `+N` receipt language.
    - Reward explanations avoid raw receipt wording and use in-world lore for the future Ink Feather amount.
    - Existing thresholds/bonuses/profile write/no-downgrade/New Game idempotency remain unchanged.
    - Completion state and `DarenShowcaseEnding` carry shared epilogue/reward fields for console/browser consumption.
    - Browser shared-state rendering preserves no-downgrade clarity after a higher saved tier and a lower replay tier.
    - No new reward profile files, ending-state runtime, QTE check types, or frontend-only ending mapping are introduced.

- **Modify if needed**: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
    - Add or update browser state tests when `DarenShowcaseEndingDto` gains fields so browser contract coverage proves shared ending data is available.
    - Add a regression where a saved higher tier remains the effective future reward after a lower replay.

### GM-Facing Docs / Examples

- **Normally unchanged**: `Rules/`, `Examples/`, `CLI_API_Specification.md`
  - #960 changes client-owned Daren showcase content and presentation, not the GM-authored campaign QTE offer contract.
  - If Codex changes QTE contract fields that a GM must author, validation rules, or example requirements, it must update GM-facing docs/examples/source guards in the same PR and report the scope expansion.

### Spec Kit

- **Create/Modify**: `specs/960-daren-endings-rewards/spec.md`, `plan.md`, `tasks.md`, `contracts/daren-endings-rewards.md`, `checklists/requirements.md`.
  - Codex may update `tasks.md` with RED/GREEN and verification evidence for implementation tasks.
  - Hermes owns independent review, PR, merge, issue closure, evidence comment, and cleanup tasks.

## Implementation Strategy

Use TDD:

1. Write failing ending/reward tests first in `DarenQteShowcaseTests.cs` and, if needed, `BrowserApiContractTests.cs`.
2. Run a focused RED filter and verify failures are caused by missing epilogue/reward presentation, not typos or harness errors.
3. Implement the minimum shared ending data and completion rendering needed to satisfy #960.
4. Update `DarenQteNarrativeSpine.json` only to keep ending/reward handoff truth aligned.
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

Observed baseline before #960 code changes: `304/304 passed`.

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

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files change or a browser display bug is found.

Hermes post-Codex verification should include a focused rerun, `git diff --check`, added-line static scan, independent review, PR/merge readback, and post-merge focused gate on `main`.

## Risk and Mitigation

- **Risk**: #960 changes reward mechanics while improving presentation.
  **Mitigation**: Tests must keep exact tier ids, thresholds, bonuses, profile path, no-downgrade behavior, and New Game idempotency unchanged.

- **Risk**: Ending text becomes browser-only or console-only.
  **Mitigation**: Author ending data in shared C# records and verify browser DTO/console completion use the same source.

- **Risk**: Ending copy claims route-specific memories that the runtime does not track.
  **Mitigation**: Use tier-level and consequence-category language from score/grade outcomes unless existing route/result text already carries specific choices. Do not add branch-memory state.

- **Risk**: #960 broadens into #961 broad content-quality infrastructure.
  **Mitigation**: Limit tests to ending/reward presentation and source-boundary guards; broad scenario-wide content gates remain #961.

- **Risk**: Tests judge subjective prose quality.
  **Mitigation**: Guard objective properties: non-empty/distinct epilogues, tier-specific consequence terms, reward explanation, shared DTO fields, and unchanged mechanics.

## Spec Kit Applicability

Applicable. #960 is player-facing QTE/story UX work across shared console/browser ending presentation, depends on #956/#957/#958/#959 durable Daren artifacts, touches persistent reward explanation while preserving #919 mechanics, and requires handoff evidence under parent #955. The active feature directory is `specs/960-daren-endings-rewards/`.
