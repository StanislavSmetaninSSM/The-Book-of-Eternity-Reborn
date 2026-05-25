# Browser Sidebar Player Status Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issue #722 by turning the Browser Client default sidebar from a debug/status dashboard into a player-facing book/world status panel.

**Architecture:** Keep C# and existing browser DTOs authoritative. Add a React presentational sidebar component that derives player-friendly status cards from already-loaded menu/session/game/audio results, and add source/CSS guards so raw sidebar debug copy does not return.

**Tech Stack:** .NET 8/xUnit source guards, React 18 + TypeScript + Vite, CSS design-system files under `BookOfEternityClient.WebFrontend/src/styles/`.

---

## File structure

- Modify `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
  - Add one focused xUnit source guard for #722 player-status sidebar requirements.
- Modify `BookOfEternityClient.WebFrontend/src/App.tsx`
  - Add `PlayerStatusSidebar`, `StatusSummaryCard`, `getSidebarFailure`, `getSidebarEmptyGameMessage`, `formatSidebarSessionSummary`, `formatSidebarSaveSummary`, `formatSidebarStatusMetric`, and `formatSidebarAudioSummary` helper functions.
  - Replace the default raw sidebar panels with `PlayerStatusSidebar`.
  - Keep `AudioSettingsPanel` and advanced diagnostics behind existing shared C# DTO/state paths.
- Modify `BookOfEternityClient.WebFrontend/src/styles/components.css`
  - Add `.player-status-sidebar`, `.status-summary-card`, `.status-summary-card.is-soft`, `.status-summary-card.is-attention`, `.status-summary-grid`, `.advanced-sidebar-entry`, and related text/token styling.
- Create `docs/superpowers/specs/2026-05-26-browser-sidebar-status-design.md`
  - Self-reviewed design for unattended #722 work.
- Create `docs/superpowers/plans/2026-05-26-browser-sidebar-status.md`
  - This implementation plan.

---

### Task 1: Add RED source guard for #722 sidebar

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write failing test**

Add this test after `BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta`:

```csharp
    [Fact]
    public void BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        Assert.Contains("function PlayerStatusSidebar", app, StringComparison.Ordinal);
        Assert.Contains("function StatusSummaryCard", app, StringComparison.Ordinal);
        Assert.Contains("className=\"player-status-sidebar\"", app, StringComparison.Ordinal);
        Assert.Contains("Сводка книги", app, StringComparison.Ordinal);
        Assert.Contains("Слой книги", app, StringComparison.Ordinal);
        Assert.Contains("Герой и душа", app, StringComparison.Ordinal);
        Assert.Contains("Сохранение", app, StringComparison.Ordinal);
        Assert.Contains("Ожидание ГМа", app, StringComparison.Ordinal);
        Assert.Contains("Служебная панель", app, StringComparison.Ordinal);
        Assert.Contains("Подробности ремонта, проверки и команд скрыты до явного включения.", app, StringComparison.Ordinal);
        Assert.Contains("formatSidebarSessionSummary(", app, StringComparison.Ordinal);
        Assert.Contains("formatSidebarAudioSummary(", app, StringComparison.Ordinal);
        Assert.Contains("getSidebarFailure(", app, StringComparison.Ordinal);
        Assert.Contains("formatSidebarStatusMetric(", app, StringComparison.Ordinal);
        Assert.Contains("sidebarMenuFailure", app, StringComparison.Ordinal);
        Assert.Contains("sidebarSessionFailure", app, StringComparison.Ordinal);
        Assert.Contains("sidebarGameFailure", app, StringComparison.Ordinal);
        Assert.Contains("attention={Boolean(sidebarGameFailure)}", app, StringComparison.Ordinal);
        Assert.Contains("className=\"warning-text\">{sidebarGameFailure}", app, StringComparison.Ordinal);

        Assert.DoesNotContain("<ShellPanel title=\"Сессия\" eyebrow=\"локальная книга\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<ShellPanel title=\"Ход и ремонт\" eyebrow=\"безопасность хода\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Проверка: {toPlayerFacingText(gameScreen.turnState.validationLabel", app, StringComparison.Ordinal);
        Assert.DoesNotContain("healthPercentage}%", app, StringComparison.Ordinal);
        Assert.DoesNotContain("energyPercentage}%", app, StringComparison.Ordinal);
        Assert.DoesNotContain("poisePercentage}%", app, StringComparison.Ordinal);

        var sidebarIndex = app.IndexOf("className=\"player-status-sidebar\"", StringComparison.Ordinal);
        var advancedEntryIndex = app.IndexOf("className=\"advanced-sidebar-entry\"", StringComparison.Ordinal);
        var diagnosticsIndex = app.IndexOf("function AdvancedDiagnosticsPanel", StringComparison.Ordinal);
        Assert.True(sidebarIndex > 0, "Player status sidebar must render before advanced entry.");
        Assert.True(advancedEntryIndex > sidebarIndex, "Advanced entry should be lower priority than player status cards.");
        Assert.True(diagnosticsIndex > advancedEntryIndex, "Advanced diagnostics implementation should stay outside the default sidebar source slice.");

        Assert.Contains(".player-status-sidebar", styles, StringComparison.Ordinal);
        Assert.Contains(".status-summary-card", styles, StringComparison.Ordinal);
        Assert.Contains(".advanced-sidebar-entry", styles, StringComparison.Ordinal);
        Assert.Contains(".status-summary-grid", styles, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run focused test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard"
```

Expected: FAIL because `PlayerStatusSidebar` and CSS classes do not exist yet.

- [ ] **Step 3: Commit?**

Do not commit yet; proceed to implementation after RED is verified.

---

### Task 2: Implement player-facing sidebar component

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`

- [ ] **Step 1: Replace the default sidebar block**

Replace the current `<aside className="workspace-sidebar" aria-label="Сводка состояния"> ... </aside>` content with:

```tsx
        <aside className="workspace-sidebar" aria-label="Сводка книги">
          <PlayerStatusSidebar
            readyState={readyState}
            menu={menu}
            session={session}
            gameScreen={gameScreen}
            realmTheme={realmTheme}
            activeRoute={activeRoute}
            advancedEnabled={advancedEnabled}
            setAdvancedEnabled={setAdvancedEnabled}
          />
        </aside>
```

- [ ] **Step 2: Add component and helpers before `renderActiveRoute`**

Add:

```tsx
function PlayerStatusSidebar({
  readyState,
  menu,
  session,
  gameScreen,
  realmTheme,
  activeRoute,
  advancedEnabled,
  setAdvancedEnabled
}: {
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null;
  menu: BrowserMainMenuDto | null;
  session: LocalWebUiSessionStatus | null;
  gameScreen: BrowserGameScreenDto | null;
  realmTheme: RealmTheme;
  activeRoute: RouteId;
  advancedEnabled: boolean;
  setAdvancedEnabled: (updater: (value: boolean) => boolean) => void;
}) {
  const sidebarEmptyGame = getSidebarEmptyGameMessage(readyState);
  const hasGame = Boolean(gameScreen);
  const sidebarMenuFailure = getSidebarFailure(readyState?.menu);
  const sidebarSessionFailure = getSidebarFailure(readyState?.session);
  const sidebarGameFailure = getSidebarFailure(readyState?.game);
  const saveNeedsAttention = Boolean(sidebarMenuFailure || sidebarSessionFailure);
  const turnNeedsAttention = Boolean(sidebarGameFailure || gameScreen?.turnState.severity === 'error' || gameScreen?.turnState.severity === 'repair');

  return (
    <div className="player-status-sidebar">
      <div className="sidebar-heading">
        <p className="panel-eyebrow">игровая сводка</p>
        <h2>Сводка книги</h2>
        <p className="muted">Мягкая сводка текущей главы без служебных журналов и внутренних проверок.</p>
      </div>

      <StatusSummaryCard title="Слой книги" eyebrow="мир и глава" attention={Boolean(sidebarMenuFailure || sidebarGameFailure)}>
        <p className="status-pill">{realmTheme.label}</p>
        <p>{gameScreen ? `${gameScreen.soul.name || 'Душа'} · ход ${gameScreen.world.turnNumber}` : sidebarEmptyGame}</p>
        {sidebarMenuFailure ? (
          <p className="warning-text">{sidebarMenuFailure}</p>
        ) : (
          <p className="muted">{menu ? toPlayerFacingText(menu.session.validationLabel, 'Книга ждёт открытия') : 'Книга ждёт открытия.'}</p>
        )}
      </StatusSummaryCard>

      <StatusSummaryCard title="Герой и душа" eyebrow="персонаж" soft={!hasGame && !sidebarGameFailure} attention={Boolean(sidebarGameFailure)}>
        {sidebarGameFailure ? (
          <>
            <p className="warning-text">{sidebarGameFailure}</p>
            <p className="muted">Герой и душа появятся снова, когда локальная книга отдаст игровую сводку.</p>
          </>
        ) : gameScreen ? (
          <>
            <p><strong>{gameScreen.player.name || 'Герой'}</strong> · {gameScreen.player.currentCondition}</p>
            <p className="muted">Душа: {gameScreen.soul.name || 'без имени'} · {formatRealmName(gameScreen.soul.realm)}</p>
            <div className="status-summary-grid" aria-label="Состояние героя">
              <span>Здоровье {formatSidebarStatusMetric(gameScreen.player.healthPercentage)}</span>
              <span>Энергия {formatSidebarStatusMetric(gameScreen.player.energyPercentage)}</span>
              <span>Стойкость {formatSidebarStatusMetric(gameScreen.player.poisePercentage)}</span>
            </div>
          </>
        ) : (
          <>
            <p>Душа и герой появятся после открытия или загрузки главы.</p>
            <p className="muted">Это обычное состояние пустой книги, не ошибка клиента.</p>
          </>
        )}
      </StatusSummaryCard>

      <StatusSummaryCard title="Сохранение" eyebrow="локальная партия" soft={!session?.gameSessionExists && !saveNeedsAttention} attention={saveNeedsAttention}>
        {sidebarSessionFailure ? (
          <p className="warning-text">{sidebarSessionFailure}</p>
        ) : (
          <p>{formatSidebarSessionSummary(session, menu)}</p>
        )}
        {sidebarMenuFailure ? (
          <p className="warning-text">{sidebarMenuFailure}</p>
        ) : (
          <p className="muted">{formatSidebarSaveSummary(menu)}</p>
        )}
      </StatusSummaryCard>

      <StatusSummaryCard title="Ожидание ГМа" eyebrow="ход" attention={turnNeedsAttention}>
        {sidebarGameFailure ? (
          <>
            <p className="warning-text">{sidebarGameFailure}</p>
            <p className="muted">Глава сохранена; подробности ремонта и проверки остаются в расширенном режиме.</p>
          </>
        ) : gameScreen ? (
          <>
            <p className={`status-pill turn-phase turn-phase--${gameScreen.turnState.severity}`}>{formatTurnStateTitle(gameScreen.turnState)}</p>
            <p>{formatTurnStateMessage(gameScreen.turnState)}</p>
            <p className="muted">Подробности ремонта, проверки и команд скрыты до явного включения.</p>
          </>
        ) : (
          <>
            <p>{sidebarEmptyGame}</p>
            <p className="muted">Когда появится ожидающий ход или ответ ГМа, книга покажет это здесь игровым языком.</p>
          </>
        )}
      </StatusSummaryCard>

      {readyState && <AudioSettingsPanel result={readyState.audio} activeRoute={activeRoute} advancedEnabled={advancedEnabled} />}

      <section className="advanced-sidebar-entry" aria-label="Служебная панель">
        <div>
          <p className="panel-eyebrow">по запросу</p>
          <h3>Служебная панель</h3>
          <p className="muted">Служебные проверки и сведения для ремонта остаются вторичным режимом.</p>
        </div>
        <button
          type="button"
          className="advanced-toggle"
          aria-controls="advanced-diagnostics"
          aria-expanded={advancedEnabled}
          onClick={() => setAdvancedEnabled((value) => !value)}
        >
          {advancedEnabled ? 'Скрыть расширенный режим' : 'Открыть расширенный режим'}
        </button>
      </section>
    </div>
  );
}

function StatusSummaryCard({
  title,
  eyebrow,
  children,
  soft = false,
  attention = false
}: {
  title: string;
  eyebrow: string;
  children: ReactNode;
  soft?: boolean;
  attention?: boolean;
}) {
  const className = `status-summary-card${soft ? ' is-soft' : ''}${attention ? ' is-attention' : ''}`;
  return (
    <section className={className}>
      <p className="panel-eyebrow">{eyebrow}</p>
      <h3>{title}</h3>
      {children}
    </section>
  );
}

function getSidebarFailure<TData>(result: BrowserApiResult<TData> | null | undefined): string | null {
  if (!result || isSuccess(result) || result.kind === 'no-active-session') {
    return null;
  }

  return toPlayerFacingText(result.playerMessage, 'Книга требует внимания.');
}

function getSidebarEmptyGameMessage(readyState: Extract<BrowserShellState, { status: 'ready' }> | null): string {
  const gameFailure = getSidebarFailure(readyState?.game);
  if (gameFailure) {
    return gameFailure;
  }

  return 'Книга ждёт открытия главы.';
}

function formatSidebarSessionSummary(session: LocalWebUiSessionStatus | null, menu: BrowserMainMenuDto | null): string {
  if (session?.gameSessionExists) {
    return session.canStartBrowserWrite
      ? 'Локальная партия найдена, запись следующего хода доступна.'
      : 'Локальная партия найдена, но ход сейчас ждёт безопасного момента.';
  }

  if (menu?.session.gameSessionExists || menu?.session.canContinue) {
    return 'Есть глава, которую можно продолжить с главной страницы.';
  }

  return 'Активной главы пока нет — начните новую или загрузите сохранение.';
}

function formatSidebarSaveSummary(menu: BrowserMainMenuDto | null): string {
  if (!menu) {
    return 'Список сохранений появится после ответа локальной книги.';
  }

  if (menu.saves.length > 0) {
    return `Доступно сохранений: ${menu.saves.length}. Последние записи доступны на главной странице.`;
  }

  return 'Сохранений пока не найдено; можно начать новую главу.';
}

function formatSidebarStatusMetric(value: string): string {
  const normalized = value.trim();
  if (!normalized) {
    return '—';
  }

  return normalized.endsWith('%') ? normalized : `${normalized}%`;
}

function formatSidebarAudioSummary(audio: BrowserAudioSettingsDto): string {
  const availablePlaylists = audio.playlists.filter((playlist) => playlist.available).length;
  const availableCues = audio.cues.filter((cue) => cue.available).length;
  return `Музыка ${audio.musicEnabled ? 'включена' : 'выключена'}; плейлистов найдено: ${availablePlaylists}; подсказок: ${availableCues}.`;
}
```

- [ ] **Step 3: Use the audio summary in `AudioSettingsPanel`**

Inside successful `AudioSettingsPanel`, add a short status line near the top:

```tsx
        <p className="muted">{formatSidebarAudioSummary(audio)}</p>
```

- [ ] **Step 4: Run focused test**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard"
```

Expected: still FAIL until CSS classes from Task 3 are added.

---

### Task 3: Add sidebar status CSS

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

- [ ] **Step 1: Add CSS**

Add near the sidebar/component styles:

```css
.player-status-sidebar {
  display: grid;
  gap: var(--space-4);
}

.sidebar-heading,
.status-summary-card,
.advanced-sidebar-entry {
  border: 1px solid color-mix(in srgb, var(--realm-accent) 24%, rgba(255, 255, 255, 0.08));
  border-radius: var(--radius-md);
  padding: var(--space-4);
  background: rgba(255, 255, 255, 0.045);
}

.sidebar-heading h2,
.status-summary-card h3,
.advanced-sidebar-entry h3 {
  margin: 0 0 var(--space-2);
}

.status-summary-card {
  display: grid;
  gap: var(--space-2);
}

.status-summary-card.is-soft {
  border-color: color-mix(in srgb, var(--color-mist) 18%, rgba(255, 255, 255, 0.08));
  background: rgba(255, 255, 255, 0.032);
}

.status-summary-card.is-attention {
  border-color: color-mix(in srgb, var(--state-repair) 40%, rgba(255, 255, 255, 0.1));
  background: color-mix(in srgb, var(--state-repair) 10%, rgba(255, 255, 255, 0.035));
}

.status-summary-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.45rem;
}

