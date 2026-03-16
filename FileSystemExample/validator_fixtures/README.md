# Validator Fixtures

This folder contains **broken/fixed** examples for major validator contract classes.

Each fixture pack is organized as:

- `broken/` — intentionally invalid file or response fragment
- `fixed/` — corrected version of the same scenario
- `expected_errors.json` — the validator codes that should appear on the broken variant

These fixtures are not a full playable session. They are focused contract examples for:

1. terminal and accepted-turn narrative rules
2. lifecycle triggers
3. faction contract
4. sentient-item journals
5. client-owned files
6. append-only item text updates
7. QTE offer root contract
8. pending Memory Legacy root contract
9. accepted-turn interface/debug output shapes
10. known-location vs new-location `currentLocationData`
11. group combatant `healthStates` invariants
12. runtime-authored protocol surfaces that GM must not rewrite
13. accepted-turn `narrative_response` top-level shape
14. mandatory `equipmentChanges` slot arrays
15. Rare+ item bond / fate-card contract
16. `skillMasteryChanges` semantic linkage to active skills
17. faction add/update ids that must be `null` for add and string for update
18. known-location `currentLocationData` must carry `coordinates` together with `locationId` and `lastEventsDescription`

The goal is to make validator behavior explainable and reproducible for both developers and GM-side debugging.
