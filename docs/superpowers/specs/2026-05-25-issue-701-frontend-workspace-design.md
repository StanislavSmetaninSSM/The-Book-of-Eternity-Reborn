# Issue 701 Frontend Workspace Design

## Problem

Issue #701 is the first architecture slice for the Browser Client roadmap. The current browser shell is still mostly inline HTML/CSS/JavaScript inside `BookOfEternityClient/WebUi/LocalWebUiHost.cs`. That MVP proved the local API and DTO path, but it is not a maintainable place to grow the full game client, typed API layer, React shell, design system, audio/QTE/media screens, and browser smoke pipeline.

## Goal

Create a dedicated Vite + React + TypeScript workspace that can build independently from the C# runtime, while keeping the C# client layer as the authority for game rules, persistence, validation, command handling, and afterlife/mortal contracts. This slice establishes the frontend project structure and documented commands only; later issues wire the C# host to built assets and migrate real screens.

## Constraints

- The tracked task is GitHub issue #701, parent #680.
- Browser code is presentation and interaction plumbing only. It must not reimplement gameplay logic or copy old-project mechanics/prompts.
- The old TypeScript project is a UI/UX reference only for structure and comfort: React-like component organization, `App.tsx`, component/style split, and game-client feel.
- This issue should not alter `LocalWebUiHost.BuildShellHtml()` behavior yet; #702 is the host/static-asset serving task.
- No Afterlife runtime contract or GM-authored surface changes are expected, so GM-facing afterlife docs/tests are not required for this slice.

## Approaches considered

1. **Root-level `package.json` only.** Simple for `npm ci`, but it mixes frontend dependency state into the repository root before the frontend has its own lifecycle and makes later C# host output paths less explicit.
2. **Nested workspace under `BookOfEternityClient/WebUi/Frontend`.** Close to the host, but it risks making the .NET project tree contain a large Node project and generated files under the C# source directory.
3. **Sibling workspace `BookOfEternityClient.WebFrontend` (selected).** Keeps frontend ownership clear, avoids C# project glob interference, gives #702 a predictable build output to serve, and matches a future “frontend project” boundary without requiring a full monorepo package setup.

## Selected design

Add `BookOfEternityClient.WebFrontend/` as the Browser Client frontend workspace:

- `package.json` names the workspace `book-of-eternity-reborn-webfrontend`, marks it private, and exposes:
  - `npm run dev` for a Vite dev server bound to `127.0.0.1`;
  - `npm run typecheck` for strict TypeScript checks;
  - `npm run build` for typecheck + production bundle;
  - `npm run preview` for local preview of the built bundle.
- `vite.config.ts` uses React, writes production assets to `dist/`, and leaves the base path as `/` for later C# static hosting.
- `tsconfig*.json` enables strict browser and Vite/Node config typechecking.
- `src/App.tsx`, `src/main.tsx`, `src/styles.css`, and `src/vite-env.d.ts` create a small Russian-first shell placeholder that states the frontend is presentation-only and that the C# local API remains authoritative.
- `README.md` documents install/build/dev commands, the future C# host handoff for #702, and the rule that TypeScript must not own game mechanics.
- `.gitignore` ignores `node_modules`, Vite `dist`, and local frontend cache without hiding source files.

## Tests

Use TDD through a C# documentation/workspace guard before creating the workspace. Add `BrowserFrontendWorkspaceTests` that fails until:

- the workspace package manifest exists and has the required scripts/dependencies;
- strict TypeScript/Vite config files exist;
- source placeholders mention the C# runtime/API as the authority and avoid debug-shell wording as the product identity;
- the docs explain the frontend workspace commands and the #702 host integration boundary.

Then verify with:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests"`;
- `npm install` in `BookOfEternityClient.WebFrontend` to produce the lockfile;
- `npm run typecheck`;
- `npm run build`;
- focused `LocalWebUiDocumentationTests` to ensure browser docs stay coherent.

## Autonomous approval rationale

Stanislav explicitly authorized this worker to proceed unattended through GitHub issues. The selected slice is conservative: it adds a standalone frontend foundation and docs/tests, without changing runtime behavior, browser host endpoints, gameplay rules, persistence, or GM-facing contracts.

## Self-review

No placeholders remain. The design is scoped to issue #701 only; #702 serves built assets, #703 adds typed endpoint contracts, #704 builds the app shell, and #705 adds CI/smoke verification. The architecture keeps game/application logic in C# and treats the frontend as presentation-only.