.status-summary-grid span {
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 999px;
  padding: 0.35rem 0.55rem;
  background: rgba(255, 255, 255, 0.055);
  color: #fff3d6;
  font-size: 0.78rem;
}

.advanced-sidebar-entry {
  display: grid;
  gap: var(--space-3);
  border-color: rgba(255, 255, 255, 0.1);
  background: rgba(0, 0, 0, 0.18);
}

.advanced-sidebar-entry .advanced-toggle {
  width: 100%;
  box-shadow: none;
}
```

- [ ] **Step 2: Run focused test to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard"
```

Expected: PASS.

---

### Task 4: Verify, review, and prepare PR

**Files:**
- All files modified above.

- [ ] **Step 1: Run frontend type/build verification**

Run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: typecheck and Vite build pass.

- [ ] **Step 2: Run focused and relevant .NET tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserFrontendWorkspaceTests"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|WebUi|LocalWebUi|Browser" --logger "console;verbosity=minimal"
```

Expected: all selected tests pass.

- [ ] **Step 3: Static/diff checks**

Run:

```bash
git diff --check
git diff -- BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs
```

Expected: no whitespace errors; diff only covers #722 presentation/test/doc/plan files.

- [ ] **Step 4: Independent review**

Dispatch an independent reviewer with the issue, design, plan, and current diff. Fix any Critical/Important issues and rerun relevant tests.

- [ ] **Step 5: Visual smoke**

Run Vite preview and inspect `http://127.0.0.1:4173/` desktop view. Confirm the right column reads as player status, the advanced entry is secondary, and no repeated unavailable alerts appear in the default no-session state.

- [ ] **Step 6: Commit**

Stage only the intended files:

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs \
  BookOfEternityClient.WebFrontend/src/App.tsx \
  BookOfEternityClient.WebFrontend/src/styles/components.css \
  docs/superpowers/specs/2026-05-26-browser-sidebar-status-design.md \
  docs/superpowers/plans/2026-05-26-browser-sidebar-status.md

git commit -m "feat(web): make browser sidebar player-facing"
```

- [ ] **Step 7: PR and merge**

Push branch, create PR with `Closes #722`, wait for green CI, squash-merge to `main`, delete the branch, and update local `main`.
