# Feature Specification: Daren Approach Manor Fail Literary Aftermath

**Feature Branch**: `work/990-daren-approach-fail`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), completed same-scene siblings [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988) and [#989](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/989), and completed downstream result surfaces [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991)-[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Source Issues & Scope

- **Source GitHub issue**: #990 - rewrite result surface `approach_manor` / `approach_manor_action` / `fail` ("Подступ к поместью" dangerous outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #969 already rewrote the `approach_manor` scene opening as a full literary page. This issue only rewrites the fail post-action outcome.
- **Same-scene sibling context**: #988 success and #989 partial are completed and must remain unchanged.
- **Completed downstream context**: #991/#992/#993 completed `informant_parley_action`, #994/#995/#996 completed `gadget_infiltration_action`, #997/#998/#999 completed `stealth_crossing_action`, #1000/#1001/#1002 completed `guard_interrogation_action`, #1003/#1004/#1005 completed `lock_pick_action`, and #1006/#1007/#1008 completed `rune_memory_action`; all must remain unchanged.
- **Spec Kit justification**: #990 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `approach_manor_action`, a focused objective guard that fails on the current one-sentence fail text, and local verification evidence.
- **Out of scope**: rewriting the `approach_manor` scene opening (#969), success outcome (#988), partial outcome (#989), downstream Mira/hook/gallery/keykeeper/cabinet/rune outcomes (#991-#1008), other Daren scenes/results, parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Дарен теряет драгоценный миг у освещённой калитки, и патруль начинает смотреть в его сторону.

## User Scenarios & Testing

### User Story 1 - Approach Fail Reads As Dangerous Stealth Aftermath (Priority: P1)

As a player resolving "Подступ к поместью" with a fail result, I want the outcome to read like a dangerous dark-fantasy stealth aftermath page centered on Daren's costly mistake at the lit gate/linden approach, so the result communicates concrete alarm, pursuit, witness, or evidence pressure instead of a short mechanical failure notification.

**Why this priority**: This is the only user-visible value of #990 and completes the `approach_manor_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `approach_manor_action.Routing.Fail` / fail result text from shared route data, asserting dangerous-outcome literary qualities, and confirming unchanged action contract plus success/partial sibling and downstream surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `approach_manor_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail means the approach goes badly, **When** the insert is read, **Then** it shows Daren losing the clean shadow near the old linden/lit gate while patrol, guard, dog, lantern, witness, pursuit, or evidence pressure becomes concrete.
3. **Given** the existing approach action contract, **When** route data is inspected, **Then** beat id, title, action id, `BranchChoice` check, choice ids/grades, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result may echo the issue's Mira quality example as a form/quality bar, but it must not copy that Mira text. It should stay specific to the already-authored #969 approach scene, old linden, illuminated gate, lantern blind spots, patrol/guard pressure, wall/garden approach, and the dangerous transition toward Mira.
- The result must not duplicate the clean #988 success or mixed #989 partial prose. It should be clearly fail/dangerous: Daren is not simply delayed; the night gains a real trail, witness, alarm vector, or pursuit pressure.
- The result must not create a new branch, skip the Mira scene, permanently stop the route, or change downstream stakes beyond authored prose; it may plant concrete pursuit/evidence pressure while keeping the existing route contract.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `approach_manor_action` fail text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist while showing his stealth mistake through movement, body control under pressure, shadow/light contrast, sound, and patrol response.
- **FR-003**: The insert MUST reflect the `fail` grade: Daren loses the clean approach near the lit gate/old linden, and patrol/guard/dog/lantern/witness/pursuit/evidence pressure becomes concrete.
- **FR-004**: The insert MUST include concrete sensory/stealth details around the old linden, illuminated gate, wet night/stone/wall/garden approach, patrol or guard attention, Daren's breath/steps/hands/body control, and the specific danger/escalation signal.
- **FR-005**: The insert MUST bridge naturally into the next `informant_parley` / "Шёпот Миры" beat and Mira contact without changing that scene text or route ordering; Daren may continue under pressure, with a trail behind him.
- **FR-006**: The focused test guard MUST use grouped motif checks for manor-wall/linden setting, lantern/gate/light exposure, guard/patrol/dog/witness pressure, fail-grade pursuit/evidence escalation, Daren body-control under danger, continued route progress, and next Mira/informant continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `BranchChoice` type, choice ids/labels/grades, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.
- **FR-009**: The #988 success outcome, #989 partial outcome, and downstream #991-#1008 result surfaces MUST remain unchanged unless a test proves a real accidental drift that must be reverted.

### Key Entities

- **Daren approach action**: `approach_manor_action` inside the shared Daren route chapter `approach_manor` / "Подступ к поместью".
- **Fail result surface**: The dangerous outcome string shown after the BranchChoice action resolves as fail.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `approach_manor` fail text and passes after the rewrite.
- **SC-002**: The fail aftermath is at least 800 characters, has at least 7 scene sentences, and mentions Daren at least 3 times.
- **SC-003**: The fail aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/partial outcome, downstream Mira/hook/gallery/keykeeper/cabinet/rune-memory, frontend, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `approach_manor` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, downstream result drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets, shell injection, eval/unsafe deserialization, and SQL string-formatting patterns, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/990-daren-approach-fail` was created from `origin/main` at `5dda30b` after #989 / PR #1046 had landed.
- Baseline focused Daren test before implementation passed: 77 total, 77 passed, 0 failed, 0 skipped.
- Baseline affected Daren/QTE/docs/browser C# slice before implementation passed: 346 total, 346 passed, 0 failed, 0 skipped.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #990 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
