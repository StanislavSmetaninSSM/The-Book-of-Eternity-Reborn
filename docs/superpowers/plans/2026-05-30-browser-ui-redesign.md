# Browser UI Redesign — Command-Driven Interface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 8-tab debug layout with a 4-tab command-driven interface where players execute commands and see structured results.

**Architecture:** Single-page app with 4 tabs (Scene/Status/Help/Settings). Commands executed via unified input or Help catalog. Results replace content area with "back to scene" navigation. Existing `CommandResult.tsx` block renderer and `browserApi` client reused.

**Tech Stack:** React 18, TypeScript, Vite, CSS (existing dark theme variables)

---

## File Structure

### New files to create:
| File | Responsibility |
|------|---------------|
| `src/components/TabBar.tsx` | 4 horizontal tabs with active indicator |
| `src/components/SceneView.tsx` | Narrative + dialogues + quick actions (replaces GameRoute) |
| `src/components/CommandResultView.tsx` | Full-screen command result with "back" button |
| `src/components/HelpView.tsx` | Searchable categorized command catalog |
| `src/components/StatusView.tsx` | Character status summary card |
| `src/components/SettingsView.tsx` | Consolidated settings (reuse existing logic) |
| `src/components/UnifiedInput.tsx` | Bottom input bar with `/` autocomplete |
| `src/components/CommandAutocomplete.tsx` | Dropdown for matching commands |
| `src/components/BlockRenderer.tsx` | Improved recursive UiBlock renderer |

### Files to heavily modify:
| File | Changes |
|------|---------|
| `src/App.tsx` | Remove Sidebar, PlayerStatusSidebar, 8-route system. Replace with TabBar + ContentArea + UnifiedInput |
| `src/context/ShellContext.tsx` | Change `RouteId` to `TabId`, add `commandResult` state, rewrite `submitComposer` to handle `/commands` |

### Files to delete (or stop importing):
- `src/components/Sidebar.tsx`
- `src/components/navBarConfig.ts`
- `src/components/PlayerStatusSidebar.tsx`
- `src/components/ActionPalette.tsx`
- `src/components/Composer.tsx`
- `src/routes/HomeRoute.tsx`
- `src/routes/GameRoute.tsx`
- `src/routes/SoulRoute.tsx`
- `src/routes/WorldRoute.tsx`
- `src/routes/JournalRoute.tsx`
- `src/routes/InventoryRoute.tsx`
- `src/routes/MediaRoute.tsx`

### Files to keep (reuse):
- `src/api/client.ts` (unchanged)
- `src/api/contracts.ts` (unchanged)
- `src/components/PromptForm.tsx` (reused in CommandResultView)
- `src/components/SceneHero.tsx` (reused in SceneView)
- `src/components/LoadingCard.tsx` (reused)
- `src/components/ErrorNotice.tsx` (reused)
- `src/components/ConnectionBanner.tsx` (reused)
- `src/hooks/useShellState.ts` (unchanged)
- `src/hooks/useSceneImage.ts` (reused in SceneView)
- `src/utils/playerCopy.ts` (reused)
- `src/utils/formatters.ts` (reused)

---

### Task 1: Rewrite ShellContext — new tab system and command state

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/context/ShellContext.tsx`

- [ ] **Step 1: Replace RouteId with TabId and add command state**

Replace the entire `ShellContext.tsx` with the new version. Key changes:
- `RouteId` → `TabId = 'scene' | 'status' | 'help' | 'settings'`
- Add `commandResult: ExplorerCommandResult | null`
- Add `isCommandView: boolean`
- Add `executeCommand(command: string): Promise<void>`
- Add `clearCommandResult(): void`
- Rewrite `submitComposer` to handle `/commands` via `executeExplorerCommand`
- Remove references to old route system

```tsx
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
  type ReactNode
} from 'react';
import { browserApi } from '../api/client';
import type {
  BrowserApiResult,
  BrowserAudioSettingsDto,
  BrowserClientSettingsDto,
  BrowserCommandCoverageDto,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserMainMenuDto,
  ExplorerCommandResult,
  LocalWebUiSessionStatus
} from '../api/contracts';
import { useShellState } from '../hooks/useShellState';

export type TabId = 'scene' | 'status' | 'help' | 'settings';

export type BrowserShellState =
  | { status: 'loading' }
  | {
      status: 'ready';
      connectionStatus: 'connected' | 'partial';
      menu: BrowserApiResult<BrowserMainMenuDto>;
      session: BrowserApiResult<LocalWebUiSessionStatus>;
      game: BrowserApiResult<BrowserGameScreenDto>;
      audio: BrowserApiResult<BrowserAudioSettingsDto>;
      settings: BrowserApiResult<BrowserClientSettingsDto>;
      lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> | null;
      commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null;
    }
  | { status: 'error'; playerMessage: string; technicalDetails?: string };

export interface RealmTheme {
  key: string;
  label: string;
  icon: string;
  accent: string;
}

export interface ShellContextValue {
  shellState: BrowserShellState;
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null;
  gameScreen: BrowserGameScreenDto | null;
  menu: BrowserMainMenuDto | null;
  session: LocalWebUiSessionStatus | null;
  clientSettings: BrowserClientSettingsDto | null;
  realmTheme: RealmTheme;
  activeTab: TabId;
  setActiveTab: (tab: TabId) => void;
  connectionStatus: 'connected' | 'partial' | 'disconnected';
  advancedEnabled: boolean;
  setAdvancedEnabled: (updater: (value: boolean) => boolean) => void;
  composerText: string;
  setComposerText: (value: string) => void;
  composerNotice: string | null;
  submitComposer: (event: FormEvent<HTMLFormElement>) => void;
  commandResult: ExplorerCommandResult | null;
  isCommandView: boolean;
  executeCommand: (command: string) => Promise<void>;
  clearCommandResult: () => void;
  loadBrowserState: () => Promise<void>;
}

const fallbackTheme: RealmTheme = {
  key: 'mortal-world',
  label: 'Мир смертных',
  icon: '🌘',
  accent: '#c9a24d'
};

export const ShellContext = createContext<ShellContextValue | null>(null);

export function isSuccess<T>(result: BrowserApiResult<T>): result is Extract<BrowserApiResult<T>, { ok: true }> {
  return result.ok;
}

export function resolveRealmTheme(gameScreen: BrowserGameScreenDto | null): RealmTheme {
  if (!gameScreen) return fallbackTheme;
  return {
    key: gameScreen.theme.key,
    label: gameScreen.theme.label,
    icon: gameScreen.theme.icon,
    accent: gameScreen.theme.accent || fallbackTheme.accent
  };
}

export function useShell() {
  const context = useContext(ShellContext);
  if (!context) throw new Error('useShell must be used within a ShellProvider.');
  return context;
}

