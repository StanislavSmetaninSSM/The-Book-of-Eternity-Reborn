import { useMemo, useState } from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import type { BrowserCommandCoverageEntryDto } from '../api/contracts';

interface CommandGroup {
  label: string;
  icon: string;
  commands: BrowserCommandCoverageEntryDto[];
}

const GROUP_ICONS: Record<string, string> = {
  UniversalMeta: '🎭',
  MortalWorld: '🗺️',
  ChaosSea: '🌊',
  ShiningAbode: '✨',
  AfterlifeCombat: '⚔️',
  LifecycleLocalTurn: '🔧',
  Help: '❓',
  Math: '🧮'
};

const GROUP_LABELS: Record<string, string> = {
  UniversalMeta: 'Персонаж и душа',
  MortalWorld: 'Мир смертных',
  ChaosSea: 'Море Хаоса',
  ShiningAbode: 'Сияющая Обитель',
  AfterlifeCombat: 'Духовный бой',
  LifecycleLocalTurn: 'Системные',
  Help: 'Справка',
  Math: 'Утилиты'
};

export function HelpView() {
  const { readyState, executeCommand, setActiveTab } = useShell();
  const [search, setSearch] = useState('');
  const [expandedGroup, setExpandedGroup] = useState<string | null>(null);

  const coverage = readyState?.commandCoverage;
  const commands = coverage && isSuccess(coverage) ? coverage.data.commands : [];

  const groups = useMemo(() => {
    const map = new Map<string, BrowserCommandCoverageEntryDto[]>();
    for (const cmd of commands) {
      if (cmd.browserStatus === 'not-browser-executable') continue;
      const key = cmd.handlerKind;
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(cmd);
    }
    const result: CommandGroup[] = [];
    for (const [key, cmds] of map) {
      result.push({
        label: GROUP_LABELS[key] || key,
        icon: GROUP_ICONS[key] || '📋',
        commands: cmds
      });
    }
    return result;
  }, [commands]);

  const filteredGroups = useMemo(() => {
    if (!search.trim()) return groups;
    const q = search.toLowerCase();
    return groups
      .map((g) => ({
        ...g,
        commands: g.commands.filter((cmd) =>
          cmd.aliases.some((a) => a.toLowerCase().includes(q)) ||
          cmd.primaryActionLabel.toLowerCase().includes(q) ||
          cmd.id.toLowerCase().includes(q)
        )
      }))
      .filter((g) => g.commands.length > 0);
  }, [groups, search]);

  function handleCommandClick(command: string) {
    void executeCommand(command);
    setActiveTab('scene');
  }

  if (!coverage || !isSuccess(coverage)) {
    return (
      <div className="help-view">
        <p className="block-text--muted">Загрузка каталога команд… Включите расширенный режим в настройках для полного доступа.</p>
      </div>
    );
  }

  return (
    <div className="help-view">
      <div className="help-view__search">
        <input
          type="text"
          placeholder="🔍 Поиск команды..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="help-search-input"
        />
      </div>

      <div className="help-view__categories">
        {filteredGroups.map((group) => {
          const isExpanded = expandedGroup === group.label || search.trim().length > 0;
          return (
            <div key={group.label} className="help-category">
              <button
                type="button"
                className="help-category__header"
                onClick={() => setExpandedGroup(isExpanded && !search ? null : group.label)}
              >
                <span className="help-category__icon">{group.icon}</span>
                <span className="help-category__label">{group.label}</span>
                <span className="help-category__count">{group.commands.length}</span>
                <span className="help-category__chevron">{isExpanded ? '▾' : '▸'}</span>
              </button>
              {isExpanded && (
                <div className="help-category__commands">
                  {group.commands.map((cmd) => (
                    <button
                      key={cmd.id}
                      type="button"
                      className="help-command"
                      onClick={() => handleCommandClick(cmd.primaryCommand)}
                    >
                      <span className="help-command__alias">{cmd.aliases[0]}</span>
                      <span className="help-command__label">{cmd.primaryActionLabel}</span>
                      <span className="help-command__run">▶</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
