import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { DarenShowcaseWebStateDto, QteWebActionDto } from '../src/api/contracts';
import { DarenShowcaseView } from '../src/components/DarenShowcaseView';

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';

describe('DarenShowcaseView #919', () => {
  it('renders the Daren showcase as a separate authored route with permanent reward copy', () => {
    const html = renderToStaticMarkup(<DarenShowcaseView initialState={introState()} />);

    expect(html).toContain('Ограбление поместья');
    expect(html).toContain('Дарен');
    expect(html).toContain('отдельная');
    expect(html).toContain('Чернильные Перья');
    expect(html).toContain('Начать вылазку');
    expect(html).not.toContain('Свободная тренировка');
    expect(html).not.toMatch(/\/api\//i);
    expect(html).not.toMatch(/\bDTO\b/i);
    expect(html).not.toMatch(/\bendpoint\b/i);
    expect(html).not.toMatch(/\bdebug\b/i);
    expect(html).not.toMatch(/\bJSON\b/i);
    expect(html).not.toMatch(/manual/i);
  });

  it('uses the shared QTE mini-game surface for active Daren actions', () => {
    const html = renderToStaticMarkup(<DarenShowcaseView initialState={activeState()} />);

    expect(html).toContain('Тихий проход');
    expect(html).toContain('Подготовьтесь перед запуском мини-игры');
    expect(html).toContain('Счёт вылазки');
    expect(html).toContain('Скрытность');
    expect(html).not.toContain('Исход проверки');
    expect(html).not.toContain('<select');
    expect(html).not.toContain('StealthNoise');
  });

  it('formats active scene prose without repeating it in the ready gate', () => {
    const state = activeState();
    state.activeScene.currentChapter.narrative =
      'Дарен проверяет ремень с отмычками у стены поместья. За окнами проходит стражник с фонарём. ' +
      'Ветер несёт запах мокрого камня и гари от сторожевой жаровни. Теперь нужно дождаться пустого промежутка между обходами.';

    const html = renderToStaticMarkup(<DarenShowcaseView initialState={state} />);

    expect(html).toContain('class="daren-showcase-prose"');
    expect((html.match(/class="daren-showcase-prose__paragraph"/g) ?? []).length).toBeGreaterThanOrEqual(2);
    expect((html.match(/Дарен проверяет ремень/g) ?? []).length).toBe(1);
    expect(html).toContain('Нажмите, когда будете готовы.');
    expect(html).toContain('Таймер начнётся после нажатия.');
    expect(html).not.toContain('Прочитайте сцену и запускайте таймер');
  });

  it('shows deterministic ending and New Game reward source without practice wording', () => {
    const html = renderToStaticMarkup(<DarenShowcaseView initialState={completedState()} />);

    expect(html).toContain('Идеальная тень');
    expect(html).toContain('6 Чернильных Перьев');
    expect(html).toContain('будущ');
    expect(html).toContain('Книга');
    expect(html).not.toContain('тренировочный счёт');
    expect(html).not.toMatch(/manual grade/i);
  });

  it('does not present a lower replay tier as the saved future New Game reward', () => {
    const html = renderToStaticMarkup(<DarenShowcaseView initialState={lowerReplayAfterPerfectState()} />);

    expect(html).toContain('Идеальная тень');
    expect(html).toContain('6 Чернильных Перьев');
    expect(html).toContain('Чистая кража');
    expect(html).toContain(
      'Будущая новая игра помнит лучший след: Идеальная тень, 6 Чернильных Перьев. Эта вылазка завершилась как Чистая кража, счёт 80/100.'
    );
    expect(html).not.toContain('Будущая новая игра помнит лучший след: Чистая кража, 4 Чернильных Пера');
    expect(html).not.toContain('будущий бонус 4 Чернильных Перьев');
    expect(html).not.toContain('+4 Чернильных Перьев для будущей новой игры');
  });

  it('keeps launcher, route, and typed API wiring explicit', () => {
    const app = readFileSync(join(cwd, 'src', 'App.tsx'), 'utf-8');
    const shellContext = readFileSync(join(cwd, 'src', 'context', 'ShellContext.tsx'), 'utf-8');
    const launcher = readFileSync(join(cwd, 'src', 'components', 'GameLauncher.tsx'), 'utf-8');
    const client = readFileSync(join(cwd, 'src', 'api', 'client.ts'), 'utf-8');
    const component = readFileSync(join(cwd, 'src', 'components', 'DarenShowcaseView.tsx'), 'utf-8');

    expect(app).toContain("import { DarenShowcaseView } from './components/DarenShowcaseView';");
    expect(app).toContain("activeRoute === 'daren-showcase'");
    expect(app).toContain('<DarenShowcaseView />');
    expect(app).toContain('!isDarenShowcaseRoute');
    expect(shellContext).toContain("'daren-showcase'");
    expect(launcher).toContain("'daren-showcase'");
    expect(launcher).toContain("action.id === 'daren-showcase'");
    expect(launcher).toContain("onActiveRouteChange('daren-showcase')");
    expect(client).toContain('getDarenShowcase');
    expect(client).toContain('/api/qte/daren');
    expect(client).toContain('resolveDarenShowcaseAction');
    expect(component).toContain("import { QteMiniGame } from './qte/QteMiniGame';");
    expect(component).toContain('browserApi.resolveDarenShowcaseAction');
  });
});

function introState(): DarenShowcaseWebStateDto {
  return {
    state: 'Intro',
    introTitle: 'Ограбление поместья Дареном',
    introText: 'Дарен выходит к запертому поместью за магическим посохом.',
    boundaryNotice: 'Это отдельная QTE-вылазка: обычная глава не меняется.',
    rewardNotice: 'Лучший итог откроет Чернильные Перья для будущей новой игры.',
    bestReward: null,
    activeScene: null,
    resolution: null,
    completion: null,
    ending: null,
    availableOperations: ['start'],
    notification: null,
    error: null
  };
}

function activeState(): DarenShowcaseWebStateDto {
  return {
    ...introState(),
    state: 'Active',
    activeScene: {
      qteId: 'daren_qte_showcase',
      title: 'Ограбление поместья',
      acceptedAtTurn: 0,
      currentChapter: {
        chapterId: 'stealth_crossing',
        title: 'Тихий проход',
        narrative: 'Дарен гасит шум плаща у галереи.',
        chapterImagePrompt: null,
        actions: [
          action('StealthNoise', {
            kind: 'StealthNoise',
            supported: true,
            durationMs: 6000,
            startingNoise: 12,
            dangerThreshold: 70,
            noiseDriftPerSecond: 9,
            recoveryPerInput: 12,
            allowedOverThresholdMs: 800,
            recoveryKey: 'space',
            gradeThresholds: {
              successMaxNoise: 45,
              successMaxOverThresholdMs: 0,
              partialMaxNoise: 75,
              partialMaxOverThresholdMs: 800
            }
          })
        ]
      },
      scoreState: {
        metrics: [
          { id: 'normalized_score', label: 'Счёт вылазки', value: 72, min: 0, max: 100, visibility: 'always' },
          { id: 'stealth', label: 'Скрытность', value: 80, min: 0, max: 100, visibility: 'always' }
        ]
      }
    },
    availableOperations: ['submitAction', 'exit']
  };
}

function completedState(): DarenShowcaseWebStateDto {
  return {
    ...introState(),
    state: 'Completed',
    completion: {
      qteId: 'daren_qte_showcase',
      outcomeId: 'perfect_shadow',
      summary: 'Идеальная тень: лучший результат сохранён.',
      scoreSummary: {
        rank: { id: 'perfect_shadow', label: 'Идеальная тень', summary: 'Чистая вылазка без заметных следов.' },
        metrics: [
          { id: 'normalized_score', label: 'Счёт вылазки', value: 94, min: 0, max: 100, visibility: 'always' }
        ]
      }
    },
    ending: {
      tierId: 'perfect_shadow',
      displayName: 'Идеальная тень',
      normalizedScore: 94,
      inkFeatherBonus: 6,
      grantsReward: true,
      epilogue: 'Дарен уходит под мост без свидетелей. Дом просыпается слишком поздно, а посох уже молчит в тайнике.',
      rewardExplanation: 'Книга признаёт Дарена тенью этой ночи. На будущей новой странице она развернёт для него шесть Чернильных Перьев. Это не добыча из кармана, а память о краже, которую никто не сумел назвать.',
      rewardMessage: 'Книга признаёт Дарена тенью этой ночи. На будущей новой странице она развернёт для него шесть Чернильных Перьев.'
    },
    availableOperations: ['retry', 'exit']
  };
}

function lowerReplayAfterPerfectState(): DarenShowcaseWebStateDto {
  return {
    ...completedState(),
    bestReward: {
      tierId: 'perfect_shadow',
      tierName: 'Идеальная тень',
      inkFeatherBonus: 6,
      bestScore: 94,
      completedAtUtc: '2026-06-11T01:00:00Z'
    },
    completion: {
      qteId: 'daren_qte_showcase',
      outcomeId: 'clean_heist',
      summary: 'Чистая кража: Дарен донёс посох, но не переписал лучшую легенду.',
      scoreSummary: {
        rank: { id: 'clean_heist', label: 'Чистая кража', summary: 'Посох вынесен с управляемыми следами.' },
        metrics: [
          { id: 'normalized_score', label: 'Счёт вылазки', value: 80, min: 0, max: 100, visibility: 'always' }
        ]
      }
    },
    ending: {
      tierId: 'clean_heist',
      displayName: 'Чистая кража',
      normalizedScore: 80,
      inkFeatherBonus: 4,
      grantsReward: true,
      epilogue: 'Дарен закрывает тайник до рассвета, но помнит, что эта ночь тяжелее прежней легенды.',
      rewardExplanation: 'Книга уже хранит более чистую тень Дарена. Будущая новая игра сохранит высшую память и не обменяет её на более слабый след.',
      rewardMessage: 'Книга уже хранит более чистую тень Дарена. Будущая новая игра сохранит высшую память и не обменяет её на более слабый след.'
    }
  };
}

function action(checkType: string, checkConfig: Record<string, unknown>): QteWebActionDto {
  return {
    actionId: checkType.toLowerCase(),
    label: 'Пройти без шума',
    checkType,
    baseDifficulty: 3,
    primaryCharacteristic: 'dexterity',
    requiresSubmittedGrade: true,
    gradeOptions: ['success', 'partial', 'fail'],
    checkConfig
  } as unknown as QteWebActionDto;
}
