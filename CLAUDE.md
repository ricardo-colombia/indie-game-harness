# Studio rules (read first, every session)

This repo is one indie game built with an AI team. The human is the **creative director**: they make every taste call (feel, look, fun). Agents build, measure and report. When a decision is about taste, don't decide — surface options with screenshots/metrics and set `taste_review: true`. Talk to the director in their language.

`./hx` = `python3 harness/hx.py` (Windows: `hx`). It switches itself to `.venv` after setup.

## First session
Run `./hx doctor`. If a required item is red, tell the director what `./hx setup` will do and run `./hx setup "<Game Name>"` (it creates the Unity project in `game/`, wires the packages, makes `.venv`). If no Unity 6 editor is installed it downloads one (several GB): ask first, then add `--yes`. Then use the `new-game` skill: **the goal of session one is a playable game, not a plan.**

## Driving the editor
Open it with `unity open game`, wait with `unity status --until-ready --project-path game`, then use `unity command` (find commands with `unity command --query <term>`; arbitrary C# via `unity command script eval`). No MCP server is needed.
**One editor per Unity project folder, and batch commands can't share it.** `./hx build|test|verify` refuse to run while the editor has `game/` open: close it (`unity close game`) or stay inside it (`unity test`, `unity command`). A git worktree is a different project folder: it needs its own editor and first import (minutes). Check `unity status` before touching a project another session may have open.

## Layout
- `game/` Unity 6 project (empty until `hx setup`). `packages/com.studio.harness` = out-of-game harness (build entry, Input System driver, probes, AutoPilot, CameraTakes), compiled only into the editor and harness builds.
- `backlog/{inbox,ready,review,done,failed}/` cards (see `backlog/_template.md`). Only humans move cards inbox → ready.
- `scenarios/*.json` autotest scripts + metric thresholds (the "feel contract"). Protected; the runner reads them from main.
- `design/PILLARS.md` human-owned vision — read, never edit. `design/TASTE_LOG.md` decisions with reasons.
- `art/manifest.yaml` asset pipeline state (optional). `marketing/` takes, shorts, posting schedule. `steam/` store + depot.
- `harness/hx.py` the only CLI you need: `setup|doctor|verify|build|web|test|autotest|capture|cards|cost`.

## Code contract (Unity / C#)
- **Game code never references the harness** (`using Harness` is forbidden; it's stripped from release builds). The harness drives and observes the game from outside.
- Input uses the **Input System** package (InputActions / PlayerInput), never the legacy `Input.GetAxis`. That is what lets scenarios drive the game with virtual devices or recorded traces. Give every control a touch equivalent from the start (swipe / tap) so the web build is playable on a phone.
- Any tunable feel value is a serialized field or ScriptableObject, never a magic number. State worth measuring should be readable as a field/property, so scenario **probes** can read it (e.g. a public `CutCount`).
- **Rules live in plain C# classes** (no MonoBehaviour, no scene access: grid, movement, scoring, level tables, AI targeting) and MonoBehaviours only wire them to the scene. That is what makes EditMode tests cheap and lets you rewrite the look or the movement without breaking the rules.
- Setup actions for tests are ordinary public methods on game objects (`GameManager.StartLevel(string)`), which scenarios call with `send` steps.
- New systems get EditMode tests for logic; PlayMode/autotest for behaviour. Never delete or weaken a test to pass.
- Always commit `.meta` files. Don't hand-edit `.unity`/`.prefab` YAML beyond trivial fixes — use the editor (`unity command`, e.g. `script eval`) or an Editor script. Prefer building the scene from code at startup (a bootstrap `MonoBehaviour`) for small games: it diffs, merges and tests better than scene YAML.
- Never read `game/Library`, `Temp`, `Logs`, `Builds` (huge, useless context).
- Unattended cards may only change `game/Assets/` (not `Editor/` folders, `.asmdef`, plugins) unless the card's `allow_paths` says so. Game code never reads harness command-line flags (`-autotest`, `-harnessOut`), never writes `metrics.json`, and never uses `InitializeOnLoad`, `Process.Start` or networking without a card that asks for it — such code isn't run by the gates, it goes to the director.

## Lessons from the first game (Pac-Man)
- **Playable first.** The first game got good by iterating live in one session (code → play → screenshot → adjust), not by queueing cards. Use cards and `hx run` once a core loop exists and you want polish done in parallel.
- **Procedural before assets.** Primitives, runtime-generated sprites/materials and synthesized sound/music got a finished look with zero imports. The Blender/FBX route was tried and dropped. Reach for the `art-asset` pipeline only when the director asks for a specific model.
- **Copying a known game? Get the reference first** (image or spec), put the map/tables in data, and assert them in tests. An invented layout had to be thrown away; the rules (speeds, targeting, level tables) came from the published reference and were pinned by tests.
- **Ship a web build early** (`./hx web --serve`, then the `web-release` skill): it is the fastest way for the director to feel it on a phone. WebGL gotchas: no OS fonts (use `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` under `#if UNITY_WEBGL`), no Quit button, fit camera and UI to any aspect ratio, cap the pixel ratio on phones.
- **Check state before claiming it works.** After a rewrite run the EditMode tests (`unity test --mode EditMode`) and look at a screenshot; compile errors in the editor console are the usual first failure.

## Verify before claiming done
`./hx verify --scenarios <relevant>` must pass (close the editor first). In an interactive session with the editor open, use `unity test --mode EditMode`, enter play mode, simulate and screenshot through `unity command` instead.

## Cost discipline
- Default model is Sonnet; Opus only for cards marked `difficulty: hard`. Use the `Explore` subagent (cheap, read-only) for codebase searches instead of reading many files.
- Read files in parts; grep before reading. Keep cards small (≤ 1 system, ≤ ~300 lines changed).
- $0 is the normal mode: agents run on the plan quota, and art providers default to manual/ComfyUI. Paid APIs (Anthropic API key, Magnific, Tripo) are used only when config or the card says so; they are logged as `billed: api` in `harness/ledger.jsonl`.

## Never
push, publish to Steam or the web, post to social, spend on generation APIs, or change prices/store text without the human's explicit go-ahead in chat. The runner never pushes.
