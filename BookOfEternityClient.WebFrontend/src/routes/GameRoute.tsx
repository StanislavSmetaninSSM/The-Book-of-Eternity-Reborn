import type { FormEvent } from 'react';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { ShellPanel } from '../components/ShellPanel';
import { TurnLifecycleActions } from '../components/ActionMenu';
import { isSuccess, useShell } from '../context/ShellContext';
import {
  formatDialogueCategory,
  formatQteStateLabel,
  formatTurnStateLabel,
  formatTurnStateMessage,
  formatTurnStateTitle,
  getComposerDisabledReason,
  getComposerGuidance,
  getComposerPlaceholder
} from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';

export default function GameRoute() {
  const { advancedEnabled, composerNotice, composerText, readyState, setComposerText, submitComposer } = useShell();

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.game)) {
    return <EmptyOrFailure result={readyState.game} advancedEnabled={advancedEnabled} errorTitle="Игровой экран требует внимания" empty={{
      title: 'Глава ещё не открыта',
      message: 'Нарратив и ход ГМа появятся после выбора или загрузки игровой сессии.',
      action: 'Вернитесь на главную страницу и откройте книгу, чтобы продолжить историю.'
    }} />;
  }

  const game = readyState.game.data;

  return (
    <ShellPanel title="Игра" eyebrow="нарратив и ход">
      <article className="narrative-card is-featured">
        <h2>{game.theme.icon} {game.theme.label}</h2>
        <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
      </article>

      <div className="split-grid">
        <ShellPanel title="Состояние хода" eyebrow={formatTurnStateLabel(game.turnState.phase || game.turnState.state)} nested variant="turn">
          <p className={`status-pill turn-phase turn-phase--${game.turnState.severity}`}>{formatTurnStateTitle(game.turnState)}</p>
          <p>{formatTurnStateMessage(game.turnState)}</p>
          <p className="muted">{toPlayerFacingText(game.turnState.playerGuidance, 'Следуйте безопасному состоянию хода.')}</p>
          <TurnLifecycleActions turnState={game.turnState} />
          <p className="muted">Быстрая сцена: {formatQteStateLabel(game.qte)}</p>
        </ShellPanel>
        <ShellPanel title="Варианты" eyebrow="для игрока" nested variant="choices">
          {game.narrative.dialogueOptions.length > 0 ? (
            <ul className="choice-list">
              {game.narrative.dialogueOptions.map((option) => (
                <li key={option.id}><strong>{option.text}</strong><span>{formatDialogueCategory(option.category)}</span></li>
              ))}
            </ul>
          ) : (
            <p className="muted">Варианты появятся здесь после ответа ГМа.</p>
          )}
        </ShellPanel>
      </div>

      <form className="composer" onSubmit={submitComposer as (event: FormEvent<HTMLFormElement>) => void}>
        <label htmlFor="player-action">Основной художественный ввод</label>
        <textarea
          id="player-action"
          name="player-action"
          rows={4}
          value={composerText}
          onChange={(event) => setComposerText(event.currentTarget.value)}
          placeholder={getComposerPlaceholder(game.actionComposer)}
          disabled={!game.actionComposer.canSubmit}
        />
        <p className="muted">{getComposerGuidance(game.actionComposer)}</p>
        {!game.actionComposer.canSubmit && <p className="warning-text">{getComposerDisabledReason(game.actionComposer)}</p>}
        <button type="submit" disabled={!composerText.trim()}>Подготовить действие</button>
        {composerNotice && <p className="composer-notice">{composerNotice}</p>}
      </form>

      <section className="summary-card" aria-label="Жизненный цикл хода">
        <h3>Жизненный цикл хода</h3>
        <p className="muted">{toPlayerFacingText(game.turnState.phaseLabel, 'Текущее состояние хода')}</p>
        <div className="phase-chip-grid">
          {game.turnState.knownPhases.map((phase) => (
            <span key={phase.id} className={phase.id === game.turnState.phase ? 'status-pill' : 'status-pill is-muted'}>
              {toPlayerFacingText(phase.label, 'Этап')}
            </span>
          ))}
        </div>
      </section>
    </ShellPanel>
  );
}
