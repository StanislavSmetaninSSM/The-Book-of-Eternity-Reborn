# Resource Projection, Privacy, and Domain Cutover Contract

## 1. Projection-only reads

Ordinary console, browser, local actions, and GM-context builders obtain resources from one accepted projection service. They do not parse resource canonical files independently and never fall back to removed domain fields.

The projection receives a validated immutable definition/state/history snapshot and returns localized, typed rows. A malformed/unavailable snapshot yields a generic unavailable result and zero mechanical actions.

## 2. Player-visible row

A player-visible row may contain:

- safe owner selector or current-context binding;
- localized resource name/unit;
- exact current and maximum display values;
- percentage only when meaningful;
- active/suspended in-world state;
- safe recent visible delta;
- locally available operations/actions.

It must not contain internal owner IDs, event/operation/transition/receipt IDs, source evidence, fingerprints, paths, raw canonical nodes, pending DTOs, validation/repair codes, rollback evidence, or agent guidance.

`hidden` and `gm_only` resources contribute no row, count, delta, action, derived warning, map/news/quest implication, or serialized nested data to ordinary player surfaces. `owner_visible` is shown only to the owning player's applicable view.

## 3. Console/browser parity

For the same accepted snapshot, console and browser expose equivalent semantic resource facts, blocking states, and actions. Visual layout may differ. All player copy is in-world Russian and all dynamic text is escaped/sanitized before rendering.

Existing in-memory UI DTO fields such as health/energy/poise percentages may remain as projection outputs. Their existence does not authorize corresponding persisted fields.

## 4. GM context

GM context exposes safe resource labels, allowed operations, current narrative availability, numeric command bounds, and exact temporary refs required for same-turn authoring. It explicitly says the GM must use resource commands and must not write current/max/history/IDs/paths.

Hidden mechanics may be available to the authorized GM context but never to player projections. Diagnostic/repair DTOs remain operator-only.

## 5. Mortal player cutover

Remove persisted health/energy/poise percentages and their legacy delta response fields. Keep narrative condition and money in their owning status/economic state. Bootstrap creates canonical resource entries. Status bar, `/status`, `/stats`, GM context, and browser game screen use the projection.

## 6. NPC/combat cutover

Remove NPC current/max health mirrors and combatant `currentHealth`, `currentPoise`, and group `healthStates[]`. Combat/NPC state retains identity, description, actions, effects, and resource-owner bindings. Detail, combat, target, and GM views resolve resources by exact owner.

## 7. Vehicle cutover

Remove vehicle `currentHealth`/`maxHealth` from `UpdateVehicles` and canonical vehicle companions. Vehicle status/detail/action eligibility resolves permanent `vehicleId` through the accepted projection. Activation, parking, location changes, and destruction use owner lifecycle authority and never a raw health fallback.

## 8. Item cutover

Remove item durability/max-durability current authority and the legacy item-resource sidecar values/commands. Item materialization declares resource capabilities/capacity; use, repair, firing, charge consumption, movement, destruction, and UI all use the common resource plan/projection.

## 9. Afterlife cutover

Remove persisted current/max action-economy pools, per-return gacha used/max counters, and numeric reroll mirrors. Spiritual conflict, Guardian/Shining gacha, blessing entitlements, previews, journals, and status views use accepted resource projections and transition evidence. Currency, treasury, faction resource ledger, progression, and spiritual combat axes remain specialized.

## 10. Effect integration

Periodic damage/restore and resource-event triggers use exact projected owner/resource capability at application and the accepted resource plan at execution. No effect adapter reads a legacy player/NPC/vehicle/combat/item/afterlife field.

## 11. No migration

Active templates, examples, manifests, and fixtures are rewritten directly. Any old save with a removed legacy resource field or missing required resource authority is incompatible. No compatibility reader, promotion, dual-write, synchronization, or fallback is permitted.

## 12. Documentation proof

The same change updates:

- Mortal rules/API/daemon/task guidance and at least one worked example;
- afterlife contract matrix, terminology/help, GM turn example, manifest, and documentation tests;
- active file-system example/template state and fixture integrity tests;
- player privacy and console/browser parity regressions;
- a source guard proving legacy resource fields are absent from active GM contracts and mechanical consumers, with explicit allow-list only for historical design/audit text.
