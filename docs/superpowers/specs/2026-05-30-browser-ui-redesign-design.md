# Browser Client UI Redesign — Command-Driven Interface

**Date:** 2026-05-30  
**Status:** Approved  
**Issue:** #762 (partial), general UX overhaul

## Problem Statement

The current browser client has 8 navigation tabs (Home, Game, Soul, World, Journal, Inventory, Media, Settings) with mostly empty or non-functional content. The "Actions" toggle does nothing, clicking "open section" shows "game action performed" without displaying results, and the player cannot access the 60+ commands that the console client offers. The UI is a technical debug menu rather than a playable game interface.

## Design Decision

Replace the multi-tab debug layout with a **command-driven hybrid interface**: minimal tabs at the top, single content area, and commands executed via text input or clickable help catalog. The backend Explorer API already supports all 60+ commands with structured UI block responses — the frontend just needs to properly render them.

## Architecture

### 4 Tabs

| Tab | Purpose | Content |
|-----|---------|---------|
| 📖 Сцена | Primary gameplay | Narrative, dialogues, quick actions, command results |
| 📊 Статус | Character snapshot | Key stats, HP, energy, location, effects (auto-rendered via `/status` command) |
| ❓ Помощь | Command catalog | Search + categorized clickable commands |
| ⚙️ Настройки | Client config | Sound, language, font size, advanced mode toggle |

### Content Flow

```
┌─────────────────────────────────────────────────┐
│  📖 Сцена │ 📊 Статус │ ❓ Помощь │ ⚙️ Настройки   │  ← tabs
├─────────────────────────────────────────────────┤
│                                                 │
│   [← Назад к сцене]  (shown after command)     │
│                                                 │
│   ┌─────────────────────────────────────┐       │
│   │                                     │       │
│   │   Content Area                      │       │
│   │   - Narrative + quick actions       │       │
│   │   - OR command result (replaces)    │       │
│   │                                     │       │
│   └─────────────────────────────────────┘       │
│                                                 │
├─────────────────────────────────────────────────┤
│  [Опишите действие или введите /команду...]  [↵]│  ← input
└─────────────────────────────────────────────────┘
```

### Interaction Model

1. **Default state (Scene tab):** Shows narrative text, dialogue blocks, GM quick actions as clickable chips.
2. **Player types prose** (no `/`): Submits as narrative action via `POST /api/explorer/player-action`.
3. **Player types `/command`**: Executes via `POST /api/explorer/command`. Result replaces content area. "Back to scene" button restores narrative.
4. **Player clicks command in Help tab**: Same as typing it — executes, switches to Scene tab showing result.
5. **Player clicks quick action chip**: Submits as prose action (same as typing it).
6. **New GM turn arrives**: Auto-restores narrative view with fresh content.

### Input Behavior

- Single textarea at the bottom, always visible on all tabs
- `/` prefix → command mode (show autocomplete dropdown with matching commands)
- No prefix → prose mode (free text action)
- Enter or button to submit
- Placeholder: `«Опишите действие или введите /команду...»`

### Help Tab Structure

```
┌─────────────────────────────────────┐
│ 🔍 Поиск команды...                │  ← instant filter
├─────────────────────────────────────┤
│ ▾ 🎭 Персонаж                      │
│   /статус — Подробный статус     [▶]│
│   /навыки — Навыки и умения      [▶]│
│   /статы — Характеристики        [▶]│
│   /эффекты — Эффекты, раны       [▶]│
│                                     │
│ ▸ 🗺️ Мир (8 команд)               │
│ ▸ 🎒 Инвентарь (4 команды)         │
│ ▸ 📜 Квесты и сюжет (5 команд)    │
│ ▸ 🕯️ Душа и метапрогресс (7)      │
│ ▸ 🌊 Море Хаоса (9 команд)        │
│ ▸ ✨ Сияющая Обитель (4 команды)   │
│ ▸ ⚔️ Духовный бой (8 команд)      │
│ ▸ 🔧 Системные (5 команд)         │
└─────────────────────────────────────┘
```

- Categories derived from `ExplorerCommandCatalog` groups
- Each command shows: alias (RU), one-line description, [▶] execute button
- Click on command → execute → switch to Scene tab → show result
- Search filters by alias and description text (client-side, instant)

### Command Result Rendering

The backend already returns structured `ExplorerCommandResult` with polymorphic `UiBlock` types. The frontend must render each block type:

| Block Kind | Render As |
|-----------|-----------|
| `text` | Paragraph with tone-based color (default/muted/accent/success/warning/error) |
| `panel` | Titled card with border, containing nested blocks |
| `table` | HTML table with sortable columns |
| `keyValueGrid` | Two-column key: value layout |
| `list` | Ordered/unordered list |
| `message` | Alert banner with severity icon + color |
| `image` | Image element with mediaId → `/api/media/{id}` |
| `map` | Interactive map component (nodes, links, regions) |
| `rawJson` | Collapsible code block (advanced mode only) |

