# Issue #727 Browser Navigation IA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Browser Client default navigation read as a game-client taxonomy, with technical routes behind explicit advanced mode.

**Architecture:** React remains a presentation shell over existing C# DTOs. The change adds journal/inventory presentation routes, splits primary player routes from secondary utility routes, and guards the route order/source boundary with .NET source tests.

**Tech Stack:** .NET 8 xUnit tests, React 18, TypeScript, Vite, plain CSS.

---

## File map

- Modify `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`: add a failing source guard for #727 route taxonomy, utility separation, advanced-only diagnostics, and locked states.
- Modify `BookOfEternityClient.WebFrontend/src/App.tsx`: extend route ids, route metadata, nav rendering, active-route dispatch, and add `JournalRoute`/`InventoryRoute` plus small action-section filter helpers.
- Modify `BookOfEternityClient.WebFrontend/src/styles/components.css`: add utility route styling and route tints for journal/inventory.
- Modify `BookOfEternityClient.WebFrontend/src/styles/layout.css`: add responsive layout for primary and utility route navigation.
- Modify `BookOfEternityClient.WebFrontend/README.md`: document #727 taxonomy.
- Modify `docs/web-ui/local-web-host.md`: document #727 taxonomy in the Browser MVP section.
- Create/keep `docs/superpowers/specs/2026-05-26-issue-727-browser-navigation-ia-design.md` and this plan.

---

### Task 1: Add failing #727 route taxonomy guard

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this test after `ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn`:

```csharp
    [Fact]
    public void BrowserNavigationIa_SeparatesPrimaryPlayerRoutesFromUtilityAndAdvancedRoutes()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("type RouteKind = 'primary' | 'utility';", app, StringComparison.Ordinal);
        Assert.Contains("const primaryPlayerRoutes = playerRoutes.filter((route) => route.kind === 'primary');", app, StringComparison.Ordinal);
        Assert.Contains("const utilityPlayerRoutes = playerRoutes.filter((route) => route.kind === 'utility');", app, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Основные игровые разделы браузерного клиента\"", app, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Дополнительные игровые разделы браузерного клиента\"", app, StringComparison.Ordinal);
        Assert.Contains("className=\"route-grid route-grid--primary\"", app, StringComparison.Ordinal);
        Assert.Contains("className=\"route-grid route-grid--utility\"", app, StringComparison.Ordinal);

        var routeOrder = new[] { "id: 'home'", "id: 'game'", "id: 'soul'", "id: 'world'", "id: 'journal'", "id: 'inventory'", "id: 'media'", "id: 'settings'" };
        var previousIndex = -1;
        foreach (var marker in routeOrder)
        {
            var index = app.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Route marker {marker} should appear after the previous player route marker.");
            previousIndex = index;
        }

        Assert.Contains("label: 'Журнал'", app, StringComparison.Ordinal);
        Assert.Contains("label: 'Инвентарь'", app, StringComparison.Ordinal);
        Assert.Contains("function JournalRoute", app, StringComparison.Ordinal);
        Assert.Contains("function InventoryRoute", app, StringComparison.Ordinal);
        Assert.Contains("filterActionSections(game.actionMenu, journalSectionMatchers)", app, StringComparison.Ordinal);
        Assert.Contains("filterActionSections(game.actionMenu, inventorySectionMatchers)", app, StringComparison.Ordinal);
        Assert.Contains("Журнал ждёт главу", app, StringComparison.Ordinal);
        Assert.Contains("Инвентарь ждёт главу", app, StringComparison.Ordinal);
        Assert.Contains("Сводка / Игра / Душа / Мир / Журнал / Инвентарь", app, StringComparison.Ordinal);

        var routeArrayStart = app.IndexOf("const playerRoutes", StringComparison.Ordinal);
        var routeArrayEnd = app.IndexOf("const fallbackTheme", StringComparison.Ordinal);
        Assert.True(routeArrayStart >= 0 && routeArrayEnd > routeArrayStart, "Route metadata should stay near the top of App.tsx.");
        var routeMetadata = app[routeArrayStart..routeArrayEnd];
        Assert.DoesNotContain("Debug", routeMetadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", routeMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("Network", routeMetadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command coverage", routeMetadata, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(".route-grid--primary", styles, StringComparison.Ordinal);
        Assert.Contains(".route-grid--utility", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card--journal", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card--inventory", styles, StringComparison.Ordinal);

        Assert.Contains("#727", readme, StringComparison.Ordinal);
        Assert.Contains("Главная → Игра → Душа → Мир → Журнал → Инвентарь", readme, StringComparison.Ordinal);
        Assert.Contains("#727", hostDoc, StringComparison.Ordinal);
        Assert.Contains("Главная → Игра → Душа → Мир → Журнал → Инвентарь", hostDoc, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run focused test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserNavigationIa_SeparatesPrimaryPlayerRoutesFromUtilityAndAdvancedRoutes" --logger "console;verbosity=minimal"
```

