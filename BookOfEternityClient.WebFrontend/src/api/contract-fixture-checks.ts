import mainMenuFixture from './contract-fixtures/main-menu.json';
import sessionStatusFixture from './contract-fixtures/session-status.json';
import gameScreenFixture from './contract-fixtures/game-screen.json';
import lifecycleDashboardFixture from './contract-fixtures/lifecycle-dashboard.json';
import explorerCommandResultFixture from './contract-fixtures/explorer-command-result.json';
import qteStateFixture from './contract-fixtures/qte-state.json';
import qtePracticeStateFixture from './contract-fixtures/qte-practice-state.json';
import qteDarenStateFixture from './contract-fixtures/qte-daren-state.json';
import audioSettingsFixture from './contract-fixtures/audio-settings.json';
import clientSettingsFixture from './contract-fixtures/client-settings.json';
import commandCoverageFixture from './contract-fixtures/command-coverage.json';
import apiErrorFixture from './contract-fixtures/api-error.json';
import type {
  BrowserApiErrorPayload,
  BrowserAudioSettingsDto,
  BrowserClientSettingsDto,
  BrowserCommandCoverageDto,
  DarenShowcaseWebStateDto,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserMainMenuDto,
  ExplorerCommandResult,
  LocalWebUiSessionStatus,
  QtePracticeWebStateDto,
  QteWebStateDto
} from './contracts';

const mainMenuContract = mainMenuFixture satisfies BrowserMainMenuDto;
const sessionStatusContract = sessionStatusFixture satisfies LocalWebUiSessionStatus;
const gameScreenContract = gameScreenFixture satisfies BrowserGameScreenDto;
const lifecycleDashboardContract = lifecycleDashboardFixture satisfies BrowserLifecycleDashboardDto;
const explorerCommandResultContract =
  (explorerCommandResultFixture as unknown as ExplorerCommandResult) satisfies ExplorerCommandResult;
const qteStateContract = qteStateFixture satisfies QteWebStateDto;
const qtePracticeStateContract = qtePracticeStateFixture satisfies QtePracticeWebStateDto;
const qteDarenStateContract = qteDarenStateFixture satisfies DarenShowcaseWebStateDto;
const audioSettingsContract = audioSettingsFixture satisfies BrowserAudioSettingsDto;
const clientSettingsContract = clientSettingsFixture satisfies BrowserClientSettingsDto;
const commandCoverageContract = commandCoverageFixture satisfies BrowserCommandCoverageDto;
const apiErrorContract = apiErrorFixture satisfies BrowserApiErrorPayload;

export const browserApiContractFixtures = {
  mainMenuContract,
  sessionStatusContract,
  gameScreenContract,
  lifecycleDashboardContract,
  explorerCommandResultContract,
  qteStateContract,
  qtePracticeStateContract,
  qteDarenStateContract,
  audioSettingsContract,
  clientSettingsContract,
  commandCoverageContract,
  apiErrorContract
};
