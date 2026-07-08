# Tasks: Mortal Bootstrap Placeholder Name Guard

**Source GitHub issue**: #1461 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1461

- [x] Add RED test: accepted first Mortal bootstrap with placeholder location/faction/link/NPC names reports `mortal_bootstrap_placeholder_player_visible_name`.
- [x] Add RED test: client-owned pre-materialization Mortal bootstrap baseline with the same placeholders does not report that issue.
- [x] Implement validator detection for accepted first Mortal bootstrap materialization.
- [x] Scan player-visible fields in `current_location.json`, `world_map.json`, `faction_core.json`, `faction_resources.json`, and `npc_core.json`.
- [x] Add actionable repair hints that require in-world names instead of deletion or client auto-generation.
- [x] Update first Mortal bootstrap GM prompt guidance in `game_master_daemon.ps1`.
- [x] Update worked GM example/guidance in `Examples/E_CLI_Step_Main.txt`.
- [x] Run focused validation and documentation/source-guard tests.
- [x] Commit the fix and return to the golden path live test.
