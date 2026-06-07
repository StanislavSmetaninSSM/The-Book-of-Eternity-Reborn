# Data Model: Browser Shining Abode Politics

## Existing Pending Shining Faction Founding Request

- **Storage**: `game_state/control/pending_shining_faction_foundings.json`
- **Authority**: `ShiningFactionRequestState.PendingShiningFactionFoundingRequest`
- **Writer**: `ShiningFactionRequestState.WriteFoundingRequestAsync`
- **Browser inputs**:
  - faction name
  - hall name
  - charter summary
  - hall description
  - favored archetype
  - patron effect family
  - service tags
  - supporting ascended resident IDs
  - confirmation
- **Derived by C#**:
  - proposed faction ID
  - proposed hall ID
  - quoted Ink Feather cost
  - quoted Light Spark cost
  - created-at turn
- **State changes**: writes one pending founding request and reserves the console costs from soul/Shining resources.

## Existing Pending Shining Faction Realignment Request

- **Storage**: `game_state/control/pending_shining_faction_realignments.json`
- **Authority**: `ShiningFactionRequestState.PendingShiningFactionRealignmentRequest`
- **Writer**: `ShiningFactionRequestState.WriteRealignmentRequestAsync`
- **Browser inputs**:
  - resident ID from canonical eligible resident roster
  - realignment mode
  - target faction ID when mode is accepted transfer
  - confirmation
- **Derived by C#**:
  - resident name
  - source faction ID/name
  - current loyalty/restlessness context
  - current turn
- **State changes**: writes one pending realignment request for GM resolution.

## Existing Pending Shining Faction Leadership Transition Request

- **Storage**: `game_state/control/pending_shining_faction_leadership_transitions.json`
- **Authority**: `ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest`
- **Writer**: `ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync`
- **Browser inputs**:
  - visible faction ID
  - transition mode
  - candidate head choice when required
  - supporting resident IDs when required
  - confirmation
- **Derived by C#**:
  - incumbent head type/ID/name
  - candidate head type/ID/name
  - current turn
- **State changes**: writes one pending leadership transition request for GM resolution.

## Visible Selection Sources

- **Visible factions**: existing Shining player-visible faction filtering.
- **Residents**: canonical entries in `guardian_abode_residents.json`, not nested historical/receipt objects.
- **Guardians/radiant actors**: existing C# state readers only when console leadership semantics allow them.
- **Costs**: `ShiningFactionRequestState` founding constants.

## Browser Prompt Session

- **Lifecycle**: command result returns `RequiresInput`; `ExplorerWebPromptSessionService` creates a session; browser submit calls `BrowserAfterlifeWriteService`.
- **Guards**: direct command realm guard, local-turn session lock, write-time realm/pending/local-write guard.
- **Default copy**: Russian/player-facing, no raw paths or DTO/API wording.
