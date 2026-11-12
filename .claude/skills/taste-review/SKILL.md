---
name: taste-review
description: Prepare the director's morning review — summarise runner results and present backlog/review branches with screenshots and metric deltas so they can make fast taste calls. Use for "what happened overnight", "review queue", "morning".
---
1. `python harness/hx.py report` and `hx cards`.
2. For each card in `backlog/review/`: one block with the goal, metrics before → after (from its report vs. last main report), 2–4 screenshots, and the single question to answer ("Turn radius went 9 m → 6 m; keep?").
3. For `failed/`: one line each — cause class (card unclear / code bug / env issue / budget) and the proposed next step.
4. Spend: session total vs. budget, and the most expensive card.
5. Apply the director's decisions: accepted → `git merge --no-ff card/<id>` and move card to `done/`; rejected → move to `inbox/` with their feedback appended verbatim. Taste decisions with reasons → `design/TASTE_LOG.md`.
