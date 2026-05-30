import { Composer } from '../components/Composer';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { SceneHero } from '../components/SceneHero';
import { ShellPanel } from '../components/ShellPanel';
import type { BrowserGameScreenDto } from '../api/contracts';
import { isSuccess, useShell } from '../context/ShellContext';
import {
  formatDialogueCategory,
  formatTurnLifecycleActionDescription,
  formatTurnStateMessage,
  formatTurnStateTitle
} from '../utils/formatters';
import { useSceneImage } from '../hooks/useSceneImage';
import { toPlayerFacingText } from '../utils/playerCopy';

export default function GameRoute() {
  const { advancedEnabled, readyState } = useShell();
  const game = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  const sceneImage = useSceneImage(game?.narrative.imagePrompt, game?.media.gallery ?? []);

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.game) || !game) {
    return <EmptyOrFailure result={readyState.game} advancedEnabled={advancedEnabled} errorTitle="Игровой экран требует внимания" empty={{
      title: 'Глава ещё не открыта',
      message: 'Нарратив и ход ГМа появятся после выбора или загрузки игровой сессии.',
      action: 'Вернитесь на главную страницу и откройте книгу, чтобы продолжить историю.'
    }} />;
  }

  return (
    <ShellPanel title="Игра" eyebrow="нарратив и ход">
      <SceneHero
        imageUrl={sceneImage.url}
        eyebrow={`Ход ${game.world.turnNumber}`}
        title={game.theme.label}
        subtitle={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || 'время уточняется'}`}
        loading={sceneImage.loading}
      />
      <article className="narrative-card is-featured">
        <h2>{game.theme.icon} Последний нарратив</h2>
        <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
      </article>

      {game.narrative.combatLog && (
        <article className="summary-card combat-log-card" aria-label="Журнал боя">
          <h3>⚔️ Журнал боя</h3>
          <div className="combat-log-content">
            {game.narrative.combatLog.split('\n').map((line, i) => (
              <p key={i}>{line}</p>
            ))}
          </div>
        </article>
      )}

      {game.player.activeConditions && game.player.activeConditions.length > 0 && (
        <section className="summary-card conditions-card" aria-label="Активные состояния">
          <h3>🩹 Активные состояния</h3>
          <ul className="conditions-list">
            {game.player.activeConditions.map((condition, i) => (
              <li key={i} className="condition-item">{condition}</li>
            ))}
          </ul>
        </section>
      )}

      <section className="summary-card" aria-label="Варианты диалога">
        <h3>Варианты для игрока</h3>
        {game.narrative.dialogueOptions.length > 0 ? (
          <div className="dialogue-options">
            {game.narrative.dialogueOptions.map((option) => (
              <div
                key={option.id}
                className={`dialogue-option dialogue-option--${mapDialogueCategory(option.category)}`}
              >
                <span className="dialogue-option__text">{option.text}</span>
                <span className="dialogue-option__category">{formatDialogueCategory(option.category)}</span>
              </div>
            ))}
          </div>
        ) : (
          <p className="muted">Варианты появятся здесь после ответа ГМа.</p>
        )}
      </section>

      <Composer actionComposer={game.actionComposer} />

      <TurnStateCard turnState={game.turnState} advancedEnabled={advancedEnabled} />
    </ShellPanel>
  );
}

function mapDialogueCategory(category: string): string {
  const lower = category.toLowerCase();
  if (lower.includes('исследов') || lower.includes('знани') || lower.includes('explor') || lower.includes('knowl')) return 'explore';
  if (lower.includes('действ') || lower.includes('атак') || lower.includes('action') || lower.includes('attack')) return 'action';
  if (lower.includes('социал') || lower.includes('диплом') || lower.includes('social') || lower.includes('diplo')) return 'social';
  return 'neutral';
}

function TurnStateCard({ turnState, advancedEnabled }: { turnState: BrowserGameScreenDto['turnState']; advancedEnabled: boolean }) {
  const isWaitingForGm = turnState.phase === 'gm-turn' || turnState.phase === 'waiting-for-gm' || turnState.state === 'gm-turn';
  const needsRepair = turnState.severity === 'error' || turnState.severity === 'repair' || turnState.validationState === 'invalid';
  const isNormal = !isWaitingForGm && !needsRepair;

  const playerActions = turnState.recommendedActions.filter(a => a.surface === 'player-default');

  if (isNormal && playerActions.length === 0) {
    return null;
  }

  return (
    <section className={`turn-state-card turn-state-card--${needsRepair ? 'repair' : isWaitingForGm ? 'waiting' : 'normal'}`} aria-label="Состояние хода">
      <div className="turn-state-card__header">
        <span className={`status-pill turn-phase turn-phase--${turnState.severity}`}>
          {formatTurnStateTitle(turnState)}
        </span>
      </div>

      <p>{formatTurnStateMessage(turnState)}</p>

      {needsRepair && (
        <p className="turn-state-card__guidance">
          {toPlayerFacingText(turnState.playerGuidance, 'Игра требует восстановления состояния. Используйте рекомендуемые действия ниже.')}
        </p>
      )}

      {isWaitingForGm && !needsRepair && (
        <p className="turn-state-card__guidance">Ожидается ответ ГМа. Ввод игрока откроется после записи нового хода.</p>
      )}

      {playerActions.length > 0 && (
        <ul className="choice-list">
          {playerActions.map((action) => (
            <li key={action.id}>
              <strong>{toPlayerFacingText(action.label, 'Действие')}</strong>
              <span className="muted">{formatTurnLifecycleActionDescription(action)}</span>
            </li>
          ))}
        </ul>
      )}

      {advancedEnabled && turnState.knownPhases.length > 0 && (
        <details className="turn-state-card__phases">
          <summary>Фазы хода (расширенный режим)</summary>
          <ul>
            {turnState.knownPhases.map(phase => (
              <li key={phase.id}><strong>{phase.label}</strong>: {phase.description}</li>
            ))}
          </ul>
        </details>
      )}
    </section>
  );
}
