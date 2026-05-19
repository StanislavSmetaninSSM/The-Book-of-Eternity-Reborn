# Local Web UI over existing C# game

Tracking issue: #559

Date: 2026-05-20

## Goal

Add a local browser UI over the existing C# game without replacing the console UI.

The game remains local. Existing C# services, validators, normalizers, GM-daemon flow, and `game_session` files remain the source of truth.

The browser UI is a second frontend, not a second implementation of game logic.

## Non-goals

- No hosted multiplayer web app.
- No cloud saves.
- No account system.
- No rewrite of the game in JavaScript.
- No duplicate game rules in the frontend.
- No partial browser-only fork that supports only a few commands long-term.

## Target architecture

```text
Current game core / services / validators / GM-daemon / game_session files
                              |
                  UI-neutral application layer
                    |                         |
              Console adapter            Local Web adapter
              Spectre.Console            Browser UI
```

The console UI and browser UI must call the same command/application layer.

## Why IExplorerConsole is useful but not sufficient

`IExplorerConsole` is already a useful seam because `ExplorerMode` accepts a console abstraction and tests use `TestExplorerConsole`.

However, it is still Spectre-bound:

```csharp
void Write(IRenderable content);
T Prompt<T>(IPrompt<T> prompt);
```

These are console rendering concepts, not browser UI concepts. A web UI should not receive `IRenderable`, `SelectionPrompt`, or Spectre markup as its canonical model.

The next layer must be UI-neutral.

## UI-neutral command protocol

Introduce command results that represent intent, not console rendering.

Candidate DTOs:

```csharp
public sealed record ExplorerCommandResult(
    IReadOnlyList<UiBlock> Blocks,
    IReadOnlyList<UiAction> Actions,
    UiPrompt? Prompt,
    IReadOnlyList<UiNotification> Notifications,
    CommandExecutionState ExecutionState);
```

Block types:

- `TextBlock`
- `PanelBlock`
- `TableBlock`
- `ListBlock`
- `KeyValueGridBlock`
- `WarningBlock`
- `ErrorBlock`
- `RawJsonBlock`
- `ImageBlock` if needed later

Action types:

- command button;
- menu choice;
- confirm;
- cancel/back;
- submit prompt.

Prompt types:

- text input;
- number input;
- selection;
- multi-selection;
- confirmation;
- long text input.

The web UI renders these DTOs to HTML. The console UI renders the same DTOs through Spectre.

## Migration principle

The final goal is all player-facing commands available in both UI modes.

Migration can be incremental, but every command should be classified and eventually migrated.

Each command should end in one of:

- `migrated`: uses UI-neutral protocol.
- `blocked`: depends on a system that needs refactor first.
- `console_only_temporarily`: accepted only with an issue reference and reason.

No command should remain accidentally console-only.

## Command groups to migrate

Suggested groups:

1. Universal/help/status:
   - `/help`, `/status`, `/soul`, `/story`, `/chronicle`, `/validate`.
2. Meta/lore/debug:
   - `/codex`, `/achievements`, `/lives`, `/gallery`, `/debug`.
3. Mortal world:
   - inventory, NPCs, quests, factions, world status, character allocation.
4. Chaos Sea:
   - Guardians, Abodes, offerings, projects, Eternal Guardians, Chaos navigation.
5. Shining Abode:
   - overview, Gates, politics, treasury, trade/forge, Source of Light.
6. Afterlife combat/entity systems:
   - spiritual arts, combat log, entity profiles, special arts.
7. Story systems:
   - current and future `/сареф`, hidden mainline status, faction campaigns.
8. Lifecycle and local-turn commands:
   - incarnate, return/reenter Shining, pending setup, rollback/cancel flows.
9. QTE and interactive scenes:
   - likely hard path requiring separate interaction protocol.

## Local web host

Recommended first implementation:

- ASP.NET Core local server hosted by the game process or a companion executable.
- Browser opens `http://localhost:<port>`.
- Server is local-only by default.
- Web frontend calls C# endpoints.

Candidate endpoints:

```http
GET  /api/explorer/state
POST /api/explorer/command
POST /api/explorer/prompt
GET  /api/session/status
POST /api/session/lock
POST /api/session/unlock
```

The exact frontend technology can be chosen during implementation. Blazor Server is attractive because it keeps C# end-to-end, but ASP.NET Core + simple frontend/React is also viable.

## Session safety

Console UI and browser UI must not write to the same `game_session` concurrently.

Add a local session lock:

- records active UI owner: `console` or `web`;
- has heartbeat/lease timeout;
- blocks state-mutating commands from a second UI;
- allows read-only status when locked if safe;
- has recovery path for stale locks.

This protects pending-turn snapshots, local Shining economy writes, rollback baselines, and GM response processing.

## Rendering strategy

Short term:

- Keep existing console rendering working.
- For migrated commands, render `ExplorerCommandResult` through console adapter.
- Browser renders the same logical result.

Medium term:

- Reduce direct `AnsiConsole`/Spectre calls.
- Avoid adding new command logic that writes directly to console.

Long term:

- Explorer commands are UI-neutral by default.
- Console/Spectre is only an adapter.

## Direct console dependency audit

Audit categories:

- `IExplorerConsole` usages already abstracted.
- Direct `AnsiConsole` in `GameInterface`, `GameEngine`, validation/repair, turn lifecycle.
- `QteSceneService` direct interactive rendering.
- `Spectre.Console` prompts inside Explorer partials.
- `IRenderable` creation in command logic.

Each dependency should be classified:

- easy adapter conversion;
- requires command DTO;
- requires lifecycle protocol;
- can remain console-only temporarily.

## Testing strategy

Tests should validate logical UI output, not only string markup.

Add:

- DTO snapshot/unit tests for migrated commands.
- Console renderer tests for representative DTOs.
- Web API tests for command execution.
- Session lock tests.
- Migration coverage test: every registered command has a migration status.

Existing `TestExplorerConsole` can help during transition, but new tests should prefer UI-neutral DTOs.

## Documentation

Docs should cover:

- how to launch console mode;
- how to launch local web mode;
- local-only security model;
- session lock behavior;
- known console-only temporary commands during migration;
- troubleshooting stale locks.

## Initial task breakdown

1. Architecture/design spec.
2. Audit direct console/Spectre dependencies.
3. UI-neutral command protocol DTOs.
4. Migration registry for all commands.
5. Console renderer for DTOs.
6. Convert first read-only command group.
7. Local web host skeleton.
8. Web renderer MVP.
9. Command API.
10. Session lock.
11. Convert command groups one by one until all are migrated.
12. QTE/interactive scene protocol.
13. Docs and launch instructions.

## Self-review

No placeholders remain. The design intentionally prioritizes full command migration over a small permanent web demo. Implementation must proceed through tracked GitHub issues.
