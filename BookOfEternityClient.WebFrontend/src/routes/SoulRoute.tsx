import { DetailSurfaceCard } from '../components/DetailSurface';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { ShellPanel } from '../components/ShellPanel';
import { StatusBar } from '../components/StatusBar';
import { isSuccess, useShell } from '../context/ShellContext';
import { formatRealmName, formatSidebarStatusMetric } from '../utils/formatters';

export default function SoulRoute() {
  const { advancedEnabled, readyState } = useShell();

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.game)) {
    return <EmptyOrFailure result={readyState.game} advancedEnabled={advancedEnabled} errorTitle="Данные души требуют внимания" empty={{
      title: 'Душа ещё не проявилась',
      message: 'Данные героя, души и слоя мира появятся после открытия главы.',
      action: 'Начните или загрузите игру, затем вернитесь к разделу души.'
    }} />;
  }

  const { soul, player } = readyState.game.data;

  return (
    <ShellPanel title="Душа" eyebrow="персонаж и состояние">
      <div className="detail-surface-grid">
        <DetailSurfaceCard
          detailSurfaceId="soul-identity"
          eyebrow="душа и царство"
          title="Душа"
          icon="🕯️"
          summary={`${soul.name || 'Безымянная душа'} · ${formatRealmName(soul.realm)}`}
          status={`Перья ${soul.inkFeathers}`}
          detailsTitle="Детали души"
          detailsIntro={<p>Эта панель показывает только текущую игровую сводку души из локальной книги.</p>}
          sections={[
            {
              title: 'Проявление',
              eyebrow: 'имя и слой',
              icon: '✦',
              content: (
                <dl className="kv-list">
                  <div><dt>Имя</dt><dd>{soul.name || 'без имени'}</dd></div>
                  <div><dt>Царство</dt><dd>{formatRealmName(soul.realm)}</dd></div>
                  <div><dt>Инкарнация</dt><dd>{soul.incarnation}</dd></div>
                </dl>
              )
            },
            {
              title: 'Посмертный прогресс',
              eyebrow: 'ресурсы души',
              icon: '✨',
              content: (
                <dl className="kv-list">
                  <div><dt>Чернильные перья</dt><dd>{soul.inkFeathers}</dd></div>
                  <div><dt>Просветление</dt><dd>{soul.enlightenmentTier || 'нет данных'}</dd></div>
                  <div><dt>Хранитель</dt><dd>{soul.activeGuardianName || 'не назначен'}</dd></div>
                </dl>
              )
            }
          ]}
        />
        <DetailSurfaceCard
          detailSurfaceId="player-condition"
          eyebrow="герой"
          title="Герой"
          icon="⚔️"
          summary={`${player.name || 'Герой'} · ${player.currentCondition}`}
          status={player.activeConditions && player.activeConditions.length > 0
            ? `${formatSidebarStatusMetric(player.healthPercentage)} здоровья · ${player.activeConditions.length} сост.`
            : `${formatSidebarStatusMetric(player.healthPercentage)} здоровья`}
          detailsTitle="Детали героя"
          detailsIntro={<p>Карточка героя раскрывает состояние персонажа без служебных команд и внутренних файлов.</p>}
          sections={[
            {
              title: 'Личность',
              eyebrow: 'персонаж',
              icon: '☉',
              content: (
                <dl className="kv-list">
                  <div><dt>Имя</dt><dd>{player.name || 'Герой'}</dd></div>
                  <div><dt>Раса</dt><dd>{player.race || 'не указана'}</dd></div>
                  <div><dt>Класс</dt><dd>{player.class || 'не указан'}</dd></div>
                </dl>
              )
            },
            {
              title: 'Состояние',
              eyebrow: 'виталы',
              icon: '♡',
              content: (
                <>
                  <p>{player.currentCondition || 'Состояние уточняется.'}</p>
                  <StatusBar label="Здоровье" value={player.healthPercentage} />
                  <StatusBar label="Энергия" value={player.energyPercentage} />
                  <StatusBar label="Стойкость" value={player.poisePercentage} />
                  {player.activeConditions && player.activeConditions.length > 0 && (
                    <div className="active-conditions-section">
                      <h5>Активные состояния</h5>
                      <ul className="conditions-list">
                        {player.activeConditions.map((cond, i) => (
                          <li key={i} className="condition-item">{cond}</li>
                        ))}
                      </ul>
                    </div>
                  )}
                </>
              )
            }
          ]}
        />
      </div>
    </ShellPanel>
  );
}
