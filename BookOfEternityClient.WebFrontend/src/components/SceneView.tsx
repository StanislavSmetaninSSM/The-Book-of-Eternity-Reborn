import type React from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import { SceneHero } from './SceneHero';
import { CommandResultView } from './CommandResultView';
import { useSceneImage } from '../hooks/useSceneImage';
import { toPlayerFacingText } from '../utils/playerCopy';

export function SceneView() {
  const { readyState, isCommandView, executeCommand } = useShell();

  if (isCommandView) {
    return <CommandResultView />;
  }

  const game = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  if (!game) {
    return (
      <div className="scene-empty">
        <p>Игровая сессия не загружена. Откройте настройки или загрузите сохранение.</p>
      </div>
    );
  }

  return <SceneContent game={game} onCommand={executeCommand} />;
}

function SceneContent({ game, onCommand }: {
  game: NonNullable<ReturnType<typeof useShell>['gameScreen']>;
  onCommand: (cmd: string) => Promise<void>;
}) {
  const sceneImage = useSceneImage(game.narrative.imagePrompt, game.media.gallery ?? []);

  return (
    <div className="scene-view">
      <SceneHero
        imageUrl={sceneImage.url}
        eyebrow={`Ход ${game.world.turnNumber}`}
        title={game.theme.label}
        subtitle={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || ''}`}
        loading={sceneImage.loading}
      />

      <article className="scene-narrative">
        <p>{game.narrative.text || 'Нарратив ещё не получен от ГМа.'}</p>
      </article>

      {game.narrative.combatLog && (
        <section className="scene-combat-log">
          <h3>⚔️ Журнал боя</h3>
          <div>{game.narrative.combatLog.split('\n').map((line, i) => (
            <CombatLogLine key={i} line={line} />
          ))}</div>
        </section>
      )}

      {game.narrative.dialogueOptions.length > 0 && (
        <section className="scene-dialogues">
          <h3>💬 Варианты диалога</h3>
          <div className="scene-dialogues__list">
            {game.narrative.dialogueOptions.map((opt) => (
              <button
                key={opt.id}
                type="button"
                className="scene-dialogue-chip"
                onClick={() => void onCommand(`/player_action ${opt.text}`)}
              >
                {toPlayerFacingText(opt.text, 'вариант')}
              </button>
            ))}
          </div>
        </section>
      )}

      {game.actionComposer.canSubmit && game.actionMenu.sections.length > 0 && (
        <section className="scene-quick-actions">
          <h4>Быстрые действия</h4>
          <div className="scene-quick-actions__list">
            {game.actionMenu.sections
              .filter((s) => s.playerDefault)
              .flatMap((s) => s.actions)
              .filter((a) => a.playerDefault && a.enabled)
              .slice(0, 8)
              .map((action) => (
                <button
                  key={action.id}
                  type="button"
                  className="scene-action-chip"
                  onClick={() => void onCommand(action.advancedCommand)}
                >
                  {action.label}
                </button>
              ))}
          </div>
        </section>
      )}
    </div>
  );
}

/** Renders a single combat-log line with basic markdown (headings, bold, list items). */
function CombatLogLine({ line }: { line: string }) {
  if (!line.trim()) return null;

  // Heading: ### text
  const headingMatch = line.match(/^#{1,4}\s+(.+)$/);
  if (headingMatch) {
    return <h4 className="combat-log__heading">{headingMatch[1]}</h4>;
  }

  // List item: - text
  if (line.startsWith('- ')) {
    return <p className="combat-log__item">• {renderBold(line.slice(2))}</p>;
  }

  return <p className="combat-log__line">{renderBold(line)}</p>;
}

/** Converts **bold** markdown to <strong> elements. */
function renderBold(text: string): React.ReactNode {
  const parts = text.split(/\*\*(.+?)\*\*/g);
  if (parts.length === 1) return text;
  return parts.map((part, i) =>
    i % 2 === 1 ? <strong key={i}>{part}</strong> : part
  );
}