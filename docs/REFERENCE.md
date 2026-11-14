# Reference

How the harness works under the hood. For day-to-day use, see the [README](../README.md).

A template for making small games with an AI team, built on Claude Code, Unity 6 (driven through the Unity CLI), Magnific, Tripo and Blender.
**You are the creative director.** Agents write code, build, playtest, generate assets and cut marketing clips. Every call about how the game feels, how it looks or what's fun stays with you, enforced by `taste_review` cards, `pick` stages and `approved` flags.

```
notes / feedback ──► producer ──► backlog/inbox ──(you)──► backlog/ready
                                                           │  hx run (on demand)
                         ┌─────────────────────────────────┘
                         ▼
  git worktree card/<id> → claude -p (model by difficulty, $ cap, turn cap, timeout)
  → gates: writable-paths allowlist · suspicious-code check · build · release build (no harness) · EditMode · PlayMode
          · autotest (nonce + plausibility) · review · post-gate tamper check
  → 1 fix pass on failure (resumed session) → merge │ backlog/review (taste / BLOCKED / needs you) │ backlog/failed
                         ▼
  after a run: "taste-review" skill → you decide → `hx merge <id>` (re-gated on today's main) / feedback card
```

## What's in it
| Path | What |
|---|---|
| `CLAUDE.md` | Studio rules: the code contract, verification, cost discipline, hard "nevers" |
| `.claude/agents/` | producer, gameplay-engineer, qa-playtester, code-reviewer (haiku), tech-artist, marketing-producer (haiku), build-release (haiku) |
| `.claude/skills/` | new-game, playtest, web-release, write-card, run-card, taste-review, art-asset, capture-shorts, steam-page, steam-release |
| `.claude/settings.json` + `harness/hooks/` | permissions (paid/irreversible actions require approval), C# guard hook |
| `.mcp.json` | empty by default; the editor is driven with `unity command`, no MCP needed |
| `.claude-plugin/` | lets other game repos install the agents and skills as a plugin |
| `hx` · `harness/hx.py` | single CLI (`./hx`, switches to `.venv` by itself): `setup doctor verify build web test autotest capture card cards run report cost` |
| `harness/runner.py` | the on-demand card runner |
| `packages/com.studio.harness` | Unity package (editor + harness builds only): `HarnessBuild`, `InputDriver` (virtual devices + trace replay), probes, AutoPilot (`-autotest`), CameraTakes (`-capture`), *Record Input*, smoke tests |
| `scenarios/` | input scripts and metric thresholds (your "feel contract"), read from the main checkout, never from the card's branch |
| `art/` + `harness/art/` | manifest-driven concept → pick → Tripo → Blender cleanup → Unity |
| `marketing/` + `harness/marketing/` | camera takes → ffmpeg shorts → approval → slot schedule → poster |
| `steam/` | store checklist, steamcmd templates, beta-branch upload |
| `scripts/publish_web.sh` | publishes the `./hx web` build to GitHub Pages (needs the director's go-ahead) |

## The key idea: the built game is driven and measured from the outside, with zero harness code in the game
The game is written normally, with **Unity's Input System** (actions / `PlayerInput`). The harness only observes it from outside.

| Need | How (no game code) |
|---|---|
| Play the built game without a human | virtual Keyboard/Mouse/Gamepad devices fed through the Input System (the same technique as Unity's `InputTestFixture`), or **replaying a real session** you recorded with *Harness → Record Input* (`InputEventTrace`) |
| Measure feel | **probes** in the scenario JSON read fields/properties by reflection, e.g. `Player › Rigidbody › linearVelocity.magnitude` |
| Set up a test situation | `send` steps call the game's own methods (`SendMessage`), e.g. `GameManager.StartLevel("1")` |
| Keep it out of the shipped game | the harness assembly only compiles in the editor and in harness builds (`HARNESS` define); `-release` builds contain none of it, and game assemblies can't reference it |

```json
"probes": [{"name": "vehicle.speed", "target": "Player", "component": "Rigidbody", "member": "linearVelocity.magnitude"}],
"steps":  [{"action": "set", "control": "<Gamepad>/rightTrigger", "value": 1, "duration": 4},
           {"action": "screenshot", "name": "full_throttle"}],
"expect": {"vehicle.speed.max": {"min": 6, "max": 12}}
```
A scenario (`scenarios/*.json`) plays the **built** player headlessly (`Game -autotest scenario.json -harnessOut dir`), takes screenshots, writes `metrics.json`, and the runner checks the `expect` thresholds. Once you've tuned something by feel, lock it in with a threshold so agents can't quietly break it. Camera takes use the same input path to play the game while filming it.

The only requirement on a game is that it uses the Input System package, which is the default in Unity 6; the legacy Input Manager can't be driven. A small, optional rule: keep feel values in serialized fields, so they can be tuned and probed.



## Money is optional ($0 is the default)
- **Default `money_budget_usd: 0`:** every agent call runs on your Claude plan's included quota. The runner strips `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, `ANTHROPIC_BASE_URL` and the Bedrock/Vertex/Foundry switches from agent calls, and refuses to start if any settings file sets `apiKeyHelper` or those variables.
- **Turn off extra usage/overage on your Claude account first.** With it on, hitting the plan limit bills money instead of stopping, and no client can tell. The runner won't start a $0 run until you set `runner.plan_overage_disabled: true`.
- **When the quota runs out:** the current card is paused (it stays in `ready`, its worktree, commits and session are kept and resumed later). Then the run stops; run `hx run` again after your quota resets.
- **Money as a boost:** with `hx run --budget 10` (or `money_budget_usd`), real API spend is allowed only for cards marked `paid: true`, for models not in `plan_models`, and for one escalation attempt (Opus) after a card fails on quota. The ledger separates `plan` usage (an estimate, not billed) from `api` (real money); `hx cost` shows both.
- **Art is $0 by default:** `providers` are `manual` (drop in images or models from free tools, CC0 libraries or a sketch) or `comfyui` (local GPU, any image or image-to-3D workflow). Magnific and Tripo are opt-in per project or per asset.
- **Still not free:** the Steam app fee, and music licensing if you use music.

## Validate the harness at $0: `hx selftest`
| Level | Cost | What it proves |
|---|---|---|
| `hx selftest` | $0, no AI | 36 checks: compact output, no hang on Unity's lingering child process, runner merge, writable-path allowlist, suspicious code not executed, main-checkout tamper stop, forged metrics rejected, release scan, BLOCKED → review, quota pause resumes, strict review verdict, billing preflight, agent can't start the runner, taste cards re-gated, key stripping, forced model, protected paths and resumed fix pass, no escalation without money, escalation billed as `api` with money, quota → card back to ready, taste parking, dependencies, thresholds, C# hook, approval-gated posting queue, real ffmpeg short cut to 1080×1920, art pipeline free modes, Blender cleanup (if installed), all config/agent/skill/plugin files |
| `--unity` | $0 | temp sample project → package compiles → headless build → EditMode + PlayMode → autotest (input → movement → metrics → thresholds) → camera capture frames |
| `--live` | plan quota only, **Haiku forced**, $0.05 caps | headless call on your login, project skills load, one real card through the runner |

Run it after setup and after changing the harness. Everything runs in temp copies, so your checkout isn't touched.

## Trust model (what stops an unattended agent from grading its own work)
The gates run code the agent wrote (Editor scripts during the build, tests, the player), so they are treated as hostile:
- **Allowlist:** an agent may only change `runner.agent_writable` (default `game/Assets/` plus two settings files), and not
  `Editor/` folders, `.asmdef`, `.rsp` or native/managed plugins. Anything else needs the card's `allow_paths`.
- **Suspicious code is not run:** added C# lines matching `runner.suspicious_cs` (`InitializeOnLoad`, `Process.Start`,
  `GetCommandLineArgs`, `harnessOut`, reflection on `Harness`, networking…) send the card to `review` with no gates run.
- **Judges come from main:** scenarios are read from the main checkout; the worktree is checked after the gates for judge
  files changed on disk; the main checkout's judges are hashed before and after each card and the run stops on change.
- **Metrics can't be pre-baked:** each autotest gets a random nonce, a fresh output folder, and a plausibility check
  (duration vs. its steps, expected screenshots).
- **Review treats the diff as untrusted:** strict last-line verdict, Sonnet by default.
- **Release builds are proven clean:** the `release` gate builds without `HARNESS` and scans the output for harness code;
  `steam/upload.sh` only uploads a `hx release --to-steam` build of the current clean commit.
- **Real isolation is still yours to switch on:** in-process checks can't stop deliberate, obfuscated code. Set
  `runner.sandbox.prefix` (a separate OS user, firejail, a container) so gate processes can't reach your home, credentials
  or main checkout. Secrets in `sandbox.scrub_env` are always removed from gate processes.
- **Interactive sessions:** `unity command script eval` (arbitrary C# in your editor) asks first; `hx setup`, `hx run`, `hx merge`,
  `hx card ready` and `hx release --to-steam` ask, and `run`/`merge`/`card ready` refuse inside an agent session. The Pipeline
  server listens on localhost only. No MCP server is configured by default (`.mcp.json` is empty).

## Keeping quota/cost down
- Model by card `difficulty`: haiku for trivial cards and the review gate, sonnet by default, opus only for `hard` cards. `force_model` / `HX_FORCE_MODEL=haiku` pins everything to the cheapest model.
- Caps on every run: `--max-budget-usd` per card (on plan quota this keeps one runaway card from eating your quota), `--max-turns`, a wall-clock timeout, and a stop after 3 consecutive failures. The budget cap is a client-side estimate.
- The fix pass resumes the same session, so cached context is reused instead of re-reading the repo.
- Cheap gates run first (paths, suspicious code, then compile); slow ones (PlayMode, autotest) run only after those pass.
- The Unity `Library/` cache is copied into each worktree, which avoids a full reimport per card.
- Paid generation only happens through `harness/art/pipeline.py` with `variants` set per asset and costs logged (`hx cost`); every paid command needs approval in `.claude/settings.json`.

## Things to adapt per game
- `harness/config.yaml`: `build_target` / `player_binary` (`auto` = the OS you are on; autotest has to run the player it built, so a Windows target on a Mac does not work), `plan_models` (what your plan includes), art providers, and API prices for the ledger.
- `harness/runner/runner.settings.json`: deny rules assume the Unity project is at `game/`.
- `art/style.md` and `design/PILLARS.md` (yours).
- Check the endpoint and field names in `harness/art/pipeline.py` (Magnific generation endpoint, Tripo v3 fields) against your plan's current API docs, and use `--dry-run` first.
- TikTok/Instagram posting are manual reminders until you wire an adapter in `queue.py` (YouTube is implemented).
