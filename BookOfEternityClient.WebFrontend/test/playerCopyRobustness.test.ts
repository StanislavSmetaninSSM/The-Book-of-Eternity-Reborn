import { describe, expect, it } from 'vitest';
import { toPlayerFacingText, sanitizePlayerMessage } from '../src/utils/playerCopy';
import * as formatters from '../src/utils/formatters';

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { basename, join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
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
    expect(result).toContain('запись из браузера');
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
});
