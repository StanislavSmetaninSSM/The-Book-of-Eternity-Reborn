# Browser Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issue #720 by turning the Browser Client `Главная` route into a player-facing start launcher with one dominant primary CTA and lower-priority save/config actions.

**Architecture:** Keep C# browser APIs as the source of truth. React consumes `BrowserMainMenuDto`, derives launcher view models, routes within existing player screens, and calls the existing `browserApi.loadSave()` endpoint for save selection. No gameplay rules, save/load rules, or afterlife/mortal contracts move into React.

**Tech Stack:** React 19 + TypeScript + Vite frontend, .NET 8 xUnit source guards, existing `LocalWebUiMainMenuService` browser APIs, GitHub issue #720.

---

## File Structure

- Create: `docs/superpowers/specs/2026-05-26-browser-launcher-design.md`
  - Design record and unattended approval rationale.
- Create: `docs/superpowers/plans/2026-05-26-browser-launcher.md`
  - This plan.
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
  - Add #720 source guard for launcher structure, primary CTA, secondary action hierarchy, save list, and advanced separation.
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
  - Add launcher types/helper functions.
  - Pass `setActiveRoute` and `loadBrowserState` into `HomeRoute`.
  - Replace action-grid-only `HomeRoute` with `GameLauncher`.
  - Wire save slot loading to `browserApi.loadSave()` and refresh browser state.
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
  - Add launcher window, mode tabs, primary CTA, secondary action, save list, and mobile-friendly grid styles.

---

### Task 1: Add #720 Guard Test and Implement Game Launcher

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
- Create: `docs/superpowers/specs/2026-05-26-browser-launcher-design.md`
- Create: `docs/superpowers/plans/2026-05-26-browser-launcher.md`

- [ ] **Step 1: Write the failing source guard**

Add this xUnit test after `BrowserDefaultScreen_UsesPlayerFacingCopyAndNeutralEmptyStates()` in `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`:

```csharp
    [Fact]
    public void BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        Assert.Contains("function GameLauncher", app, StringComparison.Ordinal);
        Assert.Contains("interface LauncherPrimaryAction", app, StringComparison.Ordinal);
        Assert.Contains("selectPrimaryLauncherAction(", app, StringComparison.Ordinal);
        Assert.Contains("launcher-primary-action", app, StringComparison.Ordinal);
        Assert.Contains("launcher-mode-tabs", app, StringComparison.Ordinal);
        Assert.Contains("launcher-save-list", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.loadSave({ saveId: slot.saveId })", app, StringComparison.Ordinal);
        Assert.Contains("onActiveRouteChange('game')", app, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", app, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", app, StringComparison.Ordinal);
        Assert.Contains("Начать новую главу", app, StringComparison.Ordinal);
        Assert.Contains("Загрузить сохранение", app, StringComparison.Ordinal);
        Assert.Contains("Настроить клиент", app, StringComparison.Ordinal);
        Assert.Contains("Сведения о книге", app, StringComparison.Ordinal);
        Assert.Contains("className=\"launcher-secondary-actions\"", app, StringComparison.Ordinal);
        Assert.Contains("className=\"advanced-toggle\"", app, StringComparison.Ordinal);

        Assert.Contains(".game-launcher", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-primary-action", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-secondary-actions", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-mode-tabs", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-save-list", styles, StringComparison.Ordinal);

        var primaryIndex = app.IndexOf("launcher-primary-action", StringComparison.Ordinal);
        var secondaryIndex = app.IndexOf("launcher-secondary-actions", StringComparison.Ordinal);
        var advancedIndex = app.IndexOf("className=\"advanced-toggle\"", StringComparison.Ordinal);
        Assert.True(primaryIndex > 0, "Launcher primary CTA must be explicit.");
        Assert.True(secondaryIndex > primaryIndex, "Secondary actions must follow the primary CTA.");
        Assert.True(advancedIndex > secondaryIndex, "Advanced mode must stay lower priority than launcher actions in source order.");
    }
```

