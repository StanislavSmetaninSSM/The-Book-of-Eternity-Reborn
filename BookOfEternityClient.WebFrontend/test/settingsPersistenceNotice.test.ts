import { createBrowserApiClient } from '../src/api/client.js';
import type { BrowserApiFailure, BrowserApiResult } from '../src/api/contracts.js';
import { createSettingsWriteNoticeTracker, mergeSettingsPatch } from '../src/utils/settingsPersistenceNotice.js';
import type { SettingsPersistenceResponse } from '../src/utils/settingsPersistenceNotice.js';

const failures: string[] = [];
let passed = 0;
function assert(condition: unknown, message: string): asserts condition {
  if (!condition) throw new Error(message);
}
async function scenario(name: string, check: () => void | Promise<void>) {
  try { await check(); passed++; console.log(`PASS ${name}`); }
  catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    failures.push(`${name}: ${message}`); console.error(`FAIL ${name}: ${message}`);
  }
}
const warning = 'Настройки сохранены. Обновление интерфейса требует проверки.';
const success = (persistenceWarning?: string): BrowserApiResult<SettingsPersistenceResponse> =>
  ({ ok: true, status: 200, data: { persistenceWarning } });
const failure = (persistenceStatus: string): BrowserApiFailure => ({
  ok: false, status: 409, kind: 'http-error', message: 'Не удалось завершить запрос настроек.',
  playerMessage: 'Запрос требует внимания.', payload: { persistenceStatus }
});

await scenario('HTTP client preserves committed follow-up metadata', async () => {
  const client = createBrowserApiClient({ fetcher: async () => new Response(JSON.stringify({ persistenceWarning: warning }),
    { status: 200, headers: { 'Content-Type': 'application/json' } }) });
  const result = await client.updateClientSettings({ language: 'en' });
  assert(result.ok && result.data.persistenceWarning === warning, 'The API client dropped committed warning metadata.');
});

await scenario('committed warning is visible and survives ordinary refresh handling', () => {
  const tracker = createSettingsWriteNoticeTracker();
  const notice = tracker.resolve(tracker.begin(), success(warning));
  assert(notice?.kind === 'follow-up' && notice.message.includes(warning), 'A committed warning must stay explicitly persisted.');
  // GET refresh does not resolve a write request or clear the notice.
  assert(notice.message.includes('сохранены'), 'Refresh must not relabel committed settings as rolled back.');
});

await scenario('a later clean update deliberately clears the previous warning', () => {
  const tracker = createSettingsWriteNoticeTracker(); tracker.resolve(tracker.begin(), success(warning));
  const clean = tracker.resolve(tracker.begin(), success());
  assert(clean?.kind === 'saved' && !clean.message.includes(warning), 'A clean later commit should replace stale follow-up advice.');
});

await scenario('stale clean response cannot erase a newer committed warning', () => {
  const tracker = createSettingsWriteNoticeTracker(); const old = tracker.begin(); const current = tracker.begin();
  const notice = tracker.resolve(current, success(warning));
  assert(notice?.kind === 'follow-up', 'The current warning was lost.');
  assert(tracker.resolve(old, success()) === null, 'An older response must not own the current notice.');
});

await scenario('stale warning response cannot replace a newer clean result', () => {
  const tracker = createSettingsWriteNoticeTracker(); const old = tracker.begin(); const current = tracker.begin();
  assert(tracker.resolve(current, success())?.kind === 'saved', 'Clean response was not accepted.');
  assert(tracker.resolve(old, success(warning)) === null, 'An older warning must not overwrite a later confirmed result.');
});

await scenario('unmounted or invalidated response ownership cannot update the notice', () => {
  const tracker = createSettingsWriteNoticeTracker(); const request = tracker.begin(); tracker.invalidate();
  assert(tracker.resolve(request, success(warning)) === null, 'An invalidated response still owned UI state.');
  assert(tracker.interrupted(request) === null, 'An invalidated rejection still owned UI state.');
});

await scenario('a lost response is uncertain rather than a rollback claim', () => {
  const tracker = createSettingsWriteNoticeTracker(); const notice = tracker.interrupted(tracker.begin());
  assert(notice?.kind === 'uncertain', 'A missing response cannot establish whether the server committed.');
  assert(!notice.message.includes('не сохранены'), 'Network interruption incorrectly claimed the settings were not saved.');
});

await scenario('server uncertainty remains explicit and retains prior committed advice', () => {
  const tracker = createSettingsWriteNoticeTracker(); tracker.resolve(tracker.begin(), success(warning));
  const notice = tracker.resolve(tracker.begin(), failure('uncertain'));
  assert(notice?.kind === 'uncertain' && notice.message.includes(warning), 'An uncertain update silently discarded previous committed follow-up.');
});

await scenario('blocked and rolled-back outcomes are distinguishable', () => {
  const tracker = createSettingsWriteNoticeTracker();
  assert(tracker.resolve(tracker.begin(), failure('blocked'))?.kind === 'blocked', 'Admission block lost its outcome.');
  assert(tracker.resolve(tracker.begin(), failure('rolledback'))?.kind === 'rolled-back', 'Confirmed rollback lost its outcome.');
});

await scenario('a committed failure-shaped response still reports persisted follow-up', () => {
  const tracker = createSettingsWriteNoticeTracker();
  assert(tracker.resolve(tracker.begin(), failure('committed'))?.kind === 'follow-up', 'HTTP failure shape overrode explicit commit authority.');
});

await scenario('rapid independent settings changes coalesce without losing fields', () => {
  const merged = mergeSettingsPatch<{ language?: string; difficulty?: string }>({ language: 'en' }, { difficulty: 'hard' });
  assert(merged.language === 'en' && merged.difficulty === 'hard', 'Debounce lost an independent settings change.');
  assert(mergeSettingsPatch(merged, { language: 'ru' }).language === 'ru', 'The last value for one field did not win.');
});

await scenario('both actual settings consumers render and own persistence notices', async () => {
  const fsSpecifier = 'node:fs'; const pathSpecifier = 'node:path';
  const { readFileSync } = await import(fsSpecifier); const { join, basename } = await import(pathSpecifier);
  const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
  const root = basename(cwd) === 'BookOfEternityClient.WebFrontend' ? cwd : join(cwd, 'BookOfEternityClient.WebFrontend');
  for (const file of ['SettingsView.tsx', 'AudioPanel.tsx']) {
    const source = readFileSync(join(root, 'src', 'components', file), 'utf8');
    assert(source.includes('createSettingsWriteNoticeTracker'), `${file} does not use the checked response-owner logic.`);
    assert(source.includes('persistenceNotice.message') && source.includes('role="status"'), `${file} does not visibly render the persistence notice.`);
    assert(source.includes('.invalidate()'), `${file} does not invalidate response ownership on unmount.`);
  }
});

console.log(`Settings persistence scenarios: passed=${passed} failed=${failures.length}`);
if (failures.length) throw new Error(failures.join('\n'));
