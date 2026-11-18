# Indie Game Harness

Make a small Unity 6 game by talking to Claude. You decide what's fun; Claude builds, plays and tests it.
`./hx` is the harness CLI (`python3 harness/hx.py`; on Windows `hx`).

## Your first game in ~15 minutes

**Once per machine:** install [Claude Code](https://claude.com/claude-code) and sign in, and install the Unity CLI: `curl -fsSL https://unity.com/install.sh | bash` (sign in to Unity when asked). No Unity 6 editor? Setup asks before downloading one (several GB).

1. **Get a clean copy of the template** and open the folder in Claude Code. The template lives on the `template` branch (`game/` empty, name `My Game`); `main` may hold a finished game.
   ```bash
   git clone --branch template https://github.com/ricardo-colombia/indie-game-harness.git my-game
   ```
   "Use this template" on GitHub copies the repo's *default* branch, so it gives the clean template only if the owner set Settings → General → Default branch to `template`.
2. **Say what game you want**, e.g. *"quiero un juego tipo Snake para el celular"*. Claude runs `./hx setup` (Unity project, packages, `.venv`), asks ≤5 questions, and builds a playable first version with you watching.
3. **Play it.** Claude opens the editor, plays, and shows you screenshots. Say what feels wrong: *"make the jump floatier"*.
4. **Share it.** *"Give me a link"* → `./hx web --serve` plays it in your browser; publishing to GitHub Pages (so you can open it on your phone) happens only when you say so.

Keep going by talking. When you want polish done in parallel, or you want the game protected by tests: *"add to backlog: …"* → cards in `backlog/inbox/` → `./hx card ready C-0001` → `./hx run` (agents implement every approved card and test it) → *"what happened?"* → `./hx merge C-0001`. Before your first `./hx run`, turn off extra usage on your Claude account and set `runner.plan_overage_disabled: true` in `harness/config.yaml`, so runs use your plan and cost $0 extra.

## Maintaining the template

- Template changes go on the `template` branch, never on a game's branch. Keep `game/` empty, `harness/config.yaml` `name: My Game`, and run `python3 harness/selftest.py` (all green) before pushing.
- Bringing the template into a game that already has its own `art-director`, `animation-director` or `environment-artist` agents (or a custom `gamedev-pro-playbook` skill): commit the game's versions first, then in the merge keep the game's copies of those files and take everything else from the template. The template's agents are generic and must not overwrite game-specific ones.

## Where things live

| File | What |
|---|---|
| `design/PILLARS.md`, `art/style.md` | your vision; agents read it, never edit it |
| `art/manifest.yaml`, `art/animation_spec.yaml` | asset registry and the clip/skeleton contract; approved art is locked (`art/locks.json`) |
| `backlog/{inbox,ready,review,done,failed}/` | cards; only you move inbox → ready |
| `harness/config.yaml` | game name, build target (default: your OS), models, budgets |
| `scenarios/*.json` | the "feel contract": lock what feels right with a metric threshold |

## Commands

| Command | What |
|---|---|
| `./hx doctor` · `./hx setup "Name"` | check requirements · fix them and create the Unity project |
| `./hx web [--serve]` | browser build in `game/Builds/Web`; `--serve` plays it at localhost:8000 |
| `bash scripts/publish_web.sh` | publish that build to GitHub Pages (Claude asks you first) |
| `./hx verify [--only editmode build]` · `./hx build` · `./hx autotest smoke` | run the checks by hand (close the Unity editor first: it locks the project). A passing step prints one line; failures print the error excerpt; `--metrics` adds the numbers |
| `./hx cards` · `./hx card new "Title"` · `./hx card ready <id>` | list, create, approve cards |
| `./hx assets check` · `status` · `export <id> <rig.blend>` · `approve <id>` · `new-version <id>` | art registry: validate (skeleton, clips, loop seams), export a rigged `.blend` to Unity clips, lock approved art (you approve, agents never) |
| `./hx run [--dry-run] [--budget 10]` · `./hx merge <id>` · `./hx report` · `./hx cost` | work through approved cards, merge, results, spend |

**Keeping it cheap:** with the editor open, Claude iterates with `unity test --mode EditMode` and one screenshot per change, and runs `./hx verify` once before calling something done. The rules are in `CLAUDE.md` → "Cost discipline"; `./hx run` cards are capped at 50 turns / $2 / 30 min (`runner.defaults` in `harness/config.yaml`).

Also: skills `gamedev-playtest`, `gamedev-platformer`, `gamedev-web-release`, `gamedev-art-asset`, `gamedev-animation-set`, `gamedev-capture-shorts`, `gamedev-steam-page`, `gamedev-steam-release`. Internals: [docs/REFERENCE.md](docs/REFERENCE.md). Checking the harness itself: [TESTING.md](TESTING.md).

The only requirement on the game: use Unity's **Input System** (the Unity 6 default).
The first game made with this template was a Pac-Man clone (branch `pacman-game`); what it taught is in `CLAUDE.md` → "Lessons".
