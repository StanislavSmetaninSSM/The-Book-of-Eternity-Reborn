import { type CSSProperties, useState } from 'react';
import { motion } from 'framer-motion';
import { isSuccess, useShell } from '../context/ShellContext';
import { SceneHero } from './SceneHero';
import { CommandResultView } from './CommandResultView';
import { useSceneImage } from '../hooks/useSceneImage';
import { toPlayerFacingText } from '../utils/playerCopy';
import { formatWorldTimeForPlayer } from '../utils/formatters';
import { browserUiAssets } from '../browserUiAssets';
import { RuneFrame } from './decorative';
import { staggerContainer, fadeUp } from '../lib/motion';

type ScenePostId = 'scene-narrative';

const defaultPostScale = 1;
const postScaleStep = 0.1;
const minPostScale = 0.7;
const maxPostScale = 2.4;

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
  const { clientSettings } = useShell();
  const reducedMotion = Boolean(clientSettings?.accessibility.reducedMotion);
  const sceneImage = useSceneImage(game.narrative.imagePrompt, game.media.gallery ?? []);
  const [postTextScales, setPostTextScales] = useState<Record<ScenePostId, number>>({
    'scene-narrative': defaultPostScale
  });
  const scenePostScale = postTextScales['scene-narrative'];

  function updatePostScale(postId: ScenePostId, nextScale: number) {
    setPostTextScales((current) => ({
      ...current,
      [postId]: clampPostScale(nextScale)
    }));
  }

  return (
    <motion.div
      className="scene-view"
      variants={staggerContainer}
      initial="hidden"
      animate="visible"
    >
      <SceneHero
        imageUrl={sceneImage.url}
        fallbackImageUrl={browserUiAssets.sceneHeroFallback.url}
        eyebrow={`Ход ${game.world.turnNumber}`}
        title={game.theme.label}
        subtitle={`${game.world.location || 'Локация уточняется'} · ${formatWorldTimeForPlayer(game.world.worldTime, '')}`}
        loading={sceneImage.loading}
        reducedMotion={reducedMotion}
      />

      <motion.article
        className="scene-narrative scene-post"
        variants={fadeUp}
        style={{ '--scene-post-scale': scenePostScale } as CSSProperties}
      >
        <RuneFrame variant="subtle">
          <p>{game.narrative.text || 'Нарратив ещё не получен от ГМа.'}</p>
        </RuneFrame>
        <ScenePostTextScaleControls
          scale={scenePostScale}
          onDecrease={() => updatePostScale('scene-narrative', scenePostScale - postScaleStep)}
          onReset={() => updatePostScale('scene-narrative', defaultPostScale)}
          onIncrease={() => updatePostScale('scene-narrative', scenePostScale + postScaleStep)}
        />
      </motion.article>

      {game.narrative.dialogueOptions.length > 0 && (
        <motion.section className="scene-dialogues" variants={fadeUp}>
          <h3>Варианты диалога</h3>
          <div className="scene-dialogues__list">
            {game.narrative.dialogueOptions.map((opt) => (
              <button
                key={opt.id}
                type="button"
                className="scene-dialogue-chip"
                onClick={() => void onCommand(`/player_action ${opt.inputValue || opt.text}`)}
              >
                {toPlayerFacingText(opt.text, 'вариант')}
              </button>
            ))}
          </div>
        </motion.section>
      )}

      {game.actionComposer.canSubmit && game.actionMenu.sections.length > 0 && (
        <motion.section className="scene-quick-actions" variants={fadeUp}>
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
        </motion.section>
      )}
    </motion.div>
  );
}

function ScenePostTextScaleControls({ scale, onDecrease, onReset, onIncrease }: {
  scale: number;
  onDecrease: () => void;
  onReset: () => void;
  onIncrease: () => void;
}) {
  const percentage = Math.round(scale * 100);

  return (
    <div className="scene-post-controls" aria-label="Масштаб текста сцены">
      <button
        type="button"
        onClick={onDecrease}
        disabled={scale <= minPostScale}
        aria-label="Уменьшить текст сцены"
        title="Уменьшить текст сцены"
      >
        A-
      </button>
      <button
        type="button"
        onClick={onReset}
        disabled={scale === defaultPostScale}
        aria-label="Обычный размер текста сцены"
        title="Обычный размер текста сцены"
      >
        {percentage}%
      </button>
      <button
        type="button"
        onClick={onIncrease}
        disabled={scale >= maxPostScale}
        aria-label="Увеличить текст сцены"
        title="Увеличить текст сцены"
      >
        A+
      </button>
    </div>
  );
}

function clampPostScale(scale: number): number {
  return Math.min(maxPostScale, Math.max(minPostScale, Number(scale.toFixed(2))));
}
