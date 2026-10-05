# Reproduce the cross-platform development environment

## Detached workspace byte component (T041-WORKSPACE)

Exact Linux source `f8c7eeae24cf702b87f581f442ea7c720264bc34` passes47/47:
44 isolated filesystem cases and three exact source/operational guards. This
covers create, pinned byte staging, bounded proposal/contentRef reads, cancellation,
partial-failure cleanup and disposal. It launches no host, worker, CLI or GM.
[Qualification and raw command/log manifests](recovery/worker-workspace-qualification.json)
separate both causal REDs, the intermediate GREEN, Windows source correction,
final fresh builds/GREEN, discovery-only audit and restoration. Independent
Sol6.1/xhigh complete final source/runtime/evidence review PASS at carrier
`7309ee4b37666a7c8f4b8b239946b04dc2cacf38`, with no unresolved findings.
Final verdict metadata is published and freshly restored after recording this
verdict; its exact carrier SHA is supplied in handoff.

```sh
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-detached-workspace -Parallelism 1 -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-detached-workspace -Parallelism 1 -NoBuild
pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild
```

Use the documented SDK10/runtime8/PowerShell7 environment below. NoBuild requires
the fresh selected build. Audit performs discovery only. Linux-specific link/FIFO
fixtures do not qualify Windows. Linux ordinary hardlinks are read/unlinked only;
Windows retained single-link identity is unchanged. Neither platform receives a
new hostile-owner protection guarantee. Existing load/storage code remains intact.

`GmWorkerProcessTreeFactory.Attach` still rejects non-Windows before Release:
complete descendant ownership and authenticated confirmed-stop qualification are
the next lifecycle prerequisite. Quarantine receipt handle publication is also
still Windows-only. Do not enable execution, import/apply, quarantine publication,
PTY or live GM from this component result; no production gate is relaxed.

## Worker environment case semantics (T041-ENV)

Source `43b60b954265e3bb404bf562cf7b9ff13dc4f953` passes 16/16 synthetic environment
checks on Linux. [Qualification](recovery/worker-environment-qualification.json)
separates capture/strict JSON, pure host reconstruction and two actual hidden-host
Ready/owner-close rows without Release. Independent **gpt-6.1-sol/xhigh**
implementation/final evidence review passed with no actionable findings.
Linux keeps case-distinct names; Windows retains case-insensitive map semantics.
This fixes the earlier inherited HTTP_PROXY/http_proxy capture collision without
filtering variables or changing inheritance/network/security settings. All test
payload values are synthetic; do not print or archive real environment values.

Use the same SDK10/runtime8/PowerShell7 and process-local short TMPDIR below:

```sh
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-host-environment -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-host-environment -NoBuild
pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild
```

NoBuild requires the fresh successful selected build. Audit executes zero tests.
Ready is before worker StartInfo reconstruction; the latter is tested purely.
Windows native execution and successful worker Release remain unqualified. Full
IPC34, FRAME49 and run-record90 need a separate source reason before repetition.
The older IPC qualification below retains its source-specific environment limits.

## Linux worker-host IPC admission (T041-IPC-LINUX)

