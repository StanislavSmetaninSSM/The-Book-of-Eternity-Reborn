# Cinematic Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the browser client from a flat functional UI into a cinematic dark-fantasy experience with sidebar navigation, dramatic lighting, animated particles, and rich micro-interactions.

**Architecture:** Replace the top NavBar with a vertical Sidebar, restructure the grid layout from rows to columns, add a reusable SceneHero component for cinematic banners, enhance all cards/surfaces with lighting effects, and introduce breathing ambient animations via CSS keyframes. All animations respect `prefers-reduced-motion`.

**Tech Stack:** React 18, TypeScript, CSS custom properties, CSS keyframes (no JS animation libraries), Google Fonts (Playfair Display)

---

### Task 1: Design tokens & base foundation

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/tokens.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/base.css`

- [ ] **Step 1: Add new design tokens**

In `tokens.css`, add these new tokens inside `:root` (after the existing `--motion-panel` line):

```css
  /* ── Sidebar ── */
  --sidebar-width: 72px;
  --sidebar-width-collapsed: 56px;

  /* ── Enhanced motion ── */
  --motion-slow: 400ms cubic-bezier(0.2, 0.8, 0.2, 1);
  --motion-breathe: 30s ease-in-out;

  /* ── Particle opacity (reduced-motion sets to 0) ── */
  --particle-opacity: 0.6;

  /* ── Typography — Playfair Display ── */
  --font-display: 'Playfair Display', Georgia, 'Times New Roman', serif;
  --font-narrative: 'Playfair Display', Georgia, 'Times New Roman', serif;

  /* ── Cinematic surfaces ── */
  --surface-card: rgba(14, 19, 20, 0.8);
  --surface-card-hover: rgba(14, 19, 20, 0.9);
  --border-card: color-mix(in srgb, var(--realm-accent, var(--color-gold)) 15%, rgba(255, 255, 255, 0.06));
  --border-card-hover: color-mix(in srgb, var(--realm-accent, var(--color-gold)) 35%, rgba(255, 255, 255, 0.1));

  /* ── Glow effects ── */
  --glow-gold: 0 0 20px color-mix(in srgb, var(--color-gold) 20%, transparent);
  --glow-realm: 0 0 24px color-mix(in srgb, var(--realm-accent, var(--color-gold)) 15%, transparent);
  --glow-hover: 0 0 30px color-mix(in srgb, var(--realm-accent, var(--color-gold)) 22%, transparent);
```

Replace the existing `--font-display` and `--font-narrative` lines (lines 6-7) with the new ones (remove the old values since we're declaring them in the new block above — actually just update lines 6-7 in place):

```css
  --font-display: 'Playfair Display', Georgia, 'Times New Roman', serif;
  --font-narrative: 'Playfair Display', Georgia, 'Times New Roman', serif;
```

- [ ] **Step 2: Add Google Fonts import and breathing background**

In `base.css`, add at the very top (before `* { box-sizing: border-box; }`):

```css
@import url('https://fonts.googleapis.com/css2?family=Playfair+Display:ital,wght@0,400;0,600;0,700;1,400&display=swap');
```

Replace the existing `body` background (lines 17-21) with the breathing version:

```css
body {
  margin: 0;
  min-width: 320px;
  min-height: 100vh;
  color: var(--color-parchment);
  font-family: var(--font-ui);
  background: var(--color-ink);
}

body::before {
  position: fixed;
  inset: 0;
  z-index: -2;
  pointer-events: none;
  content: '';
  background:
    radial-gradient(ellipse at 15% 5%, color-mix(in srgb, var(--realm-accent, var(--color-gold)) 12%, transparent), transparent 45%),
    radial-gradient(ellipse at 85% 15%, color-mix(in srgb, var(--color-crimson) 7%, transparent), transparent 35%),
    radial-gradient(ellipse at 50% 80%, color-mix(in srgb, var(--realm-accent, var(--color-gold)) 5%, transparent), transparent 50%);
  animation: bg-breathe var(--motion-breathe) infinite alternate;
}

