#!/usr/bin/env bash
# Upload steam/content/ to the beta branch configured in harness/config.yaml.
# The director logs in to steamcmd once interactively (credentials are cached by steamcmd; never stored here):
#   steamcmd +login <build_account> +quit
set -euo pipefail
cd "$(dirname "$0")/.."
read -r APP DEPOT BRANCH < <(python -c "import sys;sys.path.insert(0,'harness');from core import load_config as l;s=l()['steam'];print(s['app_id'],s['depot_id'],s['branch'])")
[ -n "$APP" ] && [ -n "$DEPOT" ] || { echo "Set steam.app_id and steam.depot_id in harness/config.yaml"; exit 1; }
: "${STEAM_BUILD_USER:?export STEAM_BUILD_USER=<steam build account>}"
# Only a release build made by `hx release --to-steam` from this exact, clean commit may be uploaded.
[ -z "$(git status --porcelain)" ] || { echo "Working tree is dirty; commit first."; exit 1; }
MARK=steam/build_out/release_ok.json
[ -f "$MARK" ] || { echo "No $MARK — run: ./hx release --to-steam"; exit 1; }
python - "$MARK" <<'PY' || exit 1
import json, subprocess, sys
sys.path.insert(0, "harness")
from pathlib import Path
from unity import scan_for_harness
m = json.load(open(sys.argv[1]))
head = subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
if m.get("commit") != head:
    sys.exit(f"steam/content was built from {m.get('commit')}, HEAD is {head}; rebuild with hx release --to-steam")
leaks = scan_for_harness(Path("steam/content"))
if leaks:
    sys.exit(f"harness code found in steam/content: {leaks}")
print("release check: built from HEAD, no harness code")
PY
VERSION=$(git describe --tags --always)
mkdir -p steam/build_out
sed -e "s/{{APP_ID}}/$APP/g" -e "s/{{DEPOT_ID}}/$DEPOT/g" -e "s/{{BRANCH}}/$BRANCH/g" -e "s/{{DESC}}/$VERSION/g" \
  steam/app_build.vdf.tmpl > steam/build_out/app_build.vdf
sed -e "s/{{DEPOT_ID}}/$DEPOT/g" steam/depot_build.vdf.tmpl > steam/build_out/depot_build.vdf
steamcmd +login "$STEAM_BUILD_USER" +run_app_build "$(pwd)/steam/build_out/app_build.vdf" +quit
echo "Uploaded $VERSION to branch '$BRANCH'. Set it live yourself in Steamworks."
