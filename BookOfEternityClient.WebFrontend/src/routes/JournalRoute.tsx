import { FilteredActionSections } from '../components/ActionMenu';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { ShellPanel } from '../components/ShellPanel';
import { isSuccess, useShell } from '../context/ShellContext';
import { filterActionSections, journalSectionMatchers } from '../utils/actionFilters';

export default function JournalRoute() {
  const { advancedEnabled, readyState } = useShell();

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.game)) {
    return <EmptyOrFailure result={readyState.game} advancedEnabled={advancedEnabled} errorTitle="Журнал требует внимания" empty={{
      title: 'Журнал ждёт главу',
      message: 'Квесты, хроника и заметки появятся после открытия или загрузки игровой сессии.',
      action: 'Откройте книгу на главной странице, затем вернитесь в журнал.'
    }} />;
  }

  const game = readyState.game.data;
  const sections = filterActionSections(game.actionMenu, journalSectionMatchers);

  return (
    <ShellPanel title="Журнал" eyebrow="квесты, хроника и заметки">
      <div className="split-grid">
        <div className="summary-card">
          <h2>Текущая глава</h2>
          <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
        </div>
        <div className="summary-card">
          <h2>Ориентир игрока</h2>
          <p>{game.narrative.dialogueOptions.length > 0 ? `Доступно вариантов: ${game.narrative.dialogueOptions.length}.` : 'Варианты выбора появятся после ответа ГМа.'}</p>
          <p className="muted">Журнал показывает игровые разделы из каталога действий без служебных команд.</p>
        </div>
      </div>
      <FilteredActionSections sections={sections} emptyMessage="Квестовые, архивные и фракционные разделы появятся здесь, когда каталог действий отдаст их для текущей главы." />
    </ShellPanel>
  );
}
