"""Bake a walk + idle sprite sheet from three painted views of one character (front, side facing right, back).
Each view is a matted RGBA cut-out with a hand-placed bone rig; the painting is deformed with linear-blend skinning
(the same technique as 2D skeletal animation tools) and each pose is saved as a frame that art/blender/pack.py packs.
  python art/comfy/bake_walk.py <cut_dir> <out_dir> [name]
<cut_dir> holds front.png, side.png, back.png. Frames: <out_dir>/<name>_{walk|idle}_{s|e|n}_{00..07}.png
Coordinates below are pixels of those cut-outs (640 px tall), y down."""
import sys, os, math
import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.spatial import Delaunay
from skimage import measure

# bone: (name, parent, head, [segments])
RIGS = {
    "front": [
        ("root", None, (118, 335), [((95, 335), (145, 335))]),
        ("spine", "root", (118, 330), [((118, 330), (118, 170))]),
        ("head", "spine", (118, 160), [((118, 150), (118, 60)), ((10, 75), (225, 85))]),
        ("armL1", "spine", (48, 205), [((48, 205), (28, 290))]),
        ("armL2", "armL1", (28, 290), [((28, 290), (30, 395))]),
        ("armR1", "spine", (190, 205), [((190, 205), (212, 290))]),
        ("armR2", "armR1", (212, 290), [((212, 290), (205, 405))]),
        ("legL1", "root", (95, 340), [((95, 340), (88, 470))]),
        ("legL2", "legL1", (88, 470), [((88, 470), (85, 555))]),
        ("footL", "legL2", (85, 555), [((85, 555), (70, 610))]),
        ("legR1", "root", (145, 340), [((145, 340), (152, 470))]),
        ("legR2", "legR1", (152, 470), [((152, 470), (160, 555))]),
        ("footR", "legR2", (160, 555), [((160, 555), (195, 610))]),
    ],
    "side": [
        ("root", None, (135, 345), [((120, 345), (150, 350))]),
        ("spine", "root", (135, 340), [((135, 340), (145, 170))]),
        ("head", "spine", (150, 155), [((150, 150), (160, 60)), ((80, 70), (235, 35))]),
        ("armL1", "spine", (140, 200), [((140, 200), (70, 285))]),
        ("armL2", "armL1", (70, 285), [((70, 285), (115, 385))]),
        ("legL1", "root", (128, 350), [((128, 350), (78, 515))]),
        ("legL2", "legL1", (78, 515), [((78, 515), (50, 585))]),
        ("footL", "legL2", (50, 585), [((50, 585), (10, 600))]),
        ("legR1", "root", (140, 355), [((140, 355), (185, 520))]),
        ("legR2", "legR1", (185, 520), [((185, 520), (185, 590))]),
        ("footR", "legR2", (185, 590), [((185, 590), (245, 625))]),
    ],
    "back": [
        ("root", None, (150, 395), [((120, 395), (180, 395))]),
        ("spine", "root", (150, 390), [((150, 390), (150, 180))]),
        ("head", "spine", (125, 120), [((125, 115), (125, 20))]),
        ("armL1", "spine", (45, 210), [((45, 210), (30, 300))]),
        ("armL2", "armL1", (30, 300), [((30, 300), (35, 360))]),
        ("armR1", "spine", (195, 205), [((195, 205), (205, 290))]),
        ("armR2", "armR1", (205, 290), [((205, 290), (220, 400))]),
        ("legL1", "root", (115, 400), [((115, 400), (98, 500))]),
        ("legL2", "legL1", (98, 500), [((98, 500), (88, 565))]),
        ("footL", "legL2", (88, 565), [((88, 565), (78, 615))]),
        ("legR1", "root", (180, 400), [((180, 400), (185, 500))]),
        ("legR2", "legR1", (185, 500), [((185, 500), (185, 565))]),
        ("footR", "legR2", (185, 565), [((185, 565), (200, 615))]),
    ],
}
DIRN = {"front": "s", "side": "e", "back": "n"}
PAD = 70          # canvas margin around the cut-out so swinging limbs are not clipped
FINAL_H = 176     # frame height of the figure after baking (px); the sheet stays small for the web build