Expected: FAIL because the new test method references route metadata/helpers/docs that do not exist yet.

- [ ] **Step 3: Commit is not allowed yet**

Do not commit until the test has gone green with implementation.

---

### Task 2: Implement the React route taxonomy and route surfaces

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`

- [ ] **Step 1: Extend route types and metadata**

Replace the route type and `RouteCard`/`playerRoutes` definitions with:

```typescript
type RouteId = 'home' | 'game' | 'soul' | 'world' | 'journal' | 'inventory' | 'media' | 'settings';
type RouteKind = 'primary' | 'utility';

interface RouteCard {
  id: RouteId;
  kind: RouteKind;
  label: string;
  description: string;
  icon: string;
}

const playerRoutes: RouteCard[] = [
  { id: 'home', kind: 'primary', label: 'Главная', description: 'Сводка партии, продолжение, загрузка и безопасные действия.', icon: '✦' },
  { id: 'game', kind: 'primary', label: 'Игра', description: 'Текущая сцена, нарратив, ход ГМа и основной художественный ввод.', icon: '📖' },
  { id: 'soul', kind: 'primary', label: 'Душа', description: 'Персонаж, душа, состояние героя и текущий слой мира.', icon: '🕯️' },
  { id: 'world', kind: 'primary', label: 'Мир', description: 'Локация, карта, фракции и игровые действия окружения.', icon: '🗺️' },
  { id: 'journal', kind: 'primary', label: 'Журнал', description: 'Квесты, хроника, заметки, архив и история текущей главы.', icon: '✍️' },
  { id: 'inventory', kind: 'primary', label: 'Инвентарь', description: 'Предметы, экипировка, ремесло и локальные хранилища.', icon: '🎒' },
  { id: 'media', kind: 'utility', label: 'Медиа', description: 'Галерея, быстрые сцены и игровые материалы.', icon: '🎞️' },
  { id: 'settings', kind: 'utility', label: 'Настройки', description: 'Локальный профиль, звук, язык и комфорт клиента.', icon: '⚙️' }
];

const primaryPlayerRoutes = playerRoutes.filter((route) => route.kind === 'primary');
const utilityPlayerRoutes = playerRoutes.filter((route) => route.kind === 'utility');
```

- [ ] **Step 2: Split navigation rendering**

Replace the single `<nav className="route-grid" ...>` with two nav blocks:

```tsx
      <nav className="route-grid route-grid--primary" aria-label="Основные игровые разделы браузерного клиента">
        {primaryPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, setActiveRoute))}
      </nav>

      <nav className="route-grid route-grid--utility" aria-label="Дополнительные игровые разделы браузерного клиента">
        <p className="utility-route-heading">Сводка / Игра / Душа / Мир / Журнал / Инвентарь — основная цепочка игрока. Медиа и настройки доступны отдельно.</p>
        {utilityPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, setActiveRoute))}
      </nav>
