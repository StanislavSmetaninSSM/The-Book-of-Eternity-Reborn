import { describe, expect, it } from 'vitest';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { LoadContinuationBlockedNotice, LoadFollowUpNotice } from '../src/components/LoadContinuationBlockedNotice';
import { toLoadNotice } from '../src/utils/loadPersistenceNotice';
import type { BrowserApiClient } from '../src/api/client';
import { createBrowserApiClient } from '../src/api/client';
import { refreshShellAfterLoad } from '../src/hooks/refreshShellAfterLoad';
import { createSettingsComponentHarness, deferred, flushPromises } from './helpers/settingsComponentHarness';

const bundle = () => ({ establishedGeneration: 'generation-A', menu: { saves: [{ saveId: 'save' }] },
  session: {}, game: {}, settings: {}, audio: {}, noActiveSession: false });
const api = (data: unknown, status = 200) => createBrowserApiClient({ fetcher: async () => new Response(JSON.stringify(data),
  { status, headers: { 'Content-Type': 'application/json' } }) });

describe('complete load refresh uses exact replacement authority', () => {
  it('publishes every required surface together and clears old optional diagnostics', async () => {
    const publications: any[] = []; const owner = { current: 0 };
    expect(await refreshShellAfterLoad(api(bundle()), s => publications.push(s), owner, 'generation-A', () => true)).toBe(true);
    expect(publications).toHaveLength(1);
    expect(publications[0]).toMatchObject({ status: 'ready', lifecycle: null, commandCoverage: null });
    for (const field of ['menu', 'session', 'game', 'settings', 'audio']) expect(publications[0][field].ok).toBe(true);
  });
  for (const fault of ['generation', 'menu', 'session', 'game', 'settings', 'audio', 'http', 'lost']) {
    it(`refuses ${fault} without publishing a partial or unbound shell`, async () => {
      const data: any = bundle(); if (fault === 'generation') data.establishedGeneration = 'generation-B';
      else if (fault in data) data[fault] = null;
      const client = fault === 'lost' ? createBrowserApiClient({ fetcher: async () => { throw new Error('lost'); } })
        : api(data, fault === 'http' ? 409 : 200);
      const published: any[] = [];
      expect(await refreshShellAfterLoad(client, s => published.push(s), { current: 0 }, 'generation-A', () => true)).toBe(false);
      expect(published).toHaveLength(0);
    });
  }
  it('does not use archive-list presence to accept another generation', async () => {
    const data = { ...bundle(), establishedGeneration: 'old-generation' };
    expect(await refreshShellAfterLoad(api(data), () => { throw new Error('must not publish'); }, { current: 0 }, 'generation-A', () => true)).toBe(false);
  });
  for (const reason of ['newer-refresh', 'unmounted']) {
    it(`discards the entire late bundle after ${reason}`, async () => {
      const response = deferred<any>(); const owner = { current: 0 }; let mounted = true; const published: any[] = [];
      const work = refreshShellAfterLoad({ getLoadState: () => response.promise } as BrowserApiClient,
        s => published.push(s), owner, 'generation-A', () => mounted);
      if (reason === 'newer-refresh') owner.current++; else mounted = false;
      response.resolve({ ok: true, status: 200, data: bundle() });
      expect(await work).toBe(false); expect(published).toHaveLength(0);
    });
  }
  it('accepts only explicitly allowed no-active-chapter absence for safe reconciliation', async () => {
    const data = { ...bundle(), game: null, noActiveSession: true };
    expect(await refreshShellAfterLoad(api(data), () => {}, { current: 0 }, 'generation-A', () => true)).toBe(false);
    const published: any[] = [];
    expect(await refreshShellAfterLoad(api(data), s => published.push(s), { current: 0 }, null, () => true, true)).toBe(true);
    expect(published[0].game).toMatchObject({ ok: false, kind: 'no-active-session' });
  });
});

