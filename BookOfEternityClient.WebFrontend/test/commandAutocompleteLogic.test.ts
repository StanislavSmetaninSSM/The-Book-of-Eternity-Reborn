import { describe, expect, it } from 'vitest';
import type { BrowserCommandCoverageEntryDto } from '../src/api/contracts.js';
import {
  getAutocompleteMatches,
  moveAutocompleteHighlight,
  resolveAutocompleteEnterAction
} from '../src/components/commandAutocompleteLogic.js';

function command(id: string, aliases: string[], primaryActionLabel: string, primaryCommand = aliases[0]): BrowserCommandCoverageEntryDto {
  return {
    id,
    aliases,
    group: 'Help',
    mutationMode: 'read-only',
    browserStatus: 'browser-executable',
    handlerKind: 'Help',
    uxDecision: 'player-default',
    surface: 'player-default',
    formMode: 'none',
    primaryActionLabel,
    primaryCommand,
    subcommands: [],
    followUpIssue: '',
    reason: ''
  };
}

describe('command autocomplete logic', () => {
  const commands = [
    command('inventory', ['/inventory', '/inv'], 'Open inventory', '/inventory'),
    command('inspect', ['/inspect'], 'Inspect surroundings'),
    command('status', ['/status'], 'Character status'),
    command('map', ['/map'], 'Open map')
  ];

  it('matches slash queries by alias or label and caps visible results', () => {
    const manyCommands = Array.from({ length: 10 }, (_, index) =>
      command(`item-${index}`, [`/item_${index}`], `Item command ${index}`)
    );

    expect(getAutocompleteMatches(commands, '/in').map((match) => match.id)).toEqual(['inventory', 'inspect']);
    expect(getAutocompleteMatches(commands, '/map').map((match) => match.id)).toEqual(['map']);
    expect(getAutocompleteMatches(manyCommands, '/item')).toHaveLength(8);
    expect(getAutocompleteMatches(commands, 'inventory')).toEqual([]);
  });

  it('moves a visible highlight only after arrow navigation starts it', () => {
    expect(moveAutocompleteHighlight(null, 'down', 3)).toBe(0);
    expect(moveAutocompleteHighlight(0, 'down', 3)).toBe(1);
    expect(moveAutocompleteHighlight(2, 'down', 3)).toBe(0);
    expect(moveAutocompleteHighlight(null, 'up', 3)).toBe(2);
    expect(moveAutocompleteHighlight(0, 'up', 3)).toBe(2);
    expect(moveAutocompleteHighlight(1, 'up', 0)).toBeNull();
  });

  it('selects the highlighted suggestion on Enter without submitting in the same event', () => {
    const matches = getAutocompleteMatches(commands, '/in');

    expect(resolveAutocompleteEnterAction({
      isOpen: true,
      activeIndex: 0,
      matches,
      text: '/in',
      canSubmit: true
    })).toEqual({ kind: 'select', command: '/inventory' });
  });

  it('submits the current command on Enter when autocomplete is open with no highlighted suggestion', () => {
    const matches = getAutocompleteMatches(commands, '/inventory');

    expect(resolveAutocompleteEnterAction({
      isOpen: true,
      activeIndex: null,
      matches,
      text: '/inventory',
      canSubmit: true
    })).toEqual({ kind: 'submit', text: '/inventory' });
  });

  it('does not submit blank text or disabled composer text without a highlighted suggestion', () => {
    expect(resolveAutocompleteEnterAction({
      isOpen: true,
      activeIndex: null,
      matches: getAutocompleteMatches(commands, '/in'),
      text: '   ',
      canSubmit: true
    })).toEqual({ kind: 'none' });

    expect(resolveAutocompleteEnterAction({
      isOpen: false,
      activeIndex: null,
      matches: [],
      text: '/inventory',
      canSubmit: false
    })).toEqual({ kind: 'none' });
  });
});
