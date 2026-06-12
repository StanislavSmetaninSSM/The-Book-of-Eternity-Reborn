# Feature Specification: Daren Heavy-Grate Partial Literary Aftermath

**Feature Branch**: `work/1013-daren-heavy-grate-partial`
**Created**: 2026-06-12
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), preceding result [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012), sibling result follow-up [#1014](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1014)

## Source Issues & Scope

- **Source GitHub issue**: #1013 - rewrite result surface `physical_pressure` / `physical_pressure_action` / `partial` ("Тяжёлая решётка" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #977 already rewrote the `physical_pressure` scene opening as a full page. #1012 already rewrote the clean `success` result and must remain unchanged.
- **Spec Kit justification**: #1013 changes player-facing story/UX copy shared by console and browser. It must preserve QTE route mechanics, grade semantics, reward/profile behavior, runtime state, endpoints, and sibling result surfaces while providing durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, or frontend contract change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `physical_pressure_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting `physical_pressure` scene opening, `success` outcome (#1012), `fail` outcome (#1014), other Daren scenes/results (#988-#1011 and #979-#983), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Железо проседает и бьёт Дарена по плечу, но посох уже свободен от каменной ниши.

## User Scenarios & Testing

### User Story 1 - Mixed Heavy-Grate Outcome Reads As Literary Aftermath (Priority: P1)

As a player resolving the "Тяжёлая решётка" QTE with a partial outcome, I want the result to read like a dark-fantasy aftermath page centered on Daren succeeding with pain, delay, trace, and doubt, so the route communicates consequence rather than a score notification.

**Why this priority**: This is the only user-visible value of #1013 and continues parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `physical_pressure_action.Routing.Partial` / partial result text from shared route data, asserting its mixed-outcome literary aftermath qualities, and confirming unchanged action contract and grade/routing/score semantics.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `physical_pressure_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial is success with a cost/doubt, **When** the insert is read, **Then** the staff/posoh is freed, but the grate strikes Daren, leaves physical cost, noise/trace/delay/suspicion risk, or later consequence visible without naming mechanics, score, or debug framing.
3. **Given** the existing heavy-grate action contract, **When** route data is inspected, **Then** beat id, title, action id, `MashInput` check, Strength characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not force Mira-style dialogue or invent an NPC exchange because this is a physical/action aftermath, not a social slot.
- The result must not duplicate the scene opening or #1012 success text; it should begin after the mixed hold and carry the route toward the next `timed_rhythm` corridor beat.
- The result must not rewrite success/fail outcomes; success is already closed by #1012 and fail remains tracked by #1014.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `physical_pressure_action` partial text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through pain, breath/silence discipline, momentum recovery, and extraction of the staff case.
- **FR-003**: The insert MUST reflect the `partial` grade: the staff/posoh becomes free, but the grate hits or catches Daren, creating visible cost, delay, trace, doubt, wound, noise, or pursuit/evidence risk.
- **FR-004**: The insert MUST include concrete physical and sensory details around iron weight, shoulder/ribs/hands/breath, stone niche/case/staff, metal/stone/oil sounds, and the listening house.
- **FR-005**: The insert MUST bridge naturally into the next alarm-pulse corridor beat with the mixed outcome's consequence still following Daren.
- **FR-006**: The focused test guard MUST use grouped motif checks for mixed heavy-grate resolution, Daren body/breath/control, staff-case/niche extraction, cost/trace/doubt/pursuit stakes, and next-corridor continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `MashInput` type, Strength characteristic, difficulty, config, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren heavy-grate action**: `physical_pressure_action` inside the shared Daren route chapter `physical_pressure` / "Тяжёлая решётка".
- **Partial result surface**: The mixed outcome string shown after the MashInput action resolves with partial success.
- **Existing QTE action contract**: The unchanged route/action/check/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `physical_pressure` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 850 characters, has at least 7 scene sentences, and mentions Daren at least 3 times.
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
- **Manual/player-facing verification**: Read the final `physical_pressure` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1013-daren-heavy-grate-partial` was created from `origin/main` at `30b6622`.
- Hermes read `AGENTS.md`, confirmed no worker pause/complete sentinel, confirmed GitHub auth, fetched `origin/main`, and selected #1013 as the next logical open Daren result child after #1012 was already reported closed.
- Spec Kit prerequisite check resolved `FEATURE_DIR=E:\Games\worktrees\boe-1013-daren-heavy-grate-partial\specs\1013-daren-heavy-grate-partial` with `contracts/` and `tasks.md`.
- Focused Daren baseline before #1013 implementation passed: 52 passed / 0 failed / 0 skipped / 52 total.
- Affected Daren/QTE/docs/browser baseline before #1013 implementation passed: 321 passed / 0 failed / 0 skipped / 321 total.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1013 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