export function ShellProvider({ children }: { children: ReactNode }) {
  const [activeTab, setActiveTab] = useState<TabId>('scene');
  const [advancedEnabled, setAdvancedEnabledState] = useState(false);
  const [composerText, setComposerTextState] = useState('');
  const [composerNotice, setComposerNotice] = useState<string | null>(null);
  const [commandResult, setCommandResult] = useState<ExplorerCommandResult | null>(null);
  const [isCommandView, setIsCommandView] = useState(false);
  const { shellState, loadBrowserState } = useShellState(advancedEnabled);

  useEffect(() => { void loadBrowserState(); }, [loadBrowserState]);

  const readyState = shellState.status === 'ready' ? shellState : null;
  const gameScreen = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  const menu = readyState && isSuccess(readyState.menu) ? readyState.menu.data : null;
  const session = readyState && isSuccess(readyState.session) ? readyState.session.data : null;
  const clientSettings = readyState && isSuccess(readyState.settings) ? readyState.settings.data : null;
  const connectionStatus: 'connected' | 'partial' | 'disconnected' =
    shellState.status === 'ready' ? shellState.connectionStatus :
    shellState.status === 'error' ? 'disconnected' : 'connected';
  const realmTheme = useMemo(() => resolveRealmTheme(gameScreen), [gameScreen]);

  const setAdvancedEnabled = useCallback((updater: (value: boolean) => boolean) => {
    setAdvancedEnabledState(updater);
  }, []);
  const setComposerText = useCallback((value: string) => { setComposerTextState(value); }, []);

  const clearCommandResult = useCallback(() => {
    setCommandResult(null);
    setIsCommandView(false);
  }, []);

  const executeCommand = useCallback(async (command: string) => {
    setComposerNotice('Выполняю команду…');
    try {
      const result = await browserApi.executeExplorerCommand({ command });
      if (result.ok) {
        setCommandResult(result.data);
        setIsCommandView(true);
        setActiveTab('scene');
        setComposerNotice(null);
      } else {
        setComposerNotice(result.playerMessage);
      }
    } catch {
      setComposerNotice('Ошибка соединения при выполнении команды.');
    }
    void loadBrowserState();
  }, [loadBrowserState]);

  const submitComposer = useCallback((event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalized = composerText.trim();
    if (!normalized) return;

    if (normalized.startsWith('/')) {
      setComposerTextState('');
      void executeCommand(normalized);
      return;
    }

    setComposerNotice('Отправляем действие…');
    void browserApi.submitPlayerAction({ text: normalized }).then((result) => {
      if (result.ok && result.data.success) {
        setComposerNotice(result.data.playerMessage);
        setComposerTextState('');
        clearCommandResult();
        void loadBrowserState();
      } else if (result.ok && !result.data.success) {
        setComposerNotice(result.data.playerMessage);
      } else {
        setComposerNotice('Не удалось отправить действие. Попробуйте ещё раз.');
      }
    }).catch(() => {
      setComposerNotice('Ошибка соединения. Убедитесь, что клиент запущен.');
    });
  }, [composerText, executeCommand, clearCommandResult, loadBrowserState]);

  const value = useMemo<ShellContextValue>(() => ({
    shellState, readyState, gameScreen, menu, session, clientSettings, realmTheme,
    activeTab, setActiveTab, connectionStatus, advancedEnabled, setAdvancedEnabled,
    composerText, setComposerText, composerNotice, submitComposer,
    commandResult, isCommandView, executeCommand, clearCommandResult, loadBrowserState
  }), [
    shellState, readyState, gameScreen, menu, session, clientSettings, realmTheme,
    activeTab, connectionStatus, advancedEnabled, setAdvancedEnabled,
    composerText, setComposerText, composerNotice, submitComposer,
    commandResult, isCommandView, executeCommand, clearCommandResult, loadBrowserState
  ]);

  return <ShellContext.Provider value={value}>{children}</ShellContext.Provider>;
}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd BookOfEternityClient.WebFrontend && npx tsc --noEmit 2>&1 | head -20`

Expected: Errors about missing imports in App.tsx and routes (expected — we haven't updated those yet). No errors in ShellContext.tsx itself.

- [ ] **Step 3: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/context/ShellContext.tsx
git commit -m "refactor(web): rewrite ShellContext — tab system + command state"
```

---

### Task 2: Create BlockRenderer — improved UiBlock rendering

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx`

- [ ] **Step 1: Create BlockRenderer.tsx**

This component renders any `UiBlock` recursively with proper styling per block kind and tone:

```tsx
import type { ReactNode } from 'react';
import type { UiBlock, UiTone } from '../api/contracts';
import { toPlayerFacingText } from '../utils/playerCopy';

function toneClassName(tone: UiTone): string {
  switch (tone) {
    case 'Muted': return 'block-text--muted';
    case 'Subtle': return 'block-text--subtle';
    case 'Accent': return 'block-text--accent';
    case 'Success': return 'block-text--success';
    case 'Warning': return 'block-text--warning';
    case 'Error': return 'block-text--error';
    default: return '';
  }
}

