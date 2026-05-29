import type {
  BrowserApiResult,
  BrowserCommandCoverageDto
} from '../src/api/contracts.js';
import type { BrowserShellState, ShellContextValue } from '../src/context/ShellContext.js';
import { isSuccess } from '../src/context/ShellContext.js';

const extractedModules = {
  homeRoute: null as unknown as typeof import('../src/routes/HomeRoute.js').default,
  gameRoute: null as unknown as typeof import('../src/routes/GameRoute.js').default,
  soulRoute: null as unknown as typeof import('../src/routes/SoulRoute.js').default,
  worldRoute: null as unknown as typeof import('../src/routes/WorldRoute.js').default,
  journalRoute: null as unknown as typeof import('../src/routes/JournalRoute.js').default,
  inventoryRoute: null as unknown as typeof import('../src/routes/InventoryRoute.js').default,
  mediaRoute: null as unknown as typeof import('../src/routes/MediaRoute.js').default,
  settingsRoute: null as unknown as typeof import('../src/routes/SettingsRoute.js').default,
  actionCard: null as unknown as typeof import('../src/components/ActionCard.js').ActionCard,
  commandResult: null as unknown as typeof import('../src/components/CommandResult.js').ActionCommandResult,
  promptForm: null as unknown as typeof import('../src/components/PromptForm.js').PromptForm,
  rebornSystemsPanel: null as unknown as typeof import('../src/components/RebornSystemsPanel.js').RebornSystemsPanel,
  qteScenePanel: null as unknown as typeof import('../src/components/QteScenePanel.js').QteScenePanel,
  audioPanel: null as unknown as typeof import('../src/components/AudioPanel.js').AudioPanel,
  playerStatusSidebar: null as unknown as typeof import('../src/components/PlayerStatusSidebar.js').PlayerStatusSidebar,
  advancedDiagnostics: null as unknown as typeof import('../src/components/AdvancedDiagnostics.js').AdvancedDiagnosticsPanel
};

const shellContextValue = null as unknown as ShellContextValue;
const composerState: Pick<ShellContextValue, 'composerText' | 'setComposerText' | 'composerNotice' | 'submitComposer'> = shellContextValue;

const readyShellState = null as unknown as Extract<BrowserShellState, { status: 'ready' }>;
const commandCoverage = null as BrowserApiResult<BrowserCommandCoverageDto> | null;

void extractedModules;
void composerState;
void readyShellState;
void commandCoverage;
void isSuccess;
