# Browser Client UX Reset — Decompose & Composer-First

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decompose the 3316-line App.tsx monolith into focused component files, replace the dashboard-like button wall with a compact nav + central composer, and produce a game-ready UI that feels like a dark-fantasy client rather than an admin panel.

**Architecture:** Extract a `ShellContext` providing shared state (shell data, realm theme, advanced toggle, route navigation). Each route becomes a lazy-loaded file ≤400 lines. The `GameRoute` centers on the prose composer with an optional action palette toggle. Navigation moves to a slim top-bar with icons + keyboard shortcuts.

**Tech Stack:** React 18 (already in use), TypeScript, Vite (already configured), CSS custom properties (dark-fantasy token system already landed via PR #775). No new dependencies.

---

## File Structure

After this plan, the frontend `src/` will look like:

```
src/
├── App.tsx                          (~120 lines — shell layout + suspense + context provider)
├── main.tsx                         (unchanged)
├── context/
│   └── ShellContext.tsx             (~100 lines — state loading, context value, provider)
├── hooks/
│   └── useShellState.ts            (~60 lines — extracted load logic, polling interval)
├── routes/
│   ├── HomeRoute.tsx               (~200 lines — launcher + saves)
│   ├── GameRoute.tsx               (~300 lines — narrative + composer + turn state)
│   ├── SoulRoute.tsx               (~120 lines — soul + player detail cards)
│   ├── WorldRoute.tsx              (~200 lines — location + action menu + reborn)
│   ├── JournalRoute.tsx            (~100 lines — filtered journal sections)
│   ├── InventoryRoute.tsx          (~100 lines — filtered inventory sections)
│   ├── MediaRoute.tsx              (~250 lines — gallery + QTE + atlas)
│   └── SettingsRoute.tsx           (~200 lines — settings grid)
├── components/
│   ├── DetailSurface.tsx           (existing — unchanged)
│   ├── ShellPanel.tsx              (~30 lines — reusable panel wrapper)
│   ├── NavBar.tsx                  (~80 lines — compact icon nav + keyboard shortcuts)
│   ├── Composer.tsx                (~120 lines — prose textarea + action mode toggle)
│   ├── ActionPalette.tsx           (~150 lines — contextual actions, collapsed by default)
│   ├── ActionCard.tsx              (~120 lines — single guided-form action)
│   ├── CommandResult.tsx           (~100 lines — ActionCommandResult + block renderer)
│   ├── PromptForm.tsx              (~100 lines — prompt controls)
│   ├── PlayerStatusSidebar.tsx     (~150 lines — sidebar summary cards)
│   ├── AudioPanel.tsx              (~150 lines — audio sidebar controls)
│   ├── QteScenePanel.tsx           (~180 lines — QTE offer/actions/resolution)
│   ├── RebornSystemsPanel.tsx      (~150 lines — afterlife overview panels)
│   ├── AdvancedDiagnostics.tsx     (~120 lines — technical mode panel)
│   ├── StatusBar.tsx               (~20 lines — health/energy bar)
│   ├── ErrorNotice.tsx             (~40 lines — error + empty state)
│   └── LoadingCard.tsx             (~15 lines — loading state)
├── utils/
│   ├── playerCopy.ts              (~80 lines — toPlayerFacingText + replacement tables)
│   ├── formatters.ts              (~100 lines — realm names, turn state labels, media formatters)
│   └── actionFilters.ts           (~60 lines — section/action matcher helpers)
├── api/
│   ├── client.ts                  (existing — unchanged)
│   └── contracts.ts               (existing — unchanged)
├── playerFacingCommandResult.ts    (existing — unchanged)
├── styles/                         (existing 5 CSS files — unchanged)
└── styles.css                      (existing — unchanged)
```

---

## Verification Commands

Throughout this plan, after any code change:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

This runs: typecheck → player-facing tests → production build. All must pass.

For C# guard tests (when touching backend or integration behavior):

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

---

## Task 1: Extract utility modules (playerCopy, formatters, actionFilters)

**Files:**
- Create: `src/utils/playerCopy.ts`
- Create: `src/utils/formatters.ts`
- Create: `src/utils/actionFilters.ts`
- Modify: `src/App.tsx` (remove moved code, add imports)

**Why first:** These have zero UI dependencies and many consumers. Extracting them makes all subsequent route extractions cleaner because routes can import directly.

- [ ] **Step 1: Create `src/utils/playerCopy.ts`**

Move from App.tsx:
- `playerCopyReplacements` array (lines 103–159)
- `launcherAboutCopyReplacements` array (lines 162–169)
- `toPlayerFacingText` function (lines 2082–2094)
- `playerLauncherAboutText` function (lines 2065–2074)

Export all as named exports. No default export.

```typescript
// src/utils/playerCopy.ts
export const playerCopyReplacements: Array<[RegExp, string]> = [
  // ... exact content from App.tsx lines 103-159
];

export const launcherAboutCopyReplacements: Array<[RegExp, string]> = [
  // ... exact content from App.tsx lines 162-169
];

export function toPlayerFacingText(value: string | null | undefined, fallback: string): string {
  const source = value?.trim();
  if (!source) {
    return fallback;
  }
  const normalized = playerCopyReplacements.reduce(
    (text, [pattern, replacement]) => text.replace(pattern, replacement),
    source
  );
  return normalized.trim() || fallback;
}

export function playerLauncherAboutText(text: string): string {
  const fallback = 'Браузерный клиент открывает локальную книгу и оставляет игровые решения в основном клиенте.';
  const playerText = toPlayerFacingText(text, fallback);
  const sanitized = launcherAboutCopyReplacements.reduce(
    (copy, [pattern, replacement]) => copy.replace(pattern, replacement),
    playerText
  );
  return sanitized.trim() || fallback;
}
```

- [ ] **Step 2: Create `src/utils/formatters.ts`**

Move from App.tsx all standalone formatting functions:
- `formatRealmName` (lines 2096–2110)
- `formatDialogueCategory` (lines 2112–2130)
- `formatTurnStateTitle` (lines 2132–2134)
- `formatTurnStateMessage` (lines 2136–2143)
- `formatTurnStateLabel` (lines 2178–2218)
- `formatSessionStatus` (lines 2160–2176)
- `formatQteStateLabel` (lines 2221–2247)
- `commandStateLabel` (lines 2249–2262)
- `formatQteGradeLabel` (lines 2618–2627)
- `formatQteActionCheck` (lines 2629–2633)
- `normalizeQteGrade` (lines 2602–2616)
- `qteGradeOptionsForAction` (lines 2596–2600) + `qteGradeOrder` const
- `formatMediaSize` (lines 2635–2649)
- `formatMediaDate` (lines 2651–2658)
- `formatHeroStatusLabel` (lines 2037–2047 — note: renamed from original location)
- `formatSidebarLayerStatus` (lines 1049–1060)
- `formatSidebarSessionSummary` (lines 1070–1086)
- `formatSidebarSaveSummary` (lines 1088–1098)
- `formatSidebarStatusMetric` (lines 1100–1107)
- `formatSidebarAudioSummary` (lines 1109–1113)
- `formatShiningGateStatus` (lines 1651–1661)
- `formatRebornLockStatus` (lines 1643–1649)
- `formatActionPreview` (lines 1663–1669)
- `formatTurnLifecycleActionDescription` (lines 1785–1791)
- `getComposerPlaceholder` (lines 2145–2147)
- `getComposerGuidance` (lines 2149–2153)
- `getComposerDisabledReason` (lines 2156–2158)
- `toLauncherSaveFailureNotice` (lines 2076–2080)
- `toCommandNotice` (lines 2050–2063)

Import `toPlayerFacingText` from `./playerCopy`. Import types from `../api/contracts`.

```typescript
// src/utils/formatters.ts
import { toPlayerFacingText } from './playerCopy';
import type { BrowserGameScreenDto, BrowserGameScreenAfterlifeDto, BrowserMainMenuDto, /* etc */ } from '../api/contracts';

// Each function exported exactly as-is from App.tsx
export function formatRealmName(realm: string): string { /* ... */ }
// ... all other functions
```

- [ ] **Step 3: Create `src/utils/actionFilters.ts`**

Move from App.tsx:
- `journalSectionMatchers`, `inventorySectionMatchers`, `rebornSectionMatchers`, `shiningAbodeActionMatchers`, `chaosSeaActionMatchers` (lines 1409–1413)
- `filterActionSections` (lines 1704–1717)
- `matchesActionSectionOrAction` (lines 1719–1738)
- `filterActionsForPanel` (lines 1623–1641)

```typescript
// src/utils/actionFilters.ts
import type { BrowserPlayerCommandSectionDto, BrowserPlayerCommandActionDto, BrowserPlayerCommandMenuDto } from '../api/contracts';

export const journalSectionMatchers = [/* ... */];
export const inventorySectionMatchers = [/* ... */];
export const rebornSectionMatchers = [/* ... */];
export const shiningAbodeActionMatchers = [/* ... */];
export const chaosSeaActionMatchers = [/* ... */];

export function filterActionSections(menu: BrowserPlayerCommandMenuDto, matchers: string[]): BrowserPlayerCommandSectionDto[] { /* ... */ }
export function matchesActionSectionOrAction(/* ... */): boolean { /* ... */ }
export function filterActionsForPanel(/* ... */): BrowserPlayerCommandActionDto[] { /* ... */ }
```

- [ ] **Step 4: Update App.tsx imports**

Replace inline definitions with imports:

```typescript
import { toPlayerFacingText, playerLauncherAboutText, playerCopyReplacements, launcherAboutCopyReplacements } from './utils/playerCopy';
import { formatRealmName, formatTurnStateTitle, /* ... all */ } from './utils/formatters';
import { filterActionSections, filterActionsForPanel, journalSectionMatchers, inventorySectionMatchers, rebornSectionMatchers, shiningAbodeActionMatchers, chaosSeaActionMatchers } from './utils/actionFilters';
```

Remove all moved code from App.tsx. The file should shrink by ~500 lines.

- [ ] **Step 5: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: typecheck pass, test pass, build pass. No behavioral change.

- [ ] **Step 6: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/utils/ BookOfEternityClient.WebFrontend/src/App.tsx
git commit -m "refactor(browser): extract utility modules from App.tsx monolith

Move player copy replacements, formatting functions, and action
section filters into focused utility modules under src/utils/.

No behavioral change — all exports used exactly as before.

Refs #760"
```

---

## Task 2: Extract ShellContext and shared components

**Files:**
- Create: `src/context/ShellContext.tsx`
- Create: `src/hooks/useShellState.ts`
- Create: `src/components/ShellPanel.tsx`
- Create: `src/components/StatusBar.tsx`
- Create: `src/components/ErrorNotice.tsx`
- Create: `src/components/LoadingCard.tsx`
- Modify: `src/App.tsx`

- [ ] **Step 1: Create `src/hooks/useShellState.ts`**

Extract the state loading logic from App.tsx (the `loadBrowserState` callback + initial state + derived values):

```typescript
// src/hooks/useShellState.ts
import { useCallback, useEffect, useMemo, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserShellState, RouteId } from '../context/ShellContext';

export function useShellState(advancedEnabled: boolean) {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });

  const loadBrowserState = useCallback(async () => {
    setShellState({ status: 'loading' });
    try {
      const [menu, session, game, audio, settings] = await Promise.all([
        browserApi.getMainMenu(),
        browserApi.getSessionStatus(),
        browserApi.getGameScreen(),
        browserApi.getAudioSettings(),
        browserApi.getClientSettings()
      ]);
      const [lifecycle, commandCoverage] = advancedEnabled ? await Promise.all([
        browserApi.getLifecycleDashboard(),
        browserApi.getCommandCoverage()
      ]) : [null, null];
      setShellState({ status: 'ready', menu, session, game, audio, settings, lifecycle, commandCoverage });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown browser shell error.';
      setShellState({
        status: 'error',
        playerMessage: 'Браузерный клиент не смог собрать состояние игры.',
        technicalDetails: message
      });
    }
  }, [advancedEnabled]);

  useEffect(() => { void loadBrowserState(); }, [loadBrowserState]);

  return { shellState, loadBrowserState };
}
```

- [ ] **Step 2: Create `src/context/ShellContext.tsx`**

```typescript
// src/context/ShellContext.tsx
import { createContext, useContext } from 'react';
import type { CSSProperties, ReactNode } from 'react';
import type {
  BrowserApiResult, BrowserGameScreenDto, BrowserMainMenuDto,
  BrowserClientSettingsDto, BrowserLifecycleDashboardDto,
  BrowserCommandCoverageDto, BrowserAudioSettingsDto, LocalWebUiSessionStatus
} from '../api/contracts';

