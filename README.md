# Indie Game Harness

A template for making small games with an AI team, built on Claude Code, Unity 6, Funplay MCP, Magnific, Tripo and Blender.
**You are the creative director.** Agents write code, build, playtest, generate assets and cut marketing clips. Every call about how the game feels, how it looks or what's fun stays with you, enforced by `taste_review` cards, `pick` stages and `approved` flags.

```
notes / feedback ──► producer ──► backlog/inbox ──(you)──► backlog/ready
                                                           │  overnight: hx run
                         ┌─────────────────────────────────┘
                         ▼
  git worktree card/<id> → claude -p (model by difficulty, $ cap, turn cap, timeout)
  → gates: protected-paths · headless build · EditMode · PlayMode · autotest(metrics+screenshots) · haiku review
  → 1 fix pass on failure (resumed session) → merge │ backlog/review (taste) │ backlog/failed
                         ▼
  morning: "taste-review" skill → you decide → merge / feedback card
```

## What's in it
| Path | What |
|---|---|
| `CLAUDE.md` | Studio rules: the code contract, verification, cost discipline, hard "nevers" |
| `.claude/agents/` | producer, gameplay-engineer, qa-playtester, code-reviewer (haiku), tech-artist, marketing-producer (haiku), build-release (haiku) |
| `.claude/skills/` | new-game, write-card, run-card, playtest, art-asset, capture-shorts, steam-page, steam-release, taste-review |
| `.claude/settings.json` + `harness/hooks/` | permissions (paid/irreversible actions require approval), C# guard hook |
| `.mcp.json` | Funplay (Unity editor, HTTP :8765) and Blender MCP |
| `.claude-plugin/` | lets other game repos install the agents and skills as a plugin |
| `harness/hx.py` | single CLI: `verify build test autotest capture card cards run report cost` |
| `harness/runner.py` | the overnight card runner |
| `packages/com.studio.harness` | Unity package: `HarnessBuild`, `HarnessInput`, `HarnessMetrics`, `HarnessHooks`, AutoPilot (`-autotest`), CameraTakes (`-capture`), smoke tests |
| `scenarios/` | input scripts and metric thresholds (your "feel contract"), protected from agents |
| `art/` + `harness/art/` | manifest-driven concept → pick → Tripo → Blender cleanup → Unity |
| `marketing/` + `harness/marketing/` | camera takes → ffmpeg shorts → approval → slot schedule → poster |
| `steam/` | store checklist, steamcmd templates, beta-branch upload |

## The key idea: the built game can be driven and measured by a script
Gameplay reads input only through `HarnessInput` and reports what defines the feel through `HarnessMetrics`:
```csharp
// one-time input bootstrap (the only place that touches the Input System)
HarnessInput.LiveAxis = n => n == "throttle" ? throttleAction.ReadValue<float>() : steerAction.ReadValue<float>();
// in gameplay
float t = HarnessInput.Axis("throttle");
HarnessMetrics.Report("vehicle.speed", rb.linearVelocity.magnitude);
HarnessMetrics.Count("crop.cut");
```
A scenario (`scenarios/*.json`) plays the **built** player headlessly (`Game -autotest scenario.json -harnessOut dir`), takes screenshots, writes `metrics.json`, and the runner checks `expect` thresholds such as `"vehicle.speed.max": {"min": 6, "max": 12}`. Once you've tuned something by feel, lock it in with a threshold so overnight agents can't quietly break it. The same input path lets the camera takes play the game while filming it.

## Quick start
1. Use this repo as a template → `bash scripts/bootstrap.sh "My Game"`, then follow its printout (Unity 6 project in `game/`, Funplay package, `UNITY_PATH`).
2. Log in to Claude Code with your plan (`claude` → `/login`). Install `ffmpeg`, `blender`, `uv` (for Blender MCP), and `xvfb` on headless Linux. Optional paid keys: `ANTHROPIC_API_KEY`, `MAGNIFIC_API_KEY`, `TRIPO_API_KEY`. Then run `python harness/hx.py selftest --all`.
3. In Claude Code: *"use the new-game skill"* → pillars interview, style guide, first cards.
4. Promote cards: `python harness/hx.py card ready C-0001`.
5. Overnight: `python harness/hx.py run` ($0, plan quota) or `run --budget 10` to allow paid boosts. Schedule it with cron or Task Scheduler. Morning: *"taste-review"*.

