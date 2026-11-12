#!/usr/bin/env bash
# Upload steam/content/ to the beta branch configured in harness/config.yaml.
# The director logs in to steamcmd once interactively (credentials are cached by steamcmd; never stored here):
#   steamcmd +login <build_account> +quit
set -euo pipefail
cd "$(dirname "$0")/.."
read -r APP DEPOT BRANCH < <(python -c "import sys;sys.path.insert(0,'harness');from core import load_config as l;s=l()['steam'];print(s['app_id'],s['depot_id'],s['branch'])")
[ -n "$APP" ] && [ -n "$DEPOT" ] || { echo "Set steam.app_id and steam.depot_id in harness/config.yaml"; exit 1; }
: "${STEAM_BUILD_USER:?export STEAM_BUILD_USER=<steam build account>}"
VERSION=$(git describe --tags --always)
mkdir -p steam/build_out
sed -e "s/{{APP_ID}}/$APP/g" -e "s/{{DEPOT_ID}}/$DEPOT/g" -e "s/{{BRANCH}}/$BRANCH/g" -e "s/{{DESC}}/$VERSION/g" \
  steam/app_build.vdf.tmpl > steam/build_out/app_build.vdf
sed -e "s/{{DEPOT_ID}}/$DEPOT/g" steam/depot_build.vdf.tmpl > steam/build_out/depot_build.vdf
steamcmd +login "$STEAM_BUILD_USER" +run_app_build "$(pwd)/steam/build_out/app_build.vdf" +quit
echo "Uploaded $VERSION to branch '$BRANCH'. Set it live yourself in Steamworks."
