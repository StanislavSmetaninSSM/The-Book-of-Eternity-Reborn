# Issue 685 Browser Design System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Browser Client React shell into a maintainable dark-fantasy game-client design system with CSS asset structure, Russian player-facing defaults, responsive layout, and restrained motion.

**Architecture:** Keep `src/styles.css` as the single Vite import entrypoint, but make it import focused CSS files under `src/styles/`. Add .NET source guard tests for the design-system contract, then split/enhance CSS, add small semantic React hooks/classes, and document the boundary. React/CSS remain presentation-only; C# remains gameplay/runtime authority.

**Tech Stack:** .NET 8 xUnit guard tests, Vite + React + TypeScript, plain CSS modules imported through Vite, GitHub issue #685.

---

## File structure

- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` — add source guard tests and a helper that reads all frontend CSS files.
- Modify: `BookOfEternityClient.WebFrontend/src/styles.css` — convert to design-system CSS aggregator.
- Create: `BookOfEternityClient.WebFrontend/src/styles/tokens.css` — reusable design tokens and realm/state variables.
- Create: `BookOfEternityClient.WebFrontend/src/styles/base.css` — base document, typography, texture, focus, scrollbar styles.
- Create: `BookOfEternityClient.WebFrontend/src/styles/components.css` — cards, panels, buttons, forms, alerts, action/audio components.
- Create: `BookOfEternityClient.WebFrontend/src/styles/layout.css` — shell, hero, navigation, workspace, route layout, responsive rules.
- Create: `BookOfEternityClient.WebFrontend/src/styles/motion.css` — animations and reduced-motion safeguards.
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx` — add semantic visual hooks and replace default English/technical labels in player UI.
- Modify: `BookOfEternityClient.WebFrontend/README.md` — document #685 design system structure.
- Modify: `docs/web-ui/local-web-host.md` — add #685 to tracked tasks and frontend/design-system documentation.

---

### Task 1: Add RED design-system source guards

**Objective:** Make tests fail until the CSS design system structure and player-facing copy contract exist.

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Test: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write the failing tests and CSS helper**

Add this helper near the bottom of `BrowserFrontendWorkspaceTests` before the closing brace:

```csharp
    private static string ReadFrontendStyles()
    {
        var paths = new[]
        {
            Path.Combine(FrontendRoot, "src", "styles.css"),
            Path.Combine(FrontendRoot, "src", "styles", "tokens.css"),
            Path.Combine(FrontendRoot, "src", "styles", "base.css"),
            Path.Combine(FrontendRoot, "src", "styles", "components.css"),
            Path.Combine(FrontendRoot, "src", "styles", "layout.css"),
            Path.Combine(FrontendRoot, "src", "styles", "motion.css"),
        };

        return string.Join("\n", paths.Where(File.Exists).Select(File.ReadAllText));
    }
```

Change `ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn` so the style read becomes:

```csharp
        var styles = ReadFrontendStyles();
```

Then add this new test after `ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn`:

