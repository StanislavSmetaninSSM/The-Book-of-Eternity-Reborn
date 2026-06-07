# Console Column Alignment Tasks

**Source issues:**
- #879 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/879
- #882 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/882

**Spec:** `specs/879-882-console-column-alignment/spec.md`
**Plan:** `specs/879-882-console-column-alignment/plan.md`

## Tasks

### T1 — Establish regression evidence and TDD guard

- [x] Inspect `BookOfEternityClient/UI/ConsoleLayout.cs`, `BookOfEternityClient/UI/GameInterface.cs`, and existing `ExplorerModeSourceGuardTests` HUD/layout guards.
- [x] Add or tighten a focused source/regression guard that fails on the current implementation for the missing shared aligned-row invariant or drift-prone HUD layout shape.
- [x] Run the focused test and record the RED failure reason.

### T2 — Implement shared aligned-column layout support

- [x] Update `ConsoleLayout` with the smallest reusable helper/API or helper invariant needed to make aligned label/value or metric rows explicit.
- [x] Preserve existing escaping boundaries and percent clamping behavior.
- [x] Avoid broad refactors or unrelated console style changes.

### T3 — Migrate/confirm HUD and representative consumers

- [x] Update `GameInterface.RenderStatusBar` so Health, Energy, and Poise use the shared fixed label/bar/value group plus flexible spacer (or equivalent) without Spectre expansion drift.
- [x] Check representative console surfaces already using `CreateInfoTable` / `CreateBarMetricTable`; migrate only obvious drift-prone call sites required by #882.
- [x] Keep Browser Client and gameplay behavior unchanged.

### T4 — Verification and visual artifact

- [x] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter 'FullyQualifiedName~ExplorerModeSourceGuardTests' --logger 'console;verbosity=minimal'` and record pass/fail counts.
- [x] Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and record the result.
- [x] Run `git diff --check origin/main...HEAD`.
- [x] Run an added-line static scan for secrets/injection and record `NO_MATCHES` or findings.
- [x] Run the real Console Client and capture a Console Client screenshot/terminal capture showing the HUD alignment.
  - Evidence path: `E:/Games/worktrees/boe-879-882-console-alignment/TestResults/console-hud-alignment-879-882/after-fix-20260607-111535/console-client-terminal-capture.txt`
  - Plain capture: `E:/Games/worktrees/boe-879-882-console-alignment/TestResults/console-hud-alignment-879-882/after-fix-20260607-111535/console-client-terminal-capture.plain.txt`
  - PNG visual artifact rendered from the real terminal capture: `E:/Games/worktrees/boe-879-882-console-alignment/TestResults/console-hud-alignment-879-882/after-fix-20260607-111535/console-client-terminal-capture.png`
  - Durable local copy after Hermes reconciliation: `E:/Games/The Book of Eternity Reborn/TestResults/console-hud-alignment-879-882/after-fix-20260607-111535/`
  - Measurement: Health/Energy/Poise rows all measured `barIndex=19` and `percentIndex=45`.
  - Visual inspection statement prepared for PR/issue evidence: `Verified visually in Console Client screenshot`; primary source is a real Console Client terminal capture with a PNG artifact generated from that capture.
- [x] Store/link the visual artifact and include the exact statement `Verified visually in Console Client screenshot/terminal capture` in PR/issue evidence.

### T5 — Review, PR, merge, and closure

- [x] Reconcile `spec.md`, `plan.md`, and `tasks.md` against the final diff; only mark tasks complete when implementation and verification evidence exist.
  - Hermes reconciliation confirmed final diff is presentation-only Console Client code/tests/spec artifacts.
- [x] Obtain independent review focused on #879/#882 acceptance, Spectre.Console column behavior, and visual evidence.
  - Review run: `E:/Games/codex-runs/20260607-1129-boe-879-882-console-alignment-review`
  - Verdict: `APPROVED`; safe to merge: yes; Critical/Important/Minor findings: none.
  - Visual evidence accepted as valid local-gated #879 evidence.
- [x] Fix critical/important review findings and rerun relevant verification.
  - No Critical/Important review findings were reported; no review-fix loop required.
- [ ] Create a PR linked to #879 and #882 with local verification and visual artifact evidence.
- [ ] Squash-merge locally gated PR with `[skip ci]`, delete the remote branch, fetch `main`, and verify both issues are closed.
