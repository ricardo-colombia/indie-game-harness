"""Deterministic model cleanup. Runs inside Blender:
blender -b --factory-startup -P harness/art/blender_cleanup.py -- --in raw.glb --out game/Assets/Art/Models/x/x.fbx
        --name x --height 2.4 --faces 8000 --split none|loose|material
Steps: import → unparent/keep transforms → join meshes → merge by distance → decimate to face budget →
scale to target height (m) → origin at bottom-centre → recalc normals → optional split → export FBX for Unity.
Prints a JSON stats line for the agent to read."""
import argparse
import json
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
p = argparse.ArgumentParser()
p.add_argument("--in", dest="src", required=True)
p.add_argument("--out", required=True)
p.add_argument("--name", default="asset")
p.add_argument("--height", type=float, default=0)
p.add_argument("--faces", type=int, default=0)
p.add_argument("--split", default="none", choices=["none", "loose", "material"])
p.add_argument("--merge-dist", type=float, default=0.0005)
a = p.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
ext = Path(a.src).suffix.lower()
if ext in (".glb", ".gltf"):
    bpy.ops.import_scene.gltf(filepath=a.src)
elif ext == ".fbx":
    bpy.ops.import_scene.fbx(filepath=a.src)
elif ext == ".obj":
    bpy.ops.wm.obj_import(filepath=a.src)
else:
    raise SystemExit(f"unsupported input {ext}")

meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not meshes:
    raise SystemExit("no meshes imported")
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
for o in [o for o in bpy.context.scene.objects if o.type != "MESH"]:
    bpy.data.objects.remove(o, do_unlink=True)
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = a.name
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bm = bmesh.new()
bm.from_mesh(obj.data)
before = len(bm.faces)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=a.merge_dist)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(obj.data)
bm.free()

faces = len(obj.data.polygons)
if a.faces and faces > a.faces:
    mod = obj.modifiers.new("decimate", "DECIMATE")
    mod.ratio = a.faces / faces
    bpy.ops.object.modifier_apply(modifier=mod.name)

if a.height > 0:
    zs = [v.co.z for v in obj.data.vertices]
    h = max(zs) - min(zs)
    if h > 0:
        obj.data.transform(Matrix.Scale(a.height / h, 4))

vs = [v.co for v in obj.data.vertices]
lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
pivot = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
obj.data.transform(Matrix.Translation(-pivot))
obj.location = (0, 0, 0)
for poly in obj.data.polygons:
    poly.use_smooth = True

parts = [obj]
if a.split != "none":
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE" if a.split == "loose" else "MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for i, o in enumerate(sorted(parts, key=lambda o: -len(o.data.polygons))):
        o.name = f"{a.name}_{i:02d}"

Path(a.out).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
if a.out.lower().endswith(".fbx"):
    bpy.ops.export_scene.fbx(filepath=a.out, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True)
else:
    bpy.ops.export_scene.gltf(filepath=a.out, use_selection=True)

dims = hi - lo  # measured after scaling, before the pivot move (translation doesn't change size)
print("HARNESS_STATS " + json.dumps({
    "faces_before": before, "faces_after": sum(len(o.data.polygons) for o in parts), "parts": len(parts),
    "size_m_xyz_blender": [round(dims.x, 3), round(dims.y, 3), round(dims.z, 3)], "out": a.out}))
