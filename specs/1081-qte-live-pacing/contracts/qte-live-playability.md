# Console QTE Live Playability Contract (#1081)

## Source

- GitHub issue: [#1081 [QTE] Fix live mini-game pacing and memory-input visibility bugs](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1081)
- Scope: console live mini-game UX for TimingBar, PromptChain, BalanceMeter, and PatternMemory.

## Contract Boundary

This contract documents expected console-client live behavior. It does **not** introduce new GM-authored QTE fields, save-state fields, browser write contracts, afterlife contracts, rewards, scoring/ranks, or Daren route content.

If implementation discovers that a GM-authored QTE schema field must change, this contract is incomplete until the GM-facing QTE docs/examples/tests are updated in the same branch.

## TimingBar

- Live panel must show a moving marker, a success/target zone, and remaining time.
- Effective marker speed or challenge must be difficulty-sensitive:
  - high difficulty must not be equivalent to low difficulty for the same stat tier;
  - stat tier may soften difficulty but must not make every difficulty trivially winnable;
  - normal/easy attempts must remain readable and fair.
- Test evidence should prefer deterministic effective-speed/window assertions over wall-clock sleeps.
- Live evidence must reference the actual live TimingBar path, not only a static config builder.

## PromptChain

- Live panel must show the current sign/key, step progress, mistake count where applicable, and remaining time.
- The first actionable prompt must have a readable startup/display/input window; the timer must not already be near zero at the moment the player can react.
- Timeout/cancel/fail/success semantics must remain compatible with existing QTE result handling.
- Live evidence must demonstrate that a run no longer fails immediately before the player can read/react.

## BalanceMeter

- Live panel must show current marker/position and safe/target range.
- Player-facing controls must state direction and effective step, e.g. A/← moves left by the configured/effective amount and D/→ moves right by that amount.
- The visible hint and actual input effect must match.
- Copy must remain Russian/in-world/player-facing and must not expose debug/API/DTO terms.

## PatternMemory

- Reveal phase may show the full sequence for memorization.
- Input phase must not render the full original reveal sequence or otherwise leak the answer.
- Input phase may show progress, current input prompt, entered-count feedback, controls, timer, and mistakes if those do not reveal the remaining full answer.
- Result summaries must preserve existing grade semantics.

## Live Evidence Requirement

Closure evidence for #1081 must cover all four reported mini-games. Acceptable evidence includes:

1. A real console/manual smoke transcript or screenshot/artifact.
2. A ConPTY/winpty-style harness snapshot that reads the visible screen.
3. A deterministic in-app/scripted harness or test mode that exercises the same live mini-game loop/rendering/timer/control code and records observation artifacts.
4. A focused runtime test with a deterministic clock/input seam, if it genuinely covers the live path.

Static source guards or config-builder tests may supplement but must not be the only evidence.

If autonomous execution cannot perform a human visual smoke, the final issue comment/report must say so and cite the deterministic live-path substitute plus any residual risk.

## Verification Checklist

- TimingBar pacing/difficulty regression test or live-path probe passes.
- PromptChain startup/readability regression test or live-path probe passes.
- BalanceMeter control readability test/snapshot passes.
- PatternMemory input reveal-hiding test/snapshot passes.
- Focused QTE neighborhood tests pass with non-zero counts.
- Client build passes.
- `git diff --check origin/main...HEAD` passes.
- Added-line static scan has no real security/run-artifact findings.
- Independent review approves the live-path evidence and scope boundaries.