```

Add this helper before `PlayerStatusSidebar`:

```tsx
function renderRouteButton(route: RouteCard, activeRoute: RouteId, setActiveRoute: (route: RouteId) => void): ReactNode {
  return (
    <button
      key={route.id}
      type="button"
      className={`route-card route-card--${route.id}${activeRoute === route.id ? ' is-active' : ''}`}
      onClick={() => setActiveRoute(route.id)}
      aria-pressed={activeRoute === route.id}
    >
      <span aria-hidden="true">{route.icon}</span>
      <strong>{route.label}</strong>
      <small>{route.description}</small>
    </button>
  );
}
```

- [ ] **Step 3: Dispatch the new routes**

Add cases in `renderActiveRoute`:

```tsx
    case 'journal':
      return <JournalRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'inventory':
      return <InventoryRoute state={state} advancedEnabled={advancedEnabled} />;
```

- [ ] **Step 4: Add journal/inventory route helpers and components**

Add after `WorldRoute`:

```tsx
const journalSectionMatchers = ['quest', 'квест', 'journal', 'журнал', 'archive', 'архив', 'chronicle', 'хроника', 'story', 'история', 'faction', 'фракц', 'guardian', 'хранител'];
const inventorySectionMatchers = ['inventory', 'инвентар', 'item', 'предмет', 'craft', 'ремес', 'equip', 'экип', 'storage', 'хранилищ'];

function JournalRoute({ state, advancedEnabled }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean }) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Журнал требует внимания" empty={{
      title: 'Журнал ждёт главу',
      message: 'Квесты, хроника и заметки появятся после открытия или загрузки игровой сессии.',
      action: 'Откройте книгу на главной странице, затем вернитесь в журнал.'
    }} />;
  }

  const game = state.game.data;
  const sections = filterActionSections(game.actionMenu, journalSectionMatchers);

  return (
    <ShellPanel title="Журнал" eyebrow="квесты, хроника и заметки">
      <div className="split-grid">
        <div className="summary-card">
          <h2>Текущая глава</h2>
          <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
        </div>
        <div className="summary-card">
          <h2>Ориентир игрока</h2>
          <p>{game.narrative.dialogueOptions.length > 0 ? `Доступно вариантов: ${game.narrative.dialogueOptions.length}.` : 'Варианты выбора появятся после ответа ГМа.'}</p>
          <p className="muted">Журнал показывает игровые разделы из C# каталога действий без raw slash-команд.</p>
        </div>
      </div>
      <FilteredActionSections sections={sections} emptyMessage="Квестовые, архивные и фракционные разделы появятся здесь, когда C# каталог действий отдаст их для текущей главы." />
    </ShellPanel>
  );
}

function InventoryRoute({ state, advancedEnabled }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean }) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Инвентарь требует внимания" empty={{
      title: 'Инвентарь ждёт главу',
      message: 'Предметы, экипировка, ремесло и хранилища появятся после открытия или загрузки игровой сессии.',
      action: 'Откройте книгу на главной странице, затем вернитесь к инвентарю.'
    }} />;
  }

  const game = state.game.data;
  const sections = filterActionSections(game.actionMenu, inventorySectionMatchers);

  return (
    <ShellPanel title="Инвентарь" eyebrow="предметы, ремесло и хранилища">
      <div className="split-grid">
        <div className="summary-card">
          <h2>Герой</h2>
          <p>{game.player.name || 'Герой'} · {game.player.currentCondition}</p>
          <p className="muted">Здоровье {formatSidebarStatusMetric(game.player.healthPercentage)} · энергия {formatSidebarStatusMetric(game.player.energyPercentage)} · стойкость {formatSidebarStatusMetric(game.player.poisePercentage)}</p>
        </div>
        <div className="summary-card">
          <h2>Ремесло и предметы</h2>
          <p>Инвентарь использует существующие C# игровые действия и не добавляет отдельные правила предметов в React.</p>
        </div>
      </div>
      <FilteredActionSections sections={sections} emptyMessage="Инвентарные, ремесленные и складские разделы появятся здесь, когда C# каталог действий отдаст их для текущей главы." />
    </ShellPanel>
  );
}

