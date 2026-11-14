#!/usr/bin/env bash
# Launcher for the harness CLI: ./hx doctor | setup | verify | web ...  (same as `python3 harness/hx.py ...`)
# Works with whatever python3/python you have; the CLI switches itself to .venv once `./hx setup` has made it.
dir="$(cd "$(dirname "$0")" && pwd)"
py="$(command -v python3 || command -v python)" || { echo "Python 3.10+ is required (python.org or brew install python)"; exit 1; }
exec "$py" "$dir/harness/hx.py" "$@"
