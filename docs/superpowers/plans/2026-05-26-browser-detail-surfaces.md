# Browser Detail Surfaces Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issue #728 by adding a reusable player-facing card → modal/full-panel detail-surface pattern to the React Browser Client.

**Architecture:** Keep C# as the authority and add only React presentation infrastructure. A reusable `DetailSurfaceCard` component owns compact-card rendering, modal/full-panel state, focus restoration, Escape handling, fullscreen toggle, and player-facing empty/loading/error states. `SoulRoute` and `WorldRoute` pass existing `/api/game-screen` DTO data into that component; no API contracts or runtime rules change.

**Tech Stack:** .NET 8/xUnit source guards and smoke tests; Vite + React + TypeScript; existing plain-CSS design-system files under `BookOfEternityClient.WebFrontend/src/styles/`.

---

## File structure

- Create `BookOfEternityClient.WebFrontend/src/components/DetailSurface.tsx` — reusable presentational detail-surface component.
- Modify `BookOfEternityClient.WebFrontend/src/App.tsx` — import the component and apply it to `SoulRoute` and `WorldRoute`.
- Modify `BookOfEternityClient.WebFrontend/src/styles/components.css` — component/modal/card visual styling.
- Modify `BookOfEternityClient.WebFrontend/src/styles/layout.css` — detail-surface grid and responsive full-panel behavior.
- Modify `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` — source guards for component, route use, styles, and debug-copy absence.
- Modify `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs` — generated `detail-surfaces.html` visual smoke artifact.
- Modify `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md` — document #728 pattern and artifact.

---

### Task 1: Add failing source guards for the reusable detail-surface pattern

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this `[Fact]` before `ReactAppShell_DocumentsIssue704RoutingAndPlayerAdvancedBoundary()`:

```csharp
    [Fact]
    public void BrowserDetailSurfaces_DefineReusableCardToModalPattern()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var detailSurfacePath = Path.Combine(FrontendRoot, "src", "components", "DetailSurface.tsx");
        Assert.True(File.Exists(detailSurfacePath), $"Missing {detailSurfacePath}");
        var detailSurface = File.ReadAllText(detailSurfacePath);
        var styles = ReadFrontendStyles();
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("export interface DetailSurfaceSection", detailSurface, StringComparison.Ordinal);
        Assert.Contains("export function DetailSurfaceCard", detailSurface, StringComparison.Ordinal);
        Assert.Contains("role=\"dialog\"", detailSurface, StringComparison.Ordinal);
        Assert.Contains("aria-modal=\"true\"", detailSurface, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Вернуться к карточке\"", detailSurface, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Развернуть панель подробностей\"", detailSurface, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Закрыть подробности\"", detailSurface, StringComparison.Ordinal);
        Assert.Contains("event.key === 'Escape'", detailSurface, StringComparison.Ordinal);
        Assert.Contains("triggerRef.current?.focus()", detailSurface, StringComparison.Ordinal);
        Assert.Contains("detail-surface-empty", detailSurface, StringComparison.Ordinal);
        Assert.Contains("detail-surface-error", detailSurface, StringComparison.Ordinal);
        Assert.Contains("detail-surface-loading", detailSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/", detailSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", detailSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", detailSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("advancedCommand", detailSurface, StringComparison.Ordinal);

        Assert.Contains("import { DetailSurfaceCard", app, StringComparison.Ordinal);
        Assert.Contains("<DetailSurfaceCard", app, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"soul-identity\"", app, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"player-condition\"", app, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"world-location\"", app, StringComparison.Ordinal);
        Assert.Contains("Детали души", app, StringComparison.Ordinal);
        Assert.Contains("Детали героя", app, StringComparison.Ordinal);
        Assert.Contains("Детали локации", app, StringComparison.Ordinal);

        Assert.Contains(".detail-surface-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".detail-surface-card", styles, StringComparison.Ordinal);
        Assert.Contains(".detail-surface-overlay", styles, StringComparison.Ordinal);
        Assert.Contains(".detail-surface-modal", styles, StringComparison.Ordinal);
        Assert.Contains(".detail-surface-modal.is-fullscreen", styles, StringComparison.Ordinal);
        Assert.Contains(".detail-surface-sections", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 640px)", styles, StringComparison.Ordinal);
        Assert.Contains(".detail-surface-modal", styles[styles.IndexOf("@media (max-width: 640px)", StringComparison.Ordinal)..], StringComparison.Ordinal);

        Assert.Contains("#728", readme, StringComparison.Ordinal);
        Assert.Contains("card → modal/full-panel", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("detail-surfaces.html", readme, StringComparison.Ordinal);
        Assert.Contains("#728", hostDoc, StringComparison.Ordinal);
        Assert.Contains("detail-surfaces.html", hostDoc, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDetailSurfaces_DefineReusableCardToModalPattern" --logger "console;verbosity=minimal"
```

