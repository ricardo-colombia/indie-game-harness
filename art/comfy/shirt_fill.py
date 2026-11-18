"""Cover the bare pale belly that Flux draws under an open shirt with shirt cloth (it read as a second face).
  python art/comfy/shirt_fill.py <cut.png> <y0> <y1> [min_brightness=200]
Pale pinkish skin pixels below y0 (under the face) are recoloured to cream cloth, keeping the shading."""
import sys, numpy as np
from PIL import Image
from scipy import ndimage
p, y0, y1 = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]); thr = float(sys.argv[4]) if len(sys.argv) > 4 else 200
a = np.array(Image.open(p).convert("RGBA")).astype(float); r, g, b = a[..., 0], a[..., 1], a[..., 2]
m = np.zeros(r.shape, bool); m[y0:y1] = True
skin = (r > g + 5) & (g > b) & (a[..., 3] > 200) & (r > thr) & m
sel = ndimage.binary_opening(skin, iterations=2)
lab, n = ndimage.label(sel); sz = ndimage.sum(sel, lab, range(1, n + 1)); sel = np.isin(lab, [i + 1 for i, v in enumerate(sz) if v > 2500])
sel = ndimage.binary_fill_holes(ndimage.binary_closing(sel, iterations=6))
sel = ndimage.binary_opening(ndimage.binary_dilation(sel, iterations=4) & m & (a[..., 3] > 200), iterations=3)
lum = 0.3 * r + 0.59 * g + 0.11 * b; shade = np.clip((lum - lum[sel].min()) / (np.ptp(lum[sel]) + 1), 0, 1)
sm = ndimage.gaussian_filter(sel.astype(float), 1.5)[..., None]
new = np.array([244., 240, 228])[None, None, :] * (0.80 + 0.2 * shade[..., None])
a[..., :3] = a[..., :3] * (1 - sm) + new * sm
Image.fromarray(a.astype("uint8"), "RGBA").save(p); print(p, int(sel.sum()), "px")
