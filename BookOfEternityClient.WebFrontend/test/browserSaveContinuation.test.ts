import { describe, expect, it } from 'vitest';
import { createSettingsComponentHarness, flushPromises, nodes, ok } from './helpers/settingsComponentHarness';
import { createBrowserApiClient } from '../src/api/client';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { SaveContinuationBlockedNotice } from '../src/components/SaveContinuationBlockedNotice';

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
      const html = renderToStaticMarkup(createElement(SaveContinuationBlockedNotice,
        { notice: harness.shell.saveContinuationNotice }));
      expect(html).toContain('Продолжение остановлено');
      expect(html).toContain('role="alert"');
      expect(html).not.toContain('<button');
      expect(html).not.toContain('<form');
      if (disposition === 'Committed') {
        expect(html).toContain('Сохранение создано');
        expect(html).toContain('manual:exact-created.zip');
      }
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
    const html = renderToStaticMarkup(createElement(SaveContinuationBlockedNotice,
      { notice: harness.shell.saveContinuationNotice }));
    expect(html).toContain('Сохранение создано');
    expect(html).toContain('manual:exact-created.zip');
  });

  it('latches a lost save response after the original settings view has unmounted', async () => {
    const harness = createSettingsComponentHarness(); const view = harness.settings();
    findSave(view.tree).props.onClick(); view.unmount();
    harness.save.reject(new Error('Response lost after archive request dispatch.')); await flushPromises();
    expect(harness.counts()).toMatchObject({ savePosts: 1, refreshes: 0 });
    expect(harness.shell.saveContinuationNotice).toMatchObject({ kind: 'uncertain', continuationBlocked: true });
  });

  for (const failure of ['all-network', 'menu-http'] as const) {
    it(`preserves the committed identity and stops after real loadShellState ${failure} results`, async () => {
      const harness = createSettingsComponentHarness(); const view = harness.settings();
      const client = createBrowserApiClient({ fetcher: async (url) => {
        if (failure === 'all-network') throw new Error('Real shell refresh network response lost.');
        const failed = String(url).endsWith('/api/main-menu');
        return new Response(JSON.stringify(failed ? { error: 'The required save menu could not be read.' } : {}),
          { status: failed ? 503 : 200, headers: { 'Content-Type': 'application/json' } });
      } });
      harness.setRealRefreshClient(client);
      findSave(view.tree).props.onClick(); harness.save.resolve(ok(outcome('Committed', false)));
      await flushPromises(); await new Promise(resolve => setImmediate(resolve)); await flushPromises();
      expect(harness.counts()).toMatchObject({ savePosts: 1, refreshes: 1 });
      const refreshed = harness.realRefreshState();
      expect(failure === 'all-network' ? refreshed.status === 'error' : refreshed.status === 'ready' && !refreshed.menu.ok).toBe(true);
      expect(harness.shell.saveContinuationNotice).toMatchObject({
        committed: true, kind: 'follow-up', continuationBlocked: true, createdSaveId: 'manual:exact-created.zip'
      });
    });
  }

  for (const hasExactId of [true, false]) {
    it(`uses real refreshed save identity to ${hasExactId ? 'confirm continuation' : 'stop continuation'}`, async () => {
      const harness = createSettingsComponentHarness(); const view = harness.settings();
      const client = createBrowserApiClient({ fetcher: async (url) => {
        const isMenu = String(url).endsWith('/api/main-menu');
        // Optional diagnostic coverage failure does not hide confirmed required surfaces.
        const isCoverage = String(url).endsWith('/api/explorer/command-coverage');
        const data = isMenu ? { saves: [{ saveId: hasExactId ? 'manual:exact-created.zip' : 'manual:other.zip' }] }
          : String(url).endsWith('/api/client/settings') ? harness.persistedSettings : {};
        return new Response(JSON.stringify(data),
          { status: isCoverage ? 503 : 200, headers: { 'Content-Type': 'application/json' } });
      } });
      harness.setRealRefreshClient(client);
      findSave(view.tree).props.onClick(); harness.save.resolve(ok(outcome('Committed', false)));
      await flushPromises(); await new Promise(resolve => setImmediate(resolve)); await flushPromises();
      expect(harness.counts()).toMatchObject({ savePosts: 1, refreshes: 1 });
      expect(harness.realRefreshState().status).toBe('ready');
      if (hasExactId) expect(harness.shell.saveContinuationNotice).toBeUndefined();
      else expect(harness.shell.saveContinuationNotice).toMatchObject({
        committed: true, kind: 'follow-up', continuationBlocked: true, createdSaveId: 'manual:exact-created.zip'
      });
    });
  }
});
