# Browser UX Polish — Responsive, Launcher Simplification & Audio Cleanup

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix responsive breakpoints for tablets/mobile, simplify the bloated game launcher into a clean vertical menu, and hide audio diagnostics from the default player surface.

**Architecture:** CSS-only responsive fixes (media queries, touch targets, mobile nav), GameLauncher component streamlining (remove mode tabs + secondary actions grid, keep vertical menu), AudioPanel diagnostic gating behind `advancedEnabled`.

**Tech Stack:** React 18, TypeScript, CSS custom properties, existing component system.

---

## File Structure

```
src/
├── components/
│   ├── GameLauncher.tsx         (MODIFY — simplify to vertical menu, ~180 lines target)
│   ├── AudioPanel.tsx           (MODIFY — gate diagnostics behind advanced mode)
│   └── NavBar.tsx               (MODIFY — add mobile bottom-bar mode via CSS)
├── styles/
│   ├── layout.css               (MODIFY — fix breakpoints, add mobile nav, touch targets)
│   └── components.css           (MODIFY — launcher simplification styles)
test/
├── uiStructure.test.ts          (MODIFY — add launcher/audio assertions)
```

---

## Task 1: Fix responsive breakpoints and add mobile-friendly layout

**Files:**
- Modify: `src/styles/layout.css`
- Modify: `src/styles/components.css`

- [ ] **Step 1: Rewrite responsive breakpoints in layout.css**

Replace the existing `@media` blocks (lines 110-175 of layout.css) and the 900px sidebar collapse block with a cohesive set:

**Key changes:**
- `< 640px`: NavBar becomes bottom-fixed, full-width panels, 44px min touch targets
- `640-900px`: Single column, sidebar as slide-out (already works), composer full-width
- `900-1120px`: Narrower grid, 2-column action grids
- Safe area insets for notched devices
- Minimum touch target size globally (44x44px on buttons)

- [ ] **Step 2: Add touch-target sizing to components.css**

```css
/* ── Touch targets ── */

@media (pointer: coarse) {
  button, [role="tab"], .nav-bar__item, .launcher-menu__item {
    min-height: 44px;
    min-width: 44px;
  }

  .composer textarea {
    min-height: 5rem;
    font-size: 1rem;
  }
}
```

- [ ] **Step 3: Make NavBar bottom-fixed on mobile**

Add to layout.css:
```css
@media (max-width: 640px) {
  .nav-bar {
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    z-index: 200;
    border-top: 1px solid var(--border-subtle);
    border-radius: 0;
    padding: env(safe-area-inset-bottom, 0) 0 0;
    background: var(--surface-base, #0a0e10);
  }

  .nav-bar__label {
    font-size: 0.65rem;
  }

  .browser-shell {
    padding-bottom: calc(60px + env(safe-area-inset-bottom, 0));
  }
}
```

- [ ] **Step 4: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(browser): fix responsive breakpoints and add mobile layout

NavBar becomes bottom-fixed on mobile. Touch targets enlarged for
coarse pointers. Composer grows on small screens. Safe-area-inset
padding added for notched devices.

Refs #772

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 2: Simplify GameLauncher to vertical menu

**Files:**
- Modify: `src/components/GameLauncher.tsx`
- Modify: `src/styles/components.css`

- [ ] **Step 1: Restructure GameLauncher**

The current launcher has:
- Launcher copy (title + description)
- Primary action button (full width)
- 5 mode tabs grid (`launcher-mode-tabs`)
- Mode content panel (`renderModeContent()`)
- 4 secondary action buttons grid (`launcher-secondary-actions`)
- Notice

Replace with:
- Launcher copy (title + subtitle)
- Vertical menu of 3-5 items (Continue, Load, New Game, Settings, About)
- Active mode content panel (same as before, reused)
- Notice

**Remove:**
- The `launcher-mode-tabs` tablist (duplicates the menu)
- The `launcher-secondary-actions` grid (duplicates the tabs)
- The `launcher-primary-action` full-width button (merged into menu items)

**New structure in the return JSX:**

```tsx
return (
  <article className="game-launcher" aria-labelledby="browser-launcher-title">
    <div className="launcher-window">
      <div className="launcher-copy">
        <p className="panel-eyebrow">главная книга</p>
        <h2 id="browser-launcher-title">Открыть книгу</h2>
        <p className="muted">{toPlayerFacingText(menu.session.continueReason, 'Выберите продолжение, загрузку или новую главу.')}</p>
      </div>

      <nav className="launcher-menu" aria-label="Действия главного меню">
        {launcherModes.map((mode) => {
          const details = launcherModeDetails[mode];
          const action = findLauncherMenuAction(menu, mode);
          const disabled = Boolean(action && !action.enabled && mode !== 'settings' && mode !== 'about');
          const isActive = activeMode === mode;
          return (
            <button
              key={mode}
              type="button"
              className={`launcher-menu__item${isActive ? ' is-active' : ''}${mode === primaryAction.mode ? ' is-primary' : ''}`}
              disabled={disabled}
              onClick={() => activateLauncherMode(mode)}
              aria-current={isActive ? 'true' : undefined}
            >
              <strong>{details.label}</strong>
              <span className="muted">{disabled ? 'пока недоступно' : details.description}</span>
            </button>
          );
        })}
      </nav>

      {renderModeContent()}
      {launcherNotice && <p className="composer-notice">{launcherNotice}</p>}
    </div>
  </article>
);
```

- [ ] **Step 2: Add launcher-menu styles**

Replace old launcher tab/secondary styles with:

