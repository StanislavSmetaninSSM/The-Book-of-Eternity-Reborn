# Research: Local Training And Trade Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Findings

1. `TrainingService.BuildMortalTrainingViewAsync` enumerates every teacher and never reads `game_state/world/current_location.json`.
2. `TrainingService.BuildAfterlifeTrainingViewAsync` enumerates every mentor profile and does not read realm, active Guardian, or current abode authority.
3. `BuyTrainingAsync` resolves source actors globally, so a crafted internal action can buy from a remote teacher even if UI filtering is later added.
4. `NpcTradeService` had private ID/name matching but accepted a matching alias even when another supplied alias contradicted it, and direct entry points did not independently prove the actual Mortal realm.
5. `GuardianTradeService` limited ordinary discovery to the active Guardian, but direct service entry points still needed an actual Chaos Sea realm recheck before mutation.
6. Shining Abode already models `halls[]`, `currentHallId`, faction `hallId`, resident faction membership, faction leadership, and political actor faction origin. The local contract must resolve these relationships rather than treating the realm as one location.
7. Console and browser training both depend on `TrainingService`, so service-level filtering gives parity without duplicate UI logic.
8. Shining return-cycle trade auto-refresh is pre-existing realm-wide world-lifecycle work. It may prepare stored faction inventories, but it does not make a remote faction selectable or purchasable; player-triggered discovery and mutation remain hall-local.

## Decision

Add a shared local interaction scope resolver/matcher in `BookOfEternityClient/Services/`. Training uses it for listing, pending request creation, and purchase-time enforcement. Mortal and Chaos trade re-resolve that scope at every public service entry point; Shining trade filters visible factions by `currentHallId`. Exact alias sets reject contradictory ID/name evidence instead of trusting whichever field matches first.

## Rejected Approaches

- **UI-only filtering**: rejected because hidden/browser actions could still target remote teachers and spend resources.
- **Separate matchers per command**: rejected because training and trade would drift on aliases and missing authority.
- **Treat Shining as realm-wide**: rejected because `currentHallId`, `halls[]`, and faction `hallId` already provide canonical location authority.
