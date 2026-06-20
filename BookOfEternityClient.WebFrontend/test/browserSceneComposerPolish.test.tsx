import { readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { ShellContext, type ShellContextValue } from '../src/context/ShellContext';
import type { BrowserApiResult, BrowserGameScreenDto } from '../src/api/contracts';
import { SceneView } from '../src/components/SceneView';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
}

function unavailable<T>(): BrowserApiResult<T> {
  return {
    ok: false,
    status: null,
    kind: 'no-active-session',
    message: 'no session',
    playerMessage: 'Книга ещё не открыта.'
  };
}

function createGameScreen(): BrowserGameScreenDto {
  return {
    schemaVersion: 1,
    theme: {
      key: 'mortal-world',
      label: 'Смертный мир',
      icon: '🌘',
      accent: '#c9a24d'
    },
    player: {
      name: 'Асуран',
      class: 'Аристократ-маг',
      race: 'Человек',
      currentCondition: 'Лёгкое недомогание',
      healthPercentage: '85%',
      energyPercentage: '60%',
      poisePercentage: '95%',
      activeConditions: []
    },
    soul: {
      name: 'Пепельная Искра',
      realm: 'Мир смертных',
      incarnation: 2,
      inkFeathers: 80,
      enlightenmentTier: 'Ученик',
      activeGuardianName: ''
    },
    world: {
      location: 'Покои виконта де Вальмонта',
      worldTime: '1 Month of Beginnings 124, 08:15',
      turnNumber: 3,
      sessionId: 'session-test'
    },
    narrative: {
      text: 'Утренний свет пробивается сквозь тяжёлые бархатные шторы.',
      dialogueOptions: [],
      combatLog: '',
      imagePrompt: ''
    },
    media: {
      schemaVersion: 1,
      sceneImagePrompt: '',
      gallery: [],
      map: {
        schemaVersion: 1,
        realm: 'MortalWorld',
        title: '',
        currentNodeId: '',
        layers: [],
        zLevels: [],
        nodes: [],
        links: [],
        regions: []
      }
    },
    afterlife: {
      shiningRadianceExperience: 0,
      shiningRadianceTier: 0,
      shiningLightSparks: 0,
      shiningHallCount: 0,
      shiningFactionCount: 0,
      hasOpenShiningGatesDraft: false,
      isShiningGatesDraftStale: false
    },
    turnState: {
      state: 'ready',
      title: '',
      message: '',
      canStartBrowserWrite: true,
      validationState: 'ok',
      validationLabel: '',
      phase: 'ready',
      phaseLabel: '',
      severity: 'Info',
      playerGuidance: '',
      recommendedActions: [],
      knownPhases: []
    },
    actionComposer: {
      canSubmit: true,
      mode: 'text',
      placeholder: '',
      guidance: '',
      disabledReason: ''
    },
    qte: {
      state: 'idle',
      offer: null,
      activeScene: null,
      resolution: null,
      completion: null,
      lastResolvedReminder: null,
      lastDeclinedQteId: null,
      availableOperations: [],
      notification: null,
      error: null
    },
    actionMenu: {
      schemaVersion: 1,
      sections: []
    },
    flags: {
      isInChaosSea: false,
      isInAnyShiningAbodeState: false,
      isInShiningAbode: false,
      isInShiningAbodePendingBootstrap: false,
      isInAfterlifeRealm: false,
      canReenterShiningAbode: false
    }
  };
}

function renderSceneView(game: BrowserGameScreenDto): string {
  const gameResult: BrowserApiResult<BrowserGameScreenDto> = {
    ok: true,
    status: 200,
    data: game
  };
  const readyState = {
    status: 'ready' as const,
    connectionStatus: 'connected' as const,
    menu: unavailable(),
    session: unavailable(),
    game: gameResult,
    audio: unavailable(),
    settings: unavailable(),
    lifecycle: null,
    commandCoverage: null
  };
  const context = {
    shellState: readyState,
    readyState,
    gameScreen: game,
    menu: null,
    session: null,
    clientSettings: null,
    realmTheme: game.theme,
    activeTab: 'scene',
    setActiveTab: () => undefined,
    activeRoute: 'game',
    setActiveRoute: () => undefined,
    connectionStatus: 'connected',
    advancedEnabled: false,
    setAdvancedEnabled: () => undefined,
    composerText: '',
    setComposerText: () => undefined,
    composerNotice: null,
    submitComposer: () => undefined,
    submitComposerText: () => undefined,
    commandResult: null,
    isCommandView: false,
    executeCommand: async () => undefined,
    clearCommandResult: () => undefined,
    loadBrowserState: async () => undefined
  } satisfies ShellContextValue;

  return renderToStaticMarkup(
    <ShellContext.Provider value={context}>
      <SceneView />
    </ShellContext.Provider>
  );
}

