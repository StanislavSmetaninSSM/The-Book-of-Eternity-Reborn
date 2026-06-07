# Data Model: Browser Shining Abode Actions

**Source Issue**: [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811)

## Existing Runtime Contract

The feature reuses the existing Shining core action pending control contract:

```text
game_state/control/pending_shining_abode_actions.json
```

The implementation must write through `ShiningCoreActionRequestState` and preserve the existing request shape. No new fields, files, response fields, receipts, reports, or GM-authored output contracts are planned.

## Browser Command Forms

### Native Faction Discovery

- Command ID: `shining_native_faction_discovery`
- Player-facing purpose: open/discover a native Shining faction.
- Form inputs:
  - confirmation that the player accepts canonical resource spend.
- Existing request fields:
  - `actionType = discover_native_faction`
  - `radianceTierAtRequest`
  - `quotedCostFeathers`
  - `quotedCostLightSparks`
  - `createdAtTurn`

### Faction Investment

- Command ID: `shining_faction_investment`
- Player-facing purpose: invest in a visible eligible Shining faction.
- Form inputs:
  - `faction_id` selected from visible eligible factions.
  - confirmation that the player accepts canonical resource spend.
- Existing request fields:
  - `actionType = invest_in_faction`
  - `factionId`
  - `factionName`
  - `quotedCostFeathers`
  - `quotedCostLightSparks`
  - `createdAtTurn`

### Project Support

- Command ID: `shining_project_support`
- Player-facing purpose: support a visible completed unsupported Shining project.
- Form inputs:
  - `project_choice`, a browser-local selection value resolving to canonical `factionId` and `projectId`.
  - confirmation.
- Existing request fields:
  - `actionType = support_project`
  - `factionId`
  - `projectId`
  - `projectDisplayName`
  - zero quoted costs
  - `createdAtTurn`

### Project Unsupport

- Command ID: `shining_project_unsupport`
- Player-facing purpose: remove support from a visible supported project.
- Form inputs:
  - `project_choice`, resolving to canonical `factionId` and `projectId`.
  - confirmation.
- Existing request fields:
  - `actionType = unsupport_project`
  - `factionId`
  - `projectId`
  - `projectDisplayName`
  - zero quoted costs
  - `createdAtTurn`

### Project Retirement

- Command ID: `shining_project_retirement`
- Player-facing purpose: retire a visible completed Shining project.
- Form inputs:
  - `project_choice`, resolving to canonical `factionId` and `projectId`.
  - confirmation.
- Existing request fields:
  - `actionType = retire_project`
  - `factionId`
  - `projectId`
  - `projectDisplayName`
  - zero quoted costs
  - `createdAtTurn`

## Visibility and Eligibility

- Factions come from existing visible Shining faction authority and must be operational/player-visible for default browser forms.
- Investment options exclude factions that are already at the current investment cap according to current C# state.
- Support options include visible completed projects that are not already supported and only when the support cap has room.
- Unsupport options include visible supported projects.
- Retirement options include visible completed projects that are not already retired.

## Validation

Prompt open performs player-facing pre-filtering and blocker checks. Submit performs the same realm/actionability checks again, then delegates final validation to `ShiningCoreActionRequestState`.
