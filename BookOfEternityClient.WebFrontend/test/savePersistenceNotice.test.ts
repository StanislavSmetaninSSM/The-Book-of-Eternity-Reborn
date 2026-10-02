import { createBrowserApiClient } from '../src/api/client.js';
import { toSaveCreationNotice } from '../src/utils/savePersistenceNotice.js';

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

console.log(`${passed} passed, ${failures.length} failed`);
if (failures.length > 0) throw new Error(failures.join('\n'));
