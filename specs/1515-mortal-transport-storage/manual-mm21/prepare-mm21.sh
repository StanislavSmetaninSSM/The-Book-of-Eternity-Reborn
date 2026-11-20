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
test "$#" = 2 || { test "$#" = 3 && test "$3" = "--self-save-load-turn"; }
: "${MM_REVIEWED_INPUT_SHA256:?Requires reviewed complete input manifest hash}"
MANIFEST="$1"; DESTINATION="$2"
test ! -e "$DESTINATION" && test ! -L "$DESTINATION"
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd /workspace/shared/mortal-isolated-20261119
mode=("${@:3}")
python3 "$HERE/manual-mm21.py" "$MANIFEST" "$DESTINATION" "${mode[@]}"
python3 "$HERE/prepare-mm21-profile.py" "$DESTINATION" "${mode[@]}"
