# Issue #721 Browser Route Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Browser Client emoji route tiles with a unified inline-SVG icon and semantic route-state system.

**Architecture:** Keep C# as the gameplay/application authority. React derives route card presentation states from existing browser shell API results and renders local inline SVG route glyphs. CSS owns the dark-fantasy visual treatment for active, available, locked, loading, and attention states.

**Tech Stack:** .NET 8 xUnit source guards, Vite + React + TypeScript, plain CSS design-system files.

---

## File structure

- Modify `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`: add a source guard for route icon/state contracts.
- Modify `BookOfEternityClient.WebFrontend/src/App.tsx`: replace emoji icon metadata with `RouteIconId`, add `RouteGlyph`, add route-state helpers, and render state-aware route cards.
- Modify `BookOfEternityClient.WebFrontend/src/styles/components.css`: style route icon medallions and semantic route states.
- Modify `BookOfEternityClient.WebFrontend/README.md`: document #721 route icon/state conventions.

---

### Task 1: Add failing source guard for route icons and states

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this test after `BrowserNavigationIa_SeparatesPrimaryPlayerRoutesFromUtilityAndAdvancedRoutes`:

```csharp
    [Fact]
    public void BrowserRouteCards_UseInlineSvgIconsAndSemanticStates()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        var routeArrayStart = app.IndexOf("const playerRoutes", StringComparison.Ordinal);
        var routeArrayEnd = app.IndexOf("const fallbackTheme", StringComparison.Ordinal);
        Assert.True(routeArrayStart >= 0 && routeArrayEnd > routeArrayStart, "Route metadata should stay near the top of App.tsx.");
        var routeMetadata = app[routeArrayStart..routeArrayEnd];

        foreach (var emojiIcon in new[] { "✦", "📖", "🕯️", "🗺️", "✍️", "🎒", "🎞️", "⚙️" })
        {
            Assert.DoesNotContain($"icon: '{emojiIcon}'", routeMetadata, StringComparison.Ordinal);
        }

        Assert.Contains("type RouteIconId = 'book' | 'flame' | 'soul' | 'map' | 'journal' | 'satchel' | 'gallery' | 'settings';", app, StringComparison.Ordinal);
        Assert.Contains("type RouteAvailabilityState = 'active' | 'available' | 'locked' | 'loading' | 'attention';", app, StringComparison.Ordinal);
        Assert.Contains("function RouteGlyph({ icon }: { icon: RouteIconId })", app, StringComparison.Ordinal);
        Assert.Contains("<RouteGlyph icon={route.icon} />", app, StringComparison.Ordinal);
        Assert.Contains("resolveRouteStates(playerRoutes, activeRoute, shellState, readyState)", app, StringComparison.Ordinal);
        Assert.Contains("data-route-state={routeState.state}", app, StringComparison.Ordinal);
        Assert.Contains("route-card-state--${routeState.state}", app, StringComparison.Ordinal);
        Assert.Contains("aria-label={`${route.label}. ${route.description} Состояние: ${routeState.label}`}", app, StringComparison.Ordinal);

        Assert.Contains(".route-card__icon", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card__state", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card-state--active", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card-state--available", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card-state--locked", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card-state--loading", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card-state--attention", styles, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserRouteCards_UseInlineSvgIconsAndSemanticStates" --logger "console;verbosity=minimal"
```

Expected: FAIL because `playerRoutes` still contains emoji `icon` values and no `RouteGlyph`/route-state contract exists.

---

### Task 2: Replace emoji route metadata with inline SVG icons and route states

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`

- [ ] **Step 1: Update route/icon/state types**

Replace the top route type/interface block with:

```typescript
type RouteId = 'home' | 'game' | 'soul' | 'world' | 'journal' | 'inventory' | 'media' | 'settings';
type RouteKind = 'primary' | 'utility';
type RouteIconId = 'book' | 'flame' | 'soul' | 'map' | 'journal' | 'satchel' | 'gallery' | 'settings';
type RouteAvailabilityState = 'active' | 'available' | 'locked' | 'loading' | 'attention';
type LauncherMode = 'continue' | 'load' | 'new-game' | 'settings' | 'about';

interface RouteCard {
  id: RouteId;
  kind: RouteKind;
  label: string;
  description: string;
  icon: RouteIconId;
}

