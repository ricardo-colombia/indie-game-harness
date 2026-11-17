"""Real human walk (CMU Mocap Database, art/mocap/cmu_02_01_walk.bvh, free for commercial use) retargeted onto the
cast's armature. FK is computed from the BVH directly (numpy); limbs are aimed along the captured bone directions, which
is the right retarget for stylised bodies whose proportions differ from the actor's. Returns N loop-closed poses."""
import math, numpy as np
import bpy
from mathutils import Vector, Matrix, Quaternion

def parse_bvh(path):
    toks = open(path).read().split()
    i = 0; joints = []; stack = []
    def nxt():
        nonlocal i; t = toks[i]; i += 1; return t
    while True:
        t = nxt()
        if t in ("ROOT", "JOINT"):
            name = nxt(); nxt()  # {
            assert nxt() == "OFFSET"; off = [float(nxt()) for _ in range(3)]
            assert nxt() == "CHANNELS"; n = int(nxt()); ch = [nxt() for _ in range(n)]
            joints.append({"name": name, "parent": stack[-1] if stack else -1, "off": np.array(off), "ch": ch})
            stack.append(len(joints) - 1)
        elif t == "End":
            nxt(); nxt(); nxt(); [nxt() for _ in range(3)]; nxt()  # Site { OFFSET x y z }
        elif t == "}":
            stack.pop()
            if not stack: break
    assert nxt() == "MOTION"; nxt(); frames = int(nxt()); nxt(); nxt(); ft = float(nxt())
    nch = sum(len(j["ch"]) for j in joints)
    data = np.array([float(nxt()) for _ in range(frames * nch)]).reshape(frames, nch)
    return joints, data, ft

def rot(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if axis == "X": return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == "Y": return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])

def fk(joints, data):
    F = len(data); J = len(joints)
    pos = np.zeros((F, J, 3)); col = 0
    for f in range(F):
        Rw = [None] * J; row = data[f]; col = 0
        for k, j in enumerate(joints):
            R = np.eye(3); p = np.zeros(3)
            for ch in j["ch"]:
                v = row[col]; col += 1
                if ch.endswith("position"): p["XYZ".index(ch[0])] = v
                else: R = R @ rot(ch[0], v)
            if j["parent"] < 0:
                Rw[k] = R; pos[f, k] = j["off"] + p
            else:
                q = j["parent"]; Rw[k] = Rw[q] @ R; pos[f, k] = pos[f, q] + Rw[q] @ j["off"]
    return pos

def walk_poses(arm, bvh_path, n=8, gain=1.0):
    joints, data, ft = parse_bvh(bvh_path)
    names = [j["name"] for j in joints]; ix = {nm: k for k, nm in enumerate(names)}
    P = fk(joints, data)
    P = np.stack([P[..., 0], -P[..., 2], P[..., 1]], axis=-1)       # BVH y-up -> Blender z-up
    P *= 1.0 / 20.0 * 0.0 + 1.0                                          # unit scale cancels: only directions are used
    hip = P[:, ix["Hips"]]
    travel = hip[-1] - hip[0]; travel[2] = 0
    yaw = math.atan2(travel[1], travel[0]); target = -math.pi / 2            # face -Y
    c, s = math.cos(target - yaw), math.sin(target - yaw)
    Rz = np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
    P = P @ Rz.T
    hip = P[:, ix["Hips"]]
    # which CMU leg is on the -x side? my "L" side bones live at -x
    left_is_minus_x = P[:, ix["LeftUpLeg"], 0].mean() < P[:, ix["RightUpLeg"], 0].mean()
    A, B = ("Left", "Right") if left_is_minus_x else ("Right", "Left")       # A -> my L bones, B -> my R bones
    # period: autocorrelation of the A foot's forward offset from the hips
    sig = (P[:, ix[A + "Foot"], 1] - hip[:, 1])
    sig = sig - sig.mean(); lo, hi = int(0.6 / ft), int(min(len(sig) - 20, 1.8 / ft))
    ac = [float(np.dot(sig[:-l], sig[l:])) / (len(sig) - l) for l in range(lo, hi)]
    T = lo + int(np.argmax(ac))
    start = 12                                                              # frame 0 is the BVH's T-pose
    idxs = [(start + int(round(k * T / n))) for k in range(n)]
    # bone -> (source joint at the bone head, source joint at its far end)
    links = {"spine": ("Spine", "Neck"), "head": ("Neck", "Head")}
    for side, S in (("L", A), ("R", B)):
        links[f"arm{side}1"] = (S + "Arm", S + "ForeArm"); links[f"arm{side}2"] = (S + "ForeArm", S + "Hand")
        links[f"leg{side}1"] = (S + "UpLeg", S + "Leg"); links[f"leg{side}2"] = (S + "Leg", S + "Foot")
    pbs = arm.pose.bones
    for b in pbs: b.rotation_mode = "QUATERNION"; b.rotation_quaternion = (1, 0, 0, 0); b.location = (0, 0, 0)
    bpy.context.view_layer.update()
    order = [b.name for b in pbs]                                           # parent-first (creation order)
    rest = {b.name: (b.bone.matrix_local.copy(), (b.bone.tail_local - b.bone.head_local).normalized()) for b in pbs}
    hip_h = float(hip[:, 2].mean()); out = []
    for f in idxs:
        for b in pbs: b.rotation_quaternion = (1, 0, 0, 0); b.location = (0, 0, 0)
        bpy.context.view_layer.update()
        # pelvis: yaw twist from the hip line, vertical bob from the hips height
        lat = P[f, ix[B + "UpLeg"]] - P[f, ix[A + "UpLeg"]]
        twist = math.atan2(lat[1], lat[0]) - 0.0
        bob = (hip[f, 2] - hip_h) * 0.0 + (hip[f, 2] - hip_h) / (hip_h) * 0.8 * 0.08 * 0.0
        for name in order:
            pb = pbs[name]; m, rdir = rest[name]
            if name == "root":
                R = Matrix.Rotation(twist * 0.6, 4, "Z") @ m
                R.translation = m.translation + Vector((0, 0, (hip[f, 2] - hip_h) / hip_h * 0.9 * 0.12))
                pb.matrix = R
            elif name in links:
                a, b = links[name]
                d = Vector(P[f, ix[b]] - P[f, ix[a]]).normalized()
                g = gain * {"spine": 0.5, "head": 0.25}.get(name, 1.0)           # torso and head only echo the actor's lean
                q = rdir.rotation_difference(rdir.lerp(d, g).normalized())
                M = q.to_matrix().to_4x4() @ m.to_3x3().to_4x4()
                M.translation = pb.matrix.translation if pb.parent else m.translation
                pb.matrix = M
            bpy.context.view_layer.update()
        out.append({b.name: (Quaternion(b.rotation_quaternion), Vector(b.location)) for b in pbs})
    return out
