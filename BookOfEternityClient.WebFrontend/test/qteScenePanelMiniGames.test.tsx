import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { BrowserGameScreenDto, QtePracticeWebStateDto, QteWebActionDto, QteWebStateDto } from '../src/api/contracts';
import { QtePracticeView } from '../src/components/QtePracticeView';
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

  it('renders scored QTE metrics and final rank without raw contract wording', () => {
    const activeHtml = renderToStaticMarkup(<QteScenePanel qte={activeScoredQte()} />);

    expect(activeHtml).toContain('Счёт сцены');
    expect(activeHtml).toContain('Скрытность');
    expect(activeHtml).toContain('87');
    expect(activeHtml).toContain('Тревога');
    expect(activeHtml).not.toContain('Улики');
    expect(activeHtml).not.toContain('Тайное давление');

    const finalHtml = renderToStaticMarkup(<QteScenePanel qte={completedScoredQte()} />);
    expect(finalHtml).toContain('Ранг: Удачный исход');
    expect(finalHtml).toContain('Цель достигнута, тревога осталась управляемой.');
    expect(finalHtml).toContain('Улики');
    expect(finalHtml).toContain('34');
    expect(finalHtml).not.toContain('Тайное давление');

    for (const html of [activeHtml, finalHtml]) {
      for (const forbidden of [/\/api\//i, /\bDTO\b/i, /\bendpoint\b/i, /\bdebug\b/i, /\bJSON\b/i, /scoreDeltas/i, /scoreModel/i]) {
        expect(html).not.toMatch(forbidden);
      }
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

  it('keeps QTE frame shortcuts from swallowing focused child button keyboard activation', () => {
    const source = readFileSync(join(cwd, 'src', 'components', 'qte', 'QteMiniGame.tsx'), 'utf-8');

    expect(source).toContain('function shouldHandleQteFrameShortcut');
    expect(source).toContain('if (!shouldHandleQteFrameShortcut(event))');
    expect(source).toContain("target.closest('button, a, input, select, textarea, [role=\"button\"], [contenteditable=\"true\"]')");
  });
});

describe('QtePracticeView #925', () => {
  it('renders the implemented QTE catalog as reward-free player-facing training choices', () => {
    const html = renderToStaticMarkup(<QtePracticeView initialState={practiceCatalogState()} />);

    for (const title of [
      'Выбор ветки',
      'Полоса реакции',
      'Цепь знаков',
      'Равновесие',
      'Накопление силы',
      'Рывок усилия',
      'Память рун',
      'Пульс ритма',
      'Точный выбор',
      'Тихий проход',
      'Штифты замка'
    ]) {
      expect(html).toContain(title);
    }

    for (const typeId of [
      'BranchChoice',
      'TimingBar',
      'PromptChain',
      'BalanceMeter',
      'ChargeRelease',
      'MashInput',
      'PatternMemory',
      'RhythmPulse',
      'PrecisionChoice',
      'StealthNoise',
      'LockPinSet'
    ]) {
      expect(html).not.toContain(typeId);
    }

    expect(html).toContain('без наград');
    expect(html).toContain('не меняет сюжет');
    expect(html).not.toContain('FutureMirror');
    expect(html).not.toContain('/api/');
    expect(html).not.toMatch(/\bDTO\b/i);
    expect(html).not.toMatch(/\bdebug\b/i);
  });

  it('shows a ready gate with instructions before mounting the active practice mini-game', () => {
    const html = renderToStaticMarkup(<QtePracticeView initialState={practiceActiveState()} />);

    expect(html).toContain('Рывок усилия');
    expect(html).toContain('Тренировочный счёт');
    expect(html).toContain('Подготовьтесь перед запуском мини-игры');
    expect(html).toContain('Начать мини-игру');
    expect(html).not.toContain('data-qte-mini-game=');
    expect(html).not.toContain('MashInput');
    expect(html).not.toContain('Исход проверки');
    expect(html).not.toContain('<select');
  });

  it('shows completion actions for retrying, changing difficulty, choosing another QTE, or exiting', () => {
    const html = renderToStaticMarkup(<QtePracticeView initialState={practiceCompletedState()} />);

    expect(html).toContain('Повторить');
    expect(html).toContain('Сменить сложность');
    expect(html).toContain('Выбрать другое QTE');
    expect(html).toContain('Выйти');
    expect(html).toContain('Ранг: Удачный исход');
    expect(html).toContain('без наград');
  });

  it('keeps practice browser UI tied to the shared mini-game implementation and practice endpoints', () => {
    const source = readFileSync(join(cwd, 'src', 'components', 'QtePracticeView.tsx'), 'utf-8');
    const client = readFileSync(join(cwd, 'src', 'api', 'client.ts'), 'utf-8');

    expect(source).toContain("import { QteMiniGame } from './qte/QteMiniGame';");
    expect(source).toContain('browserApi.resolveQtePracticeAction');
    expect(client).toContain('/api/qte/practice/action');
    expect(client).toContain('/api/qte/practice/start');
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
      },
      scoreState: null
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

type QteScoreMetricFixture = {
  id: string;
  label: string;
  value: number;
  min: number;
  max: number;
  visibility: 'always' | 'final' | 'hidden';
};

type QteScoreRankFixture = {
  id: string;
  label: string;
  summary: string | null;
};

type QteScoreSummaryFixture = {
  rank: QteScoreRankFixture;
  metrics: QteScoreMetricFixture[];
};

type ScoreAwareActiveQte = BrowserGameScreenDto['qte'] & {
  activeScene: NonNullable<BrowserGameScreenDto['qte']['activeScene']> & {
    scoreState: {
      metrics: QteScoreMetricFixture[];
    };
  };
};

type ScoreAwareCompletedQte = BrowserGameScreenDto['qte'] & {
  completion: NonNullable<BrowserGameScreenDto['qte']['completion']> & {
    scoreSummary: QteScoreSummaryFixture;
  };
};

function activeScoredQte(): BrowserGameScreenDto['qte'] {
  const qte = activeQte([
    action('BranchChoice', { kind: 'BranchChoice', supported: true, choiceGrade: 'success' }, { requiresSubmittedGrade: false })
  ]) as ScoreAwareActiveQte;

  qte.activeScene.scoreState = {
    metrics: [
      { id: 'stealth', label: 'Скрытность', value: 87, min: 0, max: 100, visibility: 'always' },
      { id: 'alarm', label: 'Тревога', value: 12, min: 0, max: 100, visibility: 'always' }
    ]
  };
  return qte;
}

function completedScoredQte(): BrowserGameScreenDto['qte'] {
  return {
    state: 'Completed',
    offer: null,
    activeScene: null,
    resolution: null,
    completion: {
      qteId: 'qte_browser_scored',
      outcomeId: 'escaped',
      summary: 'QTE[qte_browser_scored] -> Уход (Успех). Ранг: Удачный исход',
      scoreSummary: {
        rank: {
          id: 'good',
          label: 'Удачный исход',
          summary: 'Цель достигнута, тревога осталась управляемой.'
        },
        metrics: [
          { id: 'stealth', label: 'Скрытность', value: 64, min: 0, max: 100, visibility: 'always' },
          { id: 'evidence', label: 'Улики', value: 34, min: 0, max: 100, visibility: 'final' }
        ]
      }
    },
    lastResolvedReminder: null,
    lastDeclinedQteId: null,
    availableOperations: [],
    notification: null,
    error: null
  } as ScoreAwareCompletedQte;
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

function practiceCatalogState(): QtePracticeWebStateDto {
  return {
    state: 'Catalog',
    catalog: [
      practiceCatalogEntry('BranchChoice', 'Выбор ветки'),
      practiceCatalogEntry('TimingBar', 'Полоса реакции'),
      practiceCatalogEntry('PromptChain', 'Цепь знаков'),
      practiceCatalogEntry('BalanceMeter', 'Равновесие'),
      practiceCatalogEntry('ChargeRelease', 'Накопление силы'),
      practiceCatalogEntry('MashInput', 'Рывок усилия'),
      practiceCatalogEntry('PatternMemory', 'Память рун'),
      practiceCatalogEntry('RhythmPulse', 'Пульс ритма'),
      practiceCatalogEntry('PrecisionChoice', 'Точный выбор'),
      practiceCatalogEntry('StealthNoise', 'Тихий проход'),
      practiceCatalogEntry('LockPinSet', 'Штифты замка')
    ],
    selectedTypeId: null,
    selectedDifficultyId: null,
    activeScene: null,
    resolution: null,
    completion: null,
    feedbackTitle: 'Свободная тренировка',
    feedback: 'Выберите QTE. Тренировка не меняет сюжет.',
    localScoreNotice: 'Тренировочный счёт остаётся только на этой попытке: без наград.',
    availableOperations: ['startAttempt', 'exit'],
    notification: null,
    error: null
  };
}

function practiceActiveState(): QtePracticeWebStateDto {
  return {
    ...practiceCatalogState(),
    state: 'Active',
    selectedTypeId: 'MashInput',
    selectedDifficultyId: 'normal',
    activeScene: activeQte([
      action('MashInput', { kind: 'MashInput', supported: true, keys: ['space'], durationMs: 3000, successTarget: 8, partialTarget: 4 }, { label: 'Рывок усилия' })
    ])!.activeScene,
    feedbackTitle: 'Рывок усилия',
    feedback: 'Тренировка не меняет сюжет.',
    availableOperations: ['submitAction', 'retry', 'changeDifficulty', 'chooseAnother', 'exit']
  };
}

function practiceCompletedState(): QtePracticeWebStateDto {
  return {
    ...practiceCatalogState(),
    state: 'Completed',
    selectedTypeId: 'MashInput',
    selectedDifficultyId: 'normal',
    completion: completedScoredQte().completion,
    feedbackTitle: 'Попытка завершена',
    feedback: 'Итог показан только для тренировки и не меняет сюжет.',
    availableOperations: ['retry', 'changeDifficulty', 'chooseAnother', 'exit']
  };
}

function practiceCatalogEntry(typeId: string, title: string): QtePracticeWebStateDto['catalog'][number] {
  return {
    typeId,
    title,
    description: 'Тренировка без наград и без изменения прохождения.',
    instructions: 'Запустите попытку и сыграйте мини-игру.',
    available: true,
    unavailableReason: null,
    supportedSurfaces: ['console', 'browser'],
    difficulties: [
      { difficultyId: 'easy', label: 'Мягкая', description: 'Больше времени на реакцию.' },
      { difficultyId: 'normal', label: 'Обычная', description: 'Базовая скорость.' },
      { difficultyId: 'hard', label: 'Сложная', description: 'Меньше права на ошибку.' }
    ]
  };
}
