---
name: gamedev-animation-set
description: Produce, validate and wire a character's animation set (clip list, Blender export, spec check, Unity Animator) against art/animation_spec.yaml. Use for "animate this character", "add a run cycle", "export the clips", "is this rig valid".
---
The contract is data: `art/animation_spec.yaml` (clip vocabulary with loop flags, profiles, skeleton budget, transitions). Don't invent names; add to the spec first.

1. **Brief** — `animation-director` agent: choose `animation_profile` (`platformer_hero`, `platformer_enemy`, `npc`), list clips with frames, loop/one-shot, key poses. Put `kind`, `animation_profile`, `depends_on` in the asset's `art/manifest.yaml` entry.
2. **Rig + animate** (Blender, by `tech-artist` or the director's own file): one armature, root bone `root`, ≤ 4 influences/vertex, ≤ `max_bones`, transforms applied, 1 unit = 1 m. One Blender *action per clip*, named exactly like the clip (`Idle`, `Run`…). Loop clips: last frame = first frame. Expressions are shape keys `Expr_Happy` etc., not clips.
   The Blender MCP / `execute_blender_code` can build and key poses; save the result as a `.blend` in `art/blender/` (LFS).
3. **Export** — `./hx assets export <id> art/blender/<id>.blend` writes `<id>.fbx` + `<id>@<Clip>.fbx` for every action into `game/Assets/Art/Models/<id>/` with the right settings (no leaf bones, Y-up/-Z fwd, FBX_SCALE_ALL, one clip per file) and fills `animations`/`outputs`. Actions with names outside the spec are refused, not guessed (`--map Walk_v2=Walk` to rename on purpose).
4. **Validate** — `./hx assets check <id>`: required clips present, root bone, bone budget, influences, model height vs `height_m`, **loop seams measured in metres**. Report format: passed / failed / warnings / skipped / fixes; "skipped" (no Blender) is never a pass.
5. **Wire in Unity** (open editor, `unity command`): set the FBX Rig to Generic (or Humanoid only for a true biped), loop flags from the spec, build an AnimatorController whose states are the clips and whose transitions are the spec's `transitions` (`*` = Any State; damage interrupts anything; `Death` has no exit). Drive it from game state through parameters set by a thin MonoBehaviour — the rules stay in plain C#. Game code never touches the harness.
6. **Look at it** — `gamedev-playtest`: play every clip in engine on the real controller, capture a frame strip, check for foot sliding (stride vs controller speed) and that idle breathes. Then `art-director`/`animation-director` write the review.
7. **Approval is the director's.** Present the strips + report (`taste_review: true`). Only after their yes do they run `./hx assets approve <id>`; from then on the files are locked.

Limits to state honestly: a generated mesh is rarely rig-ready (retopology and weight painting are real work); auto-rigs and AI motion are drafts; clip *feel* can't be measured by the harness, only structure, loops, timing and in-engine behaviour.
