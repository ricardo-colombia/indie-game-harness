---
name: run-card
description: Protocol for implementing one backlog card unattended inside a runner worktree. Used by harness/runner.py; also usable interactively ("run card C-0012").
---
You are alone, on a budget, on branch `card/<id>`. Nobody will answer questions. Optimise for a change that passes the independent gates on the first try.

1. **Understand (≤10% of budget).** Re-read the card's Goal and Acceptance criteria. Grep for the systems named; read only relevant parts. If the card is ambiguous, pick the most conservative reading and say so in your final summary.
2. **Plan.** Write a 3–6 item TodoWrite list. If the card clearly can't be done within its scope (needs taste decisions, new assets, or >1 system), STOP: commit nothing and put a line starting with `BLOCKED: <why> — suggest split: ...` in your final message. The runner sends it to the director; that's a success, not a failure.
3. **Implement** per CLAUDE.md's code contract, delegating to the `gameplay-engineer` approach. Add/extend tests. If the card lists `scenarios`, make sure the fields their probes read exist and are named as the scenario expects.
4. **Self-verify.** `./hx verify --scenarios <card scenarios>`. On failure read the specific error lines, fix, re-run. Max 3 verify loops — builds are slow; compile-check reasoning first.
5. **Commit** with `git add -A && git commit -m "<id>: <title>"`. Never push or merge. Change only `game/Assets/` (not `Editor/` folders, `.asmdef`, plugins) unless the card's `allow_paths` lists the path. If a scenario probe points at a field you renamed, restore the name — scenarios are the director's.
6. **Finish** with ≤15 lines: what changed, metrics before→after, risks, and any taste question for the director.
