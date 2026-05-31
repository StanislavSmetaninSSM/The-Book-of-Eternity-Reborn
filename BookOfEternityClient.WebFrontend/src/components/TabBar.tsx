import { useEffect } from 'react';
import { useShell } from '../context/ShellContext';
import { resolveTabShortcut, tabNav } from './tabBarConfig';

function isShortcutBlockedTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement
    || (target instanceof HTMLElement && target.isContentEditable);
}

export function TabBar() {
  const { activeTab, setActiveTab, gameScreen } = useShell();

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (isShortcutBlockedTarget(event.target) || event.ctrlKey || event.altKey || event.metaKey) {
        return;
      }

      const tabId = resolveTabShortcut(event.key);
      if (!tabId) {
        return;
      }

      event.preventDefault();
      setActiveTab(tabId);
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [setActiveTab]);

  return (
    <nav className="tab-bar" role="tablist" aria-label="Навигация">
      <div className="tab-bar__tabs">
        {tabNav.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            className={`tab-bar__tab${activeTab === tab.id ? ' is-active' : ''}`}
            aria-selected={activeTab === tab.id}
            aria-label={`${tab.label} (${tab.shortcut})`}
            title={`${tab.label} — клавиша ${tab.shortcut}`}
            onClick={() => setActiveTab(tab.id)}
          >
            <span className="tab-bar__icon">{tab.icon}</span>
            <span className="tab-bar__label">{tab.label}</span>
          </button>
        ))}
      </div>
      {gameScreen && (
        <div className="tab-bar__info">
          Ход {gameScreen.world.turnNumber} • {gameScreen.world.location || '—'}
        </div>
      )}
    </nav>
  );
}
