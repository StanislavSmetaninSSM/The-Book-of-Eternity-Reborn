# Dark-Fantasy Theming, Realm Differentiation & Copy Robustness

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the browser client visually distinct per realm, apply the dark-fantasy design system comprehensively, and fix the fragile regex copy system that produces linguistic artifacts.

**Architecture:** Pure frontend changes. The C# backend already exposes `realmTheme.key` (e.g. `mortal-world`, `chaos-sea`, `shining-abode`), `isInAfterlifeRealm`, and related flags. CSS uses `[data-theme-key]` attribute selectors on `.browser-shell` to apply realm-specific styles. The `playerCopy.ts` module handles EN→RU localization of technical strings via sequential regex replacement.

**Tech Stack:** CSS custom properties, React (TypeScript), Vite, Vitest

---

## File Structure

| File | Responsibility |
|------|---------------|
| `src/utils/playerCopy.ts` | Robust copy replacement system (Task 1) |
| `test/playerCopyRobustness.test.ts` | Regression tests for copy edge cases (Task 1) |
| `src/styles/tokens.css` | Add missing semantic aliases (Task 3) |
| `src/styles/realms.css` | NEW — realm-specific mood/ambient styles (Task 2) |
| `src/styles/components.css` | Reconcile hardcoded fallbacks to tokens (Task 3) |
| `src/styles.css` | Import realms.css (Task 2) |
| `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` | Guard test updates (Task 4) |

---

### Task 1: Fix playerCopy regex fragility (#767)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/utils/playerCopy.ts`
- Create: `BookOfEternityClient.WebFrontend/test/playerCopyRobustness.test.ts`

**Problem analysis:**
The current regex list has these bugs:
1. `/\bby\b/gi` (line 24) replaces ALL "by" in text with "из-за" — e.g. "Pass by the gate" → "Pass из-за the gate"
2. `/\baction\b/gi` (line 48) replaces normal word "action" everywhere
3. `/\bresolved\b/gi` (line 49) — same, too broad
4. `/\brepair\b/gi` (line 50) — conflicts with compound "repair pending turn" (line 13)
5. `/\bUI\b/g` (line 47) — case-sensitive but over-broad
6. Order-dependent: compound patterns must fire before their sub-patterns
7. Grammatical artifacts: "blocked by" → "заблокировано из-за", then standalone "blocked" could re-match

**Solution:** Split into compound phrases (always match first) and technical-only terms (only match in clearly technical context). Remove dangerous broad patterns entirely. Add `containsTechnicalContext()` guard for borderline terms.

- [ ] **Step 1: Write failing regression tests**

Create `BookOfEternityClient.WebFrontend/test/playerCopyRobustness.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { toPlayerFacingText, sanitizePlayerMessage } from '../src/utils/playerCopy';

describe('playerCopy robustness', () => {
  it('does not mangle normal narrative text', () => {
    const narrative = 'You pass by the ancient gate. The hero resolved to act by sunrise.';
    const result = toPlayerFacingText(narrative, 'fallback');
    expect(result).not.toContain('из-за');
    expect(result).not.toContain('действие');
    expect(result).not.toContain('завершена');
    expect(result).toContain('pass');
    expect(result).toContain('gate');
  });

  it('still translates compound technical phrases', () => {
    const technical = 'repair pending turn blocked by validation';
    const result = toPlayerFacingText(technical, 'fallback');
    expect(result).toContain('починка ожидающего хода');
    expect(result).toContain('заблокировано');
    expect(result).toContain('проверка');
  });

  it('translates realm names consistently', () => {
    const text = 'You are in Chaos Sea. The Shining Abode awaits.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('Море Хаоса');
    expect(result).toContain('Сияющая Обитель');
  });

  it('translates GM terminology', () => {
    const text = 'Waiting for GM-turn. The GM will respond.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('ход ГМа');
    expect(result).toContain('ГМ');
  });

  it('handles debug shell replacement without mangling', () => {
    const text = 'Use the debug shell for diagnostics.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('служебная оболочка');
    expect(result).not.toContain('debug shell');
  });

  it('does not replace "by" as standalone word', () => {
    const text = 'Stand by the door. Crafted by the smith.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('by the door');
    expect(result).toContain('by the smith');
  });

  it('does not replace "action" in narrative context', () => {
    const text = 'Take action against the darkness.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('action');
  });

  it('preserves sanitizePlayerMessage behavior for file paths', () => {
    const text = 'Error in game_state/meta/soul_state.json — repair needed';
    const { safe, hasTechnical } = sanitizePlayerMessage(text, 'fallback');
    expect(hasTechnical).toBe(true);
    expect(safe).not.toContain('soul_state.json');
  });
});
```

