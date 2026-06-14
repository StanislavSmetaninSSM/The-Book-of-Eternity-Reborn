# Feature Specification: Daren Silent Gallery Success Literary Aftermath

**Feature Branch**: `work/997-daren-gallery-success`
**Created**: 2026-06-14
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), sibling result follow-ups [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998)/[#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999), completed downstream result trios [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000)/[#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)/[#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #997 - rewrite result surface `stealth_crossing` / `stealth_crossing_action` / `success` ("Галерея без звука" clean outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #972 already rewrote the `stealth_crossing` scene opening as a full literary page. This issue only rewrites the success post-action outcome.
- **Sibling context**: #998 and #999 will cover partial/fail outcomes for the same `stealth_crossing_action`; #997 must preserve both current sibling strings unchanged.
- **Completed downstream context**: #1000/#1001/#1002 completed the next `guard_interrogation_action` result trio, #1003/#1004/#1005 completed the following `lock_pick_action` trio, and #1006/#1007/#1008 completed the following `rune_memory_action` trio; all must remain unchanged.
- **Spec Kit justification**: #997 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `success` aftermath prose insert for `stealth_crossing_action`, a focused objective guard that fails on the current one-sentence success text, and local verification evidence.
- **Out of scope**: rewriting the `stealth_crossing` scene opening, partial/fail outcomes (#998/#999), downstream keykeeper/cabinet/rune-memory outcomes (#1000-#1008), other Daren scenes/results (#988-#996, #1000-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Дарен переводит вес с доски на доску, проходит чисто и не оставляет галерее ни следа, ни проснувшегося дыхания.

## User Scenarios & Testing

### User Story 1 - Successful Gallery Crossing Reads As Clean Stealth Aftermath (Priority: P1)

As a player resolving "Галерея без звука" with a success result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren's controlled movement through the listening gallery, so the route communicates competence, reduced evidence risk, and momentum toward the keykeeper/service-door beat instead of a short success notification.

**Why this priority**: This is the only user-visible value of #997 and begins the `stealth_crossing_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `stealth_crossing_action.Routing.Success` / success result text from shared route data, asserting its clean-outcome literary qualities, and confirming unchanged action contract plus partial/fail sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `success` result for `stealth_crossing_action`, **When** console or browser renders the outcome, **Then** the shared success text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** success is the clean gallery-crossing outcome, **When** the insert is read, **Then** Daren's weight, breath, hands, boots, and attention turn the gallery's floorboards, portraits, glass, dust, and sleeping air into obstacles he passes without waking alarm or leaving evidence.
3. **Given** the existing gallery action contract, **When** route data is inspected, **Then** beat id, title, action id, `StealthNoise` check, Dexterity characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to the dark gallery, floorboards, portrait glass, dust, curtains, sleeping guard/breath, Daren's body control, and the service-door/keykeeper continuity.
- The result must not duplicate the scene opening, future #998 partial prose, future #999 fail prose, or later keykeeper/cabinet/rune prose; it should begin after the successful StealthNoise resolution and carry the route toward `guard_interrogation` / "Ключник в галерее" under reduced suspicion.
- The result must not rewrite partial/fail outcomes; #998 and #999 remain separate tracked siblings.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `stealth_crossing_action` success text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist through controlled weight shifts, breath, hand/boot placement, and judgment of the gallery's listening surfaces.
- **FR-003**: The insert MUST reflect the `success` grade: no alarm wakes, no clear evidence remains, the gallery's silence stays under Daren's control, and Daren gains clean momentum toward the keykeeper/service-door beat.
- **FR-004**: The insert MUST include concrete sensory/stealth details around floorboards, portrait frames or glass, dust, curtains/doors, sleeping breath or guard presence, Daren's body control, and the corridor/service-door continuity.
- **FR-005**: The insert MUST bridge naturally into the next `guard_interrogation` / "Ключник в галерее" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for gallery surfaces/listening atmosphere, Daren body/breath control, floorboard/noise avoidance, absence of alarm/evidence, clean passage/momentum, and next keykeeper/service-door continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `StealthNoise` type, Dexterity characteristic, difficulty/config, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.

### Key Entities

- **Daren gallery action**: `stealth_crossing_action` inside the shared Daren route chapter `stealth_crossing` / "Галерея без звука".
- **Success result surface**: The clean outcome string shown after the StealthNoise action resolves as a success.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `stealth_crossing` success text and passes after the rewrite.
- **SC-002**: The success aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The success aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, partial/fail outcome, frontend, downstream keykeeper/cabinet/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `stealth_crossing` success prose and inspect the diff for prohibited implementation terminology, partial/fail drift, downstream keykeeper/cabinet/rune drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/997-daren-gallery-success` was created from `origin/main` at `e7e2f4b` after #1002 / PR #1035 had landed.
- Focused baseline before #997 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 66 / failed 0 / skipped 0 / total 66.
- Affected baseline before #997 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 335 / failed 0 / skipped 0 / total 335.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-997-daren-gallery-success\\specs\\997-daren-gallery-success` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #997 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
