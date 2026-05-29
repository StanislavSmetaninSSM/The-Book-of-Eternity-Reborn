import type { ReactNode } from 'react';
import type { BrowserApiResult } from '../api/contracts';
import { BrowserShellState, isSuccess, useShell } from '../context/ShellContext';
import {
  formatRealmName,
  formatSidebarLayerStatus,
  formatSidebarSaveSummary,
  formatSidebarSessionSummary,
  formatSidebarStatusMetric,
  formatTurnStateMessage,
  formatTurnStateTitle
} from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';
import { AudioPanel } from './AudioPanel';

export function PlayerStatusSidebar() {
  const { advancedEnabled, gameScreen, menu, readyState, realmTheme, session, setAdvancedEnabled } = useShell();
  const sidebarEmptyGame = getSidebarEmptyGameMessage(readyState);
  const hasGame = Boolean(gameScreen);
  const sidebarMenuFailure = getSidebarFailure(readyState?.menu);
  const sidebarSessionFailure = getSidebarFailure(readyState?.session);
  const sidebarGameFailure = getSidebarFailure(readyState?.game);
  const saveNeedsAttention = Boolean(sidebarMenuFailure || sidebarSessionFailure);
  const turnNeedsAttention = Boolean(sidebarGameFailure || gameScreen?.turnState.severity === 'error' || gameScreen?.turnState.severity === 'repair');

  return (
    <div className="player-status-sidebar">
      <div className="sidebar-heading">
        <p className="panel-eyebrow">игровая сводка</p>
        <h2>Сводка книги</h2>
        <p className="muted">Мягкая сводка текущей главы без служебных журналов и внутренних проверок.</p>
      </div>

      <StatusSummaryCard title="Слой книги" eyebrow="мир и глава" attention={Boolean(sidebarMenuFailure || sidebarGameFailure)}>
        <p className="status-pill">{realmTheme.label}</p>
        <p>{gameScreen ? `${gameScreen.soul.name || 'Душа'} · ход ${gameScreen.world.turnNumber}` : sidebarEmptyGame}</p>
        {sidebarMenuFailure ? <p className="warning-text">{sidebarMenuFailure}</p> : <p className="muted">{formatSidebarLayerStatus(menu)}</p>}
      </StatusSummaryCard>

      <StatusSummaryCard title="Герой и душа" eyebrow="персонаж" soft={!hasGame && !sidebarGameFailure} attention={Boolean(sidebarGameFailure)}>
        {sidebarGameFailure ? (
          <>
            <p className="warning-text">{sidebarGameFailure}</p>
            <p className="muted">Герой и душа появятся снова, когда локальная книга отдаст игровую сводку.</p>
          </>
        ) : gameScreen ? (
          <>
            <p><strong>{gameScreen.player.name || 'Герой'}</strong> · {gameScreen.player.currentCondition}</p>
            <p className="muted">Душа: {gameScreen.soul.name || 'без имени'} · {formatRealmName(gameScreen.soul.realm)}</p>
            <div className="status-summary-grid" aria-label="Состояние героя">
              <span>Здоровье {formatSidebarStatusMetric(gameScreen.player.healthPercentage)}</span>
              <span>Энергия {formatSidebarStatusMetric(gameScreen.player.energyPercentage)}</span>
              <span>Стойкость {formatSidebarStatusMetric(gameScreen.player.poisePercentage)}</span>
            </div>
          </>
        ) : (
          <>
            <p>Душа и герой появятся после открытия или загрузки главы.</p>
            <p className="muted">Это обычное состояние пустой книги, не ошибка клиента.</p>
          </>
        )}
      </StatusSummaryCard>

      <StatusSummaryCard title="Сохранение" eyebrow="локальная партия" soft={!session?.gameSessionExists && !saveNeedsAttention} attention={saveNeedsAttention}>
        {sidebarSessionFailure ? <p className="warning-text">{sidebarSessionFailure}</p> : <p>{formatSidebarSessionSummary(session, menu)}</p>}
        {sidebarMenuFailure ? <p className="warning-text">{sidebarMenuFailure}</p> : <p className="muted">{formatSidebarSaveSummary(menu)}</p>}
      </StatusSummaryCard>

      <StatusSummaryCard title={getTurnSidebarTitle(hasGame, sidebarGameFailure)} eyebrow="ход" attention={turnNeedsAttention}>
        {sidebarGameFailure ? (
          <>
            <p className="warning-text">{sidebarGameFailure}</p>
            <p className="muted">Глава сохранена; подробности ремонта и проверки остаются в расширенном режиме.</p>
          </>
        ) : gameScreen ? (
          <>
            <p className={`status-pill turn-phase turn-phase--${gameScreen.turnState.severity}`}>{formatTurnStateTitle(gameScreen.turnState)}</p>
            <p>{formatTurnStateMessage(gameScreen.turnState)}</p>
            <p className="muted">Подробности ремонта, проверки и команд скрыты до явного включения.</p>
          </>
        ) : (
          <>
            <p>{sidebarEmptyGame}</p>
            <p className="muted">Когда появится ожидающий ход или ответ ГМа, книга покажет это здесь игровым языком.</p>
          </>
        )}
      </StatusSummaryCard>

      <AudioPanel />

      <section className="advanced-sidebar-entry" aria-label="Служебная панель">
        <div>
          <p className="panel-eyebrow">по запросу</p>
          <h3>Служебная панель</h3>
          <p className="muted">Служебные проверки и сведения для ремонта остаются вторичным режимом.</p>
        </div>
        <button
          type="button"
          className="advanced-toggle"
          aria-controls="advanced-diagnostics"
          aria-expanded={advancedEnabled}
          onClick={() => setAdvancedEnabled((value) => !value)}
        >
          {advancedEnabled ? 'Скрыть расширенный режим' : 'Открыть расширенный режим'}
        </button>
      </section>
    </div>
  );
}

function StatusSummaryCard({
  title,
  eyebrow,
  children,
  soft = false,
  attention = false
}: {
  title: string;
  eyebrow: string;
  children: ReactNode;
  soft?: boolean;
  attention?: boolean;
}) {
  const className = `status-summary-card${soft ? ' is-soft' : ''}${attention ? ' is-attention' : ''}`;
  return (
    <section className={className}>
      <p className="panel-eyebrow">{eyebrow}</p>
      <h3>{title}</h3>
      {children}
    </section>
  );
}

function getSidebarFailure<TData>(result: BrowserApiResult<TData> | null | undefined): string | null {
  if (!result || isSuccess(result) || result.kind === 'no-active-session') {
    return null;
  }

  return toPlayerFacingText(result.playerMessage, 'Книга требует внимания.');
}

function getSidebarEmptyGameMessage(readyState: Extract<BrowserShellState, { status: 'ready' }> | null): string {
  const gameFailure = getSidebarFailure(readyState?.game);
  if (gameFailure) {
    return gameFailure;
  }

  return 'Книга ждёт открытия главы.';
}

function getTurnSidebarTitle(hasGame: boolean, sidebarGameFailure: string | null): string {
  if (!hasGame && !sidebarGameFailure) {
    return 'Ход ещё не начат';
  }

  return 'Ожидание ГМа';
}