- [ ] **Step 2: Run tests — expect failures**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: Tests fail on "does not mangle normal narrative text", "does not replace 'by'", "does not replace 'action'"

- [ ] **Step 3: Rewrite playerCopy.ts with robust replacement strategy**

Replace the `playerCopyReplacements` array in `src/utils/playerCopy.ts` with a two-tier system:

```typescript
/**
 * Tier 1: Compound phrases — matched first, safe because they are specific multi-word patterns.
 * These will never accidentally match single words in narrative text.
 */
const compoundReplacements: Array<[RegExp, string]> = [
  [/\bMortal World\b/gi, 'Мир смертных'],
  [/\bChaos Sea\b/gi, 'Море Хаоса'],
  [/\bShining Abode\b/gi, 'Сияющая Обитель'],
  [/QTE action resolved\.?/gi, 'Быстрая сцена завершена.'],
  [/\bGM[- ]?turn\b/g, 'ход ГМа'],
  [/\bdebug shell\b/gi, 'служебная оболочка'],
  [/Slash-команды/gi, 'служебные команды'],
  [/\bslash commands?\b/gi, 'служебные команды'],
  [/Нужен repair pending turn/gi, 'Нужна починка ожидающего хода'],
  [/repair pending turn/gi, 'починка ожидающего хода'],
  [/нужен repair/gi, 'нужна починка'],
  [/\bpending[- ]turn\b/gi, 'ожидающий ход'],
  [/\bturn[- ]writer\b/gi, 'запись хода'],
  [/\bBrowser[- ]write\b/gi, 'запись из браузера'],
  [/\bbrowser write\b/gi, 'запись из браузера'],
  [/\blocal[- ]write\b/gi, 'локальная запись'],
  [/\bprompt[- ]session\b/gi, 'игровая форма'],
  [/\bblocked by\b/gi, 'заблокировано из-за'],
  [/\bSpectre\.Console\b/g, 'консольный интерфейс'],
  [/state\/contract/gi, 'файлы состояния и контракта'],
  [/snapshot artifact/gi, 'снимок состояния'],
  [/game_state\/meta\/soul_state\.json/gi, 'файл души'],
  [/soul_state\.json/gi, 'файл души'],
  [/repair\/validation/gi, 'починка и проверка'],
  [/UI-блокировка/gi, 'блокировка интерфейса'],
  [/\bBrowser Client\b/gi, 'браузерный клиент'],
  [/\bsound-notification\b/gi, 'звуковая подсказка'],
  [/локальный запись хода/gi, 'локальную запись хода'],
  [/тот же локальную/gi, 'ту же локальную'],
];

/**
 * Tier 2: Single technical terms — only safe in clearly technical/system messages.
 * These are NOT applied to narrative/player text from the GM.
 */
const technicalTermReplacements: Array<[RegExp, string]> = [
  [/\bGM\b/g, 'ГМ'],
  [/\bQTE\b/g, 'быстрая сцена'],
  [/\brollback\b/gi, 'откат'],
  [/\bblocked\b/gi, 'заблокировано'],
  [/\bgame_session\b/gi, 'сохранение игры'],
  [/\bwrite-flow\b/gi, 'запись хода'],
  [/\bmanual_saves\b/gi, 'ручные сохранения'],
  [/\bautosaves\b/gi, 'автосохранения'],
  [/--web\b/g, 'браузерный режим'],
  [/\bsnapshot\b/gi, 'снимок'],
  [/\bgame_state\b/gi, 'папка состояния игры'],
  [/\bvalidation\b/gi, 'проверка'],
  [/\blifecycle\b/gi, 'состояние хода'],
  [/\bruntime\b/gi, 'игровой слой'],
  [/\bendpoints?\b/gi, 'разделы локального интерфейса'],
  [/\bAPI\b/g, 'локальный интерфейс'],
  [/\bDTO\b/g, 'данные интерфейса'],
  [/\bNPC\b/g, 'персонажи мира'],
];

export const playerCopyReplacements: Array<[RegExp, string]> = [
  ...compoundReplacements,
  ...technicalTermReplacements,
];
```

