import { isSuccess, useShell } from '../context/ShellContext';
import { formatWorldTimeForPlayer } from '../utils/formatters';

export function StatusView() {
  const { readyState } = useShell();
  const game = readyState && isSuccess(readyState.game) ? readyState.game.data : null;

  if (!game) {
    return <div className="status-view"><p className="block-text--muted">Данные недоступны.</p></div>;
  }

  const { player, soul, world, afterlife } = game;

  return (
    <div className="status-view">
      <section className="status-card">
        <h3>🎭 Персонаж</h3>
        <dl className="block-kv">
          <div className="block-kv__row"><dt>Имя</dt><dd>{player.name}</dd></div>
          <div className="block-kv__row"><dt>Класс</dt><dd>{player.class}</dd></div>
          <div className="block-kv__row"><dt>Раса</dt><dd>{player.race}</dd></div>
          <div className="block-kv__row"><dt>Состояние</dt><dd>{player.currentCondition}</dd></div>
        </dl>
        <div className="status-bars">
          <StatusMeter label="❤️ Здоровье" value={player.healthPercentage} color="var(--color-error)" />
          <StatusMeter label="⚡ Энергия" value={player.energyPercentage} color="var(--color-accent)" />
          <StatusMeter label="🛡️ Самообладание" value={player.poisePercentage} color="var(--color-success)" />
        </div>
        {player.activeConditions.length > 0 && (
          <div className="status-conditions">
            <h4>Активные состояния</h4>
            <ul>{player.activeConditions.map((c, i) => <li key={i}>{c}</li>)}</ul>
          </div>
        )}
      </section>

      <section className="status-card">
        <h3>🕯️ Душа</h3>
        <dl className="block-kv">
          <div className="block-kv__row"><dt>Имя души</dt><dd>{soul.name}</dd></div>
          <div className="block-kv__row"><dt>Царство</dt><dd>{soul.realm}</dd></div>
          <div className="block-kv__row"><dt>Инкарнация</dt><dd>{soul.incarnation}</dd></div>
          <div className="block-kv__row"><dt>Чернильные перья</dt><dd>{soul.inkFeathers}</dd></div>
          <div className="block-kv__row"><dt>Просветление</dt><dd>{soul.enlightenmentTier}</dd></div>
          <div className="block-kv__row"><dt>Хранитель</dt><dd>{soul.activeGuardianName}</dd></div>
        </dl>
      </section>

      <section className="status-card">
        <h3>🗺️ Мир</h3>
        <dl className="block-kv">
          <div className="block-kv__row"><dt>Локация</dt><dd>{world.location || '—'}</dd></div>
          <div className="block-kv__row"><dt>Время</dt><dd>{formatWorldTimeForPlayer(world.worldTime, '—')}</dd></div>
          <div className="block-kv__row"><dt>Ход</dt><dd>{world.turnNumber}</dd></div>
        </dl>
      </section>

      {(afterlife.shiningRadianceExperience > 0 || afterlife.shiningHallCount > 0) && (
        <section className="status-card">
          <h3>✨ Посмертие</h3>
          <dl className="block-kv">
            <div className="block-kv__row"><dt>Сияние</dt><dd>{afterlife.shiningRadianceExperience} (уровень {afterlife.shiningRadianceTier})</dd></div>
            <div className="block-kv__row"><dt>Искры света</dt><dd>{afterlife.shiningLightSparks}</dd></div>
            <div className="block-kv__row"><dt>Залы</dt><dd>{afterlife.shiningHallCount}</dd></div>
            <div className="block-kv__row"><dt>Фракции</dt><dd>{afterlife.shiningFactionCount}</dd></div>
          </dl>
        </section>
      )}
    </div>
  );
}

function StatusMeter({ label, value, color }: { label: string; value: string; color: string }) {
  const numValue = parseInt(value) || 0;
  return (
    <div className="status-meter">
      <div className="status-meter__label">
        <span>{label}</span>
        <span className="status-meter__value">{value}</span>
      </div>
      <div
        className="status-meter__track"
        role="meter"
        aria-label={`${label} ${value}`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={numValue}
        aria-valuetext={value}
        title={`${label} ${value}`}
      >
        <div className="status-meter__fill" style={{ width: `${numValue}%`, background: color }} />
      </div>
    </div>
  );
}
