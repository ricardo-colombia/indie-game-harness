---
id: C-0000
title: TITLE
type: feature            # feature | bug | tuning | art | marketing | chore
priority: 3              # 1 = highest
difficulty: normal       # trivial (haiku) | normal (sonnet) | hard (opus)
budget_usd: 3.0
timeout_min: 45
max_turns: 80
attempts: 2              # 2 = one automatic fix pass after gate failure
scenarios: []            # autotest scenarios that must pass (smoke always runs)
gates: [build, release, editmode, playmode, autotest, review]
taste_review: false      # true = passes gates, then waits for the director instead of merging
depends_on: []
allow_paths: []          # protected paths this card may change, e.g. scenarios/new_thing.json
---
## Goal
One sentence: what the player experiences differently.

## Acceptance criteria
- [ ] Objective check (test name / metric threshold in a scenario / screenshot shows X)

## Context
Files and systems involved; director's feedback verbatim.

## Out of scope
-
