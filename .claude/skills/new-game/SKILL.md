---
name: new-game
description: Bootstrap a new game from this template — Unity project, harness package, pillars interview, first backlog. Use once at the start of a project ("start a new game", "set up the template").
---
1. Run `python harness/hx.py doctor`. If a required item is red, tell the director what setup will do (create the Unity project in `game/`, add the harness and Pipeline packages; if no Unity 6 editor is installed it downloads one, several GB: ask first) and run `python harness/hx.py setup "<Game Name>"` (add `--yes` once they agree to the download). If the Unity CLI is missing, give them the install command it prints.
2. Run `python harness/hx.py selftest` (offline, ~30 s) and fix anything red before writing cards.
3. Open the editor: `unity open game`, then `unity status --until-ready --project-path game` (the first import takes a few minutes).
4. Interview the director (≤8 questions, one round) for `design/PILLARS.md`: the one-sentence fantasy, core loop, 3 pillars, what it is NOT, target session length, art direction references, platforms, scope (weeks). Write their answers in their words.
5. Write `art/style.md` from the art direction answer (prompt prefix, palette, negative prompt).
6. Create the first cards with `write-card`: Input System action map for the core controls, a greybox scene with camera, the core-loop prototype, and the first handling scenario (thresholds TBD by the director).
