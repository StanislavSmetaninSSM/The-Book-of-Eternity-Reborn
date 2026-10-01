import { loadShellState } from '../src/hooks/loadShellState.js';
import type { BrowserShellState } from '../src/context/ShellContext.js';
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

await scenario('an earlier commit warning remains relevant while the latest request is uncertain', () => {
  const tracker = createSettingsWriteNoticeTracker(); const earlier = tracker.begin(); const latest = tracker.begin();
  assert(tracker.resolve(earlier, success(warning)) === null, 'Older response must not own the UI.');
  const notice = tracker.interrupted(latest);
  assert(notice?.kind === 'uncertain' && notice.message.includes(warning), 'Pending response ordering discarded known committed follow-up.');
});

await scenario('duplicate older completion cannot restore advice cleared by a later clean commit', () => {
  const tracker = createSettingsWriteNoticeTracker(); const earlier = tracker.begin();
  tracker.resolve(earlier, success(warning)); const latest = tracker.begin(); tracker.resolve(latest, success());
  tracker.resolve(earlier, success(warning));
  assert(!tracker.interrupted(tracker.begin())?.message.includes(warning), 'A duplicate old response resurrected cleared advice.');
});

await scenario('stale and unmounted responses cannot apply state or start refresh side effects', () => {
  const tracker = createSettingsWriteNoticeTracker(); const old = tracker.begin(); const latest = tracker.begin();
  let state = 'newer draft'; let refreshes = 0;
  tracker.resolve(old, success(), () => { state = 'stale'; refreshes++; });
  assert(state === 'newer draft' && refreshes === 0, 'Stale response still mutated state or refreshed.');
  tracker.invalidate();
  tracker.interrupted(latest, () => { state = 'unmounted'; refreshes++; });
  assert(state === 'newer draft' && refreshes === 0, 'Unmounted rejection still applied side effects.');
});

await scenario('current response applies once and a delayed refresh loses ownership after a new request', () => {
  const tracker = createSettingsWriteNoticeTracker(); const request = tracker.begin(); let applied = 0;
  tracker.resolve(request, success(), () => { applied++; });
  assert(applied === 1 && tracker.isCurrent(request), 'Current response was not applied.');
  const refreshIsCurrent = () => tracker.isCurrent(request); tracker.begin();
  assert(!refreshIsCurrent(), 'A refresh still owned publication after a newer request.');
});

await scenario('unmount cancels unsent dispatch but superseded same-mount changes remain admitted', () => {
  const tracker = createSettingsWriteNoticeTracker(); const older = tracker.begin(); const newer = tracker.begin();
  assert(tracker.canDispatch(older) && tracker.canDispatch(newer), 'A superseded independent queued patch was dropped.');
  tracker.invalidate();
  assert(!tracker.canDispatch(older) && !tracker.canDispatch(newer), 'An unsent POST can still start after unmount.');
});

await scenario('save-load boundary invalidates both settings and audio dispatch and response ownership', () => {
  const scope = { generation: 0 };
  const settings = createSettingsWriteNoticeTracker(scope); const audio = createSettingsWriteNoticeTracker(scope);
  const oldSetting = settings.begin(); const oldAudio = audio.begin(); scope.generation++;
  assert(!settings.canDispatch(oldSetting) && !audio.canDispatch(oldAudio), 'Old-view writes can start in the replacement session.');
  assert(!settings.isCurrent(oldSetting) && !audio.isCurrent(oldAudio), 'Old-session responses can change the new view.');
  const newSetting = settings.begin();
  assert(settings.canDispatch(newSetting) && !settings.canDispatch(oldSetting), 'Starting in the new scope resurrected old queued work.');
});

await scenario('two locally current settings/audio refreshes publish only the newest shared snapshot', async () => {
  let version = 0;
  const gates = [0, 1].map(() => { let release!: () => void; const promise = new Promise<void>((resolve) => { release = resolve; }); return { promise, release }; });
  const client = createBrowserApiClient({ fetcher: async () => {
    const captured = version; await gates[captured].promise;
    return new Response(JSON.stringify({ tag: captured }), { status: 200, headers: { 'Content-Type': 'application/json' } });
  } });
  let state: BrowserShellState = { status: 'loading' };
  const publish = (next: BrowserShellState | ((previous: BrowserShellState) => BrowserShellState)) => { state = typeof next === 'function' ? next(state) : next; };
  const owner = { current: 0 };
  const older = loadShellState(client, publish, owner, false, () => true);
  version = 1;
  const newer = loadShellState(client, publish, owner, false, () => true);
  gates[1].release(); await newer;
  gates[0].release(); await older;
  const actual = state as BrowserShellState;
  assert(actual.status === 'ready' && actual.settings.ok && (actual.settings.data as unknown as { tag: number }).tag === 1,
    'Slower older refresh overwrote the newer other-flow snapshot.');
});

