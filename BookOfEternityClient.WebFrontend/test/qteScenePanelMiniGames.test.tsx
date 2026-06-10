import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { BrowserGameScreenDto, QteWebActionDto, QteWebStateDto } from '../src/api/contracts';
import { QteScenePanel } from '../src/components/QteScenePanel';

type QteCheckConfigFixture = Record<string, unknown>;

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';

const supportedMiniGames: Array<[string, string, QteCheckConfigFixture]> = [
  ['TimingBar', 'Полоса реакции', { kind: 'TimingBar', supported: true, width: 32, successStart: 12, successWidth: 8, partialStart: 9, partialWidth: 14, tickMs: 80 }],
  ['PromptChain', 'Цепь знаков', { kind: 'PromptChain', supported: true, sequence: ['q', 'w', 'space'], allowedMistakes: 1, timeoutMs: 1200 }],
  ['BalanceMeter', 'Равновесие', { kind: 'BalanceMeter', supported: true, safeHalfWidth: 12, ticks: 12, tickMs: 100 }],
  ['ChargeRelease', 'Накопление силы', { kind: 'ChargeRelease', supported: true, targetStart: 42, targetWidth: 18, tickMs: 80 }],
  ['MashInput', 'Рывок усилия', { kind: 'MashInput', supported: true, keys: ['space'], durationMs: 3000, successTarget: 8, partialTarget: 4 }],
  ['PatternMemory', 'Память рун', { kind: 'PatternMemory', supported: true, sequence: ['q', 'w', 'space'], allowedMistakes: 1, revealMs: 800, inputTimeoutMs: 2400 }],
  ['RhythmPulse', 'Пульс ритма', { kind: 'RhythmPulse', supported: true, pulseOffsetsMs: [500, 1000, 1500], hitWindowMs: 90, allowedMisses: 1 }],
  ['PrecisionChoice', 'Точный выбор', { kind: 'PrecisionChoice', supported: true, timeoutMs: 6000, timeoutGrade: 'partial', choices: [{ id: 'open_gate', label: 'Открыть врата', grade: 'success' }] }],
  ['StealthNoise', 'Тихий проход', { kind: 'StealthNoise', supported: true, durationMs: 6000, startingNoise: 10, dangerThreshold: 70, noiseDriftPerSecond: 9, recoveryPerInput: 12, allowedOverThresholdMs: 800, recoveryKey: 'space', gradeThresholds: { successMaxNoise: 45, successMaxOverThresholdMs: 0, partialMaxNoise: 75, partialMaxOverThresholdMs: 800 } }],
  ['LockPinSet', 'Штифты замка', { kind: 'LockPinSet', supported: true, pinCount: 2, pinWindows: [{ pin: 1, min: 20, max: 30, label: 'первый штифт' }, { pin: 2, min: 60, max: 70, label: 'второй штифт' }], timerMs: 9000, pickDurability: 4, maxMistakes: 2, adjustKey: 'q', setKey: 'space', gradeThresholds: { successMaxTimeMs: 3000, successMaxMistakes: 0, partialMaxTimeMs: 8000, partialMaxMistakes: 2 } }]
];

