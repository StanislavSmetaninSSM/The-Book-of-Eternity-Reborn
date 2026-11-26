import { useCallback, useRef, useState } from 'react';
import type { BrowserApiResult, BrowserLoadStateDto, LocalWebUiSessionStatus } from '../api/contracts';
import { browserApi } from '../api/client';
import type { BrowserShellState } from '../context/ShellContext';
import { loadShellState } from './loadShellState';
import { refreshShellAfterLoad } from './refreshShellAfterLoad';
import { refreshShellAfterSave } from './refreshShellAfterSave';
import { canDisplaySettledTurnScene } from './waitForBrowserTurnSettled';

export function useShellState(advancedEnabled: boolean) {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });
  const publicationOwner = useRef(0);
  const loadBrowserState = useCallback((isCurrent: () => boolean = () => true, preserveReadyOnFailure = false) => {
    return loadShellState(browserApi, setShellState, publicationOwner, advancedEnabled, isCurrent, preserveReadyOnFailure);
  }, [advancedEnabled]);

  const refreshAfterSave = useCallback((createdSaveId: string, isCurrent: () => boolean = () => true) => {
    return refreshShellAfterSave(browserApi, setShellState, publicationOwner, advancedEnabled, createdSaveId, isCurrent);
  }, [advancedEnabled]);

  // The final turn refresh is narrow and publishes only successful, current
  // session+scene DATA. A failed scene fetch never erases the previous screen.
  const refreshSettledTurn = useCallback(async (
    session: Extract<BrowserApiResult<LocalWebUiSessionStatus>, { ok: true }>,
    signal: AbortSignal,
    isCurrent: () => boolean,
    turnRefreshToken: number
  ): Promise<boolean> => {
    if (!isCurrent() || shellState.status !== 'ready' || signal.aborted) return false;
    const refresh = ++publicationOwner.current;
    const current = () => isCurrent() && publicationOwner.current === refresh;
    const game = await browserApi.getGameScreen(signal);
    if (!current() || signal.aborted || !game.ok ||
      !canDisplaySettledTurnScene(game.data.turnState.state)) return false;
    setShellState(previous => current() && previous.status === 'ready'
      ? { ...previous, session, game, turnRefreshToken }
      : previous);
    return true;
  }, [shellState.status]);

  const invalidateRefresh = useCallback(() => { publicationOwner.current++; }, []);
  const refreshAfterLoad = useCallback((generation: string | null, isCurrent: () => boolean, allowNoActiveSession = false, state?: BrowserLoadStateDto | null) =>
    refreshShellAfterLoad(browserApi, setShellState, publicationOwner, generation, isCurrent, allowNoActiveSession, state), []);
  return { shellState, loadBrowserState, refreshAfterSave, refreshAfterLoad, refreshSettledTurn, invalidateRefresh };
}
