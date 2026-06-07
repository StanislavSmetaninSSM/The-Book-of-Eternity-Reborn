# Feature Specification: Browser Shining Abode Relic Forge

**Feature Branch**: `task/813-browser-relic-forge`
**Created**: 2026-06-07
**Status**: Draft for autonomous implementation
**Input**: GitHub issue #813, "feat(web): Ковка реликвий — reshape, retune, strengthen, stabilize, uplift"
**Source Issue**: [#813](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/813)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open a browser forge request flow (Priority: P1)

As a player in the Shining Abode, I can open a browser guided forge form, choose an available Shining faction, choose one of the existing forge actions, and choose one of my Soul Relics without using the console-only menu.

**Why this priority**: Console already exposes `⚒ Создать запрос на перековку`; #813 is incomplete until browser players can start the same flow through the Browser Client.

**Independent Test**: Seed Shining Abode state with a faction, Soul Relics, resources, and no active pending core action; open the browser forge command and verify player-facing prompt fields/choices for faction, action type, relic, and confirmation are available without writing anything before submit.

**Acceptance Scenarios**:

1. **Given** the player is in active Shining Abode, **When** they open the forge form in the browser, **Then** the form lists available Shining factions and Soul Relics in player-facing Russian copy.
2. **Given** a core action or local write is already pending, **When** the browser opens the forge command, **Then** it returns player-facing blocker text and writes nothing.
3. **Given** the player has no forgeable Soul Relics or the Shining Abode state is unavailable, **When** they open the forge command, **Then** the browser explains the unavailable forge state without exposing raw file paths, `.json`, DTO, API, or debug wording.

---

### User Story 2 - Reshape a relic from the browser (Priority: P1)

As a player, I can request `reshape` for a Soul Relic in the browser by choosing or entering the target form, optionally spending existing relic-reroll entitlement, and confirming the same Shining core action request that console writes.

**Why this priority**: Reshape is the first console forge action and includes the reroll/target-form behavior called out in #813.

**Independent Test**: Submit a browser reshape prompt with a target form and optional reroll count; verify `pending_shining_abode_actions.json` contains `actionType: forge_relic.reshape`, the selected faction/relic, `targetFormTag`, quoted costs, `createdAtTurn`, and relic reroll commit metadata handled through `WriteForgeRequestWithRelicRerollCommitAsync`.

**Acceptance Scenarios**:

1. **Given** a reshape prompt remains valid, **When** the player confirms, **Then** the browser writes the existing Shining core forge request contract for `forge_relic.reshape`.
2. **Given** a stale prompt references a missing relic, wrong faction, invalid target form, exhausted reroll entitlement, or insufficient resources, **When** it is submitted, **Then** the browser blocks and writes nothing.
3. **Given** the reshape prompt previews form names, **Then** default copy humanizes form tags and does not leak raw canonical tags except where the existing contract preview intentionally records the target tag after confirmation.

---

### User Story 3 - Retune, strengthen, stabilize, and uplift a relic from the browser (Priority: P1)

As a player, I can request the remaining four console forge actions from the browser using the same property, rarity, echo, and cost authority as the console.

**Why this priority**: Issue #813 explicitly requires the complete forge action set, not only reshape.

**Independent Test**: Submit browser prompts for `retune_property`, `strengthen_band`, `stabilize_echo`, and `uplift_rarity`; verify each creates a single existing pending Shining core action request with the correct action type and action-specific payload fields (`propertyIndex`, `replacementProperty`, `addedProperties`, or no extra payload where console uses only the action type/relic).

**Acceptance Scenarios**:

1. **Given** retune is selected, **When** the player chooses a property and replacement property, **Then** the request stores `propertyIndex` and `replacementProperty` through the existing contract.
2. **Given** strengthen is selected, **When** the player chooses a property, **Then** the request stores `propertyIndex` and quotes the existing forge cost.
3. **Given** stabilize is selected, **When** the player confirms a relic with echo/manifests eligible under existing C# rules, **Then** the request uses `forge_relic.stabilize_echo` without inventing a browser-only mutation.
4. **Given** uplift is selected, **When** the player accepts or edits additional properties, **Then** the request stores `addedProperties` using the existing structured property shape.

---

### User Story 4 - Browser discovery, help, coverage, and player-facing safeguards reflect forge support (Priority: P2)

As a browser player, I can discover Shining forge through command help/menu metadata, and default browser surfaces do not present #813 as an unresolved browser parity gap once implemented.

**Why this priority**: Browser parity must be visible to the player and to the repo's command coverage/source guards.

**Independent Test**: Command coverage/help/menu/API fixture tests show #813 forge actions as supported guided forms, keep #817 open for remaining siblings, and source guards assert default player-facing copy does not expose internal pending/control diagnostics.

**Acceptance Scenarios**:

1. **Given** browser command coverage is collected, **When** Shining treasury/forge rows are inspected, **Then** #813 is no longer listed as an open browser forge gap while #817 remains open for unresolved siblings.
2. **Given** browser help/menu surfaces render forge entry points, **Then** they use in-world Russian labels and do not expose slash-command audit framing in default mode.
3. **Given** browser result/blocker copy is rendered for failed forge open/submit attempts, **Then** no `.json`, `pending_`, raw `actionType`, DTO/API/endpoint/debug/file-path/raw validation wording appears in default player-facing output.

### Edge Cases

- Existing, malformed, or duplicate `pending_shining_abode_actions.json` blocks forge open and stale submit with player-facing copy.
- Active GM turn/local write blockers must be enforced on both direct command-open and prompt-submit paths.
- Stale prompt submissions must re-check realm, faction availability, relic existence, action eligibility, resource cost quote, reroll entitlement, and canonical validation immediately before writing.
- Browser may use internal ids as form answer values, but labels, summaries, blockers, and result copy must remain player-facing.
- The implementation must not add React-side gameplay rules for forge costs, relic mutations, property tiers, or rarity upgrades.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The browser command catalog MUST define a player-facing command or guided action for the Shining relic forge flow required by #813.
- **FR-002**: Browser prompt builders MUST reuse existing C# Shining Abode, Soul Relic, and forge quote authority for factions, relics, actions, property choices, target forms, suggested replacement/additional properties, reroll entitlement, and resource costs.
- **FR-003**: Browser submit handlers MUST write through the existing `ShiningCoreActionRequestState.WriteForgeRequestWithRelicRerollCommitAsync` path or an equivalent existing C# authority path that preserves relic-reroll commit behavior.
- **FR-004**: Reshape MUST write `ShiningCoreActionRequestState.ActionTypeForgeRelicReshape` with `targetFormTag` and any spent relic-reroll count.
- **FR-005**: Retune MUST write `ActionTypeForgeRelicRetuneProperty` with `propertyIndex`, `replacementProperty`, and any spent relic-reroll count.
- **FR-006**: Strengthen MUST write `ActionTypeForgeRelicStrengthenBand` with `propertyIndex`.
- **FR-007**: Stabilize MUST write `ActionTypeForgeRelicStabilizeEcho` for eligible relics without adding browser-only state mutation.
- **FR-008**: Uplift MUST write `ActionTypeForgeRelicUpliftRarity` with `addedProperties` when the existing authority requires extra property data.
- **FR-009**: Direct command-open paths and stale prompt-submit paths MUST re-check realm, Shining availability, pending core action state, local-write/active GM blockers, relic/action eligibility, and canonical validation before writing.
- **FR-010**: Browser help, command menu metadata, command coverage, and API contract fixtures MUST recognize the #813 forge flow as supported while keeping the default UI Russian/player-facing.
- **FR-011**: Focused tests/source guards MUST be added before production implementation and must include command-open and stale-submit guard coverage where existing browser parity patterns support both.
- **FR-012**: This feature MUST keep existing afterlife runtime contract shapes unchanged. If implementation requires adding, renaming, or removing any pending/control/state field, the spec must be revised and afterlife contract docs/examples/tests must be updated before completion.

### Key Entities

- **Shining Forge Request**: Existing `pending_shining_abode_actions.json` request with `actionType` values for `forge_relic.reshape`, `forge_relic.retune_property`, `forge_relic.strengthen_band`, `forge_relic.stabilize_echo`, and `forge_relic.uplift_rarity`.
- **Soul Relic**: Existing player relic in `soul_state.json.soulRelics.stored/equipped` with `relicId`, name, rarity/quality, form tag, properties, and optional companion echo data.
- **Forge Cost Quote**: Existing `ShiningAbodeState.TryQuoteForgeAction` result that determines Ink Feather/Light Spark costs and eligibility.
- **Relic Reroll Entitlement**: Existing Shining blessing entitlement in soul state that can be spent by reshape/retune suggestion rerolls only when the confirmed forge request is written.
- **Browser Prompt Session**: Existing browser guided form flow that opens from command metadata and submits through C# write authority.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused browser parity tests prove reshape writes the existing forge pending request contract and preserves relic-reroll commit semantics.
- **SC-002**: Focused browser parity tests prove retune, strengthen, stabilize, and uplift write their action-specific payloads through existing C# authority.
- **SC-003**: Realm, pending-core-action/local-write, missing-relic, invalid-action, invalid-property, invalid-target-form, insufficient-resource, and exhausted-reroll blockers are covered by command-open and/or stale-submit tests with player-facing copy.
- **SC-004**: Command/help/menu/coverage/API fixture tests prove #813 forge actions are browser-supported and #813 is removed from open browser coverage gaps while #817 remains tracked.
- **SC-005**: No afterlife runtime contract, GM-authored response surface, prompt contract, or example manifest shape changes are introduced; afterlife contract docs/examples are unchanged unless implementation discovers a contract change.

## Out of Scope

- Sibling issues #814, #815, #816 and umbrella issue #817 closure.
- Browser storage/transport, Ink Feather fate rewrite, afterlife archive, or direct pull actions.
- New forge mechanics, new Soul Relic property schema, new Shining resources, automatic GM turn resolution, or local application of forge deltas.
- React/TypeScript gameplay handlers for forge; React remains generic prompt/result presentation unless shared rendering cannot display the C# metadata safely.
- PR creation, merge, GitHub issue closure, or cron/job changes.

## Assumptions

- Existing console forge code in `ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs`, existing `ShiningAbodeState.TryQuoteForgeAction`, and existing `ShiningCoreActionRequestState` remain the authority.
- Existing afterlife GM-facing docs already describe Shining forge action types and mutation table; no docs update is required if browser simply exposes the existing contract without shape changes.
- If implementation discovers a required runtime contract shape change, this spec must be revised and afterlife contract matrix/examples/coverage tests must be updated before completion.
