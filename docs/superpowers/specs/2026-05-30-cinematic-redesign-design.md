# Browser Client Cinematic Redesign — Design Spec

**Date:** 2026-05-30  
**Issue:** Visual overhaul — modern cinematic dark-fantasy UI  
**Status:** Draft

---

## 1. Problem

The current browser client is functional but visually flat. It uses basic cards with minimal depth, a crowded bottom navigation bar with 8 buttons, and lacks the dramatic visual storytelling appropriate for a dark-fantasy game. The UI doesn't evoke atmosphere or create a "wow" first impression.

## 2. Design Direction

**Cinematic dark-fantasy** — inspired by AAA game UI (Diablo IV, Path of Exile, Baldur's Gate 3). Key qualities:
- Dramatic lighting with directional gradients and volumetric light beams
- Deep spatial layering (background art → particles → content → foreground effects)
- Slow, breathing ambient animations that create life without distraction
- Strong typographic hierarchy: serif narrative text (Playfair Display/Georgia) contrasting with clean sans-serif UI (Inter)
- Gold/crimson accent palette on near-black surfaces

**Balance principle:** Epic enough to impress, restrained enough for long reading sessions. Narrative text legibility is never sacrificed for visual effect.

## 3. Architecture of Changes

### 3.1 Navigation — Sidebar

Replace the bottom `NavBar` with a vertical sidebar:
- **Desktop (>900px):** Fixed left sidebar, 72px wide. Icon + small label for each route. Active item glows with realm-accent.
- **Tablet (640–900px):** Collapsed sidebar, 56px, icons only with tooltips.
- **Mobile (<640px):** Bottom bar (current behavior but simplified — only 5 primary items visible, rest in overflow "⋯" menu).

**Routes grouped:**
- Primary (always visible): Главная, Игра, Душа, Мир, Журнал, Инвентарь
- Secondary (bottom of sidebar): Медиа, Настройки

**Active state:** Gradient background + left accent bar (3px) + glow shadow. Inactive items are muted with hover glow.

### 3.2 Layout Grid

Change from `grid-template-rows: auto 1fr auto` (header/content/footer) to:

```
grid-template-columns: var(--sidebar-width, 72px) 1fr;
grid-template-rows: 1fr;
min-height: 100vh;
```

Main content area gets full height, scroll is within main. Sidebar is sticky.

### 3.3 Scene Hero Zone

Every route page gets an optional hero area at the top:
- **GameRoute:** Generated scene art (via useSceneImage hook, already exists) displayed as a 240px cinematic banner with gradient fade to content.
- **WorldRoute:** Location art banner (already has locationImage hook).
- **HomeRoute / GameLauncher:** Static branded art with animated light rays.
- **Other routes:** Subtle gradient header with realm-accent coloring.

Hero zone includes:
- Background image (generated or gradient)
- Cinematic light beam (CSS pseudo-element, animated subtly)
- Top: golden accent line (2px gradient)
- Bottom: gradient fade into page background
- Text overlay: eyebrow (uppercase, gold, tracked) + title (serif, large) + subtitle (muted)

### 3.4 Card Redesign

All cards (`.narrative-card`, `.summary-card`, `.game-launcher`, etc.) get:
- Top accent line: `background: linear-gradient(90deg, transparent, var(--realm-accent), transparent)` — 2px height
- Background: slightly elevated from base (`rgba(14,19,20,0.8)`)
- Border: `1px solid rgba(var(--realm-accent-rgb), 0.15)` — subtle realm coloring
- Border-radius: 16px (increased from current 1.2rem ≈ 19px — keep at `--radius-lg`)
- Hover state: border brightens, subtle upward translate (-2px), glow shadow expands

### 3.5 Dialogue Options Redesign

Replace flat `<li>` list with color-coded action cards:
- Each option gets its own bordered card
- Category coloring:
  - Исследование/Знание: gold border (`rgba(201,162,77,0.2)`)
  - Действие/Атака: crimson border (`rgba(139,26,26,0.2)`)
  - Социальное/Дипломатия: teal border (`rgba(109,207,184,0.15)`)
  - Default: neutral mist border
- Hover: border brightens, background fills slightly, glow appears
- Structure: main text (bold, parchment) + category label below (muted, small)

### 3.6 Ambient Particle System

Enhance existing `ember-drift` particles:
- Increase particle count from 7 to ~12–15 (still CSS radial-gradients, no JS)
- Add second layer with slightly different timing (`ember-drift-slow`, 20s cycle)
- Particle colors respond to realm: gold (mortal), purple (chaos), teal (shining)
- Add `--particle-opacity` token controlled by prefers-reduced-motion and user setting
- Sidebar gets its own micro-particles (2–3 dots drifting vertically)

### 3.7 Breathing Background

Replace static body gradient with slowly animating one:
- CSS `@keyframes bg-breathe` — 30s cycle, subtly shifts radial gradient positions
- Three overlapping radial gradients with realm-accent colors
- Movement is barely perceptible (2–3% position shift) but creates life
- `prefers-reduced-motion: reduce` disables this

### 3.8 Typography Enhancement

- Add `Playfair Display` (Google Fonts) as `--font-display` for headings and narrative
- Narrative text: Playfair Display, 1.1rem, line-height 1.8, `text-shadow: 0 0 2rem rgba(201,162,77,0.06)`
- Section eyebrows: Inter, 0.7rem, weight 800, letter-spacing 0.2em, uppercase, gold
- Card headings: Inter SemiBold, 0.95rem
- Body text: Inter, 0.875rem

### 3.9 Action Composer Redesign

Replace the current composer with a cinematic input area:
- Dark container with subtle inner border
- Textarea: transparent background, parchment text, placeholder in mist color
- Submit button: gradient gold (`linear-gradient(135deg, #c9a24d, #8a7033)`), dark text, rounded
- Button hover: glow expands, brightness increases
- Disabled state: desaturated, no glow

### 3.10 Hover & Interaction Effects

All interactive elements get consistent micro-interactions:
- Buttons: `transform: translateY(-1px)` + shadow expansion on hover
- Cards: `transform: translateY(-2px)` + border glow brighten + shadow expand
- Nav items: background glow pulse + icon scale(1.05)
- Transitions: 200ms cubic-bezier(0.2, 0.8, 0.2, 1) — snappy but smooth
- Click/active: scale(0.98) briefly — tactile "press" feedback

### 3.11 Parallax Hero (Optional Enhancement)

On scroll, hero image moves at 0.5x speed relative to content, creating depth. Implemented via:
- CSS `background-attachment: fixed` (simple, performant)
- OR `transform: translateY(calc(var(--scroll-y) * 0.3))` with a lightweight scroll listener
- Disabled when `prefers-reduced-motion` is active

## 4. Files Changed

### New files:
- `src/components/Sidebar.tsx` — new sidebar navigation component
- `src/components/SceneHero.tsx` — reusable cinematic hero zone
- `src/components/ParticleField.tsx` — CSS particle overlay (no JS particles — pure CSS)
- `src/styles/sidebar.css` — sidebar-specific styles
- `src/styles/hero.css` — hero zone styles

### Modified files:
- `src/styles/tokens.css` — new tokens (sidebar width, particle opacity, font-display update)
- `src/styles/base.css` — breathing background, enhanced particles, Playfair import
- `src/styles/layout.css` — sidebar grid, remove bottom nav grid
- `src/styles/components.css` — card redesign, dialogue options, composer, hover effects
- `src/styles/motion.css` — new keyframes (bg-breathe, glow-pulse, parallax)
- `src/components/NavBar.tsx` → refactored into `Sidebar.tsx` (keep NavBar as mobile fallback)
- `src/routes/GameRoute.tsx` — scene hero integration, dialogue option styling
- `src/routes/WorldRoute.tsx` — location hero integration
- `src/routes/HomeRoute.tsx` — branded hero zone
- `src/App.tsx` — layout grid change, sidebar slot

### Deleted files:
- None (NavBar.tsx kept for mobile, just not rendered on desktop)

## 5. Accessibility

- `prefers-reduced-motion: reduce` disables all animations, particles, parallax, breathing background
- Existing `is-reduced-motion` class on shell continues to work
- Sidebar keyboard navigation: Tab through items, Enter to activate, keyboard shortcuts preserved
- High contrast mode: `is-contrast-friendly` increases accent opacity, disables background effects
- Mobile: bottom nav ensures thumb reachability
- ARIA: sidebar has `role="navigation"`, active item has `aria-current="page"`

## 6. Performance

- All particles are CSS-only (radial-gradients + keyframes) — no JS particle libraries
- Breathing background uses `will-change: background-position` (or `transform` on pseudo-element)
- Hero images use `loading="lazy"` (already implemented)
- Animations use `transform` and `opacity` only (GPU-composited, no layout thrashing)
- Google Fonts loaded with `font-display: swap` to prevent FOIT

## 7. Success Criteria

- **First impression:** Opening the app immediately communicates "premium dark-fantasy game" — not a generic admin panel
- **Atmosphere:** Particles, glow, breathing background create a living world feel
- **Readability:** Narrative text is still perfectly legible, no visual noise over text areas
- **Responsiveness:** Smooth experience from 320px to 4K
- **Performance:** No dropped frames on mid-range hardware (60fps target for all animations)
