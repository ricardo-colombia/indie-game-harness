"""The three pests: doctor (tax auditor), flaco (rebel), hernan (landowner). Blender -b -P cast.py -- <name> <outdir>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from person import *
from lib import *
argv = sys.argv[sys.argv.index("--") + 1:]
WHO, OUT = argv[0], argv[1]
MOCAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "mocap", "cmu_02_01_walk.bvh")
BASE = {"eyewhite": ((1, 1, 1),), "dark": ((0.08, 0.06, 0.06),), "sole": ((0.22, 0.16, 0.12),)}

def doctor():
    mats = dict(BASE, skin=((0.93, 0.74, 0.62),), shirt=((0.42, 0.42, 0.46),), sleeve=((0.42, 0.42, 0.46),), pants=((0.36, 0.36, 0.40),),
                shoe=((0.42, 0.24, 0.14),), hair=((0.10, 0.07, 0.07),), tie=((0.95, 0.45, 0.10),), collar=((0.96, 0.96, 0.94),),
                glass=((0.06, 0.05, 0.05),), nosec=((0.88, 0.48, 0.42),), folder=((0.85, 0.72, 0.40),))
    cfg = {"mats": mats, "mocap": MOCAP, "head_scale": (0.36, 0.31, 0.34), "head_z": 1.46, "torso_w": 0.25, "cuffs": False, "bare_shin": False, "nose": (0.07, 0.08, 0.07)}
    def ex(x):
        M, P = x["M"], x["P"]
        P(ellipsoid("collar", (0, -0.1, 1.19), (0.16, 0.10, 0.06), M["collar"]), "spine")
        P(ellipsoid("tie", (0, -0.185, 1.02), (0.045, 0.02, 0.17), M["tie"], outline=0.006), "spine")
        P(ellipsoid("lapelL", (-0.13, -0.17, 1.08), (0.07, 0.03, 0.17), M["shirt"], rot=(0, math.radians(-15), 0), outline=0.006), "spine")
        P(ellipsoid("lapelR", (0.13, -0.17, 1.08), (0.07, 0.03, 0.17), M["shirt"], rot=(0, math.radians(15), 0), outline=0.006), "spine")
        h = 1.46
        P(ellipsoid("nose2", (0, -0.335, h - 0.04), (0.075, 0.07, 0.075), M["nosec"], outline=0.006), "head")
        for s in (-1, 1):
            P(torus(f"glass{s}", (0.115 * s, -0.285, h + 0.05), 0.07, 0.014, M["glass"], rot=(math.radians(90), 0, 0), outline=0.004), "head")
        P(between("bridge", (-0.045, -0.29, h + 0.05), (0.045, -0.29, h + 0.05), 0.012, M["glass"], outline=0), "head")
        P(ellipsoid("hair", (0, 0.07, h + 0.09), (0.34, 0.30, 0.21), M["hair"]), "head")
        P(ellipsoid("tuft", (0.05, -0.12, h + 0.27), (0.11, 0.08, 0.07), M["hair"], rot=(0, math.radians(30), 0)), "head")
        P(ellipsoid("folder", (0.33, -0.05, 0.62), (0.03, 0.13, 0.17), M["folder"]), "armR2")
    build(WHO, cfg, ex, OUT)

def flaco():
    mats = dict(BASE, skin=((0.55, 0.36, 0.25),), shirt=((0.40, 0.42, 0.22),), sleeve=((0.40, 0.42, 0.22),), pants=((0.36, 0.38, 0.20),),
                shoe=((0.12, 0.09, 0.08),), hair=((0.07, 0.05, 0.05),), beret=((0.34, 0.38, 0.18),), scarf=((0.80, 0.10, 0.10),),
                belt=((0.30, 0.18, 0.10),), pouch=((0.45, 0.30, 0.16),))
    cfg = {"mats": mats, "mocap": MOCAP, "mocap_gain": 1.1, "torso_w": 0.23, "leg_w": 0.88, "head_scale": (0.30, 0.28, 0.30), "cuffs": False, "bare_shin": False, "stride": 0.7}
    def ex(x):
        M, P = x["M"], x["P"]
        P(torus("belt", (0, 0, 0.88), 0.22, 0.03, M["belt"], outline=0.006), "spine")
        P(ellipsoid("pouch", (0.27, -0.02, 0.80), (0.06, 0.1, 0.10), M["pouch"]), "spine")
        P(torus("scarf", (0, -0.02, 1.20), 0.20, 0.06, M["scarf"], outline=0.008), "spine")
        P(ellipsoid("scarf_tail", (0.08, -0.2, 1.10), (0.06, 0.03, 0.14), M["scarf"], rot=(0, math.radians(12), 0), outline=0.006), "spine")
        h = 1.44
        P(ellipsoid("beard", (0, -0.20, h - 0.17), (0.20, 0.13, 0.15), M["hair"]), "head")
        P(ellipsoid("mouth_hide", (0, -0.285, h - 0.15), (0.09, 0.03, 0.025), M["skin"], outline=0), "head")
        P(ellipsoid("hair", (0, 0.10, h + 0.08), (0.30, 0.27, 0.18), M["hair"]), "head")
        P(ellipsoid("beret", (0.02, 0.03, h + 0.20), (0.30, 0.29, 0.10), M["beret"], rot=(0, math.radians(-8), 0)), "head")
        P(ellipsoid("beret_top", (0.02, 0.03, h + 0.27), (0.06, 0.06, 0.04), M["beret"], outline=0.006), "head")
    build(WHO, cfg, ex, OUT)

def hernan():
    mats = dict(BASE, skin=((0.76, 0.52, 0.38),), shirt=((0.96, 0.94, 0.88),), sleeve=((0.50, 0.32, 0.18),), pants=((0.22, 0.28, 0.42),),
                shoe=((0.40, 0.24, 0.12),), hair=((0.10, 0.08, 0.07),), coat=((0.52, 0.34, 0.20),), felt=((0.62, 0.44, 0.24),),
                hband=((0.92, 0.80, 0.45),), shades=((0.04, 0.04, 0.05),), buckle=((0.85, 0.70, 0.25),))
    cfg = {"mats": mats, "mocap": MOCAP, "mocap_gain": 0.95, "torso_w": 0.31, "leg_w": 1.1, "leg_x": 0.14, "arm_x": 0.34, "head_scale": (0.33, 0.30, 0.31),
           "cuffs": False, "bare_shin": False, "stride": 0.6}
    def ex(x):
        M, P = x["M"], x["P"]
        P(torus("belt", (0, 0, 0.88), 0.27, 0.03, M["shoe"], outline=0.006), "spine")
        P(ellipsoid("buckle", (0, -0.27, 0.88), (0.05, 0.02, 0.04), M["buckle"], outline=0.005), "spine")
        # long coat: flared cone that hangs below the belt
        bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=0.46, radius2=0.28, depth=0.58, location=(0, 0.02, 0.86))
        co = bpy.context.active_object; co.name = "coat"; co.scale = (1.0, 0.8, 1.0); bpy.ops.object.transform_apply(scale=True)
        finish(co, M["coat"], 0.012); P(co, "spine")
        P(ellipsoid("shirtfront", (0, -0.17, 1.05), (0.09, 0.05, 0.22), M["shirt"], outline=0.006), "spine")
        h = 1.44
        P(ellipsoid("shades", (0, -0.268, h + 0.05), (0.19, 0.03, 0.06), M["shades"], outline=0.004), "head")
        P(ellipsoid("mustache", (0, -0.285, h - 0.075), (0.15, 0.04, 0.04), M["hair"], outline=0.005), "head")
        P(ellipsoid("hair", (0, 0.07, h + 0.09), (0.31, 0.28, 0.2), M["hair"]), "head")
        tilt = (math.radians(-8), 0, 0)
        for o in (cylinder("brim", (0, 0.02, 1.66), 0.46, 0.03, M["felt"], seg=44, rot=tilt),
                  ellipsoid("brim_edge", (0, 0.02, 1.66), (0.46, 0.46, 0.028), M["felt"], seg=44, rot=tilt, outline=0.01),
                  ellipsoid("crown", (0, 0.05, 1.77), (0.24, 0.24, 0.16), M["felt"], rot=tilt),
                  torus("hatband", (0, 0.03, 1.70), 0.243, 0.022, M["hband"], rot=tilt, outline=0.006)):
            P(o, "head")
    build(WHO, cfg, ex, OUT)

{"doctor": doctor, "flaco": flaco, "hernan": hernan}[WHO]()
