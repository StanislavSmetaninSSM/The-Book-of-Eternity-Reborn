import type { BrowserCommandCoverageEntryDto } from '../api/contracts';

interface Props {
  commands: BrowserCommandCoverageEntryDto[];
  query: string;
  onSelect: (command: string) => void;
}

export function CommandAutocomplete({ commands, query, onSelect }: Props) {
  const q = query.replace(/^\//, '').toLowerCase();
  if (!q) return null;

  const matches = commands
    .filter((cmd) =>
      cmd.aliases.some((a) => a.replace(/^\//, '').toLowerCase().startsWith(q)) ||
      cmd.primaryActionLabel.toLowerCase().includes(q)
    )
    .slice(0, 8);

  if (matches.length === 0) return null;

  return (
    <div className="autocomplete-dropdown">
      {matches.map((cmd) => (
        <button
          key={cmd.id}
          type="button"
          className="autocomplete-item"
          onMouseDown={(e) => { e.preventDefault(); onSelect(cmd.primaryCommand); }}
        >
          <span className="autocomplete-item__alias">{cmd.aliases[0]}</span>
          <span className="autocomplete-item__label">{cmd.primaryActionLabel}</span>
        </button>
      ))}
    </div>
  );
}
