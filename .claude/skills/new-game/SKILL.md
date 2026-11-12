---
name: new-game
description: Bootstrap a new game from this template — Unity project, harness package, Funplay MCP, pillars interview, first backlog. Use once at the start of a project ("start a new game", "set up the template").
---
1. Run `bash scripts/bootstrap.sh "<Game Name>"` (creates/links the Unity project at `game/`, adds the harness package + testables to `Packages/manifest.json`, git init, LFS, .gitignore).
2. Tell the director to: open `game/` in Unity 6 once, install Funplay MCP from its Git URL in Package Manager, start it (Funplay → MCP Server), then run `/mcp` here to confirm `funplay` connects.
3. Interview the director (≤8 questions, one round) for `design/PILLARS.md`: the one-sentence fantasy, core loop, 3 pillars, what it is NOT, target session length, art direction references, platforms, scope (weeks). Write their answers in their words.
4. Write `art/style.md` from the art direction answer (prompt prefix, palette, negative prompt).
5. Create the first cards with `write-card`: input bootstrap wiring `HarnessInput.LiveAxis` to the Input System, a greybox scene with camera, the core-loop prototype, and the first handling scenario (thresholds TBD by the director).
