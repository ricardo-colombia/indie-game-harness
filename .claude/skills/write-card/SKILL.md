---
name: write-card
description: Turn notes, design ideas, playtest feedback or bug reports into well-formed backlog cards the runner can complete. Use for "add to backlog", "make cards from these notes", "plan this feature".
---
1. Create each card with `./hx card new "<imperative title>" --type <feature|bug|tuning|art|marketing|chore>`; it lands in `backlog/inbox/` from `backlog/_template.md`.
2. Fill it in:
   - **Goal**: one sentence of player-facing outcome.
   - **Acceptance criteria**: checkable statements — a test name, a metric threshold (`vehicle.speed.max between 6 and 12 in scenario drive_straight`), or "screenshot X shows Y". No "feels good".
   - **Context**: file paths / systems involved (grep to confirm they exist), links to `design/` notes, the director's words verbatim for feedback.
   - **Out of scope**: what not to touch.
3. Front matter: `difficulty` (trivial/normal/hard → haiku/sonnet/opus), `budget_usd` (trivial 0.5–1, normal 2–4, hard 5–10), `scenarios`, `depends_on`, `taste_review: true` for anything the director must feel/see before merge. Only add `paid: true` if the card truly can't be done on the plan quota (it then waits for a funded run). New scenario files need `allow_paths: [scenarios/<name>.json]` and the thresholds come from the director.
4. Split if it touches more than one system, needs assets that don't exist yet, or you can't write an objective acceptance check.
5. Show the director the list (id, title, difficulty, budget, taste?) and stop. They promote with `hx card ready <id>`.
