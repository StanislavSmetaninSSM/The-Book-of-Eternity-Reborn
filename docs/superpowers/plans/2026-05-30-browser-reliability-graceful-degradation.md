# Browser Client Reliability — Graceful Degradation & Clear States

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the browser client resilient to API failures, hide technical diagnostics from players, and clearly separate "waiting for GM" from "validation/repair needed" states.

**Architecture:** Replace `Promise.all` with `Promise.allSettled` so partial UI renders when some calls fail. Add a connection banner with retry. Sanitize prompt-form error messages through `toPlayerFacingText`. Restructure GameRoute turn status to show one clear player-facing state instead of mixing lifecycle labels.

**Tech Stack:** React 18, TypeScript, Vite, existing `browserApi` client, existing `toPlayerFacingText` sanitizer.

---

## File Structure

```
src/
├── hooks/
│   └── useShellState.ts             (MODIFY — Promise.allSettled + partial state)
├── components/
│   ├── ConnectionBanner.tsx          (CREATE — offline/reconnect UI)
│   ├── ActionCard.tsx                (MODIFY — sanitize form error messages)
│   └── CommandResult.tsx             (MODIFY — hide technical details by default)
├── routes/
│   └── GameRoute.tsx                 (MODIFY — clear turn state presentation)
├── context/
│   └── ShellContext.tsx              (MODIFY — add connectionStatus to context)
├── styles/
│   └── components.css               (MODIFY — connection banner + turn state styles)
test/
├── gracefulDegradation.test.ts       (CREATE — tests for partial load + sanitization)
```

---

## Task 1: Replace Promise.all with Promise.allSettled for partial rendering

**Files:**
- Modify: `src/hooks/useShellState.ts`
- Modify: `src/context/ShellContext.tsx` (add `connectionStatus` field)

- [ ] **Step 1: Update ShellContext types**

Add `connectionStatus` to the context and shell state:

```typescript
// In ShellContext.tsx, add to ShellContextValue interface:
connectionStatus: 'connected' | 'partial' | 'disconnected';
```

Add to the `BrowserShellState` ready variant:
```typescript
| {
    status: 'ready';
    connectionStatus: 'connected' | 'partial';
    menu: BrowserApiResult<BrowserMainMenuDto>;
    session: BrowserApiResult<BrowserSessionStatus>;
    game: BrowserApiResult<BrowserGameScreenDto>;
    audio: BrowserApiResult<BrowserAudioSettingsDto>;
    settings: BrowserApiResult<BrowserClientSettingsDto>;
    lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> | null;
    commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null;
  }
```

- [ ] **Step 2: Rewrite useShellState to use Promise.allSettled**

Replace `src/hooks/useShellState.ts` with:

```typescript
import { useCallback, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserApiResult, BrowserApiFailure } from '../api/contracts';
import type { BrowserShellState } from '../context/ShellContext';

function settledToResult<T>(outcome: PromiseSettledResult<BrowserApiResult<T>>): BrowserApiResult<T> {
  if (outcome.status === 'fulfilled') {
    return outcome.value;
  }

  const message = outcome.reason instanceof Error ? outcome.reason.message : 'Network request failed.';
  return {
    ok: false,
    status: null,
    kind: 'network-error',
    message,
    playerMessage: 'Локальный игровой клиент сейчас недоступен.',
    technicalDetails: message
  } satisfies BrowserApiFailure;
}

export function useShellState(advancedEnabled: boolean) {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });

  const loadBrowserState = useCallback(async () => {
    setShellState((prev) => prev.status === 'ready' ? prev : { status: 'loading' });

    const results = await Promise.allSettled([
      browserApi.getMainMenu(),
      browserApi.getSessionStatus(),
      browserApi.getGameScreen(),
      browserApi.getAudioSettings(),
      browserApi.getClientSettings()
    ]);

    const [menu, session, game, audio, settings] = results.map(settledToResult) as [
      BrowserApiResult<any>, BrowserApiResult<any>, BrowserApiResult<any>,
      BrowserApiResult<any>, BrowserApiResult<any>
    ];

    const allFailed = [menu, session, game, audio, settings].every(r => !r.ok && r.kind === 'network-error');

    if (allFailed) {
      setShellState({
        status: 'error',
        playerMessage: 'Локальный игровой клиент недоступен. Убедитесь, что клиент запущен.',
        technicalDetails: (menu as BrowserApiFailure).message
      });
      return;
    }

    const anyFailed = [menu, session, game, audio, settings].some(r => !r.ok && r.kind === 'network-error');

    let lifecycle = null;
    let commandCoverage = null;
    if (advancedEnabled) {
      const advResults = await Promise.allSettled([
        browserApi.getLifecycleDashboard(),
        browserApi.getCommandCoverage()
      ]);
      lifecycle = settledToResult(advResults[0]);
      commandCoverage = settledToResult(advResults[1]);
    }

    setShellState({
      status: 'ready',
      connectionStatus: anyFailed ? 'partial' : 'connected',
      menu, session, game, audio, settings,
      lifecycle, commandCoverage
    });
  }, [advancedEnabled]);

  return { shellState, loadBrowserState };
}
```

