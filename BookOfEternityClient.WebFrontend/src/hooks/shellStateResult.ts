import type { BrowserApiFailure, BrowserApiResult } from '../api/contracts';

export function settledToResult<T>(outcome: PromiseSettledResult<BrowserApiResult<T>>): BrowserApiResult<T> {
  if (outcome.status === 'fulfilled') {
    return outcome.value;
  }

  const message = outcome.reason instanceof Error ? outcome.reason.message : 'Network request failed.';
  return {
    ok: false,
    status: null,
    kind: 'network-error',
    message,
    playerMessage: 'Локальный игровой клиент сейчас недоступен.',
    technicalDetails: message
  } satisfies BrowserApiFailure;
}
