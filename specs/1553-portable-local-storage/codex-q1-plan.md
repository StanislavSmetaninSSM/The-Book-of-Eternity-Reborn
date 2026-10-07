# T043-Q1 Codex startup feasibility plan

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), owner's Q1 request, accepted desktop checkpoint `428127226308b98b630074e05a839200dad748c0`.

**Goal:** observe the installed Codex startup through the original M1 terminal/fence without sending a model prompt, and either qualify a narrowly demonstrated readiness presentation or preserve a precise blocker.

**Architecture:** inline sole writer, Spec Kit current feature plus Superpowers writing-plans/executing-plans and bridge. Reuse the ordinary launcher, production admission, retained main/worker coordinator, original native owner and independent guardian. No alternate launcher, Codex API, journal or synthetic substitute for the actual CLI.

**Spec:** FR-009/012/013/014/015; SC-004 remains open. [Production design](production-main-admission-design.md), [owned terminal](owned-main-terminal-design.md), [F2](main-run-fence-f2-handoff.md), [M1](production-main-m1-handoff.md), [roadmap](roadmap-after-daren.md). Historical handoffs retain their checkpoint-specific debt; later accepted closures are not undone.

## Source map and feasibility boundary

| Source | Boundary used here |
|---|---|
| `BookOfEternityClient/Launcher/bookofeternity.ps1` | ordinary foreground `start-bridge visible`; no window registration or bootstrap paste |
| `BookOfEternityClient/Services/GmRuntime/ProductionMainLaunch.cs` | exact configured PowerShell command/cwd, explicit NativeLineage, prebuilt helper validation, consumed single launch |
| `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.cs` | Prepared before creation, Running before single release, original stop and actual retirement/Stopped ACK |
| `BookOfEternityGMBridge/Program.cs` | original BridgeHost/InputLifetime/output pump and real pipe loop/status; independent retirement tasks |
| `BookOfEternityGMBridge/BridgeHost.PromptDispatch.cs` | supported profile + reliable fresh empty composer are needed for readiness; no automatic trust/update answers |
| `BookOfEternityGMBridge/TerminalScreen.cs` | neutral-v1 only: UTF8 text, CR/LF/BS, clear+home. Other sequences invalidate observation; no cursor queries/alternate screen/width qualification |
| `BookOfEternityClient/Configuration/GmCliInputProfile.cs` | default markers are empty/unsupported. A warning is never an idle composer, and no Codex profile is invented from this probe |
| `tests/fixtures/ProductionMain/ordinary.py` | existing installed foreground/PTy/pipe shape; reuse shape without running its neutral fixture or old cohorts |
| `recovery/production-main-design.json` | sole historical raw-PTY probe: TERM=dumb confirmation, no answer, original root exit and guardian ECHILD. It is not M1 evidence |

Current inherited `TERM=dumb`. Keep it byte-for-byte; do not set `TERM=xterm*`, answer a terminal query, supply `--no-alt-screen` as an unproved presentation fix, or auto-confirm anything. Existing neutral-v1 is honest for its declared subset but not evidence of a usable Codex TUI. A manual acceptance would still not qualify the renderer.

## Smallest execution sequence

