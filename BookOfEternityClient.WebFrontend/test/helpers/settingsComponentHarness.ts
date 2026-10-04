import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

// A deterministic handler-level harness, not a DOM/browser renderer. It executes
// the actual TSX component and tracker code with controlled hooks, effects,
// promises, timer dispatch and JSX handler props. Every test gets its own state.
export function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}
export const ok = (data: unknown) => ({ ok: true, status: 200, data });
export async function flushPromises() {
  for (let i = 0; i < 12; i++) await Promise.resolve();
}

export interface ElementNode { type: unknown; props: Record<string, any> }
export function nodes(node: any, result: ElementNode[] = []): ElementNode[] {
  if (!node || typeof node !== 'object') return result;
  if (Array.isArray(node)) { for (const child of node) nodes(child, result); return result; }
  result.push(node);
  nodes(node.props?.children, result);
  return result;
}

export function createSettingsComponentHarness() {
  const root = process.cwd(); // The category Vitest adapter sets the frontend cwd.
  let current: any;
  let timerId = 0;
  const timers = new Map<number, () => void>();
  const cache = new Map<string, any>();
  const load = deferred<any>();
  const save = deferred<any>();
  const audioWrite = deferred<any>();
  const persistedSettings = {
    language: { value: 'ru', choices: [] }, difficulty: { value: 'normal', choices: [] },
    showGmThoughts: false, accessibility: { fontScalePercent: 100, uiScalePercent: 100, reducedMotion: false },
    locality: { sessionLabel: 'test', gmBridgeLabel: 'test', safetySummary: 'test' }
  };
  const initialAudio = { musicEnabled: false, musicVolume: 17, soundEnabled: false, soundVolume: 20,
    playlists: [], cues: [], autoplayGuidance: '', missingAssetsMessage: '' };
  let refreshes = 0;
  let settingsPosts = 0;
  let audioPosts = 0;
  let refreshFailure = false;
  let savePosts = 0;
  let loadPosts = 0;
  let navigations = 0;
  let loadOwner: any = null;
  let navigation = 0;
  let saveLatch: any;
  let realRefreshClient: any;
  let realRefreshState: any = { status: 'loading' };
  const refreshOwner = { current: 0 };
  const api = {
    updateClientSettings: async () => { settingsPosts++; return ok(persistedSettings); },
    loadSave: () => { loadPosts++; return load.promise; },
    createSave: () => { savePosts++; return save.promise; },
    updateAudioSettings: () => { audioPosts++; return audioWrite.promise; }
  };
  const shell: any = {
    readyState: { settings: ok(persistedSettings), audio: ok(initialAudio) },
    menu: { session: { gameSessionExists: true, hasReadableSoul: true, canStartBrowserWrite: true }, saves: [{ saveId: 'save', displayName: 'save', description: '', scopeLabel: '', characterName: '', turnLabel: '' }] },
    advancedEnabled: false, activeRoute: 'settings', setAdvancedEnabled() {},
    setActiveRoute(route: string) { navigations++; navigation++; shell.activeRoute = route; },
    beginLoad: () => {
      if (loadOwner || shell.loadContinuationNotice || saveLatch?.isBlocked()) return null;
      loadOwner = { navigation }; return loadOwner;
    },
    isLoadCurrent: (owner: any) => loadOwner === owner && owner.navigation === navigation && !shell.loadContinuationNotice,
    finishLoad: (owner: any) => { if (loadOwner === owner) loadOwner = null; },
    blockLoadContinuation: (notice: any) => { shell.loadContinuationNotice ??= notice; },
    refreshAfterLoad: async (_generation: string, isCurrent: () => boolean = () => true) => {
      refreshes++;
      if (refreshFailure) return false;
      if (isCurrent()) shell.readyState = { settings: ok(persistedSettings), audio: ok(initialAudio) };
      return isCurrent();
    },
    loadBrowserState: async (isCurrent: () => boolean = () => true) => {
      if (saveLatch?.isBlocked()) return;
      refreshes++;
      if (realRefreshClient) {
        await module('src/hooks/loadShellState.ts').loadShellState(realRefreshClient, (next: any) => {
          realRefreshState = typeof next === 'function' ? next(realRefreshState) : next;
          shell.readyState = realRefreshState.status === 'ready' ? realRefreshState : null;
        }, refreshOwner, false);
        return;
      }
      if (refreshFailure) throw new Error('controlled refresh failure');
      if (isCurrent()) shell.readyState = { settings: ok(persistedSettings), audio: ok({ ...initialAudio, musicVolume: 99 }) };
    },
    refreshAfterSave: async (createdSaveId: string, isCurrent: () => boolean = () => true) => {
      if (saveLatch?.isBlocked()) return false;
      refreshes++;
      const reply = async (data: unknown) => {
        if (refreshFailure) throw new Error('controlled refresh response failure');
        return ok(data);
      };
      const client = realRefreshClient ?? {
        getMainMenu: () => reply({ ...shell.menu, saves: [{ saveId: createdSaveId }] }),
        getSessionStatus: () => reply({}), getGameScreen: () => reply({}),
        getAudioSettings: () => reply(initialAudio), getClientSettings: () => reply(persistedSettings),
        getCommandCoverage: () => reply({})
      };
      return module('src/hooks/refreshShellAfterSave.ts').refreshShellAfterSave(client, (next: any) => {
        realRefreshState = typeof next === 'function' ? next(realRefreshState) : next;
        shell.readyState = realRefreshState.status === 'ready' ? realRefreshState : null;
      }, refreshOwner, false, createdSaveId, isCurrent);
    },
    blockSaveContinuation: (notice: unknown) => {
      saveLatch ??= module('src/utils/savePersistenceNotice.ts').createSaveContinuationLatch();
      shell.saveContinuationNotice = saveLatch.block(notice);
    }
  };
  shell.menu.actions = [{ id: 'load', enabled: true, label: 'Загрузить сохранение', description: '', disabledReason: '' },
    { id: 'about', enabled: true, label: 'Сведения', description: '', disabledReason: '' }];
  shell.menu.options = { guidance: '' }; shell.menu.about = { title: 'Книга', body: '' };
  const react = {
    useMemo(factory: any, _dependencies: unknown[]) { return factory(); },
    useState(initial: any) {
      const owner = current; const index = owner.index++;
      if (!(index in owner.hooks)) owner.hooks[index] = typeof initial === 'function' ? initial() : initial;
      return [owner.hooks[index], (next: any) => { owner.hooks[index] = typeof next === 'function' ? next(owner.hooks[index]) : next; }];
    },
    useRef(initial: any) {
      const index = current.index++;
      return current.hooks[index] ?? (current.hooks[index] = { current: initial });
    },
    useEffect(effect: () => any, dependencies?: unknown[]) {
      const owner = current; const index = owner.index++; const old = owner.hooks[index];
      if (!old || !dependencies || dependencies.some((value, i) => !Object.is(value, old.dependencies[i]))) {
        owner.hooks[index] = { dependencies, cleanup: old?.cleanup };
        owner.effects.push(() => { old?.cleanup?.(); owner.hooks[index].cleanup = effect(); });
      }
    },
    useCallback(callback: any, dependencies: unknown[]) {
      const index = current.index++; const old = current.hooks[index];
      if (!old || dependencies.some((value, i) => !Object.is(value, old.dependencies[i]))) current.hooks[index] = { callback, dependencies };
      return current.hooks[index].callback;
    }
  };
  function module(file: string): any {
    if (cache.has(file)) return cache.get(file);
    const output = ts.transpileModule(readFileSync(join(root, file), 'utf8'), {
      compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }
    }).outputText;
    const result = { exports: {} };
    const jsx = (type: unknown, props: Record<string, unknown>) => ({ type, props });
    const require = (id: string): any => {
      if (id === 'react') return react;
      if (id === 'react/jsx-runtime') return { jsx, jsxs: jsx, Fragment: 'fragment' };
      if (id.includes('api/client')) return { browserApi: api };
      if (id.includes('ShellContext')) return { useShell: () => shell, isSuccess: (value: any) => value?.ok === true };
      if (id.includes('settingsPersistenceNotice')) return module('src/utils/settingsPersistenceNotice.ts');
      if (id.includes('savePersistenceNotice')) return module('src/utils/savePersistenceNotice.ts');
      if (id.includes('loadPersistenceNotice')) return module('src/utils/loadPersistenceNotice.ts');
      if (id === 'framer-motion') return { motion: { nav: 'nav', div: 'div' } };
      if (id.includes('playerFacingCommandResult')) return { sanitizePlayerDefaultCommandResult: (v: unknown) => v };
      if (id.includes('CommandResult')) return { ActionCommandResult: () => null };
      if (id.includes('PromptForm')) return { buildDefaultPromptAnswers: () => ({}) };
      if (id.includes('decorative')) return { OrnamentBorder: 'div' };
      if (id.includes('lib/motion')) return {};

      if (id.includes('shellStateResult')) return module('src/hooks/shellStateResult.ts');
      if (id.includes('loadShellState')) return module('src/hooks/loadShellState.ts');
      if (id.includes('playerCopy')) return { toPlayerFacingText: (value: string, fallback: string) => value || fallback };
      if (id.includes('formatters')) return { toLauncherSaveFailureNotice: () => 'load failed', formatSidebarAudioSummary: () => '' };
      if (id === './AudioPanel') return { AudioPanel: () => null };
      if (id === './ErrorNotice') return { EmptyOrFailure: () => null };
      throw new Error(`Uncontrolled component dependency: ${id}`);
    };
    runInNewContext(output, { require, exports: result.exports, module: result, console,
      setTimeout: (callback: () => void) => { timers.set(++timerId, callback); return timerId; },
      clearTimeout: (id: number) => timers.delete(id) });
    cache.set(file, result.exports);
    return result.exports;
  }
  function renderer(file: string, name: string, props = {}) {
    const component = module(file)[name];
    const instance: any = { hooks: [], index: 0, effects: [], tree: null,
      render() { current = instance; instance.index = 0; instance.effects = []; instance.tree = component(props); for (const effect of instance.effects) effect(); return instance.tree; },
      unmount() { for (const hook of instance.hooks) hook?.cleanup?.(); }
    };
    instance.render(); instance.render();
    return instance;
  }
  const launcher = () => renderer('src/components/GameLauncher.tsx', 'GameLauncher', { menu: shell.menu });
  const settings = () => renderer('src/components/SettingsView.tsx', 'SettingsView');
  const audio = (writeScope: { generation: number }) => renderer('src/components/AudioPanel.tsx', 'AudioPanel', { writeScope });
  return { settings, launcher, audio, shell, load, save, audioWrite, initialAudio, persistedSettings, timers,
    setRefreshFailure: () => { refreshFailure = true; },
    setRealRefreshClient: (client: unknown) => { realRefreshClient = client; },
    realRefreshState: () => realRefreshState,
    counts: () => ({ refreshes, settingsPosts, audioPosts, savePosts, loadPosts, navigations }) };
}
