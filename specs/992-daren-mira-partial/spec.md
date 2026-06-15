# Feature Specification: Daren Mira Whisper Partial Literary Aftermath

**Feature Branch**: `work/992-daren-mira-partial`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#992](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/992), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), completed same-scene sibling [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991), open same-scene sibling [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993), open previous-result follow-ups [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988)/[#989](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/989)/[#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990), and completed downstream result trios [#994](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/994)-[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Source Issues & Scope

- **Source GitHub issue**: #992 - rewrite result surface `informant_parley` / `informant_parley_action` / `partial` ("Шёпот Миры" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #970 already rewrote the `informant_parley` scene opening as a full literary page. This issue only rewrites the partial post-action outcome.
- **Same-scene sibling context**: #991 success is already completed and must remain unchanged; #993 fail remains a separate open follow-up and must remain unchanged.
- **Previous-result context**: #988/#989/#990 remain separate follow-ups for the preceding `approach_manor_action` results and must remain unchanged.
- **Completed downstream context**: #994/#995/#996 completed `gadget_infiltration_action`, #997/#998/#999 completed `stealth_crossing_action`, #1000/#1001/#1002 completed `guard_interrogation_action`, #1003/#1004/#1005 completed `lock_pick_action`, and #1006/#1007/#1008 completed `rune_memory_action`; all must remain unchanged.
- **Spec Kit justification**: #992 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `informant_parley_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting the `informant_parley` scene opening (#970), success outcome (#991), fail outcome (#993), previous `approach_manor` results (#988/#989/#990), downstream hook/gallery/keykeeper/cabinet/rune outcomes (#994-#1008), other Daren scenes/results, parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Мира берёт монету Дарена, но отвечает коротко: ключник устал, а имя капитана она оставляет за следующим долгом.

## User Scenarios & Testing

### User Story 1 - Mira Partial Reads As Costly Trust Aftermath (Priority: P1)

As a player resolving "Шёпот Миры" with a partial result, I want the outcome to read like a dark-fantasy social aftermath page centered on Daren buying partial cooperation at a cost, so the route communicates a mixed informant beat rather than a short mechanical partial-success notification.

**Why this priority**: This is the only user-visible value of #992 and continues the `informant_parley_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `informant_parley_action.Routing.Partial` / partial result text from shared route data, asserting its mixed-outcome literary qualities, and confirming unchanged action contract plus success/fail sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `informant_parley_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial means Daren's action works but not cleanly, **When** the insert is read, **Then** Mira accepts the coin or bargain enough to give useful but incomplete information, while a trace, debt, doubt, delay, source-exposure pressure, or future consequence remains visible.
3. **Given** the existing informant action contract, **When** route data is inspected, **Then** beat id, title, action id, `PrecisionChoice` check, Wisdom characteristic, difficulty/config, choice ids/grades, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result may echo the issue's Mira quality example as a quality bar, but it must not copy that example. It should stay specific to the already-authored #970 scene, the wet awning, Mira's green ribbon and knife-hand tension, the offered coin/debt, Лукьян, Орвальд, source pressure, and Daren moving toward the wall/hook beat.
- The result must not duplicate #991 success or #993 fail prose. It should be clearly mixed: Daren gets a path forward, but not a clean trust win, and Mira retains leverage or leaves a consequence behind.
- The result must not make Mira a permanent enemy, erase all useful information, or change downstream route stakes; it may leave debt/source risk/future suspicion while still letting the route proceed.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `informant_parley_action` partial text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist while showing him make a costly or imperfect social bargain with Mira.
- **FR-003**: The insert MUST reflect the `partial` grade: the action works enough for Mira to give usable direction or warning, but she withholds part of the truth, keeps the captain's name/source leverage, accepts a coin/debt, or leaves a future consequence visible.
- **FR-004**: The insert MUST include concrete sensory/social details around the wet awning, Mira's green ribbon or knife-hand/body language, whispered bargaining, the danger of witnesses/guards/source exposure, and Daren's breath/voice/body control.
- **FR-005**: The insert MUST bridge naturally into the next `gadget_infiltration` / "Крюк и леска" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for Mira/Night Thread presence, partial bargain/coin/debt, incomplete Лукьян/Орвальд information, remaining suspicion/source-pressure consequences, wet-awning/social atmosphere, Daren body/voice control, and next hook-line continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, Wisdom characteristic, difficulty/config, choice ids/labels/grades, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.
- **FR-009**: The #991 success outcome, #993 fail outcome, #988-#990 previous-result surfaces, and downstream #994-#1008 result surfaces MUST remain unchanged unless a test proves a real accidental drift that must be reverted.

### Key Entities

- **Daren informant action**: `informant_parley_action` inside the shared Daren route chapter `informant_parley` / "Шёпот Миры".
- **Partial result surface**: The mixed outcome string shown after the PrecisionChoice action resolves as a partial success.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `informant_parley` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 800 characters, has at least 7 scene sentences, and mentions Daren at least 3 times.
- **SC-003**: The partial aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/fail outcome, previous-result, frontend, downstream hook/gallery/keykeeper/cabinet/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `informant_parley` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, previous-result drift, downstream hook/gallery/keykeeper/cabinet/rune drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/992-daren-mira-partial` was created from `origin/main` at `995be5d` after #991 / PR #1042 had landed.
- Focused baseline before #992 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed 73 / failed 0 / skipped 0 / total 73.
- Affected baseline before #992 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 342 / failed 0 / skipped 0 / total 342.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-992-daren-mira-partial\\specs\\992-daren-mira-partial` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #992 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
