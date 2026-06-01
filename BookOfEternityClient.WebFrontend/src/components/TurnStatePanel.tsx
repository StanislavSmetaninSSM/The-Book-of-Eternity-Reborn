import type { BrowserGameScreenDto } from '../api/contracts';
import {
  formatTurnLifecycleActionDescription,
  formatTurnStateLabel,
  formatTurnStateMessage,
  formatTurnStateTitle
} from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';

type TurnState = BrowserGameScreenDto['turnState'];

export function TurnStatePanel({ turnState }: { turnState: TurnState }) {
  const activePhaseId = turnState.phase || turnState.state;
  const phaseLabel = toPlayerFacingText(turnState.phaseLabel, formatTurnStateLabel(activePhaseId));
  const guidance = toPlayerFacingText(turnState.playerGuidance, formatTurnStateMessage(turnState));
  const recommendedActions = turnState.recommendedActions
    .filter((action) => action.surface !== 'advanced-only')
    .slice(0, 3);
  const knownPhases = turnState.knownPhases
    .filter((phase) => phase.surface !== 'advanced-only' || phase.id === activePhaseId)
    .slice(0, 8);

  return (
    <section className={`turn-state-card ${resolveTurnStateClass(turnState)}`} aria-label="Жизненный цикл хода">
      <header className="turn-state-card__header">
        <p className="panel-eyebrow">Жизненный цикл хода</p>
        <h3>{formatTurnStateTitle(turnState)}</h3>
        <span className="status-pill">{phaseLabel}</span>
      </header>
      <p>{formatTurnStateMessage(turnState)}</p>
      <p className="turn-state-card__guidance">{guidance}</p>

      {recommendedActions.length > 0 && (
        <div className="turn-lifecycle-actions" aria-label="Рекомендованные действия">
          {recommendedActions.map((action) => (
            <article key={action.id} className={`action-card${action.enabled ? '' : ' is-disabled'}`}>
              <header>
                <strong>{toPlayerFacingText(action.label, 'Действие')}</strong>
                <span className="availability-pill">{action.enabled ? 'доступно' : 'недоступно'}</span>
              </header>
              <p>{formatTurnLifecycleActionDescription(action)}</p>
            </article>
          ))}
        </div>
      )}

      {knownPhases.length > 0 && (
        <details className="turn-state-card__phases">
          <summary>Этапы хода</summary>
          <div className="phase-chip-grid">
            {knownPhases.map((phase) => (
              <span
                key={phase.id}
                className={`status-pill${phase.id === activePhaseId ? '' : ' is-muted'}`}
                title={toPlayerFacingText(phase.description, phase.label)}
              >
                {toPlayerFacingText(phase.label, formatTurnStateLabel(phase.id))}
              </span>
            ))}
          </div>
        </details>
      )}
    </section>
  );
}

function resolveTurnStateClass(turnState: TurnState): string {
  if (turnState.severity === 'error' || turnState.validationState === 'repair-required') {
    return 'turn-state-card--repair';
  }

  if (!turnState.canStartBrowserWrite) {
    return 'turn-state-card--waiting';
  }

  return 'turn-state-card--normal';
}
