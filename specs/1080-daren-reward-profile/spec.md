# Feature Specification: Daren Reward Profile Presentation

**Feature Branch**: `codex/1080-daren-reward-profile`

**Created**: 2026-06-17

**Status**: Draft

**Input**: GitHub issue #1080 asks to finish the Daren QTE showcase polish by making the final reward/profile data readable and explanatory for players, without changing route mechanics or reward semantics.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1080 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1080
- **Issue type**: task / player-facing UX polish
- **Spec Kit justification**: The issue changes player-facing copy and console/browser parity for a reward/profile surface, so it meets the repository Spec Kit policy.
- **Contract scope**: player-facing, console, browser, frontend, runtime-state projection. The Daren showcase remains client-owned and is not a GM-authored QTE contract.
- **Out of scope**: Do not change Daren route ids, action ids, QTE check types, scoring thresholds, reward tiers, profile persistence semantics, New Game grant semantics, GM prompts, afterlife contracts, validation rules, or ordinary campaign state.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the Permanent Daren Reward (Priority: P1)

After finishing or replaying the Daren showcase, the player can understand what happened, which permanent tier is stored, how many Чернильные Перья future New Game receives, and why a weaker replay does not downgrade or stack rewards.

**Why this priority**: This is the unfinished acceptance criterion in #1080. Without it, the showcase can technically save the reward while still presenting the result as a short pile of fields.

**Independent Test**: Complete or simulate a Daren showcase outcome and inspect the shared C# result, browser DTO, and browser rendering. The player-facing text must explain current outcome, stored best tier, Ink Feather count, New Game timing, and non-downgrade/non-stacking behavior without raw ids or debug terms.

**Acceptance Scenarios**:

1. **Given** the player completes Daren with a first reward, **When** the final result is shown, **Then** the player sees an explanatory profile summary naming the stored tier, score, Ink Feather bonus, future New Game timing, and that the reward is permanent.
2. **Given** the profile already stores a better Daren result, **When** the player completes a weaker replay, **Then** the player sees that the lower outcome happened now but the saved future reward remains the better tier and does not stack.
3. **Given** the player opens the browser Daren showcase with an existing best reward, **When** the intro/best reward block renders, **Then** it uses the shared player-facing profile summary rather than assembling a terse field dump in the frontend.

### Edge Cases

- If no Daren profile exists, the UI keeps the existing reward notice and does not invent a stored tier.
- If the stored profile is malformed, existing normalization keeps only a valid known tier; invalid profiles remain absent.
- If a completion grants no reward, the final result explains that no permanent reward is recorded.
- Default player-facing surfaces must not expose `tierId`, DTO/API language, JSON, endpoint, debug, or manual-grade wording.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a shared player-facing Daren reward profile summary for a valid saved best result.
- **FR-002**: The summary MUST explain best recorded tier, best score, Ink Feather bonus, future New Game timing, permanence, and non-stacking/non-downgrade behavior.
- **FR-003**: The Daren completion result MUST include player-facing reward profile context that distinguishes current outcome from saved best reward.
- **FR-004**: Browser Daren UI MUST render the shared profile summary instead of independently composing terse best-reward text from raw fields.
- **FR-005**: Console Daren completion MUST include the same reward profile meaning through shared C# authority.
- **FR-006**: The change MUST preserve all existing Daren mechanics, reward thresholds, storage schema compatibility, and New Game grant semantics.
- **FR-007**: Player-facing copy MUST avoid raw ids and technical/debug terms.

### Key Entities

- **Daren ending**: The result of the current showcase attempt, including display name, score, epilogue, reward explanation, and whether it grants a persistent reward.
- **Daren reward profile**: The best saved Daren reward record used for future New Game Ink Feather grants.
- **Daren reward profile summary**: A player-facing text projection of the saved profile and its relationship to the current attempt.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused Daren tests prove first reward, weaker replay, and browser DTO projection all include the shared profile summary.
- **SC-002**: Browser frontend tests prove the best-reward block and lower-replay final block render the shared explanatory copy.
- **SC-003**: Existing Daren threshold, no-downgrade, non-stacking, and New Game grant tests continue to pass unchanged.
- **SC-004**: Player-facing Daren reward/profile text contains no default API/DTO/debug/manual-grade language.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Daren"`
- **Documentation/contract verification**: Source guards in `DarenQteShowcaseTests` confirm this remains client-owned and does not introduce GM-authored contract work.
- **Frontend verification**: `npm run test:player-facing` and `npm run build` from `BookOfEternityClient.WebFrontend/`.
- **Manual/player-facing verification**: Inspect browser-rendered Daren intro/completion states for readable profile copy and no terse raw field dump.

## Assumptions

- Existing `client_profile/qte_showcase_rewards.json` schema remains valid; new player-facing summaries are derived at runtime and do not require persisted schema migration.
- Daren showcase rewards are client-owned; no GM prompt/example update is required because the GM does not author this reward profile.
- The browser DTO may grow with additional player-facing text fields while preserving existing fields for compatibility.
