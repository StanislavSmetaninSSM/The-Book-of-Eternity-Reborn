import { describe, expect, it } from 'vitest';
import { createSettingsComponentHarness, flushPromises, nodes, ok } from './helpers/settingsComponentHarness';

const findSave = (tree: unknown) => nodes(tree).find(node => node.type === 'button' && node.props.children === 'Сохранить игру')!;
const findLoad = (tree: unknown) => nodes(tree).find(node => node.type === 'button' && node.props.children === 'Загрузить сохранение')!;
const outcome = (disposition: string, continuationBlocked: boolean) => ({
  success: disposition === 'Committed', error: '', menu: null, disposition,
  needsFollowUp: continuationBlocked, continuationBlocked,
  createdSaveId: disposition === 'Committed' ? 'manual:exact-created.zip' : ''
});

describe('actual browser save handler preserves outcomes and stops continuation', () => {
  for (const disposition of ['Committed', 'Uncertain']) {
    it(`retains ${disposition} from an HTTP failure and stops further actions and refresh`, async () => {
      const harness = createSettingsComponentHarness(); const view = harness.settings();
      const save = findSave(view.tree); const load = findLoad(view.tree);
      expect(save.props.disabled).toBe(false);
      save.props.onClick();
      harness.save.resolve({ ok: false, status: 409, payload: outcome(disposition, true) });
      await flushPromises(); view.render();
      expect(harness.counts()).toMatchObject({ savePosts: 1, refreshes: 0 });
      expect(harness.shell.saveContinuationNotice).toMatchObject({
        continuationBlocked: true, committed: disposition === 'Committed',
        createdSaveId: disposition === 'Committed' ? 'manual:exact-created.zip' : ''
      });
      // Invoke retained handler references, as if a stale surface tried to dispatch.
      save.props.onClick(); load.props.onClick(); await flushPromises();
      expect(harness.counts()).toMatchObject({ savePosts: 1, refreshes: 0 });
    });
  }

  it('keeps the known created destination when actual post-save refresh fails', async () => {
    const harness = createSettingsComponentHarness(); const view = harness.settings();
    harness.setRefreshFailure(); findSave(view.tree).props.onClick();
    harness.save.resolve(ok(outcome('Committed', false))); await flushPromises();
    expect(harness.counts()).toMatchObject({ savePosts: 1, refreshes: 1 });
    expect(harness.shell.saveContinuationNotice).toMatchObject({
      committed: true, kind: 'follow-up', continuationBlocked: true, createdSaveId: 'manual:exact-created.zip'
    });
  });

  it('latches a lost save response after the original settings view has unmounted', async () => {
    const harness = createSettingsComponentHarness(); const view = harness.settings();
    findSave(view.tree).props.onClick(); view.unmount();
    harness.save.reject(new Error('Response lost after archive request dispatch.')); await flushPromises();
    expect(harness.counts()).toMatchObject({ savePosts: 1, refreshes: 0 });
    expect(harness.shell.saveContinuationNotice).toMatchObject({ kind: 'uncertain', continuationBlocked: true });
  });
});
