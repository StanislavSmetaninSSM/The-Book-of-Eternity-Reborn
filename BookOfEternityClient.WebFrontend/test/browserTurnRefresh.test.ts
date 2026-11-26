import type { BrowserApiClient } from '../src/api/client';
import type { BrowserShellState } from '../src/context/ShellContext';
import { loadShellState } from '../src/hooks/loadShellState';
import { describe, expect, it, vi } from 'vitest';
import { canDisplaySettledTurnScene, waitForBrowserTurnSettled } from '../src/hooks/waitForBrowserTurnSettled';

describe('the submitted browser turn refresh', () => {
  it('reads only status until current game is settled, despite temporary read refusals', async () => {
    const pending = vi.fn<() => Promise<boolean | null>>()
      .mockResolvedValueOnce(true)
      .mockRejectedValueOnce(new Error('phase preparing'))
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(false);
    let now = 0;
    const delays: number[] = [];
    const settled = await waitForBrowserTurnSettled(
      pending, () => true, () => now, async (delay) => { delays.push(delay); now += delay; });
    expect(settled).toBe(true);
    expect(pending).toHaveBeenCalledTimes(4);
    expect(delays).toEqual([2000, 4000, 8000, 10000]);
  });

  it('never completes a refresh after owner replacement during a status request', async () => {
    let current = true;
    const pending = vi.fn(async () => { current = false; return false; });
    expect(await waitForBrowserTurnSettled(pending, () => current, () => 0, async () => {})).toBe(false);
    expect(pending).toHaveBeenCalledTimes(1);
  });

  it('keeps following idle status until the same owner acknowledges a successful scene', async () => {
    const status = vi.fn(async () => false);
    const acknowledge = vi.fn<() => Promise<boolean>>().mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    let now = 0;
    expect(await waitForBrowserTurnSettled(status, () => true, () => now,
      async (delay) => { now += delay; }, acknowledge)).toBe(true);
    expect(status).toHaveBeenCalledTimes(2);
    expect(acknowledge).toHaveBeenCalledTimes(2);
    expect(now).toBe(6000);
  });

  it('does not start a status request for an already retired owner', async () => {
    const status = vi.fn(async () => false);
    expect(await waitForBrowserTurnSettled(status, () => false)).toBe(false);
    expect(status).not.toHaveBeenCalled();
  });

  it('preserves an existing scene when the enqueue-time shell reads all fail', async () => {
    const reject = async () => { throw new Error('temporary'); };
    const api = {
      getMainMenu: reject, getSessionStatus: reject, getGameScreen: reject,
      getAudioSettings: reject, getClientSettings: reject, getCommandCoverage: reject
    } as unknown as BrowserApiClient;
    const previous = { status: 'ready', game: { ok: true } } as BrowserShellState;
    let current = previous;
    await loadShellState(api, next => { current = typeof next === 'function' ? next(current) : next; },
      { current: 0 }, false, () => true, true);
    expect(current).toBe(previous);
  });

  it('can show the accepted scene even when the read reports a write-blocked UI', () => {
    expect(canDisplaySettledTurnScene('blocked')).toBe(true);
    expect(canDisplaySettledTurnScene('ready')).toBe(true);
    expect(canDisplaySettledTurnScene('ready-gm-response')).toBe(false);
    expect(canDisplaySettledTurnScene('pending-gm-turn')).toBe(false);
  });

  it('stops at the monotonic budget and does not mistake unavailability for completion', async () => {
    let now = 0;
    const pending = vi.fn(async () => null);
    expect(await waitForBrowserTurnSettled(pending, () => true, () => now, async (delay) => { now += delay; })).toBe(false);
    expect(now).toBe(900000);
    expect(pending.mock.calls.length).toBeLessThan(94);
  });
});
