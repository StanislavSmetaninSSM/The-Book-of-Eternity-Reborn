import type { BrowserCommandCoverageDto, BrowserCommandCoverageEntryDto } from '../../src/api/contracts';
import { browserApiContractFixtures } from '../../src/api/contract-fixture-checks';

// Public component DATA only: preserve the actual wounds routing fields, not a live backend claim.
export function createWoundHelpCoverage(): BrowserCommandCoverageDto {
  const template = browserApiContractFixtures.commandCoverageContract.commands.find(row => row.id === 'status');
  if (!template) throw new Error('The existing public read-only status template is required.');
  const row: BrowserCommandCoverageEntryDto = {
    ...structuredClone(template),
    id: 'wounds', aliases: ['/раны', '/wounds'], group: 'UniversalMeta',
    mutationMode: 'ReadOnly', browserStatus: 'ReadOnlyParity', handlerKind: 'Wounds',
    surface: 'player-default', formMode: 'none', primaryActionLabel: 'Раны и заживление',
    primaryCommand: '/раны', subcommands: []
  };
  return {
    schemaVersion: 2,
    summary: {
      descriptorCount: 1, aliasCount: 2, subcommandCount: 0,
      browserExecutableCount: 1, browserExecutableSubcommandCount: 0,
      playerDefaultActionCount: 1, playerDefaultExecutableSubcommandCount: 0,
      advancedOnlyActionCount: 0, mutatingCommandCount: 0,
      commandsNeedingFollowUpCount: 0, subcommandsNeedingFollowUpCount: 0
    },
    commands: [row]
  };
}
