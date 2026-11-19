"""Prepare one doll for the Blender rig (system python, needs PIL/scipy):  python art/blender/doll_rig_prep.py <name> [view...]
Writes art/doll/<name>/<view>_rig.json for doll_rig.py: grid mesh (pixels, y down), per-vertex draw order, and the joints
(hip, neck, shoulders, elbows, wrists, fingertips, hips, knees) read from the part pivots that doll_cut.py already computed."""
import sys, os, json
import numpy as np
from PIL import Image
from scipy import ndimage
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "comfy"))
import doll_skin as ds

ROOT = ds.ROOT

def far_point(base, view, part, origin, fallback):
    """Fingertip: the opaque pixel of a part image farthest from its joint (parts are trimmed; bbox gives the offset)."""
    meta = json.load(open(os.path.join(base, view + "_parts.json")))
    p = next((q for q in meta["parts"] if q["name"] == part), None)
    if p is None: return fallback
    a = np.array(Image.open(os.path.join(base, "parts", p["file"])).convert("RGBA").getchannel("A"))
    ys, xs = np.nonzero(a > 100)
    xs = xs + p["bbox"][0]; ys = ys + p["bbox"][1]
    d = np.hypot(xs - origin[0], ys - origin[1]); k = int(np.argsort(d)[int(len(d) * 0.98)])
    return [float(xs[k]), float(ys[k])]

def prep(name, view):
    base = os.path.join(ROOT, "art", "doll", name)
    os.environ["VIEW"] = view
    fig = Image.open(os.path.join(base, view + ".png")).convert("RGBA")
    spec = json.load(open(os.path.join(base, view + "_spec.json")))
    meta = json.load(open(os.path.join(base, view + "_parts.json")))
    bones = [q["name"] for q in meta["parts"]]
    order = {q["name"]: q["order"] for q in meta["parts"]}
    piv = {q["name"]: q["pivot"] for q in meta["parts"]}
    M = ds.masks(fig, spec, meta)
    B = ds.blur({k: M[k] for k in bones if k in M}, {"default": 11.0, "torso": 12.0})
    bones = [b for b in bones if b in B]
    P, T = ds.build_mesh(fig, 8)
    H = fig.height
    stack = np.stack([B[b] for b in bones], 0); lonely = stack.sum(0) < 0.15
    iy, ix = ndimage.distance_transform_edt(lonely, return_distances=False, return_indices=True)
    xi = np.clip(P[:, 0].round().astype(int), 0, fig.width - 1); yi = np.clip(P[:, 1].round().astype(int), 0, H - 1)
    yi, xi = iy[yi, xi], ix[yi, xi]
    dom = stack[:, yi, xi].argmax(0)
    vo = [int(order[bones[k]]) for k in dom]
    allow = (stack[:, yi, xi] > 0.05)                                  # which parts' (blurred) regions cover each vertex
    mask = [int(sum(1 << k for k in range(len(bones)) if allow[k, i])) for i in range(len(P))]
    J = {"hip": piv["torso"], "neck": piv["head"], "headTop": [piv["head"][0], 0.0]}
    for s in "LR":
        if "thigh" + s in piv:
            J["hip" + s] = piv["thigh" + s]; J["knee" + s] = piv["shin" + s]
            J["foot" + s] = [piv["shin" + s][0], 760.0]
        if "arm" + s in piv:
            J["sh" + s] = piv["arm" + s]
            if "fore" + s in piv:
                J["el" + s] = piv["fore" + s]; J["wr" + s] = piv["hand" + s]
                J["tip" + s] = far_point(base, view, "hand" + s, piv["hand" + s], piv["hand" + s])
            else:
                J["tip" + s] = far_point(base, view, "arm" + s, piv["arm" + s], piv["arm" + s])
    # headTop: highest opaque pixel above the neck (hats make it taller than 0)
    a = np.array(fig.getchannel("A")) > 60
    ys = np.nonzero(a.any(1))[0]; J["headTop"] = [float(piv["head"][0]), float(ys.min())]
    out = {"w": fig.width, "h": H, "verts": [[round(float(x), 1), round(float(y), 1)] for x, y in P], "tris": T.tolist(), "vo": vo, "parts": bones, "allow": mask, "joints": J}
    json.dump(out, open(os.path.join(base, view + "_rig.json"), "w"))
    print(name, view, len(P), "verts", sorted(J))

if __name__ == "__main__":
    for v in (sys.argv[2:] or ["front", "back", "side"]): prep(sys.argv[1], v)
