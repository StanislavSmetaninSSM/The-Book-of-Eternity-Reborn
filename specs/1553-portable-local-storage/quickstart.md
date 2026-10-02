# Reproduce the cross-platform development environment

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)

Bounded T030-G backup lifecycle is accepted with normal Git source, a verified 94-case Linux union across separate 43/50/19 cohorts and one actual 30/30 Windows run. Follow the [accepted plan](plan.md#accepted-t030-g--ordinary-backup-lifecycle) and [normal recovery recipe](recovery/README.md#accepted-t030-g-backup-lifecycle-recovery); no active packet is needed. Its real Linux quarantine body passed in the 50-case continuation. Native quarantine was not selected in the T030-G Windows subset. These bounded results do not establish full gameplay, whole-preparation/accepted-turn atomicity, save/load or remaining B4/B5 portability. Earlier T030-F/B2/B3 evidence below remains historical and tied to its named source.

## Restore source first

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

Current user-approved acceptance uses real console process/menu/settings/restart checks and automated browser-client checks. A later live browser run will use a server/access supplied by the user; it is not a current-phase blocker. Use separate fresh roots for eventual console/web live checks. Change one ordinary setting, stop the full client process, restart and inspect the retained value; then verify real GM turn, save/load and failure/conflict recovery. Exact accepted command/scenario evidence is added as each block becomes runnable; automated checks are never relabelled live browser execution.

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
