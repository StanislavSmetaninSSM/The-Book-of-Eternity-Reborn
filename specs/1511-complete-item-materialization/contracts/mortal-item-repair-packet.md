# Contract: Mortal Item Materialization Repair Packet v1

**Issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)
**Packet kind**: `mortal_item_materialization_repair`

## Grouping

One packet represents one exact item coordinate:

- `mortal_item:new:<creationRef>` before permanent identity; or
- `mortal_item:existing:<itemId>` after identity exists.

Global duplicate/ambiguity errors may produce one
`mortal_item:identity_authority` packet listing only the conflicting exact
coordinates. Packets are not grouped by display name, route label, or file.
`mortal_item:unknown`, `mortal_item:unresolved:*`, missing snapshot authority,
and unreadable whole-carrier failures never become item packets; they fail
closed through the diagnostic/rollback path.
`unknown` is not a reserved identity suffix: literal
`mortal_item:new:unknown` and exact identities with internal whitespace remain
valid coordinates. Only the standalone `mortal_item:unknown` form denotes a
missing coordinate.

## Required packet evidence

- `kind`: exact packet kind.
- `priority`: `critical`.
- `canonicalActorNames`: the exact item coordinate only.
- `transitionClass`: `create`, `transfer`, `split`, `merge`,
  `consume`, or `destroy`.
- `route`: exact creation/transition route.
- `sourceCarrier` and `destinationCarrier`: exact carrier coordinates or
  null when not applicable.
- `targetFiles`: minimal sorted GM-owned canonical/companion targets.
- `missingFields`: exact missing paths, if any.
- `exactFieldCorrections`: code/path/expected/actual/repair hint for every
  actionable GM-owned grouped issue. Client-owned receipt, seal, index, and
  transition issues are excluded.
- `requiredCompanionTargets`: exact companion roots required by the section
  dispositions and route.
- `expectedAuthority`: request/receipt/reward/carrier authority.
- `actualEvidence`: bounded invalid evidence.
- `templateRefs`: Mortal item contract/template references only.
- `expectedShape`, `steps`, and `doNotDo`: bounded instructions below.

## Target allowlist

The resolver begins with the exact active raw item carrier and adds only issue
paths rooted in:

- `game_state/inventory/items.json`;
- `game_state/inventory/item_resources.json`;
- `game_state/inventory/item_bonds.json`;
- `game_state/inventory/item_text_updates.json`;
- `game_state/inventory/recipes.json`;
- `game_state/inventory/item_movements.json`;
- `game_state/inventory/item_removals.json`;
- `game_state/inventory/storage_operations.json`;
- `game_state/npcs/npc_core.json`;
- `game_state/npcs/npc_inventory.json`;
- `game_state/npcs/item_journals.json`;
- `game_state/world/current_location.json`;
- `game_state/misc/vehicles.json`;
- `game_state/quests/regular_quests.json`;
- `game_state/quests/quest_history.json`;
- the exact active Mortal route request/receipt surface.

`item_identity_index.json`, receipt fields, pending snapshot artifacts, and
other client-owned files are never GM repair targets.

If any accepted-turn error reports changed receipt, seal, identity-index, or
client transition authority, the client does not dispatch a GM repair at all.
It rejects the accepted result and returns control to the pre-turn rollback
path, because asking the GM to edit protected evidence would make the repair
cycle impossible to complete safely.

## Expected shape

- One complete GM-owned item/envelope package for the exact coordinate.
- One valid route authority and one real destination carrier.
- Every section disposition matches physically present semantic/companion
  evidence.
- No GM-authored permanent ID, embedded receipt, index entry, or transition.
- No unrelated item, actor, quest, storage, vehicle, or narrative change.

## Required steps

1. Open the current validation request, listed target files, and template refs.
2. Locate the exact `creationRef` or `itemId`; never resolve by name.
3. Apply only the listed exact corrections and required companions.
4. Preserve request/transaction identity and do not repeat grants, payments,
   ingredient consumption, quest completion, or quantity changes.
5. Rerun raw item materialization validation.
6. Signal repair readiness only after the complete package passes.

## Do not do

- Do not author or edit permanent IDs, receipts, seals, index entries, or
  client transition history.
- Do not promote or tolerate a receipt-less current item.
- Do not replace a whole carrier/file to repair one item.
- Do not change another item with the same display name.
- Do not replay a trade, craft, loot, or quest transaction.
- Do not rewrite player-facing narrative unless a separate stale-output issue
  explicitly targets it.
- Do not create a new turn or terminal completion signal during repair.

## Rollback and idempotence

The validated pre-turn snapshot remains authoritative until post-seal canonical
validation succeeds. Failed repair restores carrier, companion, route,
currency/ingredient/reward, output, and index state. For craft, trade, quest
reward, loot acquisition, transfer, split, and merge, a repeated valid repair
with the same request/creation reference performs at most one settlement,
preserves quantity conservation, and creates no duplicate item, receipt, or
index transition. Creation replay is rejected against both materialization-ID
and creation-reference origin history in active and retired identity-index
entries, so changing only one half of the creation key, destroying an item, or
merging it as a contributor cannot make its original authority reusable.
Exact historical replay and case/whitespace/Unicode-confusable historical
aliases fail closed before GM dispatch; they are not repairable by renaming an
already-settled grant.

## Player-facing output

Packet content is operator-only. Console/browser narrative, inventory, quest,
trade, and crafting surfaces must not expose packet kind, file paths,
`creationRef`, receipt/index language, validation codes, or repair steps.
