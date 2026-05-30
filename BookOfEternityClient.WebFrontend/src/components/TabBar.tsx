import { useShell, type TabId } from '../context/ShellContext';

interface TabDef {
  id: TabId;
  icon: string;
  label: string;
}

const TABS: TabDef[] = [
  { id: 'scene', icon: '📖', label: 'Сцена' },
  { id: 'status', icon: '📊', label: 'Статус' },
  { id: 'help', icon: '❓', label: 'Помощь' },
  { id: 'settings', icon: '⚙️', label: 'Настройки' }
];

export function TabBar() {
  const { activeTab, setActiveTab, gameScreen } = useShell();

  return (
    <nav className="tab-bar" role="tablist" aria-label="Навигация">
      <div className="tab-bar__tabs">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            className={`tab-bar__tab${activeTab === tab.id ? ' is-active' : ''}`}
            aria-selected={activeTab === tab.id}
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