export type RouteId = 'home' | 'game' | 'soul' | 'world' | 'journal' | 'inventory' | 'media' | 'settings';

export type BrowserShellState =
  | { status: 'loading' }
  | { status: 'ready'; menu: BrowserApiResult<BrowserMainMenuDto>; session: BrowserApiResult<LocalWebUiSessionStatus>; game: BrowserApiResult<BrowserGameScreenDto>; audio: BrowserApiResult<BrowserAudioSettingsDto>; settings: BrowserApiResult<BrowserClientSettingsDto>; lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> | null; commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null }
  | { status: 'error'; playerMessage: string; technicalDetails?: string };

export interface RealmTheme {
  key: string; label: string; icon: string; accent: string;
}

export interface ShellContextValue {
  shellState: BrowserShellState;
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null;
  gameScreen: BrowserGameScreenDto | null;
  menu: BrowserMainMenuDto | null;
  session: LocalWebUiSessionStatus | null;
  clientSettings: BrowserClientSettingsDto | null;
  realmTheme: RealmTheme;
  activeRoute: RouteId;
  setActiveRoute: (route: RouteId) => void;
  advancedEnabled: boolean;
  setAdvancedEnabled: (updater: (value: boolean) => boolean) => void;
  loadBrowserState: () => Promise<void>;
}

