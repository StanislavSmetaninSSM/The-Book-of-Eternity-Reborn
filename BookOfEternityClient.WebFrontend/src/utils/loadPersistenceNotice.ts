import type { BrowserApiResult, BrowserLoadSaveResultDto, BrowserLoadStateDto } from '../api/contracts';

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
  refresh: (generation: string | null, allowNoActiveSession: boolean, state: BrowserLoadStateDto | null) => Promise<boolean>,
  navigate: () => void,
  retain: (notice: LoadPersistenceNotice) => void = () => {},
  lifecycle?: { operationId: string; complete: (generation: string | null) => Promise<BrowserApiResult<BrowserLoadSaveResultDto>>; cancel: (generation: string | null) => Promise<unknown> }
): Promise<LoadPersistenceNotice> {
  let result: BrowserApiResult<BrowserLoadSaveResultDto> | undefined;
  try { result = await load(); } catch { /* A dispatched request can commit without its response. */ }
  let notice = toLoadNotice(result);
  const data = result?.ok ? result.data : result?.payload as BrowserLoadSaveResultDto | undefined;
  const abandon = async () => { if (lifecycle) try { await lifecycle.cancel(notice.establishedGeneration); } catch { /* No retry. */ } };
  retain(notice);
  const publish = () => { if (isCurrent()) apply(notice); };
  const stop = (reason = 'Обновление текущего состояния не подтверждено.') => {
    notice = { ...notice, needsFollowUp: true, continuationBlocked: true,
      message: `${notice.message} ${reason} Продолжение остановлено до проверки книги.` };
    retain(notice); publish(); block(notice);
  };
  publish();
  if (notice.continuationBlocked) { await abandon(); block(notice); return notice; }
  // Safe non-loading has no replacement to publish into a view which no longer owns it.
  if (notice.disposition === 'NotLoaded' && !isCurrent()) { await abandon(); return notice; }
  if (!isCurrent() || (notice.disposition === 'RolledBack' && !notice.establishedGeneration)) {
    await abandon(); stop(); return notice;
  }
  let confirmed = false;
  try { confirmed = await refresh(notice.disposition === 'NotLoaded' ? null : notice.establishedGeneration,
    notice.disposition !== 'Committed', ((result?.ok ? result.data : result?.payload) as BrowserLoadSaveResultDto | undefined)?.state ?? null); } catch { /* Preserve the established decision. */ }
  if (!confirmed || !isCurrent()) { await abandon(); stop(); return notice; }
  if (data?.freshLaunchRequired === true) {
    if (!lifecycle || data.lifecycleOperationId !== lifecycle.operationId || notice.disposition !== 'Committed' || !data.state) {
      await abandon(); stop(); return notice;
    }
    let completed: BrowserApiResult<BrowserLoadSaveResultDto> | undefined;
    try { completed = await lifecycle.complete(notice.establishedGeneration); } catch { /* Preserve Committed. */ }
    const fresh = completed?.ok ? completed.data : completed?.payload as BrowserLoadSaveResultDto | undefined;
    const completedNotice = toLoadNotice(completed);
    if (!fresh || fresh.lifecycleOperationId !== lifecycle.operationId || fresh.disposition !== notice.disposition ||
        fresh.establishedGeneration !== notice.establishedGeneration || fresh.freshLaunchRequired !== false ||
        fresh.mainSessionState !== 'Running' || completedNotice.continuationBlocked ||
        completedNotice.loadedSaveId !== notice.loadedSaveId || completedNotice.selectedSourcePath !== notice.selectedSourcePath || !isCurrent()) {
      stop('Новая сессия ГМа не подтверждена; неизвестная команда не повторяется.'); return notice;
    }
    notice = completedNotice; retain(notice); publish();
  }
  if (notice.disposition === 'Committed') navigate();
  return notice;
}

export interface BrowserLoadOwner { readonly sequence: number; readonly navigation: number; readonly operationId: string }

/** Owns admission and interruption evidence across route/component lifetimes. */
export function createBrowserLoadController() {
  let sequence = 0; let navigation = 0;
  let active: BrowserLoadOwner | null = null;
  let blocked: LoadPersistenceNotice | null = null;
  return {
    begin(): BrowserLoadOwner | null {
      if (active || blocked) return null;
      active = { sequence: ++sequence, navigation, operationId: crypto.randomUUID().replaceAll('-', '') }; return active;
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
