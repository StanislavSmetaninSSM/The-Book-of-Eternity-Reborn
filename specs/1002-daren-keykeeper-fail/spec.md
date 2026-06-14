# Feature Specification: Daren Keykeeper Gallery Fail Literary Aftermath

**Feature Branch**: `work/1002-daren-keykeeper-fail`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), completed keykeeper siblings [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000) and [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001), completed downstream result trios [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005) and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #1002 - rewrite result surface `guard_interrogation` / `guard_interrogation_action` / `fail` ("Ключник в галерее" dangerous outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #973 already rewrote the `guard_interrogation` scene opening as a full literary page. This issue only rewrites the fail post-action outcome.
- **Sibling context**: #1000 completed the clean success outcome and #1001 completed the mixed partial outcome for the same `guard_interrogation_action`; both sibling strings must remain unchanged.
- **Completed downstream context**: #1003/#1004/#1005 completed the next `lock_pick_action` result trio and #1006/#1007/#1008 completed the following `rune_memory_action` result trio; all must remain unchanged.
- **Spec Kit justification**: #1002 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `guard_interrogation_action`, a focused objective guard that fails on the current one-sentence fail text, and local verification evidence.
- **Out of scope**: rewriting the `guard_interrogation` scene opening, success/partial outcomes (#1000/#1001), downstream lock-pick/rune-memory outcomes (#1003-#1008), other Daren scenes/results (#988-#999 and #1009-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Лукьян поднимает фонарь к лицу Дарена, и в галерее рождается свидетель, которого нельзя назвать случайным.

## User Scenarios & Testing

### User Story 1 - Fail Keykeeper Outcome Reads As Dangerous Social Aftermath (Priority: P1)

As a player resolving the "Ключник в галерее" PrecisionChoice with a fail result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren's failed attempt to slip past Лукьян, so the route communicates concrete witness/evidence/noise/pursuit pressure instead of a short failure notification.

**Why this priority**: This is the only user-visible value of #1002 and completes the `guard_interrogation_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `guard_interrogation_action.Routing.Fail` / fail result text from shared route data, asserting its dangerous-outcome literary qualities, and confirming unchanged action contract plus success/partial sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `guard_interrogation_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail is the dangerous keykeeper outcome, **When** the insert is read, **Then** Daren's attempt to hide his face, pass without the expected answer, or rely on silence breaks social cover; Лукьян raises the lantern/keys/voice and becomes a concrete witness who can feed alarm, journal, pursuit, or evidence pressure.
3. **Given** the existing keykeeper action contract, **When** route data is inspected, **Then** beat id, title, action id, `PrecisionChoice` check, Persuasion characteristic, difficulty/config choices, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to Лукьян, the service door, lantern, keys, failed silence/hidden-face attempt, Daren's exposed face/body control, sleeping guard/gallery silence turning into danger, and corridor/cabinet continuity.
- The result must not duplicate the #1000 clean success prose or #1001 mixed partial prose; it should begin after the failed PrecisionChoice and carry the route toward `lock_pick` / "Замок кабинета" under active witness/evidence pressure.
- The result must not rewrite success/partial outcomes; #1000 and #1001 are already complete sibling closures.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `guard_interrogation_action` fail text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through the failed answer/silence, Лукьян's discovery, and the dangerous continuation.
- **FR-003**: The insert MUST reflect the `fail` grade: social cover collapses, Лукьян identifies or memorizes Daren, and concrete witness/evidence/noise/pursuit pressure escalates while the route still continues toward the cabinet.
- **FR-004**: The insert MUST include concrete sensory/social details around Лукьян's keys/lantern/voice/breath, the service door, the sleeping guard or gallery silence, Mira's absent phrase/authority, Daren's exposed face/voice/body control, witness memory or written trace, and corridor/cabinet continuity.
- **FR-005**: The insert MUST bridge naturally into the next `lock_pick` / "Замок кабинета" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for failed answer/hidden-face/silence, Лукьян lantern/keys reaction, Daren exposed face/body control, witness/evidence/alarm/pursuit risk, service-door passage under danger, and next-cabinet continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, Persuasion characteristic, difficulty, dialogue config choice ids/labels/outcome hints, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren keykeeper action**: `guard_interrogation_action` inside the shared Daren route chapter `guard_interrogation` / "Ключник в галерее".
- **Fail result surface**: The dangerous outcome string shown after the PrecisionChoice action resolves as a failure.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `guard_interrogation` fail text and passes after the rewrite.
- **SC-002**: The fail aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The fail aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/partial outcome, frontend, downstream lock-pick/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `guard_interrogation` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, downstream lock-pick/rune-memory drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1002-daren-keykeeper-fail` was created from `origin/main` at `b99a8a2` after #1001 / PR #1034 had landed.
- Focused baseline before #1002 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 65 / failed 0 / skipped 0 / total 65.
- Affected baseline before #1002 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 334 / failed 0 / skipped 0 / total 334.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1002 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