export const ShellContext = createContext<ShellContextValue | null>(null);

export function useShell(): ShellContextValue {
  const ctx = useContext(ShellContext);
  if (!ctx) throw new Error('useShell must be used within ShellContext.Provider');
  return ctx;
}
```

- [ ] **Step 3: Create shared leaf components**

`src/components/ShellPanel.tsx`:
```typescript
import type { ReactNode } from 'react';

export function ShellPanel({ title, eyebrow, children, nested = false, variant }: {
  title: string; eyebrow: string; children: ReactNode; nested?: boolean; variant?: string;
}) {
  const className = ['shell-panel', nested ? 'is-nested' : '', variant ? `panel-${variant}` : ''].filter(Boolean).join(' ');
  return (
    <section className={className} data-panel={variant ?? title}>
      <p className="panel-eyebrow">{eyebrow}</p>
      <h2>{title}</h2>
      {children}
    </section>
  );
}
```

`src/components/StatusBar.tsx`:
```typescript
export function StatusBar({ label, value }: { label: string; value: string }) {
  const numericValue = Number.parseFloat(value);
  const percent = Number.isFinite(numericValue) ? Math.max(0, Math.min(100, numericValue)) : 0;
  return (
    <div className="status-bar">
      <span>{label}</span>
      <div aria-hidden="true"><i style={{ width: `${percent}%` }} /></div>
      <strong>{value || '0%'}</strong>
    </div>
  );
}
```

`src/components/ErrorNotice.tsx`:
```typescript
import { toPlayerFacingText } from '../utils/playerCopy';
import type { BrowserApiResult, BrowserApiFailure } from '../api/contracts';