describe('browser scene composer polish #1185', () => {
  it('renders per-post local text scale controls under the scene post', () => {
    const html = renderSceneView(createGameScreen());

    expect(html).toContain('scene-post');
    expect(html).toContain('scene-post-controls');
    expect(html).toContain('Уменьшить текст сцены');
    expect(html).toContain('Обычный размер текста сцены');
    expect(html).toContain('Увеличить текст сцены');
    expect(html).toContain('--scene-post-scale:1');
  });

  it('keeps post scale local to scene messages and styles it through a scoped variable', () => {
    const sceneView = readSource('src', 'components', 'SceneView.tsx');
    const styles = readSource('src', 'styles', 'command-ui.css') + readSource('src', 'styles', 'components.css');

    expect(sceneView).toContain('postTextScales');
    expect(sceneView).toContain("updatePostScale('scene-narrative'");
    expect(sceneView).toContain("'--scene-post-scale'");
    expect(styles).toContain('.scene-post');
    expect(styles).toContain('.scene-post-controls');
    expect(styles).toContain('var(--scene-post-scale, 1)');
  });

  it('defines compact command mode and large artistic post mode in the bottom composer', () => {
    const unifiedInput = readSource('src', 'components', 'UnifiedInput.tsx');
    const styles = readSource('src', 'styles', 'command-ui.css');

    expect(unifiedInput).toContain("type ComposerMode = 'command' | 'post'");
    expect(unifiedInput).toContain('Художественный пост');
    expect(unifiedInput).toContain("composerMode === 'post'");
    expect(unifiedInput).toContain("composerMode === 'command'");
    expect(unifiedInput).toContain('rows={composerMode ===');
    expect(unifiedInput).toContain('!isPostMode && e.key ===');
    expect(unifiedInput).toContain('className="unified-input__actions"');
    expect(styles).toContain('.unified-input.is-post-mode');
    expect(styles).toContain('.unified-input__mode-toggle');
    expect(styles).toContain('.unified-input__actions');
    expect(styles).toContain('.unified-input.is-post-mode .unified-input__actions');
    expect(styles).toContain('.unified-input.is-post-mode .unified-input__textarea');
    expect(styles).toContain('grid-template-columns: minmax(0, 1fr);');
    expect(styles).toContain('resize: vertical');
  });

  it('uses content-area overflow that does not reserve a visible scrollbar when content fits', () => {
    const commandUi = readSource('src', 'styles', 'command-ui.css');
    const layout = readSource('src', 'styles', 'layout.css');

    expect(commandUi).toContain('overflow-y: auto;');
    expect(commandUi).not.toContain('scrollbar-gutter: stable;');
    expect(commandUi).toContain('scrollbar-gutter: auto;');
    expect(layout).toContain('overflow: hidden;');
  });

  it('keeps scene blocks visually separated before real overflow appears', () => {
    const commandUi = readSource('src', 'styles', 'command-ui.css');

    expect(commandUi).toContain(".browser-shell[data-active-tab='scene'] .content-area");
    expect(commandUi).toContain('padding-block: clamp(0rem, 0.2vh, 0.2rem);');
    expect(commandUi).toContain(".browser-shell[data-active-tab='scene'] .cinematic-hero");
    expect(commandUi).toContain('height: clamp(4.5rem, 9vh, 7rem);');
    expect(commandUi).toContain('gap: clamp(0.6rem, 1.1vh, 0.8rem);');
    expect(commandUi).toContain(".browser-shell[data-active-tab='scene'] .scene-post .rune-frame");
    expect(commandUi).toContain(".browser-shell[data-active-tab='scene'] .scene-dialogues");
    expect(commandUi).toContain(".browser-shell[data-active-tab='scene'] .scene-quick-actions");
    expect(commandUi).toContain('margin-top: clamp(0.8rem, 1.8vh, 1.25rem);');
    expect(commandUi).toContain('margin-top: clamp(0.95rem, 2vh, 1.4rem);');
    expect(commandUi).toContain('gap: 0.5rem;');
  });

  it('preserves the styled content scrollbar instead of hiding it', () => {
    const commandUi = readSource('src', 'styles', 'command-ui.css');

    expect(commandUi).not.toContain('scrollbar-width: none;');
    expect(commandUi).not.toContain('.content-area::-webkit-scrollbar');
    expect(commandUi).not.toContain('scrollbar-width: thin;');
  });

  it('does not render the ornamental divider as extra scene-flow content', () => {
    const sceneView = readSource('src', 'components', 'SceneView.tsx');

    expect(sceneView).not.toContain('OrnamentBorder');
  });
});