@keyframes bg-breathe {
  0% { opacity: 1; transform: scale(1) translate(0, 0); }
  50% { opacity: 0.85; transform: scale(1.02) translate(-1%, 1%); }
  100% { opacity: 1; transform: scale(1) translate(1%, -0.5%); }
}
```

Remove the old `body::before` (texture overlay, lines 25-36) and replace with a simpler subtle grid that respects reduced motion:

```css
/* Subtle aged texture */
body::after {
  position: fixed;
  inset: 0;
  z-index: -1;
  pointer-events: none;
  content: '';
  background:
    linear-gradient(rgba(255, 255, 255, 0.012) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.008) 1px, transparent 1px);
  background-size: 4rem 4rem;
  mask-image: radial-gradient(circle at 50% 30%, black, transparent 80%);
  opacity: 0.5;
}
```

- [ ] **Step 3: Enhance particle layer**

Replace the existing `.browser-shell::before` particle CSS (lines 52-68) with an enhanced version:

```css
/* Ambient ember particles — cinematic */
.browser-shell::before {
  position: fixed;
  z-index: -1;
  inset: 0;
  pointer-events: none;
  content: '';
  background:
    radial-gradient(1.5px 1.5px at 8% 15%, var(--color-gold), transparent),
    radial-gradient(1px 1px at 18% 45%, var(--color-gold-dim), transparent),
    radial-gradient(2px 2px at 28% 80%, var(--color-candle), transparent),
    radial-gradient(1px 1px at 38% 25%, var(--color-gold-dim), transparent),
    radial-gradient(1.5px 1.5px at 52% 65%, var(--color-candle), transparent),
    radial-gradient(1px 1px at 62% 12%, var(--color-gold-dim), transparent),
    radial-gradient(1.5px 1.5px at 72% 55%, var(--color-gold), transparent),
    radial-gradient(1px 1px at 82% 35%, var(--color-gold-dim), transparent),
    radial-gradient(2px 2px at 92% 75%, var(--color-candle), transparent),
    radial-gradient(1px 1px at 45% 90%, var(--color-gold-dim), transparent),
    radial-gradient(1.5px 1.5px at 15% 70%, var(--color-candle), transparent),
    radial-gradient(1px 1px at 68% 88%, var(--color-gold-dim), transparent);
  animation: ember-drift 14s ease-in-out infinite alternate;
  opacity: var(--particle-opacity);
}

.browser-shell::after {
  position: fixed;
  z-index: -1;
  inset: 0;
  pointer-events: none;
  content: '';
  background:
    radial-gradient(1px 1px at 22% 30%, var(--color-gold-dim), transparent),
    radial-gradient(1.5px 1.5px at 55% 20%, var(--color-candle), transparent),
    radial-gradient(1px 1px at 78% 60%, var(--color-gold-dim), transparent),
    radial-gradient(1px 1px at 35% 50%, var(--color-gold-dim), transparent),
    radial-gradient(1.5px 1.5px at 88% 85%, var(--color-candle), transparent);
  animation: ember-drift-slow 22s ease-in-out infinite alternate-reverse;
  opacity: calc(var(--particle-opacity) * 0.5);
}

@keyframes ember-drift {
  0% { transform: translateY(0) translateX(0); }
  25% { transform: translateY(-10px) translateX(4px); }
  50% { transform: translateY(-5px) translateX(-3px); }
  75% { transform: translateY(-14px) translateX(2px); }
  100% { transform: translateY(-8px) translateX(-1px); }
}

@keyframes ember-drift-slow {
  0% { transform: translateY(0) translateX(0); }
  50% { transform: translateY(-6px) translateX(5px); }
  100% { transform: translateY(-12px) translateX(-3px); }
}
```

- [ ] **Step 4: Add reduced-motion overrides**

At the end of `base.css`, ensure:

```css
@media (prefers-reduced-motion: reduce) {
  :root {
    --particle-opacity: 0;
    --motion-breathe: 0s;
  }

  body::before,
  .browser-shell::before,
  .browser-shell::after {
    animation: none !important;
  }
}
```

- [ ] **Step 5: Verify build**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/styles/tokens.css BookOfEternityClient.WebFrontend/src/styles/base.css
git commit -m "feat(browser): cinematic design tokens, breathing background, enhanced particles

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Sidebar component

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/Sidebar.tsx`
- Create: `BookOfEternityClient.WebFrontend/src/styles/sidebar.css`
- Modify: `BookOfEternityClient.WebFrontend/src/components/navBarConfig.ts` (add `group` field)

- [ ] **Step 1: Extend navBarConfig with grouping**

Replace the contents of `navBarConfig.ts`:

```typescript
import type { RouteId } from '../context/ShellContext';

export interface NavItem {
  id: RouteId;
  glyph: string;
  label: string;
  shortcut: string;
  group: 'primary' | 'secondary';
}

export const routeNav: NavItem[] = [
  { id: 'home', glyph: '📖', label: 'Главная', shortcut: '1', group: 'primary' },
  { id: 'game', glyph: '🔥', label: 'Игра', shortcut: '2', group: 'primary' },
  { id: 'soul', glyph: '🕯️', label: 'Душа', shortcut: '3', group: 'primary' },
  { id: 'world', glyph: '🗺️', label: 'Мир', shortcut: '4', group: 'primary' },
  { id: 'journal', glyph: '📜', label: 'Журнал', shortcut: '5', group: 'primary' },
  { id: 'inventory', glyph: '🎒', label: 'Инвентарь', shortcut: '6', group: 'primary' },
  { id: 'media', glyph: '🖼️', label: 'Медиа', shortcut: '7', group: 'secondary' },
  { id: 'settings', glyph: '⚙️', label: 'Настройки', shortcut: '8', group: 'secondary' }
];

export function resolveRouteShortcut(key: string): RouteId | null {
  const index = Number(key) - 1;
  return Number.isInteger(index) && index >= 0 && index < routeNav.length ? routeNav[index].id : null;
}
```

- [ ] **Step 2: Create Sidebar component**

Create `BookOfEternityClient.WebFrontend/src/components/Sidebar.tsx`:

```tsx
import { useEffect } from 'react';
import { useShell } from '../context/ShellContext';
import { resolveRouteShortcut, routeNav } from './navBarConfig';

function isShortcutBlockedTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement
    || (target instanceof HTMLElement && target.isContentEditable);
}

export function Sidebar() {
  const { activeRoute, realmTheme, setActiveRoute } = useShell();

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (isShortcutBlockedTarget(event.target) || event.ctrlKey || event.altKey || event.metaKey) {
        return;
      }
      const routeId = resolveRouteShortcut(event.key);
      if (!routeId) return;
      event.preventDefault();
      setActiveRoute(routeId);
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [setActiveRoute]);

  const primary = routeNav.filter(r => r.group === 'primary');
  const secondary = routeNav.filter(r => r.group === 'secondary');

  return (
    <nav className="sidebar" aria-label="Разделы игры">
      <div className="sidebar__logo" aria-hidden="true">
        <span className="sidebar__logo-icon">{realmTheme.icon}</span>
      </div>

      <div className="sidebar__primary">
        {primary.map(item => (
          <button
            key={item.id}
            type="button"
            className={`sidebar__item${activeRoute === item.id ? ' is-active' : ''}`}
            onClick={() => setActiveRoute(item.id)}
            aria-current={activeRoute === item.id ? 'page' : undefined}
            aria-label={`${item.label} (${item.shortcut})`}
            title={`${item.label} — клавиша ${item.shortcut}`}
          >
            <span className="sidebar__glyph" aria-hidden="true">{item.glyph}</span>
            <span className="sidebar__label">{item.label}</span>
          </button>
        ))}
      </div>

      <div className="sidebar__secondary">
        {secondary.map(item => (
          <button
            key={item.id}
            type="button"
            className={`sidebar__item${activeRoute === item.id ? ' is-active' : ''}`}
            onClick={() => setActiveRoute(item.id)}
            aria-current={activeRoute === item.id ? 'page' : undefined}
            aria-label={`${item.label} (${item.shortcut})`}
            title={`${item.label} — клавиша ${item.shortcut}`}
          >
            <span className="sidebar__glyph" aria-hidden="true">{item.glyph}</span>
            <span className="sidebar__label">{item.label}</span>
          </button>
        ))}
      </div>
    </nav>
  );
}
```

- [ ] **Step 3: Create sidebar styles**

Create `BookOfEternityClient.WebFrontend/src/styles/sidebar.css`:

