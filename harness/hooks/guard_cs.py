"""PostToolUse hook: cheap static checks on edited C# so contract violations are caught in seconds,
not after a 10-minute build. Exit code 2 feeds the message back to Claude."""
import json
import re
import sys

data = json.load(sys.stdin)
path = (data.get("tool_input") or {}).get("file_path", "")
if not path.endswith(".cs") or "/packages/com.studio.harness/" in path.replace("\\", "/") or "/Editor/" in path:
    sys.exit(0)
try:
    src = open(path, encoding="utf-8").read()
except OSError:
    sys.exit(0)

problems = []
if re.search(r"\bInput\.(GetAxis|GetKey|GetButton|GetMouseButton)\b|Keyboard\.current|Gamepad\.current", src) \
        and "LiveAxis" not in src and "LiveButton" not in src:
    problems.append("Reads input directly. Gameplay must use Harness.HarnessInput.Axis/Button "
                    "(only the input bootstrap that sets HarnessInput.LiveAxis may touch the Input System).")
if problems:
    print(f"{path}:\n- " + "\n- ".join(problems), file=sys.stderr)
    sys.exit(2)