def seg_dist(p, a, b):
    a = np.array(a, float); b = np.array(b, float); ab = b - a; L = float(ab @ ab)
    if L < 1e-6: return np.linalg.norm(p - a, axis=1)
    t = np.clip(((p - a) @ ab) / L, 0, 1)
    return np.linalg.norm(p - (a + t[:, None] * ab), axis=1)

class Rig:
    def __init__(self, img, bones, step=12, power=3, smooth=4):
        self.img = img; self.bones = bones; self.names = [b[0] for b in bones]
        a = np.array(img.getchannel("A")).astype(float) / 255
        h, w = a.shape; self.w, self.h = w, h
        a = ndimage.maximum_filter(a, size=5)
        cpts = []
        for c in measure.find_contours(np.pad(a, 1), 0.5):
            c = measure.approximate_polygon(c, 1.0)
            for i in range(len(c) - 1):
                p, q = c[i][::-1] - 1, c[i + 1][::-1] - 1
                n = max(1, int(np.linalg.norm(q - p) // (step * 0.6)))
                for k in range(n): cpts.append(tuple(p + (q - p) * k / n))
        cpts = np.array(cpts, float)
        ipts = [(x, y) for y in range(step // 2, h, step) for x in range(step // 2, w, step)
                if a[y, x] > 0.5 and a[max(0, y - 5):y + 6, max(0, x - 5):x + 6].min() > 0.5
                and np.min(np.linalg.norm(cpts - (x, y), axis=1)) > step * 0.6]
        pts = np.concatenate([cpts, np.array(ipts, float)])
        tri = Delaunay(pts)
        good = []
        for t in tri.simplices:
            c = pts[t].mean(axis=0)
            ok = a[int(np.clip(c[1], 0, h - 1)), int(np.clip(c[0], 0, w - 1))] > 0.3
            for i in range(3):
                m = (pts[t[i]] + pts[t[(i + 1) % 3]]) / 2
                ok = ok and a[int(np.clip(m[1], 0, h - 1)), int(np.clip(m[0], 0, w - 1))] > 0.2
            if ok: good.append(t)
        tris = np.array(good); used = np.unique(tris)
        self.pts = pts[used]
        # weights: inverse distance to each bone's segments, 4 strongest, smoothed across the mesh
        D = np.stack([np.min([seg_dist(self.pts, s0, s1) for s0, s1 in b[3]], axis=0) for b in bones], axis=1)
        W = 1.0 / (D + 6.0) ** power
        W /= W.sum(axis=1, keepdims=True)
        remap = {int(o): i for i, o in enumerate(used)}
        self.tris = np.vectorize(remap.get)(tris)
        nb = [set() for _ in range(len(self.pts))]
        for t in self.tris:
            for i in range(3): nb[t[i]].update((int(t[(i + 1) % 3]), int(t[(i + 2) % 3])))
        for _ in range(smooth):
            W2 = W.copy()
            for i in range(len(self.pts)):
                if nb[i]: W2[i] = 0.4 * W[i] + 0.6 * W[list(nb[i])].mean(axis=0)
            W = W2
        keep = np.argsort(-W, axis=1)[:, 4:]
        np.put_along_axis(W, keep, 0, axis=1); W /= W.sum(axis=1, keepdims=True)
        self.W = W

    def pose(self, ang, move=None):
        """ang: bone -> degrees; move: bone -> (dx, dy) applied in the parent's frame. Returns warped points."""
        move = move or {}
        mats = {}
        for name, parent, head, _ in self.bones:
            th = math.radians(ang.get(name, 0.0)); c, s = math.cos(th), math.sin(th)
            hx, hy = head
            local = np.array([[c, -s, hx - c * hx + s * hy], [s, c, hy - s * hx - c * hy], [0, 0, 1]])
            dx, dy = move.get(name, (0, 0))
            local = np.array([[1, 0, dx], [0, 1, dy], [0, 0, 1]]) @ local
            mats[name] = (mats[parent] @ local) if parent else local
        P = np.concatenate([self.pts, np.ones((len(self.pts), 1))], axis=1)
        out = np.zeros((len(self.pts), 2))
        for k, name in enumerate(self.names):
            out += self.W[:, k:k + 1] * (P @ mats[name].T)[:, :2]
        return out

    def layer(self, tri):
        """Painter's order: far leg, near leg, body, arms last (so folds resolve the same way every frame)."""
        dom = self.names[int(np.argmax(self.W[tri].sum(axis=0)))]
        for k, prefix in enumerate(("legL", "footL", "legR", "footR", "root", "spine", "head", "arm")):
            if dom.startswith(prefix): return k
        return 4

    def render(self, ang, move=None):
        from skimage.draw import polygon
        from scipy.ndimage import map_coordinates
        h, w = self.h, self.w
        src = np.array(self.img).astype(float) / 255
        src[..., :3] *= src[..., 3:4]                              # premultiply: no dark halos
        dst = self.pose(ang, move) + PAD
        H, W = h + 2 * PAD, w + 2 * PAD
        out = np.zeros((H, W, 4))
        if not hasattr(self, "order"):
            self.order = sorted(range(len(self.tris)), key=lambda i: self.layer(self.tris[i]))
        for i in self.order:
            t = self.tris[i]; d = dst[t]; s0 = self.pts[t]
            c = d.mean(axis=0); dd = c + (d - c) * 1.04 + (d - c) / (np.linalg.norm(d - c, axis=1, keepdims=True) + 1e-6) * 0.6
            rr, cc = polygon(dd[:, 1], dd[:, 0], (H, W))
            if len(rr) == 0: continue
            # affine from output (x, y) to source (x, y)
            A = np.hstack([d, np.ones((3, 1))])
            try: M = np.linalg.solve(A, s0)
            except np.linalg.LinAlgError: continue
            xy = np.stack([cc, rr, np.ones_like(cc)], axis=1).astype(float) @ M
            for ch in range(4):
                out[rr, cc, ch] = map_coordinates(src[..., ch], [xy[:, 1], xy[:, 0]], order=1, mode="nearest")
        a = out[..., 3:4]
        out[..., :3] = np.where(a > 1e-3, out[..., :3] / np.maximum(a, 1e-3), 0)
        return Image.fromarray((np.clip(out, 0, 1) * 255).astype("uint8"), "RGBA")

def stylize(fr):
    """Match the world: richer colour, flatter painted shapes, no paper-cream fringe, and the dark brown contour the bushes have."""
    from scipy import ndimage
    from PIL import ImageFilter, ImageEnhance
    a = np.array(fr.getchannel("A")).astype(float) / 255
    solid = ndimage.binary_erosion(a > 0.5, iterations=3)              # drop the soft, paper-coloured rim
    rgb = fr.convert("RGB").filter(ImageFilter.MedianFilter(5))
    rgb = ImageEnhance.Color(rgb).enhance(1.15); rgb = ImageEnhance.Contrast(rgb).enhance(1.12)
    body = np.array(rgb)
    # push the colour of the interior out into the eroded rim so no light pixels remain at the edge
    idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    body = body[idx[0], idx[1]]
    ring = ndimage.distance_transform_edt(~solid) <= 7
    out = np.zeros(body.shape[:2] + (4,), "uint8")
    out[ring] = (46, 27, 17, 255)
    soft = ndimage.gaussian_filter(solid.astype(float), 0.8)
    out[solid, :3] = body[solid]; out[solid, 3] = 255
    out[..., 3] = np.maximum(out[..., 3], (ndimage.gaussian_filter(ring.astype(float), 0.8) * 255).astype("uint8"))
    return Image.fromarray(out, "RGBA")

# ---- gait definitions ---------------------------------------------------------------------------------------------------
def gait(view, phase):
    s, c = math.sin(phase), math.cos(phase)
    ang, move = {}, {}
    if view == "side":
        A = 26.0 * (1 - c) / 2
        ang["legL1"], ang["legR1"] = -A, +A                      # forward = negative angle for a figure facing right
        ang["legL2"] = -A * 0.8 + 58.0 * max(0.0, s); ang["legR2"] = A * 0.8 + 58.0 * max(0.0, -s)
        ang["footL"] = -20.0 * max(0.0, s); ang["footR"] = -20.0 * max(0.0, -s)
        ang["armL1"] = 32.0 * c; ang["armL2"] = -22.0 * (1 + c) / 2
        ang["spine"] = -5.0 + 3.0 * s; ang["head"] = 3.0 * s
        move["root"] = (0, -16.0 * s * s)
    else:
        lift = 40.0
        l, r = max(0.0, s), max(0.0, -s)
        move["legL2"] = (0, -lift * l); move["legR2"] = (0, -lift * r)
        move["legL1"] = (0, -lift * 0.35 * l); move["legR1"] = (0, -lift * 0.35 * r)
        ang["armL1"] = -20.0 * s; ang["armR1"] = 20.0 * s
        ang["armL2"] = -12.0 * max(0.0, -s); ang["armR2"] = -12.0 * max(0.0, s)
        ang["spine"] = 5.0 * s; ang["head"] = -3.5 * s
        move["root"] = (12.0 * s, -14.0 * abs(s))
    return ang, move

def idle(view, phase):
    s = math.sin(phase)
    ang, move = {}, {}
    if view == "side":
        ang["legL1"] = -22.0; ang["legR1"] = 18.0                # bring the stride's feet together
        ang["footL"] = 10.0; ang["footR"] = -8.0
    ang["armL1"] = 4.5 * s; ang["armR1"] = -4.5 * s
    ang["spine"] = 1.6 * s; ang["head"] = -2.2 * s
    move["root"] = (0, 4.5 * s)
    return ang, move

def fix_side(img):
    """The matte ate the leading boot (it came out as a pale ghost): repaint it black rubber inside its box."""
    a = np.array(img).copy()
    x0, y0, x1, y1 = 165, 560, 253, 640
    box = a[y0:y1, x0:x1]
    solid = box[..., 3] > 12
    lum = box[..., :3].mean(axis=2) / 255
    dark = np.stack([30 + 40 * (1 - lum), 28 + 38 * (1 - lum), 28 + 36 * (1 - lum)], axis=-1)
    light = solid & (lum > 0.55)
    box[..., :3] = np.where(light[..., None], dark, box[..., :3])
    box[..., 3] = np.where(light, 255, box[..., 3])
    a[y0:y1, x0:x1] = box
    return Image.fromarray(a, "RGBA")

def auto_rig(img, view):
    """Rig placed from the silhouette by body proportions (used for the pests; the cafetero's rigs above were placed by hand)."""
    a = np.array(img.getchannel("A")) > 40
    H = a.shape[0]
    def ext(fy):
        y = int(fy * H); xs = np.where(a[max(0, y - 3):y + 4].any(axis=0))[0]
        return (xs.min(), xs.max()) if len(xs) else (0, a.shape[1] - 1)
    tl, tr = ext(0.38)
    cx = (tl + tr) / 2.0
    hy, ky, ay, ty = 0.54 * H, 0.735 * H, 0.915 * H, 0.985 * H
    sh_y, el_y, wr_y = 0.27 * H, 0.43 * H, 0.57 * H
    el, er = ext(0.43); wl, wr = ext(0.55); sl, sr = ext(0.27)
    kl, kr = ext(0.735); al, ar = ext(0.915)
    hd = 0.09 * H
    root = ("root", None, (cx, hy), [((cx - hd, hy), (cx + hd, hy))])
    spine = ("spine", "root", (cx, hy - 5), [((cx, hy - 5), (cx, 0.2 * H))])
    head = ("head", "spine", (cx, 0.19 * H), [((cx, 0.18 * H), (cx, 0.0))])
    if view == "side":
        fx = 0.07 * H
        lk, rk = kl + 0.18 * (kr - kl), kr - 0.18 * (kr - kl)
        la, ra_ = al + 0.15 * (ar - al), ar - 0.15 * (ar - al)
        return [root, spine, head,
            ("armL1", "spine", (cx, sh_y), [((cx, sh_y), (cx, el_y))]),
            ("armL2", "armL1", (cx, el_y), [((cx, el_y), (cx + 0.02 * H, wr_y))]),
            ("legL1", "root", (cx - 6, hy), [((cx - 6, hy), (lk, ky))]),
            ("legL2", "legL1", (lk, ky), [((lk, ky), (la, ay))]),
            ("footL", "legL2", (la, ay), [((la, ay), (la + fx, ty))]),
            ("legR1", "root", (cx + 6, hy), [((cx + 6, hy), (rk, ky))]),
            ("legR2", "legR1", (rk, ky), [((rk, ky), (ra_, ay))]),
            ("footR", "legR2", (ra_, ay), [((ra_, ay), (ra_ + fx, ty))])]
    ox = 0.06 * H
    lk, rk = kl + 0.27 * (kr - kl), kr - 0.27 * (kr - kl)
    la, ra_ = al + 0.22 * (ar - al), ar - 0.22 * (ar - al)
    lh, rh = cx - 0.07 * H, cx + 0.07 * H
    dx = -1 if view == "front" else 1                                   # the feet point to the viewer's side
    return [root, spine, head,
        ("armL1", "spine", (sl + 0.12 * (sr - sl), sh_y), [((sl + 0.12 * (sr - sl), sh_y), (el + 7, el_y))]),
        ("armL2", "armL1", (el + 7, el_y), [((el + 7, el_y), (wl + 7, wr_y))]),
        ("armR1", "spine", (sr - 0.12 * (sr - sl), sh_y), [((sr - 0.12 * (sr - sl), sh_y), (er - 7, el_y))]),
        ("armR2", "armR1", (er - 7, el_y), [((er - 7, el_y), (wr - 7, wr_y))]),
        ("legL1", "root", (lh, hy), [((lh, hy), (lk, ky))]),
        ("legL2", "legL1", (lk, ky), [((lk, ky), (la, ay))]),
        ("footL", "legL2", (la, ay), [((la, ay), (la - ox * 0.5, ty))]),
        ("legR1", "root", (rh, hy), [((rh, hy), (rk, ky))]),
        ("legR2", "legR1", (rk, ky), [((rk, ky), (ra_, ay))]),
        ("footR", "legR2", (ra_, ay), [((ra_, ay), (ra_ + ox * 0.5, ty))])]

def main():
    cut_dir, out_dir = sys.argv[1], sys.argv[2]
    name = sys.argv[3] if len(sys.argv) > 3 else "farmer"
    os.makedirs(out_dir, exist_ok=True)
    auto = os.environ.get("AUTO_RIG") == "1"
    for view in RIGS:
        img = Image.open(os.path.join(cut_dir, view + ".png")).convert("RGBA")
        if auto: bones = auto_rig(img, view)
        else:
            bones = RIGS[view]
            if view == "side": img = fix_side(img)
        rig = Rig(img, bones, power=6, smooth=1) if view == "side" else Rig(img, bones)
        hipx = int(round(bones[0][2][0])) + PAD
        scale = FINAL_H / img.height
        for anim, fn in (("walk", gait), ("idle", idle)):
            for i in range(8):
                ang, move = fn(view, 2 * math.pi * i / 8)
                fr = stylize(rig.render(ang, move))
                # centre the frame on the hips so the sprite pivots under the body, then shrink
                cw = max(hipx, fr.width - hipx)
                canvas = Image.new("RGBA", (2 * cw, fr.height), (0, 0, 0, 0)); canvas.alpha_composite(fr, (cw - hipx, 0))
                canvas = canvas.resize((max(1, round(canvas.width * scale)), max(1, round(canvas.height * scale))), Image.LANCZOS)
                canvas.save(os.path.join(out_dir, f"{name}_{anim}_{DIRN[view]}_{i:02d}.png"))
        print(view, "baked", len(rig.pts), "verts", flush=True)

if __name__ == "__main__":
    main()
