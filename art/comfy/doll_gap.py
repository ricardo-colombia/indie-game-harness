"""Remove leftover paper-coloured gaps (between the legs, under an arm) from a matted figure. python doll_gap.py <figure.png> <source.png>
Paper colour = median of the source image border; every enclosed or edge-connected blob of that colour bigger than 300 px turns transparent."""
import sys, numpy as np
from PIL import Image
from scipy import ndimage
fig = Image.open(sys.argv[1]).convert("RGBA"); a = np.array(fig)
src = np.array(Image.open(sys.argv[2]).convert("RGB")).astype(float)
bg = np.median(np.concatenate([src[0], src[:, 0], src[:, -1]]), axis=0)
# the figure was trimmed and resized: sample the paper colour from transparent-adjacent pixels instead of re-aligning, i.e. compare to bg directly
d = np.linalg.norm(a[..., :3].astype(float) - bg, axis=2)
near = (d < 10) & (a[..., 3] > 0)
lab, n = ndimage.label(near)
sizes = ndimage.sum(near, lab, range(1, n + 1))
kill = np.isin(lab, [i + 1 for i, v in enumerate(sizes) if v > 300])
a[kill, 3] = 0
al = ndimage.gaussian_filter(a[..., 3].astype(float), 0.8); a[..., 3] = np.minimum(a[..., 3], al.clip(0, 255)).astype("uint8") if False else a[..., 3]
Image.fromarray(a, "RGBA").save(sys.argv[1]); print("removed", int(kill.sum()), "px")
