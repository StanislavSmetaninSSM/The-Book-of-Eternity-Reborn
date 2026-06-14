# Feature Specification: Daren Silent Gallery Partial Literary Aftermath

**Feature Branch**: `work/998-daren-gallery-partial`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), completed success sibling [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997), future fail sibling [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999), completed downstream result trios [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000)/[#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)/[#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #998 - rewrite result surface `stealth_crossing` / `stealth_crossing_action` / `partial` ("Галерея без звука" mixed outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #972 already rewrote the `stealth_crossing` scene opening as a full literary page. This issue only rewrites the partial post-action outcome.
- **Completed sibling context**: #997 already rewrote the success outcome for the same `stealth_crossing_action`; #998 must preserve that success text unchanged.
- **Future sibling context**: #999 will cover the fail outcome for the same `stealth_crossing_action`; #998 must preserve the current fail string unchanged.
- **Completed downstream context**: #1000/#1001/#1002 completed the next `guard_interrogation_action` result trio, #1003/#1004/#1005 completed the following `lock_pick_action` trio, and #1006/#1007/#1008 completed the following `rune_memory_action` trio; all must remain unchanged.
- **Spec Kit justification**: #998 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `partial` aftermath prose insert for `stealth_crossing_action`, a focused objective guard that fails on the current one-sentence partial text, and local verification evidence.
- **Out of scope**: rewriting the `stealth_crossing` scene opening, success outcome (#997), fail outcome (#999), downstream keykeeper/cabinet/rune-memory outcomes (#1000-#1008), other Daren scenes/results (#988-#996, #1000-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Один страж шевелится от скрипа; сомнение уже тянется к фонарю, но Дарен удерживает тишину до открытых глаз.

## User Scenarios & Testing

### User Story 1 - Partial Gallery Crossing Reads As Costly Stealth Aftermath (Priority: P1)

As a player resolving "Галерея без звука" with a partial result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren forcing the crossing to work despite a real cost, so the route communicates progress with suspicion, trace, delay, or witness-risk rather than a short mixed-success notification.

**Why this priority**: This is the only user-visible value of #998 and continues the `stealth_crossing_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `stealth_crossing_action.Routing.Partial` / partial result text from shared route data, asserting its mixed-outcome literary qualities, and confirming unchanged action contract plus success/fail sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `partial` result for `stealth_crossing_action`, **When** console or browser renders the outcome, **Then** the shared partial text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** partial means the gallery crossing succeeds at a price, **When** the insert is read, **Then** Daren still reaches the service-door/keykeeper continuity, but a floorboard, guard, lantern, journal, dust trace, remembered detail, delay, or similar consequence remains credible for later suspicion.
3. **Given** the existing gallery action contract, **When** route data is inspected, **Then** beat id, title, action id, `StealthNoise` check, Dexterity characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to the dark gallery, floorboards, portrait glass, dust, curtains, sleeping guard/breath, Daren's body control, and the service-door/keykeeper continuity.
- The result must not duplicate #997 clean-success prose. It should be clearly worse than success: passage is achieved, but the gallery keeps a trace, doubt, delayed sound, remembered detail, or future risk.
- The result must not rewrite the fail outcome; #999 remains a separate tracked sibling.
- The result must not imply a full fail/alarm if routing still proceeds to `guard_interrogation`; it may include near-discovery, a stirred guard, a lantern sweep, a note, or suspicion that can matter later.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `stealth_crossing_action` partial text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through controlled movement, breath, hand/boot placement, and real-time judgment of a compromised gallery crossing.
- **FR-003**: The insert MUST reflect the `partial` grade: Daren gets through and route continuity remains, but a concrete cost, trace, suspicion, delay, near-witness, or evidence risk survives.
- **FR-004**: The insert MUST include concrete sensory/stealth details around floorboards or parquet, portrait frames or glass, dust or air, curtains/doors, sleeping guard or lantern presence, Daren's body control, and the corridor/service-door/keykeeper continuity.
- **FR-005**: The insert MUST bridge naturally into the next `guard_interrogation` / "Ключник в галерее" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for gallery surfaces/listening atmosphere, Daren body/breath control, floorboard/noise trouble, partial cost/suspicion/evidence, still-achieved passage, and next keykeeper/service-door continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `StealthNoise` type, Dexterity characteristic, difficulty/config, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.
- **FR-009**: The #997 success outcome and #999 fail outcome MUST remain unchanged unless a test proves a real accidental drift that must be reverted.

### Key Entities

- **Daren gallery action**: `stealth_crossing_action` inside the shared Daren route chapter `stealth_crossing` / "Галерея без звука".
- **Partial result surface**: The mixed outcome string shown after the StealthNoise action resolves as a partial success.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `stealth_crossing` partial text and passes after the rewrite.
- **SC-002**: The partial aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The partial aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/fail outcome, frontend, downstream keykeeper/cabinet/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `stealth_crossing` partial prose and inspect the diff for prohibited implementation terminology, success/fail drift, downstream keykeeper/cabinet/rune drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/998-daren-gallery-partial` was created from `origin/main` at `273c41c` after #997 / PR #1036 had landed.
- Focused baseline before #998 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed 67 / failed 0 / skipped 0 / total 67.
- Affected baseline before #998 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 336 / failed 0 / skipped 0 / total 336.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-998-daren-gallery-partial\\specs\\998-daren-gallery-partial` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #998 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
