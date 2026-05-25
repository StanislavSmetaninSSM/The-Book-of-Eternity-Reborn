import mainMenuFixture from './contract-fixtures/main-menu.json';
import sessionStatusFixture from './contract-fixtures/session-status.json';
import gameScreenFixture from './contract-fixtures/game-screen.json';
import lifecycleDashboardFixture from './contract-fixtures/lifecycle-dashboard.json';
import explorerCommandResultFixture from './contract-fixtures/explorer-command-result.json';
import qteStateFixture from './contract-fixtures/qte-state.json';
import audioSettingsFixture from './contract-fixtures/audio-settings.json';
import apiErrorFixture from './contract-fixtures/api-error.json';
import type {
  BrowserApiErrorPayload,
  BrowserAudioSettingsDto,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserMainMenuDto,
  ExplorerCommandResult,
  LocalWebUiSessionStatus,
  QteWebStateDto
} from './contracts';

const mainMenuContract = mainMenuFixture satisfies BrowserMainMenuDto;
const sessionStatusContract = sessionStatusFixture satisfies LocalWebUiSessionStatus;
const gameScreenContract = gameScreenFixture satisfies BrowserGameScreenDto;
const lifecycleDashboardContract = lifecycleDashboardFixture satisfies BrowserLifecycleDashboardDto;
const explorerCommandResultContract =
  (explorerCommandResultFixture as unknown as ExplorerCommandResult) satisfies ExplorerCommandResult;
const qteStateContract = qteStateFixture satisfies QteWebStateDto;
const audioSettingsContract = audioSettingsFixture satisfies BrowserAudioSettingsDto;
const apiErrorContract = apiErrorFixture satisfies BrowserApiErrorPayload;

export const browserApiContractFixtures = {
  mainMenuContract,
  sessionStatusContract,
  gameScreenContract,
  lifecycleDashboardContract,
  explorerCommandResultContract,
  qteStateContract,
  audioSettingsContract,
  apiErrorContract
};
