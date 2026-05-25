# Browser Player-Facing Copy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issue #719 by replacing technical Browser Client hero/default empty-state copy with player-facing Reborn language while keeping advanced diagnostics explicitly gated.

**Architecture:** Keep all gameplay/application authority in the existing C# browser APIs. Change only the React presentation layer in `BookOfEternityClient.WebFrontend/src/App.tsx`, source/CSS guard tests in `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`, and neutral visual styling in `BookOfEternityClient.WebFrontend/src/styles/components.css`.

**Tech Stack:** React 19 + TypeScript + Vite frontend, .NET 8 xUnit source guards, GitHub issue #719.

---

## File Structure

- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
  - Add focused #719 guard test near existing browser design-system/source guard tests.
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
  - Replace default hero headline/lead.
  - Add `EmptyState` and `EmptyOrFailure` helper components.
  - Use neutral empty states only for `no-active-session` results.
  - Keep `ErrorNotice`/`ApiFailure` for true shell/API errors and advanced-gated technical details.
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
  - Add `.empty-state` styling and include it in neutral card styling.
- Create: `docs/superpowers/specs/2026-05-26-browser-player-facing-copy-design.md`
  - Already written before this plan as the unattended design/spec record.
- Create: `docs/superpowers/plans/2026-05-26-browser-player-facing-copy.md`
  - This plan.

---

### Task 1: Add #719 Source Guards and Implement Player-Facing Copy

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

- [ ] **Step 1: Write the failing guard test**

Add this xUnit test immediately after `BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens()` or before `ReactAppShell_DocumentsIssue704RoutingAndPlayerAdvancedBoundary()` in `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`:

```csharp
    [Fact]
    public void BrowserDefaultScreen_UsesPlayerFacingCopyAndNeutralEmptyStates()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        Assert.Contains("Книга Вечности: Перерождение", app, StringComparison.Ordinal);
        Assert.Contains("Откройте книгу", app, StringComparison.Ordinal);
        Assert.Contains("function EmptyState", app, StringComparison.Ordinal);
        Assert.Contains("function EmptyOrFailure", app, StringComparison.Ordinal);
        Assert.Contains("result.kind === 'no-active-session'", app, StringComparison.Ordinal);
        Assert.Contains("return <ApiFailure title={errorTitle}", app, StringComparison.Ordinal);
        Assert.Contains("className=\"empty-state\"", app, StringComparison.Ordinal);
        Assert.Contains(".empty-state", styles, StringComparison.Ordinal);
        Assert.Contains("Технические подробности доступны после явного включения расширенного режима", app, StringComparison.Ordinal);

        Assert.DoesNotContain("<h1 id=\"browser-client-title\">Локальный игровой клиент</h1>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("источник истины", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("маршруты", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("состояние интерфейса", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("посмертные контракты", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("отдельный слой", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Главное меню недоступно", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Игровой экран недоступен", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Данные души недоступны", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Мир недоступен", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Медиа недоступны", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Настройки недоступны", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Сессия недоступна", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Аудио-настройки недоступны", app, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserDefaultScreen_UsesPlayerFacingCopyAndNeutralEmptyStates" --logger "console;verbosity=minimal"
```

Expected: FAIL because `function EmptyState`, `function EmptyOrFailure`, `.empty-state`, and player-facing replacements do not exist yet, and banned default copy is still present.

- [ ] **Step 3: Implement the minimal React copy and empty-state changes**

In `BookOfEternityClient.WebFrontend/src/App.tsx`, change the hero block to this copy:

```tsx
<section className="shell-hero" aria-labelledby="browser-client-title">
  <p className="eyebrow">Книга Вечности: Перерождение · локальная книга</p>
  <div className="hero-layout">
    <div>
      <h1 id="browser-client-title">Книга Вечности: Перерождение</h1>
      <p className="lead">
        Откройте книгу, продолжите сохранённую главу или подготовьте новую сцену. Браузер показывает локальную партию мягким игровым языком,
        а служебные сведения остаются в расширенном режиме.
      </p>
    </div>
    <div className="hero-status" aria-label="Текущий слой мира">
      <span className="theme-icon" aria-hidden="true">{realmTheme.icon}</span>
      <strong>{realmTheme.label}</strong>
      <span>{gameScreen ? formatTurnStateTitle(gameScreen.turnState) : menu?.session.validationLabel ?? 'Книга ждёт открытия'}</span>
    </div>
  </div>
</section>
```

