import { useEffect } from 'react';
import { useShell } from '../context/ShellContext';
import { resolveRouteShortcut, routeNav } from './navBarConfig';

function isShortcutBlockedTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement
    || (target instanceof HTMLElement && target.isContentEditable);
}

export function NavBar() {
  const { activeRoute, realmTheme, setActiveRoute } = useShell();

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (isShortcutBlockedTarget(event.target) || event.ctrlKey || event.altKey || event.metaKey) {
        return;
      }

      const routeId = resolveRouteShortcut(event.key);
      if (!routeId) {
        return;
      }

      event.preventDefault();
      setActiveRoute(routeId);
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [setActiveRoute]);

  return (
    <nav className="nav-bar" aria-label="Разделы игры">
      <span className="nav-bar__realm" aria-label="Текущее царство">
        {realmTheme.icon} {realmTheme.label}
      </span>
      <div className="nav-bar__items">
        {routeNav.map(({ id, glyph, label, shortcut }) => (
          <button
            key={id}
            type="button"
            className={`nav-bar__item${activeRoute === id ? ' is-active' : ''}`}
            onClick={() => setActiveRoute(id)}
            aria-pressed={activeRoute === id}
            aria-label={`${label} (${shortcut})`}
            title={`${label} — клавиша ${shortcut}`}
          >
            <span className="nav-bar__glyph" aria-hidden="true">{glyph}</span>
            <span className="nav-bar__label">{label}</span>
          </button>
        ))}
      </div>
    </nav>
  );
}
