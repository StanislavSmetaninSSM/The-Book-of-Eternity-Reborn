# Feature Specification: Daren Keykeeper Gallery Partial Literary Aftermath

**Feature Branch**: `work/1001-daren-keykeeper-partial`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), completed success sibling [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000), remaining fail sibling [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), completed downstream result trios [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005) and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #1001 - rewrite result surface `guard_interrogation` / `guard_interrogation_action` / `partial` ("Ключник в галерее" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #973 already rewrote the `guard_interrogation` scene opening as a full literary page. This issue only rewrites the partial post-action outcome.
- **Sibling context**: #1000 completed the clean success outcome for the same `guard_interrogation_action`; #1002 remains the separate fail outcome. #1001 must preserve both sibling strings outside the partial surface.
- **Completed downstream context**: #1003/#1004/#1005 completed the next `lock_pick_action` result trio and #1006/#1007/#1008 completed the following `rune_memory_action` result trio; all must remain unchanged.
- **Spec Kit justification**: #1001 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `guard_interrogation_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting the `guard_interrogation` scene opening, success/fail outcomes (#1000/#1002), downstream lock-pick/rune-memory outcomes (#1003-#1008), other Daren scenes/results (#988-#999 and #1009-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Лукьян пропускает Дарена с сомнением, но его взгляд цепляется за плащ и уже ищет вторую встречу.

## User Scenarios & Testing

### User Story 1 - Partial Keykeeper Outcome Reads As Mixed Social Aftermath (Priority: P1)

As a player resolving the "Ключник в галерее" PrecisionChoice with a partial result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren's plausible but imperfect answer, so the route communicates progress with a remaining witness/evidence consequence instead of a short partial notification.

**Why this priority**: This is the only user-visible value of #1001 and continues the `guard_interrogation_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `guard_interrogation_action.Routing.Partial` / partial result text from shared route data, asserting its mixed-outcome literary qualities, and confirming unchanged action contract plus success/fail sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `guard_interrogation_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial is the mixed keykeeper outcome, **When** the insert is read, **Then** Daren's late-order cover or imperfect social proof gets him through the service door while Лукьян keeps doubt, a remembered face/plaque/cloak trace, delayed suspicion, or later consequence that can feed pursuit pressure.
3. **Given** the existing keykeeper action contract, **When** route data is inspected, **Then** beat id, title, action id, `PrecisionChoice` check, Persuasion characteristic, difficulty/config choices, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to Лукьян, the service door, the lantern, keys, Mira's phrase/authority, Daren's plausible order, remembered face/plaque/cloak, the sleeping guard, and corridor/cabinet continuity.
- The result must not duplicate the #1000 clean success prose or prewrite #1002 fail prose; it should begin after the partial PrecisionChoice and carry the route toward `lock_pick` / "Замок кабинета" under unresolved suspicion.
- The result must not rewrite success/fail outcomes; #1000 is already complete and #1002 remains a separate tracked sibling.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `guard_interrogation_action` partial text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through the imperfect answer, Лукьян's reaction, the managed passage, and the remaining doubt.
- **FR-003**: The insert MUST reflect the `partial` grade: Daren gets through the service door and can continue toward the cabinet, but Лукьян retains suspicion, a remembered detail, a possible journal/log trace, or delayed witness pressure.
- **FR-004**: The insert MUST include concrete sensory/social details around Лукьян's keys/lantern/voice/breath, the service door, the sleeping guard or gallery silence, Mira's authority/phrase/order, Daren's face/voice/body control, a remembered cloak/face/name/detail, and corridor/cabinet continuity.
- **FR-005**: The insert MUST bridge naturally into the next `lock_pick` / "Замок кабинета" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for imperfect order/social proof, Лукьян lantern/keys reaction, Daren body/voice control, remaining witness/evidence risk, service-door passage with cost, and next-cabinet continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, Persuasion characteristic, difficulty, dialogue config choice ids/labels/outcome hints, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren keykeeper action**: `guard_interrogation_action` inside the shared Daren route chapter `guard_interrogation` / "Ключник в галерее".
- **Partial result surface**: The mixed outcome string shown after the PrecisionChoice action resolves as a partial success.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `guard_interrogation` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The partial aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/fail outcome, frontend, downstream lock-pick/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `guard_interrogation` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, downstream lock-pick/rune-memory drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1001-daren-keykeeper-partial` was created from `origin/main` at `2cd7ee4` after #1000 / PR #1033 had landed.
- Focused baseline before #1001 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 64 / failed 0 / skipped 0 / total 64.
- Affected baseline before #1001 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 333 / failed 0 / skipped 0 / total 333.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1001-daren-keykeeper-partial\\specs\\1001-daren-keykeeper-partial` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1001 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
