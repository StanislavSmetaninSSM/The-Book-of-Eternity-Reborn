import type { BrowserApiClient } from '../api/client';
import type { BrowserShellState } from '../context/ShellContext';
import { loadShellState, type PublishShellState, type ShellRefreshOwner } from './loadShellState';

/** Confirms a save's exact destination from an actually published refresh without changing ordinary refresh semantics. */
export async function refreshShellAfterSave(
  browserApi: BrowserApiClient,
  setShellState: PublishShellState,
  publicationOwner: ShellRefreshOwner,
  advancedEnabled: boolean,
  createdSaveId: string,
  requestIsCurrent: () => boolean = () => true
): Promise<boolean> {
  if (!createdSaveId || !requestIsCurrent()) return false;
  const expectedRefresh = publicationOwner.current + 1;
  let published: BrowserShellState | undefined;
  await loadShellState(browserApi, next => {
    if (typeof next !== 'function') published = next;
    setShellState(next);
  }, publicationOwner, advancedEnabled, requestIsCurrent);
  const snapshot = published as BrowserShellState | undefined;
  if (!requestIsCurrent() || publicationOwner.current !== expectedRefresh || snapshot?.status !== 'ready') return false;
  // Diagnostic command coverage/lifecycle does not establish ordinary save continuation.
  // Every surface needed by the resumed shell must have a confirmed response.
  if (!snapshot.menu.ok || !snapshot.session.ok || !snapshot.game.ok || !snapshot.settings.ok || !snapshot.audio.ok)
    return false;
  return Array.isArray(snapshot.menu.data?.saves)
    && snapshot.menu.data.saves.some(slot => slot?.saveId === createdSaveId);
}
