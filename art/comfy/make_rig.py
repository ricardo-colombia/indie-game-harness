"""Build a 2D skinned-mesh rig (bones + weighted triangle mesh) from a matted character sprite.
  python art/comfy/make_rig.py farmer
Reads  game/Assets/Resources/Themes/Vintage/char_<name>.png (RGBA)
Writes game/Assets/Resources/Themes/Vintage/char_<name>.rig.json  (+ art/comfy/rig_<name>_debug.png)
Pixel coordinates, y down. Bones are listed parent-first; each has a head position in the bind pose and one or more
capsule segments that define which pixels it owns (weights = inverse distance to those segments, 3 strongest bones)."""
import sys, os, json
import numpy as np
from PIL import Image, ImageDraw
from scipy.spatial import Delaunay
from skimage import measure

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DIR = os.path.join(ROOT, "game", "Assets", "Resources", "Themes", "Vintage")

# name: (parent, head(x,y), [segments ((x0,y0),(x1,y1))]) -- farmer, 299x528 px
RIGS = {
    "doctor": [  # 285x528, no hat; the big head is its own bone
        ("root",   None,    (140, 425), [((140, 425), (140, 425))]),
        ("spine",  "root",  (140, 410), [((140, 410), (140, 285))]),
        ("head",   "spine", (140, 262), [((140, 262), (140, 120))]),
        ("armL1",  "spine", (98, 300),  [((98, 300), (82, 368))]),
        ("armL2",  "armL1", (82, 368),  [((82, 368), (112, 402))]),
        ("armR1",  "spine", (182, 300), [((182, 300), (195, 365))]),
        ("armR2",  "armR1", (195, 365), [((195, 365), (168, 402))]),
        ("legL",   "root",  (122, 428), [((122, 428), (114, 492))]),
        ("footL",  "legL",  (114, 494), [((114, 494), (98, 512))]),
        ("legR",   "root",  (160, 428), [((160, 428), (168, 492))]),
        ("footR",  "legR",  (168, 494), [((168, 494), (190, 512))]),
    ],
    "flaco": [  # 218x528, the beret is the "hat" bone
        ("root",   None,    (105, 308), [((105, 308), (105, 308))]),
        ("spine",  "root",  (105, 295), [((105, 295), (105, 165))]),
        ("head",   "spine", (105, 155), [((105, 155), (105, 95))]),
        ("hat",    "head",  (105, 70),  [((105, 70), (105, 25)), ((60, 55), (150, 55))]),
        ("armL1",  "spine", (72, 200),  [((72, 200), (58, 252))]),
        ("armL2",  "armL1", (58, 252),  [((58, 252), (84, 300))]),
        ("armR1",  "spine", (148, 200), [((148, 200), (186, 240))]),
        ("armR2",  "armR1", (186, 240), [((186, 240), (156, 288))]),
        ("legL",   "root",  (90, 312),  [((90, 312), (84, 470))]),
        ("footL",  "legL",  (84, 472),  [((84, 472), (45, 505))]),
        ("legR",   "root",  (124, 312), [((124, 312), (140, 470))]),
        ("footR",  "legR",  (140, 472), [((140, 472), (185, 505))]),
    ],
    "farmer": [
        ("root",   None,    (152, 322), [((152, 322), (152, 322))]),
        ("spine",  "root",  (152, 305), [((152, 305), (152, 185))]),
        ("head",   "spine", (150, 175), [((150, 170), (150, 118))]),
        ("hat",    "head",  (150, 100), [((150, 100), (150, 20)), ((30, 72), (270, 92))]),
        ("armL1",  "spine", (105, 205), [((105, 205), (50, 262))]),
        ("armL2",  "armL1", (50, 262),  [((50, 262), (95, 318))]),
        ("armR1",  "spine", (198, 205), [((198, 205), (252, 258))]),
        ("armR2",  "armR1", (252, 258), [((252, 258), (212, 318))]),
        ("legL",   "root",  (130, 328), [((130, 328), (125, 465))]),
        ("footL",  "legL",  (125, 468), [((125, 468), (110, 507))]),
        ("legR",   "root",  (176, 328), [((176, 328), (188, 465))]),
        ("footR",  "legR",  (188, 468), [((188, 468), (228, 502))]),
    ],
}

def seg_dist(p, a, b):
    a = np.array(a, float); b = np.array(b, float)
    ab = b - a; L = float(ab @ ab)
    if L < 1e-6: return np.linalg.norm(p - a, axis=1)
    t = np.clip(((p - a) @ ab) / L, 0, 1)
    return np.linalg.norm(p - (a + t[:, None] * ab), axis=1)

