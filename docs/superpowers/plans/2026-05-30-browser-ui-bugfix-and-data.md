# Browser UI Bug Fixes, Data Display & Scaling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 5 critical bugs, add UI/element scaling, rework inventory display from file-path dump to proper item cards, add interactive JSON viewer, and render Actions/Notifications from command results.

**Architecture:** Bug fixes are surgical edits to existing components. Inventory rework rewrites the C# `BuildBundle` path to emit structured item blocks. JSON viewer is a new React component. UI scale adds a second CSS custom property alongside the existing font-scale.

**Tech Stack:** React/TypeScript (Vite), C# (.NET 8), CSS custom properties, existing UiBlock protocol

**Issues:** #793, #794, #795, #796, #797, #799, #800

---

## File Structure

| File | Responsibility |
|------|---------------|
| `WebFrontend/src/components/UnifiedInput.tsx` | Fix Enter race condition + autocomplete UX |
| `WebFrontend/src/components/StatusView.tsx` | Fix stat bar label contrast |
| `WebFrontend/src/hooks/useSceneImage.ts` | Fix image generation retry blocking |
| `WebFrontend/src/components/CommandResultView.tsx` | Render Actions + Notifications sections |
| `WebFrontend/src/components/JsonTreeViewer.tsx` | NEW — interactive JSON tree viewer |
| `WebFrontend/src/components/BlockRenderer.tsx` | Use JsonTreeViewer for rawJson blocks |
| `WebFrontend/src/components/SettingsView.tsx` | Add UI scale slider |
| `WebFrontend/src/styles/command-ui.css` | Add stat bar fix, JSON tree styles, scale |
| `WebFrontend/src/App.tsx` | Apply `--browser-ui-scale` CSS var |
| `WebFrontend/src/styles/layout.css` | Consume `--browser-ui-scale` |
| `WebFrontend/src/api/contracts.ts` | Add `uiScalePercent` to accessibility DTO |
| `BookOfEternityClient/Configuration/GameSettings.cs` | Add `BrowserUiScalePercent` |
| `BookOfEternityClient/WebUi/BrowserClientSettingsService.cs` | Add uiScale to DTO + apply |
| `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` | Rewrite inventory to emit item blocks |

---

## Phase A: Critical Bug Fixes

### Task 1: Fix Enter-to-submit race condition (#795)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/components/UnifiedInput.tsx`

**Root cause:** `submitComposer` is a `useCallback` that captures `composerText` from closure. When `handleKeyDown` fires immediately after a keystroke, the closure may have stale `composerText`. The form's native `onSubmit` works because React has reconciled by then.

**Fix:** Read value directly from the textarea ref, bypassing the closure entirely.

- [ ] **Step 1: Rewrite handleKeyDown to submit via form ref**

Replace the current Enter handler in `UnifiedInput.tsx`:

```tsx
import { useRef, useState } from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import { CommandAutocomplete } from './CommandAutocomplete';

export function UnifiedInput() {
  const { composerText, setComposerText, composerNotice, submitComposer, readyState, gameScreen } = useShell();
  const [showAutocomplete, setShowAutocomplete] = useState(false);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const formRef = useRef<HTMLFormElement>(null);

  const coverage = readyState?.commandCoverage;
  const commands = coverage && isSuccess(coverage) ? coverage.data.commands : [];
  const canSubmit = gameScreen?.actionComposer.canSubmit ?? false;

  function handleChange(value: string) {
    setComposerText(value);
    setShowAutocomplete(value.startsWith('/') && value.length > 1);
  }

  function handleAutocompleteSelect(command: string) {
    setComposerText(command + ' ');
    setShowAutocomplete(false);
    inputRef.current?.focus();
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Escape') {
      setShowAutocomplete(false);
      return;
    }
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      if (showAutocomplete) {
        setShowAutocomplete(false);
        // Don't return — fall through to submit so user doesn't need double-Enter
      }
      // Submit via native form submit to avoid stale closure issue
      if (canSubmit && formRef.current) {
        formRef.current.requestSubmit();
      }
    }
  }

  return (
    <div className="unified-input">
      {composerNotice && <p className="unified-input__notice">{composerNotice}</p>}
      <form ref={formRef} className="unified-input__form" onSubmit={submitComposer}>
        <div className="unified-input__wrapper">
          <textarea
            ref={inputRef}
            rows={1}
            value={composerText}
            onChange={(e) => handleChange(e.target.value)}
            onKeyDown={handleKeyDown}
            onFocus={() => { if (composerText.startsWith('/')) setShowAutocomplete(true); }}
            onBlur={() => setTimeout(() => setShowAutocomplete(false), 200)}
            placeholder="Опишите действие или введите /команду..."
            disabled={!canSubmit}
            className="unified-input__textarea"
          />
          {showAutocomplete && (
            <CommandAutocomplete
              commands={commands}
              query={composerText}
              onSelect={handleAutocompleteSelect}
            />
          )}
        </div>
        <button type="submit" disabled={!composerText.trim() || !canSubmit} className="unified-input__submit">
          Отправить
        </button>
      </form>
    </div>
  );
}
```

Key changes:
- Added `formRef` to reference the form element
- Enter handler uses `formRef.current.requestSubmit()` instead of calling `submitComposer` directly — this triggers the form's native submit event which React handles synchronously with current state
- Removed the early `return` after closing autocomplete — Enter now always submits (closing autocomplete + submitting in one press)

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd BookOfEternityClient.WebFrontend && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 3: Test in browser**

Navigate to http://127.0.0.1:5173, type `/inventory`, press Enter once.
Expected: command executes and CommandResultView appears.

- [ ] **Step 4: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/UnifiedInput.tsx
git commit -m "fix(web): Enter-to-submit race condition — use requestSubmit (#795, #793)"
```

---

### Task 2: Fix status bar label contrast (#794)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/command-ui.css`
- Modify: `BookOfEternityClient.WebFrontend/src/components/StatusView.tsx`

- [ ] **Step 1: Update StatusView to separate label from bar**

In `StatusView.tsx`, find the stat bar rendering section. The labels (emoji + text) should be placed ABOVE the progress bar, not overlaid on it. Update the bar component to show:

