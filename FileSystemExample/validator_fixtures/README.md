# Validator Fixtures

This folder contains **broken/fixed** examples for major validator contract classes.

Each fixture pack is organized as:

- `fixture.json` — canonical runnable manifest for the test harness
- `broken/` — intentionally invalid file or response fragment
- `fixed/` — corrected version of the same scenario
- `expected_errors.json` — legacy human-readable expectation file kept for reference during the transition to `fixture.json`

These fixtures are not a full playable session. They are focused contract examples for:

1. terminal and accepted-turn narrative rules
2. lifecycle triggers
3. faction contract
4. sentient-item journals
5. client-owned files, including world setup dossiers that GM must read but never rewrite
6. append-only item text updates
7. QTE offer root contract
8. pending Memory Legacy root contract
9. accepted-turn interface/debug output shapes
10. known-location vs new-location `currentLocationData`
11. group combatant `healthStates` invariants
12. runtime-authored protocol request surfaces must not trigger false GM-blame during repair-loop revalidation
13. accepted-turn `narrative_response` top-level shape
14. mandatory `equipmentChanges` slot arrays
15. Rare+ item bond / fate-card contract
16. `skillMasteryChanges` semantic linkage to active skills
17. faction add/update ids that must be `null` for add and string for update
18. known-location `currentLocationData` must carry `coordinates` together with `locationId` and `lastEventsDescription`
19. canonical `achievements.json` top-level shape
20. canonical `codex_entries.json` top-level shape and bootstrap-safe current-world entry
21. accepted-turn `gm_thoughts_markdown` must contain structured NPC scope
22. accepted-turn relevant NPC reasoning blocks must contain an explicit current-location audit line
23. `quest_history.json` canonical `questHistory` array shape
24. `weather.json` weather contract requires both `tendency` and `description`
25. direct-root `world_time.json` requires the full absolute state payload
26. `NPCInventoryAdds` must carry a nested full item object
27. `NPCActivityUpdates` must carry a nested `activityUpdate` payload
28. critical `guardians.json` must not contain PowerShell runtime / AST serialization artifacts
29. `activeGuardian` must resolve to an entry that actually exists in `guardians[]`
30. `activeGuardian` with a materialized abode must also materialize matching `chaosSeaNavigation.currentAbodeId`
31. `completeNPCActivities` must match the canonical pre-turn `currentActivity`
32. `factionChronicleUpdates` must bind to an existing permanent faction id
33. `worldMapUpdates.storageUpdates` must target an existing canonical storage id
34. `factionBonusChanges` removals must target an existing canonical bonus id
35. `completeFactionProjects` must target an existing canonical project id
36. `factionCustomStateChanges` removals must target an existing canonical custom state id
37. `worldMapUpdates.linkUpdates` must target an existing canonical adjacency link
38. `completeThreatActivities` must target a threat with canonical active `currentActivity`

The goal is to make validator behavior explainable and reproducible for both developers and GM-side debugging.

For automated tests, the harness now:
- copies `FileSystemExample/game_session` as a minimal valid base session
- overlays `shared/`, then `broken/` or `fixed/` files according to `fixture.json`
- runs the fixture through the declared runner (`state_only`, `accepted_turn`, or `critical_state`)
- asserts expected error codes on `broken` and their disappearance on `fixed`
