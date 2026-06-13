# Feature Specification: Daren Renara Voice Fail Literary Aftermath

**Feature Branch**: `work/1011-daren-renara-fail`
**Created**: 2026-06-13
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1011](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1011), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976), completed success sibling [#1009](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1009), and completed partial sibling [#1010](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1010)

## Source Issues & Scope

- **Source GitHub issue**: #1011 - rewrite result surface `ward_steward_parley` / `ward_steward_parley_action` / `fail` ("Голос Ренары" dangerous outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #976 already rewrote the `ward_steward_parley` scene opening as a full page. This issue only rewrites the fail post-action outcome.
- **Sibling context**: #1009 rewrote the clean `success` outcome and #1010 rewrote the mixed `partial` outcome. This feature must preserve both sibling prose surfaces and must not close parent #955.
- **Spec Kit justification**: #1011 changes player-facing story/UX copy shared by console and browser. It must preserve QTE route mechanics, result grade semantics, reward/profile behavior, runtime state, endpoints, and sibling result surfaces, while providing durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, or frontend contract change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `ward_steward_parley_action`, a focused objective guard that fails on the current one-sentence fail text, and local verification evidence.
- **Out of scope**: rewriting the `ward_steward_parley` scene opening, success outcome (#1009), partial outcome (#1010), other Daren scenes/results (#988-#1008, #979-#983), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Ренара отвечает резким светом; дом узнаёт Дарена как нарушителя, и тревога получает почти человеческую волю.

## User Scenarios & Testing

### User Story 1 - Failed Renara Outcome Reads As Dangerous Literary Aftermath (Priority: P1)

As a player resolving the "Голос Ренары" PrecisionChoice with the fail outcome, I want the result to read like a dark-fantasy social and ward-alarm aftermath centered on Daren's challenge failing, Renara Wardova identifying him as an intruder, and the house turning its alarm into concrete pursuit pressure, so the route feels like an interactive book instead of a compact result notification.

**Why this priority**: This is the only user-visible value of #1011 and completes the Renara result trio after #1009 success and #1010 partial.

**Independent Test**: The result can be tested independently by reading `ward_steward_parley_action.Routing.Fail` / fail result text from shared route data, asserting its literary aftermath qualities, and confirming unchanged action contract and grade/routing/score semantics.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `ward_steward_parley_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail is the dangerous Renara outcome, **When** the insert is read, **Then** Daren's challenge or wrong answer wakes the ward, Renara/house recognize him as an intruder, and alarm/pursuit/evidence pressure becomes concrete while still letting the route proceed to the next beat.
3. **Given** the existing Renara action contract, **When** route data is inspected, **Then** beat id, title, action id, `PrecisionChoice` check, Wisdom characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira example text from the issue; it should match the quality bar while staying specific to Renara Wardova, the warded house, Daren's failed social answer, and the awakened alarm.
- The result must not duplicate the scene opening, #1009 success prose, or #1010 partial prose; it should begin after the failed answer and carry the route toward the following `physical_pressure` / "Тяжёлая решётка" beat under heightened danger.
- The result must not rewrite success/partial outcomes; those siblings are already completed and are out of scope.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `ward_steward_parley_action` fail text MUST be a substantial Russian dark-fantasy social/ward-alarm aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through body control, fear, tactical reaction, and the cost of having the house recognize him.
- **FR-003**: The insert MUST reflect the `fail` grade: Daren's challenge or incorrect answer escalates danger, Renara Wardova and the house identify him as an intruder, and alarm/pursuit/evidence pressure becomes concrete.
- **FR-004**: The insert MUST include concrete social and sensory details around Renara's ward voice, glass/runes/seals, cold or sharp light, Daren's breath/throat/hands, and the listening house turning against him.
- **FR-005**: The insert MUST bridge naturally into the next heavy-grate beat without changing the next beat's scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for Renara/ward voice, Daren's failed challenge or wrong answer, awakened alarm/pursuit pressure, retained evidence/identity/witness pressure, and next-heavy-grate continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, Wisdom characteristic, difficulty, config choices/outcome mapping, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren Renara parley action**: `ward_steward_parley_action` inside the shared Daren route chapter `ward_steward_parley` / "Голос Ренары".
- **Fail result surface**: The dangerous outcome string shown after the PrecisionChoice action resolves as fail.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `ward_steward_parley` fail text and passes after the rewrite.
- **SC-002**: The fail aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The fail aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/partial outcome, frontend, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `ward_steward_parley` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1011-daren-renara-fail` was created from `origin/main` at `514fc0f`.
- Spec Kit CLI check before implementation reported version 0.9.3.
- Hermes selected #1011 after verifying #1010 / PR #1025 were already merged, closed, and reported.
- Baseline and prerequisite evidence will be recorded in `tasks.md` before Codex implementation begins.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1011 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