Additionally:
- `Actions` from result → render as clickable chips below the content (execute on click)
- `Prompts` from result → render as inline form (selection dropdowns, text inputs, confirmations)
- `InteractiveSession` → multi-step form with submit/cancel

### Status Tab

Auto-executes `/status` equivalent on tab switch. Displays:
- Character name, race, class
- HP / Energy / Mana bars
- Current location
- Active effects
- Key resources (gold, feathers, etc.)

This is a convenience shortcut — same data as `/статус` command but always one click away.

### Settings Tab

Migrates existing settings functionality:
- Language selection
- Sound volume / music toggle
- Font size
- Advanced mode toggle (shows raw JSON blocks, lifecycle dashboard)
- Audio settings

### Components to Remove

The following existing components become obsolete:
- `Sidebar.tsx` (left navigation with 8 routes)
- `navBarConfig.ts` (8-route configuration)
- `HomeRoute.tsx`, `SoulRoute.tsx`, `WorldRoute.tsx`, `JournalRoute.tsx`, `InventoryRoute.tsx`, `MediaRoute.tsx` (empty/stub routes)
- `ActionPalette.tsx` (non-functional action list in composer)
- `FilteredActionSections` pattern (per-route action filtering)
- `PlayerStatusSidebar.tsx` (right sidebar — absorbed into Status tab)

### Components to Create/Rewrite

| Component | Purpose |
|-----------|---------|
| `TabBar.tsx` | 4 horizontal tabs with active indicator |
| `SceneView.tsx` | Narrative + dialogue + quick actions (replaces GameRoute) |
| `CommandResultView.tsx` | Renders ExplorerCommandResult blocks (rewrite of CommandResult.tsx) |
| `HelpView.tsx` | Searchable categorized command catalog |
| `StatusView.tsx` | Character status summary |
| `SettingsView.tsx` | Consolidated settings (existing code, new layout) |
| `UnifiedInput.tsx` | Bottom input bar with autocomplete |
| `CommandAutocomplete.tsx` | Dropdown showing matching commands when typing `/` |
| `BlockRenderer.tsx` | Recursive renderer for UiBlock types |
| `PromptForm.tsx` | (reuse/improve existing) Interactive form for command prompts |

### Data Flow

```
App
└─ ShellProvider (global state: game data, active tab, command result)
   └─ AppShell
      ├─ TabBar (4 tabs)
      ├─ ContentArea
      │  ├─ SceneView (tab=scene, no command result)
      │  ├─ CommandResultView (tab=scene, has command result)
      │  ├─ StatusView (tab=status)
      │  ├─ HelpView (tab=help)
      │  └─ SettingsView (tab=settings)
      └─ UnifiedInput (always visible)
         └─ CommandAutocomplete (shown when input starts with /)
```

State additions to ShellContext:
- `activeTab: 'scene' | 'status' | 'help' | 'settings'`
- `commandResult: ExplorerCommandResult | null` (current command output)
- `commandHistory: string[]` (for up-arrow recall)
- `isCommandView: boolean` (showing result vs narrative)

### API Usage

No new backend endpoints needed. Existing endpoints used:

| Action | Endpoint |
|--------|----------|
| Load game data | `GET /api/game-screen` |
| Execute command | `POST /api/explorer/command` |
| Submit form | `POST /api/explorer/prompt-sessions/submit` |
| Cancel form | `POST /api/explorer/prompt-sessions/cancel` |
| Submit prose action | `POST /api/explorer/player-action` |
| Get command list | `GET /api/explorer/command-coverage` |
| Settings CRUD | `GET/POST /api/client/settings` |
| Audio CRUD | `GET/POST /api/audio/settings` |

### Visual Design

- Dark theme (existing: `#0a0e12` background, `#161b22` cards, `#30363d` borders)
- Cinematic feel preserved from recent redesign
- Narrative text: large, readable (14-16px), good line-height
- Command results: compact, structured (tables, grids)
- Quick actions: pill/chip buttons in muted style
- Input: always at bottom, clean single line

### Responsiveness

- Desktop: max-width 800px centered content
- Tablet: full-width with padding
- Mobile: tabs become scrollable, content stacks vertically

## Out of Scope

- Map interactive viewer (render as static for now, use existing `UiMapBlock` data)
- Media gallery / QTE system (can be added later as commands)
- Audio player integration (stays in settings)
- Console client changes

## Success Criteria

1. Player can execute any of the 60+ commands from the browser and see formatted results
2. No dead buttons or non-functional UI elements
3. Help tab shows all available commands organized by category with search
4. Narrative text and quick actions always accessible via Scene tab
5. Command results properly render all UiBlock types (text, table, panel, list, keyValue, message)
6. Interactive command sessions (prompts) work end-to-end
7. `/` autocomplete suggests matching commands as user types
