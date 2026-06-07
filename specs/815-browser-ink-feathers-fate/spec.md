# Feature Specification: Browser Ink Feather Fate Reveal and Rewrite

**Feature Branch**: `codex/815-ink-feathers-fate`
**Created**: 2026-06-07
**Status**: Draft for autonomous implementation
**Input**: GitHub issue #815, "feat(web): Чернильные перья — раскрытие и перезапись судьбы"
**Source Issue**: [#815](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/815)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#815](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/815), [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
- **Issue type**: browser-client parity enhancement for console Ink Feather fate actions.
- **Spec Kit justification**: #815 changes browser/console parity, player-facing browser prompt UX, local Ink Feather spending, pending dice/gacha state, command/action discovery, and tests across multiple C#/browser surfaces. It needs durable handoff across Spec Kit, Superpowers, and Codex.
- **Console source of truth**: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` methods `HandleRevealFate` and `HandleRewriteFate`; `BookOfEternityClient/Services/PendingTurnStateService.cs` owns `game_state/control/pending_dice_state.json`.
- **Contract scope**: player-facing browser UI, console/browser parity, local runtime writes to existing `game_state/meta/soul_state.json` and `game_state/control/pending_dice_state.json`, C# tests, browser command metadata/fixtures. GM-facing prompts/docs/examples are not expected to change because reveal/rewrite fate are existing client-owned local Ink Feather actions and no new GM-authored pending/control contract is introduced.
- **Out of scope**: sibling issue #816 archive/direct-pull actions, umbrella #817 closure, new Ink Feather economy formulas, new gacha banner mechanics, new GM pending/control files, changing accepted-turn authority signatures, React-side gameplay mutation, and card-heavy Browser Client redesign.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reveal fate from the browser with explicit Ink Feather spend confirmation (Priority: P1)

As a player with Ink Feathers, I can open a browser guided action for revealing fate, see the exact cost and remaining balance, confirm the spend, and receive the same information the console flow reveals: the fixed dice pool and the precomputed gacha base.

**Why this priority**: It covers the first console flow called out in #815 and unlocks rewrite fate as a meaningful follow-up.

**Independent Test**: Seed `soul_state.json` with enough Ink Feathers and no valid locked pending dice state; open the browser reveal-fate flow, submit confirmation, and verify `soul_state.inkFeathers.current` is reduced by `Math.Max(5, (int)(feathers * 0.10))`, `pending_dice_state.json.isFateLocked` becomes true, dice/gacha state is available, and the browser result is Russian/player-facing.

**Acceptance Scenarios**:

1. **Given** the player has enough Ink Feathers, **When** the browser opens the reveal-fate flow, **Then** the prompt shows the exact cost, remaining balance, and a required confirmation field.
2. **Given** the prompt remains valid and the player confirms, **When** reveal fate is submitted, **Then** the existing C# pending-turn state service creates/reuses the pending dice/gacha state, locks fate, deducts the quoted cost once, and returns dice/gacha details to the player.
3. **Given** the player cancels or does not confirm, **When** the prompt is submitted/cancelled, **Then** no Ink Feathers are spent and pending dice state is not modified.

---

### User Story 2 - Rewrite already revealed fate from the browser (Priority: P1)

As a player who has already revealed fate, I can choose to spend a larger Ink Feather cost from the browser to reroll the locked dice/gacha base and see old vs new outcomes.

**Why this priority**: It covers the second console flow called out in #815 and must preserve the console requirement that rewrite is only available after reveal/lock.

**Independent Test**: Seed `soul_state.json` and a valid `pending_dice_state.json` with `isFateLocked=true`; open and submit the browser rewrite-fate flow; verify the cost is `Math.Max(15, (int)(feathers * 0.25))`, the old dice/gacha differs from the newly persisted locked state, and only one spend is applied.

**Acceptance Scenarios**:

1. **Given** a valid locked fate state exists, **When** the browser opens rewrite fate, **Then** the prompt shows the rewrite cost and confirms that the current locked dice/gacha will be replaced.
2. **Given** the prompt remains valid and the player confirms, **When** rewrite fate is submitted, **Then** the browser write handler deducts the quoted cost, rewrites the pending turn state through C# authority, persists a locked replacement state, and returns old/new dice and gacha summaries.
3. **Given** fate is not currently locked, **When** rewrite fate is opened or submitted from a stale prompt, **Then** the browser blocks the action with a player-facing explanation and writes nothing.

---

### User Story 3 - Browser discovery, parity metadata, and stale-state safety (Priority: P2)

As a browser player, I can discover reveal/rewrite fate from `/help`, `/feathers` or the player action surfaces, and stale or invalid submissions fail safely without raw technical leakage.

**Why this priority**: Browser parity is incomplete if the spend flows exist but are hidden or if stale prompt sessions can spend the wrong amount.

**Independent Test**: Command/menu/coverage/API tests prove reveal/rewrite fate are browser-supported guided forms; stale prompt tests prove insufficient Feathers, missing/malformed `soul_state.json`, malformed pending dice state, and realm/session/local-write blockers return player-facing failures without spending.

**Acceptance Scenarios**:

1. **Given** browser command coverage is collected, **When** Ink Feather fate actions are inspected, **Then** #815 is no longer listed as an open browser parity gap while #816 and #817 remain tracked.
2. **Given** browser help/action surfaces render Ink Feather actions, **Then** labels use in-world Russian copy and default player UI does not expose raw `.json`, local paths, DTO/API, endpoint, validation, or debug wording.
3. **Given** a prompt was opened with enough Ink Feathers but the balance or pending dice state changed before submit, **When** the prompt is submitted, **Then** the write handler re-checks current state, blocks stale submissions, and writes nothing.

### Edge Cases

- `soul_state.json` is missing, malformed, has `inkFeathers` as either `{ "current": N }`, a number, or a numeric string.
- Current balance is below reveal/rewrite cost when opening or submitting.
- `pending_dice_state.json` is missing, malformed, unlocked, locked, or stale between open and submit.
- Multiple prompt sessions are opened; the submit path must recompute/re-check current balance and pending state before spending.
- Active GM turn/local write/prompt-session blockers must prevent browser writes on both command-open and stale prompt-submit paths.
- Browser forms may use internal ids/commands as values, but labels, summaries, blockers, and results must remain Russian/player-facing.
- The implementation must not add new GM-authored contract files, pending/control action types, accepted-turn authority rules, or React-side gameplay mutation handlers.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The browser command/action catalog MUST expose guided form access for reveal fate and rewrite fate using player-facing Russian labels and the existing browser prompt-session flow.
- **FR-002**: The reveal-fate browser flow MUST compute cost exactly like the console flow: `Math.Max(5, (int)(currentFeathers * 0.10))`.
- **FR-003**: The rewrite-fate browser flow MUST compute cost exactly like the console flow: `Math.Max(15, (int)(currentFeathers * 0.25))`.
- **FR-004**: Browser submit handlers MUST mutate only through C# authority/services and existing local write coordination; React MUST remain generic prompt/result presentation.
- **FR-005**: Reveal fate MUST deduct the current quoted cost once from `soul_state.inkFeathers.current`, create/reuse `PendingTurnStateService`, set `isFateLocked=true`, preserve/generated dice/gacha fields, and return a player-facing dice/gacha summary.
- **FR-006**: Rewrite fate MUST require an existing locked pending fate state, deduct the current quoted cost once, replace the pending dice/gacha state through `PendingTurnStateService.RewriteAsync`, keep the replacement locked, and return old/new player-facing dice/gacha summaries.
- **FR-007**: Direct command-open paths and stale prompt-submit paths MUST re-check resolved realm/session state, local write/GM-turn blockers, file parseability, current Ink Feather balance, pending dice state validity, and confirmation before writing.
- **FR-008**: Failed, cancelled, unconfirmed, insufficient-balance, malformed-state, wrong-realm, blocked-write, and stale-submit paths MUST leave both `soul_state.json` and `pending_dice_state.json` unchanged.
- **FR-009**: Browser help, command/action metadata, command coverage, and API/frontend fixtures MUST recognize #815 reveal/rewrite fate as supported browser guided forms while leaving #816 and #817 open.
- **FR-010**: Focused tests/source guards MUST be added before production implementation and MUST include successful reveal, successful rewrite, insufficient-balance, unlocked/missing pending fate, stale submit, and player-facing copy coverage.
- **FR-011**: This feature MUST keep existing runtime contract shapes unchanged. If implementation requires adding, renaming, or removing any pending/control/state field, the spec must be revised and the relevant GM-facing docs/examples/tests must be updated before completion.

### Key Entities

- **Ink Feather Balance**: Existing `soul_state.json.inkFeathers` authority, supporting current object/number/string shapes already handled by console deduction logic.
- **Pending Fate State**: Existing `game_state/control/pending_dice_state.json` file owned by `PendingTurnStateService`, containing `preGeneratedDices1d20`, `gachaBaseResult`, `isFateLocked`, and timestamps.
- **Reveal Fate Prompt**: Browser prompt session requiring confirmation to spend the reveal cost and lock/display the current pending fate state.
- **Rewrite Fate Prompt**: Browser prompt session requiring confirmation to spend the rewrite cost and replace a previously locked fate state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused browser parity tests prove reveal fate deducts exactly the console-equivalent cost and persists a locked pending dice/gacha state.
- **SC-002**: Focused browser parity tests prove rewrite fate is blocked before reveal and succeeds after reveal with old/new dice/gacha summaries.
- **SC-003**: Stale/blocked submit tests prove no Ink Feather spend occurs when balance, pending state, confirmation, or local-write eligibility changes after command open.
- **SC-004**: Browser command/help/menu/coverage/API fixtures no longer list #815 as unsupported once the implementation lands; #816/#817 remain open where appropriate.
- **SC-005**: Default browser result/blocker copy remains player-facing and avoids raw `.json`, local path, DTO/API, endpoint, validation, debug, or exception wording.
- **SC-006**: Local focused tests, relevant C# build/tests, frontend verification when fixtures change, `git diff --check`, and added-line static scan all pass before PR/merge.

## Assumptions

- Reveal/rewrite fate remain client-owned local Ink Feather actions; no GM turn request or GM-authored pending/control contract is needed.
- Browser support may use new command tokens/aliases or action metadata if that best fits existing command catalog patterns, but the user-facing discovery path must include `/feathers` or other existing browser action surfaces.
- Existing console behavior remains valid; this work should share/extract C# helpers where practical rather than copying business rules into React.