**Key changes:**
- REMOVED: `/\bby\b/gi` — too dangerous, "blocked by" compound covers the needed case
- REMOVED: `/\baction\b/gi` — too broad for narrative text
- REMOVED: `/\bresolved\b/gi` — covered by "QTE action resolved" compound
- REMOVED: `/\brepair\b/gi` — covered by compound patterns
- REMOVED: `/\boffer\b/gi` — too broad ("I offer you...")
- REMOVED: `/\bartifact\b/gi` — too broad (game items can be artifacts)
- REMOVED: `/\brealm\b/gi` — too broad (narrative uses "realm" naturally)
- REMOVED: `/\bUI\b/g` — too broad, already have "UI-блокировка" compound
- REMOVED: `C\x23\s*` — was removing "C#" which can appear in "about" text legitimately
- KEPT separately: All compound phrases (safe, multi-word, unambiguous)
- KEPT: Technical identifiers (`game_session`, `manual_saves`, `autosaves`, `--web`, `DTO`, `API`) — these only appear in system messages

Also keep `launcherAboutCopyReplacements`, `containsTechnicalDetails`, and `sanitizePlayerMessage` unchanged — they work correctly.

- [ ] **Step 4: Run tests — expect pass**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix(browser): remove dangerous broad regex from playerCopy

Remove /\bby\b/, /\baction\b/, /\bresolved\b/, /\brepair\b/,
/\boffer\b/, /\bartifact\b/, /\brealm\b/, /\bUI\b/ which mangled
normal narrative text. Compound phrases still match correctly.
Split into compound (safe) and technical-term (system-only) tiers.

Closes #767"
```

---

### Task 2: Realm-specific mood differentiation (#765)

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/styles/realms.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles.css` (add import)
- Modify: `BookOfEternityClient.WebFrontend/src/styles/layout.css` (move realm selectors)

**Context:** The `.browser-shell` element already has `data-theme-key` set from `realmTheme.key`. Current values: `mortal-world`, `chaos-sea`, `shining-abode`, `shining-abode-pending`. Only `--realm-accent` color changes currently. We need distinct visual moods.

- [ ] **Step 1: Create realms.css with realm-specific ambient styles**

Create `BookOfEternityClient.WebFrontend/src/styles/realms.css`:

