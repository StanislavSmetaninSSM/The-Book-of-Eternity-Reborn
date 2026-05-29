import { useMemo, useState } from 'react';
import { useShell, isSuccess } from '../context/ShellContext';
import { toPlayerFacingText } from '../utils/playerCopy';

export function ActionPalette() {
  const { readyState } = useShell();
  const [search, setSearch] = useState('');

  const gameData = useMemo(() => {
    if (!readyState || !isSuccess(readyState.game)) {
      return null;
    }

    return readyState.game.data;
  }, [readyState]);

  const sections = useMemo(() => {
    if (!gameData) {
      return [];
    }

    const allSections = gameData.actionMenu.sections.filter(
      (section) => section.playerDefault && section.actions.length > 0
    );

    if (!search.trim()) {
      return allSections;
    }

    const needle = search.trim().toLowerCase();
    return allSections
      .map((section) => ({
        ...section,
        actions: section.actions.filter(
          (action) =>
            action.label.toLowerCase().includes(needle) ||
            action.description.toLowerCase().includes(needle)
        )
      }))
      .filter((section) => section.actions.length > 0);
  }, [gameData, search]);

  if (!gameData) {
    return <p className="muted">Действия появятся после открытия главы.</p>;
  }

  return (
    <div className="action-palette" role="tabpanel" aria-label="Палитра действий">
      <input
        type="search"
        className="action-palette__search"
        placeholder="Найти действие…"
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
        aria-label="Поиск действий"
      />
      {sections.length === 0 && (
        <p className="muted">
          {search.trim()
            ? 'Ничего не найдено. Попробуйте другой запрос.'
            : 'Каталог действий пуст для текущей главы.'}
        </p>
      )}
      <div className="action-palette__grid">
        {sections.map((section) => (
          <div key={section.id} className="action-palette__section">
            <h4>{toPlayerFacingText(section.label, 'Раздел')}</h4>
            <p className="muted">{toPlayerFacingText(section.description, '')}</p>
            <ul className="action-palette__list">
              {section.actions.map((action) => (
                <li key={action.id} className={action.enabled ? '' : 'is-disabled'}>
                  <strong>{toPlayerFacingText(action.label, 'Действие')}</strong>
                  <span className="muted">{toPlayerFacingText(action.description, '')}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </div>
  );
}