- [ ] **Step 3: Update ShellContext provider to pass connectionStatus**

In the `ShellProvider` component, derive `connectionStatus` from `shellState` and pass it through context:

```typescript
const connectionStatus: 'connected' | 'partial' | 'disconnected' =
  shellState.status === 'ready' ? shellState.connectionStatus :
  shellState.status === 'error' ? 'disconnected' : 'connected';
```

- [ ] **Step 4: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(browser): use Promise.allSettled for partial UI rendering

Individual API failures no longer block the entire shell. Partial
state renders what succeeded, shows error notices for what failed.
All-failed state shows reconnect message.

Refs #770

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 2: Add ConnectionBanner with retry

**Files:**
- Create: `src/components/ConnectionBanner.tsx`
- Modify: `src/App.tsx` (render banner)
- Modify: `src/styles/components.css` (banner styles)

- [ ] **Step 1: Create ConnectionBanner component**

```typescript
// src/components/ConnectionBanner.tsx
import { useShell } from '../context/ShellContext';

export function ConnectionBanner() {
  const { connectionStatus, loadBrowserState } = useShell();

  if (connectionStatus === 'connected') {
    return null;
  }

  const isDisconnected = connectionStatus === 'disconnected';
  const message = isDisconnected
    ? 'Клиент недоступен. Проверьте, что игра запущена.'
    : 'Некоторые разделы не загрузились. Часть данных может быть неактуальна.';

  return (
    <div className={`connection-banner ${isDisconnected ? 'is-disconnected' : 'is-partial'}`} role="alert">
      <span>{message}</span>
      <button type="button" onClick={() => void loadBrowserState()}>
        Повторить подключение
      </button>
    </div>
  );
}
```

- [ ] **Step 2: Add banner styles**

Append to `src/styles/components.css`:

```css
/* ── Connection banner ── */

.connection-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-sm);
  padding: var(--space-xs) var(--space-md);
  font-size: 0.85rem;
  border-radius: var(--radius-md);
  margin-bottom: var(--space-sm);
}

.connection-banner.is-partial {
  background: color-mix(in srgb, var(--color-gold-bright) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-gold-bright) 30%, transparent);
  color: var(--text-primary);
}

.connection-banner.is-disconnected {
  background: color-mix(in srgb, var(--color-crimson) 15%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-crimson) 40%, transparent);
  color: var(--text-primary);
}

.connection-banner button {
  padding: var(--space-xs) var(--space-sm);
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
  background: var(--surface-subtle);
  cursor: pointer;
  font-size: 0.8rem;
  white-space: nowrap;
}
```

- [ ] **Step 3: Render ConnectionBanner in App.tsx**

Import and render above the workspace grid:

```typescript
import { ConnectionBanner } from './components/ConnectionBanner';

// Inside the JSX, before <div className="workspace-grid">:
<ConnectionBanner />
```

- [ ] **Step 4: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(browser): add connection banner with retry button

