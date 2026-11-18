"""Cut turnaround-sheet figures into front/side/back cut-outs for bake_walk.py.
  python art/comfy/pest_cuts.py <name> front=<sheet.png>:<n> side=<sheet.png>:<n> back=<sheet.png>:<n>
<n> is the 1-based figure index (left to right) in that sheet. Output: art/pest_cuts/<name>/{front,side,back}.png, 640 px tall.
Matte = rembg isnet (same as the cafetero)."""
import sys, os
import numpy as np
from PIL import Image
from rembg import remove, new_session

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
H = 640
_sess = None

def solidify(rgb, cut):
    """White shirts on a cream sheet come out of rembg half transparent: keep rembg's soft edge, but make everything that is not
    background (flat cream reached from the border) fully opaque."""
    from scipy import ndimage
    a = np.array(rgb).astype(float)
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    bg = np.median(border, axis=0)
    near = np.linalg.norm(a - bg, axis=2) < 7
    lab, n = ndimage.label(near)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    sizes0 = ndimage.sum(near, lab, range(1, n + 1))
    edge |= {i + 1 for i, v in enumerate(sizes0) if 1500 < v < 45000}          # enclosed gaps (arm against the body) of the flat paper colour
    bgflood = np.isin(lab, list(edge))
    fg = ~bgflood
    fg = ndimage.binary_opening(fg, iterations=2)
    lab, n = ndimage.label(fg)
    if n:
        sizes = ndimage.sum(fg, lab, range(1, n + 1)); fg = np.isin(lab, [i + 1 for i, v in enumerate(sizes) if v > 4000])
    ra = np.array(cut.getchannel("A")).astype(float) / 255
    core = ndimage.binary_erosion(fg, iterations=3)
    soft = ndimage.gaussian_filter(fg.astype(float), 1.2)
    alpha = np.where(core, 1.0, np.minimum(soft, np.maximum(ra, 0.0) * 0 + soft))
    out = np.array(rgb.convert("RGBA")); out[..., 3] = (np.clip(alpha, 0, 1) * 255).astype("uint8")
    return Image.fromarray(out, "RGBA")

def figures(path):
    global _sess
    _sess = _sess or new_session("isnet-general-use")
    im = Image.open(path).convert("RGB")
    im2 = im.resize((im.width * 2, im.height * 2), Image.LANCZOS)
    cut = remove(im2, session=_sess)
    cut = solidify(im2, cut)
    a = np.array(cut.getchannel("A")) > 40
    col = a.any(axis=0)
    out, x = [], 0
    while x < len(col):
        if col[x]:
            x0 = x
            gap = 0
            while x < len(col) and gap < 14:
                gap = 0 if col[x] else gap + 1
                x += 1
            x1 = x - gap
            sub = cut.crop((x0, 0, x1, cut.height))
            bb = sub.getchannel("A").point(lambda v: 255 if v > 40 else 0).getbbox()
            if bb and (bb[2] - bb[0]) * (bb[3] - bb[1]) > 20000: out.append(sub.crop(bb))
        else: x += 1
    return out

def main():
    name = sys.argv[1]
    d = os.path.join(ROOT, "art", "pest_cuts", name); os.makedirs(d, exist_ok=True)
    cache = {}
    for spec in sys.argv[2:]:
        view, rest = spec.split("=")
        path, n = rest.rsplit(":", 1)
        if path not in cache: cache[path] = figures(path)
        figs = cache[path]
        print(view, path, "figures:", len(figs))
        f = figs[int(n) - 1]
        f = f.resize((round(f.width * H / f.height), H), Image.LANCZOS)
        f.save(os.path.join(d, view + ".png"))

if __name__ == "__main__":
    main()
