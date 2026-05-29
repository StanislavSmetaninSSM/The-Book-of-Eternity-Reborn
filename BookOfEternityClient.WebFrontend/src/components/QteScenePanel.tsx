import { useEffect, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserApiResult, BrowserGameScreenDto } from '../api/contracts';
import { isSuccess } from '../context/ShellContext';
import {
  formatQteActionCheck,
  formatQteGradeLabel,
  formatQteStateLabel,
  normalizeQteGrade,
  qteGradeOptionsForAction,
  type QteAction,
  type QteGrade
} from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';

export function QteScenePanel({ qte }: { qte: BrowserGameScreenDto['qte'] }) {
  const [qteState, setQteState] = useState(qte);
  const [selectedGrades, setSelectedGrades] = useState<Record<string, QteGrade>>({});
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

  async function resolveAction(action: QteAction, gradeOverride?: QteGrade) {
    const grade = action.requiresSubmittedGrade ? gradeOverride ?? selectedGrades[action.actionId] ?? qteGradeOptionsForAction(action)[0] ?? 'success' : null;
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

  function selectGrade(actionId: string, grade: QteGrade) {
    setSelectedGrades((current) => ({ ...current, [actionId]: grade }));
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
              {activeChapter.chapterImagePrompt && <p className="muted">Образ главы: {toPlayerFacingText(activeChapter.chapterImagePrompt, 'образ уточняется')}</p>}
              {activeChapter.actions.length > 0 ? (
                <div className="qte-action-list">
                  {activeChapter.actions.map((action) => {
                    const gradeOptions = qteGradeOptionsForAction(action);
                    const selectedGrade = selectedGrades[action.actionId] ?? gradeOptions[0] ?? 'success';
                    return (
                      <article key={action.actionId} className="action-card">
                        <header>
                          <h4>{toPlayerFacingText(action.label, 'Действие сцены')}</h4>
                          <span className="availability-pill">{formatQteActionCheck(action)}</span>
                        </header>
                        {action.requiresSubmittedGrade && (
                          <div className="prompt-control">
                            <label htmlFor={`qte-grade-${action.actionId}`}>Исход проверки</label>
                            <select
                              id={`qte-grade-${action.actionId}`}
                              value={selectedGrade}
                              onChange={(event) => selectGrade(action.actionId, normalizeQteGrade(event.currentTarget.value))}
                              disabled={Boolean(submitting)}
                            >
                              {gradeOptions.map((grade) => (
                                <option key={grade} value={grade}>{formatQteGradeLabel(grade)}</option>
                              ))}
                            </select>
                            <div className="phase-chip-grid" aria-label="Быстрый выбор исхода">
                              {gradeOptions.map((grade) => (
                                <button
                                  key={grade}
                                  type="button"
                                  onClick={() => {
                                    selectGrade(action.actionId, grade);
                                    void resolveAction(action, grade);
                                  }}
                                  disabled={Boolean(submitting)}
                                >
                                  {formatQteGradeLabel(grade)}
                                </button>
                              ))}
                            </div>
                          </div>
                        )}
                        <button type="button" onClick={() => void resolveAction(action)} disabled={Boolean(submitting)}>
                          {action.requiresSubmittedGrade ? 'Подтвердить исход' : 'Выбрать действие'}
                        </button>
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
        </article>
      )}

      {!hasVisibleState && <p className="muted">Быстрая сцена появится здесь, когда книга предложит короткий выбор или кинематик-эпизод.</p>}
      {notice && <p className="composer-notice">{notice}</p>}
      {result && !isSuccess(result) && <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Быстрая сцена не смогла обновиться.')}</p>}
    </section>
  );
}