```css
/* ══════════════════════════════════════════════════════════════
   Realm-specific visual moods
   Applied via [data-theme-key] on .browser-shell
   ══════════════════════════════════════════════════════════════ */

/* ── Mortal World (default) ── */
.browser-shell[data-theme-key='mortal-world'] {
  --realm-accent: var(--realm-mortal);
  --realm-mist: color-mix(in srgb, var(--color-ash) 60%, transparent);
  --realm-ember-color: var(--color-gold-dim);
  --realm-glow-spread: 28rem;
}

/* ── Chaos Sea ── */
.browser-shell[data-theme-key*='chaos'] {
  --realm-accent: var(--realm-chaos);
  --realm-mist: color-mix(in srgb, #1a0a2e 55%, transparent);
  --realm-ember-color: #9b6fd4;
  --realm-glow-spread: 36rem;
}

.browser-shell[data-theme-key*='chaos'] .browser-shell::before {
  background:
    radial-gradient(1.5px 1.5px at 15% 25%, var(--realm-chaos), transparent),
    radial-gradient(1px 1px at 45% 75%, #6b4fa4, transparent),
    radial-gradient(2px 2px at 75% 15%, var(--realm-chaos), transparent),
    radial-gradient(1px 1px at 85% 65%, #6b4fa4, transparent),
    radial-gradient(1.5px 1.5px at 25% 85%, var(--realm-chaos), transparent),
    radial-gradient(1px 1px at 55% 35%, #6b4fa4, transparent),
    radial-gradient(1px 1px at 95% 45%, var(--realm-chaos), transparent);
  opacity: 0.5;
}

.browser-shell[data-theme-key*='chaos'] body::after,
[data-theme-key*='chaos'] > .workspace-grid::after {
  content: '';
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 30vh;
  pointer-events: none;
  z-index: -1;
  background: linear-gradient(to top, var(--realm-mist), transparent);
}

/* Chaos realm: border vignette effect */
.browser-shell[data-theme-key*='chaos'] .shell-panel,
.browser-shell[data-theme-key*='chaos'] .summary-card,
.browser-shell[data-theme-key*='chaos'] .composer-form {
  border-color: color-mix(in srgb, var(--realm-chaos) 28%, rgba(255, 255, 255, 0.06));
}

.browser-shell[data-theme-key*='chaos'] h2 {
  color: color-mix(in srgb, var(--realm-chaos) 60%, var(--color-parchment));
}

/* ── Shining Abode ── */
.browser-shell[data-theme-key*='shining'],
.browser-shell[data-theme-key*='abode'] {
  --realm-accent: var(--realm-shining);
  --realm-mist: color-mix(in srgb, #0a1e1a 50%, transparent);
  --realm-ember-color: #6dcfb8;
  --realm-glow-spread: 32rem;
}

.browser-shell[data-theme-key*='shining'] .shell-panel,
.browser-shell[data-theme-key*='abode'] .shell-panel,
.browser-shell[data-theme-key*='shining'] .summary-card,
.browser-shell[data-theme-key*='abode'] .summary-card,
.browser-shell[data-theme-key*='shining'] .composer-form,
.browser-shell[data-theme-key*='abode'] .composer-form {
  border-color: color-mix(in srgb, var(--realm-shining) 24%, rgba(255, 255, 255, 0.06));
}

.browser-shell[data-theme-key*='shining'] h2,
.browser-shell[data-theme-key*='abode'] h2 {
  color: color-mix(in srgb, var(--realm-shining) 55%, var(--color-parchment));
}

/* Shining Abode: serene radial glow replacing harsh ember particles */
.browser-shell[data-theme-key*='shining']::before,
.browser-shell[data-theme-key*='abode']::before {
  background:
    radial-gradient(2px 2px at 20% 30%, rgba(109, 207, 184, 0.4), transparent),
    radial-gradient(1.5px 1.5px at 50% 70%, rgba(109, 207, 184, 0.3), transparent),
    radial-gradient(2px 2px at 80% 20%, rgba(109, 207, 184, 0.35), transparent),
    radial-gradient(1px 1px at 35% 80%, rgba(109, 207, 184, 0.25), transparent),
    radial-gradient(1.5px 1.5px at 65% 50%, rgba(109, 207, 184, 0.3), transparent);
  animation: ember-drift 16s ease-in-out infinite alternate;
  opacity: 0.4;
}

/* ── Realm badge in NavBar ── */
.realm-badge {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-sm);
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--realm-accent);
  background: color-mix(in srgb, var(--realm-accent) 10%, transparent);
  border: 1px solid color-mix(in srgb, var(--realm-accent) 24%, transparent);
  animation: candle-flicker 4s ease-in-out infinite;
}

/* ── Afterlife indicator (shared between Chaos Sea and Shining Abode) ── */
.browser-shell[data-theme-key*='chaos'] .realm-badge,
.browser-shell[data-theme-key*='shining'] .realm-badge,
.browser-shell[data-theme-key*='abode'] .realm-badge {
  box-shadow: 0 0 0.6rem color-mix(in srgb, var(--realm-accent) 30%, transparent);
}
```

- [ ] **Step 2: Import realms.css in the stylesheet**

Check how styles are imported. The entry is `src/styles.css` or directly in `App.tsx`. Add the import so `realms.css` loads after `layout.css`:

In `BookOfEternityClient.WebFrontend/src/styles.css` (or create equivalent import), ensure:
```css
@import './styles/tokens.css';
@import './styles/base.css';
@import './styles/layout.css';
@import './styles/realms.css';
@import './styles/components.css';
@import './styles/motion.css';
```

If `styles.css` doesn't exist and `App.tsx` imports them individually, add `import './styles/realms.css'` in `App.tsx` after the layout import.

- [ ] **Step 3: Update NavBar to use realm-badge class**

In `BookOfEternityClient.WebFrontend/src/components/NavBar.tsx`, wrap the realm indicator with the `.realm-badge` class:

```tsx
<span className="realm-badge">{realmTheme.icon} {realmTheme.label}</span>
```

- [ ] **Step 4: Remove duplicate realm selectors from layout.css**

Lines 30-37 of `layout.css` already have realm accent selectors. Remove them since `realms.css` now owns this:

Remove from `layout.css`:
```css
.browser-shell[data-theme-key*='chaos'] {
  --realm-accent: var(--realm-chaos);
}

.browser-shell[data-theme-key*='shining'],
.browser-shell[data-theme-key*='abode'] {
  --realm-accent: var(--realm-shining);
}
```

- [ ] **Step 5: Run verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: All pass

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(browser): add realm-specific visual moods for afterlife differentiation

Chaos Sea gets purple mist, violet particles, and purple-tinted borders.
Shining Abode gets teal glow, serene particles, and aqua-tinted borders.
NavBar realm indicator now uses .realm-badge with ambient glow.
Realm accent variable assignment moved to dedicated realms.css.

