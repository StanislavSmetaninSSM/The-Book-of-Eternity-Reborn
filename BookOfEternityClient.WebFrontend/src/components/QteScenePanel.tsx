import { useEffect, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserApiResult, BrowserGameScreenDto, QteWebScoreMetricDto, QteWebScoreSummaryDto } from '../api/contracts';
import { isSuccess } from '../context/ShellContext';
import {
  formatQteActionCheck,
  formatQteGradeLabel,
  formatQteStateLabel,
  normalizeQteGrade,
  type QteAction,
  type QteGrade
} from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';
import { qteLayoutSupportNote } from '../utils/qteKeyInput';
import { QteMiniGame } from './qte/QteMiniGame';

export function QteScenePanel({ qte }: { qte: BrowserGameScreenDto['qte'] }) {
  const [qteState, setQteState] = useState(qte);
  const [result, setResult] = useState<BrowserApiResult<BrowserGameScreenDto['qte']> | null>(null);
  const [notice, setNotice] = useState('');
  const [submitting, setSubmitting] = useState<string | null>(null);

  useEffect(() => {
    setQteState(qte);
  }, [qte]);

  async function resolveOffer(decision: 'accept' | 'decline') {
    setSubmitting(`offer-${decision}`);
    setNotice(decision === 'accept' ? 'Принимаем быструю сцену…' : 'Отклоняем быструю сцену…');

    try {
      const response = await browserApi.resolveQteOffer({ decision });
      setResult(response);
      if (isSuccess(response)) {
        setQteState(response.data);
        setNotice(formatQteStateLabel(response.data));
      } else {
        setNotice(toPlayerFacingText(response.playerMessage, 'Не удалось изменить быструю сцену.'));
      }
    } catch {
      setNotice('Не удалось связаться с локальной книгой. Попробуйте ещё раз.');
    } finally {
      setSubmitting(null);
    }
  }

  async function resolveAction(action: QteAction, grade: QteGrade | null) {
    setSubmitting(`action-${action.actionId}`);
    setNotice('Записываем выбор быстрой сцены…');

    try {
      const response = await browserApi.resolveQteAction({ actionId: action.actionId, grade });
      setResult(response);
      if (isSuccess(response)) {
        setQteState(response.data);
        setNotice(formatQteStateLabel(response.data));
      } else {
        setNotice(toPlayerFacingText(response.playerMessage, 'Не удалось записать выбор быстрой сцены.'));
      }
    } catch {
      setNotice('Не удалось связаться с локальной книгой. Попробуйте ещё раз.');
    } finally {
      setSubmitting(null);
    }
  }

  const activeChapter = qteState.activeScene?.currentChapter;
  const hasVisibleState = Boolean(qteState.offer || qteState.activeScene || qteState.resolution || qteState.completion || qteState.error);

  return (
    <section className="qte-scene-panel" aria-labelledby="qte-scene-panel-title">
      <div>
        <p className="panel-eyebrow">быстрая сцена</p>
        <h2 id="qte-scene-panel-title">Сцена выбора</h2>
        <p>{formatQteStateLabel(qteState)}</p>
        {qteState.lastResolvedReminder && <p className="muted">{toPlayerFacingText(qteState.lastResolvedReminder, 'Последний итог быстрой сцены записан.')}</p>}
      </div>

      {qteState.error && <p className="warning-text">{toPlayerFacingText(qteState.error, 'Быстрая сцена требует внимания.')}</p>}

      {qteState.offer && (
        <article className="summary-card">
          <h3>{toPlayerFacingText(qteState.offer.title, 'Быстрая сцена')}</h3>
          <p>{toPlayerFacingText(qteState.offer.offerText ?? qteState.offer.introNarrative, 'Книга предлагает короткую сцену выбора.')}</p>
          <p className="muted">{qteLayoutSupportNote}</p>
          {qteState.offer.cinematicJustification && <p className="muted">{toPlayerFacingText(qteState.offer.cinematicJustification, 'Сцена подходит текущему моменту.')}</p>}
          {qteState.offer.sceneImagePrompt && <p className="muted">Образ сцены: {toPlayerFacingText(qteState.offer.sceneImagePrompt, 'образ уточняется')}</p>}
          {qteState.offer.declineHint && <p className="muted">{toPlayerFacingText(qteState.offer.declineHint, 'Можно отказаться и продолжить обычный ход.')}</p>}
          <div className="phase-chip-grid">
            <button type="button" onClick={() => void resolveOffer('accept')} disabled={Boolean(submitting)}>
              Принять сцену
            </button>
            <button type="button" onClick={() => void resolveOffer('decline')} disabled={Boolean(submitting)}>
              Отказаться
            </button>
          </div>
        </article>
      )}

      {qteState.activeScene && (
        <article className="summary-card">
          <h3>{toPlayerFacingText(qteState.activeScene.title, 'Быстрая сцена активна')}</h3>
          {activeChapter ? (
            <>
              <p>{toPlayerFacingText(activeChapter.narrative ?? activeChapter.title, 'Выберите действие для этой сцены.')}</p>
              {renderScoreMetrics(qteState.activeScene.scoreState?.metrics ?? [], 'Счёт сцены', false)}
              <p className="muted">{qteLayoutSupportNote}</p>
              {activeChapter.chapterImagePrompt && <p className="muted">Образ главы: {toPlayerFacingText(activeChapter.chapterImagePrompt, 'образ уточняется')}</p>}
              {activeChapter.actions.length > 0 ? (
                <div className="qte-action-list">
                  {activeChapter.actions.map((action) => {
                    return (
                      <article key={action.actionId} className="action-card">
                        <header>
                          <h4>{toPlayerFacingText(action.label, 'Действие сцены')}</h4>
                          <span className="availability-pill">{formatQteActionCheck(action)}</span>
                        </header>
                        <QteMiniGame
                          action={action}
                          disabled={Boolean(submitting)}
                          onSubmit={(grade) => void resolveAction(action, grade)}
                        />
                      </article>
                    );
                  })}
                </div>
              ) : (
                <p className="muted">Сцена ждёт следующий фрагмент выбора.</p>
              )}
            </>
          ) : (
            <p className="muted">Книга готовит следующий фрагмент быстрой сцены.</p>
          )}
        </article>
      )}

      {qteState.resolution && (
        <article className="summary-card">
          <h3>Итог выбора</h3>
          <p>{toPlayerFacingText(qteState.resolution.resultText, 'Итог быстрой сцены записан.')}</p>
          <p className="muted">Исход: {formatQteGradeLabel(normalizeQteGrade(qteState.resolution.grade))}</p>
        </article>
      )}

      {qteState.completion && (
        <article className="summary-card">
          <h3>Сцена завершена</h3>
          <p>{toPlayerFacingText(qteState.completion.summary, 'Быстрая сцена завершилась.')}</p>
          {renderScoreSummary(qteState.completion.scoreSummary)}
        </article>
      )}

      {!hasVisibleState && <p className="muted">Быстрая сцена появится здесь, когда книга предложит короткий выбор или кинематик-эпизод.</p>}
      {notice && <p className="composer-notice">{notice}</p>}
      {result && !isSuccess(result) && <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Быстрая сцена не смогла обновиться.')}</p>}
    </section>
  );
}

function renderScoreSummary(scoreSummary: QteWebScoreSummaryDto | null | undefined) {
  if (!scoreSummary) {
    return null;
  }

  const rankLabel = scoreSummary.rank?.label
    ? toPlayerFacingText(scoreSummary.rank.label, 'итог сцены')
    : '';
  return (
    <div className="qte-score-summary" aria-label="Итоговый счёт сцены">
      {rankLabel && <p className="qte-score-rank">Ранг: {rankLabel}</p>}
      {scoreSummary.rank?.summary && <p>{toPlayerFacingText(scoreSummary.rank.summary, 'Итог быстрой сцены записан.')}</p>}
      {renderScoreMetrics(scoreSummary.metrics, 'Итог сцены', true)}
    </div>
  );
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
          const label = toPlayerFacingText(metric.label, 'Показатель сцены');
          const value = formatScoreValue(metric.value);
          const range = metric.max > metric.min ? Math.round(((metric.value - metric.min) / (metric.max - metric.min)) * 100) : 100;
          const clampedRange = Math.min(100, Math.max(0, range));
          return (
            <div className="qte-score-metric" key={metric.id}>
              <span className="qte-score-metric__label">{label}</span>
              <span className="qte-score-metric__value">{value}</span>
              <span className="qte-score-meter" aria-label={`${label}: ${value}`}>
                <span style={{ width: `${clampedRange}%` }} />
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