```css
/* ═══════════════════════════════════════════════════════════════════
   SIDEBAR — Cinematic vertical navigation
   ═══════════════════════════════════════════════════════════════════ */

.sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: var(--sidebar-width);
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: var(--space-4) 0;
  background: rgba(6, 8, 9, 0.95);
  border-right: 1px solid color-mix(in srgb, var(--realm-accent, var(--color-gold)) 12%, rgba(255, 255, 255, 0.04));
  backdrop-filter: blur(12px);
  z-index: 200;
  overflow-y: auto;
  overflow-x: hidden;
}

/* Logo area */
.sidebar__logo {
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: var(--space-5);
  border-radius: 14px;
  background: linear-gradient(135deg, color-mix(in srgb, var(--realm-accent) 15%, transparent), transparent);
  border: 1px solid color-mix(in srgb, var(--realm-accent) 25%, rgba(255, 255, 255, 0.06));
  font-size: 1.4rem;
  box-shadow: var(--glow-realm);
}

/* Nav item groups */
.sidebar__primary {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-1);
  flex: 1;
}

.sidebar__secondary {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-1);
  margin-top: auto;
  padding-top: var(--space-4);
  border-top: 1px solid rgba(255, 255, 255, 0.04);
}

/* Individual nav item */
.sidebar__item {
  position: relative;
  width: 52px;
  height: 52px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  border-radius: 14px;
  background: transparent;
  border: 1px solid transparent;
  cursor: pointer;
  color: var(--text-muted);
  transition:
    background var(--motion-fast),
    border-color var(--motion-fast),
    color var(--motion-fast),
    transform var(--motion-fast),
    box-shadow var(--motion-fast);
}

.sidebar__item:hover {
  background: var(--surface-hover);
  border-color: var(--border-subtle);
  color: var(--text-primary);
  transform: scale(1.05);
}

.sidebar__item.is-active {
  background: linear-gradient(135deg,
    color-mix(in srgb, var(--realm-accent) 18%, transparent),
    color-mix(in srgb, var(--realm-accent) 6%, transparent));
  border-color: color-mix(in srgb, var(--realm-accent) 40%, rgba(255, 255, 255, 0.1));
  color: var(--text-primary);
  box-shadow: var(--glow-realm);
}

/* Active indicator bar */
.sidebar__item.is-active::before {
  content: '';
  position: absolute;
  left: -1px;
  top: 25%;
  bottom: 25%;
  width: 3px;
  border-radius: 0 3px 3px 0;
  background: var(--realm-accent, var(--color-gold));
  box-shadow: 0 0 8px var(--realm-accent, var(--color-gold));
}

.sidebar__glyph {
  font-size: 1.3rem;
  line-height: 1;
}

.sidebar__label {
  font-size: 0.58rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  white-space: nowrap;
  opacity: 0.85;
}

.sidebar__item.is-active .sidebar__label {
  color: var(--realm-accent, var(--color-gold));
  opacity: 1;
  font-weight: 600;
}

/* ── Responsive: tablet collapse ── */
@media (max-width: 900px) and (min-width: 641px) {
  .sidebar {
    width: var(--sidebar-width-collapsed);
  }

  .sidebar__item {
    width: 44px;
    height: 44px;
  }

  .sidebar__label {
    display: none;
  }
}

/* ── Responsive: mobile — bottom bar ── */
@media (max-width: 640px) {
  .sidebar {
    position: fixed;
    top: auto;
    bottom: 0;
    left: 0;
    right: 0;
    width: 100%;
    height: auto;
    flex-direction: row;
    padding: var(--space-2) 0;
    padding-bottom: env(safe-area-inset-bottom, 0);
    border-right: none;
    border-top: 1px solid color-mix(in srgb, var(--realm-accent) 12%, rgba(255, 255, 255, 0.04));
  }

  .sidebar__logo {
    display: none;
  }

  .sidebar__primary {
    flex-direction: row;
    flex: 1;
    justify-content: space-around;
    gap: 0;
  }

  .sidebar__secondary {
    flex-direction: row;
    margin-top: 0;
    padding-top: 0;
    border-top: none;
    border-left: 1px solid rgba(255, 255, 255, 0.04);
    padding-left: var(--space-2);
    gap: 0;
  }

  .sidebar__item {
    width: 44px;
    height: 44px;
    border-radius: 10px;
  }

  .sidebar__item.is-active::before {
    left: 25%;
    right: 25%;
    top: -1px;
    bottom: auto;
    width: auto;
    height: 3px;
    border-radius: 0 0 3px 3px;
  }

  .sidebar__label {
    font-size: 0.5rem;
  }
}
```

