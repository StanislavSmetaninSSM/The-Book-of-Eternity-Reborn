# Feature Specification: Daren Rune Memory Fail Literary Aftermath

**Feature Branch**: `work/1008-daren-rune-fail`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975), completed sibling [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), completed sibling [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)

## Source Issues & Scope

- **Source GitHub issue**: #1008 - rewrite result surface `rune_memory` / `rune_memory_action` / `fail` ("Руны на дверце" dangerous outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #975 already rewrote the `rune_memory` scene opening as a full page. This issue only rewrites the failed post-action outcome.
- **Completed sibling context**: #1006 rewrote only the `success` result and #1007 rewrote only the `partial` result for the same action. #1008 must preserve both texts unchanged.
- **Spec Kit justification**: #1008 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `rune_memory_action`, a focused objective guard that fails on the current one-sentence fail text, and local verification evidence.
- **Out of scope**: rewriting the `rune_memory` scene opening, `success` or `partial` outcomes (#1006/#1007), other Daren scenes/results (#988-#1005 and #1009-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Руны вспыхивают тревожным светом, и Дарен понимает, что дом уже запомнил его прикосновение.

## User Scenarios & Testing

### User Story 1 - Failed Rune Outcome Reads As Dangerous Literary Aftermath (Priority: P1)

As a player resolving the "Руны на дверце" PatternMemory check with a fail result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren triggering the ward's attention and leaving concrete danger behind, so the route feels like an interactive book rather than a score notification.

**Why this priority**: This is the only user-visible value of #1008 and completes the `rune_memory_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `rune_memory_action.Routing.Fail` / fail result text from shared route data, asserting its dangerous-outcome literary qualities, and confirming unchanged action contract and success/partial sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `rune_memory_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail is the dangerous rune-memory outcome, **When** the insert is read, **Then** the ward misread, alarm, house memory, evidence, witness pressure, or pursuit risk becomes concrete and escalates around Daren without naming mechanics, score, or debug framing.
3. **Given** the existing rune action contract, **When** route data is inspected, **Then** beat id, title, action id, `PatternMemory` check, Perception characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira example text from the issue; it should match the quality bar while staying specific to the runed cabinet/door, protective pattern, flare of the ward, Daren's bodily reaction, the listening house, and the next Renara/ward-steward pressure.
- The result must not duplicate the scene opening or the already-accepted #1006/#1007 success/partial texts; it should begin after the failed pattern repeat and carry the route toward the following `ward_steward_parley` / "Голос Ренары" beat with danger heightened.
- The result must not rewrite success/partial outcomes; both are already closed by #1006/#1007.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `rune_memory_action` fail text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through memory collapse, physical control, tactical attention to sound/light, and immediate aftermath movement under pressure.
- **FR-003**: The insert MUST reflect the `fail` grade: the protective pattern rejects or marks Daren, the house/ward remembers his touch, and concrete alarm/evidence/witness/pursuit pressure escalates.
- **FR-004**: The insert MUST include concrete sensory details around runed glass/door/futlar, cold or violent blue light, dust/stone/metal, Daren's hands/breath/throat, and the listening house.
- **FR-005**: The insert MUST bridge naturally into the next Renara/ward-steward beat without changing the next beat's scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for runes/glass/ward flare, Daren's failed memory/body reaction, concrete house memory or evidence, alarm/witness/pursuit pressure, and next-Renara continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PatternMemory` type, Perception characteristic, difficulty, config pattern/sequence semantics, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren rune-memory action**: `rune_memory_action` inside the shared Daren route chapter `rune_memory` / "Руны на дверце".
- **Fail result surface**: The dangerous outcome string shown after the PatternMemory action resolves as a failure.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `rune_memory` fail text and passes after the rewrite.
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
- **Manual/player-facing verification**: Read the final `rune_memory` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1008-daren-rune-fail` was created from `origin/main` at `05c0fe0` after #1007 / PR #1028 had landed and the #1007 closure report was confirmed delivered.
- Focused baseline before #1008 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 59 / failed 0 / skipped 0 / total 59.
- Affected baseline before #1008 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 328 / failed 0 / skipped 0 / total 328.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR=E:\\Games\\worktrees\\boe-1008-daren-rune-fail\\specs\\1008-daren-rune-fail` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Implementation Evidence

- RED guard: `DarenRuneMemoryFail_ReadsAsDangerousRuneAftermathWithoutMechanicDrift` failed against the old one-sentence fail text with 59 passed / 1 failed / 0 skipped / 60 total.
- GREEN focused Daren run after prose rewrite: 60 passed / 0 failed / 0 skipped / 60 total.
- Affected Daren/QTE/docs/browser C# slice after prose rewrite: 329 passed / 0 failed / 0 skipped / 329 total.
- Client and test-project builds completed with 0 warnings / 0 errors.
- Working-tree diff inspection found only the fail prose rewrite, focused test guard/helper updates, and Spec Kit evidence/checklist updates; no frontend, runtime-state, GM-facing, success/partial, route-order, routing, scoring, reward, or endpoint changes were made.
- Added-line source/test static scan returned `NO_MATCHES`.

## Assumptions

- Issue #1008 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