- [ ] **Step 2: Run the focused guard and verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta" --logger "console;verbosity=minimal"
```

Expected: FAIL because `GameLauncher`, `launcher-primary-action`, launcher tabs/save list, and `browserApi.loadSave({ saveId: slot.saveId })` are not implemented yet.

- [ ] **Step 3: Add launcher props and view-model types**

In `BookOfEternityClient.WebFrontend/src/App.tsx`:

1. Change the home route call in `renderActiveRoute` from:

```tsx
return <HomeRoute state={state} advancedEnabled={advancedEnabled} />;
```

to:

```tsx
return <HomeRoute state={state} advancedEnabled={advancedEnabled} onActiveRouteChange={setActiveRoute} onStateRefresh={loadBrowserState} />;
```

2. Add these types near `EmptyStateCopy`:

```tsx
type LauncherMode = 'continue' | 'load' | 'new-game' | 'settings' | 'about';

interface LauncherPrimaryAction {
  mode: LauncherMode;
  label: string;
  description: string;
  enabled: boolean;
  disabledReason: string;
}
```

3. Update `renderActiveRoute` parameters/signature to accept `setActiveRoute: (route: RouteId) => void` and `loadBrowserState: () => Promise<void>`, and pass both from the call site:

```tsx
{readyState && renderActiveRoute(activeRoute, readyState, composerText, setComposerText, composerNotice, submitComposer, advancedEnabled, setActiveRoute, loadBrowserState)}
```

- [ ] **Step 4: Implement `HomeRoute` as a launcher shell**

Replace `HomeRoute` with a component that:

```tsx
function HomeRoute({
  state,
  advancedEnabled,
  onActiveRouteChange,
  onStateRefresh
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  advancedEnabled: boolean;
  onActiveRouteChange: (route: RouteId) => void;
  onStateRefresh: () => Promise<void>;
}) {
  if (!isSuccess(state.menu)) {
    return <EmptyOrFailure result={state.menu} advancedEnabled={advancedEnabled} errorTitle="Главное меню требует внимания" empty={{
      title: 'Книга ждёт открытия',
      message: 'Главная страница появится, когда локальная книга подготовит меню продолжения.',
      action: 'Откройте книгу: начните новую главу, продолжите сохранение или загрузите партию из доступных действий клиента.'
    }} />;
  }

  return <GameLauncher menu={state.menu.data} onActiveRouteChange={onActiveRouteChange} onStateRefresh={onStateRefresh} />;
}
```

Add `GameLauncher` after `HomeRoute`. It must:

- compute `const primaryAction = selectPrimaryLauncherAction(menu);`
- keep `const [launcherMode, setLauncherMode] = useState<LauncherMode>(primaryAction.mode);`
- keep a player-facing `loadNotice` string;
- render `<article className="game-launcher">` with a hero/status panel, one `<button className="launcher-primary-action">`, `<div className="launcher-mode-tabs">`, contextual panel content, `<div className="launcher-secondary-actions">`, and save list.

The primary click handler must use existing UI/API surfaces:

```tsx
function activateLauncherMode(mode: LauncherMode) {
  setLauncherMode(mode);
  if (mode === 'continue') {
    onActiveRouteChange('game');
  }
  if (mode === 'settings') {
    onActiveRouteChange('settings');
  }
}
```

The save loader must call the existing API and refresh state:

```tsx
async function loadSaveSlot(slot: BrowserMainMenuDto['saves'][number]) {
  setLoadNotice(`Открываем сохранение «${slot.displayName}»…`);
  const result = await browserApi.loadSave({ saveId: slot.saveId });
  if (isSuccess(result) && result.data.success) {
    setLoadNotice('Сохранение загружено. Обновляем книгу…');
    await onStateRefresh();
    onActiveRouteChange('game');
    return;
  }
  const message = isSuccess(result) ? result.data.error : result.playerMessage;
  setLoadNotice(toPlayerFacingText(message, 'Сохранение сейчас не удалось открыть.'));
}
```

- [ ] **Step 5: Add launcher helper functions**

Add helpers near other formatting helpers:

```tsx
function selectPrimaryLauncherAction(menu: BrowserMainMenuDto): LauncherPrimaryAction {
  const continueAction = findMainMenuAction(menu, 'continue');
  if (continueAction?.enabled) {
    return toLauncherPrimaryAction('continue', 'Продолжить главу', continueAction);
  }

  const loadAction = findMainMenuAction(menu, 'load');
  if (loadAction?.enabled) {
    return toLauncherPrimaryAction('load', 'Загрузить сохранение', loadAction);
  }

  const newGameAction = findMainMenuAction(menu, 'new-game');
  if (newGameAction?.enabled) {
    return toLauncherPrimaryAction('new-game', 'Начать новую главу', newGameAction);
  }

  return {
    mode: 'continue',
    label: 'Открыть книгу',
    description: toPlayerFacingText(menu.session.continueReason, 'Выберите сохранение или подготовьте новую главу.'),
    enabled: false,
    disabledReason: toPlayerFacingText(menu.session.continueReason, 'Книга ждёт доступного действия.')
  };
}

