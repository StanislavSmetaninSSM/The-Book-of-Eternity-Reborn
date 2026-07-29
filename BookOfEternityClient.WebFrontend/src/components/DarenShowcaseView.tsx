import { useEffect, useState } from 'react';
import { browserApi } from '../api/client';
import type {
  BrowserApiResult,
  DarenShowcaseWebStateDto,
  QteWebActionDto,
  QteWebScoreMetricDto,
  QteWebScoreSummaryDto
} from '../api/contracts';
import { isSuccess } from '../context/ShellContext';
import { toPlayerFacingText } from '../utils/playerCopy';
import { qteLayoutSupportNote } from '../utils/qteKeyInput';
import { QteMiniGame } from './qte/QteMiniGame';

interface DarenShowcaseViewProps {
  initialState?: DarenShowcaseWebStateDto;
}

export function DarenShowcaseView({ initialState }: DarenShowcaseViewProps) {
  const [showcaseState, setShowcaseState] = useState<DarenShowcaseWebStateDto | null>(initialState ?? null);
  const [result, setResult] = useState<BrowserApiResult<DarenShowcaseWebStateDto> | null>(null);
  const [notice, setNotice] = useState('');
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [readyActionId, setReadyActionId] = useState<string | null>(null);
  // When the backend returns a resolution (the just-played mini-game's result)
  // it also returns the next activeScene. Show ONLY the result in the main
  // narrative field with a "Продолжить" gate until the player advances, so the
  // result and the next scene are never shown at once. Tracked by the
  // resolution's actionId so a new resolution re-arms the gate.
  const [dismissedResolutionActionId, setDismissedResolutionActionId] = useState<string | null>(null);
  const resolutionActionId = showcaseState?.resolution?.actionId ?? null;
  const showResolution = Boolean(showcaseState?.resolution && !showcaseState?.completion && resolutionActionId !== dismissedResolutionActionId);

  useEffect(() => {
    if (initialState) {
      setShowcaseState(initialState);
      return undefined;
    }

    let cancelled = false;
    setNotice('Открываем вылазку Дарена…');
    void browserApi.getDarenShowcase().then((response) => {
      if (cancelled) {
        return;
      }
      setResult(response);
      if (isSuccess(response)) {
        setShowcaseState(response.data);
        setNotice('');
      } else {
        setNotice(toPlayerFacingText(response.playerMessage, 'Вылазка Дарена сейчас недоступна.'));
      }
    }).catch(() => {
      if (!cancelled) {
        setNotice('Не удалось связаться с локальной книгой. Попробуйте ещё раз.');
      }
    });

    return () => {
      cancelled = true;
    };
  }, [initialState]);

  const state = showcaseState ?? initialState ?? emptyDarenShowcaseState();
  const activeScene = state.activeScene;
  const activeChapter = activeScene?.currentChapter ?? null;
  const activeAction = activeChapter?.actions[0] ?? null;
  const activeActionIsReady = activeAction ? readyActionId === activeAction.actionId : false;
  const isCompleted = state.state === 'Completed' || state.completion !== null;

  async function applyResponse(response: BrowserApiResult<DarenShowcaseWebStateDto>, fallback: string) {
    setResult(response);
    if (isSuccess(response)) {
      setReadyActionId(null);
      setShowcaseState(response.data);
      setNotice(response.data.notification ?? fallback);
    } else {
      setNotice(toPlayerFacingText(response.playerMessage, fallback));
    }
  }

  async function startShowcase() {
    if (!state.interactionToken) {
      setNotice('Вылазка уже изменилась. Обновите экран и повторите выбор.');
      return;
    }

    setBusyKey('start');
    setNotice('Дарен выходит к поместью…');
    try {
      await applyResponse(
        await browserApi.startDarenShowcase({
          interactionToken: state.interactionToken
        }),
        'Вылазка Дарена началась.'
      );
    } catch {
      setNotice('Не удалось начать вылазку. Попробуйте ещё раз.');
    } finally {
      setBusyKey(null);
    }
  }

  async function resolveDarenAction(action: QteWebActionDto, grade: string | null) {
    if (!state.interactionToken) {
      setNotice('Вылазка уже изменилась. Обновите экран и повторите выбор.');
      return;
    }

    setBusyKey(action.actionId);
    setNotice('Фиксируем ход Дарена…');
    try {
      await applyResponse(
        await browserApi.resolveDarenShowcaseAction({
          actionId: action.actionId,
          grade,
          interactionToken: state.interactionToken
        }),
        'Вылазка продолжается.'
      );
    } catch {
      setNotice('Не удалось записать результат. Попробуйте ещё раз.');
    } finally {
      setBusyKey(null);
    }
  }

  async function retryShowcase() {
    if (!state.interactionToken) {
      setNotice('Вылазка уже изменилась. Обновите экран и повторите выбор.');
      return;
    }

    setBusyKey('retry');
    setNotice('Дарен начинает вылазку заново…');
    try {
      await applyResponse(
        await browserApi.retryDarenShowcase({
          interactionToken: state.interactionToken
        }),
        'Вылазка началась заново.'
      );
    } catch {
      setNotice('Не удалось повторить вылазку.');
    } finally {
      setBusyKey(null);
    }
  }

  async function exitShowcase() {
    if (!state.interactionToken) {
      setNotice('Вылазка уже изменилась. Обновите экран и повторите выбор.');
      return;
    }

    setBusyKey('exit');
    setNotice('Закрываем вылазку Дарена…');
    try {
      await applyResponse(
        await browserApi.exitDarenShowcase({
          interactionToken: state.interactionToken
        }),
        'Вылазка Дарена закрыта.'
      );
    } catch {
      setNotice('Не удалось закрыть вылазку.');
    } finally {
      setBusyKey(null);
    }
  }

  return (
    <section className="qte-practice-view daren-showcase-view" aria-labelledby="daren-showcase-title">
      <header className="qte-practice-hero daren-showcase-hero">
        <div>
          <p className="panel-eyebrow">QTE-вылазка</p>
          <h2 id="daren-showcase-title">{toPlayerFacingText(state.introTitle, 'Ограбление поместья Дареном')}</h2>
          <p>{toPlayerFacingText(state.introText, 'Дарен идёт за магическим посохом в запертое поместье.')}</p>
        </div>
        <p className="qte-practice-notice">{toPlayerFacingText(state.rewardNotice, 'Лучший итог открывает Чернильные Перья для будущей новой игры.')}</p>
      </header>

      <p className="muted">{toPlayerFacingText(state.boundaryNotice, 'Это отдельная вылазка: обычная глава не меняется.')}</p>
      {state.bestReward && (
        <p className="composer-notice">
          {toPlayerFacingText(
            state.bestReward.summary,
            `Постоянный итог Дарена: ${state.bestReward.tierName}. Будущая новая игра получит ${formatInkFeatherCount(state.bestReward.inkFeatherBonus)} один раз при создании новой игры; повторные вылазки не складывают перья.`
          )}
        </p>
      )}
      {state.error && <p className="warning-text">{toPlayerFacingText(state.error, 'Вылазка требует внимания.')}</p>}

      {!isCompleted && showResolution && state.resolution ? (
        <article className="summary-card qte-practice-attempt daren-showcase-attempt daren-showcase-resolution">
          <header>
            <h3>{toPlayerFacingText(activeChapter?.title ?? activeScene?.title, 'Результат действия')}</h3>
            <span className="availability-pill">вылазка</span>
          </header>
          {renderDarenProse(state.resolution.resultText, 'Дарен проходит к следующей точке вылазки.')}
          <div className="phase-chip-grid">
            <button type="button" onClick={() => setDismissedResolutionActionId(resolutionActionId)} disabled={busyKey !== null}>
              Продолжить
            </button>
          </div>
        </article>
      ) : !isCompleted && (state.state === 'Intro' || !state.activeScene) ? (
        <article className="summary-card daren-showcase-intro">
          <h3>Ограбление поместья</h3>
          <p>Дарен берёт складной крюк, отмычки и серый плащ. Цель проста: тихо войти, забрать посох, отрезать погоню и вернуться в убежище.</p>
          <div className="phase-chip-grid">
            <button type="button" onClick={() => void startShowcase()} disabled={busyKey !== null}>
              {busyKey === 'start' ? 'Начинаем…' : 'Начать вылазку'}
            </button>
          </div>
        </article>
      ) : !isCompleted ? (
        <article className="summary-card qte-practice-attempt daren-showcase-attempt">
          <header>
            <h3>{toPlayerFacingText(activeChapter?.title, activeScene?.title ?? 'Вылазка Дарена')}</h3>
            <span className="availability-pill">вылазка</span>
          </header>
          {renderDarenProse(activeChapter?.narrative, 'Дарен двигается к следующей точке вылазки.')}
          <p className="muted">{qteLayoutSupportNote}</p>
          {renderScoreMetrics(activeScene?.scoreState?.metrics ?? [], 'Счёт вылазки', false)}
          {activeAction ? (
            <div className="qte-action-list">
              <article className="action-card">
                <header>
                  <h4>{toPlayerFacingText(activeAction.label, 'Действие Дарена')}</h4>
                  <span className="availability-pill">быстрая сцена</span>
                </header>
                {activeActionIsReady ? (
                  <QteMiniGame
                    action={activeAction}
                    disabled={busyKey !== null}
                    onSubmit={(grade) => void resolveDarenAction(activeAction, grade)}
                  />
                ) : (
                  <div className="qte-practice-ready-gate">
                    <h4>Подготовьтесь перед запуском мини-игры</h4>
                    <p>Нажмите, когда будете готовы.</p>
                    <p className="muted">Таймер начнётся после нажатия. Результат считает клиентская вылазка Дарена, обычная глава не меняется.</p>
                    <button type="button" onClick={() => setReadyActionId(activeAction.actionId)} disabled={busyKey !== null}>
                      Начать мини-игру
                    </button>
                  </div>
                )}
              </article>
            </div>
          ) : (
            <p className="muted">Дарен ждёт следующий шаг.</p>
          )}
        </article>
      ) : null}

      {state.completion && (
        <article className="summary-card">
          <h3>{toPlayerFacingText(state.ending?.displayName, 'Вылазка завершена')}</h3>
          {state.ending ? (
            <>
              <p>{toPlayerFacingText(state.ending.epilogue, state.completion.summary)}</p>
              <p className="composer-notice">{toPlayerFacingText(state.ending.rewardExplanation, 'Итог Дарена записан для будущей новой игры.')}</p>
            </>
          ) : (
            <p>{toPlayerFacingText(state.completion.summary, 'Итог вылазки записан.')}</p>
          )}
          {state.ending && (
            <p className="composer-notice">
              {state.ending.grantsReward
                ? toPlayerFacingText(state.ending.rewardProfileSummary, renderDarenFutureRewardLine(state))
                : 'Безопасный итог не достигнут, постоянная награда не записана.'}
            </p>
          )}
          {state.ending?.rewardMessage && !state.ending.rewardProfileSummary && state.ending.rewardMessage !== state.ending.rewardExplanation && (
            <p>{toPlayerFacingText(state.ending.rewardMessage, 'Итог Дарена сохранён.')}</p>
          )}
          {renderScoreSummary(state.completion.scoreSummary)}
          <div className="phase-chip-grid">
            <button type="button" onClick={() => void retryShowcase()} disabled={busyKey !== null}>Повторить вылазку</button>
            <button type="button" onClick={() => void exitShowcase()} disabled={busyKey !== null}>Выйти</button>
          </div>
        </article>
      )}

      {notice && <p className="composer-notice">{notice}</p>}
      {result && !isSuccess(result) && <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Вылазка Дарена не смогла обновиться.')}</p>}
    </section>
  );
}

function emptyDarenShowcaseState(): DarenShowcaseWebStateDto {
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
    availableOperations: ['start', 'exit'],
    interactionToken: null,
    notification: null,
    errorCode: null,
    error: null
  };
}

