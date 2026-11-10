// Controlled public-packet host for real DOM acceptance; never part of the app entrypoint.
import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { CommandResultView } from '../../src/components/CommandResultView';
import { ShellContext } from '../../src/context/ShellContext';
import type { ExplorerCommandResult } from '../../src/api/contracts';
import { sanitizeExplorerCommandResultForPlayer } from '../../src/utils/playerCopy';
import { woundCase, woundShell, type WoundCase } from '../helpers/woundCommandFixtures';
import '../../src/styles.css';

export interface WoundBrowserFixture {
  setCase: (name: WoundCase) => void;
  result: typeof woundCase;
  commands: string[];
  clears: number;
  refreshes: number;
  holdActions: boolean;
  releaseAction: (() => void) | null;
  unmountView: () => void;
}
declare global { interface Window { woundFixture: WoundBrowserFixture } }

function Host() {
  const [result, setResult] = useState<ExplorerCommandResult | null>(() => sanitizeExplorerCommandResultForPlayer(woundCase('active')));
  window.woundFixture.setCase = name => setResult(sanitizeExplorerCommandResultForPlayer(woundCase(name)));
  window.woundFixture.unmountView = () => setResult(null);
  const shell = woundShell(result, {
    async executeCommand(command) {
      window.woundFixture.commands.push(command);
      if (window.woundFixture.holdActions)
        await new Promise<void>(resolve => { window.woundFixture.releaseAction = resolve; });
      setResult(sanitizeExplorerCommandResultForPlayer(woundCase(command === '/раны история' ? 'history' : 'active')));
    },
    clearCommandResult() { window.woundFixture.clears++; setResult(null); },
    async loadBrowserState() { window.woundFixture.refreshes++; }
  });
  return <ShellContext.Provider value={shell}>{result && <CommandResultView />}</ShellContext.Provider>;
}
window.woundFixture = { setCase() {}, result: woundCase, commands: [], clears: 0, refreshes: 0,
  holdActions: false, releaseAction: null, unmountView() {} };
createRoot(document.getElementById('root')!).render(<Host />);
