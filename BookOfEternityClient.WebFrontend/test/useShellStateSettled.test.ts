import type { BrowserApiFailure, BrowserApiResult, BrowserMainMenuDto } from '../src/api/contracts.js';
import { settledToResult } from '../src/hooks/shellStateResult.js';

const rejectedRequest: PromiseSettledResult<BrowserApiResult<BrowserMainMenuDto>> = {
  status: 'rejected',
  reason: new Error('Socket closed')
};

const networkFailure = settledToResult(rejectedRequest);
if (networkFailure.ok) {
  throw new Error('Expected rejected shell request to become a BrowserApiFailure.');
}
if (networkFailure.kind !== 'network-error') {
  throw new Error(`Expected network-error fallback, got ${networkFailure.kind}.`);
}
if (networkFailure.message !== 'Socket closed') {
  throw new Error(`Expected rejection message to be preserved, got ${networkFailure.message}.`);
}
if (networkFailure.playerMessage !== 'Локальный игровой клиент сейчас недоступен.') {
  throw new Error(`Expected player-safe network fallback message, got ${networkFailure.playerMessage}.`);
}

const originalFailure: BrowserApiFailure = {
  ok: false,
  status: 503,
  kind: 'server-diagnostics',
  message: 'Server diagnostics',
  playerMessage: 'Сервер недоступен.',
  technicalDetails: 'stack trace'
};
const fulfilledRequest: PromiseSettledResult<BrowserApiResult<BrowserMainMenuDto>> = {
  status: 'fulfilled',
  value: originalFailure
};

const passthrough = settledToResult(fulfilledRequest);
if (passthrough !== originalFailure) {
  throw new Error('Expected fulfilled BrowserApiResult values to pass through unchanged.');
}