interface RouteStateDetails {
  state: RouteAvailabilityState;
  label: string;
}
```

- [ ] **Step 2: Replace `playerRoutes` icon values**

Use these icons:

```typescript
const playerRoutes: RouteCard[] = [
  { id: 'home', kind: 'primary', label: 'Главная', description: 'Сводка партии, продолжение, загрузка и безопасные действия.', icon: 'book' },
  { id: 'game', kind: 'primary', label: 'Игра', description: 'Текущая сцена, нарратив, ход ГМа и основной художественный ввод.', icon: 'flame' },
  { id: 'soul', kind: 'primary', label: 'Душа', description: 'Персонаж, душа, состояние героя и текущий слой мира.', icon: 'soul' },
  { id: 'world', kind: 'primary', label: 'Мир', description: 'Локация, карта, фракции и игровые действия окружения.', icon: 'map' },
  { id: 'journal', kind: 'primary', label: 'Журнал', description: 'Квесты, хроника, заметки, архив и история текущей главы.', icon: 'journal' },
  { id: 'inventory', kind: 'primary', label: 'Инвентарь', description: 'Предметы, экипировка, ремесло и локальные хранилища.', icon: 'satchel' },
  { id: 'media', kind: 'utility', label: 'Медиа', description: 'Галерея, быстрые сцены и игровые материалы.', icon: 'gallery' },
  { id: 'settings', kind: 'utility', label: 'Настройки', description: 'Локальный профиль, звук, язык и комфорт клиента.', icon: 'settings' }
];
```

- [ ] **Step 3: Add `RouteGlyph` component and route-state helpers**

Add these functions before `GameLauncher`:

```typescript
function RouteGlyph({ icon }: { icon: RouteIconId }) {
  const paths: Record<RouteIconId, ReactNode> = {
    book: <path d="M5 5.5c2.5-1.2 4.8-1.2 7 0v13c-2.2-1.2-4.5-1.2-7 0v-13Zm7 0c2.2-1.2 4.5-1.2 7 0v13c-2.5-1.2-4.8-1.2-7 0m0-13v13" />,
    flame: <path d="M12 21c3.6-1.4 5.7-3.9 5.7-7.1 0-2.5-1.4-4.9-4.2-7.3-.2 2.3-1 3.9-2.3 4.9.1-2.7-.9-5-3-6.9.1 3.1-1.1 5.2-2.4 7.1A6 6 0 0 0 12 21Z" />,
    soul: <path d="M12 3.5c2.1 2.4 3.2 4.8 3.2 7.2a3.2 3.2 0 1 1-6.4 0c0-2.4 1.1-4.8 3.2-7.2Zm0 10.5v6m-3 0h6" />,
    map: <path d="m4.5 6.5 5-2 5 2 5-2v13l-5 2-5-2-5 2v-13Zm5-2v13m5-11v13" />,
    journal: <path d="M6 4.5h9.5A2.5 2.5 0 0 1 18 7v12.5H7.5A2.5 2.5 0 0 1 5 17V6.5a2 2 0 0 1 2-2Zm1 12.5h11M9 8h5m-5 3h6" />,
    satchel: <path d="M8 8V6.8A2.8 2.8 0 0 1 10.8 4h2.4A2.8 2.8 0 0 1 16 6.8V8m-9 0h10.5l1 10H5.5l1-10Zm4.5 4h2" />,
    gallery: <path d="M5 6.5A2.5 2.5 0 0 1 7.5 4h9A2.5 2.5 0 0 1 19 6.5v11A2.5 2.5 0 0 1 16.5 20h-9A2.5 2.5 0 0 1 5 17.5v-11Zm3 9 2.4-2.7 2 2 2.2-3.1L17 15.5M9 8.5h.01" />,
    settings: <path d="M12 8.5a3.5 3.5 0 1 1 0 7 3.5 3.5 0 0 1 0-7Zm0-5v2m0 13v2m8.5-8.5h-2m-13 0h-2m14.5-6.5-1.4 1.4M6.9 17.1l-1.4 1.4m0-13 1.4 1.4m10.2 10.2 1.4 1.4" />
  };

  return (
    <svg className="route-card__glyph" viewBox="0 0 24 24" role="img" focusable="false" aria-hidden="true">
      {paths[icon]}
    </svg>
  );
}

function resolveRouteStates(
  routes: RouteCard[],
  activeRoute: RouteId,
  shellState: BrowserShellState,
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null
): Record<RouteId, RouteStateDetails> {
  return routes.reduce((states, route) => {
    states[route.id] = resolveRouteState(route.id, activeRoute, shellState, readyState);
    return states;
  }, {} as Record<RouteId, RouteStateDetails>);
}

function resolveRouteState(
  routeId: RouteId,
  activeRoute: RouteId,
  shellState: BrowserShellState,
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null
): RouteStateDetails {
  if (activeRoute === routeId) {
    return { state: 'active', label: 'открыто' };
  }

  if (shellState.status === 'loading') {
    return { state: 'loading', label: 'собираем' };
  }

  if (shellState.status === 'error') {
    return { state: 'attention', label: 'нужна проверка' };
  }

  if (routeHasAttention(routeId, readyState)) {
    return { state: 'attention', label: 'нужна проверка' };
  }

  if (routeNeedsGame(routeId) && !hasGameScreen(readyState)) {
    return { state: 'locked', label: 'ждёт главу' };
  }

  return { state: 'available', label: 'доступно' };
}

