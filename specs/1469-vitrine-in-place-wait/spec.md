# Feature Specification: In-Place Vitrine Preparation Wait

**Feature Branch**: `main`
**Created**: 2026-07-09
**Status**: Draft
**Source Issue**: #1469 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1469
**Related Spec**: `specs/1378-training-vitrines/`

## User Scenarios and Tests

### User Story 1 - Missing Training Vitrine Waits In Place

When the player opens `/обучение` and the selected teacher or mentor has no ready showcase, the client stays on the training screen, tells the player that the vitrine is being prepared, sends the GM request immediately, and automatically refreshes the same command after the accepted GM response.

**Acceptance Criteria**

1. Given `/обучение` has no ready teacher showcase, when the command is executed, then the visible text says: `Витрина подготавливается. Дождитесь завершения, ГМ работает`.
2. The command must not require the player to close and reopen `/обучение`.
3. After the GM response is accepted and the showcase is valid, the client automatically renders the training choices from the same command.

### User Story 2 - Missing Trade Vitrine Waits In Place

When the player opens `/торговля` or a specific trade command and the trade stock is missing or stale, the client stays on that trade screen, sends the GM request immediately, and refreshes the same trade view after the accepted GM response.

**Acceptance Criteria**

1. Missing NPC trade stock uses the same in-place waiting copy and auto-refresh behavior.
2. Missing Guardian or Shining Abode trade stock uses the same player contract where the command already supports GM-prepared stock.
3. The client must not show a normal narrative scene as the result of a vitrine preparation request.

## Requirements

- **REQ-001**: Vitrine preparation pending actions must be distinguishable from ordinary player turns.
- **REQ-002**: The command invocation that creates the pending request must stay visually on the local command surface.
- **REQ-003**: The engine must send the GM action immediately, wait for validation/repair as usual, and then re-render the originating command once.
- **REQ-004**: If the GM response fails or the vitrine is still missing after refresh, the command must not spin in an infinite auto-dispatch loop.
- **REQ-005**: Player-facing copy must be Russian and must not mention JSON, validation, pending files, or implementation details.
- **REQ-006**: Accepted vitrine-preparation service responses must not overwrite the current visible scene, must not be appended as a normal story turn, and must not advance the player-visible turn counter.

## Out of Scope

- Redesigning training or trade offer cards.
- Changing prices, caps, validation legality, or GM showcase schema.
- Browser visual redesign beyond parity where existing command infrastructure already exposes pending actions.

## Prompt and Documentation Impact

This is primarily a client harness/UX flow. Existing GM request contracts stay the same: the GM still resolves the same pending training/trade requests. GM docs need only a short clarification if code changes the player-visible workflow or the request timing language.
