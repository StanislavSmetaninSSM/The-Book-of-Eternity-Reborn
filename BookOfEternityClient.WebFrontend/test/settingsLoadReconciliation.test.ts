import { describe, expect, it } from 'vitest';
import { createSettingsComponentHarness, flushPromises, nodes, ok } from './helpers/settingsComponentHarness';

const notLoaded = { success: false, error: '', disposition: 'NotLoaded', loadedSaveId: '', selectedSourcePath: null, establishedGeneration: null, needsFollowUp: false, continuationBlocked: false };
const failure = { ok: false, status: 400, kind: 'http-error', playerMessage: 'blocked', message: 'blocked', payload: notLoaded };
const findLoadButton = (tree: unknown) => nodes(tree).find((node) => node.type === 'button' && node.props.children === 'Загрузить сохранение')!;
const findLanguage = (tree: unknown) => nodes(tree).find((node) => node.type === 'select');
const findMusicVolume = (tree: unknown) => nodes(tree).find((node) => node.type === 'input' && node.props.type === 'range')!;

describe('actual settings/audio handlers reconcile an interrupted save-load boundary', () => {
  for (const mode of ['http-failure', 'domain-failure'] as const) {
    it(`reloads confirmed settings after ${mode} without submitting the cancelled draft`, async () => {
      const harness = createSettingsComponentHarness(); const view = harness.settings();
      findLanguage(view.tree)!.props.onChange({ target: { value: 'en' } }); view.render();
      expect(findLanguage(view.tree)!.props.value).toBe('en');
      findLoadButton(view.tree).props.onClick();
      harness.load.resolve(mode === 'domain-failure' ? ok(notLoaded) : failure);
      await flushPromises(); view.render(); view.render();
      expect(harness.counts()).toMatchObject({ settingsPosts: 0, refreshes: 1 });
      expect(harness.timers.size).toBe(0);
      expect(findLanguage(view.tree)!.props.value).toBe('ru');
    });
  }

  it('accepts a confirmed audio refresh after the shared load scope invalidates an in-flight update', async () => {
    const harness = createSettingsComponentHarness(); const settings = harness.settings();
    const scope = nodes(settings.tree).find((node) => node.props?.writeScope)?.props.writeScope;
    expect(scope).toBeDefined();
    const audio = harness.audio(scope);
    findMusicVolume(audio.tree).props.onChange({ currentTarget: { value: '77' } });
    await flushPromises(); expect(harness.counts().audioPosts).toBe(1);
    findLoadButton(settings.tree).props.onClick();
    harness.audioWrite.resolve(ok({ ...harness.initialAudio, musicVolume: 77 }));
    harness.load.resolve(failure); await flushPromises();
    // Also model a later confirmed global refresh, independent of load handling.
    harness.shell.readyState = { ...harness.shell.readyState, audio: ok({ ...harness.initialAudio, musicVolume: 99 }) };
    audio.render(); audio.render();
    expect(findMusicVolume(audio.tree).props.value).toBe(99);
  });

  it('does not refresh an unmounted view after a failed load response', async () => {
    const harness = createSettingsComponentHarness(); const view = harness.settings();
    findLoadButton(view.tree).props.onClick(); view.unmount();
    harness.load.resolve(failure); await flushPromises();
    expect(harness.counts().refreshes).toBe(0);
  });

  it('does not present a cancelled optimistic value as confirmed when reconciliation also fails', async () => {
    const harness = createSettingsComponentHarness(); const view = harness.settings();
    findLanguage(view.tree)!.props.onChange({ target: { value: 'en' } }); view.render();
    harness.setRefreshFailure(); findLoadButton(view.tree).props.onClick();
    harness.load.resolve(failure); await flushPromises(); view.render(); view.render();
    expect(harness.counts()).toMatchObject({ settingsPosts: 0, refreshes: 1 });
    expect(findLanguage(view.tree)).toBeUndefined();
    expect(nodes(view.tree).some((node) => node.props?.role === 'status')).toBe(true);
  });
});
