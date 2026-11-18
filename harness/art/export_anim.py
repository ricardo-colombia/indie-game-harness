"""Export a rigged .blend for Unity: the model plus one FBX per action, named <asset>@<Clip>.fbx.
Use `hx assets export <asset> rig.blend` (it passes --clips/--fps from art/animation_spec.yaml; Blender's Python has no yaml).
blender -b rig.blend --factory-startup -P harness/art/export_anim.py -- --asset hero --out game/Assets/Art/Models/hero --clips Idle,Run [--fps 30] [--map Action.Name=Clip]
Action names must equal clip names or be mapped with --map; anything else is refused, not guessed.
Settings follow the spec's skeleton rules: one armature, no leaf bones, Y-up/-Z forward, scale 1, 30 fps."""
import argparse
import json
import sys
from pathlib import Path

import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
p = argparse.ArgumentParser()
p.add_argument("--asset", required=True)
p.add_argument("--out", required=True)
p.add_argument("--clips", required=True, help="comma list of allowed clip names (from art/animation_spec.yaml)")
p.add_argument("--fps", type=int, default=30)
p.add_argument("--map", action="append", default=[])
a = p.parse_args(argv)

clips = set(a.clips.split(","))
rename = dict(m.split("=", 1) for m in a.map)
arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
if len(arms) != 1:
    raise SystemExit(f"need exactly 1 armature, found {len(arms)}")
arm = arms[0]
bpy.context.scene.render.fps = a.fps
out = Path(a.out)
out.mkdir(parents=True, exist_ok=True)

common = dict(apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
              bake_anim=False, path_mode="COPY", use_armature_deform_only=True)


def select(types):
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.context.scene.objects:
        if o.type in types:
            o.select_set(True)


select({"ARMATURE", "MESH"})
bpy.ops.export_scene.fbx(filepath=str(out / f"{a.asset}.fbx"), use_selection=True, **common)

delivered, refused = [], []
for act in sorted(bpy.data.actions, key=lambda x: x.name):
    clip = rename.get(act.name, act.name)
    if clip not in clips:
        refused.append(act.name)
        continue
    arm.animation_data_create().action = act
    if getattr(arm.animation_data, "action_slot", 1) is None and act.slots:
        arm.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = int(act.frame_range[0]), int(act.frame_range[1])
    select({"ARMATURE"})
    bpy.ops.export_scene.fbx(filepath=str(out / f"{a.asset}@{clip}.fbx"), use_selection=True, bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_simplify_factor=0.0, **{k: v for k, v in common.items() if k != "bake_anim"})
    delivered.append(clip)
print("HARNESS_EXPORT " + json.dumps({"asset": a.asset, "clips": delivered, "refused_actions": refused, "out": str(out)}))
if refused:
    sys.exit(f"actions not in art/animation_spec.yaml (rename them or pass --map): {refused}")
