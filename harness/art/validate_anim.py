"""Measure rigs and clips, headless. Runs inside Blender; judging against art/animation_spec.yaml is done by assets.py.
blender -b --factory-startup -P harness/art/validate_anim.py -- file1.fbx file2.fbx ...
Prints one `HARNESS_ANIM {json}` line per file. Never judges, never fabricates: unreadable files report an `error`."""
import json
import sys
from pathlib import Path

import bpy

files = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def seam(arm, act):
    """Largest distance (m) any bone has to travel between the first and last frame, root motion removed."""
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    if getattr(arm.animation_data, "action_slot", 1) is None and act.slots:  # Blender 4.4+ slotted actions
        arm.animation_data.action_slot = act.slots[0]
    start, end = int(act.frame_range[0]), int(act.frame_range[1])
    scale = arm.matrix_world.to_scale().x or 1.0
    poses = []
    for f in (start, end):
        bpy.context.scene.frame_set(f)
        ev = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())  # the animated pose lives on the evaluated copy
        root = next(b for b in ev.pose.bones if b.parent is None)
        o = root.head.copy()  # armature space; heads AND tails, so a bone that only rotates about its head still counts
        poses.append({f"{b.name}.{k}": (getattr(b, k) - o) * scale for b in ev.pose.bones for k in ("head", "tail")})
    worst = max(((poses[0][n] - poses[1][n]).length, n.rsplit(".", 1)[0]) for n in poses[0])
    return worst


def measure(path):
    sc = bpy.context.scene
    arms = [o for o in sc.objects if o.type == "ARMATURE"]
    meshes = [o for o in sc.objects if o.type == "MESH"]
    bones = [b for a in arms for b in a.data.bones if not b.name.endswith("_end")]
    out = {"file": path, "armatures": len(arms), "bones": len(bones),
           "root_bones": [b.name for b in bones if b.parent is None], "fps": sc.render.fps / sc.render.fps_base}
    infl = 0
    zs = []
    for o in meshes:
        for v in o.data.vertices:
            infl = max(infl, sum(1 for g in v.groups if g.weight > 0.001))
            zs.append((o.matrix_world @ v.co).z)
    out["max_influences"] = infl
    out["height_m"] = round(max(zs) - min(zs), 3) if zs else None
    out["actions"] = []
    for act in sorted(bpy.data.actions, key=lambda a: a.name):
        s, e = act.frame_range
        item = {"name": act.name, "frame_start": int(s), "frames": int(e - s) + 1, "seam_m": None, "seam_bone": None}
        if arms and len(arms[0].pose.bones):
            item["seam_m"], item["seam_bone"] = seam(arms[0], act)
            item["seam_m"] = round(item["seam_m"], 4)
        out["actions"].append(item)
    return out


for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        ext = Path(f).suffix.lower()
        if ext in (".glb", ".gltf"):
            bpy.ops.import_scene.gltf(filepath=f)
        elif ext == ".fbx":
            bpy.ops.import_scene.fbx(filepath=f)
        else:
            raise ValueError(f"unsupported {ext}")
        print("HARNESS_ANIM " + json.dumps(measure(f)))
    except Exception as e:  # report, don't hide
        print("HARNESS_ANIM " + json.dumps({"file": f, "error": f"{type(e).__name__}: {e}"[:300]}))
