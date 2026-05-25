# Issue #703 Typed Browser API Contract Design

Tracked issue: #703 — [Browser Client Architecture] Add typed TypeScript API contract for local web endpoints.

## Context

Issues #701 and #702 established the dedicated `BookOfEternityClient.WebFrontend/` Vite/React/TypeScript workspace and connected it to the C# loopback host. The next Browser Client architecture slice needs a maintainable contract between React code and the existing C# Minimal API endpoints (`/api/main-menu`, `/api/session`, `/api/game-screen`, lifecycle, Explorer commands, QTE, media). Without a typed client and contract fixture checks, later UI tasks can scatter untyped `fetch()` calls and drift away from C# DTOs.

Stanislav authorized unattended execution, so this design is self-approved after review. The slice is deliberately conservative: TypeScript gains endpoint types, normalized request/error handling, and contract fixtures, while C# remains the only authority for game rules, mutation, validation, local-write gating, and afterlife/mortal contracts.

## Approaches considered

1. **Generate OpenAPI from the Minimal API.** This would be ideal long-term, but the current endpoints return records, polymorphic command blocks, anonymous error objects, and in-memory prompt sessions without an existing OpenAPI pipeline. Adding generation now would make #703 mostly tooling work and delay the actual frontend contract layer.
2. **Hand-written TypeScript DTOs with fixture-backed contract tests (selected).** Add explicit TypeScript interfaces and a typed API client, then bridge C# and TypeScript through JSON fixtures: .NET tests serialize representative C# DTOs and compare them to tracked frontend fixtures; TypeScript imports the same fixtures with `satisfies` checks. This is small, reviewable, and catches common DTO-breaking changes before runtime.
3. **Only document endpoint JSON shapes.** Documentation helps humans but does not make `npm run typecheck` or .NET tests fail when DTOs drift, so it does not satisfy the regression goal.

## Selected architecture

Add a typed API layer under `BookOfEternityClient.WebFrontend/src/api/`:

- `contracts.ts` defines DTO interfaces, request shapes, command UI block unions, QTE shapes, local-write/session/lifecycle state, media response notes, and a normalized `BrowserApiResult<T>` union.
- `client.ts` exposes `createBrowserApiClient()` and a default `browserApi` singleton. Components call methods such as `getMainMenu()`, `getSessionStatus()`, `getGameScreen()`, `validateLifecycle()`, `executeExplorerCommand()`, `submitPromptSession()`, `loadSave()`, and QTE methods instead of raw `fetch()`.
- `contract-fixtures/*.json` stores representative JSON for the main menu, session status, game screen, lifecycle dashboard, Explorer command result, QTE state, and a common error payload.
- `contract-fixture-checks.ts` imports those fixtures and checks them with TypeScript `satisfies` assignments. Because `tsconfig.app.json` includes all `src`, `npm run typecheck` validates the fixtures even before #704 builds the full app shell.
- `App.tsx` consumes the typed contract metadata exported by the API layer so the current React shell demonstrates that future components should depend on the typed client boundary, not endpoint strings or ad-hoc JSON.

## C# contract guard

Add `BookOfEternityClient.Tests/BrowserApiContractTests.cs` to connect the TypeScript fixtures to the C# DTOs:

- Build representative C# DTOs for main-menu, session-status, game-screen, lifecycle-dashboard, Explorer command result, QTE state, and the common load-save error payload.
- Serialize them with the same `JsonSerializerDefaults.Web` style used by `LocalWebUiHost`.
- Parse and compare them semantically with the tracked frontend JSON fixtures.
- Guard that `src/api/client.ts`, `src/api/contracts.ts`, `src/api/contract-fixture-checks.ts`, frontend README, and local web-host docs document/use the typed contract.

This is not a full schema generator. It is a regression net for the browser-critical C# DTO path and a clear hand-off for later OpenAPI/schema generation if the Minimal API surface matures.

## Request and error handling

The TypeScript client normalizes common endpoint outcomes into a discriminated union:

- `ok: true` with typed `data` for successful JSON responses.
- `ok: false` with `kind`, `status`, `message`, player-facing `playerMessage`, and optional `technicalDetails` for failures.
- Explicit `kind` values cover validation errors, pending GM turn / blocked local write, not-found / no active session, server diagnostics for advanced mode, HTTP errors, and network errors.

Default player-facing UI should show `playerMessage`. `technicalDetails` is reserved for explicit advanced/diagnostic mode so #704 can preserve the player-vs-advanced separation.

## Boundaries and non-goals

- Do not move command execution, validation, game state mutation, local-write locking, QTE resolution rules, or afterlife/mortal mechanics into TypeScript.
- Do not expose GM/debug-only notes through player-facing DTO types. The typed client may carry technical details only in the normalized advanced diagnostics path.
- Do not implement the full routed React shell in #703; that is #704.
- Do not add CI/browser smoke here beyond focused frontend typecheck/build and .NET contract tests; #705 owns the broader verification pipeline.

## Testing strategy

1. RED: add .NET tests that fail until the TypeScript API files, contract fixtures, fixture typecheck file, README/docs contract text, and app usage exist.
2. GREEN: add the TypeScript contract/client/fixtures and documentation needed to pass the focused tests.
3. Verify with:
   - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`
   - `npm run typecheck --prefix BookOfEternityClient.WebFrontend`
   - `npm run build --prefix BookOfEternityClient.WebFrontend`
   - `git diff --check`
4. Before PR/merge, run the broader relevant browser test filter and full test project if practical.

## Documentation impact

Update:

- `BookOfEternityClient.WebFrontend/README.md` with typed client commands, files, contract update workflow, and fixture-test expectations.
- `docs/web-ui/local-web-host.md` with #703 in tracked tasks, typed API contract notes, normalized error handling, and safe contract update steps.
- `docs/superpowers/plans/2026-05-25-issue-703-typed-api-contract.md` for implementation handoff.

No afterlife runtime contract or GM-authored gameplay prompt changes are planned; GM-facing afterlife docs/tests are not required for this slice.

## Self-review

- Placeholder scan: no TBD/TODO placeholders.
- Consistency: the design keeps C# as runtime authority and gives TypeScript typed consumption plus normalized request state only.
- Scope: one closure unit for #703; #704 handles the routed app shell and #705 handles CI/smoke expansion.
- Ambiguity resolved: hand-written types with fixture-backed contract tests are selected over OpenAPI generation for this slice.
