# Issue #727 Browser Navigation IA Design

Tracked issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/727
Parent: #726, #680

## Context

The current React shell is already player-facing and keeps API/command diagnostics behind explicit `Расширенный режим`, but the primary route taxonomy still reflects the earlier shell slice: `Главная`, `Игра`, `Душа`, `Мир`, `Медиа`, `Настройки`. Issue #727 asks for the default navigation to read like a game client in the order summary/current scene → character/soul → world/location/map → journal/quests/notes → inventory/craft, while leaving Debug/API/Network/command coverage advanced-only.

The old React reference was inspected as a UI/UX baseline only. Relevant lessons for this slice: the player should see dedicated character sheet, inventory/crafting, journal/quest/faction-style sections and comfortable tabbed navigation; Reborn must not copy the old mortal-life-only rules or prompts. Current Reborn data comes from the C# `/api/game-screen` DTO and `actionMenu`; TypeScript remains presentation-only.

## Goals

- Make the primary navigation taxonomy explicit and ordered: `Главная` → `Игра` → `Душа` → `Мир` → `Журнал` → `Инвентарь`.
- Keep `Медиа` and `Настройки` reachable as player utility sections, but visually secondary to the core game taxonomy.
- Add `Журнал` and `Инвентарь` route surfaces that use existing `BrowserGameScreenDto.actionMenu` sections and existing `ActionSection`/`ActionCard` rendering rather than adding new gameplay rules.
- Render normal no-session states as locked/empty game states, not API failures.
- Add source/regression tests that guard route ordering and advanced separation.
- Update Browser Frontend and Local Web Host docs so future agents know #727 changed the primary route taxonomy.

## Non-goals

- Do not change C# runtime/gameplay logic, browser API contracts, afterlife contracts, pending/control files, validation rules, or command execution semantics.
- Do not implement the #728 card → modal/full-panel pattern.
- Do not implement #729 dedicated Afterlife/Shining Abode/Chaos Sea panels.
- Do not add browser screenshot dependencies. Use existing frontend verify and browser smoke artifacts; add visual-smoke evidence via Vite preview when practical.

## Chosen approach

Use a conservative React-only IA refinement:

1. Extend `RouteId` with `journal` and `inventory`.
2. Split route metadata into a `kind`/priority-style field: core player routes first, utility routes second.
3. Render primary route grid first for `Главная`, `Игра`, `Душа`, `Мир`, `Журнал`, `Инвентарь`; render `Медиа` and `Настройки` in a secondary utility navigation strip below it.
4. Implement `JournalRoute` and `InventoryRoute` as read-only presentation routes over existing `state.game.data.actionMenu.sections`:
   - journal route shows quest/archive/story/faction/guardian sections when present;
   - inventory route shows inventory/craft/item/equipment/storage sections when present;
   - both include neutral locked states when no game session exists;
   - both fall back to clear player-facing guidance when a session exists but matching sections are not available.
5. Keep advanced diagnostics untouched and lazy-loaded only after `advancedEnabled` is true.
6. Add source guard tests in `BrowserFrontendWorkspaceTests` for route order, utility separation, absence of technical labels from default route arrays, and the new locked states.
7. Update `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md` with the #727 route taxonomy.

## Alternatives considered

### A. Replace `Медиа`/`Настройки` entirely with `Журнал`/`Инвентарь`

Rejected. Settings and media are already tracked by later/browser tasks (#688, #689) and are valid player utility sections. Removing them would create avoidable churn and break existing launcher/settings flow.

### B. Add a C# navigation DTO

Rejected for this slice. The issue is IA/presentation order, and existing C# DTOs already expose enough action metadata. Adding a contract would increase scope and require fixture/API updates without solving the immediate navigation problem.

### C. Conservative React-only taxonomy and docs/tests

Chosen. This satisfies #727's default navigation requirements, keeps technical surfaces advanced-only, and avoids gameplay or contract changes.

## Data flow

- React loads `BrowserGameScreenDto` through existing `browserApi.getGameScreen()`.
- `JournalRoute` and `InventoryRoute` filter `game.actionMenu.sections` by section id/label text and `playerDefault`.
- Matching sections are passed to existing `ActionSection`, which renders existing `ActionCard` behavior and uses C# command/prompt-session endpoints when the player explicitly opens a section/action.
- No route creates or mutates game state merely by rendering.

## Error and empty-state handling

- If `state.game` is `no-active-session`, routes render locked game states with player-facing copy.
- If `state.game` is another failure, routes reuse `EmptyOrFailure`, preserving concise player messages and advanced-only technical details.
- If a session exists but no matching action sections are present, routes render a neutral summary card explaining that those pages will fill from the C# action catalog when available.

## Testing and verification

- Add focused source guard in `BrowserFrontendWorkspaceTests` before changing production code, then run it and watch it fail.
- Run the focused test again after implementation and expect pass.
- Run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- Run focused browser/frontend .NET tests for `BrowserFrontendWorkspaceTests`.
- Run broader relevant browser web UI suite when practical.
- Run `git diff --check` and an added-line static scan for secrets/eval/shell/SQL injection patterns.
- Perform a Vite preview visual smoke for default route navigation if the preview can run in the cron window; otherwise rely on generated build/browser smoke artifacts and report the limitation clearly.

## Docs/prompts impact

This is not an Afterlife or mortal-world runtime contract change, so AGENTS.md GM-facing afterlife/mortal prompt updates are not required. Documentation updates are limited to browser frontend/local web host docs and the tracked Superpowers spec/plan.

## Autonomous approval note

Stanislav explicitly authorized unattended autonomous work. I am not stopping for the normal Superpowers human approval gate; this design records the conservative chosen approach and is self-reviewed before implementation.

## Self-review

- Placeholder scan: no TBD/TODO placeholders remain.
- Consistency: the chosen React-only IA refinement matches the non-goals and data flow.
- Scope: focused on #727 only; #728/#729 remain separate.
- Ambiguity: utility routes stay available but secondary, making the primary route order explicit without deleting existing player features.