Shows warning when some API calls fail (partial) or error when
backend is fully unreachable (disconnected). Retry button re-triggers
full state load.

Refs #770

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 3: Sanitize prompt-form error messages (hide technical diagnostics)

**Files:**
- Modify: `src/components/ActionCard.tsx`
- Modify: `src/components/CommandResult.tsx`
- Modify: `src/utils/playerCopy.ts` (add file/protocol pattern sanitization)

- [ ] **Step 1: Add technical-pattern detection to playerCopy.ts**

Add a function that detects and hides file paths, JSON references, and protocol terms:

```typescript
// Append to src/utils/playerCopy.ts

const technicalPatterns: RegExp[] = [
  /\b[\w/\\]+\.json\b/gi,
  /\b[\w/\\]+\.txt\b/gi,
  /\b[\w/\\]+\.md\b/gi,
  /\bgame_state[\\/][\w/\\]+/gi,
  /\boutput[\\/][\w/\\]+/gi,
  /\bprotocol\b/gi,
  /\bartефакты? протокола\b/gi,
  /\bJSON:\s*\w+/gi,
  /\bФайл\s+\S+\s+не найден/gi,
  /\bnpc_core\b/gi,
  /\bsoul_state\b/gi,
  /\bcurrent_location\b/gi,
  /\bnarrative_response\b/gi
];

export function containsTechnicalDetails(text: string | null | undefined): boolean {
  if (!text) return false;
  return technicalPatterns.some(pattern => {
    pattern.lastIndex = 0;
    return pattern.test(text);
  });
}

export function sanitizePlayerMessage(text: string | null | undefined, fallback: string): { safe: string; hasTechnical: boolean } {
  const source = text?.trim();
  if (!source) {
    return { safe: fallback, hasTechnical: false };
  }

  const hasTechnical = containsTechnicalDetails(source);
  if (!hasTechnical) {
    return { safe: toPlayerFacingText(source, fallback), hasTechnical: false };
  }

  // Strip technical patterns and return cleaned version
  let cleaned = source;
  for (const pattern of technicalPatterns) {
    pattern.lastIndex = 0;
    cleaned = cleaned.replace(pattern, '');
  }
  cleaned = cleaned.replace(/\s{2,}/g, ' ').replace(/[—–]\s*$/g, '').trim();

  const safe = cleaned ? toPlayerFacingText(cleaned, fallback) : fallback;
  return { safe, hasTechnical: true };
}
```

- [ ] **Step 2: Update ActionCard to use sanitized messages**

In `src/components/ActionCard.tsx`, find where `notice` is set from command results. After the `submitGuidedForm` call and `submitPromptAnswers`, sanitize the notice text:

```typescript
import { sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';
import { useShell } from '../context/ShellContext';

// Inside ActionCard component, add:
const { advancedEnabled } = useShell();

// Replace notice display at the bottom of the component with:
{notice && (() => {
  const { safe, hasTechnical } = sanitizePlayerMessage(notice, 'Игровое действие обработано.');
  return (
    <>
      <p className="composer-notice">{safe}</p>
      {hasTechnical && advancedEnabled && <p className="muted technical-detail">{notice}</p>}
    </>
  );
})()}
```

- [ ] **Step 3: Update CommandResult to hide raw technical blocks by default**

In `src/components/CommandResult.tsx`, the `renderCommandBlock` function has a `rawJson` case that already hides behind muted text. But `text` blocks may contain file paths. Wrap them through sanitization:

```typescript
// In renderCommandBlock, update 'text' case:
case 'text': {
  const { safe, hasTechnical } = sanitizePlayerMessage(block.text, 'Текст игрового действия недоступен.');
  return hasTechnical
    ? <p className="muted">{safe}</p>
    : <p>{safe}</p>;
}
```

Import `sanitizePlayerMessage` at the top of CommandResult.tsx.

- [ ] **Step 4: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix(browser): hide file/protocol diagnostics from player-facing messages

Prompt form errors, action results, and command blocks now strip
file paths, JSON references, and protocol terms before showing to
players. Technical details available only in advanced mode.

