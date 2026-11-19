#!/bin/bash
set -euo pipefail
umask 077
export POWERSHELL_TELEMETRY_OPTOUT=1
export POWERSHELL_UPDATECHECK=Off
export DOTNET_CLI_TELEMETRY_OPTOUT=1
source /workspace/shared/isolated-restore-20261119/env.sh
export BOE_TERMINAL_NODE=/opt/codex/runtimes/codex-primary-runtime/dependencies/node/bin/node
export BOE_TERMINAL_PREFIX=/workspace/shared/relay-alternative/mortal-t034/terminal-deps
export PYTHONDONTWRITEBYTECODE=1
: "${MM21_REVIEWED_CHECKPOINT:?Requires saved reviewed source checkpoint}"
: "${MM21_REVIEWED_DRIVER_SHA256:?Requires reviewed driver byte pin}"
test ! -e /workspace/shared/mm21-20261119 && test ! -L /workspace/shared/mm21-20261119
cd /workspace/shared/mortal-isolated-20261119
python3 /workspace/shared/relay-alternative/mortal-t034/mm21-preparation/manual-mm21.py
python3 /workspace/shared/relay-alternative/mortal-t034/mm21-preparation/prepare-mm21-profile.py
