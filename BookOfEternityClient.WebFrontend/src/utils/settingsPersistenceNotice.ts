import type { BrowserApiResult } from '../api/contracts.js';
import { toPlayerFacingText } from './playerCopy.js';

export type SettingsWriteNoticeKind = 'saved' | 'follow-up' | 'blocked' | 'rolled-back' | 'uncertain';
export interface SettingsWriteNotice { kind: SettingsWriteNoticeKind; message: string }
export interface SettingsPersistenceResponse { persistenceWarning?: string | null }
export interface SettingsWriteScope { generation: number }
export interface SettingsWriteNoticeTracker {
  begin(): number;
  invalidate(): void;
  isCurrent(request: number): boolean;
  canDispatch(request: number): boolean;
  resolve<T extends SettingsPersistenceResponse>(request: number, result: BrowserApiResult<T>, apply?: (notice: SettingsWriteNotice) => void): SettingsWriteNotice | null;
  interrupted(request: number, apply?: (notice: SettingsWriteNotice) => void): SettingsWriteNotice | null;
}

const savedMessage = 'Настройки сохранены в общей конфигурации книги.';
const followUpMessage = 'Настройки сохранены. Обновление интерфейса требует проверки.';
const uncertainMessage = 'Результат сохранения настроек не подтверждён. Обновите данные перед следующим изменением.';

export function createSettingsWriteNoticeTracker(scope: SettingsWriteScope = { generation: 0 }): SettingsWriteNoticeTracker {
  let current = 0;
  let invalidatedThrough = 0;
  let lastCommitted = 0;
  let committedAdvice = '';
  const isCurrent = (request: number) => request === current && request > invalidatedThrough;
  const applyCurrent = (request: number, notice: SettingsWriteNotice, apply?: (notice: SettingsWriteNotice) => void) => {
    if (!isCurrent(request)) return null;
    if (committedAdvice && notice.kind !== 'saved' && notice.kind !== 'follow-up') {
      notice = { ...notice, message: `${notice.message} Ранее: ${committedAdvice}` };
    }
    apply?.(notice);
    return notice;
  };

  return {
    begin: () => ++current,
    invalidate: () => { invalidatedThrough = ++current; },
    isCurrent,
    canDispatch: () => { throw new Error('Settings dispatch ownership is not implemented.'); },
    resolve(request, result, apply) {
      let notice: SettingsWriteNotice;
      if (result.ok) {
        const warning = result.data.persistenceWarning?.trim();
        notice = warning
          ? { kind: 'follow-up', message: toPlayerFacingText(warning, followUpMessage) }
          : { kind: 'saved', message: savedMessage };
      } else {
        const payload = result.payload;
        const status = payload && typeof payload === 'object' && 'persistenceStatus' in payload
          ? payload.persistenceStatus : undefined;
        switch (status) {
          case 'committed': notice = { kind: 'follow-up', message: followUpMessage }; break;
          case 'blocked': notice = { kind: 'blocked', message: 'Запись настроек заблокирована. Дождитесь завершения текущего действия и проверьте настройки.' }; break;
          case 'rolledback': notice = { kind: 'rolled-back', message: 'Изменение настроек отменено; прежние значения восстановлены.' }; break;
          default: notice = { kind: 'uncertain', message: uncertainMessage }; break;
        }
      }
      // Remember known commit advice even if a newer request is still pending, but
      // never let a duplicate older response undo a newer confirmed result.
      if (request > invalidatedThrough && request >= lastCommitted &&
          (notice.kind === 'saved' || notice.kind === 'follow-up')) {
        lastCommitted = request;
        committedAdvice = notice.kind === 'follow-up' ? notice.message : '';
      }
      return applyCurrent(request, notice, apply);
    },
    interrupted: (request, apply) => applyCurrent(request, { kind: 'uncertain', message: uncertainMessage }, apply)
  };
}

export function mergeSettingsPatch<T extends object>(pending: T, next: T): T {
  return { ...pending, ...next };
}
