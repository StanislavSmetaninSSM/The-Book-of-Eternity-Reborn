import { useCallback, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserShellState } from '../context/ShellContext';
import { settledToResult } from './shellStateResult';

export function useShellState(advancedEnabled: boolean) {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });

  const loadBrowserState = useCallback(async () => {
    setShellState((prev) => prev.status === 'ready' ? prev : { status: 'loading' });

    const results = await Promise.allSettled([
      browserApi.getMainMenu(),
      browserApi.getSessionStatus(),
      browserApi.getGameScreen(),
      browserApi.getAudioSettings(),
      browserApi.getClientSettings()
    ]);

    const menu = settledToResult(results[0]);
    const session = settledToResult(results[1]);
    const game = settledToResult(results[2]);
    const audio = settledToResult(results[3]);
    const settings = settledToResult(results[4]);

    const allFailed = [menu, session, game, audio, settings].every((result) => !result.ok && result.kind === 'network-error');

    if (allFailed) {
      setShellState({
        status: 'error',
        playerMessage: 'Локальная книга недоступна. Убедитесь, что игра запущена.',
        technicalDetails: !menu.ok ? menu.message : 'Запросы к книге не ответили.'
      });
      return;
    }

    const anyNetworkFailed = [menu, session, game, audio, settings].some((result) => !result.ok && result.kind === 'network-error');

    let lifecycle = null;
    let commandCoverage = null;

    if (advancedEnabled) {
      const advResults = await Promise.allSettled([
        browserApi.getLifecycleDashboard(),
        browserApi.getCommandCoverage()
      ]);
      lifecycle = settledToResult(advResults[0]);
      commandCoverage = settledToResult(advResults[1]);
    }

    setShellState({
      status: 'ready',
      connectionStatus: anyNetworkFailed ? 'partial' : 'connected',
      menu,
      session,
      game,
      audio,
      settings,
      lifecycle,
      commandCoverage
    });
  }, [advancedEnabled]);

  return { shellState, loadBrowserState };
}
