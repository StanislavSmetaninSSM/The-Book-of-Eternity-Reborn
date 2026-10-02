import type { BrowserApiResult, BrowserCreateSaveResultDto } from '../api/contracts.js';

export interface SavePersistenceNotice {
  kind: 'saved' | 'follow-up' | 'rolled-back' | 'blocked' | 'uncertain';
  message: string;
  committed: boolean;
  continuationBlocked: boolean;
  shouldRefresh: boolean;
  createdSaveId: string;
}

/** Keeps a save continuation stop for the lifetime of the shell, including route changes. */
export function createSaveContinuationLatch() {
  let blockedNotice: SavePersistenceNotice | null = null;
  return {
    isBlocked: () => blockedNotice !== null,
    block(notice: SavePersistenceNotice): SavePersistenceNotice | null {
      if (notice.continuationBlocked && blockedNotice === null) blockedNotice = notice;
      return blockedNotice;
    },
    async runIfAllowed<T>(operation: () => Promise<T>): Promise<T | undefined> {
      if (blockedNotice !== null) return undefined;
      return operation();
    }
  };
}

/** Projects the explicit archive decision even when its response arrived at an HTTP error status. */
export function toSaveCreationNotice(result: BrowserApiResult<BrowserCreateSaveResultDto> | undefined): SavePersistenceNotice {
  const payload: unknown = result?.ok ? result.data : result?.payload;
  const fields = payload !== null && typeof payload === 'object' && !Array.isArray(payload) ? payload : null;
  const disposition = fields && 'disposition' in fields ? fields.disposition : undefined;
  const committed = disposition === 'Committed';
  const createdSaveId = committed && fields && 'createdSaveId' in fields && typeof fields.createdSaveId === 'string'
    ? fields.createdSaveId : '';
  const explicitFlags = fields && 'needsFollowUp' in fields && typeof fields.needsFollowUp === 'boolean'
    && 'continuationBlocked' in fields && typeof fields.continuationBlocked === 'boolean';
  const uncertain = !['Committed', 'RolledBack', 'Blocked'].includes(String(disposition));
  const continuationBlocked = uncertain || !explicitFlags || (fields && 'continuationBlocked' in fields && fields.continuationBlocked === true)
    || (committed && !createdSaveId);
  const followUp = continuationBlocked || !result?.ok || (fields && 'needsFollowUp' in fields && fields.needsFollowUp === true);
  const kind = committed ? followUp ? 'follow-up' : 'saved'
    : disposition === 'RolledBack' ? 'rolled-back' : disposition === 'Blocked' ? 'blocked' : 'uncertain';
  return {
    kind,
    message: committed
      ? followUp ? 'Сохранение создано. Служебная очистка или обновление интерфейса требуют проверки.' : 'Игра сохранена.'
      : disposition === 'RolledBack' ? 'Сохранение не создано: предыдущие файлы восстановлены.'
        : disposition === 'Blocked' ? 'Сохранение не создано: подготовка или локальное право записи заблокированы.'
          : 'Состояние сохранения не подтверждено. Проверьте локальное хранилище перед следующим действием.',
    committed,
    continuationBlocked: Boolean(continuationBlocked),
    shouldRefresh: committed && !continuationBlocked,
    createdSaveId
  };
}

/** Applies a save decision before refresh and preserves known commit if that follow-up fails. */
export async function applySaveCreationNotice(
  result: BrowserApiResult<BrowserCreateSaveResultDto> | undefined,
  apply: (notice: SavePersistenceNotice) => void,
  blockContinuation: (notice: SavePersistenceNotice) => void,
  refresh: () => Promise<void>
): Promise<SavePersistenceNotice> {
  let notice = toSaveCreationNotice(result);
  apply(notice);
  if (notice.continuationBlocked) blockContinuation(notice);
  if (notice.shouldRefresh) {
    try { await refresh(); }
    catch {
      notice = { ...notice, kind: 'follow-up', continuationBlocked: true, shouldRefresh: false,
        message: 'Сохранение создано, но обновление текущего интерфейса не подтверждено. Проверьте состояние книги перед следующим действием.' };
      apply(notice);
      blockContinuation(notice);
    }
  }
  return notice;
}

/** Runs the consuming save handler without turning a lost response into a known rollback. */
export async function executeBrowserSaveCreation(
  createSave: () => Promise<BrowserApiResult<BrowserCreateSaveResultDto>>,
  apply: (notice: SavePersistenceNotice) => void,
  blockContinuation: (notice: SavePersistenceNotice) => void,
  refresh: () => Promise<void>
): Promise<SavePersistenceNotice> {
  let result: BrowserApiResult<BrowserCreateSaveResultDto> | undefined;
  try { result = await createSave(); }
  catch { /* A lost response cannot establish non-creation or rollback. */ }
  return applySaveCreationNotice(result, apply, blockContinuation, refresh);
}