function findMainMenuAction(menu: BrowserMainMenuDto, id: string) {
  return menu.actions.find((action) => action.id === id) ?? null;
}

function toLauncherPrimaryAction(mode: LauncherMode, label: string, action: BrowserMainMenuDto['actions'][number]): LauncherPrimaryAction {
  return {
    mode,
    label,
    description: toPlayerFacingText(action.description, 'Действие готово.'),
    enabled: action.enabled,
    disabledReason: toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.')
  };
}
```

- [ ] **Step 6: Add launcher CSS**

In `BookOfEternityClient.WebFrontend/src/styles/components.css`, add focused styles:

```css
.game-launcher {
  display: grid;
  gap: var(--space-4);
}

.launcher-window {
  display: grid;
  gap: var(--space-4);
  border: 1px solid color-mix(in srgb, var(--realm-accent) 36%, rgba(255, 255, 255, 0.08));
  border-radius: var(--radius-lg);
  padding: clamp(1rem, 3vw, 1.5rem);
  background: radial-gradient(circle at 50% 0%, color-mix(in srgb, var(--realm-accent) 14%, transparent), transparent 62%), rgba(255, 255, 255, 0.045);
}

.launcher-primary-action {
  width: 100%;
  border: 1px solid color-mix(in srgb, var(--realm-accent) 64%, rgba(255, 255, 255, 0.18));
  border-radius: var(--radius-md);
  padding: 1rem 1.15rem;
  background: linear-gradient(135deg, color-mix(in srgb, var(--realm-accent) 34%, rgba(12, 16, 18, 0.92)), rgba(246, 230, 184, 0.12));
  color: #fff6df;
  font-size: clamp(1.05rem, 2vw, 1.25rem);
  font-weight: 900;
  text-align: left;
  box-shadow: var(--shadow-glow);
}

.launcher-primary-action span,
.launcher-secondary-action span,
.launcher-save-card span {
  display: block;
  margin-top: 0.25rem;
  color: var(--color-mist);
  font-size: 0.9rem;
  font-weight: 500;
}

.launcher-mode-tabs,
.launcher-secondary-actions,
.launcher-save-list {
  display: grid;
  gap: 0.65rem;
}

.launcher-mode-tabs,
.launcher-secondary-actions {
  grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
}

.launcher-mode-tabs button,
.launcher-secondary-action,
.launcher-save-card {
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 0.9rem;
  padding: 0.85rem;
  background: rgba(255, 255, 255, 0.045);
  color: inherit;
  text-align: left;
}

.launcher-mode-tabs button.is-active,
.launcher-secondary-action:hover,
.launcher-save-card:hover {
  border-color: var(--realm-accent);
  background: color-mix(in srgb, var(--realm-accent) 12%, rgba(255, 255, 255, 0.05));
}

.launcher-save-card {
  display: grid;
  gap: 0.25rem;
}
```

- [ ] **Step 7: Run focused guard and verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta" --logger "console;verbosity=minimal"
```

Expected: PASS, 1 test passed, 0 failed.

- [ ] **Step 8: Run frontend and browser-focused verification**

Run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUi" --logger "console;verbosity=minimal"
git diff --check
```

Expected: all commands exit 0; no new Browser/LocalWebUi failures.

- [ ] **Step 9: Commit**

Stage only intentional files and commit:

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/components.css docs/superpowers/specs/2026-05-26-browser-launcher-design.md docs/superpowers/plans/2026-05-26-browser-launcher.md
git commit -m "feat(web-ui): add browser start launcher"
```
