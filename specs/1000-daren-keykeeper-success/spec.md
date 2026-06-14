# Feature Specification: Daren Keykeeper Gallery Success Literary Aftermath

**Feature Branch**: `work/1000-daren-keykeeper-success`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), sibling result follow-ups [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)/[#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), completed downstream result trios [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005) and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #1000 - rewrite result surface `guard_interrogation` / `guard_interrogation_action` / `success` ("Ключник в галерее" clean outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #973 already rewrote the `guard_interrogation` scene opening as a full literary page. This issue only rewrites the success post-action outcome.
- **Sibling context**: #1001 and #1002 will cover partial/fail outcomes for the same `guard_interrogation_action`; #1000 must preserve both current sibling strings unchanged.
- **Completed downstream context**: #1003/#1004/#1005 completed the next `lock_pick_action` result trio and #1006/#1007/#1008 completed the following `rune_memory_action` result trio; all must remain unchanged.
- **Spec Kit justification**: #1000 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `success` aftermath prose insert for `guard_interrogation_action`, a focused objective guard that fails on the current one-sentence success text, and local verification evidence.
- **Out of scope**: rewriting the `guard_interrogation` scene opening, partial/fail outcomes (#1001/#1002), downstream lock-pick/rune-memory outcomes (#1003-#1008), other Daren scenes/results (#988-#999 and #1009-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Лукьян Седой Ключник узнаёт пароль Миры, отворачивает фонарь и оставляет Дарену чистую дверь к кабинету.

## User Scenarios & Testing

### User Story 1 - Successful Keykeeper Outcome Reads As Clean Social Aftermath (Priority: P1)

As a player resolving the "Ключник в галерее" PrecisionChoice with a success result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren's exact answer and quiet passage, so the route communicates competence, reduced risk, and momentum toward the cabinet instead of a short success notification.

**Why this priority**: This is the only user-visible value of #1000 and begins the `guard_interrogation_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `guard_interrogation_action.Routing.Success` / success result text from shared route data, asserting its clean-outcome literary qualities, and confirming unchanged action contract plus partial/fail sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `success` result for `guard_interrogation_action`, **When** console or browser renders the outcome, **Then** the shared success text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** success is the clean keykeeper outcome, **When** the insert is read, **Then** Daren's precise Mira-linked phrase, controlled body language, and social calibration turn Лукьян from immediate witness into a drowsy/reluctant non-alarm path without naming mechanics, score, or debug framing.
3. **Given** the existing keykeeper action contract, **When** route data is inspected, **Then** beat id, title, action id, `PrecisionChoice` check, Persuasion characteristic, difficulty/config choices, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to Лукьян, the service door, the lantern, keys, Mira's phrase, the sleeping guard, and Daren's controlled passage.
- The result must not duplicate the scene opening, future #1001 partial prose, future #1002 fail prose, or later cabinet-lock prose; it should begin after the successful PrecisionChoice and carry the route toward `lock_pick` / "Замок кабинета" under reduced suspicion.
- The result must not rewrite partial/fail outcomes; #1001 and #1002 remain separate tracked siblings.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `guard_interrogation_action` success text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through the spoken phrase, Лукьян's reaction, the managed silence, and the passage through the service door.
- **FR-003**: The insert MUST reflect the `success` grade: the exact answer lowers immediate alarm, Лукьян averts or lowers the lantern, witness risk is reduced, and Daren gains clean momentum toward the cabinet.
- **FR-004**: The insert MUST include concrete sensory/social details around Лукьян's keys/lantern/voice, the service door, the sleeping guard or gallery silence, Mira's phrase/authority, Daren's face/body control, and the corridor/cabinet continuity.
- **FR-005**: The insert MUST bridge naturally into the next `lock_pick` / "Замок кабинета" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for Mira phrase/social proof, Лукьян lantern/keys reaction, Daren body/voice control, reduced witness/alarm risk, clean service-door passage, and next-cabinet continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, Persuasion characteristic, difficulty, dialogue config choice ids/labels/outcome hints, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren keykeeper action**: `guard_interrogation_action` inside the shared Daren route chapter `guard_interrogation` / "Ключник в галерее".
- **Success result surface**: The clean outcome string shown after the PrecisionChoice action resolves as a success.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `guard_interrogation` success text and passes after the rewrite.
- **SC-002**: The success aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The success aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, partial/fail outcome, frontend, downstream lock-pick/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `guard_interrogation` success prose and inspect the diff for prohibited implementation terminology, partial/fail drift, downstream lock-pick/rune-memory drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1000-daren-keykeeper-success` was created from `origin/main` at `34575ec` after #1005 / PR #1032 had landed.
- Focused baseline before #1000 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 63 / failed 0 / skipped 0 / total 63.
- Affected baseline before #1000 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 332 / failed 0 / skipped 0 / total 332.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1000-daren-keykeeper-success\\specs\\1000-daren-keykeeper-success` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1000 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