function FilteredActionSections({ sections, emptyMessage }: { sections: BrowserPlayerCommandSectionDto[]; emptyMessage: string }) {
  if (sections.length === 0) {
    return <p className="muted">{emptyMessage}</p>;
  }

  return (
    <section className="action-menu" aria-label="Игровые разделы страницы">
      <div className="action-section-grid">
        {sections.map((section) => <ActionSection key={section.id} section={section} />)}
      </div>
    </section>
  );
}

function filterActionSections(menu: BrowserPlayerCommandMenuDto, matchers: string[]): BrowserPlayerCommandSectionDto[] {
  return menu.sections.flatMap((section) => {
    if (!section.playerDefault || section.actions.length === 0) {
      return [];
    }

    const matchingActions = section.actions.filter((action) => matchesActionSectionOrAction(section, action, matchers));
    if (matchingActions.length === 0) {
      return [];
    }

    return [{ ...section, actions: matchingActions }];
  });
}

function matchesActionSectionOrAction(
  section: BrowserPlayerCommandSectionDto,
  action: BrowserPlayerCommandActionDto,
  matchers: string[]
): boolean {
  const haystack = [
    section.id,
    section.label,
    section.description,
    action.id,
    action.label,
    action.description,
    action.formLabel,
    action.formPrompt,
    action.advancedCommand
  ].join(' ').toLocaleLowerCase('ru-RU');
  const normalizedMatchers = matchers.map((matcher) => matcher.toLocaleLowerCase('ru-RU'));

  return normalizedMatchers.some((matcher) => haystack.includes(matcher));
}
```

- [ ] **Step 5: Run frontend typecheck before style/docs**

Run:

```bash
npm run typecheck --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

---

