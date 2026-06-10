# Feature Specification: Daren QTE Training Showcase

**Feature Branch**: `work/919-daren-qte-training`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Source Issues**: [#919 QTE training mode: ограбление поместья вором Дареном](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919), parent [#911 QTE v2](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911), prerequisites [#918 Browser QTE parity](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918), [#924 QTE scoring](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/924), [#925 QTE Practice Mode](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925)

## Source Issues & Scope

- **Source GitHub issue(s)**: #919 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919>
- **Issue type**: player-facing feature, QTE showcase/training, persistent profile reward.
- **Spec Kit justification**: #919 spans authored mini-adventure content, all QTE v1/v2 mechanics, console and browser surfaces, permanent profile achievement state, New Game Ink Feather reward grants, validation/normalizer hardening, GM-facing QTE documentation, examples, and multi-session handoff. It is contract-sensitive and must be durable.
- **Contract scope**: player-facing console/browser UX, QTE runtime, QTE scoring, persistent client profile, New Game initialization, validation/normalizer, docs, examples, GM QTE guidance.
- **Out of scope**: new QTE check types, ordinary campaign GM-authored Daren scenes, external-IP lore, cloud/profile sync, afterlife pending/control files, and reward grants from reincarnation or in-session lifecycle flows.

## User Scenarios & Testing

### Scenario 1 - Player launches a standalone Daren showcase (Priority: P1)

A player can open a clearly labeled QTE showcase/training mode about the thief Daren without creating, loading, advancing, repairing, or corrupting an ordinary campaign session. The mode is client-owned authored content, not a GM turn, and it works from the console client and browser client.

**Why this priority**: The feature is only safe if it is reachable and isolated from normal campaigns.

**Independent Test**: Add console/menu and browser route/API tests that launch the Daren showcase from a no-campaign state and from an existing campaign state, then assert no ordinary `game_state`, pending action, GM turn, chat log turn, inventory, quest, XP, afterlife state, or practice-mode-only state is mutated before a completed showcase ending writes the permanent profile reward.

**Acceptance Scenarios**:
1. **Given** no active campaign session, **When** the player opens Daren showcase, **Then** the Daren route menu/story intro appears and no campaign state is created.
2. **Given** an existing campaign session, **When** the player opens and exits Daren showcase before an ending, **Then** the session files are unchanged and the player returns to the previous client state.
3. **Given** the player sees the entry point, **Then** the copy says this is a separate QTE showcase/training mini-game with permanent rewards only after valid completion.

---

### Scenario 2 - Player completes a strictly authored 20-30 minute mini-adventure (Priority: P1)

The showcase presents original in-project content: Daren is a cunning medieval thief who infiltrates a locked manor, steals a magical staff, escapes pursuit, and returns to his hideout. The route is authored enough to demonstrate mechanics reliably while still offering local choices that influence ending score.

**Why this priority**: The issue asks for a small game inside the client, not a loose QTE catalog.

**Independent Test**: Add deterministic route tests that step through every required beat without waiting for wall-clock time. Tests must prove that all required QTE types are reachable through the intended route graph and that multiple route choices affect score/endings.

**Acceptance Scenarios**:
1. **Given** the player starts Daren showcase, **When** the route begins, **Then** the story starts with patrol pressure around a locked manor and uses original project names/copy.
2. **Given** the player progresses through the route, **When** the scenario reaches infiltration, theft, pursuit, and hideout return beats, **Then** each beat offers authored choices and QTE actions rather than freeform GM prompts.
3. **Given** the player completes the route with valid inputs, **Then** the completion summary names the ending tier, key route outcomes, QTE performance, stealth/noise quality, loot condition, pursuit result, and hideout safety.

---

### Scenario 3 - The route exercises the complete QTE mechanic set (Priority: P1)

Daren showcase must use all existing QTE v1 mechanics and all available QTE v2 mechanics where practical: `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, `BranchChoice`, `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, and `LockPinSet`. Browser interactions reuse #918 mini-game parity; scoring/ranks reuse #924; practice mode #925 remains separate.

**Why this priority**: The showcase exists to prove the QTE engine can support a long cinematic route.

**Independent Test**: Add route-coverage tests that assert every required QTE type appears at least once in the Daren route definition, that each action can resolve deterministically to `success`, `partial`, or `fail`, and that console/browser surfaces expose equivalent player-facing affordances.

**Acceptance Scenarios**:
1. **Given** the route definition, **When** tests enumerate Daren actions, **Then** every required QTE type appears in a meaningful story beat.
2. **Given** a browser Daren action, **When** the player interacts with it, **Then** React handles presentation/local mini-game input and C# remains authoritative for route state, grade acceptance, scoring, and reward writes.
3. **Given** the player uses RU or EN keyboard layouts, **When** Daren QTE prompts need physical keys, **Then** the existing layout-independent QTE input helpers and labels apply only to QTE surfaces.

---

### Scenario 4 - Endings, thresholds, and permanent rewards are deterministic (Priority: P1)

The showcase stores only the best achieved ending tier. Worse or equal replays never downgrade or duplicate the permanent reward. The player can fail before a valid ending; that records no permanent reward.

**Why this priority**: The issue requires exact ending names, thresholds, reward amounts, upgrade-only semantics, and no reward duplication.

**Independent Test**: Add scoring tests for all ending boundaries and profile update tests for first completion, better replay, same-tier replay, worse replay, and failed pre-ending attempts.

**Acceptance Scenarios**:
1. **Given** a completion score below a valid ending threshold or a route failure before safe escape, **When** the attempt ends, **Then** no permanent reward achievement is written.
2. **Given** the player earns `Тень в бегах`, **When** the profile has no Daren record, **Then** the permanent Daren achievement stores that tier and a +1 New Game Ink Feather bonus.
3. **Given** the player later earns `Чистая кража` or `Идеальная тень`, **When** the stored tier is lower, **Then** the permanent profile upgrades to the better tier and never stacks lower-tier bonuses.
4. **Given** the player replays and earns the same or worse tier, **When** the profile is updated, **Then** the stored best tier and reward amount remain unchanged.

---

### Scenario 5 - New Game applies the best Daren bonus exactly once per new session (Priority: P1)

When a new game/session is created, the client reads the permanent Daren reward profile and grants the configured Ink Feather bonus to the newly initialized soul state exactly once for that session. Reincarnations, life starts within an existing session, afterlife transitions, save loads, QTE practice, and ordinary campaign turns do not grant the bonus.

**Why this priority**: The permanent reward is the feature's long-term value and its main corruption risk.

**Independent Test**: Add New Game initialization tests that prove the best-tier bonus appears in the newly created `soul_state` once, the player-facing New Game/start summary names the Daren ending tier, subsequent save loads do not regrant, and reincarnation/in-session lifecycle flows do not regrant.

**Acceptance Scenarios**:
1. **Given** the permanent Daren profile stores `Чистая кража`, **When** a new game is created, **Then** the new soul starts with +4 Ink Feathers and visible copy explains the Daren tier source.
2. **Given** the same session is loaded or repaired, **When** client state refreshes, **Then** the Daren reward is not applied again.
3. **Given** a reincarnation or life start inside an existing session, **When** lifecycle processing runs, **Then** the Daren reward is not applied.
4. **Given** the permanent profile is corrupt, missing, duplicated, or attempts to downgrade, **When** validation/normalization runs, **Then** the profile is repaired or rejected without granting duplicate rewards.

---

### Scenario 6 - Docs, examples, and player copy preserve boundaries (Priority: P2)

Documentation and player help explain that Daren showcase is client-owned authored training content, not a normal GM-authored QTE offer. GM-facing QTE docs/examples explain how standard campaign QTE offers remain authored by the GM and how the Daren route differs.

**Why this priority**: GM prompts and examples are product behavior for this repo.

**Independent Test**: Documentation/source guard tests assert that `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and relevant command/help surfaces mention Daren showcase boundaries, permanent reward behavior, and no ordinary campaign mutation.

**Acceptance Scenarios**:
1. **Given** a GM reads QTE guidance, **When** they see Daren showcase references, **Then** the docs make clear that Daren is client-owned training content and not a GM-authored campaign scene.
2. **Given** a player sees showcase or New Game reward copy, **When** the copy renders, **Then** it uses Russian in-world/player-facing terms and no DTO/API/debug/manual-grade language.

## Edge Cases

- Player exits the route before a valid ending: no permanent reward, no campaign mutation, route state cleared or resumable only if explicitly local and safe.
- Player fails during pursuit but Daren survives: award `Тень в бегах` only when the route reaches a valid ending node; otherwise no reward.
- Player earns a better tier after previously starting several new games: future new games receive the improved bonus; past sessions are not retroactively changed.
- Permanent reward profile has duplicate Daren records: normalize to the single best tier and the highest valid bonus.
- Permanent reward profile has an unknown tier or negative bonus: reject or repair to no Daren reward and report a player-facing safe reason.
- New Game is interrupted after soul state write: idempotency marker in the new session prevents a second grant on retry/repair.
- Browser route refresh during Daren showcase: restore only safe local route state or return to showcase menu without touching campaign state.
- QTE mini-game input uses Cyrillic fallback keys: normalize only in Daren/QTE input surfaces, not command composer or chat text.

## Requirements

### Functional Requirements

- **FR-001**: The client MUST expose a standalone Daren QTE showcase/training entry point in console and browser without requiring a normal campaign session.
- **FR-002**: Daren showcase MUST be clearly labeled as separate authored training/showcase content and MUST not advance or mutate ordinary campaign state before a valid ending reward write.
- **FR-003**: The route MUST use original project content: Daren, his thief tools, the manor, magical staff, pursuit, and hideout must not reference external IP.
- **FR-004**: The route MUST include authored beats for approach under patrol pressure, gadget infiltration, stealth crossing, `LockPinSet`, `PatternMemory`, `MashInput`, `RhythmPulse` or `TimingBar`, `PrecisionChoice`, staff theft, pursuit, chase chain, and hideout return.
- **FR-005**: The route MUST exercise all implemented QTE types: `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, `BranchChoice`, `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, and `LockPinSet`.
- **FR-006**: Daren QTE actions MUST reuse existing QTE validation, layout-independent input, browser mini-game parity, scoring/rank, and C# authority paths rather than duplicating gameplay resolution in React.
- **FR-007**: The showcase MUST support meaningful local choices that affect score, ending tier, stealth/evidence quality, loot/staff condition, pursuit result, and hideout safety.
- **FR-008**: The implementation MUST define exactly these ending tiers and New Game Ink Feather bonuses: `Тень в бегах` (+1), `Сорванный след` (+2), `Чистая кража` (+4), `Идеальная тень` (+6).
- **FR-009**: The route MUST define deterministic score thresholds for each ending tier and deterministic no-reward conditions for failure before a valid ending.
- **FR-010**: Successful completion MUST write a persistent client/profile Daren achievement outside ordinary `game_state` so New Game clearing does not delete it.
- **FR-011**: The persistent Daren profile MUST store the best achieved tier, reward amount, completion timestamp, route score evidence, and a schema/version marker.
- **FR-012**: Replaying with a better tier MUST upgrade the stored tier; replaying with the same or worse tier MUST NOT downgrade, duplicate, or stack rewards.
- **FR-013**: New Game initialization MUST grant the best-tier Daren Ink Feather bonus exactly once per newly created game/session and show player-facing copy naming the Daren ending tier.
- **FR-014**: Reincarnations, life starts inside an existing session, afterlife transitions, save loads, repairs, ordinary turns, and QTE Practice Mode MUST NOT grant the Daren bonus.
- **FR-015**: Validation/normalizer or equivalent profile checks MUST prevent corrupt, duplicated, unknown-tier, negative-bonus, and downgrade Daren reward states from granting invalid rewards.
- **FR-016**: Default player UI MUST NOT expose raw endpoint, DTO, JSON, file-path, debug, manual-grade, agent, or workflow language.
- **FR-017**: GM-facing QTE docs/examples and source guards MUST explain how Daren showcase differs from normal GM-authored QTE offers.
- **FR-018**: Console and browser Daren surfaces MUST be semantically aligned, though their layout may differ.

### Key Entities

- **Daren Showcase Route**: Client-owned authored route graph containing story beats, choices, QTE actions, scoring weights, and ending nodes.
- **Daren Route Attempt**: Ephemeral local state for one run of the showcase, including current beat, route choices, QTE outcomes, score metrics, and completion/failure state.
- **Daren Ending Tier**: One of `Тень в бегах`, `Сорванный след`, `Чистая кража`, or `Идеальная тень`, each mapped to an exact Ink Feather bonus.
- **Permanent Daren Reward Profile**: Client-owned profile record outside ordinary game state that stores the best Daren tier and survives New Game clearing.
- **New Game Daren Reward Grant**: Idempotent per-session application of the stored best-tier Ink Feather bonus during New Game initialization, with player-facing source copy.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Route-coverage tests prove every required QTE type is reachable in Daren showcase.
- **SC-002**: Deterministic completion tests prove all four ending tiers and no-reward failure can be reached.
- **SC-003**: Persistent profile tests prove first completion, upgrade, same-tier replay, worse replay, and corrupt profile handling.
- **SC-004**: New Game tests prove the Daren bonus grants exactly once per new session and never from reincarnation/save-load/in-session lifecycle flows.
- **SC-005**: Console and browser tests prove player-facing Daren entry, route progress, result, and New Game reward copy are available without raw debug/API language.
- **SC-006**: Docs/source guard tests prove QTE GM docs/examples explain Daren showcase boundaries.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|Daren|NewGame|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: focused documentation/source guard tests covering `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, Daren reward/profile boundaries, and New Game reward copy.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` plus focused Vitest/TypeScript tests for Daren showcase route/mini-game surfaces and player-facing copy.
- **Manual/player-facing verification**: console and browser smoke through the Daren showcase menu/start/completion summary when feasible; if full 20-30 minute manual route is not feasible, produce deterministic route-playthrough evidence from tests and at least one browser/console visual/smoke artifact.

## Assumptions

- Existing #918 browser QTE mini-games, #924 scoring/ranks, #925 practice-mode isolation, and #920 layout-independent QTE input are available on `main` and should be reused.
- The first implementation may represent the 20-30 minute route through deterministic authored chapters/actions rather than real-time wall-clock pacing; tests must avoid wall-clock sleeps.
- The permanent Daren reward profile should live outside `game_session/game_state`, with `client_profile/qte_showcase_rewards.json` as the default path unless an existing client-profile convention is discovered during implementation.
- New Game currently initializes the soul in Chaos Sea; the Daren reward should apply during that initialization and must be marked in the new session so repair/retry does not regrant.
- Daren showcase is client-owned authored content. GM docs must mention it, but ordinary campaign GMs do not author or resolve Daren route attempts.