```csharp
    [Fact]
    public void BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens()
    {
        var entryStyles = File.ReadAllText(Path.Combine(FrontendRoot, "src", "styles.css"));
        var styles = ReadFrontendStyles();
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        foreach (var fileName in new[] { "tokens.css", "base.css", "components.css", "layout.css", "motion.css" })
        {
            Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "styles", fileName)), $"Missing frontend design-system CSS file {fileName}");
            Assert.Contains($"./styles/{fileName}", entryStyles, StringComparison.Ordinal);
        }

        Assert.Contains("--color-ink", styles, StringComparison.Ordinal);
        Assert.Contains("--color-parchment", styles, StringComparison.Ordinal);
        Assert.Contains("--realm-chaos", styles, StringComparison.Ordinal);
        Assert.Contains("--realm-shining", styles, StringComparison.Ordinal);
        Assert.Contains("--state-repair", styles, StringComparison.Ordinal);
        Assert.Contains("--state-qte", styles, StringComparison.Ordinal);
        Assert.Contains("--motion-panel", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", styles, StringComparison.Ordinal);
        Assert.Contains(".design-system-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card--game", styles, StringComparison.Ordinal);
        Assert.Contains(".narrative-card.is-featured", styles, StringComparison.Ordinal);
        Assert.Contains(".shell-panel[data-panel='turn']", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 640px)", styles, StringComparison.Ordinal);

        Assert.Contains("Книга Вечности: Перерождение", app, StringComparison.Ordinal);
        Assert.Contains("data-theme-key={realmTheme.key}", app, StringComparison.Ordinal);
        Assert.Contains("route-card--${route.id}", app, StringComparison.Ordinal);
        Assert.Contains("variant=\"turn\"", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Book of Eternity Reborn · Browser Client", app, StringComparison.Ordinal);
        Assert.DoesNotContain("player-facing", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Текущий realm", app, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("#685", readme, StringComparison.Ordinal);
        Assert.Contains("src/styles/tokens.css", readme, StringComparison.Ordinal);
        Assert.Contains("dark-fantasy", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#685", hostDoc, StringComparison.Ordinal);
        Assert.Contains("design-system", hostDoc, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run focused test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens" --logger "console;verbosity=minimal"
```

Expected: FAIL because `tokens.css` and the new design-system imports/classes/docs do not exist yet.

- [ ] **Step 3: Commit the RED test**

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs
git commit -m "test: add browser design system source guards"
```

---

### Task 2: Create the CSS design-system files

**Objective:** Split the current CSS blob into maintainable design-system assets and add the visual token foundation required by #685.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles.css`
- Create: `BookOfEternityClient.WebFrontend/src/styles/tokens.css`
- Create: `BookOfEternityClient.WebFrontend/src/styles/base.css`
- Create: `BookOfEternityClient.WebFrontend/src/styles/components.css`
- Create: `BookOfEternityClient.WebFrontend/src/styles/layout.css`
- Create: `BookOfEternityClient.WebFrontend/src/styles/motion.css`
- Test: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Replace `src/styles.css` with imports**

```css
@import './styles/tokens.css';
@import './styles/base.css';
@import './styles/components.css';
@import './styles/layout.css';
@import './styles/motion.css';
```

- [ ] **Step 2: Create `tokens.css` with exact token names**

```css
:root {
  color-scheme: dark;
  --font-ui: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  --font-display: Georgia, "Times New Roman", serif;
  --font-narrative: Georgia, "Times New Roman", serif;
  --color-ink: #07090a;
  --color-ink-2: #0d1416;
  --color-obsidian: #111719;
  --color-parchment: #f5eddd;
  --color-parchment-muted: #cfc2aa;
  --color-gold: #d8b36a;
  --color-gold-bright: #f7dfa3;
  --color-glass: rgba(13, 20, 22, 0.82);
  --color-glass-strong: rgba(7, 10, 11, 0.9);
  --realm-mortal: #d8b36a;
  --realm-chaos: #9c6dff;
  --realm-shining: #8ee6d1;
  --state-success: #91d6a4;
  --state-warning: #f2d99e;
  --state-danger: #ff8d7a;
  --state-repair: #f0a85a;
  --state-qte: #ff5f9e;
  --radius-sm: 0.75rem;
  --radius-md: 1rem;
  --radius-lg: 1.4rem;
  --radius-xl: 2rem;
  --space-1: 0.35rem;
  --space-2: 0.55rem;
  --space-3: 0.8rem;
  --space-4: 1rem;
  --space-5: 1.4rem;
  --space-6: 2rem;
  --shadow-panel: 0 1.4rem 4rem rgba(0, 0, 0, 0.28);
  --shadow-glow: 0 0 2rem color-mix(in srgb, var(--realm-accent, var(--color-gold)) 24%, transparent);
  --motion-fast: 140ms ease-out;
  --motion-panel: 280ms cubic-bezier(0.2, 0.8, 0.2, 1);
  --realm-accent: var(--realm-mortal);
}
```

