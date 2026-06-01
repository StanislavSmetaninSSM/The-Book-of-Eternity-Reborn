import type { BrowserCommandCoverageEntryDto } from '../api/contracts';

interface Props {
  matches: BrowserCommandCoverageEntryDto[];
  activeIndex: number | null;
  onSelect: (command: string) => void;
}

export function CommandAutocomplete({ matches, activeIndex, onSelect }: Props) {
  const listboxId = 'command-autocomplete-list';

  return (
    <div id={listboxId} className="autocomplete-dropdown" role="listbox">
      {matches.map((cmd, index) => (
        <button
          key={cmd.id}
          id={`${listboxId}-option-${index}`}
          type="button"
          role="option"
          aria-selected={activeIndex === index}
          className={`autocomplete-item${activeIndex === index ? ' is-active' : ''}`}
          onMouseDown={(e) => { e.preventDefault(); onSelect(cmd.primaryCommand); }}
        >
          <span className="autocomplete-item__alias">{cmd.aliases[0]}</span>
          <span className="autocomplete-item__label">{cmd.primaryActionLabel}</span>
        </button>
      ))}
    </div>
  );
}
