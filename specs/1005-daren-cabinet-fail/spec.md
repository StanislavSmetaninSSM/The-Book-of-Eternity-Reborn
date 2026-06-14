# Feature Specification: Daren Cabinet Lock Fail Literary Aftermath

**Feature Branch**: `work/1005-daren-cabinet-fail`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), completed siblings [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003) and [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), completed downstream result trio [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #1005 - rewrite result surface `lock_pick` / `lock_pick_action` / `fail` ("Замок кабинета" dangerous outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #974 already rewrote the `lock_pick` scene opening as a full literary page. This issue only rewrites the fail post-action outcome.
- **Completed sibling context**: #1003 rewrote the clean success outcome and #1004 rewrote the mixed partial outcome for this same `lock_pick_action`; #1005 must preserve both unchanged.
- **Completed downstream context**: #1006/#1007/#1008 completed the next `rune_memory_action` result trio and must remain unchanged.
- **Spec Kit justification**: #1005 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `lock_pick_action`, a focused objective guard that fails on the current one-sentence fail text, and local verification evidence.
- **Out of scope**: rewriting the `lock_pick` scene opening, success/partial outcomes (#1003/#1004), rune-memory outcomes (#1006/#1007/#1008), other Daren scenes/results (#988-#1002 and #1009-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Замок щёлкает слишком громко, оставляя улику на накладке, и Дарен слышит, как за стеной меняется дыхание стражи.

## User Scenarios & Testing

### User Story 1 - Failed Cabinet-Lock Outcome Reads As Dangerous Aftermath (Priority: P1)

As a player resolving the "Замок кабинета" LockPinSet check with a fail result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren's dangerous mistake, so the route communicates escalating evidence/noise/pursuit pressure instead of a short failure notification.

**Why this priority**: This is the only user-visible value of #1005 and completes the `lock_pick_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `lock_pick_action.Routing.Fail` / fail result text from shared route data, asserting its dangerous-outcome literary qualities, and confirming unchanged action contract plus success/partial sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `lock_pick_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail is the dangerous cabinet-lock outcome, **When** the insert is read, **Then** the lock mistake creates concrete noise, scratch/evidence, witness/guard awareness, pursuit pressure, or house-memory danger without naming mechanics, score, or debug framing.
3. **Given** the existing lock-pick action contract, **When** route data is inspected, **Then** beat id, title, action id, `LockPinSet` check, Dexterity characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to the cabinet lock, pins, pick slip, loud click, bronze plate scratch, guard breath, and the cost of a failed lock attempt.
- The result must not duplicate the scene opening, #1003 success prose, #1004 partial prose, or later rune-memory prose; it should begin after the failed pin-set and carry the route toward `rune_memory` / "Руны на дверце" under elevated danger.
- The result must not rewrite success/partial outcomes; #1003 and #1004 are already complete.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `lock_pick_action` fail text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through the failed pin alignment, loud lock response, attempted recovery, and immediate consequence.
- **FR-003**: The insert MUST reflect the `fail` grade: danger escalates through noise, a visible trace, witness/guard awareness, house-memory pressure, or evidence that makes later pursuit plausible.
- **FR-004**: The insert MUST include concrete sensory details around pins, pick/tension tool, keyhole/bronze plate, scratch/dust, breath, hands/fingers, lock click/noise, guard or corridor response, and cabinet door movement or blocked entry.
- **FR-005**: The insert MUST bridge naturally into the next `rune_memory` / "Руны на дверце" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for pins/pick failure, Daren body control, loud/noisy consequence, trace/evidence/pursuit pressure, guard/house awareness, and next-rune/futlar continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `LockPinSet` type, Dexterity characteristic, difficulty, config pin/window/timer/durability semantics, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren cabinet-lock action**: `lock_pick_action` inside the shared Daren route chapter `lock_pick` / "Замок кабинета".
- **Fail result surface**: The dangerous outcome string shown after the LockPinSet action resolves as a fail.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `lock_pick` fail text and passes after the rewrite.
- **SC-002**: The fail aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The fail aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/partial outcome, frontend, downstream rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `lock_pick` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, downstream rune-memory drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/1005-daren-cabinet-fail` was created from `origin/main` at `bb33bf7` after #1004 / PR #1031 had landed.
- Focused baseline before #1005 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 62 / failed 0 / skipped 0 / total 62.
- Affected baseline before #1005 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 331 / failed 0 / skipped 0 / total 331.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1005-daren-cabinet-fail\\specs\\1005-daren-cabinet-fail` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #1005 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
