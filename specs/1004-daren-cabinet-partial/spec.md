# Feature Specification: Daren Cabinet Lock Partial Literary Aftermath

**Feature Branch**: `work/1004-daren-cabinet-partial`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), completed sibling [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), remaining sibling [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), completed downstream result trio [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #1004 - rewrite result surface `lock_pick` / `lock_pick_action` / `partial` ("Замок кабинета" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #974 already rewrote the `lock_pick` scene opening as a full literary page. This issue only rewrites the partial post-action outcome.
- **Completed sibling context**: #1003 rewrote the clean `success` outcome for this same `lock_pick_action`; #1004 must preserve it unchanged.
- **Remaining sibling context**: #1005 owns the `fail` outcome for this same `lock_pick_action`; #1004 must preserve the current fail text unchanged.
- **Completed downstream context**: #1006/#1007/#1008 completed the next `rune_memory_action` result trio and must remain unchanged.
- **Spec Kit justification**: #1004 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `lock_pick_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting the `lock_pick` scene opening, success/fail outcomes (#1003/#1005), rune-memory outcomes (#1006/#1007/#1008), other Daren scenes/results (#988-#1002 and #1009-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Замок сдаётся, но отмычка царапает накладку; Дарен уносит этот след вместе с тревогой.

## User Scenarios & Testing

### User Story 1 - Partial Cabinet-Lock Outcome Reads As Consequential Mixed Aftermath (Priority: P1)

As a player resolving the "Замок кабинета" LockPinSet check with a partial result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren opening the cabinet at a cost, so the route communicates success-with-consequence instead of a short score notification.

**Why this priority**: This is the only user-visible value of #1004 and continues the `lock_pick_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `lock_pick_action.Routing.Partial` / partial result text from shared route data, asserting its mixed-outcome literary qualities, and confirming unchanged action contract plus success/fail sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `lock_pick_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial is the mixed cabinet-lock outcome, **When** the insert is read, **Then** Daren opens or enters through the cabinet door but a visible scratch/trace, delay, doubt, or later evidence/alarm consequence remains concrete without naming mechanics, score, or debug framing.
3. **Given** the existing lock-pick action contract, **When** route data is inspected, **Then** beat id, title, action id, `LockPinSet` check, Dexterity characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to the cabinet lock, pins, pick tension, bronze plate scratch, dust, silence, and the cost of opening the cabinet imperfectly.
- The result must not duplicate the scene opening, #1003 success prose, #1005 fail prose, or later rune-memory prose; it should begin after the partial pin-set and carry the route toward `rune_memory` / "Руны на дверце" with a visible but not catastrophic consequence.
- The result must not rewrite success/fail outcomes; #1003 is already complete and #1005 remains a separate sibling task.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `lock_pick_action` partial text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through the imperfect pin alignment, controlled recovery, cabinet opening/entry, and immediate consequence.
- **FR-003**: The insert MUST reflect the `partial` grade: Daren succeeds in opening or entering the cabinet, but a trace, scratch, delay, doubt, wound, or later evidence/alarm pressure remains visible.
- **FR-004**: The insert MUST include concrete sensory details around pins, pick/tension tool, keyhole/bronze plate, scratch/dust, breath, hands/fingers, silence or small sound, and cabinet door movement.
- **FR-005**: The insert MUST bridge naturally into the next `rune_memory` / "Руны на дверце" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for pins/pick craft, Daren body control, partial trace/cost/evidence, cabinet opening, and next-rune/futlar continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `LockPinSet` type, Dexterity characteristic, difficulty, config pin/window/timer/durability semantics, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren cabinet-lock action**: `lock_pick_action` inside the shared Daren route chapter `lock_pick` / "Замок кабинета".
- **Partial result surface**: The mixed outcome string shown after the LockPinSet action resolves as a partial success.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `lock_pick` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The partial aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/fail outcome, frontend, downstream rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `lock_pick` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, downstream rune-memory drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1004-daren-cabinet-partial` was created from `origin/main` at `d64bfe6` after #1003 / PR #1030 had landed.
- Focused baseline before #1004 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 61 / failed 0 / skipped 0 / total 61.
- Affected baseline before #1004 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 330 / failed 0 / skipped 0 / total 330.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR=E:\\Games\\worktrees\\boe-1004-daren-cabinet-partial\\specs\\1004-daren-cabinet-partial` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1004 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
