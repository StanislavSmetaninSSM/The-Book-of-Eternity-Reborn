# Feature Specification: Daren Mira Whisper Fail Literary Aftermath

**Feature Branch**: `work/993-daren-mira-fail`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), completed same-scene siblings [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991) and [#992](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/992), previous-result follow-ups [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988)/[#989](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/989)/[#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990), and completed downstream result trios [#994](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/994)-[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Source Issues & Scope

- **Source GitHub issue**: #993 - rewrite result surface `informant_parley` / `informant_parley_action` / `fail` ("Шёпот Миры" dangerous outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #970 already rewrote the `informant_parley` scene opening as a full literary page. This issue only rewrites the fail post-action outcome.
- **Same-scene sibling context**: #991 success and #992 partial are already completed and must remain unchanged.
- **Previous-result context**: #988/#989/#990 remain separate follow-ups for the preceding `approach_manor` results and must remain unchanged.
- **Completed downstream context**: #994/#995/#996 completed `gadget_infiltration_action`, #997/#998/#999 completed `stealth_crossing_action`, #1000/#1001/#1002 completed `guard_interrogation_action`, #1003/#1004/#1005 completed `lock_pick_action`, and #1006/#1007/#1008 completed `rune_memory_action`; all must remain unchanged.
- **Spec Kit justification**: #993 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `informant_parley_action`, a focused objective guard that fails on the current one-sentence fail text, and local verification evidence.
- **Out of scope**: rewriting the `informant_parley` scene opening (#970), success outcome (#991), partial outcome (#992), previous `approach_manor` results (#988/#989/#990), downstream hook/gallery/keykeeper/cabinet/rune outcomes (#994-#1008), other Daren scenes/results, parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Мира замолкает после угрозы Дарена; её взгляд обещает, что слух о наглом воре найдёт стражу быстрее него.

## User Scenarios & Testing

### User Story 1 - Mira Fail Reads As Dangerous Social Aftermath (Priority: P1)

As a player resolving "Шёпот Миры" with a failed result, I want the outcome to read like a dangerous dark-fantasy social aftermath page centered on Daren losing the informant exchange and creating witness/alarm pressure, so the route communicates an escalated failure rather than a short mechanical fail notification.

**Why this priority**: This is the only user-visible value of #993 and completes the `informant_parley_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `informant_parley_action.Routing.Fail` / fail result text from shared route data, asserting dangerous-outcome literary qualities, and confirming unchanged action contract plus success/partial sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `informant_parley_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail means Daren threatens or mishandles Mira, **When** the insert is read, **Then** Mira becomes a concrete witness or hostile source of pressure, information is lost or poisoned, and guards/pursuit/evidence/social risk escalates visibly while the route can still continue toward the next beat.
3. **Given** the existing informant action contract, **When** route data is inspected, **Then** beat id, title, action id, `PrecisionChoice` check, Wisdom characteristic, difficulty/config, choice ids/grades, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result may echo the issue's Mira quality example as a quality bar, but it must not copy that example. It should stay specific to the already-authored #970 scene, the wet awning, Mira's green ribbon and knife-hand tension, Daren's failed threat, witness/source pressure, and the route's turn toward the wall/hook beat.
- The result must not duplicate #991 success or #992 partial prose. It should be clearly dangerous: Daren may still move toward the next action, but the social exchange has failed and Mira/guards/evidence now create a sharper threat.
- The result must not create a new branch, permanently end the route, or change downstream route stakes beyond authored prose; it may show pursuit pressure, witness memory, poisoned rumor, delayed guard attention, or a future consequence while keeping the existing route contract.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `informant_parley_action` fail text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist while showing that his threat or failed social pressure turns Mira from informant into witness/adversarial pressure.
- **FR-003**: The insert MUST reflect the `fail` grade: usable trust/information collapses or becomes poisoned, while guard/pursuit/evidence/source-exposure pressure becomes concrete and dangerous.
- **FR-004**: The insert MUST include concrete sensory/social details around the wet awning, Mira's green ribbon or knife-hand/body language, whispered bargaining turning hostile, witnesses/guards/source exposure, and Daren's breath/voice/body control under stress.
- **FR-005**: The insert MUST bridge naturally into the next `gadget_infiltration` / "Крюк и леска" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for Mira/Night Thread presence, failed threat/social collapse, lost/poisoned Лукьян/Орвальд information, witness/source-pressure or guard consequence, wet-awning/social atmosphere, Daren body/voice control, and next hook-line continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, Wisdom characteristic, difficulty/config, choice ids/labels/grades, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.
- **FR-009**: The #991 success outcome, #992 partial outcome, #988-#990 previous-result surfaces, and downstream #994-#1008 result surfaces MUST remain unchanged unless a test proves a real accidental drift that must be reverted.

### Key Entities

- **Daren informant action**: `informant_parley_action` inside the shared Daren route chapter `informant_parley` / "Шёпот Миры".
- **Fail result surface**: The dangerous outcome string shown after the PrecisionChoice action resolves as a failure.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `informant_parley` fail text and passes after the rewrite.
- **SC-002**: The fail aftermath is at least 800 characters, has at least 7 scene sentences, and mentions Daren at least 3 times.
- **SC-003**: The fail aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/partial outcome, previous-result, frontend, downstream hook/gallery/keykeeper/cabinet/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `informant_parley` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, previous-result drift, downstream hook/gallery/keykeeper/cabinet/rune drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/993-daren-mira-fail` was created from `origin/main` at `d0cba80` after #992 / PR #1043 had landed.
- Hermes remains responsible for baseline evidence before Codex launch, independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #993 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
