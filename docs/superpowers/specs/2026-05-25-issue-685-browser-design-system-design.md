# Issue #685 Browser Design System Design

Tracked issue: #685 — [Browser Client] Визуальная арт-дирекция и дизайн-система игрового клиента.

## Context

Issues #701-#705 established the maintainable Browser Client architecture: a Vite + React + TypeScript workspace under `BookOfEternityClient.WebFrontend/`, C# static asset serving, typed local API contracts, the first React app shell, and browser smoke/build verification. Issues #682-#684 added a player-facing game screen, contextual action menu, and audio/settings controls. The remaining #685 gap is visual coherence: the shell has useful panels, but the design rules still live in one broad `src/styles.css` file and the UI still reads more like a functional prototype than a deliberate dark-fantasy game client.

Stanislav authorized unattended execution, so this design is self-approved after review. The change is intentionally presentation-only: React/CSS owns visual structure, tokens, responsive layout, loading/alert states, and player-facing copy. C# remains the only authority for gameplay rules, saves, commands, validation, afterlife/mortal contracts, and local-write safety.

The old 0.9.14 TypeScript project was inspected as a UI/UX reference only. Useful lessons: a patterned dark background, readable narrative typography, card/panel hierarchy, tabbed sections, comfort settings, parchment-style map treatment, and graceful animation. Outdated prompts, mortal-life-only mechanics, and old game assumptions are not copied into Reborn.

## Approaches considered

1. **Pure CSS polish in the existing `styles.css`.** Fastest, but it preserves the maintenance problem named by #685: another large style blob with no token/component boundaries.
2. **Split design-system CSS into focused files and import them through `styles.css` (selected).** Add durable tokens, component primitives, route/layout styling, responsive/motion rules, and tests/docs that make the structure intentional. This satisfies the asset-structure criterion without introducing a separate UI library or moving gameplay logic.
3. **Adopt Tailwind or a component library.** Could accelerate visual iteration, but adds dependency/build complexity and risks fighting the current minimal Vite setup. YAGNI for this closure unit.

## Selected architecture

The Browser Client keeps `src/styles.css` as the public entrypoint imported by `main.tsx`, but turns it into an aggregator:

- `src/styles/tokens.css` — design tokens: color roles, realm accents, typography families, spacing, radii, shadows, state colors, motion durations, and reduced-motion defaults.
- `src/styles/base.css` — reset/base document styling, background texture, scrollbar treatment, accessible focus outlines, and global typography.
- `src/styles/components.css` — reusable primitives used across routes: shell panels, route cards, summary/narrative cards, buttons, forms, status pills, alerts, action cards, audio/settings controls, advanced diagnostics, and utility classes.
- `src/styles/layout.css` — shell layout, hero composition, route grid, workspace grid, route-specific visual sections, and responsive breakpoints.
- `src/styles/motion.css` — conservative animations for menu entrance, panel reveal, GM-waiting/QTE tension states, with `prefers-reduced-motion` guardrails.

`App.tsx` adds semantic design-system hooks rather than business logic: route descriptions remain Russian/player-facing, hero and route panels receive visual variants, and status panels expose `data-state`/class names that CSS can theme. The default UI must not show raw `/api/*`, slash IDs, or technical English; those remain behind `Расширенный режим`.

## Visual direction

The visual system should feel like The Book of Eternity rather than a generic dashboard:

- **Dark chronicle foundation:** deep ink/obsidian background, faint runic/book texture, warm parchment/gold text accents, and layered glass/stone panels.
- **Realm-aware theming:** existing C# `game.theme.accent` remains the dynamic accent. CSS tokens provide stable fallback roles for mortal world, Chaos Sea, Shining Abode, danger/repair, success, and QTE tension without hardcoding gameplay rules in React.
- **Narrative-first hierarchy:** the `Игра` route gives the narrative card stronger typography and spacing than diagnostics. Sidebar status and action panels support the story, not vice versa.
- **Comfortable route tabs:** navigation reads as game sections (`Главная`, `Игра`, `Душа`, `Мир`, `Медиа`, `Настройки`) with clear selected/focus states and no API jargon.
- **Responsive shell:** desktop keeps a broad main column plus side summary; tablet/mobile collapse into a single-column readable flow. Mobile route cards become compact tabs instead of six tall boxes.
- **Motion with restraint:** panels fade/slide in, waiting/QTE states pulse subtly, but `prefers-reduced-motion` disables nonessential animation.

## Testing strategy

1. RED: add a source guard test to `BrowserFrontendWorkspaceTests` that fails until the design-system CSS structure exists, `styles.css` imports it, and tokens include expected realm/state/motion/accessibility variables.
2. RED: add source/copy assertions that fail until the React shell exposes semantic design hooks and avoids default technical/debug wording.
3. GREEN: split and enhance CSS, then add the minimal `App.tsx` hooks/classes required for visual states and responsive/mobile design.
4. Verify with the focused source guard tests, `npm run verify --prefix BookOfEternityClient.WebFrontend`, browser local-web smoke categories, `git diff --check`, and broad relevant .NET tests when practical.

## Documentation impact

Update:

- `BookOfEternityClient.WebFrontend/README.md` with the #685 design-system file structure, visual rules, and verification commands.
- `docs/web-ui/local-web-host.md` tracked task list and Browser Frontend section with the design-system boundary.
- `docs/superpowers/plans/2026-05-25-issue-685-browser-design-system.md` for implementation handoff.

No Afterlife runtime contract, pending/control file, GM-authored prompt, or mortal-world mechanic change is planned. GM-facing afterlife/mortal documentation updates are not required for this visual Browser Client slice.

## Self-review

- Placeholder scan: no TBD/TODO placeholders.
- Consistency: C# remains runtime authority; React/CSS changes are presentation-only.
- Scope: one closure unit for #685 design-system structure and first visual pass; future issues #686-#689 keep their lifecycle/parity/media/settings feature depth.
- Ambiguity resolved: `styles.css` remains the import entrypoint for compatibility, but maintainable design assets live under `src/styles/`.
