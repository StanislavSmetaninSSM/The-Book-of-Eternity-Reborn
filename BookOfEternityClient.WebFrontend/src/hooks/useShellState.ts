import { useCallback, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserShellState } from '../context/ShellContext';

export function useShellState(advancedEnabled: boolean) {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });

  const loadBrowserState = useCallback(async () => {
    setShellState({ status: 'loading' });

    try {
      const [menu, session, game, audio, settings] = await Promise.all([
        browserApi.getMainMenu(),
        browserApi.getSessionStatus(),
        browserApi.getGameScreen(),
        browserApi.getAudioSettings(),
        browserApi.getClientSettings()
      ]);
      const [lifecycle, commandCoverage] = advancedEnabled
        ? await Promise.all([
            browserApi.getLifecycleDashboard(),
            browserApi.getCommandCoverage()
          ])
        : [null, null];

      setShellState({ status: 'ready', menu, session, game, audio, settings, lifecycle, commandCoverage });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown browser shell error.';
      setShellState({
        status: 'error',
        playerMessage: 'Браузерный клиент не смог собрать состояние игры.',
        technicalDetails: message
      });
    }
  }, [advancedEnabled]);

  return { shellState, loadBrowserState };
}
