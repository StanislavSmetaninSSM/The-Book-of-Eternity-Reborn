# Quickstart: Validate Network Pass-The-Turn Multiplayer

Source issue: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1251>

This quickstart describes the intended validation flow after implementation.

## Prerequisites

- A valid local test `game_session`.
- A local/reference relay profile.
- Two client identities: host and guest.
- Host GM bridge configured as canonical GM authority.

## Scenario 1: Create Campaign And Join

1. Start local/reference relay.
2. Start host client and create a multiplayer campaign from the current session.
3. Create an invitation.
4. Start guest client and join using the invitation.
5. Confirm guest receives a persistent seat credential and latest resume point.

Expected:

- Campaign metadata has campaign id, seats, active seat, host GM authority, turn
  ordinal, controlled entity, and latest hash.
- Guest is read-only unless the guest seat is active.
- No manual save export/import occurs.

## Scenario 2: Accepted Turn Handoff

1. Active seat submits a simple action.
2. Host GM authority resolves it.
3. Validation accepts the state.
4. Active seat ends turn.
5. Relay accepts handoff package.
6. Next seat synchronizes automatically.

Expected:

- Handoff package has manifest, previous hash, new hash, validation status, and
  controlled entity.
- Turn ordinal advances by one.
- Previous active seat becomes read-only.
- Next active seat can act after sync.

## Scenario 3: Blocked Handoff

Repeat handoff attempt with each blocked condition:

- active GM turn;
- repair loop;
- unfinished local action;
- pending QTE;
- invalid state;
- local hash mismatch.

Expected:

- Handoff is rejected.
- Console/browser show a clear player-facing reason.
- Relay latest accepted resume point does not change.

## Scenario 4: Dormancy And Resume

1. Complete one accepted handoff.
2. Disconnect all clients.
3. Reconnect guest and host later with seat credentials.
4. Synchronize from relay.

Expected:

- Latest accepted resume point is restored.
- Active seat and host GM authority are unchanged.
- No in-fiction time, NPC/faction activity, afterlife progression, or
  GM-authored state advances automatically.

## Scenario 5: Mortal Persona vs Seat Handoff

1. Start in Mortal World with persona ledger enabled.
2. Pass turn from host seat to guest seat.
3. Verify NPC/faction identity state does not change.
4. Trigger a GM-accepted disguise/persona change.
5. Verify persona ledger records the fiction event and affected NPC/faction
   knowledge can update.

Expected:

- Seat handoff is not a fiction event.
- Persona/guise change is a fiction event.

## Suggested Commands

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "NetworkMultiplayer|Handoff|PersonaLedger|Relay"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

When browser UI is touched:

```powershell
cd BookOfEternityClient.WebFrontend
npm run verify
```
