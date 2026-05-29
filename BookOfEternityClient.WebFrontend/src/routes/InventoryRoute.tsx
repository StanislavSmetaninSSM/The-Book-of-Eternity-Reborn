import { FilteredActionSections } from '../components/ActionMenu';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { ShellPanel } from '../components/ShellPanel';
import { isSuccess, useShell } from '../context/ShellContext';
import { filterActionSections, inventorySectionMatchers } from '../utils/actionFilters';
import { formatSidebarStatusMetric } from '../utils/formatters';

export default function InventoryRoute() {
  const { advancedEnabled, readyState } = useShell();

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.game)) {
    return <EmptyOrFailure result={readyState.game} advancedEnabled={advancedEnabled} errorTitle="Инвентарь требует внимания" empty={{
      title: 'Инвентарь ждёт главу',
      message: 'Предметы, экипировка, ремесло и хранилища появятся после открытия или загрузки игровой сессии.',
      action: 'Откройте книгу на главной странице, затем вернитесь к инвентарю.'
    }} />;
  }

  const game = readyState.game.data;
  const sections = filterActionSections(game.actionMenu, inventorySectionMatchers);

  return (
    <ShellPanel title="Инвентарь" eyebrow="предметы, ремесло и хранилища">
      <div className="split-grid">
        <div className="summary-card">
          <h2>Герой</h2>
          <p>{game.player.name || 'Герой'} · {game.player.currentCondition}</p>
          <p className="muted">Здоровье {formatSidebarStatusMetric(game.player.healthPercentage)} · энергия {formatSidebarStatusMetric(game.player.energyPercentage)} · стойкость {formatSidebarStatusMetric(game.player.poisePercentage)}</p>
        </div>
        <div className="summary-card">
          <h2>Ремесло и предметы</h2>
          <p>Инвентарь использует существующие игровые действия и не добавляет отдельные правила предметов в React.</p>
        </div>
      </div>
      <FilteredActionSections sections={sections} emptyMessage="Инвентарные, ремесленные и складские разделы появятся здесь, когда каталог действий отдаст их для текущей главы." />
    </ShellPanel>
  );
}