Expected: FAIL because `DetailSurface.tsx`, route usages, styles, and docs are missing.

- [ ] **Step 3: Commit after it fails correctly?**

Do not commit a red-only task. Continue to Task 2 and commit once the minimal component/routes make this guard pass.

---

### Task 2: Implement the reusable DetailSurface component and apply it to routes

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/DetailSurface.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/layout.css`

- [ ] **Step 1: Create component**

Create `DetailSurface.tsx` with this public shape:

```tsx
import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';

export interface DetailSurfaceSection {
  title: string;
  eyebrow?: string;
  icon?: string;
  content: ReactNode;
}

export interface DetailSurfaceCardProps {
  detailSurfaceId: string;
  eyebrow: string;
  title: string;
  icon?: string;
  summary: ReactNode;
  status?: string;
  detailsTitle: string;
  detailsIntro?: ReactNode;
  sections: DetailSurfaceSection[];
  emptyMessage?: string;
  errorMessage?: string;
  loading?: boolean;
}

export function DetailSurfaceCard(props: DetailSurfaceCardProps) {
  // useState for open/fullscreen, refs for trigger and close button
  // button compact card -> dialog overlay when open
  // Escape closes; close restores focus to triggerRef
}
```

The completed implementation must include:

- a compact `<button className="detail-surface-card">` with `aria-haspopup="dialog"` and `aria-expanded={isOpen}`;
- modal overlay with `role="dialog"`, `aria-modal="true"`, and `aria-labelledby`;
- header controls with labels exactly `Вернуться к карточке`, `Развернуть панель подробностей`, `Свернуть панель подробностей`, `Закрыть подробности`;
- conditional player-facing `.detail-surface-loading`, `.detail-surface-error`, `.detail-surface-empty` states;
- Escape key close and focus restoration.

- [ ] **Step 2: Apply component in `App.tsx`**

Import:

```tsx
import { DetailSurfaceCard } from './components/DetailSurface';
```

In `SoulRoute`, replace the two flat soul/player `summary-card` blocks with a `detail-surface-grid` containing:

- `detailSurfaceId="soul-identity"`, title `Душа`, detailsTitle `Детали души`, sections for realm/incarnation and guardian/enlightenment;
- `detailSurfaceId="player-condition"`, title `Герой`, detailsTitle `Детали героя`, sections for identity and condition/status bars.

In `WorldRoute`, replace the flat location `summary-card` with:

- `detailSurfaceId="world-location"`, title `Локация`, detailsTitle `Детали локации`, sections for location/time and turn/session summary.

Do not change `ActionMenu`, `AdvancedDiagnosticsPanel`, command execution, prompt sessions, or DTO contracts.

- [ ] **Step 3: Add CSS**

In `components.css`, add styles for:

```css
.detail-surface-card { ... }
.detail-surface-card:hover,
.detail-surface-card:focus-visible { ... }
.detail-surface-overlay { ... }
.detail-surface-modal { ... }
.detail-surface-modal.is-fullscreen { ... }
.detail-surface-header { ... }
.detail-surface-controls { ... }
.detail-surface-section { ... }
.detail-surface-loading,
.detail-surface-error,
.detail-surface-empty { ... }
```

In `layout.css`, add `.detail-surface-grid` and mobile `@media (max-width: 640px)` rules that make `.detail-surface-overlay` fill the viewport and `.detail-surface-modal` behave as a full-screen panel.

- [ ] **Step 4: Run guard to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDetailSurfaces_DefineReusableCardToModalPattern" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 5: Run TypeScript gate**

Run:

```bash
npm run typecheck --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

---

