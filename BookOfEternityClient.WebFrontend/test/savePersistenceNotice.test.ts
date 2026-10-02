import { createBrowserApiClient } from '../src/api/client.js';
import { createSaveContinuationLatch, executeBrowserSaveCreation, toSaveCreationNotice } from '../src/utils/savePersistenceNotice.js';

const failures: string[] = [];
let passed = 0;
function assert(condition: unknown, message: string): asserts condition {
  if (!condition) throw new Error(message);
}
async function scenario(name: string, check: () => Promise<void>) {
  try { await check(); passed++; console.log(`PASS ${name}`); }
  catch (error) { failures.push(`${name}: ${String(error)}`); console.error(`FAIL ${name}: ${String(error)}`); }
}
async function response(disposition: string, status: number, needsFollowUp = false, continuationBlocked = false) {
  const payload = { success: disposition === 'Committed', error: 'Служебная проверка требуется.',
    createdSaveId: disposition === 'Committed' ? 'manual:exact-created.zip' : '', menu: null,
    disposition, needsFollowUp, continuationBlocked };
  const client = createBrowserApiClient({ fetcher: async () => new Response(JSON.stringify(payload),
    { status, headers: { 'Content-Type': 'application/json' } }) });
  return client.createSave({ saveName: 'same-name' });
}

await scenario('committed follow-up survives the success transport and retains exact identity', async () => {
  const notice = toSaveCreationNotice(await response('Committed', 200, true));
  assert(notice.committed && notice.kind === 'follow-up', 'Committed follow-up became an ordinary success or failure.');
  assert(notice.createdSaveId === 'manual:exact-created.zip', 'Exact destination identity was dropped.');
});
await scenario('committed decision in an HTTP failure remains committed and blocks continuation', async () => {
  const notice = toSaveCreationNotice(await response('Committed', 409, true, true));
  assert(notice.committed && notice.kind === 'follow-up', 'The HTTP failure erased an established commit.');
  assert(notice.continuationBlocked && !notice.shouldRefresh, 'An unresolved committed follow-up started another request.');
  assert(notice.createdSaveId === 'manual:exact-created.zip', 'Committed failure payload lost its exact destination.');
});
await scenario('uncertain failure retains uncertainty and never claims known non-creation', async () => {
  const notice = toSaveCreationNotice(await response('Uncertain', 409, true, true));
  assert(notice.kind === 'uncertain' && notice.continuationBlocked && !notice.shouldRefresh, 'Uncertain evidence was treated as a retryable failure.');
  assert(!notice.message.includes('не удалось создать'), 'Uncertain outcome falsely claimed no archive was created.');
});
await scenario('known rollback stays distinct from uncertainty', async () => {
  const notice = toSaveCreationNotice(await response('RolledBack', 400));
  assert(notice.kind === 'rolled-back' && !notice.committed && !notice.continuationBlocked, 'Known rollback was not preserved.');
  assert(notice.createdSaveId === '', 'Rollback invented a created archive identity.');
});
await scenario('a lost response blocks continuation instead of claiming rollback', async () => {
  const client = createBrowserApiClient({ fetcher: async () => { throw new Error('Response interrupted.'); } });
  const notice = toSaveCreationNotice(await client.createSave({ saveName: null }));
  assert(notice.kind === 'uncertain' && notice.continuationBlocked && !notice.shouldRefresh, 'Missing response falsely established non-creation.');
});

await scenario('the consuming save handler latches unknown evidence before any refresh or later action', async () => {
  const latch = createSaveContinuationLatch();
  const calls: string[] = [];
  const notice = await executeBrowserSaveCreation(() => response('Uncertain', 409, true, true),
    () => calls.push('notice'), value => { latch.block(value); calls.push('blocked'); },
    async () => { calls.push('refresh'); return true; });
  assert(calls.join(',') === 'notice,blocked' && latch.isBlocked(), 'The actual save handler refreshed or failed to latch uncertainty.');
  await latch.runIfAllowed(async () => { calls.push('later-write'); });
  await latch.runIfAllowed(async () => { calls.push('route-refresh'); });
  assert(calls.length === 2, 'The shell latch admitted later work after the consuming handler stopped continuation.');
  assert(notice.kind === 'uncertain', 'The handler lost its unresolved save decision.');
});

await scenario('the consuming save handler preserves a commit and identity when its refresh fails', async () => {
  const latch = createSaveContinuationLatch();
  const notices: string[] = [];
  let refreshes = 0;
  const notice = await executeBrowserSaveCreation(() => response('Committed', 200),
    value => notices.push(value.kind), value => { latch.block(value); },
    async () => { refreshes++; throw new Error('Post-save shell request interrupted.'); });
  assert(refreshes === 1 && notices.join(',') === 'saved,follow-up', 'Refresh failure did not preserve the previously applied commit.');
  assert(notice.committed && notice.createdSaveId === 'manual:exact-created.zip' && latch.isBlocked(), 'Post-commit failure erased identity or allowed continuation.');
});

await scenario('a lost response still latches after its original settings consumer is gone', async () => {
  const latch = createSaveContinuationLatch();
  const notice = await executeBrowserSaveCreation(async () => { throw new Error('Lost response after dispatch.'); },
    () => { /* The original settings mount no longer owns local notices. */ }, value => { latch.block(value); },
    async () => { throw new Error('The handler must not refresh an unknown save.'); });
  assert(notice.kind === 'uncertain' && latch.isBlocked(), 'Unmounted consumer lost the shell-wide continuation stop.');
});

console.log(`${passed} passed, ${failures.length} failed`);
if (failures.length > 0) throw new Error(failures.join('\n'));
