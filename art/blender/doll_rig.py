"""Rig a painted doll in Blender and render its walk as a flipbook.
  Blender -b -P art/blender/doll_rig.py -- <name> <outdir> [view...]      (run doll_rig_prep.py <name> first)
Standard game-production route: textured grid mesh + armature, Blender's own automatic weights (bone heat), the CMU motion-capture
walk (art/mocap, retargeted by mocap.py) drives the bones, each pose is rendered with an orthographic camera.
The paper figure lives in the X-Z plane (front/back) or the Y-Z plane (side), so limbs that swing along the walking direction
foreshorten by themselves when seen from the front. Output: <outdir>/<name>_<walk|idle>_<s|e|n>_<00-07>.png (pack.py makes the sheet)."""
import sys, os, json, math
import bpy, bmesh
from mathutils import Vector, Quaternion
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import mocap

argv = sys.argv[sys.argv.index("--") + 1:]
NAME, OUT = argv[0], argv[1]
VIEWS = argv[2:] or ["front", "back", "side"]
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
BASE = os.path.join(ROOT, "art", "doll", NAME)
BVH = os.path.join(ROOT, "art", "mocap", "cmu_02_01_walk.bvh")
S = 404.0                      # pixels per Blender unit: the 768 px figure is 1.9 units tall
PPU = 160                      # render pixels per unit
FRAME_W, FRAME_H = 1.9, 2.1    # units
DIRKEY = {"front": "s", "side": "e", "back": "n"}
ZK = 0.0004                    # draw-order depth per order step (invisible edge-on, resolves overlaps)

def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def place(view, cx, h):
    """pixel (x, y down) -> world, and the unit vector toward the camera"""
    def f(x, y, vo=0):
        u, v = (x - cx) / S, (h - y) / S
        if view == "front": return Vector((u, -ZK * vo, v))
        if view == "back": return Vector((-u, ZK * vo, v))
        return Vector((-ZK * vo, -u, v))
    return f

