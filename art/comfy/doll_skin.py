"""Skinned doll: one mesh per view, weighted to the bones the rig already has (Unity 2D Animation SpriteSkin deforms it).
  python art/comfy/doll_skin.py <name> [view...]
Reads art/doll/<name>/<view>.png + <view>_spec.json + <view>_parts.json (bones, pivots, parents, rests from doll_cut.py), writes
  game/Assets/Resources/Themes/Vintage/doll_<name>_<view>_fig.bytes  (the figure PNG)
and adds a "skin" block to each view in doll_<name>.json: bones[], vx/vy (pixels, y up), tris, bi/bw (4 per vertex).
Weights: every bone owns the pixels of its spec polygon (arms split in arm/forearm/hand by doll_cut.split_arm); masks are blurred so joints blend,
and an arm's weight ramps up with distance from the shoulder so the shoulder cap stays with the body."""
import sys, os, json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import Delaunay
from skimage import measure
sys.path.insert(0, os.path.dirname(__file__))
import doll_cut as dc

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "Resources", "Themes", "Vintage")

def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def masks(fig, spec, parts_meta):
    """Per bone: float mask 0..1 over the figure canvas."""
    A = np.array(fig.getchannel("A")).astype(float) / 255.0
    Y, X = np.mgrid[0:A.shape[0], 0:A.shape[1]]
    poly = {}
    for p in spec["parts"]:
        m = Image.new("L", fig.size, 0); ImageDraw.Draw(m).polygon([tuple(q) for q in p["poly"]], fill=255)
        poly[p["name"]] = np.array(m).astype(float) / 255.0
    pivot = {q["name"]: q["pivot"] for q in parts_meta["parts"]}
    out = {}; lost = np.zeros(A.shape)
    tx = next(q["pivot"][0] for q in spec["parts"] if q["name"] == "torso")
    for p in spec["parts"]:
        n = p["name"]; m = poly[n] * (A > 0.15)
        if n in ("armL", "armR"):
            sp = dc.split_arm(p, m * 255.0, tx, os.environ.get("VIEW") == "side")
            S = np.array(p["pivot"], float)
            ramp = smoothstep(18, 110, np.hypot(X - S[0], Y - S[1]))      # the shoulder cap belongs to the torso
            lost += m * (1 - ramp)
            if sp is None:
                out[n] = m * ramp
            else:
                out[n] = sp[0] / 255.0 * ramp
                out["fore" + n[-1]] = sp[1] / 255.0 * ramp
                out["hand" + n[-1]] = sp[2] / 255.0 * ramp
        else:
            out[n] = m
    out["torso"] = np.clip(out["torso"] + lost, 0, 1)
    return out

def blur(masks_, sig):
    return {k: ndimage.gaussian_filter(v, sig.get(k, sig["default"])) for k, v in masks_.items()}

def build_mesh(fig, step=9):
    """Regular grid over the figure: every cell that touches the (dilated) figure becomes two triangles. A grid has no slivers and no
    torn edges, so the skin bends smoothly; the silhouette comes from the texture's own alpha."""
    a = np.array(fig.getchannel("A")) > 8
    a = ndimage.binary_dilation(a, iterations=6)
    H, W = a.shape
    xs = list(range(0, W, step)) + [W - 1] if (W - 1) % step else list(range(0, W, step))
    ys = list(range(0, H, step)) + [H - 1] if (H - 1) % step else list(range(0, H, step))
    gx, gy = len(xs), len(ys); used = {}; P = []; T = []
    def vid(i, j):
        if (i, j) not in used: used[(i, j)] = len(P); P.append((xs[i], ys[j]))
        return used[(i, j)]
    for j in range(gy - 1):
        for i in range(gx - 1):
            if a[ys[j]:ys[j + 1] + 1, xs[i]:xs[i + 1] + 1].any():
                v00, v10, v01, v11 = vid(i, j), vid(i + 1, j), vid(i, j + 1), vid(i + 1, j + 1)
                if (i + j) % 2 == 0: T += [[v00, v10, v11], [v00, v11, v01]]
                else: T += [[v00, v10, v01], [v10, v11, v01]]
    return np.array(P, float), np.array(T)

