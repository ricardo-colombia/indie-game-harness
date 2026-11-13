# Studio rules (read first, every session)

This repo is one indie game built with an AI team. The human is the **creative director**: they make every taste call (feel, look, fun). Agents build, measure and report. When a decision is about taste, don't decide — surface options with screenshots/metrics and set `taste_review: true`.

## First session
Run `python harness/hx.py doctor`. If a required item is red, tell the director what `hx setup` will do and run `python harness/hx.py setup "<Game Name>"` (it creates the Unity project in `game/` and wires the packages). If no Unity 6 editor is installed it downloads one (several GB): ask first, then add `--yes`. Then use the `new-game` skill.

## Driving the editor
Open it with `unity open game`, wait with `unity status --until-ready --project-path game`, then use `unity command` (find commands with `unity command --query <term>`; arbitrary C# via `unity command script eval`). No MCP server is needed.

## Layout
- `game/` Unity 6 project. `packages/com.studio.harness` = out-of-game harness (build entry, Input System driver, probes, AutoPilot, CameraTakes), compiled only into the editor and harness builds.
- `backlog/{inbox,ready,review,done,failed}/` cards (see `backlog/_template.md`). Only humans move cards inbox → ready.
- `scenarios/*.json` autotest scripts + metric thresholds (the "feel contract"). Protected; the runner reads them from main.
- `design/PILLARS.md` human-owned vision — read, never edit. `design/TASTE_LOG.md` decisions with reasons.
- `art/manifest.yaml` asset pipeline state. `marketing/` takes, shorts, posting schedule. `steam/` store + depot.
- `harness/hx.py` the only CLI you need: `python harness/hx.py setup|doctor|verify|build|test|autotest|capture|cards|cost`.

## Code contract (Unity / C#)
- **Game code never references the harness** (`using Harness` is forbidden; it's stripped from release builds). The harness drives and observes the game from outside.
- Input uses the **Input System** package (InputActions / PlayerInput), never the legacy `Input.GetAxis`. That is what lets scenarios drive the game with virtual devices or recorded traces.
- Any tunable feel value is a serialized field or ScriptableObject, never a magic number. State worth measuring should be readable as a field/property, so scenario **probes** can read it (e.g. a public `CutCount`).
- Setup actions for tests are ordinary public methods on game objects (`GameManager.StartLevel(string)`), which scenarios call with `send` steps.
- New systems get EditMode tests for logic; PlayMode/autotest for behaviour. Never delete or weaken a test to pass.
- Always commit `.meta` files. Don't hand-edit `.unity`/`.prefab` YAML beyond trivial fixes — use the editor (`unity command`, e.g. `script eval`) or an Editor script.
- Never read `game/Library`, `Temp`, `Logs`, `Builds` (huge, useless context).
- Unattended cards may only change `game/Assets/` (not `Editor/` folders, `.asmdef`, plugins) unless the card's `allow_paths` says so. Game code never reads harness command-line flags (`-autotest`, `-harnessOut`), never writes `metrics.json`, and never uses `InitializeOnLoad`, `Process.Start` or networking without a card that asks for it — such code isn't run by the gates, it goes to the director.

## Verify before claiming done
`python harness/hx.py verify --scenarios <relevant>` must pass. In an interactive session with the editor open, also look: use `unity command` to enter play mode, simulate, and screenshot.

## Cost discipline
- Default model is Sonnet; Opus only for cards marked `difficulty: hard`. Use the `Explore` subagent (cheap, read-only) for codebase searches instead of reading many files.
- Read files in parts; grep before reading. Keep cards small (≤ 1 system, ≤ ~300 lines changed).
- $0 is the normal mode: agents run on the plan quota, and art providers default to manual/ComfyUI. Paid APIs (Anthropic API key, Magnific, Tripo) are used only when config or the card says so; they are logged as `billed: api` in `harness/ledger.jsonl`.

## Never
push, publish to Steam, post to social, spend on generation APIs, or change prices/store text without the human's explicit go-ahead in chat. The runner never pushes.