Native Linux source `9a346308d02d55ddb50a49fab109fc99b8871505` passes 34/34;
separate **gpt-6.1-sol / xhigh** final implementation/evidence review passed. See the exact
[qualification matrix](recovery/worker-ipc-linux-qualification.json) and
[current plan](plan.md#t041-ipc-linux-admission--2026-10-05-verified-component).
This is the later both-channel SO_PEERCRED PID/effective-UID adapter and controlled
pre-Release host qualification. The historical FRAME checkpoint below retains its
original platform boundary; it is not the source of the new Linux claim.

Use a separate checkout at that exact SHA, .NET SDK 10/runtime 8 and PowerShell 7.
The actually tested environment is Debian13.6 x86_64, SDK10.0.401/runtime8.0.31,
PowerShell7.6.6, effective UID1000, normal permissions. Configure the existing
process-local telemetry/cache setup below without changing HOME. Linux named-pipe
names plus TMPDIR must fit Unix-domain socket length; the verified short directory
is `/workspace/ipc-tmp`:

```sh
mkdir -p /workspace/ipc-tmp
export TMPDIR=/workspace/ipc-tmp
export TMP="$TMPDIR" TEMP="$TMPDIR"
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-host-ipc-admission -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-host-ipc-admission -NoBuild
pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild
```

Only the first selected build permits the following NoBuild calls. Catalog audit
executes zero tests. Preserve plan, summary, TRX, exact source and cleanup. Stop
and report an actual permission denial; do not alter security or transport to
bypass it. No credentials, new accounts or elevated privileges are prerequisites.

The fixture worker payload owns a minimal environment and an absolute PowerShell
canary executable; the hidden host retains ordinary inherited environment. Existing
production payload capture still rejects Linux inherited case aliases such as
HTTP_PROXY/http_proxy through its case-insensitive dictionary. General worker
execution/environment portability is not established by this IPC qualification.
The actual host only reaches Ready and exits125 on owner close without Release.
Linux process-tree/workspace and main PTY/live-GM gates remain closed. Windows,
real different-UID peers and successful worker Release require separate evidence.
Unchanged FRAME49 and run-record90 are not part of this execution selection.

## Persistent-main record component (T041-RUN-RECORD)

Exact source: `dc8c742cb3dc86d6cff09dadbbb412c77ae779a6`, tree
`4d1f0e595983dab702692f7804e8357607c7784a`. The component is internal and unwired:
it does not enable a GM process or make current rollback/cleanup safe. Its
[qualification](recovery/gm-run-record-qualification.json) records 90/90 pure and
isolated ordinary-file checks, separate Sol/xhigh review and clean source recovery.

With PowerShell 7, SDK 10 and .NET 8 runtime in a fresh exact-source checkout:

```sh
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category gm-session-run-record -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category gm-session-run-record -NoBuild
```

Use the second command only after a successful fresh selected build. These tests
create only owned ordinary files and exercise strict bytes/schema, every identity
field, retained stop/reboot evidence, cold Uncertain, trusted root/backend/generation
and terminal epoch decisions. They launch no worker/pipe/provider. Do not delete a
record or invent Missing to gain admission. Cold decode is not live-ownership proof.
A satisfied main-slot decision still requires independent worker-owner conditions
and canonical/held-lease ordering before any actual mutation.

## New saved Linux environment: capability only

At source `c706e2c3efa2c360358f99f1495f11be53c764a5`, a separate owner-authorized
saved Linux environment now has Debian 13.6, SDK 10.0.401/runtime 8.0.31,
PowerShell 7.6.6 and Spec Kit CLI 1.0.13. Its one normal-permission standalone probe
constructed both Byte/Asynchronous/CurrentUserOnly channels, connected local clients,
transferred one byte per channel and closed/unlinked them. Both servers and both
clients ran in **the same PID 1860**. This is not cross-process or expected-host
PID/UID authentication, and no application test category ran there.

The [exact source/log/install/review archive](recovery/evidence/linux-named-pipe-capability-20261005/source-and-evidence.tar.gz)
and [manifest](recovery/evidence/linux-named-pipe-capability-20261005/manifest.json)
retain provenance and limits. Inspect archive entries/checksums before extraction;
it contains no binaries, caches, certificate material or links. Do not rerun the
successful capability probe unchanged. The earlier restricted-cloud refusal and
its stopped diagnostic remain historical evidence, not a reason to alter security.

The next native prerequisite is still the separately reviewed Linux peer adapter
on both channels before Launch. Use the saved environment for a new exact-source
bounded `worker-host-ipc-admission` qualification only after that implementation and
coordinator handoff. Preserve Linux process-tree/workspace guards and require later
persistent-main fence/PTY/lifecycle work before a live GM/console session. The
separate-PC live checklist below remains applicable; this new environment capability
does not replace any live or native Windows qualification.

## Worker-host FRAME checkpoint and separate Linux-PC handoff

The source-qualified component checkpoint is
[`e9f9452deb296988a0ced6feb9b8003b54449a19`](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/e9f9452deb296988a0ced6feb9b8003b54449a19),
tree `f93412342654f05d7d0eaa07f29a23fb10afd996`. Its pure frame/protocol/operational
source-guard selection passes **49/49**; this is not native IPC or a live GM pass.
The [qualification record](recovery/worker-frame-qualification.json) and current
[plan](plan.md) separate source, review, publication and native gates. Later evidence
carriers do not change this runtime identity unless the record explicitly says so.

### Reproduce the permitted pure checks

Use a new checkout on the intended machine, .NET SDK 10 plus .NET 8 runtime and
PowerShell 7. Follow the environment and process-local telemetry setup below;
do not change HOME, credentials, security settings or global environment settings.
No provider login, game session or external credentials are needed for this owner.

```sh
git clone --single-branch --branch codex/1553-load-filesystem https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git boe-frame-check
git -C boe-frame-check checkout --detach e9f9452deb296988a0ced6feb9b8003b54449a19
git -C boe-frame-check rev-parse HEAD
git -C boe-frame-check status --porcelain
cd boe-frame-check
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-host-frame-contract -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category worker-host-frame-contract -NoBuild
```

The final status command must be empty. `-NoBuild` is valid only immediately after
the fresh successful selected build. This owner runs only strict JSON/framing,
controlled in-memory stream and documentation assertions. It creates no native
pipe or worker command. Retain the exact SHA, OS/architecture/toolchain, plan,
summary, TRX, counts and cleanup. Use discovery-only `-ValidateCatalog -NoBuild`
when validating ownership; it does not run the full suite. Never run a blanket
suite, Fast/PreMerge, all categories or unrelated accepted storage cohorts.

### Native IPC remains a separate prerequisite

At this checkpoint Linux host peer authentication is intentionally **not implemented**:
the original Windows-only identity guard remains. Linux process-tree admission and
detached execution-workspace authority also remain closed. Running the native owner
on another Linux PC now does not turn those gates into supported behavior.

After a separately reviewed Linux peer adapter is implemented at a new exact SHA,
qualify `worker-host-ipc-admission` on that machine through its own PlanOnly/build
and bounded execution. Require SO_PEERCRED PID/UID on **both** channels before any
Launch bytes; preserve Windows GetNamedPipeClientProcessId regression separately.
Check actual host authenticated Ready followed by owner close **without Release**,
bounded host exit and zero controlled worker starts. Foreign control, foreign status
and both foreign peers must receive zero Launch bytes. Verify native framing,
invalid UTF-8/EOF/nonce/schema, writer backpressure, absolute deadlines and cleanup.
Report native Windows and Linux independently. Do not use early-return OS passes.
Do not replace the transport or alter paths/security merely to evade a denial.

The existing cloud refusal occurred in `Socket..ctor` before bind or authentication:
`System.Net.Sockets.SocketException: Permission denied`. Numeric errno, actual
socket pathname and policy cause were not captured. Nine affected rows are
unqualified environmental failures, not behavioral RED and not passed tests.
No retry or alternative native transport was used in this work.

### Bounded local input lifetime (T042-INPUT-LIFETIME)

The controlled `gm-bridge-input-lifetime` category builds and invokes the actual
bridge writer, keyboard pump, stop path and connected-request error boundary with
synthetic streams/keys. It starts no terminal or provider. Use the current exact
qualified source in [the input qualification](recovery/gm-input-lifetime-qualification.json)
and `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category gm-bridge-input-lifetime -Parallelism 1`.
A fresh selected build is required before `-NoBuild`.

Every input origin keeps its local stream lifetime. Retirement rejects queued
writes and waits for actual old writes/flushes, cancellation callbacks and the
keyboard task before replacement admission. A five-second managed-drain timeout
retains those tasks and keeps replacement blocked; this is not a native-process
stop guarantee. A started write/flush fault or cancellation records
`lastInputWriteError` with possible partial-delivery uncertainty and revokes that
input lifetime. Do not interpret that diagnostic as a daemon delivery disposition,
permission to replay or confirmed native stop. Host shutdown ends control service;
shell cancellation receives an ordinary failure response while service continues.

This slice supplies no durable run/generation/fence authority. Paste-observe-submit
coordination, manual arbitration, readiness/unknown-screen/autotrust policy,
provider lifecycle, Linux PTY and live-GM qualification remain pending. Existing
readiness automation has not been redesigned; a visible idle screen does not clear
the retained input-write diagnostic or re-enable the revoked writer.

### Persistent GM and live console checklist, after implementation gates

A separate Linux PC and a working named pipe are not sufficient prerequisites for
a live GM run. First implement and qualify the planned durable run/generation/epoch
admission fence, detached-workspace portability, owned Linux supervisor/subreaper/
PTY and authoritative descendant reaping, plus main bridge/daemon/launcher platform
adapters. Root PID exit or PTY EOF cannot establish confirmed stop. Unknown ownership
must retain the workspace and block rollback, cleanup, replacement and unsafe restart.
The existing Windows-only main bridge and process gates must not simply be removed.

Then qualify persistent input with controlled fixtures: manual draft preservation,
one atomic paste-observe-submit operation, configurable paste/submit/interrupt/exit,
fragmented UTF-8, readiness, multiple turns in the same CLI, queue cancellation,
timeout/ambiguous delivery without automatic replay, owner loss and verified stop.
Unknown authentication/trust screens pause automation. No one-shot replacement,
auto-login or automatic trust acceptance is authorized by this checklist.

Only after those gates pass at a separately recorded source may the normal console
client and integrated daemon be used for a real persistent CLI → accepted turn →
save → typed Load → full restart scenario. Use the then-current verified launcher
instructions and an owned disposable session; there is no qualified Linux live-GM
launch command at the present checkpoint. Historical OpenCode mini/pure evidence
is a reusable profile, not proof that the current integration works. Preserve
accepted state/history and prove confirmed stop before rollback or folder reuse.
Browser qualification remains automated client/backend code testing; no live-browser
or visual-QA gate is added. Windows equivalent capabilities require their own runs.

The owner's 2026-10-05 decision defers full Linux live testing to another PC.
This checklist does not waive qualification or authorize access to that machine now.

## Browser load continuation (T032-B4)

Browser loading uses the typed four-state result on every business HTTP outcome.
`success` means commitment only. After Committed, both actual browser load handlers
request `/api/saves/load-state` for its exact `establishedGeneration`; only one complete
menu/session/game/settings/audio bundle permits navigation. Unknown/lost responses,
stale ownership or failed required refresh stop the whole shell without automatic retry.
A known safe NotLoaded may explicitly reconcile current existing authority; a confirmed
rollback binds its restored generation. A recognized empty chapter is explicit, never
a substitute for a failed required read. Nonblocking follow-up remains visible across
navigation and names its historical load rather than a later current save.

After a stop, preserve the source archive and retained storage evidence. Resolve the
reported local storage/recovery problem, then restart/reload the client to inspect the
reconciled state. Do not retry load merely because a response was lost or HTTP was not200.
No manual journal deletion or generation recreation is part of this procedure.

Focused browser owners are `portable-load-browser` (coordinator/menu/typed transport),
`portable-load-browser-refresh` (actual HTTP required bundle) and
`portable-load-browser-handlers` (actual TSX handlers/provider and refresh publication).
Use PowerShell7 with the existing runner and the selected owner, or the current explicit
`tests/selection.json` when qualifying all changed client consumers. Browser proof is
code-level automated testing only; live runs use the console. Native Windows public
clients, live console/GM and whole-platform acceptance remain separate open gates.


Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)

