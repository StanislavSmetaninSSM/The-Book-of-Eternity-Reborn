import type { BrowserApiResult } from '../api/contracts.js';

export type SettingsWriteNoticeKind = 'saved' | 'follow-up' | 'blocked' | 'rolled-back' | 'uncertain';
export interface SettingsWriteNotice { kind: SettingsWriteNoticeKind; message: string }
export interface SettingsPersistenceResponse { persistenceWarning?: string | null }
export interface SettingsWriteNoticeTracker {
  begin(): number;
  invalidate(): void;
  isCurrent(request: number): boolean;
  resolve<T extends SettingsPersistenceResponse>(request: number, result: BrowserApiResult<T>, apply?: (notice: SettingsWriteNotice) => void): SettingsWriteNotice | null;
  interrupted(request: number, apply?: (notice: SettingsWriteNotice) => void): SettingsWriteNotice | null;
}

export function createSettingsWriteNoticeTracker(): SettingsWriteNoticeTracker {
  throw new Error('Prepared settings notice handling is not implemented.');
}

export function mergeSettingsPatch<T extends object>(pending: T, next: T): T {
  throw new Error('Settings patch coalescing is not implemented.');
}