def skin(name, view):
    base = os.path.join(ROOT, "art", "doll", name)
    os.environ["VIEW"] = view
    fig = Image.open(os.path.join(base, view + ".png")).convert("RGBA")
    spec = json.load(open(os.path.join(base, view + "_spec.json")))
    meta = json.load(open(os.path.join(base, view + "_parts.json")))
    bones = [q["name"] for q in meta["parts"]]
    order = {q["name"]: q["order"] for q in meta["parts"]}
    M = masks(fig, spec, meta)
    # the torso keeps whatever the arms/legs do not own
    sig = {"default": 11.0, "thighL": 18.0, "thighR": 18.0, "shinL": 16.0, "shinR": 16.0, "torso": 12.0, "foreL": 15.0, "foreR": 15.0, "handL": 15.0, "handR": 15.0}
    B = blur({k: M[k] for k in bones if k in M}, sig)
    bones = [b for b in bones if b in B]
    P, T = build_mesh(fig)
    H = fig.height
    xi = np.clip(P[:, 0].round().astype(int), 0, fig.width - 1); yi = np.clip(P[:, 1].round().astype(int), 0, H - 1)
    stack = np.stack([B[b] for b in bones], 0)                                       # bones x H x W
    tot = stack.sum(0); lonely = tot < 0.15
    iy, ix = ndimage.distance_transform_edt(lonely, return_distances=False, return_indices=True)   # outside every part: copy the nearest owner
    yi, xi = iy[yi, xi], ix[yi, xi]
    Wt = stack[:, yi, xi].T                                                          # n x bones
    # arms: where the arm ramp is below 1 the remainder belongs to the torso
    ti = bones.index("torso")
    n = len(P); bi = np.zeros((n, 4), int); bw = np.zeros((n, 4))
    for i in range(n):
        w = Wt[i].copy()
        if w.sum() < 1e-4: w[ti] = 1.0
        top = np.argsort(-w)[:4]; ws = w[top]
        # sharpen: ignore weights under 6% of the total so far-off bones do not tug
        ws = np.where(ws / ws.sum() < 0.06, 0, ws); ws /= ws.sum()
        bi[i, :len(top)] = top; bw[i, :len(top)] = ws
    # draw order: triangles of higher part order come later (on top)
    tri_order = []
    for t in T:
        acc = np.zeros(len(bones))
        for v in t:
            for k in range(4): acc[bi[v, k]] += bw[v, k]
        tri_order.append(order[bones[int(acc.argmax())]])
    T = T[np.argsort(tri_order, kind="stable")]
    # winding must be consistent (Unity sprites are clockwise in y-up space)
    P2 = np.stack([P[:, 0], H - P[:, 1]], 1)
    fixed = []
    for t in T:
        a, b, c = P2[t]; cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        fixed.append(t if cross < 0 else t[[0, 2, 1]])
    T = np.array(fixed)
    fig.save(os.path.join(OUT, f"doll_{name}_{view}_fig.bytes"), format="PNG")
    print(name, view, "verts", n, "tris", len(T), "bones", len(bones))
    return {"name": view, "bones": bones, "vx": [round(float(v), 1) for v in P2[:, 0]], "vy": [round(float(v), 1) for v in P2[:, 1]],
            "tris": [int(v) for v in T.flatten()], "bi": [int(v) for v in bi.flatten()], "bw": [round(float(v), 3) for v in bw.flatten()]}

if __name__ == "__main__":
    name = sys.argv[1]; views = sys.argv[2:] or ["front", "back", "side"]
    jp = os.path.join(OUT, f"doll_{name}.json"); j = json.load(open(jp))
    for v in views:
        s = skin(name, v)
        for vm in j["views"]:
            if vm["name"] == v: vm["skin"] = s
    json.dump(j, open(jp, "w"))
