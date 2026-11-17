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
    "skin": ((0.80, 0.52, 0.34),), "shirt": ((0.96, 0.94, 0.86),), "pants": ((0.78, 0.66, 0.46),),
    "straw": ((0.93, 0.82, 0.55), tex_straw), "band": ((0.08, 0.06, 0.06),), "ruana": ((0.62, 0.13, 0.10), tex_ruana),
    "leather": ((0.36, 0.18, 0.09),), "shoe": ((0.88, 0.80, 0.62),), "sole": ((0.30, 0.20, 0.12),),
    "dark": ((0.10, 0.07, 0.06),), "eyewhite": ((1, 1, 1),), "hair": ((0.20, 0.17, 0.16),)},
       "mocap": os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "mocap", "cmu_02_01_walk.bvh")}

def extras(x):
    M, P, h = x["M"], x["P"], x["head_z"]
    P(torus("belt", (0, 0, 0.88), 0.24, 0.03, M["leather"], outline=0.006), "spine")
    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=0.40, radius2=0.20, depth=0.40, location=(0, 0.015, 1.06))
    ru = bpy.context.active_object; ru.name = "ruana"; ru.scale = (1.0, 0.82, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    finish(ru, M["ruana"], 0.012); P(ru, "spine")
    P(ellipsoid("ruana_collar", (0, 0.0, 1.20), (0.24, 0.20, 0.07), M["ruana"]), "spine")
    P(ellipsoid("carriel", (0.30, -0.02, 0.80), (0.075, 0.15, 0.13), M["leather"]), "spine")
    P(ellipsoid("carriel_flap", (0.34, -0.02, 0.86), (0.04, 0.14, 0.07), M["leather"], outline=0.006), "spine")
    P(between("strap", (-0.20, -0.06, 1.18), (0.28, -0.06, 0.86), 0.022, M["leather"]), "spine")
    P(ellipsoid("mustache", (0, -0.28, 1.35), (0.14, 0.04, 0.036), M["dark"], outline=0.005), "head")
    P(ellipsoid("hair", (0, 0.07, 1.50), (0.30, 0.27, 0.22), M["hair"]), "head")
    HZ = 1.66; tilt = (math.radians(-12), 0, 0)
    for o in (cylinder("brim", (0, 0.04, HZ), 0.42, 0.03, M["straw"], seg=44, rot=tilt),
              ellipsoid("brim_edge", (0, 0.04, HZ), (0.42, 0.42, 0.028), M["straw"], seg=44, rot=tilt, outline=0.01),
              ellipsoid("crown", (0, 0.07, HZ + 0.10), (0.235, 0.235, 0.17), M["straw"], rot=tilt),
              torus("hatband", (0, 0.047, HZ + 0.025), 0.238, 0.024, M["band"], rot=tilt, outline=0.006)):
        P(o, "head")

build("farmer", cfg, extras, OUT, test="test" in argv)