### Task 3: Add dependency-light visual smoke artifact for opened detail surfaces

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`

- [ ] **Step 1: Write failing smoke assertions**

Extend `BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics` after `navigationArtifactPath` with:

```csharp
var detailSurfaceArtifactPath = Path.Combine(artifactRoot, "detail-surfaces.html");
```

After building `navigation-ia.html`, write:

```csharp
await File.WriteAllTextAsync(detailSurfaceArtifactPath, BuildDetailSurfaceArtifact(appSource));
```

Add assertions:

```csharp
Assert.True(File.Exists(detailSurfaceArtifactPath), $"Missing browser detail-surface visual smoke artifact at {detailSurfaceArtifactPath}");
var detailSurfaceArtifact = await File.ReadAllTextAsync(detailSurfaceArtifactPath);
Assert.Contains("data-artifact=\"browser-detail-surfaces\"", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("data-viewport=\"desktop\"", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("data-state=\"compact-cards\"", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("data-state=\"opened-modal\"", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("data-viewport=\"mobile\"", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("Душа", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("Детали души", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("Детали героя", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.Contains("Детали локации", detailSurfaceArtifact, StringComparison.Ordinal);
Assert.DoesNotContain("Debug", detailSurfaceArtifact, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("/api/", detailSurfaceArtifact, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("raw JSON", detailSurfaceArtifact, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Add artifact builder**

Add `BuildDetailSurfaceArtifact(string appSource)` near `BuildNavigationIaArtifact`. It should assert the source contains the three `detailSurfaceId` markers and return a static HTML artifact with compact cards, opened desktop modal, and mobile full-panel frames.

- [ ] **Step 3: Verify RED before implementation if Task 2 was not done**

Run before adding the builder only if Task 2 was skipped. Expected failure: missing `BuildDetailSurfaceArtifact` or missing source markers. If Task 2 is already green, proceed directly to builder and then run the focused built-frontend smoke after `npm run verify`.

- [ ] **Step 4: Run focused smoke after build**

Run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics" --logger "console;verbosity=minimal"
```

Expected: PASS and `TestResults/browser-smoke/detail-surfaces.html` generated (ignored artifact, do not commit `TestResults/`).

---

### Task 4: Document the #728 pattern

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Update frontend README**

Add a short section after the #727 navigation section:

```markdown
## Detail surfaces (#728)

Issue #728 adds a shared Browser Client `card → modal/full-panel` pattern for detail-rich player-facing data. Compact cards keep the route/sidebar overview readable; opening a card shows a consistent detail surface with header, back/fullscreen/close controls, readable sections, player-facing empty/error/loading copy, Escape handling, and focus restoration.

The first representative surfaces are the `Душа`, `Герой`, and `Локация` cards. They render existing `/api/game-screen` DTO data only; they do not add gameplay rules, raw API details, raw JSON, or slash-command diagnostics. Those remain behind explicit `Расширенный режим` where appropriate.

The built-frontend smoke test writes `TestResults/browser-smoke/detail-surfaces.html` as a dependency-light visual smoke artifact for compact cards, an opened desktop modal, and mobile full-panel behavior. Full screenshot automation remains a separate future task.
```

- [ ] **Step 2: Update local web host docs**

Add `#728` to the tracked task line and add one paragraph in the Current Browser MVP section explaining the same detail-surface pattern and `detail-surfaces.html` artifact.

- [ ] **Step 3: Re-run source guard**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDetailSurfaces_DefineReusableCardToModalPattern" --logger "console;verbosity=minimal"
```

Expected: PASS.

---

### Task 5: Final verification and commit

**Files:** all changed files from Tasks 1-4.

- [ ] **Step 1: Run frontend verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

- [ ] **Step 2: Run focused .NET guards/smoke**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDetailSurfaces_DefineReusableCardToModalPattern|FullyQualifiedName~BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 3: Run broad browser-related suite**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|Category=BrowserWebUiBuiltFrontend|Category=BrowserWebUiSmoke|Category=BrowserWebUiParity|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Check whitespace and generated artifacts**

```bash
git diff --check
git status --short
```

Expected: no whitespace errors. `TestResults/`, `BookOfEternityClient.WebFrontend/dist/`, `bin/`, and `obj/` remain uncommitted/generated.

- [ ] **Step 5: Commit only tracked issue files**

```bash
git add docs/superpowers/specs/2026-05-26-browser-detail-surfaces-design.md \
  docs/superpowers/plans/2026-05-26-browser-detail-surfaces.md \
  BookOfEternityClient.WebFrontend/src/components/DetailSurface.tsx \
  BookOfEternityClient.WebFrontend/src/App.tsx \
  BookOfEternityClient.WebFrontend/src/styles/components.css \
  BookOfEternityClient.WebFrontend/src/styles/layout.css \
  BookOfEternityClient.WebFrontend/README.md \
  docs/web-ui/local-web-host.md \
  BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs \
  BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs
git commit -m "feat(web-ui): add browser detail surfaces"
```

Expected: commit succeeds on branch `task/728-browser-detail-surfaces`.
