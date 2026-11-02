import type {
  BrowserAudioSettingsDto, BrowserClientSettingsDto, BrowserCommandCoverageDto,
  BrowserGameScreenDto, BrowserMainMenuDto, ExplorerCommandResult, LocalWebUiSessionStatus
} from '../../src/api/contracts';
import { browserApiContractFixtures } from '../../src/api/contract-fixture-checks';
import { woundCase } from './woundCommandFixtures';
import { createWoundHelpCoverage } from './woundHelpCoverage';

export interface WoundActualShellPackets {
  bootstrap: {
    '/api/main-menu': BrowserMainMenuDto;
    '/api/session': LocalWebUiSessionStatus;
    '/api/game-screen': BrowserGameScreenDto;
    '/api/audio/settings': BrowserAudioSettingsDto;
    '/api/client/settings': BrowserClientSettingsDto;
    '/api/explorer/command-coverage': BrowserCommandCoverageDto;
  };
  commands: { command: string; response: ExplorerCommandResult }[];
}

// Fresh controlled transport DATA; no ShellContext, backend authority or React state switch.
export function createWoundActualShellPackets(): WoundActualShellPackets {
  const game = structuredClone(browserApiContractFixtures.gameScreenContract);
  game.narrative.imagePrompt = '';
  game.media.sceneImagePrompt = '';
  game.media.gallery = [];
  const commands = ['/раны', '/раны история', '/раны'].map((command, index) => {
    const response = woundCase(index === 1 ? 'history' : 'active');
    response.blocks.push({ kind: 'rawJson', title: 'Служебные сведения',
      json: { hidden_route: 'ACTUAL_SHELL_PRIVATE_SENTINEL' } });
    return { command, response };
  });
  return {
    bootstrap: {
      '/api/main-menu': structuredClone(browserApiContractFixtures.mainMenuContract),
      '/api/session': structuredClone(browserApiContractFixtures.sessionStatusContract),
      '/api/game-screen': game,
      '/api/audio/settings': structuredClone(browserApiContractFixtures.audioSettingsContract),
      '/api/client/settings': structuredClone(browserApiContractFixtures.clientSettingsContract),
      '/api/explorer/command-coverage': createWoundHelpCoverage()
    },
    commands
  };
}
