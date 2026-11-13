---
name: playtest
description: Play the game in the open Unity editor via `unity command` — enter play mode, drive inputs, sample values, screenshot — and report measured feel. Use for "try it", "how does it handle", "show me", or tuning sessions with the director.
---
Open the editor with `unity open game`, then `unity status --until-ready --project-path game`. Drive it with `unity command` (find commands with `unity command --query <term>`; run C# with `unity command script eval`). If it can't be reached, fall back to `hx autotest`.

Loop (keep each `script eval` call small and single-purpose):
1. Check console is clean; enter play mode; wait for scene load.
2. Drive input through the Input System: `Harness.InputDriver.Init(); Harness.InputDriver.Set("<Gamepad>/rightTrigger", 1f);` (sticks: `Set("<Gamepad>/leftStick", new Vector2(1,0))`), release with 0 and finish with `InputDriver.ReleaseAll()`. Setup via the game's own public methods. To capture how the director plays, have them use *Harness → Record Input* and save the trace for a `replay` scenario.
3. Sample over time (position, velocity, angles, counts) into a compact table — not raw dumps.
4. Screenshot at meaningful moments (start, peak, end). Look at them before describing them.
5. Exit play mode. Report facts + screenshots.

Tuning with the director: change ONE serialized value at a time, replay the same input, show before/after numbers and screenshots side by side. When they say "that's it", write the value and the reason (their words) to `design/TASTE_LOG.md`, and offer to lock it in with a scenario threshold.
