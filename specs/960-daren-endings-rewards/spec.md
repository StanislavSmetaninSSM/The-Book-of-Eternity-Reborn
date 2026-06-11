# Feature Specification: Daren Endings and Reward Presentation

**Feature Branch**: `work/960-daren-endings-rewards`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Source Issues**: [#960](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/960), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite spine [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), prerequisite prose [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), prerequisite dialogue/cast [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), prerequisite branch consequences [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), base scenario [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Source Issues & Scope

- **Source GitHub issue**: #960 — expand Daren endings, epilogues, and reward presentation.
- **Parent**: #955 — make the Daren QTE training route feel like an interactive book while staying inside the existing QTE engine.
- **Prerequisites**:
  - #956 — `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` defines the beat order, pacing, cast slots, and ending/reward handoff hooks.
  - #957 — shared C# Daren route prose frames each QTE node.
  - #958 — named NPC cast and dialogue/social-choice moments exist in the shared route.
  - #959 — branch-specific in-route consequences and carry-forward echoes are merged.
- **Related base implementation**: #919 — standalone Daren showcase route, ending thresholds, persistent reward profile, New Game reward grants, console/browser surfaces.
- **Spec Kit justification**: #960 changes player-facing ending UX and shared console/browser QTE contract presentation for a medium multi-file content/contract slice. It must preserve #919 reward mechanics while adding durable handoff evidence under parent #955, so Spec Kit artifacts are required.
- **In scope**: authored epilogue text for each Daren ending tier, ending copy that reacts to score/performance and at least some notable choices/consequences, in-world explanation of the permanent achievement and New Game Ink Feather bonus, shared DTO/console/browser presentation updates when needed, and tests/source guards for missing/empty ending prose.
- **Out of scope**: new ending thresholds or Ink Feather bonus amounts, new reward profile state files, New Game grant semantics changes, new QTE check types, a separate ending engine, React-only ending data, broad content quality gate infrastructure (#961), and new post-run campaign-state consequences outside the standalone showcase.

## User Scenarios & Testing

### Scenario 1 - Ending tier gives a distinct epilogue (Priority: P1)

A player who completes the Daren heist sees an ending that feels like the end of the story, not only a tier name and mechanical score receipt.

**Independent Test**: Add focused guards over `DarenQteRewardProfileService.ResolveEnding()` and the Daren showcase completion path that fail unless each reward tier and the no-reward failure outcome has non-empty, distinct epilogue text.

**Acceptance Scenarios**:
1. **Given** a safe completion with a low score, **When** the ending resolves to `shadow_on_the_run`, **Then** the epilogue describes a narrow survival/dirty escape and not a generic reward receipt.
2. **Given** a mixed or good completion, **When** the ending resolves to `broken_trail` or `clean_heist`, **Then** the epilogue explains the traces, witnesses, loot control, or manageable aftermath that separates the tiers.
3. **Given** an excellent completion, **When** the ending resolves to `perfect_shadow`, **Then** the epilogue presents a clean legendary theft that is clearly distinct from lower tiers.
4. **Given** an unsafe or below-threshold run, **When** no reward is granted, **Then** the failure ending explains why no permanent achievement is recorded.

---

### Scenario 2 - Ending text reacts to performance and notable route consequences (Priority: P1)

The ending should reflect the route as an interactive-book heist. At minimum, tier epilogues and completion text should reference score/performance and selected consequence categories landed by #959: suspicion/evidence, ward pressure, witnesses, route choice, pursuit pressure, or hideout safety.

**Independent Test**: Add tests that inspect ending epilogue/reward copy for concrete consequence language and distinct score/performance framing. If the existing completion context cannot pass route-specific choice state into the ending without new state, use tier-level consequence language from the accumulated score/grade model and keep route-choice-specific wording in existing #959 result text.

**Acceptance Scenarios**:
1. **Given** a low reward tier, **When** the epilogue is shown, **Then** it mentions compromised traces, pressure, noise, witnesses, or pursuit rather than only `+1` Ink Feather.
2. **Given** a high reward tier, **When** the epilogue is shown, **Then** it explains that quiet execution, cleaned evidence, controlled pursuit, or hideout safety made the achievement stronger.
3. **Given** #959 carry-forward/result text already named important choices, **When** the final ending renders, **Then** it complements those choices without inventing a new route-memory runtime.

---

### Scenario 3 - Reward presentation is in-world and mechanical behavior is unchanged (Priority: P1)

The permanent Daren achievement and one-time New Game Ink Feather start bonus must be understandable in the fiction while preserving the existing reward profile and New Game grant rules.

**Independent Test**: Add tests that assert reward messages explain the earned achievement, tier, and future New Game Ink Feather bonus in player-facing in-world wording, while existing threshold/profile/New Game idempotency tests keep passing unchanged.

**Acceptance Scenarios**:
1. **Given** a reward-granting ending, **When** the completion summary and browser ending DTO are inspected, **Then** they include the tier name, epilogue, and an in-world explanation that the achievement will add the tier's Ink Feather bonus to future new games.
2. **Given** the same profile is recorded again with a worse or equal tier, **When** the service handles it, **Then** no stacking or downgrade occurs and the player-facing message remains clear.
3. **Given** New Game consumes the best saved tier, **When** the grant is applied, **Then** the existing one-time-per-new-session marker and Ink Feather amounts remain unchanged.

---

### Scenario 4 - Console and browser consume the same ending data (Priority: P1)

Console and browser clients should display the same ending epilogue/reward data through shared C# authority. Presentation may differ, but no frontend-only ending copy or browser-only reward interpretation should exist.

**Independent Test**: Extend C# and browser contract tests so `DarenShowcaseEndingDto` or equivalent browser state exposes the same ending fields the console completion uses. Source guards should fail if a new React-only ending table or local browser reward mapping appears.

**Acceptance Scenarios**:
1. **Given** the console completes the Daren showcase, **When** `RenderDarenCompletion` receives completion data, **Then** it can show tier, epilogue, score, and reward explanation without reading browser-only code.
2. **Given** the browser Daren state is serialized after completion, **When** the ending DTO is inspected, **Then** it includes the shared epilogue/reward text, not just raw tier id and bonus amount.
3. **Given** the feature is complete, **When** #961 content-quality gates are planned, **Then** the new ending epilogue fields are available to guard without changing reward mechanics.

## Edge Cases

- Unsafe route failure before safe hideout resolution remains `no_reward_failure` and must not write or upgrade `client_profile/qte_showcase_rewards.json`.
- Scores below 40 remain no-reward outcomes even if the route reached hideout; the ending must explain the failed achievement without recording a permanent reward.
- Existing reward tiers and bonuses remain: `shadow_on_the_run` +1, `broken_trail` +2, `clean_heist` +4, `perfect_shadow` +6.
- Existing New Game application remains exactly once per newly created session via `clientRewardGrants.darenQteShowcase`.
- If the implementation adds fields to shared C# records/DTOs, browser and console tests must prove both clients receive the same data.
- Daren showcase content is client-owned, not a GM-authored campaign QTE offer. GM-facing docs/examples are not expected unless a runtime/GM-authored QTE contract changes.

## Functional Requirements

- **FR-001**: Every Daren ending outcome (`no_reward_failure`, `shadow_on_the_run`, `broken_trail`, `clean_heist`, `perfect_shadow`) MUST have non-empty distinct epilogue prose.
- **FR-002**: Ending epilogues MUST reflect performance tier and use concrete story consequences such as evidence, suspicion, ward pressure, witnesses, pursuit control, route cleanliness, or hideout safety.
- **FR-003**: Reward-granting endings MUST present the permanent Daren achievement and future New Game Ink Feather start bonus in-world, not only as a mechanical receipt.
- **FR-004**: The existing threshold ids, display names, minimum normalized scores, and Ink Feather bonus amounts MUST remain compatible with #919.
- **FR-005**: Failure/no-reward outcomes MUST clearly explain why no permanent reward profile is written.
- **FR-006**: Console completion and browser state MUST consume the same ending epilogue/reward data from shared C# authority.
- **FR-007**: The implementation MUST NOT introduce a new reward profile file, ending-state runtime, QTE check type, campaign-state side effect, or frontend-only ending mapping.
- **FR-008**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` MUST record #960 ending/reward handoff truth without drifting from #956/#957/#958/#959 route invariants.
- **FR-009**: Tests/source guards MUST fail if ending epilogue copy is empty, generic, identical across tiers, missing from browser DTO/console completion data, or divorced from reward mechanics.
- **FR-010**: Broad content-quality gate infrastructure remains #961 and MUST NOT be implemented as a side effect beyond focused #960 ending/reward guards.

## Non-Functional Requirements

- **NFR-001**: Ending copy should be concise enough for console and browser surfaces while still feeling like a story epilogue.
- **NFR-002**: Tests should verify objective presence, distinctness, tier-specific consequence terms, reward explanation, and shared DTO/console-browser authority rather than subjective literary style.
- **NFR-003**: Guard failures should name the missing ending tier/outcome and missing field so future agents can repair content drift quickly.
- **NFR-004**: Changes should stay reviewable and avoid QTE/reward engine refactors unless a failing test proves a narrow shared-data extension is necessary.

## Success Criteria

- Daren endings include multiple distinct epilogues for failure and every reward tier.
- Ending text reacts to score/performance and at least some notable consequence categories from the route.
- Reward presentation explains the permanent achievement and future Ink Feather start bonus in-world.
- Existing reward thresholds, bonuses, profile writes, and New Game one-time grants remain compatible with #919.
- Console and browser expose/display the same shared ending data.
- Focused Daren tests and affected QTE/docs/browser slice pass locally with exact counts recorded.
- Spec Kit artifacts link #960/#955/#956/#957/#958/#959/#919 and are discoverable through the repo-local prerequisite helper.