describe('QteScenePanel browser mini-games #918', () => {
  it('renders supported QTE actions as mini-game surfaces without manual grade controls', () => {
    const html = renderToStaticMarkup(
      <QteScenePanel qte={activeQte(supportedMiniGames.map(([checkType, _title, checkConfig]) => action(checkType, checkConfig)))} />
    );

    for (const [_checkType, title] of supportedMiniGames) {
      expect(html).toContain(title);
    }

    expect(html).toContain('data-qte-mini-game=');
    expect(html).not.toContain('Исход проверки');
    expect(html).not.toContain('Быстрый выбор исхода');
    expect(html).not.toContain('Подтвердить исход');
    expect(html).not.toContain('<select');
    expect(html).not.toContain('Время ушло');
    expect(html).not.toContain('Завершить проход');
    expect(html).not.toContain('Завершить взлом');
    expect(html).not.toContain('Завершить ритм');
    expect(html).not.toContain('Завершить стойку');

    for (const forbidden of [/\/api\//i, /\bDTO\b/i, /\bendpoint\b/i, /\bdebug\b/i, /\bJSON\b/i, /manual/i, /grade selector/i]) {
      expect(html).not.toMatch(forbidden);
    }
  });

  it('keeps BranchChoice as a direct static choice without mini-game grading', () => {
    const html = renderToStaticMarkup(
      <QteScenePanel qte={activeQte([
        action('BranchChoice', { kind: 'BranchChoice', supported: true, choiceGrade: 'partial' }, { requiresSubmittedGrade: false })
      ])} />
    );

    expect(html).toContain('Выбрать действие');
    expect(html).not.toContain('Исход проверки');
    expect(html).not.toContain('<select');
  });

  it('shows an explicit unsupported-state surface instead of a default manual grade fallback', () => {
    const html = renderToStaticMarkup(
      <QteScenePanel qte={activeQte([
        action('FutureMirror', { kind: 'Unsupported', supported: false, checkType: 'FutureMirror' })
      ])} />
    );

    expect(html).toContain('Эта быстрая сцена ждёт обновления книги');
    expect(html).not.toContain('Исход проверки');
    expect(html).not.toContain('Быстрый выбор исхода');
    expect(html).not.toContain('Подтвердить исход');
    expect(html).not.toContain('<select');
  });

  it('keeps representative mini-games keyboard-focusable and pointer actionable with responsive wrappers', () => {
    const html = renderToStaticMarkup(
      <QteScenePanel qte={activeQte([
        action('TimingBar', { kind: 'TimingBar', supported: true, width: 32, successStart: 12, successWidth: 8, partialStart: 9, partialWidth: 14, tickMs: 80 }),
        action('StealthNoise', { kind: 'StealthNoise', supported: true, durationMs: 6000, startingNoise: 10, dangerThreshold: 70, noiseDriftPerSecond: 9, recoveryPerInput: 12, allowedOverThresholdMs: 800, recoveryKey: 'space', gradeThresholds: { successMaxNoise: 45, successMaxOverThresholdMs: 0, partialMaxNoise: 75, partialMaxOverThresholdMs: 800 } }),
        action('LockPinSet', { kind: 'LockPinSet', supported: true, pinCount: 2, pinWindows: [{ pin: 1, min: 20, max: 30, label: 'первый штифт' }, { pin: 2, min: 60, max: 70, label: 'второй штифт' }], timerMs: 9000, pickDurability: 4, maxMistakes: 2, adjustKey: 'q', setKey: 'space', gradeThresholds: { successMaxTimeMs: 3000, successMaxMistakes: 0, partialMaxTimeMs: 8000, partialMaxMistakes: 2 } })
      ])} />
    );

    expect(html).toContain('tabindex="0"');
    expect(html).toContain('Поймать момент');
    expect(html).toContain('приглушить шум');
    expect(html).toContain('Поднять');
    expect(html).toContain('Опустить');
    expect(html).toContain('Зафиксировать');

    const styles = readFileSync(join(cwd, 'src', 'styles', 'components.css'), 'utf-8');
    expect(styles).toContain('.qte-mini-game');
    expect(styles).toContain('flex-wrap: wrap');
    expect(styles).toContain('grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr))');
    expect(styles).toContain('overflow: hidden');

    const source = readFileSync(join(cwd, 'src', 'components', 'qte', 'QteMiniGame.tsx'), 'utf-8');
    expect(source).toContain('useQteDeadline');
    expect(source).toContain("phase === 'reveal'");
    expect(source).toContain('hideExpected');
    expect(source).toContain('resolvePrecisionChoiceGrade(config.choices, null, true');
  });
});

function activeQte(actions: QteWebActionDto[]): BrowserGameScreenDto['qte'] {
  return {
    state: 'Active',
    offer: null,
    activeScene: {
      qteId: 'qte_browser_parity_test',
      title: 'Быстрая сцена',
      acceptedAtTurn: 12,
      currentChapter: {
        chapterId: 'start',
        title: 'Испытание',
        narrative: 'Книга требует реакции.',
        chapterImagePrompt: null,
        actions
      }
    },
    resolution: null,
    completion: null,
    lastResolvedReminder: null,
    lastDeclinedQteId: null,
    availableOperations: ['submitAction'],
    notification: null,
    error: null
  } satisfies QteWebStateDto;
}

function action(
  checkType: string,
  checkConfig: QteCheckConfigFixture,
  overrides: Partial<QteWebActionDto> = {}
): QteWebActionDto {
  return {
    actionId: checkType.toLowerCase(),
    label: `Действие ${checkType}`,
    checkType,
    baseDifficulty: 3,
    primaryCharacteristic: 'dexterity',
    requiresSubmittedGrade: true,
    gradeOptions: ['success', 'partial', 'fail'],
    checkConfig,
    ...overrides
  } as unknown as QteWebActionDto;
}
