# Indie Game Harness

A template for making small Unity 6 games with an AI team in Claude Code.
**You are the creative director:** agents write code, build, test, make assets and cut clips on demand; you decide what's fun and what ships.

## 1. Set up (once per game)
1. Create a repo from this template, then run `bash scripts/bootstrap.sh "My Game"` and follow its printout.
2. Install the [Unity CLI](https://unity.com/install.sh) and a Unity 6 editor (`unity install lts`), plus `ffmpeg`, Blender, `uv` (and `xvfb` on headless Linux). The editor and Blender are found automatically. Log in to Claude Code with your plan: `claude` → `/login`.
3. Turn off extra usage on your Claude account, then set `runner.plan_overage_disabled: true` in `harness/config.yaml`.
4. Check everything works: `python harness/hx.py selftest --all` ([details](TESTING.md)).
5. In Claude Code: *"use the new-game skill"* → pillars interview, style guide, first cards.

## 2. Daily loop
| When | You do | Command / skill |
|---|---|---|
| Day | Playtest and tune in the open editor | `playtest` skill |
| Day | Turn feedback into cards | `write-card` skill → `backlog/inbox/` |
| Any time | Approve cards to be worked on | `hx card ready C-0001` |
| Any time | Agents work through `backlog/ready/` | `hx run` |
| After a run | Review results, make taste calls | `taste-review` skill → `hx merge C-0001` |

Cards move `inbox → ready → done` (merged), `review` (needs you) or `failed`. A card is one Markdown file; see `backlog/_template.md`.

## 3. Commands
`hx` = `python harness/hx.py`

| Command | What |
|---|---|
| `hx cards` · `hx card new "Title"` · `hx card ready <id>` | list, create, approve cards |
| `hx run [--budget 10] [--dry-run]` | work through ready cards, on demand; $0 (plan quota) by default |
| `hx merge <id>` | re-check a reviewed card on current main and merge it |
| `hx verify` · `hx build` · `hx test EditMode` · `hx autotest smoke` | run gates by hand |
| `hx report` · `hx cost` | last results, spend |
| `hx capture <takes.json>` · `hx release [--to-steam]` | marketing footage, clean release build |

Other skills: `art-asset`, `capture-shorts`, `steam-page`, `steam-release`, `run-card`.

## 4. Make it yours
- `design/PILLARS.md`, `art/style.md` — your vision (written by `new-game`).
- `harness/config.yaml` — build target, binary name, models, art providers.
- `scenarios/*.json` — your "feel contract": once something feels right, lock it with a metric threshold so agents can't break it.

The only requirement on the game: use Unity's **Input System** (the Unity 6 default).

## More
- [docs/REFERENCE.md](docs/REFERENCE.md) — architecture, money rules, trust model, cost tips
- [TESTING.md](TESTING.md) — validating the harness at $0
