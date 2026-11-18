"""PreToolUse hook: approved assets are never overwritten by an agent. Stdlib only (system python is enough).
Blocks Edit/Write/MultiEdit on any file listed in art/locks.json (and its Unity .meta). Exit 2 feeds the message to Claude.
A new version needs the director: `hx assets new-version <asset>`. This is a guard rail; `hx assets check` is the proof."""
import json
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
data = json.load(sys.stdin)
target = (data.get("tool_input") or {}).get("file_path", "")
try:
    locks = json.loads((root / "art/locks.json").read_text())
    rel = Path(target).resolve().relative_to(root).as_posix()
except (OSError, ValueError):
    sys.exit(0)
for asset, files in locks.items():
    if rel in files or (rel.endswith(".meta") and rel[:-5] in files):
        print(f"{rel} belongs to approved asset `{asset}` and is locked. Never overwrite an approved asset: ask the "
              f"director to run `./hx assets new-version {asset}` (it archives the approved files first).", file=sys.stderr)
        sys.exit(2)