function routeNeedsGame(routeId: RouteId): boolean {
  return routeId === 'game' || routeId === 'soul' || routeId === 'world' || routeId === 'journal' || routeId === 'inventory' || routeId === 'media';
}

function hasGameScreen(readyState: Extract<BrowserShellState, { status: 'ready' }> | null): boolean {
  return Boolean(readyState && isSuccess(readyState.game));
}

function routeHasAttention(routeId: RouteId, readyState: Extract<BrowserShellState, { status: 'ready' }> | null): boolean {
  if (!readyState) {
    return false;
  }

  if (routeId === 'home') {
    return !isSuccess(readyState.menu) || !isSuccess(readyState.session);
  }

  if (routeId === 'settings') {
    return !isSuccess(readyState.audio);
  }

  if (!isSuccess(readyState.game)) {
    return routeNeedsGame(routeId);
  }

  return routeId === 'game' && (readyState.game.data.turnState.severity === 'error' || readyState.game.data.turnState.severity === 'repair');
}
```

- [ ] **Step 4: Wire route state into rendering**

Add this after `realmTheme` in `App`:

```typescript
  const routeStates = useMemo(
    () => resolveRouteStates(playerRoutes, activeRoute, shellState, readyState),
    [activeRoute, shellState, readyState]
  );
```

Change route rendering calls to pass `routeStates`:

```typescript
{primaryPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, routeStates, setActiveRoute))}
{utilityPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, routeStates, setActiveRoute))}
```

Replace `renderRouteButton` with:

```typescript
function renderRouteButton(
  route: RouteCard,
  activeRoute: RouteId,
  routeStates: Record<RouteId, RouteStateDetails>,
  setActiveRoute: (route: RouteId) => void
): ReactNode {
  const routeState = routeStates[route.id];
  return (
    <button
      key={route.id}
      type="button"
      className={`route-card route-card--${route.id} route-card-state--${routeState.state}${activeRoute === route.id ? ' is-active' : ''}`}
      data-route-state={routeState.state}
      onClick={() => setActiveRoute(route.id)}
      aria-pressed={activeRoute === route.id}
      aria-label={`${route.label}. ${route.description} Состояние: ${routeState.label}`}
    >
      <span className="route-card__icon" aria-hidden="true"><RouteGlyph icon={route.icon} /></span>
      <span className="route-card__body">
        <strong>{route.label}</strong>
        <small>{route.description}</small>
      </span>
      <span className="route-card__state" aria-hidden="true">{routeState.label}</span>
    </button>
  );
}
```

---

### Task 3: Style route icons and semantic route states

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

- [ ] **Step 1: Update route card CSS**

Replace the `.route-card` block and related route-card selectors through `.route-grid--utility .route-card` with:

```css
.route-card {
  --route-state-color: color-mix(in srgb, var(--route-tint, var(--realm-accent)) 72%, #f8e4b5);
  position: relative;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  grid-template-rows: auto 1fr;
  gap: 0.65rem 0.85rem;
  min-height: 9rem;
  padding: var(--space-4);
  color: inherit;
  text-align: left;
  transition:
    transform var(--motion-fast),
    border-color var(--motion-fast),
    background var(--motion-fast),
    box-shadow var(--motion-fast),
    opacity var(--motion-fast);
}

.route-card:hover,
.route-card.is-active {
  border-color: var(--realm-accent);
  background: color-mix(in srgb, var(--realm-accent) 16%, rgba(18, 26, 28, 0.9));
  box-shadow: var(--shadow-glow);
  transform: translateY(-2px);
}

.route-card:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--route-state-color) 72%, white);
  outline-offset: 3px;
}

.route-card__icon {
  display: inline-grid;
  width: 2.8rem;
  height: 2.8rem;
  place-items: center;
  border: 1px solid color-mix(in srgb, var(--route-state-color) 54%, rgba(255, 255, 255, 0.12));
  border-radius: 999px;
  color: var(--route-state-color);
  background:
    radial-gradient(circle at 35% 25%, color-mix(in srgb, var(--route-state-color) 22%, transparent), transparent 62%),
    rgba(255, 255, 255, 0.055);
  box-shadow: inset 0 0 18px rgba(0, 0, 0, 0.24);
}

