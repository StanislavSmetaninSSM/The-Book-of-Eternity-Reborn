import { useState } from 'react';

import { ActionMenu } from '../components/ActionMenu';
import { DetailSurfaceCard } from '../components/DetailSurface';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { RebornSystemsPanel } from '../components/RebornSystemsPanel';
import { SceneHero } from '../components/SceneHero';
import { ShellPanel } from '../components/ShellPanel';
import { isSuccess, useShell } from '../context/ShellContext';
import { useSceneImage } from '../hooks/useSceneImage';
import { formatTurnStateTitle, getComposerDisabledReason } from '../utils/formatters';

export default function WorldRoute() {
  const [showAllActions, setShowAllActions] = useState(false);
  const { advancedEnabled, readyState } = useShell();
  const game = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  const locationImage = useSceneImage(game?.narrative.imagePrompt, game?.media.gallery ?? [], 'location', game?.world.location);

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.game) || !game) {
    return <EmptyOrFailure result={readyState.game} advancedEnabled={advancedEnabled} errorTitle="Мир требует внимания" empty={{
      title: 'Мир ждёт первой записи',
      message: 'Карта, журнал и фракции заполнятся из текущей главы после открытия книги.',
      action: 'Откройте или загрузите сессию, чтобы увидеть состояние мира.'
    }} />;
  }
  const themeKey = game.theme.key.toLowerCase();
  const afterlifeRealmActive =
    themeKey.includes('chaos') ||
    themeKey.includes('shining') ||
    themeKey.includes('abode');

  return (
    <ShellPanel title="Мир" eyebrow="карта, журнал и действия">
      <SceneHero
        imageUrl={locationImage.url}
        eyebrow="Мир"
        title={game.world.location || 'Локация уточняется'}
        subtitle={`${game.world.worldTime || 'время уточняется'} · Ход ${game.world.turnNumber}`}
        loading={locationImage.loading}
      />
      <div className="split-grid three">
        <DetailSurfaceCard
          detailSurfaceId="world-location"
          eyebrow="мир и место"
          title="Локация"
          icon="🗺️"
          summary={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || 'время уточняется'}`}
          status={`Ход ${game.world.turnNumber}`}
          detailsTitle="Детали локации"
          detailsIntro={<p>Локация раскрывает текущий слой мира без технических путей и служебных журналов.</p>}
          sections={[
            {
              title: 'Текущая сцена',
              eyebrow: 'место и время',
              icon: '⌖',
              content: (
                <dl className="kv-list">
                  <div><dt>Локация</dt><dd>{game.world.location || 'локация уточняется'}</dd></div>
                  <div><dt>Время</dt><dd>{game.world.worldTime || 'время уточняется'}</dd></div>
                  <div><dt>Царство</dt><dd>{game.theme.label}</dd></div>
                </dl>
              )
            },
            {
              title: 'Ориентир главы',
              eyebrow: 'ход и запись',
              icon: '✍️',
              content: (
                <dl className="kv-list">
                  <div><dt>Номер хода</dt><dd>{game.world.turnNumber}</dd></div>
                  <div><dt>Состояние</dt><dd>{formatTurnStateTitle(game.turnState)}</dd></div>
                  <div><dt>Ввод игрока</dt><dd>{game.actionComposer.canSubmit ? 'доступен' : getComposerDisabledReason(game.actionComposer)}</dd></div>
                </dl>
              )
            }
          ]}
        />
        <div className="summary-card"><h2>Журнал</h2><p>Квесты, архив и история разворачиваются в игровых разделах без знания ручных команд.</p></div>
        <div className="summary-card"><h2>Фракции</h2><p>Панели фракций и стражей используют общие игровые данные и не дублируют правила.</p></div>
      </div>
      {afterlifeRealmActive ? <RebornSystemsPanel game={game} /> : null}
      <div className="action-catalog-toggle">
        <button type="button" onClick={() => setShowAllActions(v => !v)}>
          {showAllActions ? 'Скрыть все действия' : 'Показать все действия'}
        </button>
        {showAllActions && <ActionMenu menu={game.actionMenu} />}
      </div>
    </ShellPanel>
  );
}