export function BlockRenderer({ block }: { block: UiBlock }): ReactNode {
  switch (block.kind) {
    case 'text':
      return <p className={`block-text ${toneClassName(block.tone)}`}>{block.text}</p>;

    case 'panel':
      return (
        <section className="block-panel">
          <h4 className="block-panel__title">{toPlayerFacingText(block.title, 'Панель')}</h4>
          <div className="block-panel__body">
            {block.blocks.map((child, i) => <BlockRenderer key={`${child.kind}-${i}`} block={child} />)}
          </div>
        </section>
      );

    case 'table':
      return (
        <div className="block-table">
          {block.title && <h4 className="block-table__title">{toPlayerFacingText(block.title, 'Таблица')}</h4>}
          <div className="block-table__scroll">
            <table>
              <thead>
                <tr>{block.columns.map((col) => <th key={col}>{col}</th>)}</tr>
              </thead>
              <tbody>
                {block.rows.map((row, i) => (
                  <tr key={i}>{row.cells.map((cell, j) => <td key={j}>{cell}</td>)}</tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      );

    case 'list': {
      const ListTag = block.ordered ? 'ol' : 'ul';
      return (
        <ListTag className="block-list">
          {block.items.map((item, i) => <li key={i}>{item}</li>)}
        </ListTag>
      );
    }

    case 'keyValueGrid':
      return (
        <dl className="block-kv">
          {block.items.map((item) => (
            <div key={item.key} className="block-kv__row">
              <dt>{item.key}</dt>
              <dd>{item.value}</dd>
            </div>
          ))}
        </dl>
      );

    case 'message': {
      const severityClass = `block-message--${block.severity.toLowerCase()}`;
      return (
        <div className={`block-message ${severityClass}`}>
          <strong>{toPlayerFacingText(block.title, 'Сообщение')}</strong>
          <p>{toPlayerFacingText(block.message, '')}</p>
        </div>
      );
    }

    case 'image':
      return (
        <figure className="block-image">
          {block.url ? (
            <img src={block.url} alt={block.altText || block.title} loading="lazy" />
          ) : (
            <p className="block-text--muted">Изображение недоступно</p>
          )}
          {block.title && <figcaption>{block.title}</figcaption>}
        </figure>
      );

    case 'map':
      return (
        <div className="block-map">
          <h4>{toPlayerFacingText(block.title, 'Карта')}</h4>
          <p className="block-text--muted">Карта: {block.map.nodes.length} точек, {block.map.links.length} связей</p>
          <ul className="block-map__nodes">
            {block.map.nodes.slice(0, 20).map((node) => (
              <li key={node.id} className={node.isCurrent ? 'is-current' : ''}>
                {node.label} {node.isCurrent && '← вы здесь'}
              </li>
            ))}
            {block.map.nodes.length > 20 && <li className="block-text--muted">…и ещё {block.map.nodes.length - 20}</li>}
          </ul>
        </div>
      );

    case 'rawJson':
      return (
        <details className="block-raw">
          <summary>{toPlayerFacingText(block.title, 'Данные')}</summary>
          <pre>{JSON.stringify(block.json, null, 2)}</pre>
        </details>
      );
  }
}

export function BlockList({ blocks }: { blocks: UiBlock[] }) {
  return (
    <div className="block-list-container">
      {blocks.map((block, i) => <BlockRenderer key={`${block.kind}-${i}`} block={block} />)}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx
git commit -m "feat(web): add BlockRenderer — recursive UiBlock rendering"
```

---

### Task 3: Create CommandResultView — full-screen command output

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/CommandResultView.tsx`

- [ ] **Step 1: Create CommandResultView.tsx**

Shows command result with "back to scene" button, action buttons, and prompt forms:

```tsx
import { useState, type FormEvent } from 'react';
import { browserApi } from '../api/client';
import type { ExplorerCommandResult, JsonValue, UiAction } from '../api/contracts';
import { useShell } from '../context/ShellContext';
import { BlockList } from './BlockRenderer';
import { PromptForm, type PromptAnswers } from './PromptForm';

export function CommandResultView() {
  const { commandResult, clearCommandResult, executeCommand, loadBrowserState } = useShell();
  const [promptAnswers, setPromptAnswers] = useState<PromptAnswers>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [localResult, setLocalResult] = useState<ExplorerCommandResult | null>(null);

  const result = localResult ?? commandResult;
  if (!result) return null;

  function handleActionClick(action: UiAction) {
    if (action.requiresConfirmation && !confirm(`Выполнить: ${action.label}?`)) return;
    void executeCommand(action.command);
  }

  function handlePromptAnswerChange(promptId: string, value: JsonValue | undefined) {
    setPromptAnswers((prev) => ({ ...prev, [promptId]: value }));
  }

  async function handlePromptSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!result?.interactiveSession) return;
    setIsSubmitting(true);
    try {
      const response = await browserApi.submitPromptSession({
        sessionId: result.interactiveSession.sessionId,
        answers: promptAnswers
      });
      if (response.ok) {
        setLocalResult(response.data);
        setPromptAnswers({});
      }
      void loadBrowserState();
    } catch { /* handled by notice */ }
    setIsSubmitting(false);
  }

  return (
    <div className="command-result-view">
      <div className="command-result-view__header">
        <button type="button" className="btn-back" onClick={clearCommandResult}>
          ← Назад к сцене
        </button>
        <span className="command-result-view__command">{result.command}</span>
      </div>

      {result.notifications.length > 0 && (
        <div className="command-result-view__notifications">
          {result.notifications.map((n, i) => (
            <div key={i} className={`block-message block-message--${n.severity.toLowerCase()}`}>
              <strong>{n.title}</strong>
              <p>{n.message}</p>
            </div>
          ))}
        </div>
      )}

      <div className="command-result-view__content">
        <BlockList blocks={result.blocks} />
      </div>

      {result.actions.length > 0 && (
        <div className="command-result-view__actions">
          {result.actions.map((action) => (
            <button
              key={action.id}
              type="button"
              className={`btn-action btn-action--${action.style.toLowerCase()}`}
              onClick={() => handleActionClick(action)}
            >
              {action.label}
            </button>
          ))}
        </div>
      )}

      {result.interactiveSession && result.prompts.length > 0 && (
        <div className="command-result-view__prompts">
          <PromptForm
            prompts={result.prompts}
            promptAnswers={promptAnswers}
            onPromptAnswerChange={handlePromptAnswerChange}
            onSubmit={handlePromptSubmit}
            isSubmitting={isSubmitting}
          />
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/CommandResultView.tsx
git commit -m "feat(web): add CommandResultView — full command output display"
```

---

### Task 4: Create SceneView — narrative display

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/SceneView.tsx`

- [ ] **Step 1: Create SceneView.tsx**

Displays narrative, dialogues, quick actions. Delegates to CommandResultView when a command has been executed:

```tsx
import { isSuccess, useShell } from '../context/ShellContext';
import { SceneHero } from './SceneHero';
import { CommandResultView } from './CommandResultView';
import { useSceneImage } from '../hooks/useSceneImage';
import { toPlayerFacingText } from '../utils/playerCopy';

export function SceneView() {
  const { readyState, isCommandView, executeCommand } = useShell();

  if (isCommandView) {
    return <CommandResultView />;
  }

  const game = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  if (!game) {
    return (
      <div className="scene-empty">
        <p>Игровая сессия не загружена. Откройте настройки или загрузите сохранение.</p>
      </div>
    );
  }

  return <SceneContent game={game} onCommand={executeCommand} />;
}

function SceneContent({ game, onCommand }: {
  game: NonNullable<ReturnType<typeof useShell>['gameScreen']>;
  onCommand: (cmd: string) => Promise<void>;
}) {
  const sceneImage = useSceneImage(game.narrative.imagePrompt, game.media.gallery ?? []);

  return (
    <div className="scene-view">
      <SceneHero
        imageUrl={sceneImage.url}
        eyebrow={`Ход ${game.world.turnNumber}`}
        title={game.theme.label}
        subtitle={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || ''}`}
        loading={sceneImage.loading}
      />

      <article className="scene-narrative">
        <p>{game.narrative.text || 'Нарратив ещё не получен от ГМа.'}</p>
      </article>

      {game.narrative.combatLog && (
        <section className="scene-combat-log">
          <h3>⚔️ Журнал боя</h3>
          <div>{game.narrative.combatLog.split('\n').map((line, i) => <p key={i}>{line}</p>)}</div>
        </section>
      )}

      {game.narrative.dialogueOptions.length > 0 && (
        <section className="scene-dialogues">
          <h3>💬 Варианты диалога</h3>
          <div className="scene-dialogues__list">
            {game.narrative.dialogueOptions.map((opt) => (
              <button
                key={opt.id}
                type="button"
                className="scene-dialogue-chip"
                onClick={() => void onCommand(`/player_action ${opt.text}`)}
              >
                {toPlayerFacingText(opt.text, 'вариант')}
              </button>
            ))}
          </div>
        </section>
      )}

      {game.actionComposer.canSubmit && game.actionMenu.sections.length > 0 && (
        <section className="scene-quick-actions">
          <h4>Быстрые действия</h4>
          <div className="scene-quick-actions__list">
            {game.actionMenu.sections
              .filter((s) => s.playerDefault)
              .flatMap((s) => s.actions)
              .filter((a) => a.playerDefault && a.enabled)
              .slice(0, 8)
              .map((action) => (
                <button
                  key={action.id}
                  type="button"
                  className="scene-action-chip"
                  onClick={() => void onCommand(action.advancedCommand)}
                >
                  {action.label}
                </button>
              ))}
          </div>
        </section>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/SceneView.tsx
git commit -m "feat(web): add SceneView — narrative + quick actions"
```

---

### Task 5: Create HelpView — command catalog with search

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/HelpView.tsx`

- [ ] **Step 1: Create HelpView.tsx**

Searchable command catalog organized by categories:

```tsx
import { useMemo, useState } from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import type { BrowserCommandCoverageEntryDto } from '../api/contracts';

interface CommandGroup {
  label: string;
  icon: string;
  commands: BrowserCommandCoverageEntryDto[];
}

const GROUP_ICONS: Record<string, string> = {
  UniversalMeta: '🎭',
  MortalWorld: '🗺️',
  ChaosSea: '🌊',
  ShiningAbode: '✨',
  AfterlifeCombat: '⚔️',
  LifecycleLocalTurn: '🔧',
  Help: '❓',
  Math: '🧮'
};

const GROUP_LABELS: Record<string, string> = {
  UniversalMeta: 'Персонаж и душа',
  MortalWorld: 'Мир смертных',
  ChaosSea: 'Море Хаоса',
  ShiningAbode: 'Сияющая Обитель',
  AfterlifeCombat: 'Духовный бой',
  LifecycleLocalTurn: 'Системные',
  Help: 'Справка',
  Math: 'Утилиты'
};

export function HelpView() {
  const { readyState, executeCommand, setActiveTab } = useShell();
  const [search, setSearch] = useState('');
  const [expandedGroup, setExpandedGroup] = useState<string | null>(null);

  const coverage = readyState?.commandCoverage;
  const commands = coverage && isSuccess(coverage) ? coverage.data.commands : [];

  const groups = useMemo(() => {
    const map = new Map<string, BrowserCommandCoverageEntryDto[]>();
    for (const cmd of commands) {
      if (cmd.browserStatus === 'not-browser-executable') continue;
      const key = cmd.handlerKind;
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(cmd);
    }
    const result: CommandGroup[] = [];
    for (const [key, cmds] of map) {
      result.push({
        label: GROUP_LABELS[key] || key,
        icon: GROUP_ICONS[key] || '📋',
        commands: cmds
      });
    }
    return result;
  }, [commands]);

  const filteredGroups = useMemo(() => {
    if (!search.trim()) return groups;
    const q = search.toLowerCase();
    return groups
      .map((g) => ({
        ...g,
        commands: g.commands.filter((cmd) =>
          cmd.aliases.some((a) => a.toLowerCase().includes(q)) ||
          cmd.primaryActionLabel.toLowerCase().includes(q) ||
          cmd.id.toLowerCase().includes(q)
        )
      }))
      .filter((g) => g.commands.length > 0);
  }, [groups, search]);

  function handleCommandClick(command: string) {
    void executeCommand(command);
    setActiveTab('scene');
  }

  if (!coverage || !isSuccess(coverage)) {
    return (
      <div className="help-view">
        <p className="block-text--muted">Загрузка каталога команд… Включите расширенный режим в настройках для полного доступа.</p>
      </div>
    );
  }

  return (
    <div className="help-view">
      <div className="help-view__search">
        <input
          type="text"
          placeholder="🔍 Поиск команды..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="help-search-input"
        />
      </div>

      <div className="help-view__categories">
        {filteredGroups.map((group) => {
          const isExpanded = expandedGroup === group.label || search.trim().length > 0;
          return (
            <div key={group.label} className="help-category">
              <button
                type="button"
                className="help-category__header"
                onClick={() => setExpandedGroup(isExpanded && !search ? null : group.label)}
              >
                <span className="help-category__icon">{group.icon}</span>
                <span className="help-category__label">{group.label}</span>
                <span className="help-category__count">{group.commands.length}</span>
                <span className="help-category__chevron">{isExpanded ? '▾' : '▸'}</span>
              </button>
              {isExpanded && (
                <div className="help-category__commands">
                  {group.commands.map((cmd) => (
                    <button
                      key={cmd.id}
                      type="button"
                      className="help-command"
                      onClick={() => handleCommandClick(cmd.primaryCommand)}
                    >
                      <span className="help-command__alias">{cmd.aliases[0]}</span>
                      <span className="help-command__label">{cmd.primaryActionLabel}</span>
                      <span className="help-command__run">▶</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/HelpView.tsx
git commit -m "feat(web): add HelpView — searchable command catalog"
```

---

### Task 6: Create StatusView — character summary

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/StatusView.tsx`

- [ ] **Step 1: Create StatusView.tsx**

Quick character status display using game screen data:

```tsx
import { isSuccess, useShell } from '../context/ShellContext';

export function StatusView() {
  const { readyState } = useShell();
  const game = readyState && isSuccess(readyState.game) ? readyState.game.data : null;

  if (!game) {
    return <div className="status-view"><p className="block-text--muted">Данные недоступны.</p></div>;
  }

  const { player, soul, world, afterlife } = game;

  return (
    <div className="status-view">
      <section className="status-card">
        <h3>🎭 Персонаж</h3>
        <dl className="block-kv">
          <div className="block-kv__row"><dt>Имя</dt><dd>{player.name}</dd></div>
          <div className="block-kv__row"><dt>Класс</dt><dd>{player.class}</dd></div>
          <div className="block-kv__row"><dt>Раса</dt><dd>{player.race}</dd></div>
          <div className="block-kv__row"><dt>Состояние</dt><dd>{player.currentCondition}</dd></div>
        </dl>
        <div className="status-bars">
          <StatusBar label="❤️ Здоровье" value={player.healthPercentage} color="var(--color-error)" />
          <StatusBar label="⚡ Энергия" value={player.energyPercentage} color="var(--color-accent)" />
          <StatusBar label="🛡️ Самообладание" value={player.poisePercentage} color="var(--color-success)" />
        </div>
        {player.activeConditions.length > 0 && (
          <div className="status-conditions">
            <h4>Активные состояния</h4>
            <ul>{player.activeConditions.map((c, i) => <li key={i}>{c}</li>)}</ul>
          </div>
        )}
      </section>

      <section className="status-card">
        <h3>🕯️ Душа</h3>
        <dl className="block-kv">
          <div className="block-kv__row"><dt>Имя души</dt><dd>{soul.name}</dd></div>
          <div className="block-kv__row"><dt>Царство</dt><dd>{soul.realm}</dd></div>
          <div className="block-kv__row"><dt>Инкарнация</dt><dd>{soul.incarnation}</dd></div>
          <div className="block-kv__row"><dt>Чернильные перья</dt><dd>{soul.inkFeathers}</dd></div>
          <div className="block-kv__row"><dt>Просветление</dt><dd>{soul.enlightenmentTier}</dd></div>
          <div className="block-kv__row"><dt>Хранитель</dt><dd>{soul.activeGuardianName}</dd></div>
        </dl>
      </section>

      <section className="status-card">
        <h3>🗺️ Мир</h3>
        <dl className="block-kv">
          <div className="block-kv__row"><dt>Локация</dt><dd>{world.location || '—'}</dd></div>
          <div className="block-kv__row"><dt>Время</dt><dd>{world.worldTime || '—'}</dd></div>
          <div className="block-kv__row"><dt>Ход</dt><dd>{world.turnNumber}</dd></div>
        </dl>
      </section>

      {(afterlife.shiningRadianceExperience > 0 || afterlife.shiningHallCount > 0) && (
        <section className="status-card">
          <h3>✨ Посмертие</h3>
          <dl className="block-kv">
            <div className="block-kv__row"><dt>Сияние</dt><dd>{afterlife.shiningRadianceExperience} (уровень {afterlife.shiningRadianceTier})</dd></div>
            <div className="block-kv__row"><dt>Искры света</dt><dd>{afterlife.shiningLightSparks}</dd></div>
            <div className="block-kv__row"><dt>Залы</dt><dd>{afterlife.shiningHallCount}</dd></div>
            <div className="block-kv__row"><dt>Фракции</dt><dd>{afterlife.shiningFactionCount}</dd></div>
          </dl>
        </section>
      )}
    </div>
  );
}

function StatusBar({ label, value, color }: { label: string; value: string; color: string }) {
  const numValue = parseInt(value) || 0;
  return (
    <div className="status-bar">
      <div className="status-bar__label">{label} <span>{value}</span></div>
      <div className="status-bar__track">
        <div className="status-bar__fill" style={{ width: `${numValue}%`, background: color }} />
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/StatusView.tsx
git commit -m "feat(web): add StatusView — character status summary"
```

---

### Task 7: Create SettingsView — consolidated settings

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/SettingsView.tsx`

- [ ] **Step 1: Create SettingsView.tsx**

Consolidates existing settings logic from SettingsRoute into new component. Reuses the same API calls and state patterns:

```tsx
import { useCallback, useEffect, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserApiResult, BrowserClientSettingsDto } from '../api/contracts';
import { isSuccess, useShell } from '../context/ShellContext';

export function SettingsView() {
  const { readyState, advancedEnabled, setAdvancedEnabled, loadBrowserState } = useShell();
  const [settings, setSettings] = useState<BrowserClientSettingsDto | null>(null);
  const updateQueue = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (readyState && isSuccess(readyState.settings)) {
      setSettings(readyState.settings.data);
    }
  }, [readyState]);

  const debouncedUpdate = useCallback((patch: Record<string, unknown>) => {
    if (updateQueue.current) clearTimeout(updateQueue.current);
    updateQueue.current = setTimeout(() => {
      void browserApi.updateClientSettings(patch).then(() => void loadBrowserState());
    }, 500);
  }, [loadBrowserState]);

  if (!settings) {
    return <div className="settings-view"><p className="block-text--muted">Загрузка настроек…</p></div>;
  }

  return (
    <div className="settings-view">
      <section className="settings-card">
        <h3>⚙️ Основные</h3>
        <div className="settings-row">
          <label>Язык клиента</label>
          <select
            value={settings.language}
            onChange={(e) => { setSettings({ ...settings, language: e.target.value }); debouncedUpdate({ language: e.target.value }); }}
          >
            <option value="RU">Русский</option>
            <option value="EN">English</option>
          </select>
        </div>
        <div className="settings-row">
          <label>Сложность</label>
          <span className="settings-value">{settings.difficulty}</span>
        </div>
        <div className="settings-row">
          <label>Показывать мысли ГМа</label>
          <input
            type="checkbox"
            checked={settings.showGmThoughts}
            onChange={(e) => { setSettings({ ...settings, showGmThoughts: e.target.checked }); debouncedUpdate({ showGmThoughts: e.target.checked }); }}
          />
        </div>
      </section>

      <section className="settings-card">
        <h3>🔊 Звук</h3>
        <div className="settings-row">
          <label>Музыка</label>
          <input
            type="checkbox"
            checked={settings.audio.musicEnabled}
            onChange={(e) => { setSettings({ ...settings, audio: { ...settings.audio, musicEnabled: e.target.checked } }); debouncedUpdate({ musicEnabled: e.target.checked }); }}
          />
        </div>
        <div className="settings-row">
          <label>Громкость звуков</label>
          <input
            type="range"
            min="0"
            max="100"
            value={settings.audio.soundVolume}
            onChange={(e) => {
              const v = Number(e.target.value);
              setSettings({ ...settings, audio: { ...settings.audio, soundVolume: v } });
              debouncedUpdate({ soundVolume: v });
            }}
          />
          <span>{settings.audio.soundVolume}%</span>
        </div>
      </section>

      <section className="settings-card">
        <h3>♿ Доступность</h3>
        <div className="settings-row">
          <label>Размер шрифта</label>
          <input
            type="range"
            min="80"
            max="150"
            value={settings.accessibility.fontScalePercent}
            onChange={(e) => {
              const v = Number(e.target.value);
              setSettings({ ...settings, accessibility: { ...settings.accessibility, fontScalePercent: v } });
              debouncedUpdate({ browserFontScalePercent: v });
            }}
          />
          <span>{settings.accessibility.fontScalePercent}%</span>
        </div>
        <div className="settings-row">
          <label>Уменьшить анимации</label>
          <input
            type="checkbox"
            checked={settings.accessibility.reducedMotion}
            onChange={(e) => { setSettings({ ...settings, accessibility: { ...settings.accessibility, reducedMotion: e.target.checked } }); debouncedUpdate({ browserReducedMotion: e.target.checked }); }}
          />
        </div>
      </section>

      <section className="settings-card">
        <h3>🔧 Расширенный режим</h3>
        <div className="settings-row">
          <label>Показывать технические данные</label>
          <input
            type="checkbox"
            checked={advancedEnabled}
            onChange={() => setAdvancedEnabled((v) => !v)}
          />
        </div>
        <p className="block-text--muted">Включает доступ к полному каталогу команд, JSON-блокам и диагностике.</p>
      </section>
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/SettingsView.tsx
git commit -m "feat(web): add SettingsView — consolidated settings"
```

---

### Task 8: Create TabBar component

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/TabBar.tsx`

- [ ] **Step 1: Create TabBar.tsx**

```tsx
import { useShell, type TabId } from '../context/ShellContext';

interface TabDef {
  id: TabId;
  icon: string;
  label: string;
}

const TABS: TabDef[] = [
  { id: 'scene', icon: '📖', label: 'Сцена' },
  { id: 'status', icon: '📊', label: 'Статус' },
  { id: 'help', icon: '❓', label: 'Помощь' },
  { id: 'settings', icon: '⚙️', label: 'Настройки' }
];

export function TabBar() {
  const { activeTab, setActiveTab, gameScreen } = useShell();

  return (
    <nav className="tab-bar" role="tablist" aria-label="Навигация">
      <div className="tab-bar__tabs">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            className={`tab-bar__tab${activeTab === tab.id ? ' is-active' : ''}`}
            aria-selected={activeTab === tab.id}
            onClick={() => setActiveTab(tab.id)}
          >
            <span className="tab-bar__icon">{tab.icon}</span>
            <span className="tab-bar__label">{tab.label}</span>
          </button>
        ))}
      </div>
      {gameScreen && (
        <div className="tab-bar__info">
          Ход {gameScreen.world.turnNumber} • {gameScreen.world.location || '—'}
        </div>
      )}
    </nav>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/TabBar.tsx
git commit -m "feat(web): add TabBar — 4-tab navigation"
```

---

### Task 9: Create UnifiedInput with autocomplete

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/components/UnifiedInput.tsx`
- Create: `BookOfEternityClient.WebFrontend/src/components/CommandAutocomplete.tsx`

- [ ] **Step 1: Create CommandAutocomplete.tsx**

```tsx
import type { BrowserCommandCoverageEntryDto } from '../api/contracts';

interface Props {
  commands: BrowserCommandCoverageEntryDto[];
  query: string;
  onSelect: (command: string) => void;
}

export function CommandAutocomplete({ commands, query, onSelect }: Props) {
  const q = query.replace(/^\//, '').toLowerCase();
  if (!q) return null;

  const matches = commands
    .filter((cmd) =>
      cmd.aliases.some((a) => a.replace(/^\//, '').toLowerCase().startsWith(q)) ||
      cmd.primaryActionLabel.toLowerCase().includes(q)
    )
    .slice(0, 8);

  if (matches.length === 0) return null;

  return (
    <div className="autocomplete-dropdown">
      {matches.map((cmd) => (
        <button
          key={cmd.id}
          type="button"
          className="autocomplete-item"
          onMouseDown={(e) => { e.preventDefault(); onSelect(cmd.primaryCommand); }}
        >
          <span className="autocomplete-item__alias">{cmd.aliases[0]}</span>
          <span className="autocomplete-item__label">{cmd.primaryActionLabel}</span>
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Create UnifiedInput.tsx**

```tsx
import { useRef, useState } from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import { CommandAutocomplete } from './CommandAutocomplete';

export function UnifiedInput() {
  const { composerText, setComposerText, composerNotice, submitComposer, readyState, gameScreen } = useShell();
  const [showAutocomplete, setShowAutocomplete] = useState(false);
  const inputRef = useRef<HTMLTextAreaElement>(null);

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
    if (e.key === 'Escape') setShowAutocomplete(false);
  }

  return (
    <div className="unified-input">
      {composerNotice && <p className="unified-input__notice">{composerNotice}</p>}
      <form className="unified-input__form" onSubmit={submitComposer}>
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

- [ ] **Step 3: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/components/CommandAutocomplete.tsx BookOfEternityClient.WebFrontend/src/components/UnifiedInput.tsx
git commit -m "feat(web): add UnifiedInput with command autocomplete"
```

---

### Task 10: Rewrite App.tsx — new shell layout

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`

- [ ] **Step 1: Replace App.tsx with new layout**

Remove all old route imports, Sidebar, PlayerStatusSidebar. Replace with TabBar + content + UnifiedInput:

```tsx
import { type CSSProperties } from 'react';
import './styles.css';
import { ConnectionBanner } from './components/ConnectionBanner';
import { ErrorNotice } from './components/ErrorNotice';
import { LoadingCard } from './components/LoadingCard';
import { TabBar } from './components/TabBar';
import { SceneView } from './components/SceneView';
import { StatusView } from './components/StatusView';
import { HelpView } from './components/HelpView';
import { SettingsView } from './components/SettingsView';
import { UnifiedInput } from './components/UnifiedInput';
import { ShellProvider, useShell, type TabId } from './context/ShellContext';

export default function App() {
  return (
    <ShellProvider>
      <AppShell />
    </ShellProvider>
  );
}

function AppShell() {
  const { advancedEnabled, clientSettings, readyState, realmTheme, shellState, activeTab } = useShell();
  const browserShellClassName = [
    'browser-shell',
    clientSettings?.accessibility.reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`
  } as CSSProperties;

  return (
    <main className={browserShellClassName} data-theme-key={realmTheme.key} style={browserShellStyle}>
      <ConnectionBanner />
      <TabBar />
      <section className="content-area" aria-live="polite">
        {shellState.status === 'loading' && <LoadingCard />}
        {shellState.status === 'error' && <ErrorNotice title="Состояние клиента недоступно" failure={shellState} advancedEnabled={advancedEnabled} />}
        {readyState && <TabContent activeTab={activeTab} />}
      </section>
      <UnifiedInput />
    </main>
  );
}

function TabContent({ activeTab }: { activeTab: TabId }) {
  switch (activeTab) {
    case 'scene': return <SceneView />;
    case 'status': return <StatusView />;
    case 'help': return <HelpView />;
    case 'settings': return <SettingsView />;
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/App.tsx
git commit -m "refactor(web): rewrite App.tsx — 4-tab command-driven layout"
```

---

### Task 11: Add CSS for new components

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles.css` (append new styles)

- [ ] **Step 1: Add styles for TabBar, SceneView, HelpView, StatusView, UnifiedInput, BlockRenderer, CommandResultView**

Append to `styles.css`:

```css
/* ===== Tab Bar ===== */
.tab-bar {
  display: flex;
  align-items: center;
  background: var(--surface-raised, #161b22);
  border-bottom: 1px solid var(--border, #30363d);
  padding: 0.5rem 1rem;
  gap: 0.25rem;
}
.tab-bar__tabs { display: flex; gap: 0.25rem; }
.tab-bar__tab {
  display: flex; align-items: center; gap: 0.35rem;
  padding: 0.4rem 0.9rem; border-radius: 6px;
  background: transparent; border: none;
  color: var(--text-muted, #8b949e); font-size: 0.85rem;
  cursor: pointer; transition: background 0.15s, color 0.15s;
}
.tab-bar__tab:hover { background: var(--surface-hover, #1f2937); color: var(--text-primary, #c9d1d9); }
.tab-bar__tab.is-active { background: var(--color-accent, #1f6feb); color: #fff; font-weight: 600; }
.tab-bar__info { margin-left: auto; color: var(--text-muted, #484f58); font-size: 0.75rem; }

/* ===== Content Area ===== */
.content-area {
  flex: 1; overflow-y: auto;
  padding: 1.25rem 1.5rem;
  max-width: 850px; margin: 0 auto; width: 100%;
}

/* ===== Scene View ===== */
.scene-view { display: flex; flex-direction: column; gap: 1rem; }
.scene-narrative { color: var(--text-primary, #c9d1d9); font-size: 1rem; line-height: 1.75; }
.scene-combat-log { background: var(--surface-raised, #161b22); border-radius: 8px; padding: 1rem; border-left: 3px solid var(--color-warning, #f0883e); }
.scene-combat-log h3 { font-size: 0.85rem; color: var(--color-warning); margin-bottom: 0.5rem; }
.scene-dialogues h3 { font-size: 0.85rem; color: var(--color-success, #238636); margin-bottom: 0.5rem; }
.scene-dialogues__list { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.scene-dialogue-chip, .scene-action-chip {
  background: var(--surface-raised, #1f2937); border: 1px solid var(--border, #30363d);
  border-radius: 6px; padding: 0.4rem 0.75rem;
  color: var(--text-primary, #c9d1d9); font-size: 0.8rem;
  cursor: pointer; transition: border-color 0.15s;
}
.scene-dialogue-chip:hover, .scene-action-chip:hover { border-color: var(--color-accent, #58a6ff); }
.scene-quick-actions h4 { font-size: 0.75rem; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 0.5rem; }
.scene-quick-actions__list { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.scene-empty { display: flex; align-items: center; justify-content: center; min-height: 40vh; color: var(--text-muted); }

/* ===== Command Result View ===== */
.command-result-view { display: flex; flex-direction: column; gap: 1rem; }
.command-result-view__header { display: flex; align-items: center; gap: 0.75rem; padding-bottom: 0.75rem; border-bottom: 1px solid var(--border, #21262d); }
.command-result-view__command { color: var(--color-warning, #f0883e); font-size: 0.85rem; }
.command-result-view__actions { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.5rem; }
.btn-back {
  background: var(--surface-raised, #1f2937); border: 1px solid var(--border, #30363d);
  border-radius: 6px; padding: 0.35rem 0.7rem;
  color: var(--color-accent, #58a6ff); font-size: 0.8rem; cursor: pointer;
}
.btn-back:hover { border-color: var(--color-accent); }
.btn-action {
  background: var(--surface-raised, #1f2937); border: 1px solid var(--border, #30363d);
  border-radius: 6px; padding: 0.4rem 0.75rem;
  color: var(--text-primary, #c9d1d9); font-size: 0.8rem; cursor: pointer;
}
.btn-action--primary { background: var(--color-accent, #1f6feb); color: #fff; border-color: var(--color-accent); }
.btn-action--danger { background: #da3633; color: #fff; border-color: #da3633; }

/* ===== Block Renderer ===== */
.block-text { margin: 0.25rem 0; line-height: 1.6; }
.block-text--muted { color: var(--text-muted, #8b949e); }
.block-text--subtle { color: var(--text-muted, #8b949e); font-style: italic; }
.block-text--accent { color: var(--color-accent, #58a6ff); }
.block-text--success { color: var(--color-success, #7ee787); }
.block-text--warning { color: var(--color-warning, #f0883e); }
.block-text--error { color: var(--color-error, #f85149); }
.block-panel { background: var(--surface-raised, #161b22); border-radius: 8px; padding: 1rem; border: 1px solid var(--border, #30363d); margin: 0.5rem 0; }
.block-panel__title { font-size: 0.9rem; color: var(--color-warning, #f0883e); font-weight: 600; margin-bottom: 0.5rem; }
.block-panel__body { display: flex; flex-direction: column; gap: 0.25rem; }
.block-table { margin: 0.5rem 0; }
.block-table__title { font-size: 0.85rem; color: var(--text-muted); margin-bottom: 0.5rem; }
.block-table__scroll { overflow-x: auto; }
.block-table table { width: 100%; border-collapse: collapse; font-size: 0.82rem; }
.block-table th { text-align: left; padding: 0.4rem 0.6rem; color: var(--text-muted); border-bottom: 1px solid var(--border); }
.block-table td { padding: 0.4rem 0.6rem; color: var(--text-primary); border-bottom: 1px solid var(--border-subtle, #21262d); }
.block-list { padding-left: 1.2rem; margin: 0.25rem 0; }
.block-list li { margin: 0.2rem 0; font-size: 0.85rem; }
.block-kv { display: grid; grid-template-columns: 1fr; gap: 0.25rem; margin: 0.25rem 0; }
.block-kv__row { display: flex; gap: 0.5rem; font-size: 0.85rem; padding: 0.3rem 0; border-bottom: 1px solid var(--border-subtle, #21262d); }
.block-kv__row dt { color: var(--text-muted); min-width: 120px; }
.block-kv__row dd { color: var(--text-primary); margin: 0; }
.block-message { padding: 0.75rem 1rem; border-radius: 6px; margin: 0.5rem 0; border-left: 3px solid; }
.block-message--info { border-color: var(--color-accent); background: rgba(31, 111, 235, 0.08); }
.block-message--success { border-color: var(--color-success); background: rgba(35, 134, 54, 0.08); }
.block-message--warning { border-color: var(--color-warning); background: rgba(240, 136, 62, 0.08); }
.block-message--error { border-color: var(--color-error); background: rgba(248, 81, 73, 0.08); }
.block-message strong { display: block; margin-bottom: 0.25rem; font-size: 0.85rem; }
.block-message p { margin: 0; font-size: 0.82rem; color: var(--text-primary); }
.block-image img { max-width: 100%; border-radius: 8px; }
.block-image figcaption { color: var(--text-muted); font-size: 0.75rem; margin-top: 0.25rem; }
.block-map { margin: 0.5rem 0; }
.block-map__nodes { list-style: none; padding: 0; font-size: 0.82rem; }
.block-map__nodes li { padding: 0.2rem 0; }
.block-map__nodes .is-current { color: var(--color-accent); font-weight: 600; }
.block-raw summary { color: var(--text-muted); font-size: 0.8rem; cursor: pointer; }
.block-raw pre { background: var(--surface-raised); padding: 0.5rem; border-radius: 4px; font-size: 0.7rem; overflow-x: auto; max-height: 300px; }
.block-list-container { display: flex; flex-direction: column; gap: 0.25rem; }

/* ===== Help View ===== */
.help-view { display: flex; flex-direction: column; gap: 1rem; }
.help-search-input {
  width: 100%; padding: 0.65rem 1rem; border-radius: 8px;
  background: var(--surface-raised, #161b22); border: 1px solid var(--border, #30363d);
  color: var(--text-primary, #c9d1d9); font-size: 0.9rem;
}
.help-search-input::placeholder { color: var(--text-muted); }
.help-category { margin-bottom: 0.25rem; }
.help-category__header {
  display: flex; align-items: center; gap: 0.5rem; width: 100%;
  padding: 0.6rem 0; background: none; border: none; border-bottom: 1px solid var(--border-subtle, #21262d);
  color: var(--color-warning, #f0883e); font-size: 0.85rem; font-weight: 600; cursor: pointer;
}
.help-category__count { color: var(--text-muted); font-size: 0.75rem; font-weight: normal; }
.help-category__chevron { margin-left: auto; color: var(--text-muted); }
.help-category__commands { padding: 0.5rem 0 0.5rem 1rem; display: flex; flex-direction: column; gap: 0.35rem; }
.help-command {
  display: flex; align-items: center; gap: 0.75rem; width: 100%;
  padding: 0.5rem 0.75rem; background: var(--surface-raised, #161b22);
  border: 1px solid var(--border, #30363d); border-radius: 6px;
  cursor: pointer; transition: border-color 0.15s;
}
.help-command:hover { border-color: var(--color-accent, #58a6ff); }
.help-command__alias { color: var(--color-accent, #58a6ff); font-size: 0.82rem; min-width: 140px; }
.help-command__label { color: var(--text-muted); font-size: 0.8rem; flex: 1; text-align: left; }
.help-command__run { color: var(--text-muted); font-size: 0.7rem; }

/* ===== Status View ===== */
.status-view { display: flex; flex-direction: column; gap: 1rem; }
.status-card { background: var(--surface-raised, #161b22); border-radius: 8px; padding: 1rem; border: 1px solid var(--border, #30363d); }
.status-card h3 { font-size: 0.9rem; margin-bottom: 0.75rem; color: var(--text-primary); }
.status-bars { display: flex; flex-direction: column; gap: 0.5rem; margin-top: 0.75rem; }
.status-bar__label { display: flex; justify-content: space-between; font-size: 0.8rem; color: var(--text-muted); margin-bottom: 0.2rem; }
.status-bar__track { height: 6px; background: var(--border, #30363d); border-radius: 3px; overflow: hidden; }
.status-bar__fill { height: 100%; border-radius: 3px; transition: width 0.3s; }
.status-conditions h4 { font-size: 0.8rem; color: var(--text-muted); margin: 0.5rem 0 0.25rem; }
.status-conditions ul { list-style: none; padding: 0; font-size: 0.82rem; }
.status-conditions li { padding: 0.15rem 0; }

/* ===== Settings View ===== */
.settings-view { display: flex; flex-direction: column; gap: 1rem; }
.settings-card { background: var(--surface-raised, #161b22); border-radius: 8px; padding: 1rem; border: 1px solid var(--border, #30363d); }
.settings-card h3 { font-size: 0.9rem; margin-bottom: 0.75rem; }
.settings-row { display: flex; align-items: center; justify-content: space-between; padding: 0.5rem 0; border-bottom: 1px solid var(--border-subtle, #21262d); font-size: 0.85rem; }
.settings-row label { color: var(--text-primary); }
.settings-row select, .settings-row input[type="range"] { accent-color: var(--color-accent); }
.settings-value { color: var(--text-muted); }

/* ===== Unified Input ===== */
.unified-input { border-top: 1px solid var(--border, #30363d); padding: 0.75rem 1.5rem; background: var(--surface-base, #0d1117); }
.unified-input__notice { color: var(--color-warning, #f0883e); font-size: 0.8rem; margin-bottom: 0.5rem; }
.unified-input__form { display: flex; align-items: center; gap: 0.75rem; }
.unified-input__wrapper { position: relative; flex: 1; }
.unified-input__textarea {
  width: 100%; resize: none; padding: 0.6rem 0.9rem; border-radius: 8px;
  background: var(--surface-raised, #161b22); border: 1px solid var(--border, #30363d);
  color: var(--text-primary, #c9d1d9); font-size: 0.9rem; line-height: 1.4;
}
.unified-input__textarea::placeholder { color: var(--text-muted); }
.unified-input__submit {
  background: var(--color-success, #238636); color: #fff; border: none;
  border-radius: 8px; padding: 0.55rem 1.1rem; font-size: 0.85rem; font-weight: 600;
  cursor: pointer; transition: opacity 0.15s;
}
.unified-input__submit:disabled { opacity: 0.5; cursor: not-allowed; }

/* ===== Autocomplete ===== */
.autocomplete-dropdown {
  position: absolute; bottom: 100%; left: 0; right: 0;
  background: var(--surface-raised, #161b22); border: 1px solid var(--border, #30363d);
  border-radius: 8px; max-height: 240px; overflow-y: auto;
  box-shadow: 0 -4px 12px rgba(0,0,0,0.3); z-index: 100;
}
.autocomplete-item {
  display: flex; align-items: center; gap: 0.75rem; width: 100%;
  padding: 0.5rem 0.75rem; background: none; border: none; border-bottom: 1px solid var(--border-subtle, #21262d);
  cursor: pointer; text-align: left;
}
.autocomplete-item:hover { background: var(--surface-hover, #1f2937); }
.autocomplete-item__alias { color: var(--color-accent, #58a6ff); font-size: 0.82rem; min-width: 120px; }
.autocomplete-item__label { color: var(--text-muted); font-size: 0.8rem; }

/* ===== Layout Override ===== */
.browser-shell {
  display: flex; flex-direction: column; height: 100vh;
  background: var(--surface-base, #0a0e12); color: var(--text-primary, #c9d1d9);
}
```

- [ ] **Step 2: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/styles.css
git commit -m "style(web): add CSS for command-driven UI components"
```

---

### Task 12: Delete obsolete files and fix imports

**Files:**
- Delete: All obsolete route and component files
- Modify: Any remaining import references

- [ ] **Step 1: Remove obsolete files**

```bash
cd BookOfEternityClient.WebFrontend
rm src/routes/HomeRoute.tsx src/routes/GameRoute.tsx src/routes/SoulRoute.tsx src/routes/WorldRoute.tsx src/routes/JournalRoute.tsx src/routes/InventoryRoute.tsx src/routes/MediaRoute.tsx src/routes/SettingsRoute.tsx
rm src/components/Sidebar.tsx src/components/navBarConfig.ts src/components/PlayerStatusSidebar.tsx src/components/ActionPalette.tsx src/components/Composer.tsx
rm src/components/ActionMenu.tsx src/components/ActionCard.tsx src/components/RebornSystemsPanel.tsx src/components/DetailSurface.tsx
rm src/utils/actionFilters.ts
```

Keep but don't import: `src/components/CommandResult.tsx` (legacy, may be needed by other code temporarily), `src/components/AdvancedDiagnostics.tsx` (optional — could keep for advanced mode later).

- [ ] **Step 2: Clean up any broken imports in remaining files**

Check `src/components/SceneHero.tsx`, `src/components/PromptForm.tsx`, `src/hooks/useSceneImage.ts`, `src/hooks/useShellState.ts` — ensure they don't import deleted files.

Run: `npx tsc --noEmit 2>&1 | head -30`

Fix any remaining TypeScript errors.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(web): remove obsolete 8-route layout files"
```

---

### Task 13: Build, test, and verify

- [ ] **Step 1: Build the frontend**

Run: `cd BookOfEternityClient.WebFrontend && npm run build 2>&1`

Expected: Vite build succeeds with no errors.

- [ ] **Step 2: Verify dev server works**

Run: `npm run dev` and open `http://localhost:5173`

Check:
- 4 tabs visible at top
- Scene tab shows narrative text and quick actions
- Help tab shows searchable command list
- Clicking a command in Help executes it and shows result on Scene tab
- "Back to scene" button works
- `/команда` in input shows autocomplete and executes
- Status tab shows character info
- Settings tab allows changing options

- [ ] **Step 3: Build the full C# project with embedded frontend**

Run: `cd BookOfEternityClient && dotnet build --nologo -q`

Expected: Build succeeds (frontend assets embedded).

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "feat(web): complete command-driven UI redesign

4 tabs (Scene/Status/Help/Settings), unified input with
autocomplete, full UiBlock rendering, searchable command catalog.
Removes 8-tab debug layout.

Closes partial: #762"
```

---

### Task 14: Enable command coverage without advanced mode

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/hooks/useShellState.ts`

- [ ] **Step 1: Always fetch command coverage**

The Help tab needs command coverage data regardless of `advancedEnabled`. Find the conditional fetch and make it always run:

In `useShellState.ts`, find the part where `getCommandCoverage` is only called when `advancedEnabled` is true, and change it to always call it. The lifecycle dashboard can remain advanced-only.

- [ ] **Step 2: Verify Help tab works without advanced mode**

Run dev server, ensure Help tab shows commands even with advanced mode off.

- [ ] **Step 3: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/hooks/useShellState.ts
git commit -m "fix(web): always fetch command coverage for Help tab"
```
