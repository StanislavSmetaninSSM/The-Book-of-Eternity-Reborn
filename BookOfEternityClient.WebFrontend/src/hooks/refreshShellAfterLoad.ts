import type { BrowserApiClient } from '../api/client';
import type { BrowserApiFailure } from '../api/contracts';
import type { PublishShellState, ShellRefreshOwner } from './loadShellState';

/** Publishes one complete, exact-generation bundle. Current capture is reserved for known non-loading. */
export async function refreshShellAfterLoad(
  api: BrowserApiClient,
  publish: PublishShellState,
  publicationOwner: ShellRefreshOwner,
  generation: string | null,
  requestIsCurrent: () => boolean,
  allowNoActiveSession = false
): Promise<boolean> {
  if (!requestIsCurrent() || generation === '') return false;
  const owner = ++publicationOwner.current;
  const isCurrent = () => owner === publicationOwner.current && requestIsCurrent();
  const result = await api.getLoadState({ establishedGeneration: generation, reconcileCurrent: generation === null });
  if (!isCurrent() || !result.ok) return false;
  const bundle = result.data;
  if (!bundle || typeof bundle.establishedGeneration !== 'string' || !bundle.establishedGeneration.trim()
    || (generation !== null && bundle.establishedGeneration !== generation)
    || !bundle.menu || !bundle.session || !bundle.settings || !bundle.audio
    || (bundle.game === null ? !(allowNoActiveSession && bundle.noActiveSession === true) : !bundle.game || bundle.noActiveSession !== false))
    return false;
  const ok = <T,>(data: T) => ({ ok: true as const, status: result.status, data });
  const absent: BrowserApiFailure = { ok: false, status: 404, kind: 'no-active-session',
    message: 'Активная глава не найдена.', playerMessage: 'Активная глава не найдена.' };
  publish({ status: 'ready', connectionStatus: 'connected', menu: ok(bundle.menu), session: ok(bundle.session),
    game: bundle.game ? ok(bundle.game) : absent, settings: ok(bundle.settings), audio: ok(bundle.audio),
    lifecycle: null, commandCoverage: null });
  return isCurrent();
}
