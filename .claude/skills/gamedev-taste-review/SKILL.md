---
name: gamedev-taste-review
description: Prepare the director's review — summarise runner results and present backlog/review branches with screenshots and metric deltas so they can make fast taste calls. Use for "what happened in the last run", "review queue".
---
1. `./hx report` and `hx cards`.
2. For each card in `backlog/review/`: one block with the goal, metrics before → after (from its report vs. last main report), 2–4 screenshots, and the single question to answer ("Turn radius went 9 m → 6 m; keep?").
3. Cards in `review/` marked BLOCKED or NEEDS YOU (suspicious code, tampering, broken probe): show the reason and the flagged lines first; suspicious code must be read by the director, who then runs `hx merge <id> --trusted`.
4. For `failed/`: one line each — cause class (card unclear / code bug / env issue / budget) and the proposed next step.
5. Spend: session total vs. budget, and the most expensive card.
6. Apply the director's decisions: accepted → the director runs `./hx merge <id>` in their terminal (brings main into the branch, re-runs every gate, merges only if green — never merge by hand); rejected → move to `inbox/` with their feedback appended verbatim. Taste decisions with reasons → `design/TASTE_LOG.md`.
