# Feature Specification: Daren Rune Memory Success Literary Aftermath

**Feature Branch**: `work/1006-daren-rune-success`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975), sibling result follow-ups [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007) and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #1006 - rewrite result surface `rune_memory` / `rune_memory_action` / `success` ("Руны на дверце" clean outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #975 already rewrote the `rune_memory` scene opening as a full page. This issue only rewrites the clean/best post-action outcome.
- **Spec Kit justification**: #1006 changes player-facing story/UX copy shared by console and browser. It must preserve QTE route mechanics, result grade semantics, reward/profile behavior, runtime state, endpoints, and sibling result surfaces, while providing durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, or frontend contract change is intended.
- **In scope**: one substantial Russian `success` aftermath prose insert for `rune_memory_action`, a focused objective guard that fails on the current one-sentence success text, and local verification evidence.
- **Out of scope**: rewriting the `rune_memory` scene opening, `partial` or `fail` outcomes (#1007/#1008), other Daren scenes/results (#988-#1005 and #1009-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Дарен повторяет узор без дрожи, и руны гаснут одна за другой; дом молчит, оставляя футляр без крика.

## User Scenarios & Testing

### User Story 1 - Clean Rune Outcome Reads As Literary Aftermath (Priority: P1)

As a player resolving the "Руны на дверце" PatternMemory check with the best outcome, I want the success result to read like a dark-fantasy aftermath page centered on Daren's exact memory of the ward pattern, the runed glass/door mechanism, and the house staying quiet, so the route feels like an interactive book instead of a score notification.

**Why this priority**: This is the only user-visible value of #1006 and continues parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `rune_memory_action.Routing.Success` / success result text from shared route data, asserting its literary aftermath qualities, and confirming unchanged action contract and grade/routing/score semantics.

**Acceptance Scenarios**:

1. **Given** the player earns the `success` result for `rune_memory_action`, **When** console or browser renders the outcome, **Then** the shared success text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** success is the clean/best rune-memory outcome, **When** the insert is read, **Then** Daren's precise memory, steady hands, extinguishing/obedient ward runes, quiet opening of the door/futlar path, and reduced alarm/evidence risk are visible without naming mechanics, score, or debug framing.
3. **Given** the existing rune action contract, **When** route data is inspected, **Then** beat id, title, action id, `PatternMemory` check, Perception characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira example text from the issue; it should match the quality bar while staying specific to the runed cabinet/door, protective pattern, Daren's memory, and the listening house.
- The result must not duplicate the scene opening; it should begin after the successful pattern repeat and carry the route toward the following `ward_steward_parley` / "Голос Ренары" beat.
- The result must not rewrite partial/fail outcomes; those remain tracked by #1007 and #1008.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `rune_memory_action` success text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through memory, breath, fingers/hand control, and tactical attention to sound/light.
- **FR-003**: The insert MUST reflect the `success` grade: Daren repeats the ward pattern cleanly, the runes extinguish or obey, the house does not cry alarm, and pursuit/evidence risk is reduced rather than intensified.
- **FR-004**: The insert MUST include concrete sensory details around runed glass/door/futlar, cold light, dust/stone/metal, Daren's hands/breath/throat, and the listening house.
- **FR-005**: The insert MUST bridge naturally into the next Renara/ward-steward beat without changing the next beat's scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for runes/glass/ward pattern, Daren's precise memory/body control, alarm/evidence reduction, quiet opening/futlar access, and next-Renara continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PatternMemory` type, Perception characteristic, difficulty, config pattern/sequence semantics, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren rune-memory action**: `rune_memory_action` inside the shared Daren route chapter `rune_memory` / "Руны на дверце".
- **Success result surface**: The clean/best outcome string shown after the PatternMemory action resolves successfully.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `rune_memory` success text and passes after the rewrite.
- **SC-002**: The success aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The success aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, partial/fail outcome, frontend, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `rune_memory` success prose and inspect the diff for prohibited implementation terminology, partial/fail drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1006-daren-rune-success` was created from `origin/main` at `543cf23`.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR=E:\\Games\\worktrees\\boe-1006-daren-rune-success\\specs\\1006-daren-rune-success` with `contracts/` and `tasks.md`.
- Focused baseline before #1006 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 57 / failed 0 / skipped 0 / total 57.
- Affected baseline before #1006 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 326 / failed 0 / skipped 0 / total 326.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1006 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