// Contains: ErrorNotice, EmptyState, EmptyOrFailure, ApiFailure — moved from App.tsx
// ... (exact code from lines 3195–3252 of current App.tsx)
```

`src/components/LoadingCard.tsx`:
```typescript
import { ShellPanel } from './ShellPanel';

export function LoadingCard() {
  return (
    <ShellPanel title="Загрузка" eyebrow="локальный клиент">
      <p>Собираем главное меню, сессию, игровой экран и состояние хода из локального клиента…</p>
    </ShellPanel>
  );
}
```

- [ ] **Step 4: Wire ShellContext.Provider in App.tsx**

Replace prop-drilling: the App component creates the context value from `useShellState` hook and wraps children in `<ShellContext.Provider>`. All route components will use `useShell()` instead of receiving props.

- [ ] **Step 5: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: pass.

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor(browser): add ShellContext and extract shared components

Introduce ShellContext with useShell() hook to eliminate prop-drilling.
Extract ShellPanel, StatusBar, ErrorNotice, LoadingCard into focused
component files.

No behavioral change — all UI renders identically.

Refs #760"
```

---

## Task 3: Extract route components (HomeRoute through SettingsRoute)

**Files:**
- Create: `src/routes/HomeRoute.tsx`
- Create: `src/routes/GameRoute.tsx`
- Create: `src/routes/SoulRoute.tsx`
- Create: `src/routes/WorldRoute.tsx`
- Create: `src/routes/JournalRoute.tsx`
- Create: `src/routes/InventoryRoute.tsx`
- Create: `src/routes/MediaRoute.tsx`
- Create: `src/routes/SettingsRoute.tsx`
- Modify: `src/App.tsx`

- [ ] **Step 1: Extract all 8 route files**

Each route:
1. Gets its own file in `src/routes/`
2. Uses `useShell()` to access state (no props needed)
3. Imports utilities from `../utils/` and shared components from `../components/`
4. Has a default export (for `React.lazy()`)
5. Stays ≤400 lines

Example for `GameRoute.tsx`:
```typescript
import { useState } from 'react';
import type { FormEvent } from 'react';
import { useShell } from '../context/ShellContext';
import { ShellPanel } from '../components/ShellPanel';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { Composer } from '../components/Composer';
import { toPlayerFacingText } from '../utils/playerCopy';
import { formatTurnStateTitle, formatTurnStateMessage, formatTurnStateLabel, formatQteStateLabel, formatDialogueCategory } from '../utils/formatters';
import { isSuccess } from '../api/contracts';

export default function GameRoute() {
  const { readyState, advancedEnabled } = useShell();
  // ... game route rendering (narrative card, turn state, composer, lifecycle)
}
```

The `renderActiveRoute` function in App.tsx is replaced by `React.lazy()` + `<Suspense>`.

- [ ] **Step 2: Wire lazy loading in App.tsx**

```typescript
import { lazy, Suspense } from 'react';

const HomeRoute = lazy(() => import('./routes/HomeRoute'));
const GameRoute = lazy(() => import('./routes/GameRoute'));
const SoulRoute = lazy(() => import('./routes/SoulRoute'));
const WorldRoute = lazy(() => import('./routes/WorldRoute'));
const JournalRoute = lazy(() => import('./routes/JournalRoute'));
const InventoryRoute = lazy(() => import('./routes/InventoryRoute'));
const MediaRoute = lazy(() => import('./routes/MediaRoute'));
const SettingsRoute = lazy(() => import('./routes/SettingsRoute'));

function ActiveRoute() {
  const { activeRoute } = useShell();
  const RouteComponent = {
    home: HomeRoute, game: GameRoute, soul: SoulRoute, world: WorldRoute,
    journal: JournalRoute, inventory: InventoryRoute, media: MediaRoute, settings: SettingsRoute
  }[activeRoute];

  return (
    <Suspense fallback={<LoadingCard />}>
      <RouteComponent />
    </Suspense>
  );
}
```

- [ ] **Step 3: Extract supporting components needed by routes**

Create these during extraction as routes need them:
- `src/components/ActionCard.tsx` — the guided-form action card (used by World, Journal, Inventory)
- `src/components/CommandResult.tsx` — ActionCommandResult + renderCommandBlock
- `src/components/PromptForm.tsx` — prompt control rendering
- `src/components/RebornSystemsPanel.tsx` — afterlife panels (used by WorldRoute)
- `src/components/QteScenePanel.tsx` — QTE panel (used by MediaRoute)
- `src/components/AudioPanel.tsx` — audio settings (used by sidebar)
- `src/components/PlayerStatusSidebar.tsx` — sidebar (remains in App)
- `src/components/AdvancedDiagnostics.tsx` — diagnostics panel

