import { useCallback, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserShellState } from '../context/ShellContext';
import { loadShellState } from './loadShellState';
import { refreshShellAfterLoad } from './refreshShellAfterLoad';
import { refreshShellAfterSave } from './refreshShellAfterSave';

export function useShellState(advancedEnabled: boolean) {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });
  const publicationOwner = useRef(0);
  const loadBrowserState = useCallback((isCurrent: () => boolean = () => true) => {
    return loadShellState(browserApi, setShellState, publicationOwner, advancedEnabled, isCurrent);
  }, [advancedEnabled]);

  const refreshAfterSave = useCallback((createdSaveId: string, isCurrent: () => boolean = () => true) => {
    return refreshShellAfterSave(browserApi, setShellState, publicationOwner, advancedEnabled, createdSaveId, isCurrent);
  }, [advancedEnabled]);

  const invalidateRefresh = useCallback(() => { publicationOwner.current++; }, []);
  const refreshAfterLoad = useCallback((generation: string | null, isCurrent: () => boolean, allowNoActiveSession = false) =>
    refreshShellAfterLoad(browserApi, setShellState, publicationOwner, generation, isCurrent, allowNoActiveSession), []);
  return { shellState, loadBrowserState, refreshAfterSave, refreshAfterLoad, invalidateRefresh };
}