Closes #765"
```

---

### Task 3: Reconcile token usage and complete theming (#758)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/tokens.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

**Problem:** Components.css lines 652-696 and 833-892 (ActionPalette, Sidebar sections) use variables like `--border-subtle`, `--surface-subtle`, `--surface-elevated`, `--text-muted`, `--text-primary`, `--surface-active`, `--accent-gold` that are NOT defined in tokens.css. They fall back to hardcoded hex values (#2a2a3e, #151528, #1a1a2e, #8a8a9a, #f0e8d8, #2a2a50, #d8b36a) that don't match the dark-fantasy palette.

- [ ] **Step 1: Add semantic token aliases to tokens.css**

Add after the shadows section in `tokens.css`:

```css
  /* ── Semantic surface tokens (for components using var with fallback) ── */
  --border-subtle: color-mix(in srgb, var(--realm-accent) 16%, rgba(255, 255, 255, 0.06));
  --surface-base: var(--color-ink-2);
  --surface-subtle: var(--color-obsidian);
  --surface-elevated: var(--color-obsidian-2);
  --surface-active: color-mix(in srgb, var(--realm-accent) 12%, var(--color-obsidian-2));
  --text-primary: var(--color-parchment);
  --text-muted: var(--color-mist);
  --accent-gold: var(--color-gold);
```

- [ ] **Step 2: Remove hardcoded fallback hex values from components.css**

Replace all instances of these patterns in components.css:
- `var(--border-subtle, #2a2a3e)` → `var(--border-subtle)`
- `var(--surface-subtle, #151528)` → `var(--surface-subtle)`
- `var(--surface-elevated, #1a1a2e)` → `var(--surface-elevated)`
- `var(--text-muted, #8a8a9a)` → `var(--text-muted)`
- `var(--text-primary, #f0e8d8)` → `var(--text-primary)`
- `var(--surface-active, #2a2a50)` → `var(--surface-active)`
- `var(--accent-gold, #d8b36a)` → `var(--accent-gold)`
- `var(--realm-accent, var(--accent-gold, #d8b36a))` → `var(--realm-accent, var(--accent-gold))`

- [ ] **Step 3: Run verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(browser): define semantic tokens and remove hardcoded fallback colors

Add --border-subtle, --surface-subtle, --surface-elevated,
--surface-active, --text-primary, --text-muted, --accent-gold
to tokens.css using the dark-fantasy palette. Remove hex fallbacks
from components.css so all colors flow from the design system.

Closes #758"
```

---

### Task 4: Guard tests + final verification + PR

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` (if needed)
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx` (contract manifest if needed)
- Modify: `tsconfig.player-facing-tests.json` (add new test file)

- [ ] **Step 1: Ensure new test file is included in tsconfig**

Add `"test/playerCopyRobustness.test.ts"` to `tsconfig.player-facing-tests.json` include array.

- [ ] **Step 2: Run full frontend verification**

Run: `npm run verify --prefix BookOfEternityClient.WebFrontend`
Expected: typecheck + vitest + build all pass

- [ ] **Step 3: Run C# guard tests**

Run: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"`
Expected: 72 tests pass

- [ ] **Step 4: Fix any guard test assertions that reference removed patterns**

If C# tests assert the presence of removed regex patterns (like `/\bby\b/gi`), update them. Check for assertions referencing old playerCopy patterns in the App.tsx contract manifest and update accordingly.

- [ ] **Step 5: Push and create PR**

```bash
git push origin feat/browser-theming-realm-copy
gh pr create --title "feat(browser): dark-fantasy theming, realm moods, robust copy" \
  --body "## Summary

Fourth wave of browser client UX: comprehensive theming, realm differentiation, and copy robustness.

### Changes

1. **playerCopy regex fix** — removed dangerous broad patterns (/by/, /action/, /resolved/, etc.) that mangled narrative text. Split into compound (safe) and technical-term tiers. (#767)
2. **Realm-specific moods** — Chaos Sea gets purple mist/particles, Shining Abode gets teal glow/serenity. NavBar realm-badge with ambient effect. (#765)
3. **Token reconciliation** — defined semantic tokens (--border-subtle, --surface-elevated, etc.) and removed hardcoded hex fallbacks from all components. (#758)

### Testing
- Frontend: typecheck + vitest + vite build pass
- New regression tests for playerCopy robustness
- C# guard tests pass

Closes #758
Closes #765
Closes #767" --base main
```

- [ ] **Step 6: Commit if any guard test fixes were needed**

```bash
git add -A
git commit -m "fix(tests): align guard tests with theming and copy changes"
```
