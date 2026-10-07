# Bounded real console Codex Q1 → Q2 plan

> **For agentic workers:** use Superpowers executing-plans inline; root remains the sole writer. Independent actual Sol6.1/xhigh reviews are read-only.

**Goal:** establish honest readiness through the original M1 terminal, then attempt at most two genuine GM turns in a disposable newly initialized console game, or preserve the exact blocking boundary.

**Architecture:** existing ordinary launcher, retained terminal owner/guardian, schema1 main fence, worker inventory and T042 dispatch. No alternate CLI API, journal, ownership reconstruction or fixture substituted for gameplay.

**Spec:** #1553, FR-009/012/013/014/015 and SC-004; latest owner approval explicitly supersedes the earlier diagnostic Q1 prohibition on one TERM confirmation and bounded provider requests. The systemd source/design WIP is preserved at `63e990097570f920dfe615e7fdf1f3542c7a6eb9` and deferred.

## Global constraints

- One `y` only at the exact observed `TERM=dumb / Continue anyway? [y/N]` warning; never resend an uncertain answer. No automatic trust/access/update/terms/login or persistent grant.
- Keep inherited TERM and configured GM command/model/args unchanged. New isolated profile uses the project's existing configured default, not the executor model. Existing Codex authorization is used without reading/copying secret or session storage.
- Unsupported VT/profile observations cannot produce Ready. No query-response fabrication or advertised unsupported terminal capability.
- Prefer ordinary `GameEngine.NewGameFlow` initialization. Experimental saves are synthetic potentially stale fixtures; they are not game history and are unnecessary for the initial attempt.
- No user saves, live browser, service/setup/auth/network/system/security change, installation, production workers or systemd qualification.
- Unknown outcomes retain original owner/fence/Uncertain and never replay. Guardian cleanup is physical evidence, not logical completion.

## Source map and review focus

The accepted Q1 plan/handoff and unchanged M1/F1–F3/T042 audits remain inputs. Changed authorization does not requalify their open boundaries.

| Source | Consumed boundary |
|---|---|
| `Launcher/bookofeternity.ps1` Start-Bridge/Start-Daemon | installed ordinary foreground bridge and independent daemon stdin; no second launcher |
| `Services/GmRuntime/ProductionMainLaunch.cs`, `GmSessionRunCoordinator.cs` | consumed admission, original Prepared/Running/release, exact scoped stop/disposal/ACK |
| `BookOfEternityGMBridge/Program.cs`, `BridgeHost.PromptDispatch.cs`, `TerminalScreen.cs` | manual keyboard y through original InputLifetime; honest reliable screen/profile, fresh paste observation and single submit |
| `Configuration/GameSettings.cs`, `GmCliInputProfile.cs` | existing configured GM default command/model, default unsupported observation markers; no inference of Ready from a banner |
| `Configuration/ClientStartupOptions.cs`, `Program.cs` | ordinary explicit disposable base-path console entrypoint |
| `Core/GameEngine/GameEngine.MainMenu.cs` NewGameFlow/InitializeChaosSea | actual current-schema bootstrap and initial TurnRequest; immutable session generation |
| `Core/GameEngine/GameEngine.TurnLifecycle.cs`, `game_master_daemon.ps1` | actual dispatch/response/validation/acceptance and typed failure, not model prose alone |

Review focuses on exact one-answer gating; an unknown terminal sequence after that answer; unexpected permission gates; ordinary initialization versus stale fixtures; and original cleanup versus a false gameplay success claim. Their evidence is owned by the stages below.

## Small execution sequence

- [ ] Publish this plan/checkpoint and obtain independent feasibility review before a new startup. Verify current installed binary hash and runtime-only package source correspondence without repeating unchanged successful cohorts.
- [ ] Extend only the reviewed diagnostic driver as a separate evidence artifact. Use a new empty owned scratch, isolated M1 config outside that cwd, existing configured default GM command unchanged, NativeLineage and disabled helpers. At the complete exact TERM warning, confirm original Running/live status, send exactly `y\r` through the original foreground keyboard once, journal bytes, and capture the next bounded screen. No prompt/model request at this stage. Reuse accepted guardian maximum30000ms, driver maximum25s, startup8s and capture256KiB. Unknown presentation or a further confirmation ends observation; original expected-identity shutdown then actual Stopped/retirement/PTY/termios/ECHILD are required. This scratch diagnostic is not new-game history or Q2.
- [ ] If presentation is technically unsupported, preserve the pinned actual transcript and get a source-delta review before any minimal necessary parser/profile change. Add causal transcript tests in one narrow category through `scripts/test-csharp.ps1`; keep unknown-sequence refusal and select only affected observation/input checks. A second TERM acceptance is not included in the one-answer authorization. Report any concrete additional permission boundary; continue independent safe source/test work.
- [ ] Only after real readiness is demonstrable, refine/review the bounded gameplay orchestration and guardian duration against actual source. Start the real console/daemon/bridge routes with separate controlled stdin in a disposable root and ordinary new-game wizard. Use current configured command/model; no ad hoc model override or direct API. Initial bootstrap counts toward the maximum two model/game turns. Record exact player action, request/accepted-turn/history/receipt and canonical outputs, not just CLI text. Prevent automatic repeated unknown dispatch and bound repair/model attempts; do not turn a startup diagnostic into acceptance.
- [ ] Independently review changed source and evidence, preserve original typed failures/cleanup, publish handoff plus remaining limitations, exact remote SHA/byte readback and fresh GitHub-only restoration. Stop before another block; systemd design remains deferred.

## Verification

Diagnostic-only next startup changes no runtime/tests/catalog: no C#/frontend test executions. A causal implementation defect requires its own source-backed delta, failing real consumer/transcript test and independent review before GREEN. No Fast/PreMerge/full suite or unchanged cohort rerun. Physical desktop/audio, native Windows, primary systemd, cold salvage and unrestricted live-game qualification remain open regardless of this bounded attempt.

Status: WIP; independent feasibility review and new startup have not executed. No answer or model prompt has been sent in this stage.
