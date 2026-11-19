#!/bin/bash
set -euo pipefail
source /workspace/shared/isolated-restore-20261119/env.sh
export PYTHONDONTWRITEBYTECODE=1
: "${MM_REVIEWED_PUBLISH_INPUT_SHA256:?reviewed manifest SHA required}"
cd /workspace/shared/mortal-isolated-20261119
exec python3 specs/1515-mortal-transport-storage/manual-mm21/publish-current.py "$1"
