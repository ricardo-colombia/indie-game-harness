"""PostToolUse hook: catches, in seconds, the two things that would break the harness on game code.
Exit code 2 feeds the message back to Claude. Harness package, tests and Editor folders are exempt."""
import json
import re
import sys

data = json.load(sys.stdin)
path = (data.get("tool_input") or {}).get("file_path", "")
norm = path.replace("\\", "/")
if not norm.endswith(".cs") or "/packages/com.studio.harness/" in norm or "/Editor/" in norm or "/Tests/" in norm:
    sys.exit(0)
try:
    src = open(path, encoding="utf-8").read()
except OSError:
    sys.exit(0)

problems = []
if re.search(r"\b(UnityEngine\.)?Input\.(GetAxis|GetAxisRaw|GetKey|GetKeyDown|GetKeyUp|GetButton|GetButtonDown|GetMouseButton)\b", src):
    problems.append("Uses the legacy Input Manager. Use Input System actions (InputAction / PlayerInput / generated "
                    "input class) — scenarios drive the game through Input System virtual devices and recorded traces.")
if re.search(r"^\s*using\s+Harness\b|\bHarness\.(InputDriver|HarnessMetrics|Probe|StepRunner|AutoPilot)\b", src, re.M):
    problems.append("Game code references the harness. The harness observes the game from outside (probes in the "
                    "scenario JSON); it's stripped from release builds, so this would break the shipped game.")
if problems:
    print(f"{path}:\n- " + "\n- ".join(problems), file=sys.stderr)
    sys.exit(2)
