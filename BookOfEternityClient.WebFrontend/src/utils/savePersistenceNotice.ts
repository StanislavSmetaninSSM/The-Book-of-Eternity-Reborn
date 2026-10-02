import type { BrowserApiResult, BrowserCreateSaveResultDto } from '../api/contracts.js';

export interface SavePersistenceNotice {
  kind: 'saved' | 'follow-up' | 'rolled-back' | 'blocked' | 'uncertain';
  message: string;
  committed: boolean;
  continuationBlocked: boolean;
  shouldRefresh: boolean;
  createdSaveId: string;
}

// Compilable legacy behavior for the causal RED gate; truthful disposition handling follows it.
export function toSaveCreationNotice(result: BrowserApiResult<BrowserCreateSaveResultDto>): SavePersistenceNotice {
  const committed = result.ok && result.data.success;
  return {
    kind: committed ? 'saved' : 'blocked',
    message: committed ? 'Игра сохранена.' : 'Сохранение не удалось создать.',
    committed,
    continuationBlocked: false,
    shouldRefresh: committed,
    createdSaveId: result.ok ? result.data.createdSaveId : ''
  };
}
