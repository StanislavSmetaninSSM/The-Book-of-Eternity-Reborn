import { useCallback, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserShellState } from '../context/ShellContext';
import { loadShellState } from './loadShellState';

export function useShellState(advancedEnabled: boolean) {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });
  const publicationOwner = useRef(0);
  const loadBrowserState = useCallback((isCurrent: () => boolean = () => true) => {
    return loadShellState(browserApi, setShellState, publicationOwner, advancedEnabled, isCurrent);
  }, [advancedEnabled]);

  return { shellState, loadBrowserState };
}
