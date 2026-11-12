# Studio rules (read first, every session)

This repo is one indie game built with an AI team. The human is the **creative director**: they make every taste call (feel, look, fun). Agents build, measure and report. When a decision is about taste, don't decide — surface options with screenshots/metrics and set `taste_review: true`.

## Layout
- `game/` Unity 6 project. `packages/com.studio.harness` = build entry, `HarnessInput`, `HarnessMetrics`, AutoPilot, CameraTakes.
- `backlog/{inbox,ready,review,done,failed}/` cards (see `backlog/_template.md`). Only humans move cards inbox → ready.
- `scenarios/*.json` autotest scripts + metric thresholds (the "feel contract"). Protected.
- `design/PILLARS.md` human-owned vision — read, never edit. `design/TASTE_LOG.md` decisions with reasons.
- `art/manifest.yaml` asset pipeline state. `marketing/` takes, shorts, posting schedule. `steam/` store + depot.
- `harness/hx.py` the only CLI you need: `python harness/hx.py verify|build|test|autotest|capture|cards|cost`.

## Code contract (Unity / C#)
- Gameplay reads input **only** via `Harness.HarnessInput.Axis/Button` — so scenarios can drive the built game.
- Any tunable feel value is a serialized field or ScriptableObject, never a magic number.
- Report what defines feel: `HarnessMetrics.Report("vehicle.speed", v)`, `HarnessMetrics.Count("crop.cut")`.
- Expose debug/setup actions to scenarios via `HarnessHooks.Register(...)`.
- New systems get EditMode tests for logic; PlayMode/autotest for behaviour. Never delete or weaken a test to pass.
- Always commit `.meta` files. Don't hand-edit `.unity`/`.prefab` YAML beyond trivial fixes — use Funplay MCP (`execute_code`) or an Editor script.
- Never read `game/Library`, `Temp`, `Logs`, `Builds` (huge, useless context).

## Verify before claiming done
`python harness/hx.py verify --scenarios <relevant>` must pass. In an interactive session with the editor open, also look: use Funplay MCP to enter play mode, simulate, and screenshot.

## Cost discipline
- Default model is Sonnet; Opus only for cards marked `difficulty: hard`. Use the `Explore` subagent (cheap, read-only) for codebase searches instead of reading many files.
- Read files in parts; grep before reading. Keep cards small (≤ 1 system, ≤ ~300 lines changed).
- $0 is the normal mode: agents run on the plan quota, and art providers default to manual/ComfyUI. Paid APIs (Anthropic API key, Magnific, Tripo) are used only when config or the card says so; they are logged as `billed: api` in `harness/ledger.jsonl`.

## Never
push, publish to Steam, post to social, spend on generation APIs, or change prices/store text without the human's explicit go-ahead in chat. The runner never pushes.