- [ ] **Step 4: Verify App.tsx is ≤400 lines**

After extraction, App.tsx should contain:
- Type definitions (RouteId, RouteCard, etc.) → most moved to context
- `playerRoutes` constant
- `RouteGlyph` component (~30 lines)
- App default export (~60 lines: shell layout, provider, nav, route, sidebar)
- `resolveRouteStates` function

Target: ~120–200 lines.

- [ ] **Step 5: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Also run C# guard tests since the built frontend bundle is validated:
```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor(browser): decompose App.tsx into route components

Extract 8 route files, 10 component files, use React.lazy for
code-splitting. App.tsx reduced from 3316 to ~150 lines.
ShellContext eliminates prop-drilling.

All functionality preserved; typecheck + build + tests pass.

Closes #760
Refs #680"
```

---

## Task 4: Replace route card grid with compact NavBar

**Files:**
- Create: `src/components/NavBar.tsx`
- Modify: `src/App.tsx` (replace route grid rendering)
- Modify: `src/styles/layout.css` (add nav-bar styles, remove route-grid prominence)
- Modify: `src/styles/components.css` (nav-bar component styles)

- [ ] **Step 1: Create `src/components/NavBar.tsx`**

A slim horizontal bar showing route icons with labels on hover/focus. Active route highlighted with realm accent. Keyboard shortcuts (1–8) switch routes.

```typescript
import { useEffect } from 'react';
import type { RouteId } from '../context/ShellContext';
import { useShell } from '../context/ShellContext';

const routeIcons: Record<RouteId, { glyph: string; label: string; shortcut: string }> = {
  home:      { glyph: '📖', label: 'Главная', shortcut: '1' },
  game:      { glyph: '🔥', label: 'Игра', shortcut: '2' },
  soul:      { glyph: '🕯️', label: 'Душа', shortcut: '3' },
  world:     { glyph: '🗺️', label: 'Мир', shortcut: '4' },
  journal:   { glyph: '📜', label: 'Журнал', shortcut: '5' },
  inventory: { glyph: '🎒', label: 'Инвентарь', shortcut: '6' },
  media:     { glyph: '🖼️', label: 'Медиа', shortcut: '7' },
  settings:  { glyph: '⚙️', label: 'Настройки', shortcut: '8' }
};

const routeOrder: RouteId[] = ['home', 'game', 'soul', 'world', 'journal', 'inventory', 'media', 'settings'];

export function NavBar() {
  const { activeRoute, setActiveRoute } = useShell();

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement || event.target instanceof HTMLSelectElement) return;
      const index = Number(event.key) - 1;
      if (index >= 0 && index < routeOrder.length) {
        setActiveRoute(routeOrder[index]);
      }
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [setActiveRoute]);

  return (
    <nav className="nav-bar" aria-label="Разделы игры">
      {routeOrder.map((id) => {
        const { glyph, label, shortcut } = routeIcons[id];
        return (
          <button
            key={id}
            type="button"
            className={`nav-bar__item${activeRoute === id ? ' is-active' : ''}`}
            onClick={() => setActiveRoute(id)}
            aria-pressed={activeRoute === id}
            aria-label={`${label} (${shortcut})`}
            title={`${label} — клавиша ${shortcut}`}
          >
            <span className="nav-bar__glyph" aria-hidden="true">{glyph}</span>
            <span className="nav-bar__label">{label}</span>
          </button>
        );
      })}
    </nav>
  );
}
```

- [ ] **Step 2: Add NavBar styles to CSS**

In `src/styles/components.css`, add:
```css
.nav-bar {
  display: flex;
  gap: var(--space-xs);
  padding: var(--space-xs) var(--space-md);
  background: var(--surface-elevated);
  border-bottom: 1px solid var(--border-subtle);
  position: sticky;
  top: 0;
  z-index: 100;
}

.nav-bar__item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding: var(--space-xs) var(--space-sm);
  border-radius: var(--radius-md);
  background: transparent;
  border: 1px solid transparent;
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s;
  font-size: 0.75rem;
  color: var(--text-muted);
}

.nav-bar__item:hover,
.nav-bar__item:focus-visible {
  background: var(--surface-hover);
  border-color: var(--border-subtle);
}

.nav-bar__item.is-active {
  background: var(--surface-active);
  border-color: var(--realm-accent, var(--accent-gold));
  color: var(--text-primary);
}

.nav-bar__glyph {
  font-size: 1.25rem;
}

.nav-bar__label {
  font-size: 0.65rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
```

- [ ] **Step 3: Replace route grids in App.tsx with NavBar**

Remove the two `<nav className="route-grid ...">` sections and the `<section className="shell-hero">` header. Replace with:

```tsx
<NavBar />
```

The hero section is excessive for a game client. Keep realm theme indicator inside the NavBar or sidebar instead.

- [ ] **Step 4: Adjust layout.css**

Remove or deprecate `.route-grid`, `.route-card` styles. Remove `.shell-hero` section. Adjust `.workspace-grid` to take full viewport height minus the slim nav bar.