def build(name, step=13):
    img = Image.open(os.path.join(DIR, f"char_{name}.png")).convert("RGBA")
    w, h = img.size
    alpha = np.array(img.getchannel("A")).astype(float) / 255
    bones = RIGS[name]
    # The mesh covers the silhouette with a small margin (the texture's own alpha does the cutting), so it never nibbles the edge.
    from scipy import ndimage
    alpha = ndimage.maximum_filter(alpha, size=7)
    cpts = []
    for c in measure.find_contours(np.pad(alpha, 1), 0.5):
        c = measure.approximate_polygon(c, 1.2)
        for i in range(len(c) - 1):
            a = c[i][::-1] - 1; b = c[i + 1][::-1] - 1
            n = max(1, int(np.linalg.norm(b - a) // (step * 0.6)))
            for k in range(n): cpts.append(tuple(a + (b - a) * k / n))
    cpts = np.array(cpts, float)
    ipts = []
    for y in range(step // 2, h, step):
        for x in range(step // 2, w, step):
            if alpha[y, x] > 0.5 and alpha[max(0, y - 6):y + 7, max(0, x - 6):x + 7].min() > 0.5:
                if np.min(np.linalg.norm(cpts - (x, y), axis=1)) > step * 0.6: ipts.append((x, y))
    pts = np.concatenate([cpts, np.array(ipts, float)]) if ipts else cpts
    tri = Delaunay(pts)
    good = []
    for t in tri.simplices:
        c = pts[t].mean(axis=0)
        cx, cy = int(np.clip(c[0], 0, w - 1)), int(np.clip(c[1], 0, h - 1))
        # also test edge midpoints so triangles don't bridge gaps between limbs
        ok = alpha[cy, cx] > 0.3
        for i in range(3):
            m = (pts[t[i]] + pts[t[(i + 1) % 3]]) / 2
            ok = ok and alpha[int(np.clip(m[1], 0, h - 1)), int(np.clip(m[0], 0, w - 1))] > 0.2
        if ok: good.append(t)
    tris = np.array(good)
    used = np.unique(tris); remap = {int(o): i for i, o in enumerate(used)}
    pts = pts[used]; tris = np.vectorize(remap.get)(tris)
    # weights
    D = np.stack([np.min([seg_dist(pts, a, b) for a, b in segs], axis=0) for (_, _, _, segs) in bones], axis=1)
    W = 1.0 / (D + 6.0) ** 3
    W /= W.sum(axis=1, keepdims=True)
    # smooth the weights across the mesh so neighbouring vertices never snap to different bones (no torn edges when limbs swing)
    nb = [set() for _ in range(len(pts))]
    for t in tris:
        for i in range(3):
            nb[t[i]].add(int(t[(i + 1) % 3])); nb[t[i]].add(int(t[(i + 2) % 3]))
    for _ in range(4):
        W2 = W.copy()
        for i in range(len(pts)):
            if nb[i]: W2[i] = 0.4 * W[i] + 0.6 * W[list(nb[i])].mean(axis=0)
        W = W2
    bi = np.argsort(-W, axis=1)[:, :4]
    bw = np.take_along_axis(W, bi, axis=1); bw /= bw.sum(axis=1, keepdims=True)
    names = [b[0] for b in bones]
    rig = {
        "w": w, "h": h,
        "bones": [{"n": b[0], "p": (names.index(b[1]) if b[1] else -1), "x": b[2][0], "y": b[2][1]} for b in bones],
        "v": [round(float(v), 1) for p in pts for v in p],
        "bi": [int(v) for v in bi.flatten()],
        "bw": [round(float(v), 3) for v in bw.flatten()],
        "t": [int(v) for v in tris.flatten()],
    }
    out = os.path.join(DIR, f"char_{name}.rig.json")
    json.dump(rig, open(out, "w"), separators=(",", ":"))
    # debug image: mesh wire + dominant-bone colours
    dbg = Image.new("RGBA", (w, h), (40, 70, 45, 255)); dbg.alpha_composite(img)
    d = ImageDraw.Draw(dbg)
    pal = [(255, 80, 80), (255, 170, 60), (255, 240, 80), (120, 255, 90), (60, 230, 200), (70, 150, 255), (150, 90, 255), (255, 90, 220), (200, 200, 200), (255, 130, 130), (130, 255, 255), (255, 255, 160)]
    for t in tris:
        d.polygon([tuple(pts[i]) for i in t], outline=(255, 255, 255, 90))
    for i, p in enumerate(pts):
        c = pal[bi[i][0] % len(pal)]; d.ellipse([p[0] - 2, p[1] - 2, p[0] + 2, p[1] + 2], fill=c)
    for b in bones:
        d.ellipse([b[2][0] - 4, b[2][1] - 4, b[2][0] + 4, b[2][1] + 4], outline=(255, 255, 255, 255), width=2)
    dbg.save(os.path.join(ROOT, "art", "comfy", f"rig_{name}_debug.png"))
    print(out, len(pts), "verts", len(tris), "tris")

if __name__ == "__main__":
    build(sys.argv[1])
