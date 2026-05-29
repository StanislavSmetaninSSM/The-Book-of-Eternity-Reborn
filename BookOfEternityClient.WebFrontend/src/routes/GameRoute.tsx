import { TurnLifecycleActions } from '../components/ActionMenu';
import { Composer } from '../components/Composer';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { ShellPanel } from '../components/ShellPanel';
import { isSuccess, useShell } from '../context/ShellContext';
import {
  formatDialogueCategory,
  formatQteStateLabel,
  formatTurnStateMessage,
  formatTurnStateTitle
} from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';

export default function GameRoute() {
  const { advancedEnabled, readyState } = useShell();

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

      <section className="summary-card" aria-label="Варианты диалога">
        <h3>Варианты для игрока</h3>
        {game.narrative.dialogueOptions.length > 0 ? (
          <ul className="choice-list">
            {game.narrative.dialogueOptions.map((option) => (
              <li key={option.id}>
                <strong>{option.text}</strong>
                <span>{formatDialogueCategory(option.category)}</span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="muted">Варианты появятся здесь после ответа ГМа.</p>
        )}
      </section>

      <Composer actionComposer={game.actionComposer} />

      <section className="turn-status-compact" aria-label="Состояние хода">
        <span className={`status-pill turn-phase turn-phase--${game.turnState.severity}`}>{formatTurnStateTitle(game.turnState)}</span>
        <span>{formatTurnStateMessage(game.turnState)}</span>
        <span className="muted">{toPlayerFacingText(game.turnState.playerGuidance, 'Следуйте безопасному состоянию хода.')}</span>
        <span className="muted">Быстрая сцена: {formatQteStateLabel(game.qte)}</span>
      </section>

      <TurnLifecycleActions turnState={game.turnState} />
    </ShellPanel>
  );
}
