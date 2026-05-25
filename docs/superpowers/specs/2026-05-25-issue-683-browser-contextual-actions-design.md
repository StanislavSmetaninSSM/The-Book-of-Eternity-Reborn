# Issue #683 Browser Contextual Actions Design

## Context

Issue #683 asks the Browser Client to stop presenting the command catalog as the primary player UI. The current React shell already has player-facing routes and keeps command/API diagnostics behind `Расширенный режим`, but the `Мир`/game action area still lacks a structured, realm-aware action menu that covers browser-executable Explorer commands.

Relevant constraints checked for this design:

- `AGENTS.md` requires all implementation work to be tied to a tracked task; this work is tied to GitHub issue #683.
- Browser UI remains presentation-only. C# remains the gameplay/application authority.
- The old 0.9.14 browser project is a visual/reference baseline only: comfortable tabs, player panels, inventory/factions-style sections, and information hierarchy; no old mechanics or prompts are copied.
- Default browser UI must be Russian-first and player-facing. Raw slash commands, endpoint names, and debug tooling stay in explicit advanced mode.

## Approaches considered

### Option A — React-only static menu

Add a hard-coded TypeScript action menu in `App.tsx`.

- Pros: fastest UI change.
- Cons: duplicates command/realm rules in React, violates C# authority, and can drift from `ExplorerCommandCatalog`.

### Option B — C# player action menu DTO from `ExplorerCommandCatalog` (chosen)

Add a C# projection that converts browser-executable command descriptors into player-facing sections/actions with labels, descriptions, realm availability, mutation warnings, and guided-form hints. Include that DTO in the read-only `/api/game-screen` response and render it from React without showing slash IDs in the default UI.

- Pros: one source of command truth, C# owns realm and mutation metadata, React stays presentation-only, tests can fail closed when commands lack player metadata.
- Cons: touches both C# DTO contracts and React rendering.

### Option C — Make default UI call `/api/explorer/command` directly for every action

Default buttons/forms would execute commands immediately.

- Pros: most functional right now.
- Cons: mutating actions need local write gating and prompt sessions; executing from the default surface risks bypassing the explicit advanced-mode safety boundary. This should be a later issue after the menu metadata exists.

## Chosen architecture

Add a `BrowserPlayerCommandMenuDto` to `BrowserGameScreenDto`. `BrowserGameScreenService` will build it using the shared `ExplorerCommandCatalog`, current `AggregatedGameState`, and lifecycle/QTE state. The DTO is read-only and contains player-facing metadata only: grouped sections, labels, descriptions, realm availability, availability state, disabled reasons, mutation warnings, and guided-form copy. It may carry an internal `advancedCommand` field for future bridging/execution, but React must not display that field in player-default UI.

React will render the DTO in the existing `Мир` route as a sectioned command menu. Read-only actions appear as game cards. Mutating actions render compact guided forms with player prompts and safety warnings instead of requiring manual slash syntax. Actual command execution remains behind the already explicit advanced command surface until a later local-write lifecycle issue wires safe player-default submission.

## Data flow

1. `/api/game-screen` calls `BrowserGameScreenService.BuildAsync()`.
2. The service refreshes C# state and lifecycle/QTE data as it already does.
3. A new `BrowserPlayerCommandMenuBuilder.Build(state, lifecycle, qte)` projects browser-executable descriptors into grouped sections.
4. React receives the typed `game.actionMenu` DTO and renders sections/actions in `WorldRoute`.
5. The default UI displays only Russian/player-facing labels and warnings. Advanced command IDs remain available only in `AdvancedDiagnosticsPanel` or future explicit advanced controls.

## Sections and contextual availability

Required player sections for #683:

- `Персонаж / Душа`
- `Мир`
- `Квесты`
- `Карта`
- `Фракции`
- `Хранители`
- `Посмертие`
- `Бой`
- `Архив`
- `Настройки`

Additional implementation section:

- `Расширенный режим` for debug/GM/validation/system command metadata only. This section is not rendered by default in player panels.

Realm availability is derived from `ExplorerCommandGroup` and current C# state:

- Mortal world actions are available outside afterlife realms.
- Chaos Sea actions are available in `state.IsInChaosSea`.
- Shining Abode actions are available in `state.IsInShiningAbode` or the pending handoff state where appropriate.
- Afterlife combat/entity actions are available in afterlife realms; `spiritual_action` is additionally labelled as requiring an active spiritual conflict.
- Universal/player meta actions are generally available unless they are advanced-only.
- Local-turn/mutating actions are disabled when lifecycle/QTE says the browser cannot start a local write.

## Error handling and safety

- Missing metadata for a browser-executable descriptor is a test failure.
- The DTO must not render raw slash command strings in player-default markup.
- Mutating actions carry a non-empty mutation warning and form prompt.
- Advanced/debug actions are separated and can only be displayed when advanced mode is enabled.
- This change does not alter afterlife runtime contracts or GM-authored control files; no Afterlife contract matrix update is required.

## Testing strategy

- Add C# guard tests that every browser-executable `ExplorerCommandDescriptor` has player menu metadata with non-empty Russian label, description, realm availability, and mutation warning.
- Add C# guard tests for contextual realm availability, including Chaos Sea/Shining Abode examples and `spiritual_action` conflict guidance.
- Add TypeScript contract updates and fixture guard coverage for the new `actionMenu` DTO.
- Add React source guard tests proving default UI renders action menu/forms and does not display slash command IDs outside advanced mode.
- Verify with frontend typecheck/build, focused .NET browser/command tests, and relevant browser smoke tests.

## Self-review

- No unresolved TBD/TODO placeholders.
- Scope is limited to read-only action menu metadata/rendering plus non-executing guided forms; it does not implement full player-default command execution.
- The design keeps gameplay/application authority in C# and React as presentation.
- Acceptance criteria are covered by metadata guards, realm-aware DTO, default UI rendering, and advanced separation tests.