- [ ] Publish this WIP and exact remote/byte readback; obtain independent actual `gpt-6.1-sol`/`xhigh` feasibility review before CLI invocations or new startup probes.
- [ ] Safe official preflight: resolve executable, actual `codex --version`, bounded `--help`, `login status`; record executable SHA256 and actual outputs. No auth/config/session-file reads or copies, installation, PATH/auth workaround or model subcommand. Help determines whether the previously accepted `--no-daemon -C <own empty scratch>` invocation is still available; otherwise stop before startup and report incompatibility. Verify SDK/runtime/PowerShell only if package preparation uses them.
- [ ] Prepare one own runtime-only M1 package from current pinned sources or verified current build outputs. Reuse exact accepted prebuilt NativeLineage helper and independent guardian bytes with their provenance; no compiler/helper redesign. Preparation/build/publish is distinct from runtime/tests. Fixture profile/root/runtime metadata is outside Codex's empty cwd, entirely synthetic. No user profile/auth/config is edited. Configure only the fixture's ordinary command as installed Codex `--no-daemon -C <empty scratch>`, do not read/edit a player game profile, choose a GM model, or pass a model override; this own fixture uses the same accepted startup-only diagnostic argv and does not qualify a game profile. Helpers disabled; no daemon/game client/game turn is started.
- [ ] Run at most one new interactive startup through ordinary `start-bridge visible`, own retained 100x25 foreground PTY under the original independent guardian. Driver journal sends **zero keyboard/model/paste/submit bytes**. Bound startup to 8 seconds, capture to 256KiB, entire driver/cleanup to 25 seconds and outer guardian to 30 seconds (accepted guardian maximum: 30000ms). Capture exact safe startup output plus actual pipe status and durable Running identity. Observe warning/access/trust/update/terms gates without answering. Stop immediately when a gate is observed; otherwise collect the bounded presentation once. The raw output is visual evidence, not authority.
- [ ] Stop through the actual same live bridge RPC with exact original expected main identity; await actual process exit, drain/close own foreground PTY, original Stopped record/ACK and guardian ECHILD with zero emergency signals/failures/deadline. If any stop evidence is uncertain, preserve logical Uncertain/owner/metadata and report it; guardian emergency cleanup cannot upgrade it. Never signal a discovered PID, restart, mint a second owner from JSON/status or repeat a lost command.
- [ ] If confirmation or unknown VT blocks readiness, stop Q1 at that exact boundary; no runtime/parser/profile edit or replay merely to get past it. Record independent source/evidence/metadata reviews of the diagnostic and limitations. If genuinely observable readiness exists, first derive only its necessary pinned transcript subset, obtain targeted source-delta review, add causal transcript tests and a narrow category through `scripts/test-csharp.ps1` before any renderer change. Unknown sequences must still refuse readiness. This conditional code path is not presumed necessary.
- [ ] Publish qualification/handoff with exact source/artifact hashes, actual attempt counts, preflight/preparation/runtime separation, input journal and original cleanup. Final independent reviews, ordinary push/remote SHA/byte readback, fresh GitHub-only restore; stop before Q2 or another block.

## Verification selection and constraints

No C#/frontend/catalog execution is needed for a diagnostic-only blocker with unchanged runtime/tests/catalog. No successful F1/F2/F3/M1/terminal/input cohorts are rerun. If actual pinned transcripts require a parser correction, selection becomes one new coherent Q1 presentation category plus only affected existing terminal/input observation regressions, with `PlanOnly`, `ValidateCatalog` and `tests/selection.json` updated before execution. A blocked startup is not a causal RED of existing neutral-v1's declared scope.

Review must distinguish plain warning output from VT correctness, `Ready=false` with unsupported default profile from renderer-qualified readiness, root exit from original scoped retirement, and guardian physical cleanup from logical settlement. No claim of a positive TUI, desktop terminal, native Windows, systemd-user, provider/model availability, live GM/game turn, worker production, real saves or cold guarantees. Primary systemd remains required and separately open; no user manager/settings are created.

## Pending decision only if needed

An unexpected CLI confirmation is not authorized for automatic acceptance. Return its exact safe text and choices: explicit owner decision about that confirmation plus separately demonstrated presentation, or another actually supported foreground terminal environment. No TERM workaround or unsupported capability claim. A rendering blocker is technical feasibility, not a change to the game's GM model, CLI contract or Load UX.

Status: design/source PASS and one actual M1 startup/stop observation completed; Q1 readiness BLOCKED on unanswered TERM confirmation. See handoff/qualification for actual output and original cleanup. Conditional parser/transcript path was not entered; evidence/metadata/final writer closure pending. D1 guardian bound and S1 foreground success oracle were independently closed before startup. No repetition or runtime changes.
