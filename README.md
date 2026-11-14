# Indie Game Harness

Make a small Unity 6 game by talking to Claude. You decide what's fun; Claude builds, plays and tests it.
`./hx` is the harness CLI (`python3 harness/hx.py`; on Windows `hx`).

## Your first game in ~15 minutes

**Once per machine:** install [Claude Code](https://claude.com/claude-code) and sign in, and install the Unity CLI: `curl -fsSL https://unity.com/install.sh | bash` (sign in to Unity when asked). No Unity 6 editor? Setup asks before downloading one (several GB).

1. **Clone this repo** (or "Use this template" on GitHub) and open the folder in Claude Code.
2. **Say what game you want**, e.g. *"quiero un juego tipo Snake para el celular"*. Claude runs `./hx setup` (Unity project, packages, `.venv`), asks ≤5 questions, and builds a playable first version with you watching.
3. **Play it.** Claude opens the editor, plays, and shows you screenshots. Say what feels wrong: *"make the jump floatier"*.
4. **Share it.** *"Give me a link"* → `./hx web --serve` plays it in your browser; publishing to GitHub Pages (so you can open it on your phone) happens only when you say so.

Keep going by talking. When you want polish done in parallel, or you want the game protected by tests: *"add to backlog: …"* → cards in `backlog/inbox/` → `./hx card ready C-0001` → `./hx run` (agents implement every approved card and test it) → *"what happened?"* → `./hx merge C-0001`. Before your first `./hx run`, turn off extra usage on your Claude account and set `runner.plan_overage_disabled: true` in `harness/config.yaml`, so runs use your plan and cost $0 extra.

## Where things live

| File | What |
|---|---|
| `design/PILLARS.md`, `art/style.md` | your vision; agents read it, never edit it |
| `backlog/{inbox,ready,review,done,failed}/` | cards; only you move inbox → ready |
| `harness/config.yaml` | game name, build target (default: your OS), models, budgets |
| `scenarios/*.json` | the "feel contract": lock what feels right with a metric threshold |

## Commands

| Command | What |
|---|---|
| `./hx doctor` · `./hx setup "Name"` | check requirements · fix them and create the Unity project |
| `./hx web [--serve]` | browser build in `game/Builds/Web`; `--serve` plays it at localhost:8000 |
| `bash scripts/publish_web.sh` | publish that build to GitHub Pages (Claude asks you first) |
| `./hx verify` · `./hx build` · `./hx autotest smoke` | run the checks by hand (close the Unity editor first: it locks the project) |
| `./hx cards` · `./hx card new "Title"` · `./hx card ready <id>` | list, create, approve cards |
| `./hx run [--dry-run] [--budget 10]` · `./hx merge <id>` · `./hx report` · `./hx cost` | work through approved cards, merge, results, spend |

Also: skills `playtest`, `web-release`, `art-asset`, `capture-shorts`, `steam-page`, `steam-release`. Internals: [docs/REFERENCE.md](docs/REFERENCE.md). Checking the harness itself: [TESTING.md](TESTING.md).

The only requirement on the game: use Unity's **Input System** (the Unity 6 default).
The first game made with this template was a Pac-Man clone (branch `pacman-game`); what it taught is in `CLAUDE.md` → "Lessons".