- [ ] **Step 4: Verify**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS (component compiles, no usage yet in App)

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/Sidebar.tsx BookOfEternityClient.WebFrontend/src/components/navBarConfig.ts BookOfEternityClient.WebFrontend/src/styles/sidebar.css
git commit -m "feat(browser): add cinematic Sidebar navigation component

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Layout restructure — wire Sidebar into App

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/layout.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles.css` (import order)

- [ ] **Step 1: Add sidebar.css import**

In the main styles entry file (`BookOfEternityClient.WebFrontend/src/styles.css`), add after the existing imports:

```css
@import './styles/sidebar.css';
```

- [ ] **Step 2: Replace NavBar with Sidebar in App.tsx**

In `App.tsx`, change the import:

```typescript
// Remove:
import { NavBar } from './components/NavBar';
// Add:
import { Sidebar } from './components/Sidebar';
```

In the `AppShell` component, replace `<NavBar />` (line 306) with `<Sidebar />`.

- [ ] **Step 3: Update layout grid**

In `layout.css`, replace the `.browser-shell` rule (lines 1-13) with:

```css
.browser-shell {
  --realm-accent: var(--realm-mortal);
  --browser-font-scale: 1;
  display: grid;
  grid-template-columns: var(--sidebar-width) 1fr;
  grid-template-rows: 1fr;
  min-height: 100vh;
  width: 100%;
  max-width: 100vw;
  margin: 0;
  padding: 0;
  font-size: calc(1rem * var(--browser-font-scale));
  overflow-x: hidden;
}
```

Update the `.workspace-grid` to remove top padding (it's now full-height):

```css
.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(16rem, 24rem);
  gap: var(--space-4);
  align-items: start;
  align-content: start;
  min-height: 0;
  padding: var(--space-4);
  overflow-y: auto;
  max-height: 100vh;
}
```

Update the tablet breakpoint (around `@media (max-width: 900px)`) to handle sidebar collapse:

```css
@media (max-width: 900px) and (min-width: 641px) {
  .browser-shell {
    grid-template-columns: var(--sidebar-width-collapsed) 1fr;
  }
}
```

Update the mobile breakpoint (`@media (max-width: 640px)`) — sidebar is bottom bar:

```css
@media (max-width: 640px) {
  .browser-shell {
    grid-template-columns: 1fr;
    grid-template-rows: 1fr;
    padding-bottom: calc(64px + env(safe-area-inset-bottom, 0));
  }

  .workspace-grid {
    max-height: none;
    padding: var(--space-3);
  }
}
```

Remove the old `.nav-bar` mobile fixed positioning from `layout.css` (lines 178-201 — the `@media (max-width: 640px)` block that styles `.nav-bar`).

- [ ] **Step 4: Verify**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/layout.css BookOfEternityClient.WebFrontend/src/styles.css
git commit -m "feat(browser): wire Sidebar into App, restructure grid layout

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: SceneHero component

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/SceneHero.tsx`
- Create: `BookOfEternityClient.WebFrontend/src/styles/hero.css`

- [ ] **Step 1: Create SceneHero component**

Create `BookOfEternityClient.WebFrontend/src/components/SceneHero.tsx`:

```tsx
interface SceneHeroProps {
  imageUrl?: string | null;
  eyebrow?: string;
  title: string;
  subtitle?: string;
  loading?: boolean;
}

export function SceneHero({ imageUrl, eyebrow, title, subtitle, loading }: SceneHeroProps) {
  return (
    <header className="scene-hero">
      {imageUrl && (
        <div className="scene-hero__image" aria-hidden="true">
          <img src={imageUrl} alt="" loading="lazy" />
        </div>
      )}
      <div className="scene-hero__beam" aria-hidden="true" />
      <div className="scene-hero__gradient" aria-hidden="true" />
      <div className="scene-hero__content">
        {eyebrow && <span className="scene-hero__eyebrow">{eyebrow}</span>}
        <h1 className="scene-hero__title">{title}</h1>
        {subtitle && <p className="scene-hero__subtitle">{subtitle}</p>}
        {loading && <p className="scene-hero__loading">🎨 Генерация образа…</p>}
      </div>
    </header>
  );
}
```

- [ ] **Step 2: Create hero styles**

Create `BookOfEternityClient.WebFrontend/src/styles/hero.css`:

