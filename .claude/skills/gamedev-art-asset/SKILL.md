---
name: gamedev-art-asset
description: Concept → pick → 3D model → Blender cleanup → Unity import, driven by art/manifest.yaml. Use for creating or replacing any 3D asset, texture or material set.
---
Each asset in `art/manifest.yaml` moves through `stage`: `concept → pick → model → cleanup → import → done`. Run one stage for one batch, then stop at human gates.

1. **concept** (paid): `python harness/art/pipeline.py concept <asset>` — Magnific generates `variants` images from the prompt + `art/style.md`. Output `art/concepts/<asset>/`.
2. **pick** (HUMAN): show the images; the director writes `picked: <file>` (and notes). Never pick for them.
3. **model** (paid): `pipeline.py model <asset>` — Tripo image-to-model with `face_limit`; downloads the GLB immediately (URLs expire in minutes).
4. **cleanup** (free): `pipeline.py cleanup <asset>` — Blender headless: apply transforms, merge by distance, decimate to budget, scale to `height_m`, origin bottom-center, optional split by loose parts/material, export FBX into the Unity project.
5. **import**: verify in the open editor (`unity command`) — bounds match `height_m`, materials URP Lit, colliders simple. Screenshot beside a 1.8 m capsule. Director approves → `done`.

Textures/material maps: `pipeline.py texture <asset>` uses Magnific for tileable albedo + upscaling; derive normal/roughness in Blender/Unity rather than paying per map unless the manifest says otherwise.
Costs land in `harness/ledger.jsonl` (`kind: art`). Check `hx cost` before big batches.
