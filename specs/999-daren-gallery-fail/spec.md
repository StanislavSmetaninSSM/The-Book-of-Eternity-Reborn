# Feature Specification: Daren Silent Gallery Fail Literary Aftermath

**Feature Branch**: `work/999-daren-gallery-fail`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), completed siblings [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997) and [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998), completed downstream result trios [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000)/[#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)/[#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Source Issues & Scope

- **Source GitHub issue**: #999 - rewrite result surface `stealth_crossing` / `stealth_crossing_action` / `fail` ("Галерея без звука" dangerous outcome) as a substantial Russian literary aftermath insert.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book across scene -> QTE tension -> outcome prose -> next scene.
- **Scene prerequisite**: #972 already rewrote the `stealth_crossing` scene opening as a full literary page. This issue only rewrites the fail post-action outcome.
- **Completed sibling context**: #997 already rewrote the success outcome and #998 already rewrote the partial outcome for the same `stealth_crossing_action`; #999 must preserve both texts unchanged.
- **Completed downstream context**: #1000/#1001/#1002 completed the next `guard_interrogation_action` result trio, #1003/#1004/#1005 completed the following `lock_pick_action` trio, and #1006/#1007/#1008 completed the following `rune_memory_action` trio; all must remain unchanged.
- **Spec Kit justification**: #999 changes player-facing story/UX copy shared by console and browser, preserves route mechanics and sibling surfaces, and needs durable handoff evidence for the ongoing #955 Daren content chain.
- **Contract scope**: client-owned authored showcase prose in shared C# Daren route data. No GM-authored capability, validation rule, state file, pending/control file, endpoint, frontend contract, or runtime-state change is intended.
- **In scope**: one substantial Russian `fail` aftermath prose insert for `stealth_crossing_action`, a focused objective guard that fails on the current one-sentence fail text, and local verification evidence.
- **Out of scope**: rewriting the `stealth_crossing` scene opening, success outcome (#997), partial outcome (#998), downstream keykeeper/cabinet/rune-memory outcomes (#1000-#1008), other Daren scenes/results (#988-#996, #1000-#1014), parent #955 closure, QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, new dialogue runtime/state/endpoint, browser-only/console-only forks, GM-facing docs/examples.

## Current Main Text

> Доска отвечает резким треском, и Дарен видит, как в дальнем крыле поднимается тревожный фонарь со свидетелем.

## User Scenarios & Testing

### User Story 1 - Fail Gallery Crossing Reads As Dangerous Stealth Aftermath (Priority: P1)

As a player resolving "Галерея без звука" with a fail result, I want the outcome to read like a dark-fantasy aftermath page centered on Daren losing the gallery's silence and leaving concrete evidence/witness/pursuit pressure, so the route communicates a dangerous failure rather than a short mechanical penalty.

**Why this priority**: This is the only user-visible value of #999 and completes the `stealth_crossing_action` result trio under parent #955's corrected quality bar for post-QTE outcome prose.

**Independent Test**: The result can be tested independently by reading `stealth_crossing_action.Routing.Fail` / fail result text from shared route data, asserting its dangerous-outcome literary qualities, and confirming unchanged action contract plus success/partial sibling surfaces.

**Acceptance Scenarios**:

1. **Given** the player earns the `fail` result for `stealth_crossing_action`, **When** console or browser renders the outcome, **Then** the shared fail text is a substantial Russian literary aftermath insert rather than one terse sentence.
2. **Given** fail means the gallery crossing goes badly, **When** the insert is read, **Then** a floorboard/noise mistake, guard/fonar/witness reaction, evidence trace, and pursuit/alarm escalation become concrete while Daren still moves into the next route beat.
3. **Given** the existing gallery action contract, **When** route data is inspected, **Then** beat id, title, action id, `StealthNoise` check, Dexterity characteristic, difficulty/config, routing destinations, success/partial/fail grade identities, score deltas, reward/profile behavior, and browser/console shared authority remain unchanged.

### Edge Cases

- The result must not copy the Mira quality example from the issue; it should match that level of scene construction while staying specific to the dark gallery, floorboards, portrait glass, dust, curtains, sleeping guard/fonar, Daren's body control, and the service-door/keykeeper continuity under pressure.
- The result must not duplicate #997 clean-success prose or #998 mixed-partial prose. It should be clearly worse: silence breaks, evidence or witness pressure rises, and the gallery gives the house a usable trail.
- The result must not change routing even though the fail is dangerous; it may escalate alarm, witness memory, light, noise, or pursuit pressure while still bridging toward `guard_interrogation`.
- The text must stay player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in default prose.
- Console and browser must continue to consume the same shared result text from C# route data.

## Requirements

### Functional Requirements

- **FR-001**: `stealth_crossing_action` fail text MUST be a substantial Russian dark-fantasy aftermath insert rather than a one-sentence result notification.
- **FR-002**: The insert MUST keep Daren as the active point-of-view protagonist while showing body control failing under a noise/exposure mistake.
- **FR-003**: The insert MUST reflect the `fail` grade: the gallery crossing becomes dangerous, concrete evidence/witness/noise/pursuit pressure escalates, and any continued route movement happens under alarm or near-alarm consequences.
- **FR-004**: The insert MUST include concrete sensory/stealth details around floorboards or parquet, portrait frames or glass, dust or air, curtains/doors, sleeping guard or lantern/fonar presence, Daren's body control, and the corridor/service-door/keykeeper continuity under pressure.
- **FR-005**: The insert MUST bridge naturally into the next `guard_interrogation` / "Ключник в галерее" beat without changing that scene text or route ordering.
- **FR-006**: The focused test guard MUST use grouped motif checks for gallery surfaces/listening atmosphere, Daren body/breath control, floorboard/noise break, dangerous fail consequences, witness/alarm/pursuit/evidence pressure, and next keykeeper/service-door continuity.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `StealthNoise` type, Dexterity characteristic, difficulty/config, routing targets, success/partial/fail identities, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing result prose MUST NOT expose implementation, agent, or mechanic/debug terminology.
- **FR-009**: The #997 success outcome, #998 partial outcome, and downstream #1000-#1008 result surfaces MUST remain unchanged unless a test proves a real accidental drift that must be reverted.

### Key Entities

- **Daren gallery action**: `stealth_crossing_action` inside the shared Daren route chapter `stealth_crossing` / "Галерея без звука".
- **Fail result surface**: The dangerous outcome string shown after the StealthNoise action resolves as a fail.
- **Existing QTE action contract**: The unchanged route/action/check/config/routing/scoring/reward data surrounding the result text.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A new focused `DarenQteShowcaseTests` guard fails against the current one-sentence `stealth_crossing` fail text and passes after the rewrite.
- **SC-002**: The fail aftermath is at least 900 characters, has at least 8 scene sentences, and mentions Daren at least 4 times.
- **SC-003**: The fail aftermath satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, success/partial outcome, frontend, downstream keykeeper/cabinet/rune-memory, or GM-facing contract drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React/browser files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `stealth_crossing` fail prose and inspect the diff for prohibited implementation terminology, success/partial drift, downstream keykeeper/cabinet/rune drift, and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/999-daren-gallery-fail` was created from `origin/main` at `dd2be29` after #998 / PR #1037 had landed.
- Focused baseline before #999 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed 68 / failed 0 / skipped 0 / total 68.
- Affected baseline before #999 implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 337 / failed 0 / skipped 0 / total 337.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-999-daren-gallery-fail\\specs\\999-daren-gallery-fail` with `contracts/` and `tasks.md`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #999 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