function renderScoreSummary(scoreSummary: QteWebScoreSummaryDto | null | undefined) {
  if (!scoreSummary) {
    return null;
  }

  return (
    <div className="qte-score-summary" aria-label="Итог вылазки">
      {scoreSummary.rank?.label && <p className="qte-score-rank">Ранг: {toPlayerFacingText(scoreSummary.rank.label, 'итог вылазки')}</p>}
      {scoreSummary.rank?.summary && <p>{toPlayerFacingText(scoreSummary.rank.summary, 'Итог вылазки записан.')}</p>}
      {renderScoreMetrics(scoreSummary.metrics, 'Итоговый счёт', true)}
    </div>
  );
}

function renderDarenProse(text: string | null | undefined, fallback: string) {
  const playerText = toPlayerFacingText(text, fallback);
  const paragraphs = splitDarenProse(playerText);

  return (
    <div className="daren-showcase-prose">
      {paragraphs.map((paragraph, index) => (
        <p className="daren-showcase-prose__paragraph" key={`${index}-${paragraph.slice(0, 24)}`}>
          {paragraph}
        </p>
      ))}
    </div>
  );
}

function splitDarenProse(text: string): string[] {
  const explicitParagraphs = text
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean);
  if (explicitParagraphs.length > 1) {
    return explicitParagraphs;
  }

  const sentences = text
    .match(/[^.!?…]+[.!?…]+(?:["»])?|[^.!?…]+$/g)
    ?.map((sentence) => sentence.trim())
    .filter(Boolean) ?? [text];
  if (sentences.length <= 2) {
    return [text.trim()];
  }

  const paragraphs: string[] = [];
  for (let index = 0; index < sentences.length; index += 2) {
    paragraphs.push(sentences.slice(index, index + 2).join(' ').trim());
  }

  return paragraphs.filter(Boolean);
}

function renderScoreMetrics(metrics: readonly QteWebScoreMetricDto[], title: string, includeFinalMetrics: boolean) {
  const visibleMetrics = metrics.filter((metric) => {
    const visibility = metric.visibility.trim().toLowerCase();
    if (visibility === 'hidden') {
      return false;
    }
    return includeFinalMetrics || visibility === 'always';
  });

  if (visibleMetrics.length === 0) {
    return null;
  }

  return (
    <div className="qte-score-strip" aria-label={title}>
      <p className="qte-score-strip__title">{title}</p>
      <div className="qte-score-grid">
        {visibleMetrics.map((metric) => {
          const value = formatScoreValue(metric.value);
          const range = metric.max > metric.min ? Math.round(((metric.value - metric.min) / (metric.max - metric.min)) * 100) : 100;
          return (
            <div className="qte-score-metric" key={metric.id}>
              <span className="qte-score-metric__label">{toPlayerFacingText(metric.label, 'Показатель вылазки')}</span>
              <span className="qte-score-metric__value">{value}</span>
              <span className="qte-score-meter">
                <span style={{ width: `${Math.min(100, Math.max(0, range))}%` }} />
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function formatScoreValue(value: number): string {
  if (!Number.isFinite(value)) {
    return '0';
  }

  return Number.isInteger(value) ? value.toString() : value.toFixed(2).replace(/\.?0+$/, '');
}

function renderDarenFutureRewardLine(state: DarenShowcaseWebStateDto): string {
  if (!state.ending?.grantsReward) {
    return 'Безопасный итог не достигнут, постоянная награда не записана.';
  }

  const futureReward = state.bestReward ?? {
    tierName: state.ending.displayName,
    inkFeatherBonus: state.ending.inkFeatherBonus
  };
  return `Будущая новая игра помнит лучший след: ${futureReward.tierName}, ${formatInkFeatherCount(futureReward.inkFeatherBonus)}. Эта вылазка завершилась как ${state.ending.displayName}, счёт ${state.ending.normalizedScore}/100.`;
}

function formatInkFeatherCount(value: number): string {
  const amount = Number.isFinite(value) ? Math.max(0, Math.trunc(value)) : 0;
  const lastTwo = amount % 100;
  const lastOne = amount % 10;
  if (lastTwo < 11 || lastTwo > 14) {
    if (lastOne === 1) {
      return `${amount} Чернильное Перо`;
    }
    if (lastOne >= 2 && lastOne <= 4) {
      return `${amount} Чернильных Пера`;
    }
  }

  return `${amount} Чернильных Перьев`;
}
