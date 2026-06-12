# Feature Specification: Daren Heavy-Grate Fail Literary Aftermath

**Feature Branch**: `work/1014-daren-heavy-grate-fail`
**Created**: 2026-06-13
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1014](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1014), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), preceding result [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012), preceding result [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013)

## Source Issues & Scope

- **Source GitHub issue**: #1014 - rewrite result surface `physical_pressure` / `physical_pressure_action` / `fail` ("Тяжёлая решётка" dangerous failure) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #977 already rewrote the `physical_pressure` scene opening as a full page. #1012 and #1013 already rewrote the success/partial results and must remain unchanged.
- **Spec Kit justification**: #1014 changes player-facing story/UX copy shared by console and browser, has explicit literary acceptance criteria, and belongs to the durable #955 Daren content-quality chain. It must preserve QTE route mechanics, grade semantics, reward/profile behavior, runtime state, endpoints, and sibling result surfaces.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, or frontend contract change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `physical_pressure_action`, a focused objective guard that fails on the current terse fail text, and local verification evidence.
- **Out of scope**: rewriting the `physical_pressure` scene opening, success outcome (#1012), partial outcome (#1013), other Daren scenes/results (#988-#1011 and #979-#983), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Решётка падает на камень с тяжёлым грохотом, и Дарену приходится хватать посох под шум тревоги.

## User Scenarios & Testing

### User Story 1 - Failed Heavy-Grate Outcome Reads As Dangerous Literary Aftermath (Priority: P1)

As a player resolving the "Тяжёлая решётка" QTE with a fail outcome, I want the result to read like a dark-fantasy aftermath page centered on Daren causing a loud dangerous mistake and being forced into a compromised escape, so the route communicates escalating consequence rather than a score notification.

**Why this priority**: This is the only user-visible value of #1014 and completes the 09A/09B/09C result-surface set for the #977 heavy-grate scene under parent #955.

**Independent Test**: The result can be tested independently by reading `physical_pressure_action.Routing.Fail` / fail result text from shared route data, asserting its dangerous-failure literary aftermath qualities, and confirming unchanged action contract and grade/routing/score semantics.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `physical_pressure_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail is the dangerous outcome, **When** the insert is read, **Then** the grate/noise/evidence/witness/pursuit pressure becomes concrete while Daren still seizes or salvages the staff under compromised conditions and carries that threat toward the alarm-pulse corridor.
3. **Given** the existing heavy-grate action contract, **When** route data is inspected, **Then** beat id, title, action id, `MashInput` check, Strength characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not duplicate the success or partial aftermath; it should clearly feel worse and more dangerous than both.
- The result must not invent a new branch, fail-state, endpoint, pursuit system, persistent wound, reward penalty, or dialogue runtime; consequences are authored prose only unless existing route data already carries them.
- The result must not rewrite success/partial outcomes; those are already closed by #1012/#1013.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `physical_pressure_action` fail text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist while showing his failed body control, rapid salvage, and compromised movement.
- **FR-003**: The insert MUST reflect the `fail` grade: the grate falls or slams loudly, alarm/pursuit/evidence/witness pressure escalates, and Daren must seize the staff under visible danger rather than cleanly controlling the scene.
- **FR-004**: The insert MUST include concrete physical and sensory details around iron/stone impact, dust/echo/oil/splinters/blood or breath, the staff/case/niche, and the awakened/listening house.
- **FR-005**: The insert MUST bridge naturally into the next alarm-pulse corridor beat with the failure's threat still following Daren.
- **FR-006**: The focused test guard MUST use grouped motif checks for failed heavy-grate escalation, Daren body/breath/control under pressure, staff-case/niche salvage, evidence/noise/witness/pursuit stakes, and next-corridor continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `MashInput` type, Strength characteristic, difficulty, config, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren heavy-grate action**: `physical_pressure_action` inside the shared Daren route chapter `physical_pressure` / "Тяжёлая решётка".
- **Fail result surface**: The dangerous failure outcome string shown after the MashInput action resolves with fail.
- **Existing QTE action contract**: The unchanged route/action/check/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `physical_pressure` fail text and passes after the rewrite.
- **SC-002**: The fail aftermath is at least 850 characters, has at least 7 scene sentences, and mentions Daren at least 3 times.
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
- **Manual/player-facing verification**: Read the final `physical_pressure` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1014-daren-heavy-grate-fail` was created from `origin/main` at `3232f7b`.
- Hermes read `AGENTS.md`, confirmed no worker pause/complete sentinel, confirmed GitHub auth, fetched `origin/main`, and selected #1014 as the next logical open Daren result child after #1013 was already closed and reported.
- Spec Kit prerequisite check resolved `FEATURE_DIR=E:\Games\worktrees\boe-1014-daren-heavy-grate-fail\specs\1014-daren-heavy-grate-fail` with `contracts/` and `tasks.md`; `specify version` reported CLI 0.9.3 and `specify integration list` reported Codex CLI installed/default.
- Focused Daren baseline before implementation passed: 53 passed / 0 failed / 0 skipped / 53 total.
- Affected Daren/QTE/docs/browser baseline before implementation passed: 322 passed / 0 failed / 0 skipped / 322 total.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1014 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
