# Reproduce the cross-platform development environment

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)

## Restore source first

```sh
git clone --single-branch --branch 1553-cross-platform-runtime https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git boe
cd boe
git rev-parse HEAD
git ls-remote origin refs/heads/1553-cross-platform-runtime
git status --short
```

Compare the complete SHAs; read AGENTS.md, docs/development-workflow.md and this feature's spec/plan/tasks before editing. No local-only code cache is needed. Application/test execution is separate evidence from successful download.

## Tool versions actually verified on Debian 13 x64

- .NET SDK 10.0.401; .NET and ASP.NET runtime 8.0.31
- PowerShell 7.6.6
- Node 24.19.0 and npm 11.9.0
- Spec Kit CLI 1.0.13; repository Codex skills integration already present

Install from official sources without privileged system changes. For example, use a writable TOOLCHAIN directory outside the checkout:

```sh
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
export DOTNET_CLI_TELEMETRY_OPTOUT=1
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

## Verification

Read docs/testing.md and select categories for the actual block. Use scripts/test-csharp.ps1; new B1 categories will be named in the checkpoint when added. `-ValidateCatalog` discovers ownership without executing tests. Never use a full-suite/aggregate/all-category run. Store meaningful result counts and source SHA remotely in plan.md, not only ignored TestResults.

For actual client acceptance use separate fresh roots for console and web. Change one ordinary setting, stop the full client process, restart and inspect the retained value. Then test real GM turn, save/load and restart, including failed/conflicting operations. Exact accepted command/scenario evidence is added as each block becomes runnable.

## Windows owner handoff

Use the exact published acceptance SHA with supported .NET/PowerShell/Node versions. Run the same selected storage/path/recovery categories, then console and browser startup-setting-restart, real persistent CLI/daemon turns, cancellation/restart, core game turn, save/load, audio and clipboard. Report Windows version, filesystem, terminal/provider and exact failures. These checks are not marked passed from Linux evidence.
