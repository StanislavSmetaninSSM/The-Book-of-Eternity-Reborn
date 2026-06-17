# Animations, Background Art, Tech Terms Cleanup + Browser Testing

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add micro-interactions and CSS transitions for wow-factor, generate and integrate dark-fantasy main-menu art via Pollinations, clean remaining technical terms from player UI, then validate everything visually with browser-act.

**Architecture:** Pure frontend CSS/React changes for Tasks 1-3. Task 4 generates an image via Pollinations API and integrates it as main-menu background. Task 5 creates a game_session fixture for the C# backend. Task 6 launches the app and runs browser-act visual testing.

**Tech Stack:** CSS transitions/animations, Pollinations image API (flux model), React/TypeScript, C# (dotnet run --web), browser-act skill

---

## File Structure

| File | Responsibility |
|------|---------------|
| `src/styles/motion.css` | Enhanced with route transitions, hover effects, panel enter/exit (Task 1) |
| `src/styles/components.css` | Add transition properties to interactive elements (Task 1) |
| `src/components/ErrorNotice.tsx` | Ensure technical terms are gated (Task 2) |
| `src/routes/HomeRoute.tsx` | Integrate background art (Task 3) |
| `src/styles/components.css` | Hero background styles (Task 3) |
| `BookOfEternityClient.WebFrontend/public/art/` | Generated background images (Task 3) |
| `BookOfEternityClient/game_session/` | Test game save data (Task 4) |
| (browser-act) | Visual validation (Task 5) |

---

### Task 1: CSS transitions and micro-interactions (#763)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/motion.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

**Context:** Currently only 14 transition properties exist in the entire CSS. The design has animations (panel-rise, ember-drift, rune-glow) but interactive elements lack hover/focus/active transitions. The issue asks for "modern React wow-factor."

- [ ] **Step 1: Add interactive transition properties to components.css**

Add `transition` to these selectors in `components.css`:

```css
/* Buttons — add to existing button rules */
.nav-bar__item { transition: color var(--motion-fast), background var(--motion-fast), transform var(--motion-fast); }
.nav-bar__item:hover:not(.is-active) { transform: translateY(-1px); }
.nav-bar__item.is-active { transform: translateY(0); }

.launcher-menu__item { transition: border-color var(--motion-fast), background var(--motion-fast), transform var(--motion-fast); }
.launcher-menu__item:hover:not(:disabled) { transform: translateX(2px); }

/* Cards — smooth border/shadow on hover */
.shell-panel { transition: border-color var(--motion-panel), box-shadow var(--motion-panel); }
.shell-panel:hover { border-color: color-mix(in srgb, var(--realm-accent) 32%, rgba(255, 255, 255, 0.08)); }

.summary-card { transition: border-color var(--motion-fast), transform var(--motion-fast), box-shadow var(--motion-fast); }
.summary-card:hover { transform: translateY(-2px); box-shadow: var(--shadow-panel); }

/* Sidebar toggle */
.workspace-sidebar { transition: transform var(--motion-panel), opacity var(--motion-panel); }

/* Composer */
.composer-form { transition: border-color var(--motion-fast), box-shadow var(--motion-fast); }
.composer-form:focus-within { box-shadow: var(--shadow-glow); }
```

- [ ] **Step 2: Enhance motion.css with route transition and new keyframes**

Add to `motion.css`:

