import { describe, expect, it } from 'vitest';
import { createSettingsComponentHarness, deferred, flushPromises, nodes, ok } from './helpers/settingsComponentHarness';

const findLoad = (tree: unknown) => nodes(tree).find(node => node.type === 'button' && node.props.children === 'Загрузить сохранение')!;
const outcome = (disposition: string, blocked = false) => ({
  disposition, success: disposition === 'Committed', error: '', menu: null,
  loadedSaveId: disposition === 'Committed' ? 'save' : '', selectedSourcePath: '/private/source.zip',
  establishedGeneration: ['Committed', 'RolledBack'].includes(disposition) ? 'exact-generation' : null,
  needsFollowUp: blocked, continuationBlocked: blocked
});

describe('actual launcher/settings load handlers retain decisions through interruption', () => {
  for (const component of ['settings', 'launcher'] as const) {
    it(`${component}: suppresses duplicate dispatch synchronously before rerender`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component](); const button = findLoad(view.tree);
      expect(button).toBeDefined(); button.props.onClick(); button.props.onClick();
      expect(h.counts().loadPosts).toBe(1);
      h.load.resolve(ok(outcome('NotLoaded'))); await flushPromises();
    });
    it(`${component}: retains a nonblocking committed follow-up after successful navigation`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component]();
      findLoad(view.tree).props.onClick();
      h.load.resolve(ok({ ...outcome('Committed'), needsFollowUp: true })); await flushPromises(); view.render();
      expect(h.counts().navigations).toBe(1);
      expect(h.shell.loadContinuationNotice).toBeUndefined();
      expect(h.shell.loadFollowUpNotice).toMatchObject({ disposition: 'Committed', loadedSaveId: 'save',
        establishedGeneration: 'exact-generation', needsFollowUp: true, continuationBlocked: false });
      expect(h.shell.loadFollowUpNotice.message).toContain('проверки');
      expect(h.shell.beginLoad()).not.toBeNull();
    });
    for (const interruption of ['none', 'unmount'] as const) {
      it(`${component}: waits for required refresh before navigation (${interruption})`, async () => {
        const h = createSettingsComponentHarness(); const view = h[component](); const refresh = deferred<boolean>();
        let requestedGeneration: string | null = null;
        h.shell.refreshAfterLoad = async (generation: string) => { requestedGeneration = generation; return refresh.promise; };
        view.render();
        const button = findLoad(view.tree); button.props.onClick();
        h.load.resolve(ok(outcome('Committed'))); await flushPromises();
        expect(requestedGeneration).toBe('exact-generation'); expect(h.counts().navigations).toBe(0);
        button.props.onClick(); expect(h.counts().loadPosts).toBe(1);
        if (interruption === 'unmount') view.unmount();
        refresh.resolve(true); await flushPromises();
        expect(h.counts().navigations).toBe(interruption === 'none' ? 1 : 0);
        if (interruption === 'unmount') expect(h.shell.loadContinuationNotice).toMatchObject({ disposition: 'Committed', continuationBlocked: true });
      });
    }
    for (const disposition of ['Committed', 'Uncertain']) {
      it(`${component}: retains ${disposition} from HTTP409 and blocks even after unmount`, async () => {
        const h = createSettingsComponentHarness(); const view = h[component]();
        findLoad(view.tree).props.onClick(); view.unmount();
        h.load.resolve({ ok: false, status: 409, payload: outcome(disposition, true) }); await flushPromises();
        expect(h.shell.loadContinuationNotice).toMatchObject({ disposition, continuationBlocked: true,
          loadedSaveId: disposition === 'Committed' ? 'save' : '',
          establishedGeneration: disposition === 'Committed' ? 'exact-generation' : null });
        expect(h.counts()).toMatchObject({ refreshes: 0, navigations: 0 });
      });
    }
    it(`${component}: blocks a lost response without refresh or retry promises`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component](); const button = findLoad(view.tree);
      button.props.onClick(); h.load.reject(new Error('response lost')); await flushPromises(); view.render();
      expect(h.shell.loadContinuationNotice).toMatchObject({ disposition: 'Uncertain', continuationBlocked: true });
      button.props.onClick(); expect(h.counts()).toMatchObject({ loadPosts: 1, refreshes: 0, navigations: 0 });
      expect(JSON.stringify(view.tree)).not.toContain('попробуйте ещё раз');
    });
    it(`${component}: preserves commitment when required refresh fails`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component](); h.setRefreshFailure();
      findLoad(view.tree).props.onClick(); h.load.resolve(ok(outcome('Committed'))); await flushPromises();
      expect(h.shell.loadContinuationNotice).toMatchObject({ disposition: 'Committed', loadedSaveId: 'save',
        establishedGeneration: 'exact-generation', continuationBlocked: true });
      expect(h.counts().navigations).toBe(0);
    });
    it(`${component}: newer navigation cannot be overwritten by an old committed response`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component]();
      findLoad(view.tree).props.onClick(); h.shell.setActiveRoute('help');
      h.load.resolve(ok(outcome('Committed'))); await flushPromises();
      expect(h.shell.activeRoute).toBe('help'); expect(h.counts().navigations).toBe(1);
      expect(h.shell.loadContinuationNotice).toMatchObject({ disposition: 'Committed', continuationBlocked: true });
    });
    it(`${component}: confirmed rollback is distinct and never navigates to a loaded chapter`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component]();
      findLoad(view.tree).props.onClick(); h.load.resolve({ ok: false, status: 400, payload: outcome('RolledBack') });
      await flushPromises(); view.render();
      expect(h.shell.loadContinuationNotice).toBeUndefined();
      expect(h.counts()).toMatchObject({ refreshes: 1, navigations: 0 });
      expect(JSON.stringify(view.tree)).toContain('восстановлено');
    });
    it(`${component}: explicit NotLoaded needs no invented generation and permits safe refusal`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component]();
      findLoad(view.tree).props.onClick(); h.load.resolve({ ok: false, status: 400,
        payload: { ...outcome('NotLoaded'), needsFollowUp: true } }); await flushPromises(); view.render();
      expect(h.shell.loadContinuationNotice).toBeUndefined();
      expect(h.counts().navigations).toBe(0);
      expect(JSON.stringify(view.tree)).toContain('не загружено');
    });
  }
  it('shares synchronous admission between both mounted consuming handlers', async () => {
    const h = createSettingsComponentHarness(); const settings = h.settings(); const launcher = h.launcher();
    findLoad(settings.tree).props.onClick(); findLoad(launcher.tree).props.onClick();
    expect(h.counts().loadPosts).toBe(1);
    h.load.resolve(ok(outcome('Uncertain', true))); await flushPromises();
    expect(h.shell.loadContinuationNotice.disposition).toBe('Uncertain');
  });
  it('same-mount launcher mode change invalidates late committed navigation', async () => {
    const h = createSettingsComponentHarness(); const view = h.launcher(); findLoad(view.tree).props.onClick();
    nodes(view.tree).find(n => n.props['data-launcher-mode'] === 'about')!.props.onClick();
    h.load.resolve(ok(outcome('Committed'))); await flushPromises();
    expect(h.counts().navigations).toBe(0);
    expect(h.shell.loadContinuationNotice).toMatchObject({ disposition: 'Committed', continuationBlocked: true });
  });
});
