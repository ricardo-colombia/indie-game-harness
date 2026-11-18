"""Cut Botero item renders (art/incoming/item_<name>_<seed>.png) into theme sprites.
  python art/comfy/cut_items.py bean=301 mug=302 ...   (name=seed; bush is cut at 512x384, everything else 256x256)
Matte = rembg isnet, solid alpha, trimmed, centred with a small margin, written to game/Assets/Resources/Themes/Vintage/<name>.png"""
import sys, os
import numpy as np
from PIL import Image
from rembg import remove, new_session
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "Resources", "Themes", "Vintage")
sess = new_session("isnet-general-use")

def cut(name, seed):
    src = Image.open(os.path.join(ROOT, "art", "incoming", f"item_{name}_{seed}.png")).convert("RGB")
    cutout = remove(src, session=sess)
    a = np.array(cutout.getchannel("A")).astype(float) / 255
    # fill pinholes, drop specks, harden the edge a little so the sprite has no white fringe
    solid = a > 0.5
    # close only small holes: a mug handle or the gap between leaves must stay see-through
    holes = ndimage.binary_fill_holes(solid) & ~solid
    hl, hn = ndimage.label(holes)
    for i in range(1, hn + 1):
        if (hl == i).sum() < 0.01 * solid.sum():
            solid |= hl == i
    lab, n = ndimage.label(solid)
    if n > 1:
        sizes = ndimage.sum(solid, lab, range(1, n + 1)); solid = np.isin(lab, [i + 1 for i, v in enumerate(sizes) if v > 0.05 * sizes.max()])
    solid = ndimage.binary_erosion(solid, iterations=2)
    alpha = ndimage.gaussian_filter(solid.astype(float), 1.0)
    rgba = np.array(src.convert("RGBA")); rgba[..., 3] = (np.clip(alpha, 0, 1) * 255).astype("uint8")
    im = Image.fromarray(rgba, "RGBA"); im = im.crop(im.getchannel("A").point(lambda v: 255 if v > 20 else 0).getbbox())
    w, h = (512, 384) if name == "bush" else (256, 256)
    m = 6
    s = min((w - 2 * m) / im.width, (h - 2 * m) / im.height)
    im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.alpha_composite(im, ((w - im.width) // 2, h - m - im.height if name == "bush" else (h - im.height) // 2))
    canvas.save(os.path.join(OUT, name + ".png")); print(name, seed, canvas.size)

if __name__ == "__main__":
    for spec in sys.argv[1:]:
        n, s = spec.split("="); cut(n, s)
