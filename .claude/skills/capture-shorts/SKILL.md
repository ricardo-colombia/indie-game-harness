---
name: capture-shorts
description: Film scripted camera takes inside the built game, cut them into vertical shorts, and queue them for posting after approval. Use for shorts, trailers, GIFs and devlog clips.
---
1. **Takes**: write/extend `marketing/takes/<set>.json` (see `launch.json`): orbit/dolly/follow/static shots, each with optional gameplay `steps` so the game plays itself while filmed. Target GameObjects by name.
2. **Build + film**: `python harness/hx.py build` then `python harness/hx.py capture marketing/takes/<set>.json` → PNG sequences in `marketing/out/raw/<set>/<take>/` at a locked frame rate.
3. **Cut**: define shorts in `marketing/shorts.yaml` (takes + in/out seconds, caption, optional music) then `python harness/marketing/cut_shorts.py marketing/shorts.yaml`. Outputs `marketing/out/<short>.mp4` (1080×1920, H.264) and adds each to `marketing/schedule.yaml` as `approved: false`.
4. **Review (HUMAN)**: send the director the mp4s. They set `approved: true` (and can edit captions).
5. **Schedule/post**: `python harness/marketing/queue.py schedule` assigns approved items to free slots; `queue.py post --due` (run from cron) posts due items via configured adapters. Default is dry-run; `--live` only when the director has set up credentials and said go.
