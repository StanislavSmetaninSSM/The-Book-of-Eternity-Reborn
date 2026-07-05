# Console Live-Test Stabilization Roadmap

Tracking issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1452

## Goal

Bring the console client and live GM flow to a state where one complete player route can be played without manual file repair, repair-loop stalls, or developer-facing output in normal player screens.

## Operating Principles

- Freeze backlog growth except for true blockers, regressions, and recurring systemic failures.
- Treat live-test failures as harness/RLM feedback first: prefer validators, repair packets, rollback tools, canonical state constraints, daemon/bridge controls, and clearer agent-console surfaces before prompt-only fixes.
- Keep prompts and examples synchronized after any GM-facing contract or tool change.
- Focus on one stable golden route before widening coverage.
- Limit active work in progress to one harness/runtime blocker, one gameplay contract issue, and one live-test report.

## Phase 1: Triage The Backlog

- Classify open issues into:
  - `P0`: blocks launch, turn progression, bridge, daemon, validator, or repair-loop recovery.
  - `P1`: blocks the core gameplay route.
  - `P2`: hurts player-facing quality, localization, or output clarity.
  - `P3`: architecture, GM Workers, long-term polish, and non-blocking improvements.
- Identify duplicates and issues already resolved by recent changes.
- Mark the issues that directly block the golden route.
- Avoid creating new issues for minor observations; collect them in the active live-test report unless they are blockers or regressions.

## Phase 2: Stabilize Harness And RLM

- Fix live-test continuity problems first:
  - bridge startup and trust prompts;
  - daemon timeouts;
  - missing terminal-ready signals;
  - stale GM output detection;
  - repair-loop stalls;
  - rollback and safe cleanup;
  - compact repair packets;
  - agent-console snapshots and safe actions.
- For every recurring GM mistake, decide whether the environment can prevent, detect, repair, or roll back the mistake.
- Update GM-facing prompts, docs, examples, and source-guard tests when a contract changes.

## Phase 3: Golden Route

The first target route is:

1. Start a fully new game.
2. Meet or interact with the Guardian.
3. Enter a Mortal World scene.
4. Check status, inventory, map, help, skills, and effects.
5. Learn or improve a mortal skill through a training showcase.
6. Trade in the Mortal World.
7. Resolve a simple Mortal World combat encounter.
8. Gain experience and level up.
9. Die or end the mortal life.
10. Receive afterlife rewards.
11. Learn or improve a spiritual art.
12. Use afterlife trade or gacha.
13. Resolve a simple spiritual combat encounter.

Success means the route completes without manual JSON/file edits and without the player seeing raw technical output in normal command screens.

## Phase 4: P0/P1 Gameplay Burn-Down

Prioritize fixes that block:

- first game bootstrap;
- Guardian and afterlife entry flow;
- Mortal World scene actions;
- mortal training and skill mastery;
- mortal trade;
- mortal combat materialization;
- experience gain and level-up allocation;
- death and afterlife transition;
- spiritual art learning and upgrade;
- afterlife trade and gacha;
- spiritual combat;
- living-world changes without direct player action.

Do not spend major time on P2/P3 while a P0/P1 blocker prevents the golden route.

## Phase 5: Console Polish Pass

After the route is stable, sweep normal console commands for player-facing quality:

- no raw JSON in player screens;
- no internal enum values or technical keys without localization;
- useful summaries before detail screens;
- detail actions for large entity lists;
- a clear back action where the player enters a submenu;
- image actions for entities that support generated or stored images;
- readable formatting for nested data;
- no dead-end menus;
- no engineering/audit wording in normal player output.

The console client target is 8-9/10 player readiness, not merely "does not crash".

## Phase 6: Dedicated Afterlife Live Tests

Run separate live tests for:

- Chaos Sea;
- Shining Abode.

Each test should check:

- available commands;
- spiritual arts;
- spiritual combat;
- afterlife trade;
- gacha;
- reputation and faction-like state;
- live-world changes without direct player input;
- absence of technical leaks in player screens.

Use the existing manual saves in `FileSystemExample/game_session/saves/manual_saves/` where appropriate.

## Phase 7: GM Workers And Multi-Agent Follow-Up

Defer expansion until the golden route is stable.

Then verify:

- worker launch and hidden execution;
- delegation of concrete content-production tasks;
- roles such as lore consistency, NPC analysis, QTE content, entity filling, mortal-world data, and afterlife data;
- worker outputs returning to the main GM for review before player-facing use;
- no extra visible windows or confusing UI for the player.

## Reporting Cadence

After each work block, report:

- what was tested;
- what was fixed;
- which issues were closed or updated;
- which verification commands passed;
- what still blocks the golden route;
- current honest playability estimate.

