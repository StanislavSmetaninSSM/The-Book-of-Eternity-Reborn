# Feature Specification: Mortal Bootstrap Promised Merchant TradeState Guard

**Feature Branch**: `main`
**Created**: 2026-07-09
**Status**: Draft
**Source Issue**: #1470 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1470

## User Scenarios and Tests

### User Story 1 - Promised Merchant Is Actually Tradeable

When the player asks for a Mortal World opening with a merchant, shop, paid supplies, or a trade surface, the first mortal scene must not promise buying from an NPC while `/торговля_нпс` shows that same NPC as unavailable.

**Acceptance Criteria**

1. Given the mortal bootstrap request mentions an available merchant or shop, when the GM materializes the first mortal state, then at least one relevant NPC must have usable `tradeState.canTrade=true`.
2. The usable merchant must have a valid `merchantProfile` or a class/archetype resolvable by the existing NPC trade service.
3. If the GM intentionally blocks trade, then the player-facing opening scene must not describe the NPC as currently available for buying.

### User Story 2 - Harness Catches Missing TradeState Early

When a GM creates a merchant only in prose or through a loose `progressionType` such as `static_trade_npc`, validation must fail with a repair hint that explains the exact canonical shape needed for local trade.

**Acceptance Criteria**

1. Validation emits a clear Mortal bootstrap issue when requested trade has no usable NPC trade surface.
2. The issue points to `tradeState.canTrade=true` and `merchantProfile`.
3. The repair prompt tells the GM to either materialize trade properly or remove the promise from player-facing output.

## Requirements

- **REQ-001**: Mortal bootstrap validation must detect player-authored or opening-scene trade promises.
- **REQ-002**: A trade promise is satisfied by a relevant NPC with usable local NPC trade state.
- **REQ-003**: The guard must mirror the existing promised-teacher guard for `/обучение`.
- **REQ-004**: Player-facing repair guidance must be Russian and must avoid raw implementation dumps.
- **REQ-005**: GM-facing docs/examples must mention the required NPC trade shape when bootstrap promises local trade.

## Out of Scope

- Redesigning `/торговля_нпс` UI.
- Generating the actual trade inventory without a request; this issue only requires the merchant surface to be usable and able to request/prepare stock.
- Changing pricing or NPC trade balance.

## Prompt and Documentation Impact

This changes a GM-authored Mortal World contract. Update the Mortal GM-facing example/guidance that already documents promised training so it also covers promised local NPC trade.