```css
.browser-shell {
  display: grid;
  grid-template-rows: auto 1fr;
  min-height: 100vh;
}

.workspace-grid {
  display: grid;
  grid-template-columns: 1fr 320px;
  gap: var(--space-md);
  padding: var(--space-md);
  overflow-y: auto;
}
```

- [ ] **Step 5: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: pass. Visual structure changed but no logic change.

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(browser): replace route card grid with compact NavBar

Swap the 8-card route grid (taking ~200px vertical space) for a slim
sticky nav bar with icons, labels, and keyboard shortcuts (1-8).
Remove the shell-hero banner to give more space to game content.

Closes #766
Refs #755
Refs #680"
```

---

## Task 5: Implement composer-first GameRoute with action palette toggle

**Files:**
- Create: `src/components/Composer.tsx`
- Create: `src/components/ActionPalette.tsx`
- Modify: `src/routes/GameRoute.tsx`
- Modify: `src/styles/components.css`

- [ ] **Step 1: Create `src/components/Composer.tsx`**

The central interaction element. Two modes:
1. **Prose mode** (default): textarea for narrative player input
2. **Action mode**: shows a filtered, searchable action list

```typescript
import { useState } from 'react';
import type { FormEvent } from 'react';
import { useShell } from '../context/ShellContext';
import { browserApi } from '../api/client';
import { toPlayerFacingText } from '../utils/playerCopy';
import { getComposerPlaceholder, getComposerGuidance, getComposerDisabledReason } from '../utils/formatters';
import type { BrowserGameScreenDto } from '../api/contracts';

export type ComposerMode = 'prose' | 'actions';

export function Composer({ actionComposer }: { actionComposer: BrowserGameScreenDto['actionComposer'] }) {
  const { loadBrowserState } = useShell();
  const [text, setText] = useState('');
  const [notice, setNotice] = useState('');
  const [mode, setMode] = useState<ComposerMode>('prose');

  function submitProse(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = text.trim();
    if (normalized.startsWith('/')) {
      setNotice('Служебные команды не выполняются из основного поля. Используйте режим «Действия» или расширенный режим.');
      return;
    }
    setNotice('Отправляем действие…');
    browserApi.submitPlayerAction({ text: normalized }).then((result) => {
      if (result.ok && result.data.success) {
        setNotice(result.data.playerMessage);
        setText('');
        loadBrowserState();
      } else if (result.ok && !result.data.success) {
        setNotice(result.data.playerMessage);
      } else {
        setNotice('Не удалось отправить действие. Попробуйте ещё раз.');
      }
    }).catch(() => {
      setNotice('Ошибка соединения. Убедитесь, что клиент запущен.');
    });
  }

  return (
    <div className="composer-container">
      <div className="composer-mode-toggle">
        <button
          type="button"
          className={mode === 'prose' ? 'is-active' : ''}
          onClick={() => setMode('prose')}
        >
          Художественный ввод
        </button>
        <button
          type="button"
          className={mode === 'actions' ? 'is-active' : ''}
          onClick={() => setMode('actions')}
        >
          Действия
        </button>
      </div>

      {mode === 'prose' && (
        <form className="composer" onSubmit={submitProse}>
          <textarea
            id="player-action"
            rows={3}
            value={text}
            onChange={(e) => setText(e.currentTarget.value)}
            placeholder={getComposerPlaceholder(actionComposer)}
            disabled={!actionComposer.canSubmit}
          />
          {!actionComposer.canSubmit && <p className="warning-text">{getComposerDisabledReason(actionComposer)}</p>}
          <div className="composer-footer">
            <p className="muted">{getComposerGuidance(actionComposer)}</p>
            <button type="submit" disabled={!text.trim() || !actionComposer.canSubmit}>Отправить</button>
          </div>
          {notice && <p className="composer-notice">{notice}</p>}
        </form>
      )}

      {mode === 'actions' && <ActionPaletteSlot />}
    </div>
  );
}

function ActionPaletteSlot() {
  // Lazy import ActionPalette to keep Composer slim
  const { readyState } = useShell();
  if (!readyState) return null;
  // ActionPalette renders the contextual action menu
  return <div className="action-palette-slot" />;
}
```

- [ ] **Step 2: Create `src/components/ActionPalette.tsx`**

A collapsible, searchable list of available player actions. Replaces the permanent button wall:

```typescript
import { useMemo, useState } from 'react';
import { useShell } from '../context/ShellContext';
import { ActionCard } from './ActionCard';
import { toPlayerFacingText } from '../utils/playerCopy';
import type { BrowserPlayerCommandSectionDto } from '../api/contracts';
import { isSuccess } from '../api/contracts';

