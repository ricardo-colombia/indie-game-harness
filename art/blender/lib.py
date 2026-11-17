"""Shared helpers for the COFFEE-MAN character pipeline (Blender, headless):
   concept -> stylised 3D model -> armature -> animation -> toon render of 8 directions -> sprite sheet.
   Same method top games use for readable, consistent characters in a 3/4 top-down view (e.g. Dead Cells, Hades render 3D to 2D frames)."""
import bpy, math, os
from mathutils import Vector, Matrix

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    for name in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = name
            break
        except TypeError:
            pass
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    try:
        sc.view_settings.view_transform = "Standard"
    except Exception:
        pass
    return sc

# ------------------------------------------------------------------ materials (toon, painted)
def _ramp(nt, x, y):
    r = nt.nodes.new("ShaderNodeValToRGB")
    r.location = (x, y)
    r.color_ramp.interpolation = "CONSTANT"
    r.color_ramp.elements[0].position = 0.0
    r.color_ramp.elements[0].color = (0.50, 0.46, 0.62, 1)   # shadow: cool, warm-purple like gouache
    e = r.color_ramp.elements.new(0.38)
    e.color = (0.80, 0.78, 0.84, 1)
    e = r.color_ramp.elements.new(0.62)
    e.color = (1.0, 0.97, 0.9, 1)
    return r

def toon_material(name, color, texture=None, rough_bump=0.0):
    """Painted-looking toon material: 3-step light ramp x base colour (optionally a procedural texture node factory)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (900, 0)
    em = nt.nodes.new("ShaderNodeEmission"); em.location = (700, 0)
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse"); diff.location = (-200, -150)
    s2r = nt.nodes.new("ShaderNodeShaderToRGB"); s2r.location = (0, -150)
    ramp = _ramp(nt, 200, -150)
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"; mix.location = (500, 0)
    mix.inputs[0].default_value = 1.0
    if texture:
        col_out = texture(nt, color)
    else:
        n = nt.nodes.new("ShaderNodeRGB"); n.location = (200, 150)
        n.outputs[0].default_value = (*color, 1)
        col_out = n.outputs[0]
    nt.links.new(diff.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs[0], ramp.inputs[0])
    nt.links.new(col_out, mix.inputs[6])
    nt.links.new(ramp.outputs[0], mix.inputs[7])
    nt.links.new(mix.outputs[2], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    return m

def outline_material(color=(0.10, 0.06, 0.04)):
    m = bpy.data.materials.new("outline")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs[0].default_value = (*color, 1)
    nt.links.new(em.outputs[0], out.inputs[0])
    m.use_backface_culling = True
    return m

OUTLINE = None

def finish(obj, mat, outline=0.012):
    global OUTLINE
    if OUTLINE is None or OUTLINE.name not in bpy.data.materials:
        OUTLINE = outline_material()
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    obj.data.materials.append(OUTLINE)
    for p in obj.data.polygons:
        p.use_smooth = True
    if outline > 0:
        mod = obj.modifiers.new("outline", "SOLIDIFY")
        mod.thickness = outline
        mod.offset = 1.0
        mod.use_flip_normals = True
        mod.material_offset = 1

# ------------------------------------------------------------------ primitives
def ellipsoid(name, loc, scale, mat, rot=(0, 0, 0), seg=28, outline=0.012):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1.0, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(scale=True, rotation=False)
    finish(o, mat, outline)
    return o

def cylinder(name, loc, radius, depth, mat, rot=(0, 0, 0), seg=28, outline=0.01):
    bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    finish(o, mat, outline)
    return o

def torus(name, loc, major, minor, mat, rot=(0, 0, 0), outline=0.008):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=36, minor_segments=14, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    finish(o, mat, outline)
    return o

def between(name, a, b, radius, mat, outline=0.008):
    """Cylinder from point a to point b (straps, rope)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=d.length, location=(a + b) / 2)
    o = bpy.context.active_object
    o.name = name
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = d.to_track_quat("Z", "Y")
    finish(o, mat, outline)
    return o

# ------------------------------------------------------------------ rig
def make_armature(bones):
    """bones: list of (name, parent, head, tail). Returns the armature object (rest pose, z up)."""
    arm = bpy.data.armatures.new("rig")
    obj = bpy.data.objects.new("rig", arm)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail in bones:
        b = arm.edit_bones.new(name)
        b.head = head
        b.tail = tail
        if parent:
            b.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    return obj

def attach(obj, arm, bone):
    """Parent a part rigidly to a bone, keeping its rest-pose world position."""
    bpy.context.view_layer.update()
    pb = arm.pose.bones[bone]
    M = arm.matrix_world @ pb.matrix @ Matrix.Translation((0, pb.bone.length, 0))
    obj.parent = arm
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    obj.matrix_parent_inverse = M.inverted()

# ------------------------------------------------------------------ scene, camera, render
def setup_stage(elevation_deg=52, ortho=2.5, target_z=0.85, size=256):
    sc = bpy.context.scene
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    e = math.radians(elevation_deg)
    cam.location = (0, -8 * math.cos(e), target_z + 8 * math.sin(e))
    cam.rotation_euler = (math.radians(90 - elevation_deg), 0, 0)
    sc.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    bpy.context.scene.collection.objects.link(sun)
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    sc.render.resolution_x = size
    sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    try:
        sc.eevee.taa_render_samples = 16
    except Exception:
        pass
    return cam

def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
