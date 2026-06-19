import { useEffect, useMemo, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserApiResult, QtePracticeCatalogEntryDto, QtePracticeWebStateDto, QteWebScoreMetricDto, QteWebScoreSummaryDto } from '../api/contracts';
import { isSuccess } from '../context/ShellContext';
import { formatQteActionCheck, formatQteGradeLabel, normalizeQteGrade, type QteAction, type QteGrade } from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';
import { qteLayoutSupportNote } from '../utils/qteKeyInput';
import { getQteTypeGlyph } from '../lib/icons';
import { QteMiniGame } from './qte/QteMiniGame';

interface QtePracticeViewProps {
  initialState?: QtePracticeWebStateDto;
}

export function QtePracticeView({ initialState }: QtePracticeViewProps) {
  const [practiceState, setPracticeState] = useState<QtePracticeWebStateDto | null>(initialState ?? null);
  const [selectedDifficulties, setSelectedDifficulties] = useState<Record<string, string>>({});
  const [result, setResult] = useState<BrowserApiResult<QtePracticeWebStateDto> | null>(null);
  const [notice, setNotice] = useState('');
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [readyActionId, setReadyActionId] = useState<string | null>(null);

  useEffect(() => {
    if (initialState) {
      setPracticeState(initialState);
      return undefined;
    }

    let cancelled = false;
    setNotice('Открываем тренировку…');
    void browserApi.getQtePractice().then((response) => {
      if (cancelled) {
        return;
      }
      setResult(response);
      if (isSuccess(response)) {
        setPracticeState(response.data);
        setNotice('');
      } else {
        setNotice(toPlayerFacingText(response.playerMessage, 'Тренировка сейчас недоступна.'));
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

  const state = practiceState ?? initialState ?? emptyPracticeState();
  const activeChapter = state.activeScene?.currentChapter;
  const activeAction = activeChapter?.actions[0] ?? null;
  const catalogByType = useMemo(
    () => new Map(state.catalog.map((entry) => [entry.typeId, entry])),
    [state.catalog]
  );
  const selectedEntry = state.selectedTypeId ? catalogByType.get(state.selectedTypeId) ?? null : null;
  const selectedDifficulty = selectedEntry?.difficulties.find((difficulty) => difficulty.difficultyId === state.selectedDifficultyId) ?? null;
  const activeActionIsReady = activeAction ? readyActionId === activeAction.actionId : false;

  async function applyResponse(response: BrowserApiResult<QtePracticeWebStateDto>, fallback: string) {
    setResult(response);
    if (isSuccess(response)) {
      setReadyActionId(null);
      setPracticeState(response.data);
      setNotice(response.data.notification ?? fallback);
    } else {
      setNotice(toPlayerFacingText(response.playerMessage, fallback));
    }
  }

  async function startAttempt(entry: QtePracticeCatalogEntryDto, difficultyId: string) {
    if (!entry.available) {
      setNotice(toPlayerFacingText(entry.unavailableReason, 'Эта тренировка пока недоступна.'));
      return;
    }

    setBusyKey(`${entry.typeId}:${difficultyId}`);
    setNotice('Готовим тренировку…');
    try {
      await applyResponse(
        await browserApi.startQtePractice({ typeId: entry.typeId, difficultyId }),
        'Тренировка началась.'
      );
    } catch {
      setNotice('Не удалось начать тренировку. Попробуйте ещё раз.');
    } finally {
      setBusyKey(null);
    }
  }

  async function resolvePracticeAction(action: QteAction, grade: QteGrade | null) {
    setBusyKey(action.actionId);
    setNotice('Записываем тренировочный результат…');
    try {
      await applyResponse(
        await browserApi.resolveQtePracticeAction({ actionId: action.actionId, grade }),
        'Попытка завершена.'
      );
    } catch {
      setNotice('Не удалось записать результат. Попробуйте ещё раз.');
    } finally {
      setBusyKey(null);
    }
  }

  async function retryAttempt() {
    setBusyKey('retry');
    setNotice('Повторяем тренировку…');
    try {
      await applyResponse(await browserApi.retryQtePractice(), 'Тренировка повторена.');
    } catch {
      setNotice('Не удалось повторить тренировку.');
    } finally {
      setBusyKey(null);
    }
  }

  async function exitPractice() {
    setBusyKey('exit');
    setNotice('Закрываем тренировку…');
    try {
      await applyResponse(await browserApi.exitQtePractice(), 'Тренировка закрыта.');
    } catch {
      setNotice('Не удалось закрыть тренировку.');
    } finally {
      setBusyKey(null);
    }
  }

  return (
    <section className="qte-practice-view" aria-labelledby="qte-practice-title">
      <header className="qte-practice-hero">
        <div>
          <p className="panel-eyebrow">тренировка QTE</p>
          <h2 id="qte-practice-title">Свободная тренировка</h2>
          <p>{toPlayerFacingText(state.feedback, 'Выберите тип QTE и сыграйте тренировочную попытку.')}</p>
        </div>
        <p className="qte-practice-notice">{toPlayerFacingText(state.localScoreNotice, 'Тренировочный счёт не меняет прохождение.')}</p>
      </header>

      {state.error && <p className="warning-text">{toPlayerFacingText(state.error, 'Тренировка требует внимания.')}</p>}

      {state.state === 'Catalog' || !state.activeScene ? (
        <div className="qte-practice-catalog" aria-label="Типы QTE для тренировки">
          {state.catalog.map((entry) => {
            const selectedDifficulty = selectedDifficulties[entry.typeId] ?? entry.difficulties.find((difficulty) => difficulty.difficultyId === 'normal')?.difficultyId ?? entry.difficulties[0]?.difficultyId ?? 'normal';
            const Glyph = getQteTypeGlyph(entry.typeId);
            return (
              <article key={entry.typeId} className="qte-practice-card" data-available={entry.available ? 'true' : 'false'}>
                <div className="qte-practice-card__crest" aria-hidden="true">
                  <Glyph className="qte-practice-card__crest-glyph" strokeWidth={1.6} />
                </div>
                <div className="qte-practice-card__body">
                  <header>
                    <div>
                      <h3>{toPlayerFacingText(entry.title, entry.typeId)}</h3>
                      <p className="qte-practice-card__type">Мини-игра без наград</p>
                    </div>
                    <span className="availability-pill">{entry.available ? 'доступно' : 'позже'}</span>
                  </header>
                  <p>{toPlayerFacingText(entry.description, 'Тренировка без наград и без изменения прохождения.')}</p>
                  <p className="muted">{toPlayerFacingText(entry.instructions, 'Сыграйте мини-игру и посмотрите тренировочный результат.')}</p>
                  <div className="qte-practice-difficulty" role="group" aria-label={`Сложность ${entry.title}`}>
                    {entry.difficulties.map((difficulty) => (
                      <button
                        key={difficulty.difficultyId}
                        type="button"
                        className={selectedDifficulty === difficulty.difficultyId ? 'is-active' : ''}
                        onClick={() => setSelectedDifficulties((current) => ({ ...current, [entry.typeId]: difficulty.difficultyId }))}
                      >
                        {difficulty.label}
                      </button>
                    ))}
                  </div>
                  <button
                    type="button"
                    className="launcher-secondary-action qte-practice-card__start"
                    disabled={!entry.available || busyKey !== null}
                    onClick={() => void startAttempt(entry, selectedDifficulty)}
                  >
                    {busyKey === `${entry.typeId}:${selectedDifficulty}` ? 'Готовим…' : 'Начать тренировку'}
                  </button>
                </div>
              </article>
            );
          })}
        </div>
      ) : (
        <article className="summary-card qte-practice-attempt">
          <header>
            <div>
              <h3>{toPlayerFacingText(state.activeScene.title, selectedEntry?.title ?? 'Тренировка активна')}</h3>
              <p>{toPlayerFacingText(activeChapter?.narrative ?? state.feedback, 'Сыграйте тренировочную мини-игру.')}</p>
            </div>
            <span className="availability-pill">{toPlayerFacingText(selectedDifficulty?.label, 'тренировка')}</span>
          </header>
          <p className="muted">{qteLayoutSupportNote}</p>
          {renderScoreMetrics(state.activeScene.scoreState?.metrics ?? [], 'Тренировочный счёт', false)}
          {activeAction ? (
            <div className="qte-action-list">
              <article className="action-card">
                <header>
                  <h4>{toPlayerFacingText(activeAction.label, selectedEntry?.title ?? 'Тренировочное действие')}</h4>
                  <span className="availability-pill">{formatQteActionCheck(activeAction)}</span>
                </header>
                {activeActionIsReady ? (
                  <QteMiniGame
                    action={activeAction}
                    disabled={busyKey !== null}
                    onSubmit={(grade) => void resolvePracticeAction(activeAction, grade)}
                  />
                ) : (
                  <div className="qte-practice-ready-gate">
                    <h4>Подготовьтесь перед запуском мини-игры</h4>
                    <p>{toPlayerFacingText(activeChapter?.narrative ?? state.feedback, 'Прочитайте подсказку и запускайте таймер только когда будете готовы.')}</p>
                    <p className="muted">Таймер и действия начнутся только после этой кнопки. Попытка остаётся тренировочной: без наград и без изменения прохождения.</p>
                    <button type="button" onClick={() => setReadyActionId(activeAction.actionId)} disabled={busyKey !== null}>
                      Начать мини-игру
                    </button>
                  </div>
                )}
              </article>
            </div>
          ) : (
            <p className="muted">Тренировка ждёт следующий шаг.</p>
          )}
        </article>
      )}

      {state.resolution && (
        <article className="summary-card">
          <h3>Итог попытки</h3>
          <p>{toPlayerFacingText(state.resolution.resultText, 'Тренировочный результат записан.')}</p>
          <p className="muted">Исход: {formatQteGradeLabel(normalizeQteGrade(state.resolution.grade))}</p>
        </article>
      )}

      {state.completion && (
        <article className="summary-card">
          <h3>{toPlayerFacingText(state.feedbackTitle, 'Попытка завершена')}</h3>
          <p>{toPlayerFacingText(state.feedback, 'Итог показан только для тренировки.')}</p>
          {renderScoreSummary(state.completion.scoreSummary)}
          <div className="phase-chip-grid">
            <button type="button" onClick={() => void retryAttempt()} disabled={busyKey !== null}>Повторить</button>
            {selectedEntry?.difficulties.map((difficulty) => (
              <button
                key={difficulty.difficultyId}
                type="button"
                onClick={() => void startAttempt(selectedEntry, difficulty.difficultyId)}
                disabled={busyKey !== null}
              >
                {difficulty.difficultyId === state.selectedDifficultyId ? 'Сменить сложность' : difficulty.label}
              </button>
            ))}
            <button
              type="button"
              onClick={() => {
                setReadyActionId(null);
                setPracticeState({ ...state, state: 'Catalog', activeScene: null, completion: null, resolution: null });
              }}
              disabled={busyKey !== null}
            >
              Выбрать другое QTE
            </button>
            <button type="button" onClick={() => void exitPractice()} disabled={busyKey !== null}>Выйти</button>
          </div>
        </article>
      )}

      {notice && <p className="composer-notice">{notice}</p>}
      {result && !isSuccess(result) && <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Тренировка не смогла обновиться.')}</p>}
    </section>
  );
}

function emptyPracticeState(): QtePracticeWebStateDto {
  return {
    state: 'Catalog',
    catalog: [],
    selectedTypeId: null,
    selectedDifficultyId: null,
    activeScene: null,
    resolution: null,
    completion: null,
    feedbackTitle: 'Свободная тренировка',
    feedback: 'Выберите тип QTE. Тренировка не меняет сюжет и не выдаёт награды.',
    localScoreNotice: 'Тренировочный счёт не меняет прохождение.',
    availableOperations: ['startAttempt', 'exit'],
    notification: null,
    error: null
  };
}

function renderScoreSummary(scoreSummary: QteWebScoreSummaryDto | null | undefined) {
  if (!scoreSummary) {
    return null;
  }

  return (
    <div className="qte-score-summary" aria-label="Итоговый тренировочный счёт">
      {scoreSummary.rank?.label && <p className="qte-score-rank">Ранг: {toPlayerFacingText(scoreSummary.rank.label, 'итог тренировки')}</p>}
      {scoreSummary.rank?.summary && <p>{toPlayerFacingText(scoreSummary.rank.summary, 'Тренировочный итог записан.')}</p>}
      {renderScoreMetrics(scoreSummary.metrics, 'Тренировочный счёт', true)}
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
          const value = formatScoreValue(metric.value);
          const range = metric.max > metric.min ? Math.round(((metric.value - metric.min) / (metric.max - metric.min)) * 100) : 100;
          return (
            <div className="qte-score-metric" key={metric.id}>
              <span className="qte-score-metric__label">{toPlayerFacingText(metric.label, 'Показатель тренировки')}</span>
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
