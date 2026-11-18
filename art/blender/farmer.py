"""Don Eliseo, the Eje Cafetero campesino. Blender -b -P farmer.py -- <outdir> [test]"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from person import *
from lib import *
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "/tmp/farmer_out"

def tex_straw(nt, color):
    w = nt.nodes.new("ShaderNodeTexWave"); w.wave_type = "BANDS"; w.bands_direction = "DIAGONAL"
    w.inputs["Scale"].default_value = 38; w.inputs["Distortion"].default_value = 2.5; w.inputs["Detail"].default_value = 3
    c = nt.nodes.new("ShaderNodeTexCoord"); nt.links.new(c.outputs["Object"], w.inputs["Vector"])
    r = nt.nodes.new("ShaderNodeValToRGB")
    r.color_ramp.elements[0].color = (color[0] * 0.78, color[1] * 0.74, color[2] * 0.62, 1)
    r.color_ramp.elements[1].color = (*color, 1)
    nt.links.new(w.outputs["Fac"], r.inputs[0]); return r.outputs[0]

def tex_ruana(nt, color):
    w = nt.nodes.new("ShaderNodeTexWave"); w.wave_type = "BANDS"; w.bands_direction = "Z"
    w.inputs["Scale"].default_value = 4.2; w.inputs["Distortion"].default_value = 0.6
    c = nt.nodes.new("ShaderNodeTexCoord"); nt.links.new(c.outputs["Object"], w.inputs["Vector"])
    r = nt.nodes.new("ShaderNodeValToRGB"); r.color_ramp.interpolation = "CONSTANT"
    r.color_ramp.elements[0].color = (*color, 1)
    e = r.color_ramp.elements.new(0.62); e.color = (0.10, 0.07, 0.07, 1)
    nt.links.new(w.outputs["Fac"], r.inputs[0]); return r.outputs[0]

cfg = {"mats": {
    "skin": ((0.74, 0.46, 0.30),), "shirt": ((0.97, 0.95, 0.88),), "pants": ((0.55, 0.40, 0.25),),
    "straw": ((0.94, 0.86, 0.62), tex_straw), "band": ((0.05, 0.04, 0.04),),
    "ruana": ((0.16, 0.12, 0.10), tex_ruana), "ruana_edge": ((0.62, 0.12, 0.09),),
    "leather": ((0.40, 0.20, 0.09),), "shoe": ((0.09, 0.13, 0.09),), "sole": ((0.04, 0.05, 0.04),),
    "basket": ((0.72, 0.47, 0.20),), "cherry": ((0.80, 0.07, 0.08),),
    "dark": ((0.08, 0.06, 0.05),), "eyewhite": ((1, 1, 1),), "hair": ((0.16, 0.14, 0.14),)},
       "head_scale": (0.30, 0.28, 0.29), "torso_w": 0.25, "bare_shin": False,
       "mocap": os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "mocap", "cmu_02_01_walk.bvh")}

def extras(x):
    M, P, h = x["M"], x["P"], x["head_z"]
    P(torus("belt", (0, 0, 0.88), 0.23, 0.03, M["leather"], outline=0.006), "spine")
    # rubber boots (botas de caucho) over the trouser legs
    for s, side in ((-1, "L"), (1, "R")):
        lx = x["cfg"].get("leg_x", 0.13) * s
        P(ellipsoid(f"boot{side}", (lx, 0, 0.17), (0.118, 0.118, 0.17), M["shoe"], outline=0.007), f"leg{side}2")
        P(torus(f"bootrim{side}", (lx, 0, 0.32), 0.118, 0.03, M["pants"]), f"leg{side}2")
    # ruana: dark wool draped over the shoulders and open at the front, red hem; the white shirt shows through the opening
    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=0.38, radius2=0.19, depth=0.30, location=(0, 0.02, 1.07))
    ru = bpy.context.active_object; ru.name = "ruana"; ru.scale = (1.0, 0.85, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    finish(ru, M["ruana"], 0.012); P(ru, "spine")
    P(torus("ruana_hem", (0, 0.02, 0.925), 0.37, 0.022, M["ruana_edge"], outline=0.005), "spine")
    P(ellipsoid("ruana_collar", (0, 0.0, 1.21), (0.23, 0.19, 0.07), M["ruana"]), "spine")
    P(ellipsoid("shirt_front", (0, -0.17, 1.02), (0.10, 0.05, 0.19), M["shirt"], outline=0.005), "spine")
    # carriel (the paisa leather satchel) on the hip + strap
    P(ellipsoid("carriel", (0.30, -0.02, 0.78), (0.08, 0.16, 0.14), M["leather"]), "spine")
    P(ellipsoid("carriel_flap", (0.34, -0.02, 0.85), (0.045, 0.15, 0.08), M["leather"], outline=0.006), "spine")
    P(between("strap", (-0.19, -0.05, 1.18), (0.28, -0.06, 0.86), 0.022, M["leather"]), "spine")
    # coffee picking basket (canasto) tied at the waist, full of cherries
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.15, radius2=0.20, depth=0.20, location=(0, -0.29, 0.76))
    bk = bpy.context.active_object; bk.name = "basket"; finish(bk, M["basket"], 0.008); P(bk, "root")
    for i, (cx, cz) in enumerate(((-0.08, 0.89), (0.0, 0.92), (0.08, 0.89), (-0.04, 0.88), (0.05, 0.93))):
        P(ellipsoid(f"cherry{i}", (cx, -0.29, cz), (0.052, 0.052, 0.052), M["cherry"], outline=0.004), "root")
    P(ellipsoid("mustache", (0, -0.265, 1.34), (0.15, 0.04, 0.04), M["dark"], outline=0.005), "head")
    P(ellipsoid("hair", (0, 0.07, 1.49), (0.29, 0.26, 0.21), M["hair"]), "head")
    # sombrero aguadeno: tall pinched crown, wide flat brim, black band
    HZ = 1.65; tilt = (math.radians(-10), 0, 0)
    for o in (cylinder("brim", (0, 0.04, HZ), 0.44, 0.03, M["straw"], seg=44, rot=tilt),
              ellipsoid("brim_edge", (0, 0.04, HZ), (0.44, 0.44, 0.028), M["straw"], seg=44, rot=tilt, outline=0.01),
              ellipsoid("crown", (0, 0.06, HZ + 0.13), (0.215, 0.215, 0.21), M["straw"], rot=tilt),
              torus("hatband", (0, 0.045, HZ + 0.03), 0.222, 0.03, M["band"], rot=tilt, outline=0.006)):
        P(o, "head")

build("farmer", cfg, extras, OUT, test="test" in argv)
