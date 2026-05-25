# Browser Command Coverage Parity Design

Tracked issue: #687 — [Browser Client] Полное покрытие команд и parity audit с консольным ExplorerMode.

## Context

The Browser Client already has a shared `ExplorerCommandCatalog`, generated migration registry metadata, a browser-safe command service, and a contextual action menu in `/api/game-screen`. The remaining gap for #687 is that command coverage is not visible as a single audit surface: agents and players in advanced mode must infer coverage from tests, source files, or the action menu. New commands also need a stronger guard that proves every descriptor and subcommand has a concrete browser UX decision, not only a raw enum value.

This design follows the Browser Client references: C# remains the command/runtime authority, React/TypeScript renders typed DTOs only, default player UI remains Russian-first, and raw slash/API details stay behind explicit `Расширенный режим`.

## Approach

Add an advanced-only, read-only command coverage endpoint backed directly by `ExplorerCommandCatalog` and browser action metadata:

- `GET /api/explorer/command-coverage` returns a machine-readable `BrowserCommandCoverageDto`.
- The DTO lists every command descriptor with id, aliases, group, mutation mode, browser status, handler kind, UX decision, surface (`player-default` or `advanced-only`), form mode, primary action label, primary command, follow-up/reason, and subcommand coverage.
- `BrowserPlayerCommandMenuBuilder` exposes a small read-only metadata helper so the coverage service and the player action menu share the same section/label/surface/form-mode decisions instead of duplicating presentation rules.
- The React advanced diagnostics panel lazy-loads and renders the coverage matrix only after `Расширенный режим` is enabled. The default player routes do not fetch or display command ids/API details.

## UX decision vocabulary

The coverage DTO uses explicit UX decision strings:

- `contextual-button` — read-only player action card opens a browser result.
- `guided-form` — mutating browser parity command opens/submits the existing prompt-session form path.
- `advanced-diagnostics` — command is intentionally excluded from default player sections and belongs to advanced mode.
- `status-card` — status-only browser surface, if introduced later.
- `guided-form-pending` — interactive browser form surface is present but full parity is tracked by a follow-up.
- `planned`, `blocked`, `console-only` — known non-executable commands with follow-up/reason.

## Testing

Test-first implementation should add failing tests before production code:

1. Contract tests prove `BrowserCommandCoverageService.Build()` lists every descriptor and subcommand, exposes non-empty UX decisions, separates advanced-only commands, and keeps player-default browser executable commands in a player surface.
2. Host smoke test proves `GET /api/explorer/command-coverage` returns JSON and includes representative command/subcommand coverage.
3. Frontend contract tests prove TypeScript contracts/client/fixtures know the endpoint and DTO.
4. Frontend source guard proves the React shell fetches command coverage only when advanced mode is enabled and renders the matrix in `AdvancedDiagnosticsPanel`.

## Documentation and contracts

Update `docs/web-ui/local-web-host.md` because the local web API surface changes. This is not an Afterlife runtime contract change: no `game_state/control/*` files, Shining/Chaos pending actions, GM-authored response fields, validation rules, or authority paths change. GM-facing afterlife docs are therefore not updated.

## Self-review

- No placeholders or TBDs remain.
- Scope is one closure unit: read-only coverage audit surface plus tests/docs/frontend rendering.
- The design avoids gameplay logic in React and avoids exposing command/API details in default player routes.
- The coverage service is deterministic and fixture-friendly; no timestamps are included in the DTO.
