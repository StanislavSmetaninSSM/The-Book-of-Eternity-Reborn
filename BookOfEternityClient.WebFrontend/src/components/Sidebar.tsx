import { useEffect } from 'react';
import { useShell } from '../context/ShellContext';
import { resolveRouteShortcut, routeNav } from './navBarConfig';

function isShortcutBlockedTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement
    || (target instanceof HTMLElement && target.isContentEditable);
}

export function Sidebar() {
  const { activeRoute, realmTheme, setActiveRoute } = useShell();

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (isShortcutBlockedTarget(event.target) || event.ctrlKey || event.altKey || event.metaKey) {
        return;
      }
      const routeId = resolveRouteShortcut(event.key);
      if (!routeId) return;
      event.preventDefault();
      setActiveRoute(routeId);
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [setActiveRoute]);

  const primary = routeNav.filter(r => r.group === 'primary');
  const secondary = routeNav.filter(r => r.group === 'secondary');

  return (
    <nav className="sidebar" aria-label="Разделы игры">
      <div className="sidebar__logo" aria-hidden="true">
        <span className="sidebar__logo-icon">{realmTheme.icon}</span>
      </div>

      <div className="sidebar__primary">
        {primary.map(item => (
          <button
            key={item.id}
            type="button"
            className={`sidebar__item${activeRoute === item.id ? ' is-active' : ''}`}
            onClick={() => setActiveRoute(item.id)}
            aria-current={activeRoute === item.id ? 'page' : undefined}
            aria-label={`${item.label} (${item.shortcut})`}
            title={`${item.label} — клавиша ${item.shortcut}`}
          >
            <span className="sidebar__glyph" aria-hidden="true">{item.glyph}</span>
            <span className="sidebar__label">{item.label}</span>
          </button>
        ))}
      </div>

      <div className="sidebar__secondary">
        {secondary.map(item => (
          <button
            key={item.id}
            type="button"
            className={`sidebar__item${activeRoute === item.id ? ' is-active' : ''}`}
            onClick={() => setActiveRoute(item.id)}
            aria-current={activeRoute === item.id ? 'page' : undefined}
            aria-label={`${item.label} (${item.shortcut})`}
            title={`${item.label} — клавиша ${item.shortcut}`}
          >
            <span className="sidebar__glyph" aria-hidden="true">{item.glyph}</span>
            <span className="sidebar__label">{item.label}</span>
          </button>
        ))}
      </div>
    </nav>
  );
}
