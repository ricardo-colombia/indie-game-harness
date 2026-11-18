"""Shared humanoid base for the COFFEE-MAN cast: armature, chunky body, head with face, walk/idle cycles, 3-direction render.
A character script sets materials + dimensions in a cfg dict and adds its own clothes/props through `extras(ctx)`."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

def build(name, cfg, extras, out, test=False):
    sc = reset()
    M = {k: toon_material(k, *v) for k, v in cfg["mats"].items()}
    c = cfg
    H = c.get("hip", 0.80)
    sh = c.get("shoulder", 1.17)
    head_z = c.get("head_z", 1.44)
    bones = [
        ("root",  None,    (0, 0, H),        (0, 0, H + 0.12)),
        ("spine", "root",  (0, 0, H + 0.12), (0, 0, sh + 0.05)),
        ("head",  "spine", (0, 0, sh + 0.05), (0, 0, 1.80)),
    ]
    aw = c.get("arm_x", 0.30)
    for s, side in ((-1, "L"), (1, "R")):
        bones += [(f"arm{side}1", "spine", (aw * s, 0, sh), ((aw + 0.05) * s, 0, sh - 0.24)),
                  (f"arm{side}2", f"arm{side}1", ((aw + 0.05) * s, 0, sh - 0.24), ((aw + 0.04) * s, 0, sh - 0.45))]
    lx = c.get("leg_x", 0.13)
    for s, side in ((-1, "L"), (1, "R")):
        bones += [(f"leg{side}1", "root", (lx * s, 0, H), (lx * s, 0, 0.44)),
                  (f"leg{side}2", f"leg{side}1", (lx * s, 0, 0.44), (lx * s, 0, 0.10)),
                  (f"foot{side}", f"leg{side}2", (lx * s, 0, 0.10), (lx * s, -0.22, 0.10))]
    arm = make_armature(bones)
    P = lambda o, b: attach(o, arm, b)
    ctx = dict(M=M, P=P, arm=arm, cfg=c, H=H, sh=sh, head_z=head_z)

    # legs
    for s, side in ((-1, "L"), (1, "R")):
        x = lx * s
        t = c.get("leg_w", 1.0)
        P(ellipsoid(f"thigh{side}", (x, 0, 0.62), (0.135 * t, 0.135 * t, 0.23), M["pants"]), f"leg{side}1")
        P(ellipsoid(f"calf{side}", (x, 0, 0.28), (0.112 * t, 0.112 * t, 0.21), M["pants"]), f"leg{side}2")
        if c.get("cuffs", True): P(torus(f"cuff{side}", (x, 0, 0.22), 0.115 * t, 0.04, M["pants"]), f"leg{side}2")
        if c.get("bare_shin", True): P(ellipsoid(f"shin{side}", (x, 0, 0.145), (0.07, 0.07, 0.07), M["skin"]), f"leg{side}2")
        P(ellipsoid(f"shoe{side}", (x, -0.07, 0.065), (0.105 * t, 0.19, 0.075), M["shoe"]), f"foot{side}")
        P(ellipsoid(f"sole{side}", (x, -0.07, 0.022), (0.108 * t, 0.195, 0.03), M["sole"], outline=0.006), f"foot{side}")
    # torso + hips + arms
    tw = c.get("torso_w", 0.27)
    P(ellipsoid("torso", (0, 0, 1.00), (tw, 0.19, 0.30), M["shirt"]), "spine")
    P(ellipsoid("hips", (0, 0, 0.82), (tw - 0.02, 0.18, 0.15), M["pants"]), "root")
    for s, side in ((-1, "L"), (1, "R")):
        ax = (aw + 0.025) * s
        P(ellipsoid(f"upper{side}", (ax, 0, sh - 0.12), (0.075, 0.075, 0.17), M.get("sleeve", M["shirt"])), f"arm{side}1")
        P(ellipsoid(f"fore{side}", (ax + 0.02 * s, 0, sh - 0.34), (0.065, 0.065, 0.16), M.get("sleeve", M["shirt"])), f"arm{side}2")
        P(ellipsoid(f"hand{side}", (ax + 0.015 * s, 0, sh - 0.495), (0.065, 0.065, 0.07), M["skin"]), f"arm{side}2")
    # head and face
    hs = c.get("head_scale", (0.33, 0.30, 0.31))
    P(ellipsoid("head", (0, 0, head_z), hs, M["skin"]), "head")
    fy = -hs[1] + 0.005
    nose = c.get("nose", (0.05, 0.055, 0.055))
    P(ellipsoid("nose", (0, fy - 0.0, head_z - 0.03), nose, M["skin"], outline=0.006), "head")
    for s in (-1, 1):
        P(ellipsoid(f"eye{s}", (0.11 * s, fy + 0.04, head_z + 0.05), (0.05, 0.03, 0.055), M["eyewhite"], outline=0.005), "head")
        P(ellipsoid(f"pupil{s}", (0.11 * s, fy + 0.016, head_z + 0.05), (0.024, 0.014, 0.034), M["dark"], outline=0), "head")
        P(ellipsoid(f"brow{s}", (0.115 * s, fy + 0.04, head_z + 0.125), (0.07, 0.022, 0.018), M.get("hair", M["dark"]), rot=(0, math.radians(14 * s), 0), outline=0), "head")
        P(ellipsoid(f"ear{s}", (hs[0] * s, 0.0, head_z), (0.04, 0.055, 0.07), M["skin"], outline=0.006), "head")
    extras(ctx)

    # ---- animation
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    pb = arm.pose.bones
    for b in pb: b.rotation_mode = "XYZ"
    def key(name_, rot=None, loc=None):
        b = pb[name_]
        if rot is not None: b.rotation_euler = rot; b.keyframe_insert("rotation_euler", frame=1)
        if loc is not None: b.location = loc; b.keyframe_insert("location", frame=1)
    WALK_N, IDLE_N = 8, 8
    A, K, bob = c.get("stride", 0.65), c.get("knee", 0.95), c.get("bob", 0.035)
    mocap_poses = None
    if c.get("mocap"):
        import mocap as mc
        mocap_poses = mc.walk_poses(arm, c["mocap"], WALK_N, c.get("mocap_gain", 1.0))
    def walk(i):
        if mocap_poses:
            for nm, (q, l) in mocap_poses[i].items():
                pb[nm].rotation_mode = "QUATERNION"; pb[nm].rotation_quaternion = q; pb[nm].location = l
            return
        ph = i / WALK_N * 2 * math.pi
        for side, off, arms in (("L", 0, 1), ("R", math.pi, -1)):
            p = ph + off
            key(f"leg{side}1", (-A * math.sin(p), 0, 0))
            key(f"leg{side}2", (K * max(0, math.cos(p)), 0, 0))
            key(f"arm{side}1", (arms * A * 0.8 * math.sin(ph), 0, 0))
            key(f"arm{side}2", (-0.45 - 0.2 * math.sin(ph) * arms, 0, 0))
        key("root", (0, 0, 0.05 * math.sin(ph)), (0, 0, bob * math.cos(2 * ph)))
        key("spine", (-0.07, 0, -0.07 * math.sin(ph)))
        key("head", (0.04 * math.cos(2 * ph) + 0.03, 0, 0.05 * math.sin(ph)))
    def idle(i):
        for b in pb: b.rotation_mode = "XYZ"; b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
        ph = i / IDLE_N * 2 * math.pi
        for n in ("legL1", "legL2", "legR1", "legR2", "footL", "footR"): key(n, (0, 0, 0))
        key("armL1", (0, 0, 0.05 + 0.03 * math.sin(ph))); key("armR1", (0, 0, -0.05 - 0.03 * math.sin(ph)))
        key("armL2", (-0.25, 0, 0)); key("armR2", (-0.25, 0, 0))
        key("root", (0, 0, 0), (0, 0, -0.012 * (1 - math.cos(ph))))
        key("spine", (0.02 * math.sin(ph), 0, 0))
        key("head", (-0.02 * math.sin(ph), 0, 0.04 * math.sin(ph)))
    setup_stage(elevation_deg=40, ortho=c.get("ortho", 2.3), target_z=0.92, size=192)
    os.makedirs(out, exist_ok=True)
    DIRS = [("s", 0), ("e", 90), ("n", 180)]
    for anim, fn, n in (("walk", walk, WALK_N), ("idle", idle, IDLE_N)):
        if test and anim == "idle": continue
        for i in range(n):
            fn(i)
            for d, deg in DIRS:
                if test and d == "n": continue
                arm.rotation_euler = (0, 0, math.radians(deg))
                render_to(os.path.join(out, f"{name}_{anim}_{d}_{i:02d}.png"))
    print("rendered", out)