## Console typed-load continuation (T032-B4)

The console carries the loader's four-state decision through runtime and required
settings/UI refresh. `Сохранение загружено` means the replacement was committed;
a later warning does not undo it. `Загрузка отменена. Прежняя глава восстановлена`
means exact rollback, while a refused load leaves the chapter unreplaced.

If the console says `Продолжение остановлено`, leave the game and restart normally
so ordinary storage recovery and session validation can reconcile retained evidence.
The current process stops its existing loop, refuses a new loop or repeated load,
and offers only **About** and **Exit** in the main menu. Do not blindly repeat the
load, delete a retained journal, or infer safe retry from the legacy bool API. An
unresolved outcome has no confirmed replacement generation. If restart still blocks,
retain the original error/evidence and investigate it before a new canonical action.

Reproduce only the affected console category from the verified branch:

```powershell
./scripts/test-csharp.ps1 -Category portable-load-console -PlanOnly
./scripts/test-csharp.ps1 -Category portable-load-console
```

Native Linux component/menu-handler proof is recorded at the start of [plan.md](plan.md).
It does not establish a live interactive console/GM scenario or new Windows execution.
Bounded browser typed transport/handlers are now accepted separately (see the opening of plan.md); full B4/T033/B5 and native Windows public-client/live console-GM gates remain open. Per the owner’s
2026-10-04 decision, browser verification is through automated actual client/backend
tests only; live runs use the console client. No live-browser/visual-QA gate or cloud
access workaround is required, and automated checks are not described as visual QA. Do not repeat the
unchanged filesystem/resource cohorts for this client-only block. GM-authored output
and game schema are unchanged; this is client-owned continuation and admission.