Add these components before `ApiFailure<T>`:

```tsx
function EmptyState({ title, message, action }: EmptyStateCopy) {
  return (
    <section className="empty-state" aria-label={title}>
      <p className="panel-eyebrow">ожидание главы</p>
      <h2>{title}</h2>
      <p>{message}</p>
      <p className="muted">{action}</p>
    </section>
  );
}

function EmptyOrFailure<T>({
  result,
  empty,
  errorTitle,
  advancedEnabled
}: {
  result: BrowserApiResult<T>;
  empty: EmptyStateCopy;
  errorTitle: string;
  advancedEnabled: boolean;
}) {
  if (isSuccess(result)) {
    return null;
  }

  if (result.kind === 'no-active-session') {
    return <EmptyState {...empty} />;
  }

  return <ApiFailure title={errorTitle} result={result} advancedEnabled={advancedEnabled} />;
}
```

Route, sidebar, and audio failure paths should call `EmptyOrFailure` with route-specific neutral copy and an `errorTitle`. The neutral copy appears only when the API failure kind is `no-active-session`; all other failures continue through `ApiFailure`/`ErrorNotice` so real local shell errors remain visible and technical details stay advanced-gated. Example for the game route:

```tsx
return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Игровой экран требует внимания" empty={{
  title: 'Глава ещё не открыта',
  message: 'Нарратив и ход ГМа появятся после выбора или загрузки игровой сессии.',
  action: 'Вернитесь на главную страницу и откройте книгу, чтобы продолжить историю.'
}} />;
```

Use the same pattern for `HomeRoute`, `SoulRoute`, `WorldRoute`, `MediaRoute`, `SettingsRoute`, the sidebar session/turn panels, and `AudioSettingsPanel`.

- [ ] **Step 4: Add neutral CSS**

In `BookOfEternityClient.WebFrontend/src/styles/components.css`, include `.empty-state` in the neutral shell card selectors and add a non-red style:

```css
.shell-hero,
.shell-panel,
.route-card,
.advanced-diagnostics,
.summary-card,
.empty-state,
.error-notice,
.narrative-card {
```

```css
.shell-panel,
.advanced-diagnostics,
.empty-state,
.error-notice {
  padding: clamp(1rem, 3vw, 1.5rem);
}
```

Add after the `.error-notice` block:

```css
.empty-state {
  display: grid;
  gap: var(--space-2);
  border-color: color-mix(in srgb, var(--realm-accent) 30%, rgba(255, 255, 255, 0.08));
  background: linear-gradient(135deg, rgba(255, 255, 255, 0.06), rgba(216, 179, 106, 0.06));
}
```

- [ ] **Step 5: Run focused test and verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserDefaultScreen_UsesPlayerFacingCopyAndNeutralEmptyStates" --logger "console;verbosity=minimal"
```

Expected: PASS, 1 test passed, 0 failed.

- [ ] **Step 6: Run frontend and focused .NET verification**

Run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUi" --logger "console;verbosity=minimal"
git diff --check
```

Expected: each command exits 0; focused browser .NET slice reports 0 failures.

- [ ] **Step 7: Commit**

Stage only the intentional files and commit:

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs \
  BookOfEternityClient.WebFrontend/src/App.tsx \
  BookOfEternityClient.WebFrontend/src/styles/components.css \
  docs/superpowers/specs/2026-05-26-browser-player-facing-copy-design.md \
  docs/superpowers/plans/2026-05-26-browser-player-facing-copy.md
git commit -m "feat(web-ui): make browser default copy player-facing"
```

---

## Plan Self-Review

- Spec coverage: covers #719 hero title/copy, banned technical phrases, neutral empty states, advanced-only technical details, and source guard verification.
- Placeholder scan: no TODO/TBD/fill-in placeholders remain.
- Type consistency: `EmptyState` is a local React helper with string props; all route replacements use the same signature.
- Scope check: does not implement launcher hierarchy (#720), icon replacement (#721), sidebar redesign (#722), or artifact workflow (#723).