def build_mesh(d, f, view):
    me = bpy.data.meshes.new("doll"); ob = bpy.data.objects.new("doll", me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new()
    vs = [bm.verts.new(f(x, y, vo)) for (x, y), vo in zip(d["verts"], d["vo"])]
    bm.verts.ensure_lookup_table(); bm.verts.index_update()
    uvl = bm.loops.layers.uv.new("uv")
    for t in d["tris"]:
        try: face = bm.faces.new([vs[i] for i in t])
        except ValueError: continue
        for loop in face.loops:
            x, y = d["verts"][loop.vert.index]; loop[uvl].uv = (x / d["w"], 1.0 - y / d["h"])
    bm.to_mesh(me); bm.free()
    img = bpy.data.images.load(os.path.join(BASE, view + ".png")); img.alpha_mode = "STRAIGHT"
    mat = bpy.data.materials.new("paper"); mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.interpolation = "Linear"
    em = nt.nodes.new("ShaderNodeEmission"); tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader"); outn = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(tex.outputs["Color"], em.inputs["Color"]); nt.links.new(tex.outputs["Alpha"], mix.inputs["Fac"])
    nt.links.new(tr.outputs["BSDF"], mix.inputs[1]); nt.links.new(em.outputs["Emission"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], outn.inputs["Surface"])
    me.materials.append(mat)
    return ob

def build_armature(J, f, swap):
    """Bones named by world side (L = -x): the back view is seen from behind, so image-left is world +x."""
    side = {"L": "R", "R": "L"} if swap else {"L": "L", "R": "R"}
    P = lambda k: f(*J[k])
    bones = [("root", None, P("hip"), P("hip") + Vector((0, 0, 0.06))),
             ("spine", "root", P("hip"), P("neck")),
             ("head", "spine", P("neck"), P("headTop"))]
    for img_s in "LR":
        w = side[img_s]
        if "hip" + img_s in J:
            bones += [(f"leg{w}1", "root", P("hip" + img_s), P("knee" + img_s)), (f"leg{w}2", f"leg{w}1", P("knee" + img_s), P("foot" + img_s))]
        if "sh" + img_s in J:
            if "el" + img_s in J:
                bones += [(f"arm{w}1", "spine", P("sh" + img_s), P("el" + img_s)), (f"arm{w}2", f"arm{w}1", P("el" + img_s), P("wr" + img_s)),
                          (f"hand{w}", f"arm{w}2", P("wr" + img_s), P("tip" + img_s))]
            else:
                bones += [(f"arm{w}1", "spine", P("sh" + img_s), P("tip" + img_s))]
    arm = bpy.data.armatures.new("rig"); ob = bpy.data.objects.new("rig", arm)
    bpy.context.scene.collection.objects.link(ob); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail in bones:
        b = arm.edit_bones.new(name); b.head = head; b.tail = tail
        if parent: b.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT"); bpy.context.view_layer.update()
    return ob

def weigh(mesh, arm):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    bad = sum(1 for v in mesh.data.vertices if not any(g.weight > 0.01 for g in v.groups))
    print(f"  weights: {len(mesh.vertex_groups)} groups, {bad}/{len(mesh.data.vertices)} vertices without weight")

def clean(mesh, d, swap):
    """Production weight clean-up: heat-diffusion weights leak across a flat drawing (the sleeve drags the belly). Keep each bone's
    weight only where the drawing's own part regions (doll_rig_prep: spec polygons, blurred) say that bone may move the pixel."""
    side = {"L": "R", "R": "L"} if swap else {"L": "L", "R": "R"}
    def groups(part):
        if part == "torso": return {"root", "spine"}
        if part == "head": return {"head", "spine"}
        k, w = part[:-1], side[part[-1]]
        return {"thigh": {f"leg{w}1", "root"}, "shin": {f"leg{w}2", f"leg{w}1"}, "arm": {f"arm{w}1", "spine"},
                "fore": {f"arm{w}2", f"arm{w}1"}, "hand": {f"hand{w}", f"arm{w}2"}}[k]
    parts = d["parts"]; vg = mesh.vertex_groups; names = {g.index: g.name for g in vg}
    removed = 0
    for v, m in zip(mesh.data.vertices, d["allow"]):
        ok = set()
        for k, pn in enumerate(parts):
            if m >> k & 1: ok |= groups(pn)
        if not ok: continue
        ws = [(g.group, g.weight) for g in v.groups]
        keep = [(gi, w) for gi, w in ws if names[gi] in ok]
        if not keep: continue
        tot = sum(w for _, w in keep)
        for gi, w in ws:
            if names[gi] in ok: vg[gi].add([v.index], w / tot, "REPLACE")
            else: vg[gi].remove([v.index]); removed += 1
    print("  clean-up: removed", removed, "stray weights")

def stage(view):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.render.film_transparent = True
    sc.render.resolution_x = int(FRAME_W * PPU); sc.render.resolution_y = int(FRAME_H * PPU); sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    cam = bpy.data.cameras.new("cam"); cam.type = "ORTHO"; cam.ortho_scale = FRAME_H
    co = bpy.data.objects.new("cam", cam); bpy.context.scene.collection.objects.link(co); sc.camera = co
    z = FRAME_H / 2 - 0.05
    if view == "front": co.location, co.rotation_euler = (0, -10, z), (math.radians(90), 0, 0)
    elif view == "back": co.location, co.rotation_euler = (0, 10, z), (math.radians(90), 0, math.radians(180))
    else: co.location, co.rotation_euler = (-10, 0, z), (math.radians(90), 0, math.radians(-90))

def apply(arm, pose):
    for b in arm.pose.bones:
        q, loc = pose[b.name]; b.rotation_mode = "QUATERNION"; b.rotation_quaternion = q; b.location = loc
    bpy.context.view_layer.update()

def neutral(poses):
    """the walk frame whose two legs are closest together (passing position): the standing pose"""
    best = min(range(len(poses)), key=lambda i: poses[i]["legL1"][0].rotation_difference(poses[i]["legR1"][0]).angle
               if "legL1" in poses[i] and "legR1" in poses[i] else 9)
    return poses[best]

def render(path):
    bpy.context.scene.render.filepath = path; bpy.ops.render.render(write_still=True)

def run(view):
    clear()
    d = json.load(open(os.path.join(BASE, view + "_rig.json")))
    cx = d["joints"]["hip"][0]; f = place(view, cx, d["h"])
    mesh = build_mesh(d, f, view)
    arm = build_armature(d["joints"], f, swap=(view == "back"))
    weigh(mesh, arm); clean(mesh, d, view == "back")
    stage(view)
    if os.environ.get("RIGDEBUG"):
        vs = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
        print("  faces", len(mesh.data.polygons), "img", mesh.data.materials[0].node_tree.nodes["Image Texture"].image.size[:], mesh.data.materials[0].node_tree.nodes["Image Texture"].image.has_data); print("  mesh bbox", min(v.x for v in vs), max(v.x for v in vs), min(v.z for v in vs), max(v.z for v in vs), "parent", mesh.parent, "cam", bpy.context.scene.camera.location)
        render(os.path.join(OUT, "rest.png"))
    poses = mocap.walk_poses(arm, BVH, 8, gain=0.85)
    n = neutral(poses)
    key = DIRKEY[view]; os.makedirs(OUT, exist_ok=True)
    for i, p in enumerate(poses):
        apply(arm, p); render(os.path.join(OUT, f"{NAME}_walk_{key}_{i:02d}.png"))
    for i in range(8):
        p = {k: (Quaternion(q), Vector(l)) for k, (q, l) in n.items()}
        s = math.sin(2 * math.pi * i / 8)
        p["root"] = (p["root"][0], p["root"][1] + Vector((0, 0.006 * s, 0)))
        apply(arm, p); render(os.path.join(OUT, f"{NAME}_idle_{key}_{i:02d}.png"))
    print("done", NAME, view)

for v in VIEWS:
    run(v)
