import type { BrowserApiClient } from '../api/client.js';
import type { BrowserShellState } from '../context/ShellContext.js';
import { settledToResult } from './shellStateResult.js';

export interface ShellRefreshOwner { current: number }
export type PublishShellState = (state: BrowserShellState | ((previous: BrowserShellState) => BrowserShellState)) => void;

// Test-first extraction of the existing refresh; shared publication ordering is not yet implemented.
export async function loadShellState(
  browserApi: BrowserApiClient,
  setShellState: PublishShellState,
  publicationOwner: ShellRefreshOwner,
  advancedEnabled: boolean,
  isCurrent: () => boolean = () => true
): Promise<void> {
  if (!isCurrent()) return;
  setShellState((prev) => prev.status === 'ready' ? prev : { status: 'loading' });

  const results = await Promise.allSettled([
    browserApi.getMainMenu(),
    browserApi.getSessionStatus(),
    browserApi.getGameScreen(),
    browserApi.getAudioSettings(),
    browserApi.getClientSettings(),
    browserApi.getCommandCoverage()
  ]);

  if (!isCurrent()) return;
  const menu = settledToResult(results[0]);
  const session = settledToResult(results[1]);
  const game = settledToResult(results[2]);
  const audio = settledToResult(results[3]);
  const settings = settledToResult(results[4]);
  const commandCoverage = settledToResult(results[5]);

  const allFailed = [menu, session, game, audio, settings, commandCoverage].every((result) => !result.ok && result.kind === 'network-error');

  if (allFailed) {
    setShellState({
      status: 'error',
      playerMessage: 'Локальная книга недоступна. Убедитесь, что игра запущена.',
      technicalDetails: !menu.ok ? menu.message : 'Запросы к книге не ответили.'
    });
    return;
  }

  const anyNetworkFailed = [menu, session, game, audio, settings, commandCoverage].some((result) => !result.ok && result.kind === 'network-error');

  let lifecycle = null;

  if (advancedEnabled) {
    const advResults = await Promise.allSettled([
      browserApi.getLifecycleDashboard()
    ]);
    lifecycle = settledToResult(advResults[0]);
  }

  if (!isCurrent()) return;
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
}