- [ ] **Step 3: Create `base.css`, `components.css`, `layout.css`, and `motion.css`**

Use the existing `styles.css` rules as the baseline, preserving all selectors currently used by `App.tsx`, and distribute them by responsibility. Add these required selectors while moving the existing rules:

```css
.design-system-grid { display: grid; gap: var(--space-4); }
.route-card--game { background: color-mix(in srgb, var(--realm-accent) 18%, rgba(18, 26, 28, 0.9)); }
.narrative-card.is-featured { border: 1px solid color-mix(in srgb, var(--realm-accent) 42%, rgba(255, 255, 255, 0.08)); box-shadow: var(--shadow-glow); }
.shell-panel[data-panel='turn'] { border-color: color-mix(in srgb, var(--state-repair) 34%, rgba(255, 255, 255, 0.08)); }
@media (max-width: 640px) { .route-grid { grid-template-columns: 1fr; } }
```

Also add a `prefers-reduced-motion` block in `motion.css` that disables nonessential animation:

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 1ms !important;
    animation-iteration-count: 1 !important;
    scroll-behavior: auto !important;
    transition-duration: 1ms !important;
  }
}
```

- [ ] **Step 4: Run focused style guard**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens|FullyQualifiedName~ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn" --logger "console;verbosity=minimal"
```

Expected: still FAIL until the App copy/hooks and docs are updated in Tasks 3-4, but style-file missing failures should be gone.

- [ ] **Step 5: Commit CSS structure**

```bash
git add BookOfEternityClient.WebFrontend/src/styles.css BookOfEternityClient.WebFrontend/src/styles/tokens.css BookOfEternityClient.WebFrontend/src/styles/base.css BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.WebFrontend/src/styles/layout.css BookOfEternityClient.WebFrontend/src/styles/motion.css
git commit -m "feat: add browser design system css structure"
```

---

### Task 3: Add semantic React design hooks and Russian player-facing labels

**Objective:** Expose visual variants to CSS and remove default English/technical wording from the player UI.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Test: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Update shell root and hero copy**

Change the shell root to include the realm key:

```tsx
<main className="browser-shell" data-theme-key={realmTheme.key} style={{ '--realm-accent': realmTheme.accent } as CSSProperties}>
```

Change the hero eyebrow and aria label:

```tsx
<p className="eyebrow">Книга Вечности: Перерождение · локальный клиент</p>
...
<div className="hero-status" aria-label="Текущий слой мира">
```

- [ ] **Step 2: Add route-card visual variants**

Change route button className:

```tsx
className={`route-card route-card--${route.id}${activeRoute === route.id ? ' is-active' : ''}`}
```

- [ ] **Step 3: Add panel variants and stronger narrative hook**

Change `ShellPanel` signature and rendered section:

```tsx
function ShellPanel({
  title,
  eyebrow,
  children,
  nested = false,
  variant
}: {
  title: string;
  eyebrow: string;
  children: ReactNode;
  nested?: boolean;
  variant?: string;
}) {
  const className = ['shell-panel', nested ? 'is-nested' : '', variant ? `panel-${variant}` : '']
    .filter(Boolean)
    .join(' ');

  return (
    <section className={className} data-panel={variant ?? title}>
      <p className="panel-eyebrow">{eyebrow}</p>
      <h2>{title}</h2>
      {children}
    </section>
  );
}
```

Update the turn panel and choice panel:

```tsx
<ShellPanel title="Состояние хода" eyebrow={game.turnState.state} nested variant="turn">
...
<ShellPanel title="Варианты" eyebrow="для игрока" nested variant="choices">
```

Change the narrative card class:

```tsx
<article className="narrative-card is-featured">
```

