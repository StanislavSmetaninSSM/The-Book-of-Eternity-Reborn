# Feature Specification: Daren Hook and Line Partial Literary Aftermath

**Feature Branch**: `work/995-daren-hook-partial`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#995](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/995), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), completed same-scene sibling [#994](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/994), remaining same-scene sibling [#996](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/996), completed downstream result trio [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997)/[#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998)/[#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999), completed downstream result trios [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000)/[#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)/[#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #995 - rewrite result surface `gadget_infiltration` / `gadget_infiltration_action` / `partial` ("Крюк и леска" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #971 already rewrote the `gadget_infiltration` scene opening as a full literary page.
- **Completed same-scene sibling**: #994 rewrote the success result and must remain unchanged.
- **Remaining same-scene sibling**: #996 remains a separate follow-up for the fail result and must remain unchanged.
- **Completed downstream context**: #997/#998/#999 completed `stealth_crossing_action`, #1000/#1001/#1002 completed `guard_interrogation_action`, #1003/#1004/#1005 completed `lock_pick_action`, and #1006/#1007/#1008 completed `rune_memory_action`; all must remain unchanged.
- **Spec Kit justification**: #995 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `gadget_infiltration_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting the `gadget_infiltration` scene opening, success outcome (#994), fail outcome (#996), downstream gallery/keykeeper/cabinet/rune-memory outcomes (#997-#1008), other Daren scenes/results, parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Крюк держит, но леска звенит по камню; Дарен замирает на балконе, слушая двор.

## User Scenarios & Testing

### User Story 1 - Partial Hook Launch Reads As Mixed Infiltration Aftermath (Priority: P1)

As a player resolving "Крюк и леска" with a partial result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren reaching the balcony at a cost, so the route communicates a mixed success with danger, trace, or doubt rather than a short mechanical partial notification.

**Why this priority**: This is the only user-visible value of #995 and continues the `gadget_infiltration_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `gadget_infiltration_action.Routing.Partial` / partial result text from shared route data, asserting its mixed-outcome literary qualities, and confirming unchanged action contract plus success/fail sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `gadget_infiltration_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial means the hook-and-line action works but not cleanly, **When** the insert is read, **Then** the hook holds and Daren reaches or nears the balcony, but the line rings, scrapes, wounds, delays him, leaves a trace, or creates later witness/evidence pressure.
3. **Given** the existing hook action contract, **When** route data is inspected, **Then** beat id, title, action id, `ChargeRelease` check, Dexterity characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to wet stone, balcony, folding hook, line tension, courtyard/fonar patrols, Daren's controlled but costly ascent, and the transition into the gallery.
- The result must not duplicate #994 success or #996 fail prose. It should be clearly mixed: the hook holds and the route continues, but the action leaves noise, hesitation, pain, trace, witness suspicion, or later consequence.
- The result must not turn into a fail: no full alarm, no immediate capture, no route break, and no change to the next `stealth_crossing` scene.
- The result must not make Daren invulnerable or erase downstream route stakes; the partial cost should plausibly carry into later gallery/keykeeper/cabinet/rune pressure without changing mechanics.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `gadget_infiltration_action` partial text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist while showing a hook launch and balcony ascent that succeeds with cost.
- **FR-003**: The insert MUST reflect the `partial` grade: the hook holds and Daren continues, but line noise, scrape, delay, injury, visible trace, witness suspicion, or later evidence pressure remains.
- **FR-004**: The insert MUST include concrete sensory/action details around the folding hook, line tension or ring, wet stone or balcony wood/rail, courtyard/fonar or guard patrol presence, Daren's body/breath/hand control, and the cost that makes the outcome not clean.
- **FR-005**: The insert MUST bridge naturally into the next `stealth_crossing` / "Галерея без звука" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for hook/line equipment, partial-cost sound/trace/delay, Daren body/breath/hand control, courtyard/patrol atmosphere, balcony/window continuation, and next gallery continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `ChargeRelease` type, Dexterity characteristic, difficulty/config, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.
- **FR-009**: The #994 success outcome, #996 fail outcome, and downstream #997-#1008 result surfaces MUST remain unchanged unless a test proves a real accidental drift that must be reverted.

### Key Entities

- **Daren hook action**: `gadget_infiltration_action` inside the shared Daren route chapter `gadget_infiltration` / "Крюк и леска".
- **Partial result surface**: The mixed outcome string shown after the ChargeRelease action resolves as a partial result.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `gadget_infiltration` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The partial aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/fail outcome, frontend, downstream gallery/keykeeper/cabinet/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `gadget_infiltration` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, downstream gallery/keykeeper/cabinet/rune drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/995-daren-hook-partial` was created from `origin/main` at `5a54261` after #994 / PR #1039 had landed.
- Focused baseline before #995 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed 70 / failed 0 / skipped 0 / total 70.
- Affected baseline before #995 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 339 / failed 0 / skipped 0 / total 339.
- Spec Kit prerequisite check before implementation should resolve `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-995-daren-hook-partial\\specs\\995-daren-hook-partial` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #995 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
