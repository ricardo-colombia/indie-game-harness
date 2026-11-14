---
name: new-game
description: Bootstrap a new game from this template and get to a playable first version in one session — setup, a short pillars interview, a code-built prototype, a browser build. Use once at the start of a project ("start a new game", "quiero hacer un juego de …").
---
Goal of this session: **the director plays something by the end of it.** Plans and cards come after.

1. **Set up (≈2 min).** `./hx doctor`. If a required item is red, tell the director what setup will do (create the Unity project in `game/`, make `.venv`, add the harness and Pipeline packages; if no Unity 6 editor is installed it downloads one, several GB: ask first) and run `./hx setup "<Game Name>"` (add `--yes` once they agree to the download). If the Unity CLI is missing, give them the install command it prints.
2. **Interview (one round, ≤5 questions).** One-sentence fantasy; core loop; 3 pillars; what it is NOT; platforms (web/phone? Steam?); and if it is based on an existing game, ask for the **reference image or rules** now. Write the answers in their words to `design/PILLARS.md` and a 3-line palette/look to `art/style.md`.
3. **Open the editor** (`unity open game`, then `unity status --until-ready --project-path game`; first import takes a few minutes. Start it before interviewing so it imports meanwhile).
4. **Build the first playable, live** (CLAUDE.md code contract applies):
   - Rules in plain C# classes + EditMode tests; a small bootstrap MonoBehaviour that builds the scene from code. Input System actions with keyboard and touch (swipe/tap) from the start.
   - Procedural look and synthesized sound; no asset imports.
   - Play it with the `playtest` skill: enter play mode, drive input, screenshot, fix what is visibly wrong before showing it.
5. **Make it shareable.** `./hx web --serve` (editor closed) and tell the director the localhost link. Use `web-release` only when they say to publish.
6. **Hand over.** Show screenshots + the link, ask for taste feedback, log decisions in `design/TASTE_LOG.md`. Only now turn feedback into cards (`write-card`) and, if they want polish in parallel, explain `./hx card ready` + `./hx run`.
