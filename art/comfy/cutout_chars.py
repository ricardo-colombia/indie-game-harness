"""Cut a generated character out of its white background and write it as a game sprite.
  python art/comfy/cutout_chars.py art/incoming/farmer_gouache_1.png farmer
-> game/Assets/Resources/Themes/Vintage/char_farmer.png (RGBA, trimmed, 384 px tall)."""
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "Resources", "Themes", "Vintage")

def cut(src, name, height=384):
    im = Image.open(src).convert("RGB")
    w, h = im.size
    # Exterior = light, neutral pixels connected to the border (white sticker halo + grey/white backdrop);
    # the dark outline keeps interior light areas (white shirts) from being eaten.
    arr = np.array(im).astype(int)
    light = (arr.min(axis=2) > 150) & ((arr.max(axis=2) - arr.min(axis=2)) < 28)
    from scipy import ndimage
    labels, _ = ndimage.label(light)
    edge = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    bg = np.isin(labels, list(edge))
    alpha = Image.fromarray(((~bg) * 255).astype("uint8"))
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    rgba = im.copy(); rgba.putalpha(alpha)
    box = alpha.point(lambda v: 255 if v > 24 else 0).getbbox()
    rgba = rgba.crop(box)
    scale = height / rgba.height
    rgba = rgba.resize((max(1, round(rgba.width * scale)), height), Image.LANCZOS)
    # A thin cream rim around the dark outline (sticker look) so the figure reads against dark soil and leaves.
    pad = 10
    canvas = Image.new("RGBA", (rgba.width + 2 * pad, rgba.height + 2 * pad), (0, 0, 0, 0))
    canvas.alpha_composite(rgba, (pad, pad))
    a = canvas.getchannel("A").point(lambda v: 255 if v > 40 else 0)
    rim = a.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(0.9))
    halo = Image.new("RGBA", canvas.size, (255, 247, 228, 0)); halo.putalpha(rim)
    halo.alpha_composite(canvas)
    rgba = halo
    os.makedirs(OUT, exist_ok=True)
    out = os.path.join(OUT, f"char_{name}.png")
    rgba.save(out)
    print(out, rgba.size)

if __name__ == "__main__":
    cut(sys.argv[1], sys.argv[2])
