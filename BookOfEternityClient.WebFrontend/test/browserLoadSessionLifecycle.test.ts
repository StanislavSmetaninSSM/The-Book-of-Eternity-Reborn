import { describe, expect, it } from 'vitest';
import { createSettingsComponentHarness, flushPromises, nodes, ok } from './helpers/settingsComponentHarness';

describe('actual Load handlers await original fresh-launch receipt after applying refresh', () => {
  for (const component of ['launcher', 'settings'] as const) {
    it(`${component}: cannot navigate while fresh launch remains unconfirmed`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component]();
      const button = nodes(view.tree).find(n => n.type === 'button' && n.props.children === 'Загрузить сохранение')!;
      button.props.onClick();
      h.load.resolve(ok({ success: true, disposition: 'Committed', error: '', loadedSaveId: 'save',
        selectedSourcePath: '/isolated/source.zip', establishedGeneration: 'loaded-generation',
        needsFollowUp: false, continuationBlocked: false, lifecycleOperationId: '0123456789abcdef0123456789abcdef',
        mainSessionState: 'Stopped', freshLaunchRequired: true }));
      await flushPromises();
      expect(h.counts().refreshes).toBe(1);
      expect(h.counts().navigations).toBe(0);
      expect(h.counts().loadCompletions).toBe(1);
    });
  }
});