```tsx
// Inside the status card's stat bars section, replace current bar rendering with:
function StatBar({ label, value, max, color }: { label: string; value: number; max: number; color: string }) {
  const pct = max > 0 ? Math.min(100, (value / max) * 100) : 0;
  return (
    <div className="stat-bar">
      <div className="stat-bar__header">
        <span className="stat-bar__label">{label}</span>
        <span className="stat-bar__value">{value}/{max}</span>
      </div>
      <div className="stat-bar__track">
        <div className="stat-bar__fill" style={{ width: `${pct}%`, background: color }} />
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Update CSS for stat bars**

Add to `command-ui.css`:

```css
/* ===== Stat Bars ===== */
.stat-bar { margin-bottom: 0.5rem; }
.stat-bar__header { display: flex; justify-content: space-between; margin-bottom: 0.2rem; font-size: 0.75rem; }
.stat-bar__label { color: var(--text-primary, #c9d1d9); }
.stat-bar__value { color: var(--text-muted, #8b949e); font-variant-numeric: tabular-nums; }
.stat-bar__track { height: 6px; background: var(--surface-base, #0d1117); border-radius: 3px; overflow: hidden; }
.stat-bar__fill { height: 100%; border-radius: 3px; transition: width 0.3s ease; }
```

- [ ] **Step 3: Verify visually**

Check Status tab — labels should be clearly readable above the colored bars.

- [ ] **Step 4: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/StatusView.tsx BookOfEternityClient.WebFrontend/src/styles/command-ui.css
git commit -m "fix(web): stat bar labels above track for readability (#794)"
```

---

### Task 3: Fix scene image generation retry (#796)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/hooks/useSceneImage.ts`

**Root cause:** `lastPromptRef.current` is set before generation and NEVER cleared on failure, permanently blocking retries for that entity key.

- [ ] **Step 1: Clear lastPromptRef on failure**

Find the generation logic in `useSceneImage.ts`. After the API call, if it fails, clear the ref:

```typescript
// In the useEffect or generation function:
generatingRef.current = entityKey;
lastPromptRef.current = entityKey;

try {
  const result = await browserApi.generateMedia({ prompt: sceneImagePrompt, mediaId: entityKey });
  if (!result.ok) {
    // Generation failed — allow retry on next render
    lastPromptRef.current = null;
  }
} catch {
  lastPromptRef.current = null;
} finally {
  generatingRef.current = null;
}
```

- [ ] **Step 2: Also clear on entityKey change**

If the scene changes (new location/turn), old entityKey is stale. Add at the top of the effect:

```typescript
// If entity changed, allow new generation
if (lastPromptRef.current && lastPromptRef.current !== entityKey) {
  lastPromptRef.current = null;
}
```

- [ ] **Step 3: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/hooks/useSceneImage.ts
git commit -m "fix(web): scene image generation retries after failure (#796)"
```

---

### Task 4: Add UI element scale setting

**Files:**
- Modify: `BookOfEternityClient/Configuration/GameSettings.cs`
- Modify: `BookOfEternityClient/WebUi/BrowserClientSettingsService.cs`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/components/SettingsView.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/layout.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/command-ui.css`

- [ ] **Step 1: Add BrowserUiScalePercent to GameSettings.cs**

Find `BrowserFontScalePercent` in `Configuration/GameSettings.cs` (~line 107-115). Add below it:

```csharp
public int BrowserUiScalePercent { get; set; } = 100;
```

In the `ApplyLoaded` method (same file), add clamping similar to font scale:

```csharp
BrowserUiScalePercent = Math.Clamp(BrowserUiScalePercent, 70, 150);
```

- [ ] **Step 2: Update BrowserClientSettingsService.cs**

In the accessibility DTO class, add the field:

```csharp
// In BrowserClientAccessibilitySettingsDto record:
public int UiScalePercent { get; init; }
```

In `BrowserClientSettingsUpdateRequest`:

```csharp
public int? BrowserUiScalePercent { get; init; }
```

In `BuildDto()` where accessibility is constructed:

```csharp
Accessibility = new BrowserClientAccessibilitySettingsDto
{
    FontScalePercent = settings.BrowserFontScalePercent,
    UiScalePercent = settings.BrowserUiScalePercent,
    ReducedMotion = settings.BrowserReducedMotion,
    ContrastFriendly = settings.BrowserContrastFriendly
}
```

In `ApplyRequest()`:

```csharp
if (request.BrowserUiScalePercent.HasValue)
    settings.BrowserUiScalePercent = Math.Clamp(request.BrowserUiScalePercent.Value, 70, 150);
```

- [ ] **Step 3: Update TypeScript contracts**

In `api/contracts.ts`, add to `BrowserClientAccessibilitySettingsDto`:

```typescript
export interface BrowserClientAccessibilitySettingsDto {
  fontScalePercent: number;
  uiScalePercent: number;
  reducedMotion: boolean;
  contrastFriendly: boolean;
}
```

Add to `BrowserClientSettingsUpdateRequest`:

```typescript
browserUiScalePercent?: number | null;
```

- [ ] **Step 4: Apply CSS variable in App.tsx**

Update the style object:

```tsx
const browserShellStyle = {
  '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`,
  '--browser-ui-scale': `${(clientSettings?.accessibility.uiScalePercent ?? 100) / 100}`
} as CSSProperties;
```

- [ ] **Step 5: Use --browser-ui-scale in CSS**

In `layout.css`, add the default:

```css
--browser-ui-scale: 1;
```

In `command-ui.css`, apply UI scale to padding/gaps/buttons (NOT font-size):

```css
.tab-bar { padding: calc(0.5rem * var(--browser-ui-scale)) calc(1rem * var(--browser-ui-scale)); }
.tab-bar__tab { padding: calc(0.4rem * var(--browser-ui-scale)) calc(0.9rem * var(--browser-ui-scale)); }
.content-area { padding: calc(1.25rem * var(--browser-ui-scale)) calc(1.5rem * var(--browser-ui-scale)); }
.unified-input { padding: calc(0.75rem * var(--browser-ui-scale)) calc(1.5rem * var(--browser-ui-scale)); }
.scene-action-chip, .scene-dialogue-chip { padding: calc(0.4rem * var(--browser-ui-scale)) calc(0.75rem * var(--browser-ui-scale)); }
```

- [ ] **Step 6: Add slider to SettingsView**

In the Доступность (Accessibility) section of `SettingsView.tsx`, add below font size:

```tsx
<div className="settings-row">
  <label>Размер элементов интерфейса</label>
  <input
    type="range"
    min="70"
    max="150"
    value={settings.accessibility.uiScalePercent}
    onChange={(e) => {
      const v = Number(e.target.value);
      setSettings({ ...settings, accessibility: { ...settings.accessibility, uiScalePercent: v } });
      debouncedUpdate({ browserUiScalePercent: v });
    }}
  />
  <span>{settings.accessibility.uiScalePercent}%</span>
</div>
```

- [ ] **Step 7: Build backend and verify**

```bash
cd BookOfEternityClient && dotnet build --no-restore
cd ../BookOfEternityClient.WebFrontend && npx tsc --noEmit
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(web): add UI element scale setting separate from font (#794 related)"
```

---

## Phase B: Data Display Improvements

### Task 5: Render Actions and Notifications from CommandResult (#800)

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/components/CommandResultView.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/command-ui.css`

The `ExplorerCommandResult` contains `blocks[]`, `actions[]`, `notifications[]` but CommandResultView only renders blocks.

- [ ] **Step 1: Check current CommandResultView**

Read the file and verify it only renders `commandResult.blocks`.

- [ ] **Step 2: Add Notifications rendering**

Before the blocks section, render notifications:

```tsx
{result.notifications && result.notifications.length > 0 && (
  <section className="cmd-notifications">
    {result.notifications.map((n, i) => (
      <div key={i} className={`cmd-notification cmd-notification--${n.severity?.toLowerCase() ?? 'info'}`}>
        {n.title && <strong>{n.title}</strong>}
        <p>{n.message}</p>
      </div>
    ))}
  </section>
)}
```

- [ ] **Step 3: Add Actions rendering**

After the blocks, render available actions as clickable buttons:

```tsx
{result.actions && result.actions.length > 0 && (
  <section className="cmd-actions">
    <h4 className="cmd-actions__title">Доступные действия</h4>
    <div className="cmd-actions__list">
      {result.actions.map((action, i) => (
        <button
          key={i}
          type="button"
          className={`cmd-action-btn cmd-action-btn--${action.style ?? 'default'}`}
          onClick={() => void executeCommand(action.command)}
        >
          {action.label}
        </button>
      ))}
    </div>
  </section>
)}
```

- [ ] **Step 4: Add CSS for notifications and actions**

```css
/* Command Notifications */
.cmd-notifications { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 1rem; }
.cmd-notification { padding: 0.6rem 0.8rem; border-radius: 6px; border-left: 3px solid; font-size: 0.82rem; }
.cmd-notification--info { border-color: var(--color-accent); background: rgba(31,111,235,0.08); }
.cmd-notification--warning { border-color: var(--color-warning); background: rgba(240,136,62,0.08); }
.cmd-notification--error { border-color: var(--color-danger, #f85149); background: rgba(248,81,73,0.08); }
.cmd-notification--success { border-color: var(--color-success); background: rgba(35,134,54,0.08); }

/* Command Actions */
.cmd-actions { margin-top: 1rem; padding-top: 0.75rem; border-top: 1px solid var(--border-subtle, #21262d); }
.cmd-actions__title { font-size: 0.8rem; color: var(--text-muted); margin-bottom: 0.5rem; }
.cmd-actions__list { display: flex; flex-wrap: wrap; gap: 0.4rem; }
.cmd-action-btn { padding: 0.35rem 0.7rem; border-radius: 4px; font-size: 0.8rem; cursor: pointer; border: 1px solid var(--border); background: var(--surface-raised); color: var(--text-primary); transition: background 0.15s; }
.cmd-action-btn:hover { background: var(--surface-hover, #1f2937); }
.cmd-action-btn--primary { background: var(--color-accent); color: #fff; border-color: var(--color-accent); }
```

- [ ] **Step 5: Update contracts.ts if needed**

Ensure `ExplorerCommandResult` type includes `actions` and `notifications` arrays. Check the interface and add if missing:

```typescript
export interface ExplorerCommandResult {
  command: string;
  state: string;
  blocks: UiBlock[];
  actions?: ExplorerCommandAction[];
  notifications?: ExplorerCommandNotification[];
  prompts?: ExplorerCommandPrompt[];
}

export interface ExplorerCommandAction {
  label: string;
  command: string;
  style?: string;
}

export interface ExplorerCommandNotification {
  severity?: string;
  title?: string;
  message: string;
}
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(web): render Actions and Notifications in command results (#800)"
```

---

### Task 6: Interactive JSON tree viewer

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/JsonTreeViewer.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/command-ui.css`

- [ ] **Step 1: Create JsonTreeViewer component**

Create `src/components/JsonTreeViewer.tsx`:

```tsx
import { useState } from 'react';

interface Props {
  data: unknown;
  title?: string;
  defaultExpanded?: boolean;
}

export function JsonTreeViewer({ data, title, defaultExpanded = true }: Props) {
  return (
    <div className="json-tree">
      {title && <h4 className="json-tree__title">{title}</h4>}
      <JsonNode value={data} depth={0} defaultExpanded={defaultExpanded} />
    </div>
  );
}

function JsonNode({ value, depth, keyName, defaultExpanded }: {
  value: unknown;
  depth: number;
  keyName?: string;
  defaultExpanded: boolean;
}) {
  const [expanded, setExpanded] = useState(depth < 2 || defaultExpanded);

  if (value === null || value === undefined) {
    return (
      <span className="json-tree__line">
        {keyName && <span className="json-tree__key">{keyName}: </span>}
        <span className="json-tree__null">null</span>
      </span>
    );
  }

  if (typeof value === 'boolean') {
    return (
      <span className="json-tree__line">
        {keyName && <span className="json-tree__key">{keyName}: </span>}
        <span className="json-tree__bool">{String(value)}</span>
      </span>
    );
  }

  if (typeof value === 'number') {
    return (
      <span className="json-tree__line">
        {keyName && <span className="json-tree__key">{keyName}: </span>}
        <span className="json-tree__number">{value}</span>
      </span>
    );
  }

  if (typeof value === 'string') {
    // Detect long text (lore, descriptions) and render multiline
    const isLong = value.length > 80;
    return (
      <span className="json-tree__line">
        {keyName && <span className="json-tree__key">{keyName}: </span>}
        <span className={`json-tree__string ${isLong ? 'json-tree__string--long' : ''}`}>
          {isLong ? value : `"${value}"`}
        </span>
      </span>
    );
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return (
        <span className="json-tree__line">
          {keyName && <span className="json-tree__key">{keyName}: </span>}
          <span className="json-tree__muted">[] (пусто)</span>
        </span>
      );
    }
    return (
      <div className="json-tree__branch">
        <button
          type="button"
          className="json-tree__toggle"
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? '▼' : '▶'} {keyName && <span className="json-tree__key">{keyName}</span>}
          <span className="json-tree__count">[{value.length}]</span>
        </button>
        {expanded && (
          <div className="json-tree__children">
            {value.map((item, i) => (
              <JsonNode key={i} value={item} depth={depth + 1} keyName={`[${i}]`} defaultExpanded={depth < 1} />
            ))}
          </div>
        )}
      </div>
    );
  }

  if (typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) {
      return (
        <span className="json-tree__line">
          {keyName && <span className="json-tree__key">{keyName}: </span>}
          <span className="json-tree__muted">{'{}'} (пусто)</span>
        </span>
      );
    }
    return (
      <div className="json-tree__branch">
        <button
          type="button"
          className="json-tree__toggle"
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? '▼' : '▶'} {keyName && <span className="json-tree__key">{keyName}</span>}
          <span className="json-tree__count">{`{${entries.length}}`}</span>
        </button>
        {expanded && (
          <div className="json-tree__children">
            {entries.map(([k, v]) => (
              <JsonNode key={k} value={v} depth={depth + 1} keyName={k} defaultExpanded={depth < 1} />
            ))}
          </div>
        )}
      </div>
    );
  }

  return <span className="json-tree__line">{String(value)}</span>;
}
```

- [ ] **Step 2: Add CSS for JSON tree**

Add to `command-ui.css`:

```css
/* ===== JSON Tree Viewer ===== */
.json-tree { font-family: 'JetBrains Mono', 'Fira Code', monospace; font-size: 0.78rem; line-height: 1.5; }
.json-tree__title { font-family: inherit; font-size: 0.85rem; margin-bottom: 0.5rem; color: var(--text-primary); }
.json-tree__line { display: block; padding: 0.05rem 0; }
.json-tree__key { color: var(--color-accent, #58a6ff); }
.json-tree__string { color: var(--color-success, #7ee787); }
.json-tree__string--long { color: var(--text-secondary, #8b949e); white-space: pre-wrap; display: block; padding-left: 1rem; margin: 0.2rem 0; border-left: 2px solid var(--border-subtle); }
.json-tree__number { color: #d2a8ff; }
.json-tree__bool { color: #ffa657; }
.json-tree__null { color: var(--text-muted); font-style: italic; }
.json-tree__muted { color: var(--text-muted); font-style: italic; }
.json-tree__branch { margin: 0.1rem 0; }
.json-tree__toggle { background: none; border: none; color: var(--text-primary); cursor: pointer; font-size: 0.78rem; font-family: inherit; padding: 0.1rem 0; }
.json-tree__toggle:hover { color: var(--color-accent); }
.json-tree__count { color: var(--text-muted); font-size: 0.72rem; margin-left: 0.3rem; }
.json-tree__children { padding-left: 1.2rem; border-left: 1px solid var(--border-subtle, #21262d); margin-left: 0.4rem; }
```

- [ ] **Step 3: Update BlockRenderer to use JsonTreeViewer**

In `BlockRenderer.tsx`, replace the rawJson case (currently uses `<details>/<pre>`):

```tsx
import { JsonTreeViewer } from './JsonTreeViewer';

// In the switch/case for 'rawJson':
case 'rawJson':
  return (
    <div className="block-raw-json" key={index}>
      <JsonTreeViewer data={block.json} title={block.title} defaultExpanded={true} />
    </div>
  );
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(web): interactive JSON tree viewer replaces raw pre/details blocks"
```

---

### Task 7: Rework inventory command output (C# backend) (#799)

**Files:**
- Modify: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`

The current `BuildBundle` for inventory emits a table with file paths and raw JSON dumps. It should emit structured blocks like the console client does.

- [ ] **Step 1: Create BuildInventory method**

Replace the inventory case in the switch (lines 99-103) with a dedicated method:

```csharp
CommandKind.Inventory => await BuildInventory(normalizedCommand, fs),
```

Implement `BuildInventory`:

```csharp
private static async Task<ExplorerCommandResult> BuildInventory(string command, FileSystemManager fs)
{
    var itemsRead = await ReadJson(fs, "game_state/inventory/items.json");
    if (itemsRead.Node == null)
    {
        return Completed(command, [
            Message(UiNotificationSeverity.Info, "Инвентарь", "Инвентарь пуст или файл не найден.")
        ]);
    }

    var blocks = new List<UiBlock>();
    var root = itemsRead.Node;

    // Money & weight summary
    var summaryItems = new List<UiKeyValueItem>();
    var money = root["money"]?.GetValue<int>() ?? 0;
    if (money > 0)
        summaryItems.Add(new UiKeyValueItem { Key = "💰 Деньги", Value = money.ToString() });
    var totalWeight = root["totalWeight"]?.ToString() ?? "—";
    var maxWeight = root["maxWeight"]?.ToString() ?? "—";
    summaryItems.Add(new UiKeyValueItem { Key = "⚖ Вес", Value = $"{totalWeight} / {maxWeight}" });

    if (summaryItems.Count > 0)
        blocks.Add(new UiKeyValueGridBlock { Items = summaryItems });

    // Equipment section
    var equipment = root["equipment"];
    if (equipment != null)
    {
        var equipItems = new List<UiKeyValueItem>();
        var slotLabels = new Dictionary<string, string>
        {
            ["head"] = "🪖 Голова", ["body"] = "🛡️ Тело", ["hands"] = "🧤 Руки",
            ["feet"] = "👢 Ноги", ["mainHand"] = "⚔️ Основная рука", ["offHand"] = "🛡️ Вторая рука",
            ["neck"] = "📿 Шея", ["ring1"] = "💍 Кольцо 1", ["ring2"] = "💍 Кольцо 2"
        };
        foreach (var (key, label) in slotLabels)
        {
            var slot = equipment[key];
            if (slot == null || slot.GetValueKind() == System.Text.Json.JsonValueKind.Null)
            {
                equipItems.Add(new UiKeyValueItem { Key = label, Value = "— пусто —" });
                continue;
            }
            var name = slot["name"]?.GetValue<string>()
                ?? slot["itemName"]?.GetValue<string>()
                ?? slot["existedId"]?.GetValue<string>()
                ?? "???";
            equipItems.Add(new UiKeyValueItem { Key = label, Value = name });
        }
        blocks.Add(new UiPanelBlock
        {
            Title = "⚔ Экипировка",
            Blocks = [new UiKeyValueGridBlock { Items = equipItems }]
        });
    }

    // Items table
    var items = root["items"] ?? root["inventoryItems"];
    if (items != null && items is JsonArray itemsArray && itemsArray.Count > 0)
    {
        var rows = new List<UiTableRow>();
        foreach (var item in itemsArray)
        {
            if (item == null) continue;
            var name = item["name"]?.GetValue<string>() ?? "???";
            var type = item["type"]?.GetValue<string>() ?? "";
            var quality = item["quality"]?.GetValue<string>() ?? item["rarity"]?.GetValue<string>() ?? "";
            var count = item["count"]?.GetValue<int>() ?? item["quantity"]?.GetValue<int>() ?? 1;
            var weight = item["weight"]?.ToString() ?? "—";
            var durability = item["durability"]?.GetValue<string>() ?? "—";

            var flags = new List<string>();
            if (item["isBroken"]?.GetValue<bool>() == true) flags.Add("⚠ СЛОМАН");
            if (item["isEmpty"]?.GetValue<bool>() == true) flags.Add("⚠ ПУСТО");

            var countStr = count > 1 ? $" x{count}" : "";
            var flagStr = flags.Count > 0 ? " " + string.Join(" ", flags) : "";

            rows.Add(new UiTableRow
            {
                Cells = [
                    $"{name}{countStr}{flagStr}",
                    type,
                    quality,
                    weight,
                    durability
                ]
            });
        }
        blocks.Add(new UiTableBlock
        {
            Title = "📦 Предметы",
            Columns = ["Название", "Тип", "Качество", "Вес", "Прочность"],
            Rows = rows
        });
    }

    // Resources
    var resources = root["resources"];
    if (resources != null && resources is JsonArray resArray && resArray.Count > 0)
    {
        var resItems = new List<UiKeyValueItem>();
        foreach (var res in resArray)
        {
            if (res == null) continue;
            var name = res["name"]?.GetValue<string>() ?? "???";
            var amount = res["amount"]?.ToString() ?? res["count"]?.ToString() ?? "1";
            resItems.Add(new UiKeyValueItem { Key = $"💎 {name}", Value = amount });
        }
        if (resItems.Count > 0)
            blocks.Add(new UiPanelBlock
            {
                Title = "💎 Ресурсы",
                Blocks = [new UiKeyValueGridBlock { Items = resItems }]
            });
    }

    return Completed(command, blocks);
}
```

- [ ] **Step 2: Build and verify**

```bash
cd BookOfEternityClient && dotnet build --no-restore
```

- [ ] **Step 3: Test via API**

```bash
curl -X POST http://127.0.0.1:8787/api/explorer/command -H "Content-Type: application/json" -d "{\"command\":\"/inventory\"}" | head -c 500
```

Expected: blocks with keyValueGrid, panel, and table — NOT file paths.

- [ ] **Step 4: Commit**

```bash
git add BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
git commit -m "feat: inventory command emits structured item blocks instead of file paths (#799)"
```

---

### Task 8: Handle missing game state files gracefully (#797)

**Files:**
- Modify: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`

- [ ] **Step 1: Update BuildBundle to skip missing files**

In the `BuildBundle` method (line 245+), change the logic to not show rows for files that don't exist:

```csharp
foreach (var spec in specs)
{
    var read = reads[spec.Path];
    if (read.Node == null && !read.FileExists)
        continue; // Skip missing files entirely instead of showing "отсутствует"
    rows.Add(new UiTableRow
    {
        Cells = [
            spec.Label,
            DescribeSpec(read, spec.PropertyName)
        ]
    });
}
```

Also update the Columns to remove "Файл" column (internal paths should never be shown to players):

```csharp
Columns = ["Раздел", "Состояние"],
```

- [ ] **Step 2: Build and commit**

```bash
cd BookOfEternityClient && dotnet build --no-restore
git add BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
git commit -m "fix: hide missing game state files from player-facing command output (#797)"
```

---

## Phase C: Verification

### Task 9: Full integration test

- [ ] **Step 1: Restart backend**

```bash
cd BookOfEternityClient && dotnet run -- --web
```

- [ ] **Step 2: Verify all tabs work**

Open http://127.0.0.1:5173 in browser:
- Scene tab: narrative renders, combat log formatted, quick actions clickable
- Status tab: bars readable with labels above
- Help tab: commands searchable, click executes
- Settings tab: font scale + UI scale sliders work

- [ ] **Step 3: Test /inventory**

Type `/inventory` and press Enter.
Expected: Summary (money, weight), equipment slots, item table with names/types/quality.

- [ ] **Step 4: Test other commands**

- `/soul` — keyValueGrid renders
- `/status` — keyValueGrid renders
- `/map` — map block renders
- `/npcs` — table or list renders

- [ ] **Step 5: Push all commits**

```bash
git push origin main
```

---

## Out of Scope (Separate Plan)

- Full command data parity with console client (60+ commands) — tracked in #762
- Interactive item drill-down in browser (click item → show details panel)
- Map visualization (SVG/canvas rendering)
- Prompt sessions (interactive multi-step commands)
