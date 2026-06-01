import type { BrowserCommandCoverageEntryDto } from '../api/contracts';

export type AutocompleteHighlightDirection = 'up' | 'down';

export type AutocompleteEnterAction =
  | { kind: 'select'; command: string }
  | { kind: 'submit'; text: string }
  | { kind: 'none' };

export function getAutocompleteMatches(
  commands: BrowserCommandCoverageEntryDto[],
  query: string
): BrowserCommandCoverageEntryDto[] {
  if (!query.startsWith('/')) return [];

  const normalizedQuery = query.slice(1).toLowerCase();
  if (!normalizedQuery || normalizedQuery.includes(' ')) return [];

  return commands
    .filter((cmd) =>
      cmd.aliases.some((alias) => alias.replace(/^\//, '').toLowerCase().startsWith(normalizedQuery)) ||
      cmd.primaryActionLabel.toLowerCase().includes(normalizedQuery)
    )
    .slice(0, 8);
}

export function moveAutocompleteHighlight(
  currentIndex: number | null,
  direction: AutocompleteHighlightDirection,
  matchCount: number
): number | null {
  if (matchCount <= 0) return null;
  if (currentIndex === null || currentIndex < 0 || currentIndex >= matchCount) {
    return direction === 'down' ? 0 : matchCount - 1;
  }

  return direction === 'down'
    ? (currentIndex + 1) % matchCount
    : (currentIndex - 1 + matchCount) % matchCount;
}

export function resolveAutocompleteEnterAction({
  isOpen,
  activeIndex,
  matches,
  text,
  canSubmit
}: {
  isOpen: boolean;
  activeIndex: number | null;
  matches: BrowserCommandCoverageEntryDto[];
  text: string;
  canSubmit: boolean;
}): AutocompleteEnterAction {
  if (isOpen && activeIndex !== null) {
    const selected = matches[activeIndex];
    if (selected) return { kind: 'select', command: selected.primaryCommand };
  }

  if (text.trim() && canSubmit) return { kind: 'submit', text };
  return { kind: 'none' };
}
