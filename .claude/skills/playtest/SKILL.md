---
name: playtest
description: Play the game in the open Unity editor via Funplay MCP — enter play mode, drive inputs, sample values, screenshot — and report measured feel. Use for "try it", "how does it handle", "show me", or tuning sessions with the director.
---
Requires the editor open with Funplay MCP running (Funplay → MCP Server, port 8765). If unreachable, fall back to `hx autotest`.

Loop (keep each `execute_code` call small and single-purpose):
1. Check console is clean; enter play mode; wait for scene load.
2. Drive input: `Harness.HarnessInput.Simulated = true; Harness.HarnessInput.Set("throttle", 1f);` — release with `Set(name, 0)` and finish with `ClearAll(); Simulated = false;`. Setup actions via `Harness.HarnessHooks.Invoke("spawn_vehicle", "start")`.
3. Sample over time (position, velocity, angles, counts) into a compact table — not raw dumps.
4. Screenshot at meaningful moments (start, peak, end). Look at them before describing them.
5. Exit play mode. Report facts + screenshots.

Tuning with the director: change ONE serialized value at a time, replay the same input, show before/after numbers and screenshots side by side. When they say "that's it", write the value and the reason (their words) to `design/TASTE_LOG.md`, and offer to lock it in with a scenario threshold.
