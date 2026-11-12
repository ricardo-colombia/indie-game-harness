#!/usr/bin/env bash
# Run the harness selftests on macOS/Linux. Money: $0 at every level.
#   bash scripts/selftest.sh            # offline only (no AI, no Unity)
#   bash scripts/selftest.sh mcp        # + MCP probes (open Unity/Blender with their MCP add-ons first)
#   bash scripts/selftest.sh unity      # + Unity pipeline (needs UNITY_PATH)
#   bash scripts/selftest.sh live       # + real Claude on your PLAN quota, Haiku forced
#   bash scripts/selftest.sh all
# Output is also saved to selftest-results.txt — paste it back to Claude if anything is red.
set -uo pipefail
cd "$(dirname "$0")/.."
PY=${PYTHON:-python3}
if [ ! -d .venv ]; then "$PY" -m venv .venv; fi
# shellcheck disable=SC1091
source .venv/bin/activate
python -m pip install -q --upgrade pip >/dev/null
python -m pip install -q -r harness/requirements.txt

ARGS=()
case "${1:-}" in
  mcp) ARGS=(--mcp) ;; unity) ARGS=(--unity) ;; live) ARGS=(--live) ;; all) ARGS=(--all) ;;
  "") ;; *) echo "unknown level: $1"; exit 2 ;;
esac

{
  echo "# selftest $(date '+%Y-%m-%d %H:%M') level=${1:-offline}"
  echo "os=$(uname -sr) python=$(python --version 2>&1)"
  for t in git ffmpeg blender claude uvx xvfb-run; do printf '%s=%s\n' "$t" "$(command -v $t || echo MISSING)"; done
  echo "UNITY_PATH=${UNITY_PATH:-unset}"
  python harness/selftest.py "${ARGS[@]}"
  echo "exit=$?"
} 2>&1 | tee selftest-results.txt