describe('actual ShellProvider owns load admission and blocked continuation', () => {
  it('keeps a synchronous single dispatch owner and does not let stale finish release its successor', async () => {
    const h = createSettingsComponentHarness(); const view = h.provider(); await flushPromises(); view.render();
    const shell = () => view.tree.props.value;
    const first = shell().beginLoad(); expect(first).not.toBeNull(); expect(shell().beginLoad()).toBeNull();
    shell().finishLoad(first); const second = shell().beginLoad(); expect(second).not.toBeNull();
    shell().finishLoad(first); expect(shell().beginLoad()).toBeNull();
    expect(shell().isLoadCurrent(second)).toBe(true);
    shell().setActiveRoute('settings'); expect(shell().isLoadCurrent(second)).toBe(false);
  });
  it('blocks commands, actions and ordinary refresh during load and after a stale owner records uncertainty', async () => {
    const h = createSettingsComponentHarness(); const view = h.provider(); await flushPromises(); view.render();
    const shell = () => view.tree.props.value; const baseline = h.counts().ordinaryReads;
    const owner = shell().beginLoad();
    await shell().executeCommand('/help'); shell().submitComposerText('next'); await shell().loadBrowserState();
    expect(h.counts()).toMatchObject({ commandPosts: 0, actionPosts: 0, ordinaryReads: baseline });
    shell().setActiveRoute('settings');
    shell().blockLoadContinuation({ disposition: 'Uncertain', continuationBlocked: true, message: 'uncertain',
      loadedSaveId: '', establishedGeneration: null, selectedSourcePath: null, needsFollowUp: true });
    shell().finishLoad(owner); view.render();
    shell().setActiveRoute('game'); await shell().executeCommand('/help'); shell().submitComposerText('next'); await shell().loadBrowserState();
    view.render(); expect(shell().activeRoute).toBe('settings');
    expect(shell().loadContinuationNotice.disposition).toBe('Uncertain');
    expect(h.counts()).toMatchObject({ commandPosts: 0, actionPosts: 0, ordinaryReads: baseline });
  });
  it('retains nonblocking follow-up after navigation without stopping commands', async () => {
    const h = createSettingsComponentHarness(); const view = h.provider(); await flushPromises(); view.render();
    const shell = () => view.tree.props.value;
    const notice = toLoadNotice({ ok: true, status: 200, data: { disposition: 'Committed', success: true, error: '',
      loadedSaveId: 'manual:confirmed.zip', selectedSourcePath: '/private/not-for-display', establishedGeneration: 'exact',
      needsFollowUp: true, continuationBlocked: false, menu: null } });
    shell().reportLoadNotice(notice); shell().setActiveRoute('game'); view.render();
    expect(shell().loadFollowUpNotice).toEqual(notice); expect(shell().loadContinuationNotice).toBeNull();
    const work = shell().executeCommand('/help'); expect(h.counts().commandPosts).toBe(1);
    h.command.resolve({ ok: true, status: 200, data: { state: 'Completed', blocks: [] } }); await work;
    // A later healthy replacement does not erase unresolved earlier cleanup debt or relabel it as current.
    shell().reportLoadNotice({ ...notice, loadedSaveId: 'manual:new-current.zip', establishedGeneration: 'new-current', needsFollowUp: false });
    view.render(); expect(shell().loadFollowUpNotice.loadedSaveId).toBe('manual:confirmed.zip');
    const html = renderToStaticMarkup(createElement(LoadFollowUpNotice, { notice: shell().loadFollowUpNotice }));
    expect(html).toContain('той загрузки'); expect(html).not.toContain('manual:new-current.zip');
    expect(html).toContain('role="status"'); expect(html).toContain('manual:confirmed.zip');
    expect(html).toContain('требует проверки'); expect(html).not.toContain('/private/');
    const blocked = renderToStaticMarkup(createElement(LoadContinuationBlockedNotice,
      { notice: { ...notice, continuationBlocked: true } }));
    expect(blocked).toContain('role="alert"'); expect(blocked).toContain('manual:confirmed.zip');
    expect(blocked).not.toContain('<button'); expect(blocked).not.toContain('/private/');
  });
  it('clears an already-present command view when load takes ownership', async () => {
    const h = createSettingsComponentHarness(); const view = h.provider(); await flushPromises(); view.render();
    const shell = () => view.tree.props.value; const work = shell().executeCommand('/old');
    h.command.resolve({ ok: true, status: 200, data: { state: 'Completed', blocks: [] } }); await work; view.render();
    expect(shell().isCommandView).toBe(true);
    const owner = shell().beginLoad(); shell().finishLoad(owner); view.render();
    expect(shell().commandResult).toBeNull(); expect(shell().isCommandView).toBe(false);
  });
  it('a command reply cannot publish or dispatch an old GM action after a healthy load has finished', async () => {
    const h = createSettingsComponentHarness(); const view = h.provider(); await flushPromises(); view.render();
    const shell = () => view.tree.props.value; const command = shell().executeCommand('/old');
    const owner = shell().beginLoad(); shell().finishLoad(owner); shell().setActiveRoute('settings');
    h.command.resolve({ ok: true, status: 200, data: { state: 'Completed', blocks: [], pendingGmAction: 'old action' } });
    await flushPromises(); view.render();
    expect(shell().activeRoute).toBe('settings'); expect(shell().commandResult).toBeNull();
    expect(h.counts().actionPosts).toBe(0);
    await command;
  });
  for (const source of ['direct-action', 'command-follow-up'] as const) {
    it(`${source}: discards an old action response after successful load finish`, async () => {
      const h = createSettingsComponentHarness(); const view = h.provider(); await flushPromises(); view.render();
      const shell = () => view.tree.props.value;
      if (source === 'direct-action') shell().submitComposerText('old action');
      else {
        void shell().executeCommand('/old');
        h.command.resolve({ ok: true, status: 200, data: { state: 'Completed', blocks: [], pendingGmAction: 'old action' } });
        await flushPromises();
      }
      expect(h.counts().actionPosts).toBe(1);
      const owner = shell().beginLoad(); shell().finishLoad(owner);
      h.action.resolve({ ok: true, status: 200, data: { success: true, playerMessage: 'OLD RESPONSE' } });
      await flushPromises(); view.render(); expect(shell().composerNotice).not.toBe('OLD RESPONSE');
    });
  }
  it('a command dispatched earlier cannot navigate after load stops continuation', async () => {
    const h = createSettingsComponentHarness(); const view = h.provider(); await flushPromises(); view.render();
    const shell = () => view.tree.props.value; const command = shell().executeCommand('/help');
    shell().beginLoad(); shell().blockLoadContinuation({ disposition: 'Committed', continuationBlocked: true,
      loadedSaveId: 'save', establishedGeneration: 'generation-A', selectedSourcePath: '/private', needsFollowUp: true, message: 'loaded' });
    h.command.resolve({ ok: true, status: 200, data: { state: 'Completed', blocks: [] } }); await command; view.render();
    expect(shell().activeRoute).toBe('home'); expect(shell().commandResult).toBeNull();
  });
});