Closes #745

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 4: Clear turn state presentation in GameRoute (GM-waiting vs repair)

**Files:**
- Modify: `src/routes/GameRoute.tsx`
- Modify: `src/components/ActionMenu.tsx` (TurnLifecycleActions — simplify for non-advanced)
- Modify: `src/styles/components.css` (turn state card styles)

- [ ] **Step 1: Refactor GameRoute turn status section**

Replace the current `turn-status-compact` section with a clear single-state card that distinguishes between "waiting for GM" and "needs repair":

```typescript
// In GameRoute.tsx, replace the turn-status-compact section and TurnLifecycleActions with:

{/* Turn State — clear single message */}
<TurnStateCard turnState={game.turnState} advancedEnabled={advancedEnabled} />
```

Create a local `TurnStateCard` component within GameRoute or extract it:

```typescript
function TurnStateCard({ turnState, advancedEnabled }: { turnState: BrowserGameScreenDto['turnState']; advancedEnabled: boolean }) {
  const isWaitingForGm = turnState.phase === 'gm-turn' || turnState.phase === 'waiting-for-gm' || turnState.state === 'gm-turn';
  const needsRepair = turnState.severity === 'error' || turnState.severity === 'repair' || turnState.validationState === 'invalid';
  const isNormal = !isWaitingForGm && !needsRepair;

  const playerActions = turnState.recommendedActions.filter(a => a.surface === 'player-default');

  if (isNormal && playerActions.length === 0) {
    return null;
  }

  return (
    <section className={`turn-state-card turn-state-card--${needsRepair ? 'repair' : isWaitingForGm ? 'waiting' : 'normal'}`} aria-label="Состояние хода">
      <div className="turn-state-card__header">
        <span className={`status-pill turn-phase turn-phase--${turnState.severity}`}>
          {formatTurnStateTitle(turnState)}
        </span>
      </div>

      <p>{formatTurnStateMessage(turnState)}</p>

      {needsRepair && (
        <p className="turn-state-card__guidance">
          {toPlayerFacingText(turnState.playerGuidance, 'Игра требует восстановления состояния. Используйте рекомендуемые действия ниже.')}
        </p>
      )}

      {isWaitingForGm && !needsRepair && (
        <p className="turn-state-card__guidance">Ожидается ответ ГМа. Ввод игрока откроется после записи нового хода.</p>
      )}

      {playerActions.length > 0 && (
        <ul className="choice-list">
          {playerActions.map((action) => (
            <li key={action.id}>
              <strong>{toPlayerFacingText(action.label, 'Действие')}</strong>
              <span className="muted">{formatTurnLifecycleActionDescription(action)}</span>
            </li>
          ))}
        </ul>
      )}

      {advancedEnabled && turnState.knownPhases.length > 0 && (
        <details className="turn-state-card__phases">
          <summary>Фазы хода (расширенный режим)</summary>
          <ul>
            {turnState.knownPhases.map(phase => (
              <li key={phase.id}><strong>{phase.label}</strong>: {phase.description}</li>
            ))}
          </ul>
        </details>
      )}
    </section>
  );
}
```

- [ ] **Step 2: Add turn-state-card CSS**

Append to `src/styles/components.css`:

```css
/* ── Turn state card ── */

.turn-state-card {
  padding: var(--space-md);
  border-radius: var(--radius-md);
  border: 1px solid var(--border-subtle);
  margin-top: var(--space-md);
}

.turn-state-card--waiting {
  border-color: color-mix(in srgb, var(--color-gold-bright) 30%, transparent);
  background: color-mix(in srgb, var(--color-gold-bright) 5%, transparent);
}

.turn-state-card--repair {
  border-color: color-mix(in srgb, var(--color-crimson) 40%, transparent);
  background: color-mix(in srgb, var(--color-crimson) 8%, transparent);
}

.turn-state-card--normal {
  border-color: var(--border-subtle);
}

.turn-state-card__header {
  margin-bottom: var(--space-xs);
}

.turn-state-card__guidance {
  font-size: 0.9rem;
  margin-top: var(--space-xs);
  color: var(--text-secondary);
}

.turn-state-card__phases {
  margin-top: var(--space-sm);
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.turn-state-card__phases summary {
  cursor: pointer;
  color: var(--text-secondary);
}
```

