import type { BrowserGameScreenDto, BrowserPlayerCommandActionDto } from '../api/contracts';
import {
  chaosSeaActionMatchers,
  filterActionSections,
  filterActionsForPanel,
  rebornSectionMatchers,
  shiningAbodeActionMatchers
} from '../utils/actionFilters';
import {
  formatActionPreview,
  formatRealmName,
  formatRebornLockStatus,
  formatShiningGateStatus
} from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';
import { DetailSurfaceCard } from './DetailSurface';

export function RebornSystemsPanel({ game }: { game: BrowserGameScreenDto }) {
  const rebornSections = filterActionSections(game.actionMenu, rebornSectionMatchers);
  const afterlifeActions = filterActionsForPanel(rebornSections, rebornSectionMatchers);
  const shiningActions = filterActionsForPanel(rebornSections, shiningAbodeActionMatchers);
  const chaosActions = filterActionsForPanel(rebornSections, chaosSeaActionMatchers);
  const isAfterlifeActive = game.flags.isInAfterlifeRealm;
  const isShiningAvailable = game.flags.isInShiningAbode || game.flags.isInAnyShiningAbodeState || game.flags.canReenterShiningAbode;
  const isChaosSeaActive = game.flags.isInChaosSea;

  return (
    <section className="reborn-systems-panel" aria-labelledby="reborn-systems-title">
      <div className="reborn-systems-panel__header">
        <p className="panel-eyebrow">посмертные системы</p>
        <h2 id="reborn-systems-title">Посмертие Reborn</h2>
        <p className="muted">
          Посмертие, Сияющая Обитель и Море Хаоса отделены от смертного мира, но используют тот же
          язык карточек и только безопасные игровые данные текущей книги.
        </p>
      </div>
      <div className="detail-surface-grid">
        <DetailSurfaceCard
          detailSurfaceId="reborn-afterlife-overview"
          eyebrow="душа после смерти"
          title="Посмертие Reborn"
          icon="🕯️"
          summary={isAfterlifeActive ? `${formatRealmName(game.soul.realm)} · ${game.soul.name || 'душа без имени'}` : 'Смертный слой активен'}
          status={formatRebornLockStatus(game)}
          detailsTitle="Детали посмертия"
          detailsIntro={<p>Эта панель показывает, открыт ли посмертный слой, и какие ресурсы души уже можно читать игроку.</p>}
          sections={[
            {
              title: 'Состояние слоя',
              eyebrow: 'доступность',
              icon: '✦',
              content: (
                <dl className="kv-list">
                  <div><dt>Текущее царство</dt><dd>{formatRealmName(game.soul.realm)}</dd></div>
                  <div><dt>Посмертие</dt><dd>{isAfterlifeActive ? 'открыто' : 'ещё закрыто'}</dd></div>
                  <div><dt>Инкарнация</dt><dd>{game.soul.incarnation}</dd></div>
                </dl>
              )
            },
            {
              title: 'Ресурсы души',
              eyebrow: 'прогресс',
              icon: '✨',
              content: (
                <dl className="kv-list">
                  <div><dt>Чернильные перья</dt><dd>{game.soul.inkFeathers}</dd></div>
                  <div><dt>Просветление</dt><dd>{game.soul.enlightenmentTier || 'нет данных'}</dd></div>
                  <div><dt>Хранитель</dt><dd>{game.soul.activeGuardianName || 'не назначен'}</dd></div>
                </dl>
              )
            },
            {
              title: 'Доступные действия',
              eyebrow: 'каталог игрока',
              icon: '☉',
              content: <ActionPreviewList actions={afterlifeActions} emptyMessage="Посмертные действия появятся здесь, когда текущая глава отдаст их как безопасные для игрока." />
            }
          ]}
        />
        <DetailSurfaceCard
          detailSurfaceId="reborn-shining-abode"
          eyebrow="свет и обитель"
          title="Сияющая Обитель"
          icon="✦"
          summary={isShiningAvailable ? 'Светлая область доступна для этой души' : 'Обитель пока закрыта'}
          status={formatShiningGateStatus(game.afterlife)}
          detailsTitle="Детали Сияющей Обители"
          detailsIntro={<p>Сводка Обители остаётся игрокоориентированной: сияние, искры, залы и действия без внутренних файлов.</p>}
          sections={[
            {
              title: 'Сияние',
              eyebrow: 'ресурсы обители',
              icon: '✧',
              content: (
                <dl className="kv-list">
                  <div><dt>Опыт сияния</dt><dd>{game.afterlife.shiningRadianceExperience}</dd></div>
                  <div><dt>Ранг сияния</dt><dd>{game.afterlife.shiningRadianceTier}</dd></div>
                  <div><dt>Искры света</dt><dd>{game.afterlife.shiningLightSparks}</dd></div>
                </dl>
              )
            },
            {
              title: 'Обитель',
              eyebrow: 'структура',
              icon: '🏛️',
              content: (
                <dl className="kv-list">
                  <div><dt>Залы</dt><dd>{game.afterlife.shiningHallCount}</dd></div>
                  <div><dt>Фракции</dt><dd>{game.afterlife.shiningFactionCount}</dd></div>
                  <div><dt>Врата</dt><dd>{formatShiningGateStatus(game.afterlife)}</dd></div>
                </dl>
              )
            },
            {
              title: 'Действия Обители',
              eyebrow: 'безопасные формы',
              icon: '☼',
              content: <ActionPreviewList actions={shiningActions} emptyMessage="Действия Сияющей Обители появятся после открытия соответствующего слоя или формы." />
            }
          ]}
        />
        <DetailSurfaceCard
          detailSurfaceId="reborn-chaos-sea"
          eyebrow="хаос и навигация"
          title="Море Хаоса"
          icon="🌊"
          summary={isChaosSeaActive ? 'Душа находится в Море Хаоса' : 'Навигация Моря Хаоса пока закрыта'}
          status={isChaosSeaActive ? 'Море Хаоса активно' : 'Ожидается подходящее царство'}
          detailsTitle="Детали Моря Хаоса"
          detailsIntro={<p>Панель Моря Хаоса показывает статус навигации и безопасные для игрока действия, когда каталог их отдаёт.</p>}
          sections={[
            {
              title: 'Навигация',
              eyebrow: 'статус',
              icon: '⌁',
              content: (
                <dl className="kv-list">
                  <div><dt>Царство</dt><dd>{formatRealmName(game.soul.realm)}</dd></div>
                  <div><dt>Море Хаоса</dt><dd>{isChaosSeaActive ? 'открыто' : 'закрыто'}</dd></div>
                  <div><dt>Хранитель</dt><dd>{game.soul.activeGuardianName || 'ожидает выбора'}</dd></div>
                </dl>
              )
            },
            {
              title: 'Ориентиры',
              eyebrow: 'для игрока',
              icon: '🜁',
              content: (
                <p>{isAfterlifeActive ? 'Посмертный слой активен; действия моря появятся ниже, если они подходят текущему царству.' : 'Посмертные панели откроются, когда душа перейдёт в посмертие.'}</p>
              )
            },
            {
              title: 'Действия Моря',
              eyebrow: 'каталог игрока',
              icon: '☽',
              content: <ActionPreviewList actions={chaosActions} emptyMessage="Действия Моря Хаоса появятся здесь, когда они станут доступны в текущей главе." />
            }
          ]}
        />
      </div>
    </section>
  );
}

function ActionPreviewList({ actions, emptyMessage }: { actions: BrowserPlayerCommandActionDto[]; emptyMessage: string }) {
  if (actions.length === 0) {
    return <p className="muted">{emptyMessage}</p>;
  }

  return (
    <div className="reborn-systems-panel__actions">
      <ul>
        {actions.map((action) => (
          <li key={action.id}>
            <strong>{toPlayerFacingText(action.label, 'Игровое действие')}</strong>
            <span> — {formatActionPreview(action)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
