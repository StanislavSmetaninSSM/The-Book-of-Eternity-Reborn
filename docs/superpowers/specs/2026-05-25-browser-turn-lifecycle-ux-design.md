# Browser Turn Lifecycle UX Design

Issue: [#686](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/686) — Browser Client turn lifecycle UX.

## Context

The Browser Client already has a React/Vite shell, `/api/game-screen`, `/api/lifecycle/dashboard`, session/write gating, validation summary, QTE read-only state, audio controls, and an action menu. The remaining gap for #686 is that the player-facing screen still treats turn lifecycle as a few ad-hoc labels. The UI needs an explicit lifecycle model that explains when a turn is idle, being composed, submitted, waiting for the GM, ready to accept, blocked by validation/repair, or restored after error.

Relevant references were reviewed before this design:

- `AGENTS.md` task/afterlife guardrails.
- `book-of-eternity-reborn` Browser Client references: design reference, session safety, player-vs-advanced UI, smoke/parity, Vite workspace, React app shell, autonomous cron workdir.
- Issue #686 body and acceptance criteria.

## Scope

This closure unit does not implement the future browser turn-writer. It makes the current read-only/game-screen lifecycle understandable, safe, and test-covered while preserving C# authority over game/application state.

In scope:

- Add a player-facing lifecycle phase model to the existing game-screen DTO.
- Keep existing `turnState.state` values for compatibility, but add canonical `phase` values for Browser UI state-machine logic.
- Add a fixed phase catalog containing the #686 states: `idle`, `composing-action`, `turn-submitted`, `waiting-gm`, `ready`, `accepted`, `validation-failed`, `repair-required`, `error-restored`, and `cancelled`.
- Add recommended player/advanced actions for each current phase so the UI can explain what is safe.
- Render lifecycle phases and actions in React without exposing raw validation issue details in the default UI.
- Preserve advanced diagnostics as the place for raw validation, endpoint, command, and technical details.
- Add regression tests for main lifecycle branches and frontend source guards.

Out of scope:

- Mutating `game_session` from the default prose composer.
- Changing GM/afterlife contract files or runtime surfaces.
- Adding new lifecycle artifacts to `game_state/control/`.
- Building the full settings/media/command parity work from issues #687–#689.

## Chosen Approach

Use the current C# browser services as the source of truth and extend `BrowserGameScreenTurnStateDto` with explicit lifecycle metadata:

- `phase`: current canonical phase for the player UI.
- `phaseLabel`: short Russian player-facing label.
- `severity`: `success`, `warning`, `error`, or `info`.
- `playerGuidance`: one clear sentence for the current phase.
- `recommendedActions`: safe next actions, each tagged as `player-default` or `advanced-only`.
- `knownPhases`: full phase catalog for contract tests and future UI routing.

The React app renders these fields in the ordinary game route and sidebar. Raw validation issue lists remain in the advanced diagnostics panel only.

## Lifecycle Mapping

Current artifacts map to canonical phases as follows:

| Condition | Existing state | Canonical phase | Default UI behavior |
| --- | --- | --- | --- |
| `ready/turn_error.json` exists | `gm-turn-error` | `error-restored` | Block input, explain rollback/repair, point to advanced repair. |
| `ready/turn_complete.json` exists | `ready-gm-response` | `ready` | Block new writes, tell player GM response is ready to accept. |
| pending snapshot/rollback artifacts exist | `pending-turn-repair` | `repair-required` | Block input, explain repair is required before play. |
| `input/turn_request.json` or other active pending artifact exists | `pending-gm-turn` | `waiting-gm` | Block local actions, show waiting-for-GM screen copy. |
| QTE offer/active state exists | `qte` | `composing-action` | Block prose composer and route player to QTE action UI. |
| validation errors exist without pending turn | `validation-errors` | `validation-failed` | Block writes and show player-safe repair summary. |
| local UI lock blocks writes without pending turn | `blocked` | `turn-submitted` | Explain another local/browser write is in progress. |
| no blockers and browser writes allowed | `ready` | `idle` | Prose composer can prepare the next action. |

`accepted` and `cancelled` are catalog phases because they are short-lived transition outcomes in the shared turn lifecycle. The current read-only game-screen cannot detect them durably without adding new runtime artifacts, so the design exposes them in the catalog but does not invent fake current states.

## Components and Files

- `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
  - Extend turn-state DTO records.
  - Add phase/action helper methods.
  - Keep compatibility fields and current safe-write behavior.

- `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
  - Add TypeScript interfaces for lifecycle actions and phases.

- `BookOfEternityClient.WebFrontend/src/App.tsx`
  - Render phase label, guidance, recommended actions, and phase catalog in player-facing copy.
  - Keep validation details behind advanced mode.

- `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
  - Add smoke tests for active waiting, ready response, repair, error restored, and validation-failed phase mapping.

- `BookOfEternityClient.Tests/BrowserApiContractTests.cs` and `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`
  - Update representative DTO/fixture and source guard tests for the new contract fields.

## Testing Strategy

Follow TDD:

1. Add tests expecting `phase`, `phaseLabel`, `playerGuidance`, `recommendedActions`, and the full known phase catalog in `/api/game-screen`.
2. Run focused tests and confirm they fail because the fields do not exist yet.
3. Implement the C# DTO mapping and fixture/TypeScript contract changes.
4. Add React source guard tests and UI rendering.
5. Run focused .NET and frontend typecheck/build checks.

Required verification before PR:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|ExplorerWeb|CommandMigration" --logger "console;verbosity=minimal"
git diff --check
```

Run the full test project before merge if time allows.

## Docs/Prompts Impact

No afterlife or mortal-world runtime contract changes are introduced. No GM-facing afterlife docs are required for this PR. The new contract is Browser UI DTO-only, with coverage in browser API contract tests and frontend source tests.

## Self-review

- Placeholder scan: no TBD/TODO placeholders remain.
- Consistency check: approach keeps C# authority and React as presentation only.
- Scope check: focused on #686, not #687–#689.
- Ambiguity check: `accepted` and `cancelled` are explicitly catalog-only until a durable runtime surface exists.