- [ ] **Step 3: Update GameRoute imports**

Remove `TurnLifecycleActions` import from GameRoute since it's now inlined as `TurnStateCard`. Add needed imports:

```typescript
import type { BrowserGameScreenDto } from '../api/contracts';
import { formatTurnLifecycleActionDescription } from '../utils/formatters';
```

Remove the `formatQteStateLabel` import if it's no longer used (the QTE status line was verbose — remove it from normal view; it stays in MediaRoute).

- [ ] **Step 4: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix(browser): separate GM-waiting from repair states in GameRoute

Turn status now shows one clear card: golden for GM-waiting, red for
repair-needed, hidden when normal. Lifecycle phases moved behind
advanced-mode details. QTE label removed from main game view.

Closes #743

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 5: Add graceful degradation tests + final verification

**Files:**
- Create: `test/gracefulDegradation.test.ts`

- [ ] **Step 1: Create test file**

```typescript
// test/gracefulDegradation.test.ts
import { readFileSync, readdirSync } from 'fs';
import { join } from 'path';
import { describe, it, expect } from 'vitest';

describe('Graceful degradation guards', () => {
  const srcDir = join(__dirname, '..', 'src');

  function readSrc(path: string): string {
    return readFileSync(join(srcDir, path), 'utf-8');
  }

  it('useShellState uses Promise.allSettled, not Promise.all', () => {
    const hook = readSrc('hooks/useShellState.ts');
    expect(hook).toContain('Promise.allSettled');
    expect(hook).not.toMatch(/Promise\.all\(/);
  });

  it('ConnectionBanner component exists and handles disconnected state', () => {
    const banner = readSrc('components/ConnectionBanner.tsx');
    expect(banner).toContain('is-disconnected');
    expect(banner).toContain('loadBrowserState');
  });

  it('playerCopy exports sanitizePlayerMessage', () => {
    const copy = readSrc('utils/playerCopy.ts');
    expect(copy).toContain('export function sanitizePlayerMessage');
    expect(copy).toContain('containsTechnicalDetails');
  });

  it('GameRoute does not mix lifecycle chips with player-facing state', () => {
    const route = readSrc('routes/GameRoute.tsx');
    // Should not have raw knownPhases rendering outside of details/advanced
    expect(route).not.toMatch(/knownPhases\.map[^}]*<li/s);
    // Should use TurnStateCard or equivalent clear state pattern
    expect(route).toContain('turn-state-card');
  });

  it('CommandResult sanitizes text blocks', () => {
    const result = readSrc('components/CommandResult.tsx');
    expect(result).toContain('sanitizePlayerMessage');
  });
});
```

- [ ] **Step 2: Run full verification gate**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "test(browser): add graceful degradation structural guards

Verify Promise.allSettled usage, ConnectionBanner presence,
sanitizePlayerMessage export, and turn state card pattern.

Refs #770
Refs #745
Refs #743

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 6: Push and create PR

- [ ] **Step 1: Push branch**

```bash
git push -u origin feat/browser-reliability-graceful-degradation
```

- [ ] **Step 2: Create PR**

```bash
gh pr create --title "fix(browser): graceful degradation, sanitized errors, clear turn states" --body "## Summary
Makes the browser client resilient to API failures, hides technical
diagnostics from player-facing messages, and clearly separates
GM-waiting from validation/repair states.

Closes #770
Closes #745
Closes #743
Refs #680

## Changes
- Promise.allSettled replaces Promise.all — partial UI renders
- ConnectionBanner with retry button (gold for partial, red for disconnected)
- sanitizePlayerMessage strips file paths, JSON refs, protocol terms
- TurnStateCard distinguishes waiting/repair/normal with visual coding
- Lifecycle phases hidden behind advanced-mode details
- Structural guard tests for all three fixes

## Verification
- npm run verify ✅
- dotnet guard tests (72+) ✅"
```