```css
/* Route content fade-slide */
@keyframes route-enter {
  from {
    opacity: 0;
    transform: translateY(0.8rem);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes route-exit {
  from {
    opacity: 1;
    transform: translateY(0);
  }
  to {
    opacity: 0;
    transform: translateY(-0.4rem);
  }
}

/* Sidebar slide-in from right */
@keyframes sidebar-enter {
  from {
    opacity: 0;
    transform: translateX(1rem);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

/* Button press micro-interaction */
@keyframes btn-press {
  0% { transform: scale(1); }
  50% { transform: scale(0.96); }
  100% { transform: scale(1); }
}

/* Apply route enter to workspace main content */
.workspace-main > * {
  animation: route-enter var(--motion-panel) both;
}

/* Sidebar entrance */
.workspace-sidebar {
  animation: sidebar-enter var(--motion-panel) both;
  animation-delay: 80ms;
}

/* Button press on active */
.nav-bar__item:active,
.launcher-menu__item:active:not(:disabled),
.composer-form button[type="submit"]:active {
  animation: btn-press 150ms ease-out;
}

/* Stagger panels within a route */
.workspace-main > *:nth-child(1) { animation-delay: 0ms; }
.workspace-main > *:nth-child(2) { animation-delay: 60ms; }
.workspace-main > *:nth-child(3) { animation-delay: 120ms; }
.workspace-main > *:nth-child(4) { animation-delay: 180ms; }
```

- [ ] **Step 3: Run verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat(browser): add CSS transitions and micro-interactions

Add hover/focus/active transitions to nav items, cards, panels,
launcher menu, and composer. New keyframes: route-enter, sidebar-enter,
btn-press. Staggered panel entry. All respect prefers-reduced-motion.