.route-card__glyph {
  width: 1.7rem;
  height: 1.7rem;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.75;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.route-card__body {
  display: grid;
  gap: 0.35rem;
  min-width: 0;
}

.route-card strong {
  color: #fff3d6;
}

.route-card small,
.muted {
  color: var(--color-mist);
}

.route-card__state {
  grid-column: 1 / -1;
  justify-self: start;
  align-self: end;
  border: 1px solid color-mix(in srgb, var(--route-state-color) 42%, rgba(255, 255, 255, 0.12));
  border-radius: 999px;
  padding: 0.18rem 0.62rem;
  color: color-mix(in srgb, var(--route-state-color) 78%, #fff7da);
  background: color-mix(in srgb, var(--route-state-color) 12%, rgba(0, 0, 0, 0.24));
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.route-card-state--active {
  --route-state-color: var(--realm-accent);
  border-color: color-mix(in srgb, var(--realm-accent) 72%, rgba(255, 255, 255, 0.12));
}

.route-card-state--available {
  --route-state-color: color-mix(in srgb, var(--route-tint, var(--realm-accent)) 78%, #f8e4b5);
}

.route-card-state--locked {
  --route-state-color: color-mix(in srgb, var(--color-mist) 72%, #7b705f);
  border-style: dashed;
  opacity: 0.82;
}

.route-card-state--locked:hover {
  opacity: 1;
}

.route-card-state--loading {
  --route-state-color: var(--state-qte);
}

.route-card-state--attention {
  --route-state-color: var(--state-repair);
  border-color: color-mix(in srgb, var(--state-repair) 58%, rgba(255, 255, 255, 0.12));
}

.route-card--home { --route-tint: var(--color-gold); }
.route-card--game { --route-tint: var(--realm-chaos); }
.route-card--soul { --route-tint: var(--realm-shining); }
.route-card--world { --route-tint: #88c58f; }
.route-card--journal { --route-tint: #cda6ff; }
.route-card--inventory { --route-tint: #e0a66c; }
.route-card--media { --route-tint: var(--state-qte); }
.route-card--settings { --route-tint: #9fb7ff; }

.route-card--home,
.route-card--game,
.route-card--soul,
.route-card--world,
.route-card--journal,
.route-card--inventory,
.route-card--media,
.route-card--settings {
  border-color: color-mix(in srgb, var(--route-tint) 24%, rgba(255, 255, 255, 0.08));
}

.route-grid--utility {
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: var(--radius-md);
  padding: var(--space-3);
  background: rgba(0, 0, 0, 0.16);
}

.route-grid--utility .route-card {
  min-height: 6.2rem;
  box-shadow: none;
}
```

---

### Task 4: Document the route icon/state convention

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`

- [ ] **Step 1: Insert route icon/state section after the React app shell section**

Add:

```markdown
## Route iconography and states (#721)

Issue #721 replaces default route emoji tiles with local inline SVG glyphs. `playerRoutes` should store `RouteIconId` values, and `RouteGlyph` renders the decorative SVG inside each route card. Do not add external icon packages or return to emoji literals for default player routes.

Route cards expose semantic presentation states derived from the existing browser shell results: `active`, `available`, `locked`, `loading`, and `attention`. `locked` is the ordinary no-session/no-active-chapter state and should stay muted, not red. `attention` is reserved for real endpoint failures or repair/error turn states. These states are visual/accessibility hints only; C# remains authoritative for session, save/load, turn, and gameplay rules.
```

---

### Task 5: Verify, review, commit, and PR

**Files:**
- Verify all changed files.

- [ ] **Step 1: Run focused test after implementation**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserRouteCards_UseInlineSvgIconsAndSemanticStates" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 2: Run frontend verify**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: typecheck and Vite build complete successfully.

- [ ] **Step 3: Run focused browser frontend .NET tests**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Run diff whitespace check**

```bash
git diff --check
```

Expected: no output and exit code 0.

- [ ] **Step 5: Run independent review**

Dispatch an independent reviewer with the diff and issue/spec context. Fix Critical/Important findings and re-review until approved.

- [ ] **Step 6: Commit**

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.WebFrontend/README.md docs/superpowers/specs/2026-05-26-issue-721-browser-route-icons-design.md docs/superpowers/plans/2026-05-26-issue-721-browser-route-icons.md
git commit -m "feat(web-ui): add route icon state system"
```

- [ ] **Step 7: Push and create PR**

```bash
git push -u origin task/721-browser-route-icons
gh pr create --title "feat(web-ui): add route icon state system" --body-file <prepared-body>
```

PR body must include `Closes #721`, summary, tests, review evidence, and docs impact.

- [ ] **Step 8: Wait for CI, squash-merge, and update main**

```bash
gh pr checks --watch
gh pr merge --squash --delete-branch
git checkout main
git pull --ff-only origin main
```

Expected: CI green, PR merged, issue #721 closed automatically.