await scenario('actual consumers fence dispatch and cancel settings before save loading', async () => {
  const fsSpecifier = 'node:fs'; const pathSpecifier = 'node:path';
  const { readFileSync } = await import(fsSpecifier); const { join, basename } = await import(pathSpecifier);
  const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
  const root = basename(cwd) === 'BookOfEternityClient.WebFrontend' ? cwd : join(cwd, 'BookOfEternityClient.WebFrontend');
  for (const file of ['SettingsView.tsx', 'AudioPanel.tsx']) {
    const source = readFileSync(join(root, 'src', 'components', file), 'utf8');
    assert(source.includes('.canDispatch('), `${file} does not fence the actual queued POST dispatch.`);
  }
  const settings = readFileSync(join(root, 'src', 'components', 'SettingsView.tsx'), 'utf8');
  const load = settings.slice(settings.indexOf('async function loadSaveSlot'), settings.indexOf('if (!settings)'));
  assert(load.indexOf('invalidatePendingSettings()') >= 0 && load.indexOf('invalidatePendingSettings()') < load.indexOf('await browserApi.loadSave'), 'Save load begins before pending settings lose ownership.');
  assert(settings.includes('clearTimeout(updateQueue.current)') && settings.includes('writeScope.current.generation++'), 'Unmount/load does not invalidate the shared settings/audio scope.');
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
    assert(source.includes('persistenceTracker.current.resolve') && source.includes('persistenceTracker.current.interrupted'), `${file} bypasses guarded response application.`);
    assert(source.includes('.invalidate()'), `${file} does not invalidate response ownership on unmount.`);
  }
});

await scenario('frontend build resolves the platform-specific npm application', async () => {
  const fsSpecifier = 'node:fs'; const pathSpecifier = 'node:path';
  const { readFileSync } = await import(fsSpecifier); const { join, basename, dirname } = await import(pathSpecifier);
  const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
  const root = basename(cwd) === 'BookOfEternityClient.WebFrontend' ? dirname(cwd) : cwd;
  const source = readFileSync(join(root, 'scripts', 'test-csharp.ps1'), 'utf8');
  const resolver = source.slice(source.indexOf('function Resolve-NpmCommandPath {'), source.indexOf('function New-OwnedProcessContainment {'));
  assert(resolver.includes('$npmName = if ($IsWindows) { "npm.cmd" } else { "npm" }'), 'Windows/Linux npm application selection regressed.');
  assert(resolver.includes('Get-Command -Name $npmName -CommandType Application'), 'Resolver does not use the selected application name.');
});

await scenario('shared refresh checks optional owner after asynchronous reads before publication', async () => {
  const fsSpecifier = 'node:fs'; const pathSpecifier = 'node:path';
  const { readFileSync } = await import(fsSpecifier); const { join, basename } = await import(pathSpecifier);
  const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
  const root = basename(cwd) === 'BookOfEternityClient.WebFrontend' ? cwd : join(cwd, 'BookOfEternityClient.WebFrontend');
  const hook = readFileSync(join(root, 'src', 'hooks', 'useShellState.ts'), 'utf8');
  const source = readFileSync(join(root, 'src', 'hooks', 'loadShellState.ts'), 'utf8');
  assert(hook.includes('const publicationOwner = useRef(0)') && hook.includes('return loadShellState(browserApi, setShellState, publicationOwner, advancedEnabled, isCurrent)'), 'Hook does not use the tested shared publication owner.');
  const context = readFileSync(join(root, 'src', 'context', 'ShellContext.tsx'), 'utf8');
  assert(context.includes('loadBrowserState: (isCurrent?: () => boolean) => Promise<void>'), 'Context type drops the optional refresh owner.');
  assert(context.includes('const { shellState, loadBrowserState } = useShellState(advancedEnabled);') &&
    context.includes('    loadBrowserState\n  }),'), 'Context no longer directly forwards the owned refresh function.');
  assert(hook.includes('isCurrent: () => boolean = () => true'), 'Refresh lacks optional response ownership.');
  const readIndex = source.indexOf('const results = await Promise.allSettled');
  const errorIndex = source.indexOf("status: 'error'");
  assert(source.slice(readIndex, errorIndex).includes('if (!isCurrent()) return;'), 'Error publication is not fenced after reads.');
  const advancedRead = source.indexOf('const advResults = await Promise.allSettled');
  const readyPublication = source.indexOf("status: 'ready'", advancedRead);
  assert(source.slice(advancedRead, readyPublication).includes('if (!isCurrent()) return;'), 'Ready publication is not fenced after advanced reads.');
});

console.log(`Settings persistence scenarios: passed=${passed} failed=${failures.length}`);
if (failures.length) throw new Error(failures.join('\n'));