Closes #763

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Remove remaining technical terms from player UI (#738)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/components/AdvancedDiagnostics.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/routes/GameRoute.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/components/PlayerStatusSidebar.tsx`
- Possibly modify other route/component files

**Context:** #738 asks to "убрать технические термины и meta-комментарии из player UI". After PR #781, `playerCopy.ts` handles backend strings. But component code may still contain:
- Hardcoded English technical labels visible to players
- `className` text that bleeds into aria-labels
- System messages not wrapped in `toPlayerFacingText()`
- Debug/developer terms in non-advanced mode sections

- [ ] **Step 1: Audit all player-visible strings**

Search all `.tsx` files for strings that might be technical:
- Look for `aria-label`, `title`, button text, `<p>`, `<h2>`, `<h3>` content
- Check if any English technical terms appear without `toPlayerFacingText` wrapping
- Look for references to "API", "endpoint", "runtime", "lifecycle" in non-advanced sections
- Check for any `console.log` or debug output reaching the UI

Fix any found issues by:
- Wrapping technical strings in `toPlayerFacingText()`
- Moving technical details behind `advancedEnabled` checks
- Replacing raw English identifiers with Russian player-facing equivalents

- [ ] **Step 2: Run verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: All pass

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "fix(browser): remove remaining technical terms from player-facing UI

Audit and clean player-visible strings: wrap system messages in
toPlayerFacingText, gate technical details behind advancedEnabled,
replace English identifiers with Russian player-facing equivalents.

Closes #738

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Generate and integrate dark-fantasy background art (#759)

**Files:**
- Create: `BookOfEternityClient.WebFrontend/public/art/main-menu-bg.jpg`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css` (hero section styles)
- Modify: `BookOfEternityClient.WebFrontend/src/routes/HomeRoute.tsx` or GameLauncher

**Context:** The main menu (HomeRoute) currently has no background art — just the dark gradient. Pollinations API key: `plln_pk_ihr4JC3Cf1VyT2JGjDpjrwbDFYZ1H22e`. Use the flux model for high-quality generation.

- [ ] **Step 1: Generate background image**

Generate via URL (or curl):
```
https://gen.pollinations.ai/image/dark%20fantasy%20ancient%20book%20of%20eternity%20on%20stone%20altar%20glowing%20golden%20runes%20ethereal%20mist%20cinematic%20lighting%20ash%20and%20embers%20floating%20dramatic%20atmosphere%204k%20concept%20art?model=flux&width=1920&height=1080&key=plln_pk_ihr4JC3Cf1VyT2JGjDpjrwbDFYZ1H22e
```

Save to: `BookOfEternityClient.WebFrontend/public/art/main-menu-bg.jpg`

- [ ] **Step 2: Add hero background styles**

Add to `components.css`:

```css
/* ── Main menu hero background ── */
.game-launcher {
  position: relative;
  overflow: hidden;
}

.game-launcher::before {
  content: '';
  position: absolute;
  inset: 0;
  background: url('/art/main-menu-bg.jpg') center/cover no-repeat;
  opacity: 0.18;
  z-index: -1;
  mask-image: linear-gradient(to bottom, black 40%, transparent 100%);
  pointer-events: none;
}
```

Note: `.game-launcher` already has styles — add `position: relative; overflow: hidden;` if not present, and the `::before` pseudo-element for the image overlay. The low opacity (0.18) ensures it doesn't overwhelm the UI but adds atmosphere.

- [ ] **Step 3: Run verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat(browser): integrate dark-fantasy main-menu background art

Generate original background via Pollinations AI (flux model).
Apply as subtle overlay behind GameLauncher with mask-image fade.
Adds atmosphere without overwhelming the dark-fantasy UI.

Closes #759

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: Create game_session test fixture for browser testing

**Files:**
- Create: `BookOfEternityClient/game_session/` directory tree
- Copy structure from `FileSystemExample/game_session/`

**Context:** The C# client reads from `BookOfEternityClient/game_session/`. To test the browser UI, we need a valid game state that passes validation. Use the FileSystemExample as a template.

- [ ] **Step 1: Copy FileSystemExample structure to BookOfEternityClient/game_session/**

```powershell
Copy-Item -Recurse -Force "E:\Games\The Book of Eternity Reborn\FileSystemExample\game_session\*" "E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session\"
```

- [ ] **Step 2: Verify the C# client starts in web mode**

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient"
dotnet run -- --web
```

Check that it starts without fatal errors. If validation errors occur, fix the JSON files based on error messages.

- [ ] **Step 3: Fix any validation errors**

Read error output and fix the game_session files. Common issues:
- Missing required fields
- Schema version mismatches
- File path references that don't exist

Iterate until the client starts cleanly and serves the web UI.

- [ ] **Step 4: Do NOT commit game_session (it should be gitignored)**

Verify that `BookOfEternityClient/game_session/` is in `.gitignore`. If not, add it. This is test data only.

---

### Task 5: Guard tests + push + create PR

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` (if needed)
- Modify: `tsconfig.player-facing-tests.json` (if new tests added)

- [ ] **Step 1: Run full frontend verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`

- [ ] **Step 2: Run C# guard tests**

Run: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"`

- [ ] **Step 3: Fix any failures and commit fixes**

- [ ] **Step 4: Push and create PR**

```bash
git push origin feat/browser-animations-art-terms
gh pr create --title "feat(browser): animations, background art, tech terms cleanup" \
  --body "## Summary

Fifth wave of browser client UX: wow-factor animations, generated background art, and technical term cleanup.

### Changes

1. **Micro-interactions** — CSS transitions for nav, cards, panels, composer. Route-enter animation with stagger. Button press feedback. (#763)
2. **Background art** — Generated dark-fantasy art via Pollinations AI, integrated as subtle overlay on main menu. (#759)
3. **Tech terms cleanup** — Audited and cleaned remaining technical terminology from player-facing UI surfaces. (#738)

### Testing
- Frontend: typecheck + vitest + vite build pass
- C# guard tests pass
- Visual browser testing performed via browser-act

Closes #763
Closes #759
Closes #738" --base main
```

---

### Task 6: Browser-act visual testing

**Prerequisites:** Tasks 1-5 complete, game_session fixture exists, PR created.

- [ ] **Step 1: Start the C# backend in web mode**

```powershell
cd "E:\Games\The Book of Eternity Reborn\BookOfEternityClient"
dotnet run -- --web
```

Note the URL (likely `http://localhost:5000` or similar).

- [ ] **Step 2: Use browser-act to navigate the site**

Test scenarios:
1. Open main menu — verify background art visible, launcher renders
2. Check realm badge in NavBar
3. Hover over nav items — verify transitions work
4. Load a save (if available) — verify game route renders
5. Check that no technical terms are visible in normal mode
6. Toggle advanced mode — verify technical details appear
7. Navigate between routes — verify route-enter animations
8. Check responsive layout on mobile viewport

- [ ] **Step 3: Take screenshots and document findings**

Save observations for reporting to the user.
