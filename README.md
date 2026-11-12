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
2. Set keys in your shell: `ANTHROPIC_API_KEY` (or a logged-in Claude Code), `MAGNIFIC_API_KEY`, `TRIPO_API_KEY`. Install `ffmpeg`, `blender`, `uv` (for Blender MCP), and `xvfb` on headless Linux.
3. In Claude Code: *"use the new-game skill"* → pillars interview, style guide, first cards.
4. Promote cards: `python harness/hx.py card ready C-0001`.
5. Overnight: `python harness/hx.py run --budget 15` (or cron/Task Scheduler). Morning: *"taste-review"*.

## Day loop
- **Day (with you):** playtest + tune in the open editor via Funplay (`playtest` skill), pick concepts, approve shorts, write feedback → `write-card`.
- **Night (without you):** `hx run` works through `ready/` with a budget per card and per night.

## Keeping costs down
- Model by card `difficulty`: haiku for trivial cards and the review gate, sonnet by default, opus only for `hard` cards.
- Hard caps on every run: `--max-budget-usd`, `--max-turns`, a wall-clock timeout, a nightly budget, and a stop after 3 consecutive failures. Note that the `claude -p` budget cap is a client-side estimate, so check real spend in the Console.
- The fix pass resumes the same session, so cached context is reused instead of re-reading the repo.
- Cheap gates run first (protected paths, then compile); slow ones (PlayMode, autotest) run only after those pass.
- The Unity `Library/` cache is copied into each worktree, which avoids a full reimport per card.
- Paid generation only happens through `harness/art/pipeline.py` with `variants` set per asset and costs logged (`hx cost`); every paid command needs approval in `.claude/settings.json`.

## Things to adapt per game
- `harness/config.yaml`: build target, binary name, budgets, and API prices for the ledger.
- `harness/runner/runner.settings.json`: deny rules assume the Unity project is at `game/`.
- `art/style.md` and `design/PILLARS.md` (yours).
- Check the endpoint and field names in `harness/art/pipeline.py` (Magnific generation endpoint, Tripo v3 fields) against your plan's current API docs, and use `--dry-run` first.
- TikTok/Instagram posting are manual reminders until you wire an adapter in `queue.py` (YouTube is implemented).
