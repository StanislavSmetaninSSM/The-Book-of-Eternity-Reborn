import type { BrowserGameScreenDto, BrowserPlayerCommandMenuDto, BrowserPlayerCommandSectionDto } from '../api/contracts';
import { formatTurnLifecycleActionDescription } from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';
import { ActionCard } from './ActionCard';

export function FilteredActionSections({ sections, emptyMessage }: { sections: BrowserPlayerCommandSectionDto[]; emptyMessage: string }) {
  if (sections.length === 0) {
    return <p className="muted">{emptyMessage}</p>;
  }

  return (
    <section className="action-menu" aria-label="Игровые разделы страницы">
      <div className="action-section-grid">
        {sections.map((section) => <ActionSection key={section.id} section={section} />)}
      </div>
    </section>
  );
}

export function ActionMenu({ menu }: { menu: BrowserPlayerCommandMenuDto }) {
  const sections = menu.sections.filter((section) => section.playerDefault && section.actions.length > 0);

  return (
    <section className="action-menu" aria-labelledby="contextual-actions-title">
      <div className="action-menu-header">
        <p className="panel-eyebrow">игровые действия</p>
        <h2 id="contextual-actions-title">Игровые действия</h2>
        <p className="muted">
          Персонаж / Душа, Мир, Квесты, Карта, Фракции, Хранители, Посмертие, Бой, Архив и Настройки
          собираются из игрового каталога действий. Технические имена команд остаются в расширенном режиме.
        </p>
      </div>
      <div className="action-section-grid">
        {sections.map((section) => (
          <ActionSection key={section.id} section={section} />
        ))}
      </div>
    </section>
  );
}

export function TurnLifecycleActions({ turnState }: { turnState: BrowserGameScreenDto['turnState'] }) {
  const playerActions = turnState.recommendedActions.filter((action) => action.surface === 'player-default');
  const advancedActions = turnState.recommendedActions.filter((action) => action.surface === 'advanced-only');

  return (
    <div className="turn-lifecycle-actions" aria-label="Рекомендуемые действия состояния хода">
      {playerActions.length > 0 && (
        <ul className="choice-list">
          {playerActions.map((action) => (
            <li key={action.id}>
              <strong>{toPlayerFacingText(action.label, 'Действие')}</strong>
              <span>{formatTurnLifecycleActionDescription(action)}</span>
            </li>
          ))}
        </ul>
      )}
      {advancedActions.length > 0 && (
        <p className="muted">Технические действия для этого состояния доступны только через «Расширенный режим».</p>
      )}
    </div>
  );
}

function ActionSection({ section }: { section: BrowserPlayerCommandSectionDto }) {
  return (
    <section className="action-section" aria-labelledby={`action-section-${section.id}`}>
      <div>
        <h3 id={`action-section-${section.id}`}>{toPlayerFacingText(section.label, 'Игровой раздел')}</h3>
        <p className="muted">{toPlayerFacingText(section.description, 'Действия этого раздела доступны ниже.')}</p>
      </div>
      <div className="action-card-list">
        {section.actions.map((action) => (
          <ActionCard key={action.id} action={action} />
        ))}
      </div>
    </section>
  );
}
