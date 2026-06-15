# Feature Specification: Daren Scene 15 Full Literary Page

**Feature Branch**: `work/983-daren-hideout-return`
**Created**: 2026-06-16
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#983](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/983), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), previous scene [#982](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/982), result/aftermath follow-ups #988-#1014 remain out of scope.

## Source Issues & Scope

- **Source GitHub issue**: #983 - rewrite scene `hideout_return` / "Убежище под мостом" as a full Russian dark-fantasy literary page.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #983 changes player-facing story/UX content over shared console/browser QTE route data. It is the next open per-scene Daren literary-page child after #982, needs durable handoff, and must preserve console/browser parity through shared C# route data without drifting mechanics, rewards, endpoints, runtime state, or sibling scene scope.
- **Contract scope**: player-facing, console, browser, shared route data, C# source guard tests. No GM-facing prompt, docs, examples, validation, runtime-state, or frontend contract change is intended because this scene is client-owned authored showcase prose and does not add a GM-authored capability.
- **In scope**: one substantial Russian prose page for `hideout_return`, a focused objective guard that fails on synopsis-length copy, and local verification evidence.
- **Out of scope**: rewriting result/aftermath issues #988-#1014, changing already-merged #969-#982 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, inventing a separate route branch, or adding browser-only/console-only story forks.

## Current Main Text

> Под мостом Дарен вжимается в своё убежище, где вода глушит шаги, а тайник ждёт посох под мокрым камнем. Теперь нужно спрятать добычу и зачистить след: если капитан Орвальд доведёт погоню до этого края, ночь станет опасной даже после кражи.

## User Scenarios & Testing

### User Story 1 - Hideout Return Reads As a Page (Priority: P1)

As a player reading Daren's QTE showcase, I want the "Убежище под мостом" beat to feel like a sustained dark-fantasy hideout scene, so I experience Daren reaching the bridge, reading the water and stones, hiding the stolen staff, erasing the trace of pursuit, and deciding how cleanly the heist can end.

**Why this priority**: This is the only user-visible value of #983 and continues the parent #955 goal of replacing synopsis beats with interactive-book prose.

**Independent Test**: The scene can be tested independently by reading `hideout_return` from the shared route data and verifying authored prose plus unchanged terminal action contract.

**Acceptance Scenarios**:

1. **Given** the player reaches `hideout_return`, **When** the scene text is rendered by console or browser, **Then** the text is a substantial Russian literary page centered on Daren entering and securing the under-bridge hideout rather than a one/two-sentence summary.
2. **Given** the previous `chase_chain` beat, **When** the scene begins, **Then** the prose carries forward the courtyard chain, bridgeward water, Orvald's pursuit pressure, the stolen staff/futlyar weight, and the risk that traces can still lead to the hideout.
3. **Given** the existing hideout action beat, **When** route data is inspected, **Then** beat id/title, action id, `BranchChoice` check, characteristic, difficulty/config, terminal routing, score deltas, and rewards remain unchanged.

### Edge Cases

- Captain Orvald Shpil, guards, dogs, lanterns, voices, and water/stone trace pressure may appear as pursuit pressure, but the scene must not add a dialogue runtime or branch state.
- Daren remains the active point-of-view protagonist even when the prose names pursuers or recalls earlier witnesses.
- The prose must remain player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE` terms in default narrative.
- The page must stay within the existing broad Daren narrative length guard with safe CRLF margin while still being substantial enough to reject synopsis-length copy; target roughly 1800-3400 runtime characters, not the previous #982 3600 boundary.
- Console and browser must continue to consume the same shared route text through `DarenShowcaseBeat.PlayerText` and `QteChapter.Narrative`.

## Requirements

### Functional Requirements

- **FR-001**: `hideout_return` MUST be a substantial Russian dark-fantasy literary page rather than a one/two-sentence synopsis.
- **FR-002**: The scene MUST keep Daren as the active point-of-view protagonist through breath, hands, shoulders, balance, listening, and deliberate cleanup choices.
- **FR-003**: The scene MUST include the under-bridge hideout environment: bridge/arches, water, wet stone, low shelter, hidden cache or stone, and the sound that masks or reveals steps.
- **FR-004**: The scene MUST carry forward prior route and theft pressure: courtyard-chain arrival, pursuit from Orvald/guards/dogs/lanterns, stolen staff/futlyar balance, and the possibility of readable mud, water, footprints, fabric, blood, or other traces.
- **FR-005**: The scene MUST show Daren hiding/sealing the staff and deciding how to erase or misdirect the last traces before pursuit reaches the bridge.
- **FR-006**: The scene MUST naturally narrow into the existing action label `Спрятать посох и зачистить след` and the `BranchChoice` stakes of clean hideout safety versus an exposed cache.
- **FR-007**: The focused test guard MUST use grouped motif checks, including bridge/water/stone/hideout/cache; Daren breath/body/hand control; stolen staff/futlyar/sealing; pursuit/Orvald/guards/dogs/lantern/voices; trace/mud/footprint/blood/evidence cleanup; and a natural hide/clean action lead-in.
- **FR-008**: The implementation MUST preserve route order, beat id, title, action id, action label, `BranchChoice` type, `Characteristics.Wisdom`, difficulty `3`, `DarenBranchChoiceConfig("success")`, terminal routing to `daren_hideout_return`, score deltas from `DarenScoreDeltas(hideout: 6, evidence: -3)`, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-009**: Default player-facing prose MUST NOT expose implementation or agent terminology.

### Key Entities

- **Daren hideout-return beat**: The shared authored scene `hideout_return` / "Убежище под мостом" presented to both console and browser players.
- **Existing QTE action contract**: The unchanged `hideout_return_action` contract that controls the existing wisdom-based `BranchChoice` terminal action and final route outcome.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The new focused `DarenQteShowcaseTests` guard fails against the current synopsis and passes after the scene is rewritten.
- **SC-002**: The `hideout_return` narrative is at least 1500 characters, has at least 12 scene sentences, and mentions Daren at least 5 times.
- **SC-003**: The scene satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, terminal outcome, or frontend drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `hideout_return` prose and inspect the diff for prohibited implementation terminology and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/983-daren-hideout-return` was created from `origin/main` at `1759348f1cf6095da2288816ec7bf2724776d10f`.
- Baseline focused Daren tests passed: 82 passed / 0 failed / 0 skipped / 82 total.
- Baseline affected Daren/QTE/docs/browser slice passed: 351 passed / 0 failed / 0 skipped / 351 total.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #983 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from the shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