- [ ] **Step 4: Run focused guard to verify GREEN for App hooks**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens|FullyQualifiedName~ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn" --logger "console;verbosity=minimal"
```

Expected: design-system test may still fail on docs until Task 4; React source assertions should pass.

- [ ] **Step 5: Commit App hooks**

```bash
git add BookOfEternityClient.WebFrontend/src/App.tsx
git commit -m "feat: add browser design system shell hooks"
```

---

### Task 4: Document #685 and run verification

**Objective:** Document the design system and prove the closure unit locally before review/PR.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`
- Test: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Update frontend README**

Add a section titled `## Browser design system (#685)` after the React app shell section. Include these exact file paths and rules:

```markdown
## Browser design system (#685)

Issue #685 splits the Browser Client styling into a maintainable plain-CSS design system:

- `src/styles.css` remains the Vite import entrypoint.
- `src/styles/tokens.css` defines dark-fantasy color, realm, state, typography, spacing, shadow, and motion tokens.
- `src/styles/base.css` owns document reset, typography, background texture, scrollbars, and focus treatment.
- `src/styles/components.css` owns reusable cards, panels, buttons, forms, alert states, action cards, audio controls, and advanced diagnostics.
- `src/styles/layout.css` owns shell, hero, route, workspace, route-grid, and responsive layout rules.
- `src/styles/motion.css` owns restrained panel/QTE/waiting motion plus `prefers-reduced-motion` safeguards.

The visual direction is dark-fantasy chronicle UI: ink/obsidian background, parchment/gold narrative hierarchy, realm-aware accents from the C# game-screen DTO, clear desktop/mobile breakpoints, and technical labels only inside explicit advanced mode. CSS/React stay presentation-only; gameplay, validation, saves, commands, and afterlife contracts remain in the C# runtime.
```

- [ ] **Step 2: Update local web host docs**

Add `#685` to the tracked tasks list at the top of `docs/web-ui/local-web-host.md`. Add this paragraph to the `## Frontend Workspace` section after the existing React shell paragraphs:

```markdown
Issue #685 adds the Browser Client design-system layer. The frontend keeps `src/styles.css` as the single Vite import entrypoint, but the maintainable styling lives under `BookOfEternityClient.WebFrontend/src/styles/`: `tokens.css`, `base.css`, `components.css`, `layout.css`, and `motion.css`. This is a dark-fantasy, Russian-first, player-facing visual system over the same C# local APIs; it must not move gameplay rules, validation, persistence, command execution, or afterlife/mortal contracts into TypeScript.
```

- [ ] **Step 3: Run focused source tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens|FullyQualifiedName~ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn|FullyQualifiedName~ReactAppShell_DocumentsIssue704RoutingAndPlayerAdvancedBoundary" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Run frontend verification**

Run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: typecheck and Vite build succeed.

- [ ] **Step 5: Run browser-focused .NET tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|Category=BrowserWebUiBuiltFrontend|Category=BrowserWebUiSmoke|Category=BrowserWebUiParity" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 6: Run diff/security checks**

Run:

```bash
git diff --check
git diff HEAD -- BookOfEternityClient.WebFrontend BookOfEternityClient.Tests docs | grep '^+' | grep -iE '(api_key|secret|password|token|passwd)\s*=\s*["'\'''][^"'\''']{6,}["'\''']|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f"|\.format\(.*SELECT|\.format\(.*INSERT' || true
```

Expected: no whitespace errors and no security findings.

- [ ] **Step 7: Commit docs**

```bash
git add BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs
git commit -m "docs: document browser design system"
```

---

## Plan self-review

- Spec coverage: tasks cover maintainable CSS structure, visual direction, Russian/default UI boundary, desktop/mobile responsiveness, motion/reduced-motion, tests, docs, and verification.
- Placeholder scan: no TBD/TODO/fill-in placeholders remain.
- Type consistency: file paths and test names are consistent across tasks. `ReadFrontendStyles()` is used by both existing and new guard tests.
- Scope check: this closes the #685 design-system foundation without implementing #686 lifecycle UX, #687 parity audit, #688 media/map/QTE feature depth, or #689 settings/profile features.
