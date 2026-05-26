# Browser Client UX Redesign Spec

## Date: 2026-05-27
## Status: Approved (autonomous mode)

## Problem Statement

The browser client UI has 3 critical issues:
1. **Feels like a debug dashboard** — sidebar with 5 status cards, status pills everywhere, muted explanatory text
2. **No console-client-like input model** — composer doesn't submit, no slash commands, no command→modal flow
3. **No modern React wow-factor** — no transitions, no animations, no particles, no skeleton loading

## Design

### 1. Compact Navigation Bar (replaces route card grid)

Replace 8 route cards with a slim horizontal nav bar:
- Icon-only buttons for each route (with tooltip on hover)
- Active route highlighted with realm accent color
- Realm icon + soul name on the left
- Settings gear on the right
- Keyboard shortcuts: 1-8 for route switching

### 2. Main Game View (replaces cluttered game route)

The game route becomes the primary view with 3 zones:

**Narrative Zone** (top, ~60% of screen):
- Narrative text with typewriter fade-in effect
- Dialogue options as styled buttons below narrative
- Realm-themed decorative border

**Command Zone** (bottom, fixed):
- A command input field (like a chat/terminal input)
- Two modes: prose (text input) and command (catalog picker)
- Toggle between modes with pill buttons
- Slash command detection → auto-execute
- Command catalog opens as a modal overlay

**Context Strip** (between narrative and command):
- Compact health/energy/stamina bars (horizontal)
- Turn state indicator (just an icon + color)
- Location name + time

### 3. Command Catalog Modal

When the player opens the command catalog:
- Full modal overlay with search field
- Categorized list of actions
- Selecting an action opens a detail form in the same modal
- Submit button to execute
- Back button to return to catalog
- Cancel to close entirely

### 4. Sidebar Reduction

Replace the 5-card sidebar with:
- A compact header strip at the top of the nav bar
- Realm icon, soul name, turn state icon
- Clicking reveals a dropdown with details
- Audio/settings accessible from nav bar

### 5. Animations & Wow Factor

- **Route transitions**: fade + slide using CSS transitions
- **Narrative typewriter**: character-by-character reveal (configurable speed)
- **Staggered list entry**: items appear with 50ms delay each
- **Ambient particles**: CSS-only ember/dust particles in background
- **Card hover**: subtle scale (1.02) + shadow depth increase
- **Loading skeletons**: shimmer effect for loading states
- **Toast notifications**: slide-in from top-right, auto-dismiss after 3s

### 6. Realm-Specific Theming

- **Mortal World**: Gold accent, candle ambient, health bars visible
- **Chaos Sea**: Purple accent, void ambient, soul resources instead of health
- **Shining Abode**: Teal accent, light ambient, radiance indicators
- Transition animation between realms

## Implementation Priority

1. Phase 1: Fix composer bug (#764) + add Vite proxy (#771) — unblocks testing
2. Phase 2: Compact nav bar + context strip — declutters screen
3. Phase 3: Command catalog modal + slash commands — core UX
4. Phase 4: Animations + particles — wow factor
5. Phase 5: Realm-specific theming — immersion

## Acceptance Criteria

- [ ] Composer actually sends commands to backend
- [ ] Command catalog opens as modal, selecting action shows detail form
- [ ] Sidebar reduced to a compact strip
- [ ] Route cards replaced with icon-only nav bar
- [ ] At least 3 animation types (route transition, typewriter, stagger)
- [ ] Toast notifications for game events
- [ ] Loading skeletons for data loading
