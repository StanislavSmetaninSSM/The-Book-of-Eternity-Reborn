# Feature Specification: Daren Renara Voice Partial Literary Aftermath

**Feature Branch**: `work/1010-daren-renara-partial`
**Created**: 2026-06-13
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1010](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1010), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976), completed success sibling [#1009](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1009), and fail sibling [#1011](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1011)

## Source Issues & Scope

- **Source GitHub issue**: #1010 - rewrite result surface `ward_steward_parley` / `ward_steward_parley_action` / `partial` ("Голос Ренары" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #976 already rewrote the `ward_steward_parley` scene opening as a full page. This issue only rewrites the partial post-action outcome.
- **Sibling context**: #1009 has already rewritten the clean `success` outcome. This feature must preserve that success prose and must not close or implement #1011 fail.
- **Spec Kit justification**: #1010 changes player-facing story/UX copy shared by console and browser. It must preserve QTE route mechanics, result grade semantics, reward/profile behavior, runtime state, endpoints, and sibling result surfaces, while providing durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, or frontend contract change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `ward_steward_parley_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting `ward_steward_parley` scene opening, `success` outcome (#1009), `fail` outcome (#1011), other Daren scenes/results (#988-#1008, #979-#983), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Ренара отпускает Дарена с холодным предупреждением: дом подождёт, но голос вора она уже держит в рунах.

## User Scenarios & Testing

### User Story 1 - Mixed Renara Outcome Reads As Literary Social Aftermath (Priority: P1)

As a player resolving the "Голос Ренары" PrecisionChoice with the partial outcome, I want the result to read like a dark-fantasy social aftermath page centered on Daren's promise to return/restore the seal, Renara Wardova's cold delayed judgment, and the house keeping his voice as a trace, so the route feels like an interactive book instead of a compact result notification.

**Why this priority**: This is the only user-visible value of #1010 and continues parent #955's corrected quality bar for post-QTE outcome prose after the #1009 success closure.

**Independent Test**: The result can be tested independently by reading `ward_steward_parley_action.Routing.Partial` / partial result text from shared route data, asserting its literary aftermath qualities, and confirming unchanged action contract and grade/routing/score semantics.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `ward_steward_parley_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial is a mixed social outcome, **When** the insert is read, **Then** Daren's promise to return or restore the ward delays the alarm, but Renara Wardova keeps his voice/name/trace in the runes and the house remains suspicious without immediately escalating to the fail-grade alarm.
3. **Given** the existing Renara action contract, **When** route data is inspected, **Then** beat id, title, action id, `PrecisionChoice` check, Wisdom characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira example text from the issue; it should match the quality bar while staying specific to Renara Wardova, the warded house, and Daren's partial promise-return answer.
- The result must not duplicate the scene opening or the #1009 success prose; it should begin after the partial answer and carry the route toward the following `physical_pressure` / "Тяжёлая решётка" beat with suspicion/delay still visible.
- The result must not rewrite success/fail outcomes; success remains #1009 authority and fail remains tracked by #1011.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `ward_steward_parley_action` partial text MUST be a substantial Russian dark-fantasy social aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through voice control, posture, tactical restraint, and the social cost of promising a return/restoration.
- **FR-003**: The insert MUST reflect the `partial` grade: Daren's promise delays or softens the alarm enough to continue, but Renara Wardova and the house retain a suspicious trace of his voice and a later consequence remains plausible.
- **FR-004**: The insert MUST include concrete social and sensory details around Renara's ward voice, glass/runes/seals, cold light, Daren's breath/throat/hands, and the listening house.
- **FR-005**: The insert MUST bridge naturally into the next heavy-grate beat without changing the next beat's scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for Renara/ward voice, Daren's promise-return answer, delayed or softened alarm, retained voice/trace/consequence, and next-heavy-grate continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, Wisdom characteristic, difficulty, config choices/outcome mapping, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren Renara parley action**: `ward_steward_parley_action` inside the shared Daren route chapter `ward_steward_parley` / "Голос Ренары".
- **Partial result surface**: The mixed outcome string shown after the PrecisionChoice action resolves partially.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `ward_steward_parley` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The partial aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/fail outcome, frontend, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `ward_steward_parley` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1010-daren-renara-partial` was created from `origin/main` at `be0bbd4`.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR=E:\Games\worktrees\boe-1010-daren-renara-partial\specs\1010-daren-renara-partial` with `contracts/` and `tasks.md`.
- `specify version` reported CLI 0.9.3; `specify integration list` reported Codex CLI installed/default.
- Focused baseline before #1010 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 55 / failed 0 / skipped 0 / total 55.
- Affected baseline before #1010 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 324 / failed 0 / skipped 0 / total 324.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1010 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
