# Research: Local Training And Trade Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Findings

1. `TrainingService.BuildMortalTrainingViewAsync` enumerates every teacher and never reads `game_state/world/current_location.json`.
2. `TrainingService.BuildAfterlifeTrainingViewAsync` enumerates every mentor profile and does not read realm, active Guardian, or current abode authority.
3. `BuyTrainingAsync` resolves source actors globally, so a crafted internal action can buy from a remote teacher even if UI filtering is later added.
4. `NpcTradeService` already enforces Mortal ID/name location matching, but its matcher is private and duplicated from future training needs.
5. `GuardianTradeService` already limits Chaos Sea trade to the active Guardian whose abode matches `chaosSeaNavigation.currentAbodeId`.
6. Shining Abode state currently exposes active realm authority but no canonical sublocation. Realm-wide Shining scope is therefore the only non-invented local contract.
7. Console and browser training both depend on `TrainingService`, so service-level filtering gives parity without duplicate UI logic.

## Decision

Add a shared local interaction scope resolver/matcher in `BookOfEternityClient/Services/`. Training uses it for listing, pending request creation, and purchase-time enforcement. Mortal trade reuses the matcher; Chaos and Shining trade receive explicit regression coverage without changing their established authority.

## Rejected Approaches

- **UI-only filtering**: rejected because hidden/browser actions could still target remote teachers and spend resources.
- **Separate matchers per command**: rejected because training and trade would drift on aliases and missing authority.
- **Invent Shining sublocations**: rejected because no canonical state currently supports them.