## Day loop
- **Day (with you):** playtest + tune in the open editor via Funplay (`playtest` skill), pick concepts, approve shorts, write feedback → `write-card`.
- **Night (without you):** `hx run` works through `ready/` with a budget per card and per night.

## Money is optional ($0 is the default)
- **Default `money_budget_usd: 0`:** every agent call runs on your Claude plan's included quota. The runner strips `ANTHROPIC_API_KEY` from agent calls, so Claude Code uses your subscription login and can't bill an API key by accident.
- **When the quota runs out:** the current card goes back to `ready` (it isn't failed). The runner either waits for the quota window to reset and keeps going until `run_until`, or stops (`on_quota_exhausted`).
- **Money as a boost:** with `hx run --budget 10` (or `money_budget_usd`), real API spend is allowed only for cards marked `paid: true`, for models not in `plan_models`, and for one escalation attempt (Opus) after a card fails on quota. The ledger separates `plan` usage (an estimate, not billed) from `api` (real money); `hx cost` shows both.
- **Art is $0 by default:** `providers` are `manual` (drop in images or models from free tools, CC0 libraries or a sketch) or `comfyui` (local GPU, any image or image-to-3D workflow). Magnific and Tripo are opt-in per project or per asset.
- **Still not free:** the Steam app fee, and music licensing if you use music.

## Validate the harness at $0: `hx selftest`
| Level | Cost | What it proves |
|---|---|---|
| `hx selftest` | $0, no AI | 16 checks: runner merge, key stripping, forced model, protected paths and resumed fix pass, no escalation without money, escalation billed as `api` with money, quota → card back to ready, taste parking, dependencies, thresholds, C# hook, approval-gated posting queue, real ffmpeg short cut to 1080×1920, art pipeline free modes, Blender cleanup (if installed), all config/agent/skill/plugin files |
| `--mcp` | $0, no model call | Funplay answers MCP `tools/list` on :8765, the Blender addon socket on :9876 responds, `claude mcp list` health |
| `--unity` | $0 | temp sample project → package compiles → headless build → EditMode + PlayMode → autotest (input → movement → metrics → thresholds) → camera capture frames |
| `--live` | plan quota only, **Haiku forced**, $0.05 caps | headless call on your login, project skills load, one real card through the runner, an agent reading Unity's version through Funplay |

Run it after bootstrap and after changing the harness. Everything runs in temp copies, so your checkout isn't touched.

## Keeping quota/cost down
- Model by card `difficulty`: haiku for trivial cards and the review gate, sonnet by default, opus only for `hard` cards. `force_model` / `HX_FORCE_MODEL=haiku` pins everything to the cheapest model.
- Caps on every run: `--max-budget-usd` per card (on plan quota this keeps one runaway card from eating the night), `--max-turns`, a wall-clock timeout, `run_until`, and a stop after 3 consecutive failures. The budget cap is a client-side estimate.
- The fix pass resumes the same session, so cached context is reused instead of re-reading the repo.
- Cheap gates run first (protected paths, then compile); slow ones (PlayMode, autotest) run only after those pass.
- The Unity `Library/` cache is copied into each worktree, which avoids a full reimport per card.
- Paid generation only happens through `harness/art/pipeline.py` with `variants` set per asset and costs logged (`hx cost`); every paid command needs approval in `.claude/settings.json`.

## Things to adapt per game
- `harness/config.yaml`: build target, binary name, `plan_models` (what your plan includes), `run_until`, art providers, and API prices for the ledger.
- `harness/runner/runner.settings.json`: deny rules assume the Unity project is at `game/`.
- `art/style.md` and `design/PILLARS.md` (yours).
- Check the endpoint and field names in `harness/art/pipeline.py` (Magnific generation endpoint, Tripo v3 fields) against your plan's current API docs, and use `--dry-run` first.
- TikTok/Instagram posting are manual reminders until you wire an adapter in `queue.py` (YouTube is implemented).