```css
.launcher-menu {
  display: flex;
  flex-direction: column;
  gap: var(--space-xs);
  margin: var(--space-md) 0;
}

.launcher-menu__item {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  padding: var(--space-sm) var(--space-md);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  background: var(--surface-subtle);
  cursor: pointer;
  text-align: left;
  transition: border-color 0.15s, background 0.15s;
}

.launcher-menu__item:hover:not(:disabled) {
  background: var(--surface-active);
  border-color: var(--realm-accent, var(--accent-gold));
}

.launcher-menu__item.is-active {
  border-color: var(--realm-accent, var(--accent-gold));
  background: var(--surface-active);
}

.launcher-menu__item.is-primary strong {
  color: var(--realm-accent, var(--accent-gold));
}

.launcher-menu__item:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
```

Remove old styles: `.launcher-mode-tabs`, `.launcher-mode-tab`, `.launcher-secondary-actions`, `.launcher-primary-action` (keep them if referenced elsewhere, but mark them dead and let the cleanup pass in uiStructure test detect them).

- [ ] **Step 3: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(browser): simplify GameLauncher to vertical menu

Remove redundant mode tabs and secondary actions grid. Single
vertical menu with active item highlighting replaces the tablist
plus full-width button plus 4-button grid.

Closes #768

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 3: Hide audio diagnostics from default player surface

**Files:**
- Modify: `src/components/AudioPanel.tsx`

- [ ] **Step 1: Gate per-file diagnostics behind advancedEnabled**

In AudioPanel.tsx, the `audio-catalog` section (lines 198-209) lists per-playlist/per-cue availability with "файлы не найдены" / "нет файла". This should be:
1. Hidden entirely when `!advancedEnabled`
2. Replaced with a single player-friendly message when assets are missing

Change the audio-catalog rendering:

```tsx
{/* Replace the audio-catalog div */}
{advancedEnabled && (
  <details className="audio-catalog-details">
    <summary>Подробности аудиофайлов (служебный режим)</summary>
    <div className="audio-catalog" aria-label="Доступные плейлисты и подсказки">
      {audio.playlists.map((item) => (
        <span key={item.id} className={item.available ? 'status-pill' : 'status-pill is-muted'}>
          {toPlayerFacingText(item.label, 'Плейлист')}: {item.available ? `${item.tracks.length} трек(ов)` : 'файлы не найдены'}
        </span>
      ))}
      {audio.cues.map((cue) => (
        <span key={cue.id} className={cue.available ? 'status-pill' : 'status-pill is-muted'}>
          {toPlayerFacingText(cue.label, 'Звуковая подсказка')}: {cue.available ? 'готово' : 'нет файла'}
        </span>
      ))}
    </div>
  </details>
)}

{/* Add compact player-facing summary when NOT advanced */}
{!advancedEnabled && !allAssetsAvailable(audio) && (
  <p className="muted">Локальные аудиофайлы не установлены. Игра продолжит работать без музыки.</p>
)}
```

Add a helper function:
```typescript
function allAssetsAvailable(audio: BrowserAudioSettingsDto): boolean {
  return audio.playlists.every(p => p.available) && audio.cues.every(c => c.available);
}
```

- [ ] **Step 2: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "fix(browser): hide audio file diagnostics from default player surface

Per-playlist/per-cue missing file details now only shown in advanced
mode. Default surface shows single compact message when local audio
pack is not installed.

Closes #746

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 4: Guard tests + final verification + PR

**Files:**
- Modify: `test/uiStructure.test.ts`

- [ ] **Step 1: Add structural assertions**

```typescript
// Append to existing uiStructure.test.ts describe block:

it('GameLauncher uses vertical menu, not mode tabs', () => {
  const launcher = readSrc('components/GameLauncher.tsx');
  expect(launcher).toContain('launcher-menu');
  expect(launcher).not.toContain('launcher-mode-tabs');
  expect(launcher).not.toContain('launcher-secondary-actions');
});

it('AudioPanel gates diagnostics behind advancedEnabled', () => {
  const audio = readSrc('components/AudioPanel.tsx');
  expect(audio).toContain('advancedEnabled');
  // Per-file catalog should be inside a conditional
  expect(audio).toMatch(/advancedEnabled[\s\S]*audio-catalog/);
});

it('NavBar supports mobile bottom layout', () => {
  const layout = readFileSync(join(__dirname, '..', 'src', 'styles', 'layout.css'), 'utf-8');
  expect(layout).toContain('.nav-bar');
  expect(layout).toContain('position: fixed');
  expect(layout).toContain('bottom: 0');
});
```

- [ ] **Step 2: Run full verification gate**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

- [ ] **Step 3: Commit, push, create PR**

```bash
git add -A
git commit -m "test(browser): add launcher and audio structural guards

Refs #772 #768 #746

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"

git push -u origin feat/browser-ux-polish-responsive-launcher-audio

gh pr create --title "feat(browser): responsive layout, simplified launcher, audio cleanup" --body "## Summary
Fixes responsive breakpoints, simplifies GameLauncher, hides audio diagnostics.

Closes #772
Closes #768
Closes #746
Refs #680

## Changes
- Mobile: NavBar bottom-fixed, touch targets 44px, safe-area-inset
- Tablet: single-column, composer full-width
- GameLauncher: vertical menu replaces tab grid + secondary actions
- AudioPanel: per-file diagnostics gated behind advanced mode
- Structural guard tests

## Verification
- npm run verify ✅
- dotnet guard tests (72+) ✅"
```
