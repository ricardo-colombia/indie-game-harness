# Handoff — where this left off (read with CLAUDE.md)

Built in a cloud session on 2026-11-12, now continuing in local Claude Code.

## Goal
A reusable harness/template for indie games made with AI (not a game). Modelled on: Claude Code + the Unity CLI (Unity 6) for code, Magnific/Tripo/Blender for art, an on-demand backlog-card runner with time/money caps that only lands changes that pass the gates, and auto-filmed shorts on a posting schedule. The human makes every taste call.

## Decisions (the director's own requirements)
- **$0 must be a first-class mode.** Default runs on the Claude plan's included quota only (API key stripped from agent calls). Money is an optional boost: `paid: true` cards, models not in the plan, one Opus escalation. When the quota runs out the card returns to `ready`, and the runner waits or stops.
- **Validate at zero cost**, forcing the cheapest model (Haiku) for live tests → `hx selftest` + `scripts/selftest.{sh,ps1}` + `TESTING.md`.
- **Commits** carry no AI co-author/attribution lines.
- **No harness code in the game.** The harness drives the game through the Input System (virtual devices / recorded traces) and reads it with reflection probes. It compiles only into the editor and harness builds (`HARNESS` define); `-release` builds are clean. Games must use the Input System package.

## Status
- Done: everything in README.md. Offline selftest: 31 tests, all pass (Blender test skipped where Blender is missing).
- Adversarial review fixes (2026-11-12): see README "Trust model". Still yours to do: set `runner.sandbox.prefix` for real OS isolation, turn off account overage and set `plan_overage_disabled: true`.
- **Not yet verified on a real machine:** `hx setup` against the real Unity CLI, `selftest --unity`, `--live`. C# has never been compiled. Windows path (`.cmd` shim, stdin prompts) is untested.
- API field names in `harness/art/pipeline.py` (Magnific generate endpoint, Tripo v3 upload/output fields) are best-effort; check with `--dry-run` against current docs.
- On GitHub: github.com/ricardo-colombia/indie-game-harness (commits use the noreply email; GitHub email privacy is on).

## Next steps
1. `bash scripts/selftest.sh all` (or `scripts\selftest.ps1 all`), then fix whatever `selftest-results.txt` shows red.
2. Mark the repo as a Template repository (Settings → General). CI (`.github/workflows/selftest.yml`) runs the offline level for free.
3. For the first game: open the folder in Claude Code and describe the game (it runs `hx setup` and the `new-game` skill).
