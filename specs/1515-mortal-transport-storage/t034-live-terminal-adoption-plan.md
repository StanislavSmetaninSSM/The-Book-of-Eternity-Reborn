# Minimal live client adapter plan (read-only, supersedes broad prelive proposal)

Do NOT build a second hosted Load/daemon journey. Keep T033 canonical continuation,
existing relay owner/generation proofs and retained-daemon predicates. Actual archive
activation, hosted A→B, same-daemon heartbeat and scoped B shutdown remain the real
T016 live acceptance boundary under unchanged guards, not a new inert framework.
No code, fixture, world or process was changed/run for this plan.

## Existing relevant coverage, with honest limits

- `GmConsoleRuntimeAdmissionTests.ActualConsoleContinue_MissingOutputReadReachesPlayerAndReturnsToMenu`
  (`gm-console-continue-admission`) uses actual shipped client + real owned main.
  `OwnedTerminalScenarioDriver.ConsoleRuntime.cs:12–43` drives Continue → `/options`
  → D4/Enter → Up/Enter exit, asserts player/menu, no turn request, original owner
  retained and original operation receipts closed. It uses `--e2e-script` and
  redirected output: proves actual lifecycle, NOT new raw PTY input or cells.
- `ConsoleE2ESmokeTests.ConsoleE2E_MainMenuExitScript_UsesSandboxAndEmitsObservationArtifacts`
  runs actual maintained main menu/exit with Up/Enter and artifacts. Existing
  `ConsoleE2E_OptionsMenuScript_UsesScriptedInputAndEmitsMenuObservations` covers
  scripted Options. Neither substitutes for terminal-wire freshness.
- `ConsoleE2EScriptedInputTests.ScriptedKeyEventsDriveMainMenuSelectionThroughProductionKeyHandler`
  covers the actual key handler. Observation artifact/live-input tests cover their
  own typed APIs; do not claim they qualify raw terminal rendering.
- Accepted T034 fullroute now proves actual Options, local menus, Save/composer,
  archive focus/Back, SS3/modes, DSR, samePTY100 and cleanup. Do not rerun as a new
  geometry project. T033 stable consumer proves in-process Load continuation only.

## Smallest unsupported UI shape

Actual main menu at fixed100x25 (layout/title/options/default Continue and normal
Exit), plus fresh ordinary player text-input boundary. Unlike Options, main menu is
its own maintained custom renderer, not eight SelectionPrompt rows. Current live
fresh_main merely checks Continue + `8. ... Выход`, and startup only Continue;
current player check uses stripped text. These exact predicates and one-at-a-time
key dispatch need raw completion/callback-cell DATA, not a whole new engine.

Prefer reusing the existing `production-main-console-continue` fixture and its
already-genuine bootstrap/owner setup with a narrowly selected actual input adapter
mode, rather than adding a C# engine fixture. Before any implementation, inspect
its process seam and constructor requirements; existing scripted mode stays intact.
The one remaining inert wire gate would run that existing no-provider Continue→
local `/options`→main→Exit route through qualified terminal and exact new predicates.
If this cannot be an access/transport-only seam, report the concrete dependency
instead of starting a new owner framework. No fresh hosted Load test is required by
this minimal proposal.

## Adapter delta, one coherent block

Only foreground client in `live-mortal-phase.py`: samePTY100/noecho, pinned sidecar,
one fresh checked writer, original raw retained, reply-vs-route-key journal. Bridge,
daemon and RPC unchanged. Reuse qualified OwnedTerminal/PtyWriter and exact T034
Options/Save/menu consumers. Replace title-only action authority with complete raw
frame + same acknowledged cells; resolve arrows AFTER final drain. No query stripping,
no guessed CPR, no weakening of strict latest-frame/unknown-state refusal.

Add current adapter/Node/17package/deployed provenance to immutable package checks.
Use existing phase ownership to hand off work→reserved cleanup deadline explicitly;
never reset deadlines or give gameplay access to cleanup reserve. Sidecar is included
in original600/660/680/720/750 lifecycle, joined/drained before success. Preserve
child-configured vs owner-original termios distinction. Cleanup menu context still
requires exact Back/player readiness; ambiguous state remains UNKNOWN.

Prelive gate: source review of minimal transport + main/player/exit predicates,
cheap exact raw/cells/deadline/mode controls, existing no-provider actual Continue
fixture through adapter once, current package/preflight. Then separately authorized
fresh live journey proves actual Load/hosted replacement/daemon continuity, never
inferred from inert Back-only Load. No new game launched by this planning step.