## Current local load-filesystem continuation

Branch: `codex/1553-load-filesystem`, based on `5d2aa2ceadd8f4424e3ccaf0249a8bb164f32fae`.
Approved scope: [spec revision 1](spec.md#local-load-filesystem-continuation--revision-1-2026-10-03),
[execution plan](ordinary-load-plan.md) and v3 design; the owner waived further
spec/plan/revision approval during autonomous work.

The returned native Windows proof retains its exact sources. Native Linux now has
actual bounded admission/metadata/path/namespace/cold/resource evidence, including
the causal original-name and sampler corrections. All five current-probe resource
commands passed 12/12 with 28 measured children; catalog 221/10570 discovery and five XML
assemblies passed. See the [current handoff](load-five-fixes-handoff.md),
[Linux qualification](recovery/load-filesystem-linux-qualification.json) and
[full GitHub readback](recovery/load-filesystem-linux-github-readback.json).
Final separate Sol 6.1 XHigh review passed at `6133bfed`; the scoped filesystem
B1/B2/B3/B5-FS gate is accepted after complete proof and fresh GitHub restoration.
Full public B4, T033/live/full B5 and whole #1553 remain open; bounded A4 producer
qualification is recorded below at its later exact source.

Restore the current branch into a fresh directory and compare complete SHAs:

```sh
git clone --single-branch --branch codex/1553-load-filesystem https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git boe
cd boe
git rev-parse HEAD
git ls-remote origin refs/heads/codex/1553-load-filesystem
git status --short
```

Read AGENTS.md, docs/development-workflow.md, docs/testing.md and this feature's
spec/plan/tasks. Use PowerShell 7, SDK 10 and runtime 8. Set supported telemetry opt-outs
before starting tools; also set the recorded compiler/environment controls:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:POWERSHELL_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_PROCESSOR_COUNT = '1'
$env:GenerateDocumentationFile = 'true'
$env:NoWarn = '1591'
./scripts/test-csharp.ps1 -SelectionFile tests/selections/1553-load-linux.json -Parallelism 1 -TimeoutMinutes 15
```

That small Linux selection now has 71 cases: entry/admission/outcomes/lease/native
name controls plus the narrow sampler controls. Its evidence is a source-specific
union of separate runs, not a new 71-case run. Metadata, fresh path/batch,
namespace/native/cold, compatibility and five separate resource phases now have
native Linux evidence in the [handoff](load-five-fixes-handoff.md). Select only
actually affected owners for a future change; do not replay qualified groups solely
for handback or unchanged B4 UI work.
Use `-NoBuild` only after a fresh required build with unchanged compile inputs.
The limited Linux workflow is prepared with exact-source checks and retained artifacts.
The current native evidence was obtained directly on the cloud Linux executor;
no GitHub Actions execution is claimed or required as a substitute for those runs.

The remaining sections preserve source-specific historical setup and evidence.

Bounded T030-G backup lifecycle is accepted with normal Git source, a verified 94-case Linux union across separate 43/50/19 cohorts and one actual 30/30 Windows run. Follow the [accepted plan](plan.md#accepted-t030-g--ordinary-backup-lifecycle) and [normal recovery recipe](recovery/README.md#accepted-t030-g-backup-lifecycle-recovery); no active packet is needed. Its real Linux quarantine body passed in the 50-case continuation. Native quarantine was not selected in the T030-G Windows subset. These bounded results do not establish full gameplay, whole-preparation/accepted-turn atomicity, save/load or remaining B4/B5 portability. Earlier T030-F/B2/B3 evidence below remains historical and tied to its named source.

<a id="restore-source-first"></a>

## Historical source restoration (T030/T031)

```sh
git clone --single-branch --branch 1553-cross-platform-runtime https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git boe
cd boe
git rev-parse HEAD
git ls-remote origin refs/heads/1553-cross-platform-runtime
git status --short
```

Compare the complete SHAs; read AGENTS.md, docs/development-workflow.md and this feature's spec/plan/tasks before editing. No local-only code cache is needed. Application/test execution is separate evidence from successful download.

For T030-F, restore normal source and verify the 17 delivery identities as described in the recovery recipe. Do not replay retired patches or an old selection. The actual Linux/native selections and their exact saved outcomes are linked in the plan; passing checks are not repeated solely for publication or docs. Historical B3b handle-comparison evidence remains at [its accepted checkpoint](recovery/README.md#accepted-b3b-windows-handle-comparison-correction).

The accepted B2 menu source and catalog are normal Git blobs. The B3a scaffold catalog was normalized at e69c1668; the accepted B3a reader at `740b4d09beb2d59a7866a6919200595d0f4c74de` also has normal source/catalog blobs and requires no patch application. Its 24-case behavioral RED and 138-case common-reader covering result are preserved; the restored legacy commit-reader comparison and existing source guard also have focused GREEN results, with final ownership/selection audited. Independent Astra XHigh accepted the exact patched source and equivalent normal tip for spec compliance and code/test quality; no B3a Windows execution is claimed. After SHA verification, follow the [current source/catalog recovery procedure](recovery/README.md) to confirm the exact reader/catalog/selection identities and read the plan. The menu implementation has targeted Linux automated evidence recorded below and in the plan. Connected independent B2c review and the literal-only P3 readback are accepted through 88a02f36; a separate ordinary Linux live PTY setting/restart check passed. Five scripted Windows console checks also passed at immutable 88a02f36, as recorded in the plan and recovery/evidence/b2c-windows-20261001/. This is bounded startup/settings evidence; Windows PTY/browser/GM, B3a Windows execution and full-game behavior are not established by those five checks.

## Tool versions actually verified on Debian 13 x64

- .NET SDK 10.0.401; .NET and ASP.NET runtime 8.0.31
- PowerShell 7.6.6
- Node 24.19.0 and npm 11.9.0
- Spec Kit CLI 1.0.13; repository Codex skills integration already present

Before **any** tool startup (including first version/help checks) set the three supported opt-outs in the launching task/shell. Set the SDK opt-out before installation too: the installer has its own installation telemetry entry. These are per-task/per-invocation application settings, not global OS/security/network changes. Sources: [.NET SDK/installer telemetry](https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry), [testing-platform telemetry](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-telemetry), [PowerShell environment variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_Environment_Variables). Do not substitute a banner-suppression flag for telemetry opt-out.

Install from official sources without privileged system changes. For example, use a writable TOOLCHAIN directory outside the checkout:

```sh
export DOTNET_CLI_TELEMETRY_OPTOUT=1 POWERSHELL_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export TOOLCHAIN="$HOME/boe-toolchain"
(
set -eu
mkdir -p "$TOOLCHAIN"
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$TOOLCHAIN/dotnet-install.sh"
bash "$TOOLCHAIN/dotnet-install.sh" --version 10.0.401 --install-dir "$TOOLCHAIN/dotnet" --no-path
bash "$TOOLCHAIN/dotnet-install.sh" --version 8.0.31 --runtime aspnetcore --install-dir "$TOOLCHAIN/dotnet" --no-path
curl -fsSL https://github.com/PowerShell/PowerShell/releases/download/v7.6.6/powershell-7.6.6-linux-x64.tar.gz -o "$TOOLCHAIN/powershell.tar.gz"
printf 'ddbc4a2d113bbd46d283cfedcbcd117a70caefd7673f41f2b4e0000badf103bc  %s\n' "$TOOLCHAIN/powershell.tar.gz" | sha256sum -c -
mkdir -p "$TOOLCHAIN/powershell"
tar -xzf "$TOOLCHAIN/powershell.tar.gz" -C "$TOOLCHAIN/powershell"
chmod u+x "$TOOLCHAIN/powershell/pwsh"
uv tool install specify-cli==1.0.13
)
```

Official references: [.NET scripted installation](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual), [PowerShell release](https://github.com/PowerShell/PowerShell/releases/tag/v7.6.6), [Spec Kit](https://github.com/github/spec-kit), [Superpowers](https://github.com/obra/superpowers), [project bridge](https://github.com/StanislavSmetaninSSM/spec-kit-superpowers-bridge). Installed agent skills must be checked in the actual execution surface; a CLI version does not prove a skill is loaded. Do not overwrite existing Spec Kit scaffolding merely to repeat setup.

In restricted environments choose writable home/cache paths explicitly:

```sh
export DOTNET_ROOT="$TOOLCHAIN/dotnet" DOTNET_CLI_HOME="$TOOLCHAIN/home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 POWERSHELL_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export XDG_CONFIG_HOME="$TOOLCHAIN/config" XDG_CACHE_HOME="$TOOLCHAIN/cache" XDG_DATA_HOME="$TOOLCHAIN/data"
export NUGET_PACKAGES="$TOOLCHAIN/nuget" NUGET_HTTP_CACHE_PATH="$TOOLCHAIN/nuget-http"
export NUGET_SCRATCH="$TOOLCHAIN/nuget-scratch" NUGET_PLUGINS_CACHE_PATH="$TOOLCHAIN/nuget-plugins"
export PATH="$TOOLCHAIN/dotnet:$TOOLCHAIN/powershell:$PATH"
mkdir -p "$DOTNET_CLI_HOME" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$XDG_DATA_HOME"
dotnet --info
pwsh -NoLogo -NoProfile -Command '$PSVersionTable.PSVersion.ToString()'
specify version
specify integration list
npm ci --prefix BookOfEternityClient.WebFrontend
```

In this restricted executor, the default parallel MSBuild restore failed without a diagnostic; `export DOTNET_PROCESSOR_COUNT=1` allowed the normal category runner to build/discover successfully. Use this recorded environment workaround when reproducing that failure, without changing category selection or budgets. It is not a claimed speed improvement or a requirement for ordinary user machines.

## Verification

Read docs/testing.md and select categories for the actual block. Use scripts/test-csharp.ps1. The B1 categories `portable-storage-paths` and `portable-storage-publication` already exist; later cutover categories and the actual reviewed selection are recorded in tests/categories.json and tests/selection.json. `-ValidateCatalog` discovers ownership without executing tests. Never use a full-suite/aggregate/all-category run. Store meaningful result counts and source SHA remotely in plan.md, not only ignored TestResults.

Current user-approved acceptance uses real console process/menu/settings/restart checks and automated browser-client checks. Under the 2026-10-04 owner decision, browser behavior is verified through automated actual handler/component/backend tests; no live-browser access or visual-QA gate is required. Use separate fresh roots for live console checks and automated browser checks. Change one ordinary setting, stop the full client process, restart and inspect the retained value; then verify real GM turn, save/load and failure/conflict recovery. Exact accepted command/scenario evidence is added as each block becomes runnable; automated checks are never relabelled live browser execution.

## Windows owner handoff

Before launching `pwsh`, `dotnet`, their first version/help checks or an SDK installer, set the opt-outs in the parent Windows Command Prompt session (do not use global `setx`):

```cmd
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set POWERSHELL_TELEMETRY_OPTOUT=1
set TESTINGPLATFORM_TELEMETRY_OPTOUT=1
```

Use the exact published acceptance SHA with supported .NET/PowerShell/Node versions. Run the same selected storage/path/recovery categories, then console and browser startup-setting-restart, real persistent CLI/daemon turns, cancellation/restart, core game turn, save/load, audio and clipboard. Report Windows version, filesystem, terminal/provider and exact failures. These checks are not marked passed from Linux evidence.

The historical `-Category portable-storage-windows-paths -Parallelism 1` run executed six native bodies at 830e160d, with four passes and two failures. The subsequent seven-case handle-comparison correction run above passed at e5ee470e and supports bounded B3b acceptance. These separate runs cover selected local filesystem cases, not actual UNC-share or full-game/GM/browser behavior. Pure spelling cases and Linux early returns remain policy evidence only; failed link-fixture setup is never a product pass. Do not rerun accepted cohorts solely for documentation or recovery.

Path grants use exact spelling on both OSes, including Windows folders configured as case-sensitive. Ambiguous Windows names (trailing dots/spaces, alternate streams and device names) are rejected before normalization. Windows symlink fixtures need permission to create their links; a fixture setup failure is not a product-coverage pass, and this workflow does not change Windows security settings automatically.

Supported extended Windows drive/UNC spellings are canonicalized to ordinary drive/UNC paths. Other device namespaces are rejected. Denying a file target that aliases a declared directory root is conservative on Windows; this does not broaden any file or directory grant.

## B1 publication engine verification

The focused `portable-storage-publication` category covers the common member journal and test-only executable crash fixture. Run it through `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category portable-storage-publication`; the runner builds its fixture dependency, then executes the cases recorded for the current B1b checkpoint in plan.md. Abrupt-exit cases start a fresh process for recovery and retain no shared mutable test root. `-ValidateCatalog` checks ownership by discovery only. These tests do not launch either game client or establish settings/gameplay acceptance.

The v1 journal uses a new private runtime directory, flushed create-new sibling stages, complete member preflight and an atomic group commit decision before cleanup. It requires the existing canonical lease, explicit generation and safe existing destination parents. Only exact explicitly granted external destinations are permitted; derived private sibling names do not grant access to other external files. The integrated ordinary writer continues to require old-format evidence to use its original supported handler or block with evidence retained. Do not manually delete an unresolved journal to make startup appear successful.

Windows owner checks for the review corrections: repeat publication/bootstrap/recovery against an ordinary drive-root spelling and its supported extended `\\?\C:\...` spelling, and against an available UNC share with both ordinary and extended `\\?\UNC\server\share\...` spellings. Confirm generation members are recognized and equivalent journal/lock paths remain excluded. The 8 pure Windows spelling cases run on Linux establish comparison policy only; they do not execute a Windows filesystem. Generation encoding cases cover BOM-marked UTF-8, UTF-16 LE/BE and UTF-32 LE/BE with exact-byte bootstrap/transition/rollback, matching the current generation reader.


## Isolated verification after an inconclusive run

Never infer cleanup from an inaccessible process view or silently overlap a vanished run. The B2c execution-control denial and the later authorized isolated attempt are separate evidence in plan.md. The successful execution attempt used a fresh exact-SHA checkout with the apply-once catalog patch, a new writable `RUN_ROOT` for HOME/CLI/NuGet/XDG state and `TMPDIR`/`TMP`/`TEMP`, and only shared official tool binaries. Its one PowerShell process asserted `.NET GetTempPath()` equals that new temp root and all three opt-outs are `1`, then invoked the same selected canonical runner. No prior fixture, build output or fixed port was reused. This is a recorded, specifically authorized recovery procedure, not permission to evade another denial or force an unknown lease.


## Accepted B2c console checks

At source `48357174ccdada168b31545b2057e0f5a0f6db99`, the saved two-category selection passed **37/37** cases, including five scripted real-process checks; the unchanged preparation boundary had already passed in the prior 42-case run. `pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -Parallelism 1` produced the covering result. `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild` then validated 113 categories / 10,301 methods or files without executing tests. All invocations inherited the three opt-outs and isolated writable environment described above. Sanitized original artifacts and hashes are under `recovery/evidence/b2c-connected-green/` and `b2c-final-audit/`.

For a separate live PTY check, create a disposable base directory, copy the repository's synthetic `FileSystemExample/game_session` into its `game_session` subdirectory and copy `BookOfEternityClient/system_guardians` into its `system_guardians` subdirectory. Seed `game_session/config.json` with:

```json
{
  "language": "ru",
  "musicEnabled": false,
  "soundEnabled": false,
  "gmBridgeEnabled": false,
  "gmBridgeAutoStart": false,
  "gmWorkerBridgeProfiles": [],
  "generateSceneImages": false,
  "enableQteEvents": true
}
```

After loading the safe task environment, use the built application as the PTY's terminal process; the base directory is the existing positional argument:

```sh
exec dotnet "$REPO/BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll" "$LIVE_ROOT"
```

Use the terminal's actual advertised capabilities; do not override TERM. Omit scripted-input, agent-console and browser flags. Observe the ordinary menu, enter settings, toggle QTE, leave through the normal save point and exit. Inspect config and GM projection before restarting the complete process against the same isolated root, then confirm the retained value. W/S and Enter are supported main-menu navigation when appropriate for the terminal. No surviving shell receives input after application exit. This scenario passed separately on Linux at the frozen source above with actual TERM=dumb, QTE true → false, two normal exit-0 processes and all three member hashes unchanged after the immediate save through restart. The [sanitized observations and PTY text transcript](recovery/evidence/b2c-live-console/) retain the exact evidence and limits. Full new-game initialization, GM turns and save/load remain B3/B4 work.


## T030-F isolated verification environment

Keep `HOME`, `home` and `CODEX_HOME` unchanged. Do not source historical environment scripts that redefine them. Use the existing official tool binaries via explicit `DOTNET_ROOT` and `PATH`; set `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `POWERSHELL_TELEMETRY_OPTOUT=1`, `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` and `DOTNET_PROCESSOR_COUNT=1` before startup. Allocate a new owned run root for `DOTNET_CLI_HOME`, `XDG_CONFIG_HOME`, `XDG_CACHE_HOME`, `XDG_DATA_HOME`, `NUGET_HTTP_CACHE_PATH`, `NUGET_SCRATCH`, `NUGET_PLUGINS_CACHE_PATH`, `TMPDIR`, `TMP` and `TEMP`. Reuse only the existing immutable package cache with `NUGET_PACKAGES`. For a justified Linux reproduction, the reviewed `tests/selection.json` plans 28 methods / 58 cases / five descriptors: use `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -Parallelism 1` for discovery only, and omit `-PlanOnly` to execute. `-NoBuild` requires a fresh successful build of all selected projects at unchanged source. The separate `-Category portable-directory-backup-prerequisite` owns the recorded Linux failure; it is transparently excluded from the passing Linux selection, without an OS early return or fixture bypass. The initial three-case RED is historical evidence from its named pre-port source, not a current invocation target.

The exact completed Windows command and 30/30 result are in [normal recovery](recovery/README.md#accepted-t030-f-recovery). Use its Windows selection only for a justified native rerun. Neither a zero-test plan nor source recovery constitutes behavioral execution.

## Current native save names qualification — 2026-10-05

T032-A4 runtime `ddd86922` passed actual current Save → typed Load → Save for
Linux leaf/directory case pairs with exact bytes, manifested hashes and canonical
fixed authorities. The four new cases require Linux; they must not be selected by
the default Windows job. Use the existing Linux workflow selection:

```powershell
./scripts/test-csharp.ps1 -SelectionFile tests/selections/1553-load-linux.json -PlanOnly
./scripts/test-csharp.ps1 -SelectionFile tests/selections/1553-load-linux.json -NoBuild
```

That selection contains36 affected cases (new4, original-name8, fixed-alias24).
Default `tests/selection.json` contains only the32 Windows-compatible affected cases.
PlanOnly/discovery on Linux does not qualify Windows execution. Do not repeat the
passing behavior solely for restoration or a selection-only edit. See the current
[plan](plan.md) for the final independent PASS, bounded acceptance and exact delivery requirements. Existing
Windows collision policy and archive format/hash/budgets remain unchanged; native
Windows public-client/worker and real GM/live console/full gameplay remain open.