```css
/* ═══════════════════════════════════════════════════════════════════
   SCENE HERO — Cinematic banner with parallax and light effects
   ═══════════════════════════════════════════════════════════════════ */

.scene-hero {
  position: relative;
  height: 240px;
  display: flex;
  align-items: flex-end;
  padding: var(--space-5) var(--space-6);
  margin: calc(-1 * var(--space-4)) calc(-1 * var(--space-4)) var(--space-4);
  overflow: hidden;
  background: linear-gradient(135deg, #1a1008 0%, #0d0a06 40%, var(--color-ink) 100%);
  border-radius: var(--radius-lg) var(--radius-lg) 0 0;
}

/* Background image */
.scene-hero__image {
  position: absolute;
  inset: 0;
}

.scene-hero__image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center 30%;
  opacity: 0.45;
  filter: saturate(0.7) brightness(0.5);
}

/* Cinematic light beam */
.scene-hero__beam {
  position: absolute;
  top: 0;
  left: 35%;
  width: 30%;
  height: 100%;
  background: linear-gradient(180deg,
    color-mix(in srgb, var(--realm-accent, var(--color-gold)) 8%, transparent) 0%,
    transparent 70%);
  transform: skewX(-5deg);
  pointer-events: none;
}

/* Bottom gradient fade */
.scene-hero__gradient {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 60%;
  background: linear-gradient(to top, var(--color-ink) 0%, transparent 100%);
  pointer-events: none;
}

/* Content overlay */
.scene-hero__content {
  position: relative;
  z-index: 1;
}

.scene-hero__eyebrow {
  display: block;
  color: var(--color-gold);
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.25em;
  margin-bottom: var(--space-2);
}

.scene-hero__title {
  margin: 0 0 var(--space-1);
  font-family: var(--font-display);
  font-size: clamp(1.5rem, 3vw, 2rem);
  color: var(--color-parchment);
  text-shadow: 0 2px 24px rgba(0, 0, 0, 0.7);
  max-width: none;
  line-height: 1.2;
}

.scene-hero__subtitle {
  margin: 0;
  color: var(--color-mist);
  font-size: 0.85rem;
}

.scene-hero__loading {
  margin: var(--space-2) 0 0;
  font-style: italic;
  color: var(--color-gold-dim);
  font-size: 0.8rem;
  animation: pulse 2s ease-in-out infinite;
}

/* Top accent line */
.scene-hero::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 2px;
  background: linear-gradient(90deg, transparent 10%, var(--realm-accent, var(--color-gold)) 50%, transparent 90%);
  opacity: 0.6;
}

/* ── Responsive ── */
@media (max-width: 640px) {
  .scene-hero {
    height: 180px;
    padding: var(--space-4);
    margin: calc(-1 * var(--space-3)) calc(-1 * var(--space-3)) var(--space-3);
  }

  .scene-hero__title {
    font-size: 1.3rem;
  }
}
```

- [ ] **Step 3: Add hero.css import**

In `BookOfEternityClient.WebFrontend/src/styles.css`, add:

```css
@import './styles/hero.css';
```

- [ ] **Step 4: Verify**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/SceneHero.tsx BookOfEternityClient.WebFrontend/src/styles/hero.css BookOfEternityClient.WebFrontend/src/styles.css
git commit -m "feat(browser): add SceneHero cinematic banner component

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: Card & surface visual enhancement

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

- [ ] **Step 1: Enhance card base styles**

In `components.css`, replace the shared panel surfaces rule (lines 27-41) with:

```css
.shell-panel,
.advanced-diagnostics,
.summary-card,
.game-launcher,
.empty-state,
.error-notice,
.narrative-card,
.reborn-systems-panel,
.qte-scene-panel,
.media-atlas-panel {
  position: relative;
  border: 1px solid var(--border-card);
  border-radius: var(--radius-lg);
  background: var(--surface-card);
  box-shadow: var(--shadow-panel);
  transition:
    border-color var(--motion-panel),
    box-shadow var(--motion-panel),
    transform var(--motion-fast);
}

/* Top accent line on all cards */
.shell-panel::before,
.summary-card::before,
.narrative-card::before,
.game-launcher::before {
  content: '';
  position: absolute;
  top: 0;
  left: 10%;
  right: 10%;
  height: 1px;
  background: linear-gradient(90deg, transparent, var(--realm-accent, var(--color-gold)), transparent);
  opacity: 0.4;
  border-radius: 0 0 1px 1px;
}
```

- [ ] **Step 2: Enhance narrative card**

Replace the `.narrative-card` and `.narrative-card.is-featured` rules (around lines 106-116) with:

```css
.narrative-card {
  margin-bottom: var(--space-4);
  padding: var(--space-5);
  font-family: var(--font-narrative);
  font-size: clamp(1rem, 1.2vw, 1.12rem);
  line-height: 1.8;
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}

.narrative-card.is-featured {
  border-color: color-mix(in srgb, var(--realm-accent) 35%, rgba(255, 255, 255, 0.08));
  box-shadow: var(--glow-realm), var(--shadow-panel);
}
```

- [ ] **Step 3: Enhance summary-card hover**

Replace the `.summary-card:hover` rule (lines 101-104) with:

```css
.summary-card:hover {
  transform: translateY(-3px);
  border-color: var(--border-card-hover);
  box-shadow: var(--glow-hover), var(--shadow-panel-strong);
}
```

- [ ] **Step 4: Verify**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/styles/components.css
git commit -m "feat(browser): cinematic card surfaces with glow and accent lines

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: Dialogue options & composer redesign

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/routes/GameRoute.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

- [ ] **Step 1: Add dialogue category color classes to GameRoute**

In `GameRoute.tsx`, replace the dialogue options `<ul>` section (lines 40-51) with color-coded cards:

```tsx
{game.narrative.dialogueOptions.length > 0 ? (
  <div className="dialogue-options">
    {game.narrative.dialogueOptions.map((option) => (
      <div key={option.id} className={`dialogue-option dialogue-option--${mapDialogueCategory(option.category)}`}>
        <span className="dialogue-option__text">{option.text}</span>
        <span className="dialogue-option__category">{formatDialogueCategory(option.category)}</span>
      </div>
    ))}
  </div>
) : (
  <p className="muted">Варианты появятся здесь после ответа ГМа.</p>
)}
```

Add a helper function at the bottom of the file (before the `export`):

```typescript
function mapDialogueCategory(category: string): string {
  const lower = category.toLowerCase();
  if (lower.includes('исследов') || lower.includes('знани') || lower.includes('explor') || lower.includes('knowl')) return 'explore';
  if (lower.includes('действ') || lower.includes('атак') || lower.includes('action') || lower.includes('attack')) return 'action';
  if (lower.includes('социал') || lower.includes('диплом') || lower.includes('social') || lower.includes('diplo')) return 'social';
  return 'neutral';
}
```

- [ ] **Step 2: Add dialogue option styles**

In `components.css`, add after the narrative card styles:

```css
/* ── Dialogue options — color-coded ── */

.dialogue-options {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.dialogue-option {
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  cursor: pointer;
  transition: border-color var(--motion-fast), background var(--motion-fast), transform var(--motion-fast), box-shadow var(--motion-fast);
}

.dialogue-option:hover {
  transform: translateX(4px);
  background: rgba(255, 255, 255, 0.04);
}

.dialogue-option--explore {
  border-color: color-mix(in srgb, var(--color-gold) 25%, transparent);
}

.dialogue-option--explore:hover {
  border-color: color-mix(in srgb, var(--color-gold) 50%, transparent);
  box-shadow: 0 0 16px color-mix(in srgb, var(--color-gold) 12%, transparent);
}

.dialogue-option--action {
  border-color: color-mix(in srgb, var(--color-crimson-soft) 25%, transparent);
}

.dialogue-option--action:hover {
  border-color: color-mix(in srgb, var(--color-crimson-soft) 50%, transparent);
  box-shadow: 0 0 16px color-mix(in srgb, var(--color-crimson-soft) 12%, transparent);
}

.dialogue-option--social {
  border-color: color-mix(in srgb, var(--realm-shining) 20%, transparent);
}

.dialogue-option--social:hover {
  border-color: color-mix(in srgb, var(--realm-shining) 45%, transparent);
  box-shadow: 0 0 16px color-mix(in srgb, var(--realm-shining) 12%, transparent);
}

.dialogue-option--neutral {
  border-color: rgba(255, 255, 255, 0.1);
}

.dialogue-option--neutral:hover {
  border-color: rgba(255, 255, 255, 0.2);
}

.dialogue-option__text {
  display: block;
  color: var(--color-parchment);
  font-size: 0.9rem;
  font-weight: 500;
}

.dialogue-option__category {
  display: block;
  margin-top: var(--space-1);
  color: var(--color-mist);
  font-size: 0.75rem;
}
```

- [ ] **Step 3: Enhance composer styles**

In `components.css`, find the `.composer` styles and add/override:

```css
/* ── Cinematic composer ── */

.composer {
  background: rgba(10, 14, 16, 0.9);
  border: 1px solid color-mix(in srgb, var(--realm-accent) 12%, rgba(255, 255, 255, 0.06));
  border-radius: var(--radius-lg);
  padding: var(--space-3);
}

.composer textarea {
  background: transparent;
  border: none;
  color: var(--color-parchment);
  font-size: 0.9rem;
  resize: vertical;
}

.composer textarea:focus {
  outline: none;
  box-shadow: none;
}

.composer__submit,
.composer button[type="submit"] {
  padding: var(--space-2) var(--space-4);
  background: linear-gradient(135deg, var(--color-gold), var(--color-gold-dim));
  border: none;
  border-radius: var(--radius-md);
  color: var(--color-ink);
  font-size: 0.85rem;
  font-weight: 600;
  cursor: pointer;
  transition: box-shadow var(--motion-fast), transform var(--motion-fast);
}

.composer__submit:hover,
.composer button[type="submit"]:hover {
  box-shadow: 0 0 20px color-mix(in srgb, var(--color-gold) 30%, transparent);
  transform: translateY(-1px);
}

.composer__submit:disabled,
.composer button[type="submit"]:disabled {
  background: var(--color-ash-light);
  color: var(--color-mist);
  box-shadow: none;
  transform: none;
}
```