### Task 3: Style the primary/utility route split

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/layout.css`

- [ ] **Step 1: Add component styling**

In `components.css`, update route tints:

```css
.route-card--home { --route-tint: var(--color-gold); }
.route-card--game { background: color-mix(in srgb, var(--realm-accent) 18%, rgba(18, 26, 28, 0.9)); }
.route-card--soul { --route-tint: var(--realm-shining); }
.route-card--world { --route-tint: #88c58f; }
.route-card--journal { --route-tint: #cda6ff; }
.route-card--inventory { --route-tint: #e0a66c; }
.route-card--media { --route-tint: var(--state-qte); }
.route-card--settings { --route-tint: #9fb7ff; }
```

Update the selector list so it includes journal/inventory:

```css
.route-card--home,
.route-card--soul,
.route-card--world,
.route-card--journal,
.route-card--inventory,
.route-card--media,
.route-card--settings {
  border-color: color-mix(in srgb, var(--route-tint) 24%, rgba(255, 255, 255, 0.08));
}
```

Add after that selector:

```css
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

.utility-route-heading {
  align-self: center;
  margin: 0;
  color: var(--color-mist);
  font-size: 0.88rem;
}
```

- [ ] **Step 2: Add layout styling**

In `layout.css`, replace the `.route-grid` block with:

```css
.route-grid {
  display: grid;
  gap: var(--space-3);
  margin: var(--space-4) 0;
}

.route-grid--primary {
  grid-template-columns: repeat(6, minmax(0, 1fr));
}

.route-grid--utility {
  grid-template-columns: minmax(16rem, 1fr) repeat(2, minmax(10rem, 14rem));
  margin-top: calc(var(--space-4) * -0.35);
}
```

Update media queries:

```css
@media (max-width: 1120px) {
  .route-grid--primary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .route-grid--utility {
    grid-template-columns: 1fr 1fr;
  }

  .utility-route-heading {
    grid-column: 1 / -1;
  }

  .action-grid,
  .split-grid.three {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 840px) {
  ...
  .route-grid--primary,
  .route-grid--utility {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  ...
}

@media (max-width: 640px) {
  ...
  .route-grid--primary,
  .route-grid--utility {
    grid-template-columns: 1fr;
  }
  ...
}
```

- [ ] **Step 3: Run focused source guard**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserNavigationIa_SeparatesPrimaryPlayerRoutesFromUtilityAndAdvancedRoutes" --logger "console;verbosity=minimal"
```

Expected: still FAIL until docs are updated.

---

### Task 4: Update docs for #727 taxonomy

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Update frontend README**

Replace the React app shell route paragraph with text including:

```markdown
Issue #727 refines the player navigation taxonomy. The primary chain is now `Главная → Игра → Душа → Мир → Журнал → Инвентарь`, matching the game-client order: summary/current scene, character/soul, world/location/map, journal/quests/notes, and inventory/craft. `Медиа` and `Настройки` remain player utility sections below the primary chain. Technical diagnostics, raw endpoint/API details, lifecycle validation internals, command coverage, and slash-command workflows remain behind explicit `Расширенный режим` opt-in.
```

Update the future route bullets so journal and inventory have their own bullets:

```markdown
- map, location, factions, and world actions under `Мир`;
- quests, chronicle, archive, notes, and story-facing sections under `Журнал`;
- items, equipment, craft, and storage-facing sections under `Инвентарь`;
```

- [ ] **Step 2: Update local host doc**

In `docs/web-ui/local-web-host.md`, add #727 to tracked tasks and update the route paragraphs with the same primary route chain and advanced-only boundary.

- [ ] **Step 3: Run focused guard to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserNavigationIa_SeparatesPrimaryPlayerRoutesFromUtilityAndAdvancedRoutes" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Add action-level inventory regression guard after review feedback**

Add `BrowserNavigationIa_InventoryRouteMatchesCurrentActionMetadata` to `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`. It parses `src/api/contract-fixtures/game-screen.json`, proves current metadata exposes `inventory`, `storage_access`, and `craft` actions under broader `soul`/`world` sections, then asserts `App.tsx` filters matching actions rather than only matching section metadata.

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserNavigationIa_InventoryRouteMatchesCurrentActionMetadata" --logger "console;verbosity=minimal"
```

Expected: FAIL before the action-level filter, PASS after `filterActionSections` returns section copies with matching actions.

---

### Task 5: Verification, review, PR, merge

**Files:**
- All files modified above.

- [ ] **Step 1: Run frontend verification**

Run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS with TypeScript typecheck and Vite build.

- [ ] **Step 2: Run focused browser/frontend tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 3: Run broader browser web UI tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiBuiltFrontend|Category=BrowserWebUiSmoke|Category=BrowserWebUiParity" --logger "console;verbosity=minimal"
```

Expected: PASS. If this is too slow for cron, run focused tests plus `npm run verify`, then let CI run the broader suite before merge.

- [ ] **Step 4: Diff hygiene and static scan**

Run:

```bash
git diff --check
git diff --cached | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]" || echo NO_SECRET_MATCHES
git diff --cached | grep '^+' | grep -E "os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_INJECTION_MATCHES
```

Expected: no whitespace errors, no secret/injection matches.

- [ ] **Step 5: Independent review**

Dispatch a reviewer with the issue body, design, plan, and diff. Required verdict: issue #727 acceptance criteria are met, no Critical/Important findings. Fix any findings and re-run focused verification.

- [ ] **Step 6: Commit and PR**

Run:

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.WebFrontend/src/styles/layout.css BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md docs/superpowers/specs/2026-05-26-issue-727-browser-navigation-ia-design.md docs/superpowers/plans/2026-05-26-issue-727-browser-navigation-ia.md
git commit -m "feat(web-ui): refine browser navigation IA"
git push -u origin task/727-browser-navigation-ia
gh pr create --title "feat(web-ui): refine browser navigation IA" --body-file <generated-pr-body-file>
```

PR body must include `Closes #727`, summary, verification commands/results, independent review result, and docs impact.

- [ ] **Step 7: CI and merge**

Wait for GitHub checks. If green, squash-merge the PR to `main`, delete the remote branch, pull local main, verify issue #727 closed. If CI fails, use systematic debugging and do not merge until green.