export function ActionPalette() {
  const { readyState } = useShell();
  const [search, setSearch] = useState('');

  const sections = useMemo(() => {
    if (!readyState || !isSuccess(readyState.game)) return [];
    const allSections = readyState.game.data.actionMenu.sections.filter(s => s.playerDefault && s.actions.length > 0);
    if (!search.trim()) return allSections;
    const needle = search.toLowerCase();
    return allSections
      .map(section => ({
        ...section,
        actions: section.actions.filter(a =>
          a.label.toLowerCase().includes(needle) ||
          a.description.toLowerCase().includes(needle)
        )
      }))
      .filter(s => s.actions.length > 0);
  }, [readyState, search]);

  return (
    <div className="action-palette">
      <input
        type="search"
        className="action-palette__search"
        placeholder="Найти действие…"
        value={search}
        onChange={(e) => setSearch(e.currentTarget.value)}
      />
      {sections.length === 0 && <p className="muted">Действия появятся, когда книга откроет каталог для текущей главы.</p>}
      <div className="action-palette__grid">
        {sections.map(section => (
          <section key={section.id} className="action-palette__section">
            <h4>{toPlayerFacingText(section.label, 'Раздел')}</h4>
            {section.actions.map(action => (
              <ActionCard key={action.id} action={action} compact />
            ))}
          </section>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Update GameRoute to use Composer**

The GameRoute now centers on: Narrative → Composer → Turn state (compact). No large action button wall visible by default.

```typescript
// src/routes/GameRoute.tsx
import { useShell } from '../context/ShellContext';
import { Composer } from '../components/Composer';
import { ActionPalette } from '../components/ActionPalette';
// ... other imports

export default function GameRoute() {
  const { readyState, advancedEnabled } = useShell();
  if (!readyState || !isSuccess(readyState.game)) {
    return <EmptyOrFailure /* ... */ />;
  }
  const game = readyState.game.data;

  return (
    <ShellPanel title="Игра" eyebrow="нарратив и ход">
      {/* Narrative — the featured content */}
      <article className="narrative-card is-featured">
        <h2>{game.theme.icon} {game.theme.label}</h2>
        <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
      </article>

      {/* Dialogue options (compact) */}
      {game.narrative.dialogueOptions.length > 0 && (
        <ul className="choice-list">
          {game.narrative.dialogueOptions.map(opt => (
            <li key={opt.id}><strong>{opt.text}</strong></li>
          ))}
        </ul>
      )}

      {/* Central Composer */}
      <Composer actionComposer={game.actionComposer} />

      {/* Turn state — compact status, not a full section */}
      <div className="turn-status-compact">
        <span className={`status-pill turn-phase--${game.turnState.severity}`}>
          {formatTurnStateTitle(game.turnState)}
        </span>
        <span className="muted">{formatTurnStateMessage(game.turnState)}</span>
      </div>
    </ShellPanel>
  );
}
```

- [ ] **Step 4: Add Composer and ActionPalette CSS**

```css
.composer-container {
  margin: var(--space-md) 0;
}

.composer-mode-toggle {
  display: flex;
  gap: var(--space-xs);
  margin-bottom: var(--space-sm);
}

.composer-mode-toggle button {
  padding: var(--space-xs) var(--space-md);
  border-radius: var(--radius-md);
  background: var(--surface-subtle);
  border: 1px solid var(--border-subtle);
  cursor: pointer;
  font-size: 0.85rem;
}

.composer-mode-toggle button.is-active {
  background: var(--surface-active);
  border-color: var(--realm-accent, var(--accent-gold));
  color: var(--text-primary);
}

.composer-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-sm);
}

.action-palette__search {
  width: 100%;
  padding: var(--space-sm);
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
  background: var(--surface-subtle);
  margin-bottom: var(--space-sm);
}

.action-palette__grid {
  display: grid;
  gap: var(--space-sm);
  max-height: 400px;
  overflow-y: auto;
}

.turn-status-compact {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
  padding: var(--space-sm) 0;
  border-top: 1px solid var(--border-subtle);
}
```

- [ ] **Step 5: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(browser): implement composer-first GameRoute with action palette

Replace the dashboard-like action button wall with a central composer
(prose mode default, action palette toggle). GameRoute now flows:
narrative → dialogue options → composer → turn status.

Action palette is searchable, compact, hidden by default.
Slash commands rejected from prose mode with user-friendly notice.

Closes #755
Refs #754
Refs #680"
```

---

## Task 6: Streamline WorldRoute (collapse action menu)

**Files:**
- Modify: `src/routes/WorldRoute.tsx`
- Modify: `src/components/ActionPalette.tsx` (if needed for reuse)

- [ ] **Step 1: Refactor WorldRoute to show contextual subset**

The current WorldRoute dumps the entire action catalog (issue #769, #744). Replace with:
1. Location + turn orientation (compact)
2. Reborn systems panel (only when afterlife is active or relevant)
3. A "show all actions" toggle that defaults to collapsed

```typescript
// WorldRoute shows location info + collapsed action overview
export default function WorldRoute() {
  const { readyState, advancedEnabled } = useShell();
  const [showAllActions, setShowAllActions] = useState(false);
  // ... location card, reborn panel (only if afterlife active), toggle
}
```

- [ ] **Step 2: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(browser): collapse action catalog in WorldRoute

Default view shows location + turn orientation. Full action catalog
hidden behind explicit toggle. Reborn panels shown only when afterlife
realm is active.

Refs #769
Refs #744
Refs #680"
```

---

## Task 7: Slim down sidebar

**Files:**
- Modify: `src/components/PlayerStatusSidebar.tsx`
- Modify: `src/styles/layout.css`

- [ ] **Step 1: Condense sidebar cards**

Current sidebar has 5 full summary cards + audio panel + advanced toggle. Reduce to:
1. Realm + turn status (1 combined card)
2. Hero vitals (compact bar form, not a full card)
3. Session status (1 line)
4. Audio (collapsed, expand on click)
5. Advanced toggle

Remove verbose explanatory text that says "this is normal, not an error" (players don't need UI-explaining-UI).

- [ ] **Step 2: Add collapsible sidebar on narrow viewports**

```css
@media (max-width: 900px) {
  .workspace-grid {
    grid-template-columns: 1fr;
  }
  .workspace-sidebar {
    position: fixed;
    right: 0;
    top: var(--nav-bar-height, 48px);
    width: 300px;
    transform: translateX(100%);
    transition: transform 0.2s;
  }
  .workspace-sidebar.is-open {
    transform: translateX(0);
  }
}
```

- [ ] **Step 3: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(browser): streamline sidebar to compact game summary

Combine realm/turn into single card, compress hero vitals to bars,
collapse audio panel by default, remove explanatory meta-text.
Sidebar collapses to slide-out on narrow viewports.

Refs #761
Refs #772
Refs #680"
```

---

## Task 8: Add guard test preventing button wall regression

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/test/playerFacingCommandResult.test.ts` or create new test file
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` (if built bundle assertions needed)

- [ ] **Step 1: Add frontend structural test**

Create `test/uiStructure.test.ts` that asserts:
- The built HTML/JS bundle does NOT contain the old `.route-grid--primary` class (route cards removed)
- The bundle DOES contain `.nav-bar` class
- The bundle DOES contain `.composer-container` class

This prevents reintroducing the dashboard pattern.

```typescript
// test/uiStructure.test.ts
import { readFileSync, readdirSync } from 'fs';
import { join } from 'path';
import { describe, it, expect } from 'vitest';

function readBuiltBundle(): string {
  const distDir = join(__dirname, '..', 'dist', 'assets');
  const jsFiles = readdirSync(distDir).filter(f => f.endsWith('.js'));
  return jsFiles.map(f => readFileSync(join(distDir, f), 'utf-8')).join('\n');
}

describe('UI structure guards', () => {
  const bundle = readBuiltBundle();

  it('does not contain route-grid card pattern', () => {
    expect(bundle).not.toContain('route-grid--primary');
  });

  it('contains nav-bar component', () => {
    expect(bundle).toContain('nav-bar');
  });

  it('contains composer-container component', () => {
    expect(bundle).toContain('composer-container');
  });

  it('does not show action-menu as permanent top-level section', () => {
    // The action menu should only appear inside action palette or collapsed
    expect(bundle).not.toContain('contextual-actions-title');
  });
});
```

- [ ] **Step 2: Run full verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

Expected: all pass.

- [ ] **Step 3: Commit**

```bash
git commit -m "test(browser): add structural guard against button-wall regression

New uiStructure.test.ts checks that the built bundle uses nav-bar and
composer-container patterns, and does NOT contain the old route-grid
card wall.

Refs #755
Refs #760"
```

---

## Task 9: Final cleanup and PR

**Files:**
- Clean up any remaining dead code in App.tsx
- Remove old CSS classes no longer used (`.route-card`, `.route-grid`, `.shell-hero`)
- Update any JSDoc or comments referencing old structure

- [ ] **Step 1: Remove dead CSS**

Search for selectors in `components.css` and `layout.css` that are no longer referenced:
- `.route-card`, `.route-card--*`, `.route-card-state--*`
- `.route-grid`, `.route-grid--primary`, `.route-grid--utility`
- `.shell-hero`, `.hero-layout`, `.hero-status`
- `.utility-route-heading`

- [ ] **Step 2: Verify no file exceeds 400 lines**

```bash
find BookOfEternityClient.WebFrontend/src -name "*.tsx" -o -name "*.ts" | xargs wc -l | sort -rn | head -20
```

If any file exceeds 400 lines, split further.

- [ ] **Step 3: Final verification gate**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet build
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

Expected: typecheck pass, tests pass, build pass, all guard tests green.

- [ ] **Step 4: Create PR**

```bash
gh pr create \
  --title "feat(browser): decompose monolith and implement composer-first UX" \
  --body "## Summary
Decomposes App.tsx (3316 lines) into focused modules, replaces dashboard
button wall with compact nav + central composer, streamlines sidebar.

Closes #760 #755 #766
Refs #761 #769 #744 #680

## Changes
- 8 route files (lazy-loaded)
- 12 component files
- 3 utility modules
- ShellContext replaces prop-drilling
- NavBar replaces route card grid
- Composer replaces button wall
- ActionPalette: searchable, hidden by default
- Structural guard test prevents regression

## Verification
- npm run verify ✅
- dotnet build ✅
- 72+ guard tests ✅"
```

- [ ] **Step 5: Commit and push**

```bash
git push -u origin HEAD
```
