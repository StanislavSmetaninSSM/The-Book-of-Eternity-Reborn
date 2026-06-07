import { describe, expect, it } from 'vitest';
import { toPlayerFacingText, sanitizePlayerMessage } from '../src/utils/playerCopy';
import * as formatters from '../src/utils/formatters';

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { existsSync, readFileSync } = await import(fsSpecifier);
const { basename, join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');
const repoRoot = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? join(cwd, '..')
  : cwd;

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
}

function readRepoSource(...relativePath: string[]): string {
  return readFileSync(join(repoRoot, ...relativePath), 'utf-8');
}

function stripSourceComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/.*$/gm, '');
}

type WorldTimeFormatter = (value: string | null | undefined, fallback?: string) => string;

function formatWorldTimeViaBrowser(value: string | null | undefined): string {
  const formatter = (formatters as Partial<{ formatWorldTimeForPlayer: WorldTimeFormatter }>).formatWorldTimeForPlayer;
  return formatter ? formatter(value, 'время уточняется') : toPlayerFacingText(value, 'время уточняется');
}

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

  it('does not replace "realm" in narrative context', () => {
    const text = 'This realm holds ancient secrets.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('realm');
    expect(result).not.toContain('царство');
  });

  it('does not replace "offer" in narrative context', () => {
    const text = 'I offer you my sword and shield.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('offer');
    expect(result).not.toContain('предложение');
  });

  it('preserves sanitizePlayerMessage behavior for file paths', () => {
    const text = 'Error in game_state/meta/soul_state.json — repair needed';
    const { safe, hasTechnical } = sanitizePlayerMessage(text, 'fallback');
    expect(hasTechnical).toBe(true);
    expect(safe).not.toContain('soul_state.json');
  });

  it('sanitizes prompt-session protocol copy before default rendering', () => {
    const technical = 'Browser-write заблокирован до завершения текущего GM-turn/rollback протокола. Prompt-session prompt_123 принадлежит другому UI.';
    const { safe, hasTechnical } = sanitizePlayerMessage(technical, 'Игровая форма временно недоступна.');

    expect(hasTechnical).toBe(true);
    expect(safe).not.toMatch(/Browser-write|GM-turn|rollback|prompt-session|Prompt-session|протокол|другому UI|браузер/i);
    expect(safe).toContain('Игровая форма');
  });

  it('translates identifiers with underscores/hyphens', () => {
    const text = 'Check game_session for write-flow status in manual_saves';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('сохранение игры');
    expect(result).toContain('запись хода');
    expect(result).toContain('ручные сохранения');
  });

  it('translates pending afterlife identifiers from backend payloads', () => {
    const text = 'pending_shining_abode pending_chaos_sea incarnation turn_writer browser_write';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('ожидание Сияющей Обители');
    expect(result).toContain('ожидание Моря Хаоса');
    expect(result).toContain('инкарнация');
    expect(result).toContain('запись хода');
    expect(result).toContain('запись действия');
  });

  it('localizes canonical browser world time month names', () => {
    const result = formatWorldTimeViaBrowser('1 Month of Beginnings 124, 08:00');

    expect(result).toBe('1 Месяц Начал 124, 08:00');
    expect(result).not.toContain('Month of Beginnings');
  });

  it('preserves custom GM-authored browser world time month names', () => {
    const customTime = '15 Листопад 124, 08:00';

    expect(formatWorldTimeViaBrowser(customTime)).toBe(customTime);
  });

  it('uses shared player-facing world time formatting on browser world surfaces', () => {
    const sceneView = readSource('src', 'components', 'SceneView.tsx');
    const statusView = readSource('src', 'components', 'StatusView.tsx');

    expect(sceneView).toContain("import { formatWorldTimeForPlayer } from '../utils/formatters';");
    expect(sceneView).toContain("formatWorldTimeForPlayer(game.world.worldTime, '')");
    expect(statusView).toContain("import { formatWorldTimeForPlayer } from '../utils/formatters';");
    expect(statusView).toContain("formatWorldTimeForPlayer(world.worldTime, '—')");
  });

  it('keeps advanced diagnostics hardcoded copy in Russian', () => {
    const advancedDiagnostics = readSource('src', 'components', 'AdvancedDiagnostics.tsx');

    expect(advancedDiagnostics).toContain('Диагностика команд, проверка состояния и сведения для ремонта.');
    expect(advancedDiagnostics).toContain('eyebrow="проверка"');
    expect(advancedDiagnostics).toContain('<summary>Подробности проверки</summary>');
    expect(advancedDiagnostics).toContain('title="Контракт локального интерфейса"');
    expect(advancedDiagnostics).toContain('eyebrow="типизированная схема"');
    expect(advancedDiagnostics).toContain('Исправление:');
    expect(advancedDiagnostics).toContain('eyebrow="паритет браузера"');
    expect(advancedDiagnostics).toContain('схема ${coverage.schemaVersion}');
    expect(advancedDiagnostics).toContain('готово для браузера');
    expect(advancedDiagnostics).toContain('псевдонимы:');
    expect(advancedDiagnostics).toContain('следующий шаг не указан');

    expect(advancedDiagnostics).not.toContain('command/API diagnostics');
    expect(advancedDiagnostics).not.toContain('eyebrow="validation"');
    expect(advancedDiagnostics).not.toContain('Raw validation details');
    expect(advancedDiagnostics).not.toContain('Typed API contract');
    expect(advancedDiagnostics).not.toContain('browser parity');
    expect(advancedDiagnostics).not.toContain('browser-ready');
    expect(advancedDiagnostics).not.toContain('aliases:');
    expect(advancedDiagnostics).not.toContain('follow-up не указан');
    expect(advancedDiagnostics).not.toContain('Repair:');
  });

  it('keeps current afterlife and command help copy in Russian', () => {
    const statusView = readSource('src', 'components', 'StatusView.tsx');
    const helpView = readSource('src', 'components', 'HelpView.tsx');

    expect(statusView).toContain('✨ Посмертие');
    expect(statusView).toContain('Сияние');
    expect(statusView).toContain('Искры света');
    expect(helpView).toContain("ChaosSea: 'Море Хаоса'");
    expect(helpView).toContain("ShiningAbode: 'Сияющая Обитель'");
    expect(statusView).not.toContain('Afterlife,');
    expect(helpView).not.toContain('player-safe');
  });

  it('keeps default Browser Client source copy behind the player boundary', () => {
    const defaultPlayerSources = [
      ['BookOfEternityClient.WebFrontend/src/components/LoadingCard.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'LoadingCard.tsx')],
      ['BookOfEternityClient.WebFrontend/src/components/GameLauncher.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'GameLauncher.tsx')],
      ['BookOfEternityClient.WebFrontend/src/components/tabBarConfig.ts', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'tabBarConfig.ts')],
      ['BookOfEternityClient.WebFrontend/src/components/ConnectionBanner.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'ConnectionBanner.tsx')],
      ['BookOfEternityClient.WebFrontend/src/components/AudioPanel.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'AudioPanel.tsx')],
      ['BookOfEternityClient.WebFrontend/src/components/SettingsView.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'SettingsView.tsx')],
      ['BookOfEternityClient.WebFrontend/src/api/contract-fixtures/client-settings.json', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'api', 'contract-fixtures', 'client-settings.json')],
      ['BookOfEternityClient.WebFrontend/src/App.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'App.tsx')],
      ['BookOfEternityClient.WebFrontend/src/hooks/useShellState.ts', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'hooks', 'useShellState.ts')],
      ['BookOfEternityClient.WebFrontend/src/context/ShellContext.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'context', 'ShellContext.tsx')],
      ['BookOfEternityClient.WebFrontend/src/components/CommandResultView.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'CommandResultView.tsx')],
      ['BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx', readRepoSource('BookOfEternityClient.WebFrontend', 'src', 'components', 'BlockRenderer.tsx')],
      ['BookOfEternityClient/WebUi/BrowserClientSettingsService.cs', readRepoSource('BookOfEternityClient', 'WebUi', 'BrowserClientSettingsService.cs')],
      ['BookOfEternityClient/WebUi/LocalWebUiMainMenuService.cs', readRepoSource('BookOfEternityClient', 'WebUi', 'LocalWebUiMainMenuService.cs')]
    ] as const;

    const bannedDefaultCopyPatterns: Array<[RegExp, string]> = [
      [/игрокоориентирован/i, 'meta player-orientation phrasing'],
      [/player[- ](?:facing|oriented)/i, 'English player-facing meta phrasing'],
      [/C#\s+(?:host|runtime)/i, 'C# implementation framing'],
      [/\bDTO\b/, 'DTO implementation framing'],
      [/(?<!\.)\/api\/|API-подсказ/i, 'raw API or endpoint framing'],
      [/\bendpoint\b/i, 'endpoint implementation wording'],
      [/debug shell|debug-инструмент/i, 'debug shell wording'],
      [/Raw validation details|raw JSON/i, 'raw JSON or validation diagnostics'],
      [/локальн(?:ый|ого|ому|ом|ые|ых)?\s+клиент/i, 'local client wording'],
      [/браузерн(?:ый|ого|ому|ом)\s+клиент/i, 'browser client wording'],
      [/браузерн(?:ую|ая|ой|ом|ое|ого)\s+(?:форму|форма|меню|сессия|сессию|игровом экран|список|списка)/i, 'browser implementation surface wording'],
      [/браузер\s+только/i, 'browser implementation justification'],
      [/игров(?:ой|ом)\s+экран/i, 'game-screen implementation label'],
      [/write-flow|repair\/validation|UI-блокиров/i, 'write-flow or repair implementation wording'],
      [/localhost\/loopback/i, 'local transport implementation wording'],
      [/Папка\s+game_session|game_session\s+—\s+локальная\s+папка\s+книги|game_session.+игровых контракт|В manual_saves и autosaves/i, 'internal save directory wording'],
      [/Browser Client задач/i, 'project-task implementation wording'],
      [/Браузер\s+не\s+(?:дал|может)|Включить музыку в браузере|Клиент продолжит/i, 'browser/client audio implementation wording']
    ];

    const leaks = defaultPlayerSources.flatMap(([path, source]) => {
      const visibleSource = stripSourceComments(source);
      return bannedDefaultCopyPatterns
        .filter(([pattern]) => pattern.test(visibleSource))
        .map(([pattern, label]) => `${path}: ${label} (${pattern})`);
    });

    expect(leaks).toEqual([]);
  });

  it('keeps command coverage and advanced Help commands behind opt-in', () => {
    const helpView = readSource('src', 'components', 'HelpView.tsx');
    const shellState = readSource('src', 'hooks', 'useShellState.ts');

    expect(shellState).not.toContain('const coverageResult = await Promise.allSettled([browserApi.getCommandCoverage()]);');
    expect(helpView).toContain('advancedEnabled');
    expect(helpView).toContain('isDefaultHelpCommandVisible');
    expect(helpView).toContain("cmd.surface !== 'advanced-only'");
    expect(helpView).not.toContain('<span className="help-command__alias">/help</span>');
    expect(helpView).toContain('<span className="help-command__alias">Справка</span>');
    expect(helpView).toContain("<span className=\"help-command__alias\">{advancedEnabled ? cmd.aliases[0] : 'Действие'}</span>");
    for (const commandId of ['help', 'math', 'gm', 'debug', 'mods', 'system_guardians', 'validate']) {
      expect(helpView).toContain(`'${commandId}'`);
    }
  });

  it('keeps raw command strings and raw JSON out of default command result rendering', () => {
    const shellContext = readSource('src', 'context', 'ShellContext.tsx');
    const commandResultView = readSource('src', 'components', 'CommandResultView.tsx');
    const blockRenderer = readSource('src', 'components', 'BlockRenderer.tsx');

    expect(shellContext).not.toContain('setCommandResult(result.data);');
    expect(shellContext).toContain('sanitizeExplorerCommandResultForPlayer(result.data)');
    expect(commandResultView).not.toContain('className="command-result-view__command">{result.command}</span>');
    expect(commandResultView).not.toContain('setLocalResult({ commandResult, result: response.data });');
    expect(commandResultView).toContain('sanitizeExplorerCommandResultForPlayer(response.data)');
    expect(commandResultView).toContain('command-result-view__title');
    expect(commandResultView).toContain('sanitizePlayerMessage');
    expect(commandResultView).not.toContain('<strong>{n.title}</strong>');
    expect(commandResultView).not.toContain('<p>{n.message}</p>');
    expect(blockRenderer).not.toContain("<p>{toPlayerFacingText(block.message, '')}</p>");
    expect(blockRenderer).toContain('sanitizePlayerMessage(block.message');
    expect(commandResultView).toContain('<BlockList blocks={result.blocks} advancedEnabled={advancedEnabled} />');
    expect(blockRenderer).toContain("import { JsonTreeViewer } from './JsonTreeViewer';");
    expect(blockRenderer).toContain('if (advancedEnabled) {');
    expect(blockRenderer).toContain('<JsonTreeViewer data={block.json}');
    expect(blockRenderer).toContain('Подробные сведения доступны в расширенном режиме.');
  });

  it('renders command map blocks through a visual atlas surface instead of text or node lists', () => {
    const commandResult = readSource('src', 'components', 'CommandResult.tsx');
    const blockRenderer = readSource('src', 'components', 'BlockRenderer.tsx');
    const mapBlockPath = join(frontendDir, 'src', 'components', 'MapBlock.tsx');

    expect(commandResult).not.toContain('карта содержит');
    expect(blockRenderer).not.toContain('block.map.nodes.slice');
    expect(existsSync(mapBlockPath)).toBe(true);
    if (!existsSync(mapBlockPath)) return;

    const mapBlock = readFileSync(mapBlockPath, 'utf-8');
    expect(commandResult).toContain("import { MapBlock } from './MapBlock';");
    expect(blockRenderer).toContain("import { MapBlock } from './MapBlock';");
    expect(commandResult).toContain('<MapBlock block={block} variant="compact" />');
    expect(blockRenderer).toContain('<MapBlock block={block} />');
    expect(mapBlock).toContain('<svg');
    expect(mapBlock).toContain('className="map-canvas"');
    expect(mapBlock).toContain('block.map.links.map');
    expect(mapBlock).toContain('aria-label={mapTitle}');
  });

  it('resets browser map selection controls when a different map block is rendered', () => {
    const mapBlock = readSource('src', 'components', 'MapBlock.tsx');

    expect(mapBlock).toContain("import { useEffect, useMemo, useState } from 'react';");
    expect(mapBlock).toContain('useEffect(() => {');
    expect(mapBlock).toContain('setSelectedZ(defaultZ);');
    expect(mapBlock).toContain('setSelectedLayer(defaultLayer);');
    expect(mapBlock).toContain('setSelectedNodeId(defaultNodeId);');
    expect(mapBlock).toContain('const mapResetKey = block.map;');
    expect(mapBlock).toContain('[defaultLayer, defaultNodeId, defaultZ, mapResetKey]');
  });
});