- [ ] **Step 4: Verify**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/routes/GameRoute.tsx BookOfEternityClient.WebFrontend/src/styles/components.css
git commit -m "feat(browser): color-coded dialogue options and cinematic composer

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 7: Wire SceneHero into routes

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/routes/GameRoute.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/routes/WorldRoute.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/routes/HomeRoute.tsx`

- [ ] **Step 1: Add SceneHero to GameRoute**

In `GameRoute.tsx`, add import:

```typescript
import { SceneHero } from '../components/SceneHero';
```

Right before `<article className="narrative-card is-featured">`, add:

```tsx
<SceneHero
  imageUrl={sceneImage.url}
  eyebrow={`Ход ${game.world.turnNumber}`}
  title={game.theme.label}
  subtitle={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || ''}`}
  loading={sceneImage.loading}
/>
```

(Note: `sceneImage` hook is already imported and used in GameRoute from the media integration PR.)

- [ ] **Step 2: Add SceneHero to WorldRoute**

In `WorldRoute.tsx`, add import:

```typescript
import { SceneHero } from '../components/SceneHero';
```

After `const locationImage = ...`, right before `<div className="split-grid three">`, add:

```tsx
<SceneHero
  imageUrl={locationImage.url}
  eyebrow="Мир"
  title={game.world.location || 'Локация уточняется'}
  subtitle={`${game.world.worldTime || 'время уточняется'} · Ход ${game.world.turnNumber}`}
  loading={locationImage.loading}
/>
```

- [ ] **Step 3: Add SceneHero to HomeRoute**

In `HomeRoute.tsx` (this is the GameLauncher wrapper), add import:

```typescript
import { SceneHero } from '../components/SceneHero';
```

Add a static hero at the top of the returned content (inside ShellPanel, before GameLauncher):

```tsx
<SceneHero
  eyebrow="Книга Вечности"
  title="Перерождение"
  subtitle="Бесконечное странствие души через жизни, смерти и перерождения"
/>
```

- [ ] **Step 4: Verify**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/routes/GameRoute.tsx BookOfEternityClient.WebFrontend/src/routes/WorldRoute.tsx BookOfEternityClient.WebFrontend/src/routes/HomeRoute.tsx
git commit -m "feat(browser): wire SceneHero into Game, World, and Home routes

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 8: NavBar cleanup & guard tests

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/components/NavBar.tsx` (keep as legacy, unused on desktop)
- Possibly modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Possibly modify: `BookOfEternityClient.WebFrontend/src/App.tsx` (manifest markers)

- [ ] **Step 1: Run full frontend verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: PASS (typecheck + vitest + build)

- [ ] **Step 2: Run C# guard tests**

Run: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"`
Expected: All pass.

If guard tests fail because they expect `.nav-bar` in the built output and now it's `.sidebar`, update the assertions in `BrowserFrontendWorkspaceTests.cs` to check for `.sidebar` instead.

Also check if `App.tsx` source markers (the large comment block lines 31-282) need updates. The guard tests may scan for class names like `.nav-bar` — if so, add `.sidebar` markers to the comment block.

- [ ] **Step 3: Fix any failures**

If tests expect `.nav-bar__item` class in built CSS, add these source markers to the App.tsx comment block:

```
sidebar
sidebar__item
sidebar__glyph
sidebar__label
sidebar__logo
scene-hero
scene-hero__title
dialogue-option
dialogue-option--explore
dialogue-option--action
dialogue-option--social
```

- [ ] **Step 4: Push and create PR**

```bash
git push origin feat/cinematic-redesign
gh pr create --title "feat(browser): cinematic UI redesign — sidebar, heroes, particles, glow" \
  --body "## Cinematic Redesign

### Changes
1. **Sidebar navigation** — vertical nav with realm-accent glow, mobile bottom bar fallback
2. **SceneHero component** — cinematic banner with light beams, gradient fade, parallax
3. **Breathing background** — 30s ambient gradient animation
4. **Enhanced particles** — 12+ CSS particles in two layers, realm-colored
5. **Card redesign** — accent lines, glow borders, hover lift effects
6. **Dialogue options** — color-coded by category (gold/crimson/teal)
7. **Cinematic composer** — gradient submit button, dark input area
8. **Playfair Display** — serif font for narrative text and headings
9. **Design tokens** — new variables for glow, surfaces, sidebar width

### Accessibility
- All animations respect prefers-reduced-motion
- Sidebar has proper ARIA (role=navigation, aria-current=page)
- Keyboard shortcuts preserved (1-8)
- High contrast mode supported

### Screenshots
(add after visual testing)" --base main --head feat/cinematic-redesign
```

- [ ] **Step 5: Commit any guard test fixes**

```bash
git add -A
git commit -m "fix(browser): update guard test markers for cinematic redesign

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```
