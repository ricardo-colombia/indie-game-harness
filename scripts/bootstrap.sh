#!/usr/bin/env bash
# Usage: bash scripts/bootstrap.sh "My Game"   (run once in a fresh copy of the template)
# Creates game/ if missing (open it in Unity 6 to generate the project), wires the harness package.
set -euo pipefail
cd "$(dirname "$0")/.."
NAME="${1:?game name}"
SLUG=$(echo "$NAME" | tr -cd '[:alnum:]')

python3 -m pip install -q -r harness/requirements.txt || true
sed -i.bak -e "s/^  name: .*/  name: $NAME/" -e "s#player_binary: Builds/Player/.*#player_binary: Builds/Player/$SLUG.exe#" harness/config.yaml && rm harness/config.yaml.bak

mkdir -p game/Packages game/Assets backlog/{inbox,ready,review,done,failed}
M=game/Packages/manifest.json
[ -f "$M" ] || echo '{"dependencies":{}}' > "$M"
python3 - "$M" <<'PY'
import json, sys
p = sys.argv[1]; m = json.load(open(p))
m.setdefault("dependencies", {})["com.studio.harness"] = "file:../../packages/com.studio.harness"
m.setdefault("dependencies", {}).setdefault("com.unity.test-framework", "1.4.5")
t = m.setdefault("testables", [])
if "com.studio.harness" not in t: t.append("com.studio.harness")
json.dump(m, open(p, "w"), indent=2)
PY

git init -q -b main 2>/dev/null || true
git lfs install --local >/dev/null 2>&1 || echo "note: install git-lfs for art/audio binaries"
git add -A && git commit -qm "Bootstrap $NAME from studio template" || true

cat <<EOF
Done. Next:
 1. Unity Hub → open ./game with Unity 6 (creates the project if new; pick URP 3D).
 2. Package Manager → + → Add from git URL: https://github.com/FunplayAI/funplay-unity-mcp.git
    then Funplay → MCP Server → Start (port 8765). In Claude Code run /mcp to check 'funplay'.
 3. export UNITY_PATH=<path to Unity editor binary>; python harness/hx.py verify
 4. In Claude Code: "use the new-game skill" to write pillars and the first cards.
EOF
