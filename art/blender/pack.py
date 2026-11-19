"""Pack rendered frames into one sprite sheet + json. python pack.py <renders> <name> -> game Resources/Themes/Vintage/anim_<name>.png/.json
Rows: anim (walk, idle) x dir (s,e,n); columns: frames. Trimmed to a shared tight box so feet stay on the baseline."""
import sys, os, json
from PIL import Image
src, name = sys.argv[1], sys.argv[2]
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "Resources", "Themes", "Vintage")
DIRS = ["s", "e", "n"]; ANIMS = {"walk": 8, "idle": 8}
frames = {}
for a, n in ANIMS.items():
    for d in DIRS:
        frames[(a, d)] = [Image.open(f"{src}/{name}_{a}_{d}_{i:02d}.png").convert("RGBA") for i in range(n)]
# shared crop box over every frame
l = t = 10**9; r = b = -1
for fl in frames.values():
    for f in fl:
        bb = f.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
        if bb: l, t, r, b = min(l, bb[0]), min(t, bb[1]), max(r, bb[2]), max(b, bb[3])
pad = 3; l -= pad; t -= pad; r += pad; b += pad
W, H = r - l, b - t
cols = max(ANIMS.values()); rows = len(DIRS) * len(ANIMS)
sheet = Image.new("RGBA", (W * cols, H * rows), (0, 0, 0, 0))
rowmap = {}
ri = 0
for a in ANIMS:
    for d in DIRS:
        rowmap[f"{a}_{d}"] = ri
        for i, f in enumerate(frames[(a, d)]):
            sheet.alpha_composite(f.crop((l, t, r, b)), (i * W, ri * H))
        ri += 1
def pot(n):
    k = 1
    while k < n: k *= 2
    return k
canvas = Image.new("RGBA", (pot(sheet.width), pot(sheet.height)), (0, 0, 0, 0))
canvas.alpha_composite(sheet, (0, 0))   # top-left, so rows are still counted from the top
# the scene's bloom starts at 0.9: roll the highlights off below it so painted whites (hat, shirt) do not glow out of the level
import numpy as np
a = np.array(canvas).astype(np.float32) / 255.0
rgb = a[..., :3]; k = 0.72; top = 0.84
rgb = np.where(rgb > k, k + (rgb - k) / (1 - k) * (top - k), rgb)
a[..., :3] = rgb
canvas = Image.fromarray((a * 255 + 0.5).astype(np.uint8), "RGBA")
canvas.save(os.path.join(OUT, f"anim_{name}.png"), optimize=True)
sheet = canvas
# import settings: no mipmaps / compression / rescale, so frame rectangles stay exact
mp = os.path.join(OUT, f"anim_{name}.png.meta")
if not os.path.exists(mp):
    import re, uuid
    t = open(os.path.join(OUT, "char_farmer.png.meta")).read()
    t = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, t, count=1)
    t = t.replace("enableMipMap: 1", "enableMipMap: 0").replace("textureCompression: 1", "textureCompression: 0")
    open(mp, "w").write(t)
meta = {"w": W, "h": H, "walk": ANIMS["walk"], "idle": ANIMS["idle"], "dirs": len(DIRS), "pad": pad, "idleFps": 4}
# baseline: lowest opaque pixel across frames within crop (feet) -> fraction from the bottom of the frame
json.dump(meta, open(os.path.join(OUT, f"anim_{name}.json"), "w"))
print(W, H, sheet.size)
