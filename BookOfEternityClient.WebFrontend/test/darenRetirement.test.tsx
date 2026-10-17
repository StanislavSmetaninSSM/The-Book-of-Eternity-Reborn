import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { QtePracticeWebStateDto } from '../src/api/contracts';
import { QtePracticeView } from '../src/components/QtePracticeView';

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const types = ['BranchChoice', 'TimingBar', 'PromptChain', 'BalanceMeter', 'ChargeRelease', 'MashInput',
  'PatternMemory', 'RhythmPulse', 'PrecisionChoice', 'StealthNoise', 'LockPinSet'];

describe('Daren retirement preserves the common browser QTE client', () => {
  it('removes campaign routes and API imports while retaining actual practice navigation', () => {
    const app = readFileSync(join(cwd, 'src', 'App.tsx'), 'utf8');
    const shell = readFileSync(join(cwd, 'src', 'context', 'ShellContext.tsx'), 'utf8');
    const launcher = readFileSync(join(cwd, 'src', 'components', 'GameLauncher.tsx'), 'utf8');
    const client = readFileSync(join(cwd, 'src', 'api', 'client.ts'), 'utf8');
    expect(app).toContain('QtePracticeView');
    expect(shell).toContain("useState<RouteId>('home')");
    expect(launcher).toContain("onActiveRouteChange('practice')");
    expect(client).toContain('/api/qte/practice/start');
    for (const text of [app, shell, launcher, client]) {
      expect(text).not.toContain('DarenShowcase');
      expect(text).not.toContain("'daren-showcase'");
      expect(text).not.toContain('/api/qte/daren');
    }
  });

  it('renders all eleven retained practice catalog entries without permanent reward promises', () => {
    const state: QtePracticeWebStateDto = {
      state: 'Catalog', selectedTypeId: null, selectedDifficultyId: null, activeScene: null,
      resolution: null, completion: null, feedbackTitle: 'Свободная тренировка', feedback: 'Без наград и изменения сюжета.',
      localScoreNotice: 'Результат остаётся только в тренировке.', availableOperations: ['start'],
      interactionToken: 'owned-component-token', notification: null, errorCode: null, error: null,
      catalog: types.map((typeId, i) => ({ typeId, title: `Тренировка ${i + 1}`, description: 'Без наград и изменения сюжета.',
        instructions: 'Попробуйте мини-игру.', available: true, unavailableReason: null, supportedSurfaces: ['console', 'browser'],
        difficulties: [{ difficultyId: 'normal', label: 'Обычная', description: 'Обычная сложность.' }] }))
    };
    const html = renderToStaticMarkup(<QtePracticeView initialState={state} />);
    for (let i = 0; i < types.length; i++) expect(html).toContain(`Тренировка ${i + 1}`);
    expect(html).not.toContain('Дарен');
    expect(html).not.toContain('Daren');
    expect(html).not.toContain('/api/');
    expect(html).not.toContain('Чернильных Перьев');
  });
});
