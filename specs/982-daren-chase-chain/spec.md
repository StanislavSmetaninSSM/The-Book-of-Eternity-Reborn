# Feature Specification: Daren Scene 14 Full Literary Page

**Feature Branch**: `work/982-daren-chase-chain`
**Created**: 2026-06-16
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#982](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/982), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), previous scene [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981), next scene [#983](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/983)

## Source Issues & Scope

- **Source GitHub issue**: #982 - rewrite scene `chase_chain` / "Цепочка дворов" as a full Russian dark-fantasy literary page.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #982 changes player-facing story/UX content over shared console/browser QTE route data. It is the next open per-scene Daren literary-page child after #981, needs durable handoff, and must preserve console/browser parity through shared C# route data without drifting mechanics, rewards, endpoints, runtime state, or sibling scene scope.
- **Contract scope**: player-facing, console, browser, shared route data, C# source guard tests. No GM-facing prompt, docs, examples, validation, runtime-state, or frontend contract change is intended because this scene is client-owned authored showcase prose and does not add a GM-authored capability.
- **In scope**: one substantial Russian prose page for `chase_chain`, a focused objective guard that fails on synopsis-length copy, and local verification evidence.
- **Out of scope**: rewriting scene #983 or result/aftermath issues #988-#1014, changing already-merged #969-#981 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, inventing a separate route branch, or adding browser-only/console-only story forks.

## Current Main Text

> Дарен несётся от выбранного в оранжерее выхода через задний двор, низкую стену, телегу и тёмную аллею, вспоминая маршрут как цепочку ударов сердца. Каждый прыжок и поворот должен сбить преследователей со следа, иначе погоня прочитает всю дорогу к мосту.

## User Scenarios & Testing

### User Story 1 - Courtyard Chain Reads As a Page (Priority: P1)

As a player reading Daren's QTE showcase, I want the "Цепочка дворов" beat to feel like a sustained dark-fantasy chase sequence, so I experience Daren chaining the chosen orangerie escape, rear yard, low wall, cart, alley, mud, lanterns, voices, and bridgeward route into one remembered sequence that can shake or strengthen pursuit.

**Why this priority**: This is the only user-visible value of #982 and continues the parent #955 goal of replacing synopsis beats with interactive-book prose.

**Independent Test**: The scene can be tested independently by reading `chase_chain` from the shared route data and verifying authored prose plus unchanged action contract.

**Acceptance Scenarios**:

1. **Given** the player reaches `chase_chain`, **When** the scene text is rendered by console or browser, **Then** the text is a substantial Russian literary page centered on Daren threading a chain of courtyards rather than a one/two-sentence summary.
2. **Given** the prior `route_decision` and `pursuit` beats, **When** the scene begins, **Then** the prose carries forward the chosen orangerie/yard route, first-dash pursuit pressure, the stolen staff/futlyar, and the risk of leaving readable traces.
3. **Given** the existing courtyard-chain action beat, **When** route data is inspected, **Then** beat id/title, action id, `PromptChain` check, characteristic, difficulty, routing, score deltas, and rewards remain unchanged.

### Edge Cases

- Captain Orvald Shpil, guards, lanterns, voices, mud, cart noise, and witness pressure may appear as pursuit/trail pressure, but the scene must not add a dialogue runtime or branch state.
- Daren remains the active point-of-view protagonist even when the prose names pursuers or recalls earlier NPC pressure.
- The prose must remain player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE` terms in default narrative.
- The page must stay within the existing broad Daren narrative length guard while still being substantial enough to reject synopsis-length copy.
- Console and browser must continue to consume the same shared route text through `DarenShowcaseBeat.PlayerText` and `QteChapter.Narrative`.

## Requirements

### Functional Requirements

- **FR-001**: `chase_chain` MUST be a substantial Russian dark-fantasy literary page rather than a one/two-sentence synopsis.
- **FR-002**: The scene MUST keep Daren as the active point-of-view protagonist through observation, intent, breath, legs, hands, balance, memory, and route control.
- **FR-003**: The scene MUST include the physical route chain: rear courtyard, low wall, cart or wagon, dark alley, wet stone/mud, lanterns or guard lines, and the bridgeward escape direction.
- **FR-004**: The scene MUST carry forward prior route and theft pressure: the orangerie/servant-gate/arch choice context where relevant, the first-dash pursuit, the stolen staff/futlyar balance, and trace/noise/evidence risk.
- **FR-005**: The scene MUST include pursuit pressure from Captain Orvald Shpil, guards, voices, lanterns, dogs, or other visible trackers without making them the point-of-view characters.
- **FR-006**: The scene MUST make sequence-memory stakes clear: Daren must repeat the exact courtyard chain, jumps, turns, and timing before pursuit reads his whole route to the bridge.
- **FR-007**: The focused test guard MUST use grouped motif checks, including courtyard/wall/cart/alley; Daren breath/body/step rhythm; orangerie/route memory; stolen staff/futlyar/balance/noise; pursuit/Orvald/guards/lantern/voices; trace/mud/footprint evidence; and a natural `PromptChain`/sequence lead-in.
- **FR-008**: The implementation MUST preserve route order, beat id, title, action id, action label, `PromptChain` type, characteristic, difficulty, routing target to `hideout_return`, score deltas from `DarenScoreDeltas(pursuit: 4, evidence: -2)`, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-009**: Default player-facing prose MUST NOT expose implementation or agent terminology.

### Key Entities

- **Daren courtyard-chain beat**: The shared authored scene `chase_chain` / "Цепочка дворов" presented to both console and browser players.
- **Existing QTE action contract**: The unchanged `chase_chain_action` contract that controls the existing speed-based `PromptChain` action and route progression to `hideout_return`.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The new focused `DarenQteShowcaseTests` guard fails against the current synopsis and passes after the scene is rewritten.
- **SC-002**: The `chase_chain` narrative is at least 1500 characters, has at least 12 scene sentences, and mentions Daren at least 5 times.
- **SC-003**: The scene satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, or frontend drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `chase_chain` prose and inspect the diff for prohibited implementation terminology and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/982-daren-chase-chain` was created from `origin/main` at `057b484`.
- Baseline focused Daren tests passed: 81 passed / 0 failed / 0 skipped / 81 total.
- Baseline affected Daren/QTE/docs/browser slice passed: 350 passed / 0 failed / 0 skipped / 350 total.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #982 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from the shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