---

# Exact existing-fixture seam proposal (read-only)

No new engine/owner fixture. Add one narrow mode to the EXISTING production-main
fixture: `production-main-console-continue-pty`. Existing scripted mode unchanged.
This is technical verification under existing user authorization, not a new user
permission request. Source/review/execution gates still apply.

## Source files and branches

1. `BookOfEternityClient.Tests/GmConsoleRuntimeAdmissionTests.cs`: one Fact delegates
   unchanged `ProductionMainLinuxFixture.RunAsync(newMode)`.
2. `BookOfEternityClient.Tests/ProductionMainLinuxFixture.cs`: newmode-only external
   TestDiagnosticPaths root (existing default currently writes checkout TestResults),
   copy/register exact terminal/navigation source and dependency pins; resolve the
   pinned Node and prefix explicitly. Existing package build/publish, actual
   BootstrapLocalStorageAsync, console-prefix current-schema DATA, native owner and
   guardian preparation remain unchanged. No new seeded accepted turn/world.
3. `BookOfEternityClient.TestSupport/OwnedTerminalScenarioDriver.cs`: current
   `mode==production-main-console-continue` dispatch also admits the exact new mode,
   passing a boolean transport choice to the same existing continuation helper.
4. `OwnedTerminalScenarioDriver.ConsoleRuntime.cs`: original script generation and
   redirected child path stay in old branch. New branch invokes a single maintained
   Python navigation adapter with ship/root/evidence paths; it starts the SAME
   actual client command without `--e2e-script`/scripted input. After child result,
   reuse existing common no-request/original-root-alive checks, actual original
   closed-pin receipts and same scoped shutdown RPC. Do not add reflection; existing
   reflection receipt tail is inherited unchanged, not duplicated into new code.
5. One small `SelectionPrompt/terminal/console_navigation.py` source caller:
   qualified OwnedTerminal/PtyWriter; real raw main/player/Options predicates,
   Continue→local `/options`→numeric4/Enter→main Exit navigation, ordinary normal
   child exit/EOF. Share Options and transport code already qualified. No Load/Save,
   no GM, no provider, no arbitrary key script. Publish no fabricated observation.
6. Existing narrow Python predicate test file (or one explicitly owned DATA file)
   and one catalog entry. Main/player shape source mapping must be finalized before
   implementation; no copied UI renderer, private GameEngine access or cloned menu.

## Reused unchanged APIs/data/ownership

`ProductionMainLinuxFixture.RunAsync` already builds exact neutral CLI/native
package and current app, configures real original owned main, then calls
StateManager.BootstrapLocalStorageAsync. Its console-prefix seed is deliberately
Chaos Sea/currentIncarnation0 plus existing player_status/world_state, no accepted
Mortal turn. New mode shares that prefix and does not invent generation JSON.
OwnedTerminalScenarioDriver.RunBridgeAsync already acquires original owner, starts
server loop, checks retained worker inventory and dispatches continuation.
RunActualConsoleContinueAsync already checks missing output, actual no-request,
original root still live, original identity/ClosedObserved operation receipts,
then scoped shutdown. Existing final disposal/guardian/result handling remains.
The neutral fixture currently uses setReady as its existing test arrangement;
this is NOT permission to change live readiness or claim GM qualification.

## Bounds and current caveats

Original neutral scenario guardian30s / observer35s remains; select a small client
navigation work reserve inside it, with sidecar cleanup included. The previous
scripted client helper waits8s; do not silently inflate or move that bound before
reviewing measured setup requirements. No live45/55/60 rerun required for this seam.
Current fixture PATH is restricted to player-bin dotnet/pwsh; Python and Node must
be exact absolute argv, no ambient command discovery or network during test.
External source/package copies must contain the imported modules without exposing
checkout to child; use existing fixture copy machinery and explicit files only.

This one no-provider wire case covers the missing main/player/normal-exit topology.
It does NOT duplicate hosted Load. T033 and existing relay ownership proofs remain;
actual hosted Load/retained-daemon continuity stay required live acceptance under
unchanged nonce/generation/identity/receipt gates.

## Authorized source block

Implement one transport-only existing Continue fixture mode and foreground-client
only live adapter. No execution until saved independent source review. All client
inputs share the checked audited writer. Reserved cleanup is one-way, bounded by
the original absolute cleanup deadline, never a reset or gameplay extension.
Unknown state/partial write remains no-replay. Bridge/daemon/RPC unchanged.
