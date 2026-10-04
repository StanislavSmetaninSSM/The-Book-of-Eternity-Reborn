import type { BrowserApiResult, BrowserLoadSaveResultDto } from '../api/contracts';

export interface LoadPersistenceNotice {
  disposition: 'NotLoaded' | 'Committed' | 'RolledBack' | 'Uncertain';
  message: string;
  loadedSaveId: string;
  selectedSourcePath: string | null;
  establishedGeneration: string | null;
  needsFollowUp: boolean;
  continuationBlocked: boolean;
}

/** Retains the typed decision independently of HTTP status; a missing reply never proves rollback. */
export function toLoadNotice(result: BrowserApiResult<BrowserLoadSaveResultDto> | undefined): LoadPersistenceNotice {
  const payload: unknown = result?.ok ? result.data : result?.payload;
  const data = payload && typeof payload === 'object' && !Array.isArray(payload)
    ? payload as Record<string, unknown> : {};
  const disposition = ['NotLoaded', 'Committed', 'RolledBack', 'Uncertain'].includes(String(data.disposition))
    ? data.disposition as LoadPersistenceNotice['disposition'] : 'Uncertain';
  const text = (value: unknown) => typeof value === 'string' && value.trim() ? value : null;
  const committed = disposition === 'Committed';
  const loadedSaveId = committed ? text(data.loadedSaveId) ?? '' : '';
  const establishedGeneration = disposition === 'Uncertain' ? null : text(data.establishedGeneration);
  const selectedSourcePath = text(data.selectedSourcePath);
  const continuationBlocked = disposition === 'Uncertain' || data.continuationBlocked !== false
    || typeof data.needsFollowUp !== 'boolean'
    || (committed && (!loadedSaveId || !establishedGeneration || !selectedSourcePath));
  return {
    disposition, loadedSaveId, establishedGeneration, selectedSourcePath,
    needsFollowUp: continuationBlocked || data.needsFollowUp === true,
    continuationBlocked,
    message: (committed ? 'Сохранение загружено.'
      : disposition === 'RolledBack' ? 'Загрузка отменена: прежнее состояние книги восстановлено.'
        : disposition === 'NotLoaded' ? 'Сохранение не загружено.'
          : 'Состояние загрузки не подтверждено. Продолжение остановлено до восстановления книги.')
      + (data.needsFollowUp === true ? ' Служебное завершение операции требует проверки.' : '')
  };
}

/** Executes both consuming handlers; the global stop is independent of stale component ownership. */
export async function executeBrowserLoad(
  load: () => Promise<BrowserApiResult<BrowserLoadSaveResultDto>>,
  isCurrent: () => boolean,
  apply: (notice: LoadPersistenceNotice) => void,
  block: (notice: LoadPersistenceNotice) => void,
  refresh: (generation: string | null, allowNoActiveSession: boolean) => Promise<boolean>,
  navigate: () => void,
  retain: (notice: LoadPersistenceNotice) => void = () => {}
): Promise<LoadPersistenceNotice> {
  let result: BrowserApiResult<BrowserLoadSaveResultDto> | undefined;
  try { result = await load(); } catch { /* A dispatched request can commit without its response. */ }
  let notice = toLoadNotice(result);
  retain(notice);
  const publish = () => { if (isCurrent()) apply(notice); };
  const stop = () => {
    notice = { ...notice, needsFollowUp: true, continuationBlocked: true,
      message: `${notice.message} Обновление текущего состояния не подтверждено. Продолжение остановлено до проверки книги.` };
    retain(notice); publish(); block(notice);
  };
  publish();
  if (notice.continuationBlocked) { block(notice); return notice; }
  // Safe non-loading has no replacement to publish into a view which no longer owns it.
  if (notice.disposition === 'NotLoaded' && !isCurrent()) return notice;
  if (!isCurrent() || (notice.disposition === 'RolledBack' && !notice.establishedGeneration)) {
    stop(); return notice;
  }
  let confirmed = false;
  try { confirmed = await refresh(notice.disposition === 'NotLoaded' ? null : notice.establishedGeneration,
    notice.disposition !== 'Committed'); } catch { /* Preserve the established decision. */ }
  if (!confirmed || !isCurrent()) { stop(); return notice; }
  if (notice.disposition === 'Committed') navigate();
  return notice;
}

export interface BrowserLoadOwner { readonly sequence: number; readonly navigation: number }

/** Owns admission and interruption evidence across route/component lifetimes. */
export function createBrowserLoadController() {
  let sequence = 0; let navigation = 0;
  let active: BrowserLoadOwner | null = null;
  let blocked: LoadPersistenceNotice | null = null;
  return {
    begin(): BrowserLoadOwner | null {
      if (active || blocked) return null;
      active = { sequence: ++sequence, navigation }; return active;
    },
    isCurrent: (owner: BrowserLoadOwner) => active === owner && owner.navigation === navigation && !blocked,
    navigate: () => { navigation++; },
    finish: (owner: BrowserLoadOwner) => { if (active === owner) active = null; },
    isInFlight: () => active !== null,
    isBlocked: () => blocked !== null,
    block(notice: LoadPersistenceNotice): LoadPersistenceNotice | null {
      if (notice.continuationBlocked && !blocked) blocked = notice;
      return blocked;
    }
  };
}
