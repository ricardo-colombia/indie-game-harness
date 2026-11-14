---
name: gamedev-steam-page
description: Prepare Steam store page assets and copy — capsules, screenshots, trailer, description, tags, AI disclosure — as a checklist-driven package for the director to upload. Use for "Steam page", "store assets", "capsule art".
---
Work from `steam/STORE_CHECKLIST.md`; tick items as files land in `steam/store/`.

- **Screenshots**: run `hx autotest` with a `store_shots` scenario (16:9, 1920×1080+, gameplay only, no UI debug), pick the best 5–10 with the director.
- **Capsules**: compose from a director-picked key art + logo; export every required size listed in the checklist (Steam rejects wrong sizes). Logo/title must be readable at the small capsule size — check by downscaling.
- **Trailer**: cut from capture takes with `cut_shorts.py` using a 16:9 profile; gameplay in the first 5 seconds.
- **Copy**: short description (≤ 300 chars) leading with the verb the player does; long description with 3–5 bolded feature lines + GIFs. Draft only; the director edits and approves.
- **AI disclosure**: draft an accurate description of AI use (pre-generated content: code, concept art, 3D models) for Steamworks' content survey. Accuracy over marketing.
You never upload to Steamworks yourself; produce the package and the checklist status.
