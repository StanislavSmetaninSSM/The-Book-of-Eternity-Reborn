import { isSuccess, useShell } from '../context/ShellContext';
import { browserUiAssets } from '../browserUiAssets';
import { formatRealmName, formatWorldTimeForPlayer } from '../utils/formatters';

export function StatusView() {
  const { readyState } = useShell();
  const game = readyState && isSuccess(readyState.game) ? readyState.game.data : null;

  if (!game) {
    return <div className="status-view"><p className="block-text--muted">Данные недоступны.</p></div>;
  }

  const { player, soul, world, afterlife } = game;
  const hasMissingPlayerIdentity = [
    player.name,
    player.class,
    player.race,
    player.currentCondition
  ].some(isMissingStatusValue);
  const hasMissingSoulIdentity = [
    soul.name,
    soul.realm,
    soul.enlightenmentTier,
    soul.activeGuardianName
  ].some(isMissingStatusValue);
  const worldTime = formatWorldTimeForPlayer(world.worldTime, '—');
  const hasMissingWorldDetail = isMissingStatusValue(world.location) || isMissingStatusValue(worldTime);
  const hasAfterlifeProgress =
    afterlife.shiningRadianceExperience > 0 ||
    afterlife.shiningRadianceTier > 0 ||
    afterlife.shiningLightSparks > 0 ||
    afterlife.shiningHallCount > 0 ||
    afterlife.shiningFactionCount > 0;

  return (
    <div className="status-view">
      <div className="status-view__ambient-art" aria-hidden="true">
        <img
          src={browserUiAssets.statusSoulVignette.url}
          alt=""
          loading="lazy"
          onError={(event) => { event.currentTarget.hidden = true; }}
        />
      </div>
      <section className="status-card">
        <h3>🎭 Персонаж</h3>
        {hasMissingPlayerIdentity && (
          <StatusEmptyState
            title="Летопись героя ещё ждёт первых строк."
            body="Имя, класс, раса и состояние появятся после записи главы."
          />
        )}
        <dl className="block-kv">
          <StatusRow label="Имя" value={player.name} />
          <StatusRow label="Класс" value={player.class} />
          <StatusRow label="Раса" value={player.race} />
          <StatusRow label="Состояние" value={player.currentCondition} />
        </dl>
        <div className="status-bars">
          <StatusMeter label="❤️ Здоровье" value={player.healthPercentage} />
          <StatusMeter label="⚡ Энергия" value={player.energyPercentage} />
          <StatusMeter label="🛡️ Самообладание" value={player.poisePercentage} />
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
        {hasMissingSoulIdentity && (
          <StatusEmptyState
            title="Душа ещё не обрела полную запись."
            body="Имя души, царство и хранитель проявятся, когда книга закрепит их в главе."
          />
        )}
        <dl className="block-kv">
          <StatusRow label="Имя души" value={soul.name} />
          <StatusRow label="Царство" value={formatRealmName(soul.realm)} />
          <StatusRow label="Инкарнация" value={soul.incarnation} />
          <StatusRow label="Чернильные перья" value={soul.inkFeathers} />
          <StatusRow label="Просветление" value={soul.enlightenmentTier} />
          <StatusRow label="Хранитель" value={soul.activeGuardianName} />
        </dl>
      </section>

      <section className="status-card">
        <h3>🗺️ Мир</h3>
        {hasMissingWorldDetail && (
          <StatusEmptyState
            title="Путь пока скрыт туманом."
            body="Место и время станут яснее после следующей записи."
          />
        )}
        <dl className="block-kv">
          <StatusRow label="Локация" value={world.location} fallback="место уточняется" />
          <StatusRow label="Время" value={worldTime} fallback="время уточняется" />
          <StatusRow label="Ход" value={world.turnNumber} />
        </dl>
      </section>

      <section className="status-card">
        <h3>✨ Посмертие</h3>
        {!hasAfterlifeProgress && (
          <StatusEmptyState
            title="Следы посмертия пока не открыты."
            body="Сияние, искры и залы останутся на нуле, пока душа не вступит в посмертный путь."
          />
        )}
        <dl className="block-kv">
          <div className="block-kv__row"><dt>Сияние</dt><dd>{afterlife.shiningRadianceExperience} (уровень {afterlife.shiningRadianceTier})</dd></div>
          <StatusRow label="Искры света" value={afterlife.shiningLightSparks} />
          <StatusRow label="Залы" value={afterlife.shiningHallCount} />
          <StatusRow label="Фракции" value={afterlife.shiningFactionCount} />
        </dl>
      </section>
    </div>
  );
}

type StatusValue = string | number | null | undefined;

const missingStatusValues = new Set([
  'не указан',
  'не указана',
  'не указано',
  'не назначен',
  'unknown',
  'n/a',
  '—'
]);

function StatusEmptyState({ title, body }: { title: string; body: string }) {
  return (
    <div className="status-empty-state" role="note">
      <strong>{title}</strong>
      <p>{body}</p>
    </div>
  );
}

function StatusRow({
  label,
  value,
  fallback = 'Пока не записано'
}: {
  label: string;
  value: StatusValue;
  fallback?: string;
}) {
  return (
    <div className="block-kv__row">
      <dt>{label}</dt>
      <dd>{formatStatusValue(value, fallback)}</dd>
    </div>
  );
}

function formatStatusValue(value: StatusValue, fallback: string) {
  if (typeof value === 'number') {
    return value;
  }

  const normalized = value?.trim();
  if (!normalized || missingStatusValues.has(normalized.toLowerCase())) {
    return <span className="status-empty-inline">{fallback}</span>;
  }

  return normalized;
}

function isMissingStatusValue(value: StatusValue): boolean {
  if (typeof value === 'number') {
    return false;
  }

  const normalized = value?.trim().toLowerCase();
  return !normalized || missingStatusValues.has(normalized);
}

type StatusMeterSeverity = 'good' | 'warning' | 'danger';

function StatusMeter({ label, value }: { label: string; value: string }) {
  const numValue = parseStatusMeterPercent(value);
  const severity = resolveStatusMeterSeverity(numValue);
  const displayValue = `${formatStatusMeterPercent(numValue)}%`;

  return (
    <div className={`status-meter status-meter--${severity}`}>
      <div className="status-meter__label">
        <span>{label}</span>
        <span className="status-meter__value">{displayValue}</span>
      </div>
      <div
        className="status-meter__track"
        role="meter"
        aria-label={`${label} ${displayValue}`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={numValue}
        aria-valuetext={displayValue}
        title={`${label} ${displayValue}`}
      >
        <div className="status-meter__fill" style={{ width: `${numValue}%` }} />
      </div>
    </div>
  );
}

function parseStatusMeterPercent(value: string): number {
  const match = value.trim().replace(',', '.').match(/^(-?\d+(?:\.\d+)?)\s*%?$/);
  if (!match) {
    return 0;
  }

  const numericValue = Number.parseFloat(match[1]);
  return Number.isFinite(numericValue) ? Math.max(0, Math.min(100, numericValue)) : 0;
}

function resolveStatusMeterSeverity(percent: number): StatusMeterSeverity {
  if (percent > 66) {
    return 'good';
  }

  if (percent > 33) {
    return 'warning';
  }

  return 'danger';
}

function formatStatusMeterPercent(percent: number): string {
  return Number.isInteger(percent) ? String(percent) : percent.toFixed(1).replace(/\.0$/, '');
}
