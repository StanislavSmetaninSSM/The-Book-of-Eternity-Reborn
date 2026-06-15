# Feature Specification: Daren Approach Manor Partial Literary Aftermath

**Feature Branch**: `work/989-daren-approach-partial`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#989](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/989), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), completed same-scene sibling [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988), remaining same-scene sibling [#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990), and completed downstream result trios [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991)-[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Source Issues & Scope

- **Source GitHub issue**: #989 - rewrite result surface `approach_manor` / `approach_manor_action` / `partial` ("Подступ к поместью" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #969 already rewrote the `approach_manor` scene opening as a full literary page. This issue only rewrites the partial post-action outcome.
- **Same-scene sibling context**: #988 success is completed and must remain unchanged; #990 fail remains a separate follow-up and must remain unchanged.
- **Completed downstream context**: #991/#992/#993 completed `informant_parley_action`, #994/#995/#996 completed `gadget_infiltration_action`, #997/#998/#999 completed `stealth_crossing_action`, #1000/#1001/#1002 completed `guard_interrogation_action`, #1003/#1004/#1005 completed `lock_pick_action`, and #1006/#1007/#1008 completed `rune_memory_action`; all must remain unchanged.
- **Spec Kit justification**: #989 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `approach_manor_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting the `approach_manor` scene opening (#969), success outcome (#988), fail outcome (#990), downstream Mira/hook/gallery/keykeeper/cabinet/rune outcomes (#991-#1008), other Daren scenes/results, parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Сухая ветка ломается под каблуком, но Дарен успевает прижаться к стене, оставляя страже только сомнение.

## User Scenarios & Testing

### User Story 1 - Approach Partial Reads As Costly Stealth Aftermath (Priority: P1)

As a player resolving "Подступ к поместью" with a partial result, I want the outcome to read like a mixed dark-fantasy stealth aftermath page centered on Daren still reaching the manor approach, but with a trace, doubt, delay, bodily cost, or later consequence left behind, so the route communicates success-with-cost rather than a short mechanical partial notification.

**Why this priority**: This is the only user-visible value of #989 and continues the `approach_manor_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `approach_manor_action.Routing.Partial` / partial result text from shared route data, asserting mixed-outcome literary qualities, and confirming unchanged action contract plus success/fail sibling and downstream surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `approach_manor_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial means Daren's choice works but leaves a cost, **When** the insert is read, **Then** it shows Daren reaching or clinging to the manor-wall/linden approach while a broken branch, sound, doubt, trace, delay, wound, witness suspicion, or later evidence pressure remains plausible.
3. **Given** the existing approach action contract, **When** route data is inspected, **Then** beat id, title, action id, `BranchChoice` check, choice ids/grades, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result may echo the issue's Mira quality example as a form/quality bar, but it must not copy that Mira text. It should stay specific to the already-authored #969 approach scene, old linden, lantern blind spots, patrol/guard pressure, wall/garden approach, and mixed transition toward Mira.
- The result must not duplicate the clean #988 success or future #990 fail prose. It should be clearly mixed: Daren is not caught or stopped outright, but the night keeps a cost or question.
- The result must not create a new branch, skip the Mira scene, permanently escalate pursuit beyond the existing route, or change downstream stakes beyond authored prose; it may plant a plausible trace, doubt, delay, or later consequence while keeping the existing route contract.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `approach_manor_action` partial text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist while showing his stealth choice through movement, body control, shadow, attention to sound/light, and pressure from the environment.
- **FR-003**: The insert MUST reflect the `partial` grade: Daren still reaches or survives the manor-wall/linden approach and continues toward the route, but a broken branch, noise, trace, doubt, delay, wound, witness suspicion, or later evidence consequence remains visible.
- **FR-004**: The insert MUST include concrete sensory/stealth details around the old linden, lantern blind spots, wet night/stone/wall/garden approach, patrol or guard pressure, Daren's breath/steps/hands/body control, and the specific mixed-cost signal.
- **FR-005**: The insert MUST bridge naturally into the next `informant_parley` / "Шёпот Миры" beat and Mira contact without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for manor-wall/linden setting, lantern/shadow stealth, guard/patrol pressure, partial-cost/evidence/suspicion, Daren body-control competence, continued route progress, and next Mira/informant continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `BranchChoice` type, choice ids/labels/grades, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.
- **FR-009**: The #988 success outcome, #990 fail outcome, and downstream #991-#1008 result surfaces MUST remain unchanged unless a test proves a real accidental drift that must be reverted.

### Key Entities

- **Daren approach action**: `approach_manor_action` inside the shared Daren route chapter `approach_manor` / "Подступ к поместью".
- **Partial result surface**: The mixed outcome string shown after the BranchChoice action resolves as partial.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `approach_manor` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 800 characters, has at least 7 scene sentences, and mentions Daren at least 3 times.
- **SC-003**: The partial aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/fail outcome, downstream Mira/hook/gallery/keykeeper/cabinet/rune-memory, frontend, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `approach_manor` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, downstream result drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets, shell injection, eval/unsafe deserialization, and SQL string-formatting patterns, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/989-daren-approach-partial` was created from `origin/main` at `23abd4e` after #988 / PR #1045 had landed.
- Hermes remains responsible for baseline evidence before Codex launch, independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #989 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
