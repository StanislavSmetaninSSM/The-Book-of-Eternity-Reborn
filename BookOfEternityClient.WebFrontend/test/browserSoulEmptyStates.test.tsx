import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { ShellContext, type ShellContextValue } from '../src/context/ShellContext';
import type { BrowserApiResult, BrowserGameScreenDto } from '../src/api/contracts';
import { StatusView } from '../src/components/StatusView';

function unavailable<T>(): BrowserApiResult<T> {
  return {
    ok: false,
    status: null,
    kind: 'no-active-session',
    message: 'no session',
    playerMessage: 'Книга ещё не открыта.'
  };
}

function createGameScreen(overrides: {
  player?: Partial<BrowserGameScreenDto['player']>;
  soul?: Partial<BrowserGameScreenDto['soul']>;
  world?: Partial<BrowserGameScreenDto['world']>;
  afterlife?: Partial<BrowserGameScreenDto['afterlife']>;
} = {}): BrowserGameScreenDto {
  return {
    schemaVersion: 1,
    theme: {
      key: 'mortal-world',
      label: 'Мир смертных',
      icon: '🌘',
      accent: '#c9a24d'
    },
    player: {
      name: 'Арина',
      class: 'Хранительница порога',
      race: 'человек',
      currentCondition: 'держится на ногах',
      healthPercentage: '100%',
      energyPercentage: '66%',
      poisePercentage: '33%',
      activeConditions: [],
      ...overrides.player
    },
    soul: {
      name: 'Светлая Нить',
      realm: 'Мир смертных',
      incarnation: 1,
      inkFeathers: 0,
      enlightenmentTier: 'Искра',
      activeGuardianName: 'Белое Перо',
      ...overrides.soul
    },
    world: {
      location: 'Северные ворота',
      worldTime: '1 Month of Beginnings 124, 08:00',
      turnNumber: 7,
      sessionId: 'session-test',
      ...overrides.world
    },
    narrative: {
      text: '',
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
      shiningRadianceExperience: 12,
      shiningRadianceTier: 1,
      shiningLightSparks: 0,
      shiningHallCount: 1,
      shiningFactionCount: 0,
      hasOpenShiningGatesDraft: false,
      isShiningGatesDraftStale: false,
      ...overrides.afterlife
    },
    turnState: {
      state: 'ready',
      title: '',
      message: '',
      canStartBrowserWrite: true,
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

function renderStatusView(game: BrowserGameScreenDto): string {
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
    activeTab: 'status',
    setActiveTab: () => undefined,
    activeRoute: 'soul',
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
      <StatusView />
    </ShellContext.Provider>
  );
}

describe('browser Soul/status empty states #789', () => {
  it('renders intentional empty-state treatment for missing player and soul identity values', () => {
    const html = renderStatusView(createGameScreen({
      player: {
        name: '   ',
        class: 'не указан',
        race: 'unknown',
        currentCondition: '—'
      },
      soul: {
        name: 'n/a',
        realm: 'не назначен',
        enlightenmentTier: 'не указана',
        activeGuardianName: '—'
      },
      world: {
        location: '—',
        worldTime: ' '
      }
    }));

    expect(html).toContain('status-empty-state');
    expect(html).toContain('Летопись героя ещё ждёт первых строк.');
    expect(html).toContain('Имя, класс, раса и состояние появятся после записи главы.');
    expect(html).toContain('Душа ещё не обрела полную запись.');
    expect(html).toContain('Пока не записано');
    expect(html).toContain('место уточняется');
    expect(html).toContain('время уточняется');
    expect(html).not.toMatch(/<dd>\s*<\/dd>/);
    expect(html).not.toContain('<dd>не указан</dd>');
    expect(html).not.toContain('<dd>не указана</dd>');
    expect(html).not.toContain('<dd>не назначен</dd>');
    expect(html).not.toContain('<dd>unknown</dd>');
    expect(html).not.toContain('<dd>n/a</dd>');
    expect(html).not.toContain('<dd>—</dd>');
  });

  it('keeps meaningful character, soul, world, and afterlife values visible by default', () => {
    const html = renderStatusView(createGameScreen());

    for (const visibleValue of [
      'Арина',
      'Хранительница порога',
      'человек',
      'держится на ногах',
      'Светлая Нить',
      'Мир смертных',
      'Северные ворота',
      '1 Месяц Начал 124, 08:00',
      '<dd>0</dd>',
      'Сияние',
      'Искры света'
    ]) {
      expect(html).toContain(visibleValue);
    }

    expect(html).not.toContain('status-empty-state');
    expect(html).not.toContain('Пока не записано');

    const dormantAfterlifeHtml = renderStatusView(createGameScreen({
      afterlife: {
        shiningRadianceExperience: 0,
        shiningRadianceTier: 0,
        shiningLightSparks: 0,
        shiningHallCount: 0,
        shiningFactionCount: 0
      }
    }));

    expect(dormantAfterlifeHtml).toContain('✨ Посмертие');
    expect(dormantAfterlifeHtml).toContain('Следы посмертия пока не открыты.');
    expect(dormantAfterlifeHtml).toContain('Искры света');
    expect(dormantAfterlifeHtml).toContain('<dd>0</dd>');
  });

  it('localizes canonical realm names in the status tab', () => {
    const html = renderStatusView(createGameScreen({
      soul: {
        realm: 'Mortal World'
      }
    }));

    expect(html).toContain('Мир смертных');
    expect(html).not.toContain('Mortal World');
  });

  it('keeps default copy player-facing and preserves semantic accessible status meters', () => {
    const html = renderStatusView(createGameScreen());

    expect(html).toContain('status-meter status-meter--good');
    expect(html).toContain('status-meter status-meter--warning');
    expect(html).toContain('status-meter status-meter--danger');
    expect(html).toContain('role="meter"');
    expect(html).toContain('aria-valuemin="0"');
    expect(html).toContain('aria-valuemax="100"');
    expect(html).toContain('aria-valuenow="100"');
    expect(html).toContain('aria-valuetext="100%"');

    for (const forbidden of [
      /detailsIntro/i,
      /\/api\//i,
      /\bDTO\b/i,
      /\bendpoint\b/i,
      /\bdebug\b/i,
      /raw JSON/i,
      /validation-dashboard/i,
      /\bagent\b/i
    ]) {
      expect(html).not.toMatch(forbidden);
    }
  });
});
