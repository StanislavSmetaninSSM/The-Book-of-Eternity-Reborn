# Feature Specification: Daren Heavy-Grate Success Literary Aftermath

**Feature Branch**: `work/1012-daren-heavy-grate-success`
**Created**: 2026-06-12
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), sibling result follow-ups [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013) and [#1014](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1014)

## Source Issues & Scope

- **Source GitHub issue**: #1012 - rewrite result surface `physical_pressure` / `physical_pressure_action` / `success` ("Тяжёлая решётка" best outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #977 already rewrote the `physical_pressure` scene opening as a full page. This issue only rewrites the clean/best post-action outcome.
- **Spec Kit justification**: #1012 changes player-facing story/UX copy shared by console and browser. It must preserve QTE route mechanics, result grade semantics, reward/profile behavior, runtime state, endpoints, and sibling result surfaces, while providing durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, or frontend contract change is intended.
- **In scope**: one substantial Russian `success` aftermath prose insert for `physical_pressure_action`, a focused objective guard that fails on the current one-sentence success text, and local verification evidence.
- **Out of scope**: rewriting `physical_pressure` scene opening, `partial` or `fail` outcomes (#1013/#1014), other Daren scenes/results (#988-#1011, #979-#983), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Дарен держит решётку до последнего хода механизма, и футляр выходит из ниши без грохота.

## User Scenarios & Testing

### User Story 1 - Clean Heavy-Grate Outcome Reads As Literary Aftermath (Priority: P1)

As a player resolving the "Тяжёлая решётка" QTE with the best outcome, I want the success result to read like a dark-fantasy aftermath page centered on Daren's body, silence, competence, and reduced risk, so the route feels like an interactive book instead of a score notification.

**Why this priority**: This is the only user-visible value of #1012 and continues parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `physical_pressure_action.Routing.Success` / success result text from shared route data, asserting its literary aftermath qualities, and confirming unchanged action contract and grade/routing/score semantics.

**Acceptance Scenarios**:

1. **Given** the player earns the `success` result for `physical_pressure_action`, **When** console or browser renders the outcome, **Then** the shared success text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** success is the clean/best outcome, **When** the insert is read, **Then** Daren's competence, controlled pain, final extraction of the staff case, contained noise, and reduced pursuit/evidence risk are visible without naming mechanics, score, or debug framing.
3. **Given** the existing heavy-grate action contract, **When** route data is inspected, **Then** beat id, title, action id, `MashInput` check, Strength characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not force Mira-style dialogue or invent an NPC exchange because this is a physical/action aftermath, not a social slot.
- The result must not duplicate the scene opening; it should begin after the successful hold and carry the route toward the next `timed_rhythm` corridor beat.
- The result must not rewrite partial/fail outcomes; those remain tracked by #1013 and #1014.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `physical_pressure_action` success text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through movement, pain control, breath/silence discipline, and deliberate extraction of the staff case.
- **FR-003**: The insert MUST reflect the `success` grade: the grate is held through the last mechanism movement, the staff case exits without a crash, alarm/noise risk is reduced, and Daren leaves with controlled momentum.
- **FR-004**: The insert MUST include concrete physical and sensory details around iron weight, shoulder/ribs/hands/blood/breath, stone niche/case/staff, oil/metal/stone sounds, and the listening house.
- **FR-005**: The insert MUST bridge naturally into the next alarm-pulse corridor beat without changing the next beat's scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for heavy-grate resolution, Daren body/breath/control, staff-case/niche extraction, silence/no-crash/reduced-risk stakes, and next-corridor continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `MashInput` type, Strength characteristic, difficulty, config, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren heavy-grate action**: `physical_pressure_action` inside the shared Daren route chapter `physical_pressure` / "Тяжёлая решётка".
- **Success result surface**: The clean/best outcome string shown after the MashInput action resolves successfully.
- **Existing QTE action contract**: The unchanged route/action/check/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `physical_pressure` success text and passes after the rewrite.
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
- **Manual/player-facing verification**: Read the final `physical_pressure` success prose and inspect the diff for prohibited implementation terminology, partial/fail drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1012-daren-heavy-grate-success` was created from `origin/main` at `30d9ee7`.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR=E:\Games\worktrees\boe-1012-daren-heavy-grate-success\specs\1012-daren-heavy-grate-success` with `contracts/` and `tasks.md`.
- Focused baseline before #1012 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 51 / failed 0 / skipped 0 / total 51.
- Affected baseline before #1012 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 320 / failed 0 / skipped 0 / total 320.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1012 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
