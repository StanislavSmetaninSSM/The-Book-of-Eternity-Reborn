# Browser New-Chapter Launcher Flow Design (#742)

## Context

Issue #742 comes from the Browser Client visual/player-experience audit. The current `Главная` launcher exposes `Начать новую главу`, but the panel only describes a future browser form and leaves the player without fields, a submit path, or a truthful unavailable state. The existing C# browser command flow already exposes `/world_setup` through `BrowserMainMenuDto.actions` and `ExplorerWebCommandService`, where the command returns `UiPrompt` fields for preparing the next world/life. React should present that existing flow instead of inventing separate gameplay rules.

Stanislav explicitly authorized unattended execution, so this spec records the approval gate decision: proceed conservatively inside tracked issue #742, keep C# as the gameplay/application authority, and use the existing browser command/prompt-session APIs as presentation plumbing only.

## Goals

- Make `Начать новую главу` actionable from the player-facing launcher when the C# menu action is enabled.
- Render the existing C# prompt-session form and submit path in the launcher, using the same `ActionCommandResult` and `renderPromptControl` machinery as world action cards.
- Show a truthful unavailable state when the menu action is disabled or does not expose a command; do not promise a form unless fields/actions can be rendered.
- Keep raw slash commands, endpoint names, command IDs, and diagnostics out of the default player-facing copy.
- Add a dependency-light visual smoke artifact covering the start-new-chapter interaction.

## Approach Chosen

Use a focused React presentation change in `BookOfEternityClient.WebFrontend/src/App.tsx`. Replace the bare `new-game` launcher panel with a dedicated `NewChapterStartPanel` component that receives the `BrowserMainMenuDto` action, calls `browserApi.executeExplorerCommand({ command: startCommand, ownerLabel: 'Главная книга' })`, stores a player-default sanitized `ExplorerCommandResult`, and renders `ActionCommandResult` when the command returns prompts or messages. Prompt submission reuses `browserApi.submitPromptSession` and `buildDefaultPromptAnswers`. If the action is disabled or has no command, the panel renders C# disabled/guidance text and disables the button.

The independent review pass found that raw `UiBlock` content from the C# world-setup command can contain slash commands, client-owned state identifiers, file paths, and raw JSON labels. The closure therefore adds a small pure TypeScript sanitizer (`src/playerFacingCommandResult.ts`) used only for player-default command-result presentation. It removes technical blocks/actions and rewrites unsafe notifications/prompts to player-safe fallback text while preserving the prompt-session fields needed to submit the form.

This is safer than adding a new browser endpoint because `/api/explorer/command` and `/api/explorer/prompt-sessions/submit` already centralize migration status, prompt-session leases, and local-write ownership. It also avoids a separate TypeScript start-game model.

## Components and Data Flow

- `GameLauncher.renderModeContent()` delegates `activeMode === 'new-game'` to `NewChapterStartPanel`.
- `NewChapterStartPanel` derives availability from `findLauncherMenuAction(menu, 'new-game')` and uses that action's `command` value without rendering it.
- Opening the flow calls `browserApi.executeExplorerCommand`, sanitizes the result through `sanitizePlayerDefaultCommandResult`, and initializes `promptAnswers` from `result.data.prompts` when successful.
- Submitting the launcher form calls `browserApi.submitPromptSession` with `interactiveSession.sessionId`, `ownerId`, and the controlled answers, then applies the same player-default sanitizer before rendering.
- `ActionCommandResult`, `renderPromptControl`, `buildDefaultPromptAnswers`, and `toCommandNotice` remain shared between world action cards and the launcher flow; the launcher passes sanitized data so default Home UI does not render technical `UiBlock` content.

## Error Handling

- Missing action or missing command: show `Подготовка новой главы пока недоступна из браузерного меню` and explain to continue/load/check local state.
- Disabled action: show the C# disabled reason through `toPlayerFacingText` and keep the launch button disabled.
- API/network failure: show `result.playerMessage` through player-facing notice text.
- Validation/recoverable field errors: browser-native `required` controls remain on prompt fields; command results and notifications remain visible in the same panel.
- Advanced/raw details stay in explicit advanced diagnostics, not in the launcher.
- Player-default command results drop technical blocks/actions and fallback unsafe notification/prompt text containing slash commands, endpoint names, raw JSON labels, local paths, or client-owned state identifiers.

## Testing

- Add a failing source guard in `BrowserFrontendWorkspaceTests` proving the new launcher panel owns open/submit handlers, calls the typed API command/prompt-session flow, renders `ActionCommandResult`, and no longer promises a form in static copy.
- Add a failing built-frontend smoke expectation that writes and verifies `TestResults/browser-smoke/start-new-chapter-flow.html` with desktop/mobile frames and no `/api/`, raw command, debug, or screenshot claims.
- Add a failing pure TypeScript fixture test for representative unsafe world-setup command-result content. The fixture asserts the player-default sanitizer removes slash commands, endpoint names, raw JSON labels, file paths, client-owned state identifiers, and raw command actions while preserving safe form fields.
- Implement the minimal React/CSS/smoke/documentation changes.
- Verify with focused .NET tests, `npm run verify --prefix BookOfEternityClient.WebFrontend`, broader browser/local-web tests, `git diff --check`, and an added-line static scan.

## Scope Boundaries

No C# runtime contract, pending/control file, afterlife action type, validation rule, or GM-authored prompt behavior changes are planned. No GM-facing afterlife documentation update is required. The browser flow opens the existing world-setup prompt form; it does not complete a full new-game reset or add new mortal-world mechanics.

## Self-Review

- Placeholder scan: no TBD/TODO placeholders.
- Consistency: the approach satisfies #742 while preserving the Browser Client C# authority boundary.
- Scope: one UI closure unit tied to #742; broader first-screen/no-session repair issues remain separate audit tasks.
- Ambiguity: if the C# action cannot provide a command or is blocked by local-write state, the UI uses a truthful unavailable state rather than promising a form.
