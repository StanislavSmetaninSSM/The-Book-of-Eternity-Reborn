import { useEffect } from 'react';
import { useShell } from '../context/ShellContext';
import { resolveTabShortcut, tabNav, type TabGlyphId } from './tabBarConfig';

function isShortcutBlockedTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement
    || (target instanceof HTMLElement && target.isContentEditable);
}

export function TabBar() {
  const { activeTab, setActiveTab, setActiveRoute, gameScreen } = useShell();

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
      <button
        type="button"
        className="tab-bar__home"
        aria-label="Главное меню"
        title="Главное меню"
        onClick={() => setActiveRoute('home')}
      >
        <span className="tab-bar__glyph tab-bar__glyph--home" aria-hidden="true">
          <svg viewBox="0 0 24 24" focusable="false">
            <path d="M4 11.2 12 4l8 7.2V20a1 1 0 0 1-1 1h-4.5v-6h-5v6H5a1 1 0 0 1-1-1Z" />
          </svg>
        </span>
        <span className="tab-bar__label">Главное меню</span>
      </button>
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
            <TabGlyph glyph={tab.glyph} />
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

function TabGlyph({ glyph }: { glyph: TabGlyphId }) {
  return (
    <span className={`tab-bar__glyph tab-bar__glyph--${glyph}`} aria-hidden="true">
      <svg viewBox="0 0 24 24" focusable="false">
        {renderGlyphPath(glyph)}
      </svg>
    </span>
  );
}

function renderGlyphPath(glyph: TabGlyphId) {
  switch (glyph) {
    case 'scene':
      return (
        <>
          <path d="M6 4.6h8.8a3.2 3.2 0 0 1 3.2 3.2v10.7H8.2A3.2 3.2 0 0 1 5 15.3V5.6a1 1 0 0 1 1-1Z" />
          <path d="M8 7.6h7.4M8 10.8h6.2M8 14h4.4" />
        </>
      );
    case 'practice':
      return (
        <>
          <path d="M13.2 3.8 6.4 13h4.7l-1 7.2 7.2-9.7h-4.8l.7-6.7Z" />
          <path d="M5.5 18.2h4.3M14.5 5.8h4" />
        </>
      );
    case 'status':
      return (
        <>
          <path d="M12 4.3c3.2 1.9 5.2 4.8 5.2 8.2 0 3.2-2 5.8-5.2 7.2-3.2-1.4-5.2-4-5.2-7.2 0-3.4 2-6.3 5.2-8.2Z" />
          <path d="M9 12h2l1-3 1.4 5 1-2H16" />
        </>
      );
    case 'help':
      return (
        <>
          <path d="M5.7 7.2c1.4-2 3.5-3 6.1-3 3.6 0 6.5 2.4 6.5 5.8 0 2.5-1.5 4.2-4.2 5.2l-1 .4v2" />
          <path d="M12 21h.1M8.8 8.1a3.8 3.8 0 0 1 3-1.2c1.7 0 3.1 1 3.1 2.6 0 1.3-.7 2.1-2.3 2.8" />
        </>
      );
    case 'settings':
      return (
        <>
          <path d="M12 8.1a3.9 3.9 0 1 0 0 7.8 3.9 3.9 0 0 0 0-7.8Z" />
          <path d="m4.8 10.1 1.5-.9.4-1.1-.6-1.6 1.8-1.8 1.6.6 1.1-.4.9-1.5h2.6l.9 1.5 1.1.4 1.6-.6 1.8 1.8-.6 1.6.4 1.1 1.5.9v2.6l-1.5.9-.4 1.1.6 1.6-1.8 1.8-1.6-.6-1.1.4-.9 1.5h-2.6l-.9-1.5-1.1-.4-1.6.6-1.8-1.8.6-1.6-.4-1.1-1.5-.9v-2.6Z" />
        </>
      );
  }
}
