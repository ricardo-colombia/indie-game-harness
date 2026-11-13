# Indie Game Harness

Make a small Unity 6 game by talking to Claude. You decide what's fun; agents build and test.
`hx` below = `python3 harness/hx.py`.

## Make your first game

1. **Create a repo** from this template, clone it, open the folder in Claude Code.
2. **Say what game you want.** Claude checks requirements, creates the Unity project (`hx setup`), interviews you and writes the pillars (`design/PILLARS.md`), the style (`art/style.md`) and the first cards.
   Once per machine: install the Unity CLI (`curl -fsSL https://unity.com/install.sh | bash`) and sign in to Unity and Claude. If you have no Unity 6 editor, setup asks before downloading one (several GB).
3. **Play it.** `unity open game`, then tell Claude "try it" or "make the jump floatier".
4. **Turn feedback into cards.** Tell Claude "add to backlog: …". Cards are Markdown files in `backlog/inbox/`; edit them freely.
5. **Approve and run.** `hx card ready C-0001`, then `hx run`. Agents implement every approved card and test it.
6. **Review.** Ask "what happened?". Merge what you like with `hx merge C-0001`.

Repeat 3–6. Before your first `hx run`, turn off extra usage on your Claude account and set `runner.plan_overage_disabled: true` in `harness/config.yaml`, so runs use your plan and cost $0 extra.

## Where things live

| File | What |
|---|---|
| `design/PILLARS.md`, `art/style.md` | your vision; agents read it, never edit it |
| `backlog/{inbox,ready,review,done,failed}/` | cards; only you move inbox → ready |
| `harness/config.yaml` | game name, build target, models, budgets |
| `scenarios/*.json` | the "feel contract": lock what feels right with a metric threshold |

## Commands

| Command | What |
|---|---|
| `hx doctor` | check requirements |
| `hx cards` · `hx card new "Title"` · `hx card ready <id>` | list, create, approve cards |
| `hx run [--dry-run] [--budget 10]` | work through approved cards |
| `hx merge <id>` · `hx report` · `hx cost` | merge, last results, spend |
| `hx verify` · `hx build` · `hx autotest smoke` | run the checks by hand |

Also: skills `art-asset`, `capture-shorts`, `steam-page`, `steam-release`. Internals: [docs/REFERENCE.md](docs/REFERENCE.md).

The only requirement on the game: use Unity's **Input System** (the Unity 6 default).
