const initialDelayMs = 2_000;
const maxDelayMs = 10_000;
const observationBudgetMs = 15 * 60_000;
const requestBudgetMs = 20_000;

/**
 * Read only the existing pending status, serially. An unreadable phase is not
 * completion. The caller owns the observation; this never submits or replays.
 */
export async function waitForBrowserTurnSettled(
  readPending: (signal: AbortSignal) => Promise<boolean | null>,
  isCurrent: () => boolean,
  now: () => number = () => performance.now(),
  delay: (ms: number) => Promise<void> = (ms) => new Promise(resolve => setTimeout(resolve, ms)),
  acknowledge: (signal: AbortSignal) => Promise<boolean> = async () => true
): Promise<boolean> {
  const deadline = now() + observationBudgetMs;
  let interval = initialDelayMs;
  while (isCurrent()) {
    const remaining = deadline - now();
    if (remaining <= 0) return false;
    await delay(Math.min(interval, remaining));
    if (!isCurrent() || now() >= deadline) return false;
    try {
      const requestSignal = () => AbortSignal.timeout(Math.max(1, Math.min(requestBudgetMs, Math.ceil(deadline - now()))));
      const pending = await readPending(requestSignal());
      if (!isCurrent() || now() >= deadline) return false;
      if (pending === false && await acknowledge(requestSignal()) && isCurrent() && now() < deadline) return true;
    } catch { /* Temporary refusal is not evidence of settlement. */ }
    interval = Math.min(interval * 2, maxDelayMs);
  }
  return false;
}

/** The status read already observed idle. A scene can be readable while writes are blocked. */
export function canDisplaySettledTurnScene(state: string): boolean {
  return state !== 'ready-gm-response' && state !== 'pending-gm-turn';
}
