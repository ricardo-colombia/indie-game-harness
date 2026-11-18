"""Paper-doll part cutter for the bone rig.
  doll_cut.py matte <in.png> <out.png> [mirror]   figure on transparent background, trimmed, 768 px tall
  doll_cut.py grid  <fig.png> <out.png>           same with a labelled 50 px grid, to read joint coordinates
  doll_cut.py cut   <name> <view>                 art/doll/<name>/<view>_spec.json + <view>.png -> parts/<view>_<part>.png + <view>_parts.json
Spec: {"parts":[{"name","poly":[[x,y]..],"pivot":[x,y],"parent":"torso"|null,"order":int}]} in figure pixels (y down).
Each part = figure pixels inside its polygon (soft edge), trimmed; pivot kept in the figure frame so Unity can place it."""
import sys, os, json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(__file__))
H = 768

def matte(src, dst, mirror=False):
    import pest_cuts as pc
    figs = pc.figures(src)
    f = max(figs, key=lambda i: i.width * i.height)
    f = f.resize((round(f.width * H / f.height), H), Image.LANCZOS)
    if mirror: f = f.transpose(Image.FLIP_LEFT_RIGHT)
    f.save(dst); print(dst, f.size)

def grid(src, dst):
    im = Image.open(src).convert("RGBA"); bg = Image.new("RGBA", im.size, (200, 220, 255, 255)); bg.alpha_composite(im)
    d = ImageDraw.Draw(bg)
    for x in range(0, im.width, 50):
        d.line([(x, 0), (x, im.height)], fill=(255, 0, 0, 255) if x % 100 == 0 else (255, 150, 150, 255), width=1)
        if x % 100 == 0: d.text((x + 2, 2), str(x), fill=(200, 0, 0, 255))
    for y in range(0, im.height, 50):
        d.line([(0, y), (im.width, y)], fill=(0, 0, 255, 255) if y % 100 == 0 else (150, 150, 255, 255), width=1)
        if y % 100 == 0: d.text((2, y + 2), str(y), fill=(0, 0, 200, 255))
    bg.save(dst)


def backfill(fig, spec, torso, a):
    """The arms are cut from the body and then rotate down: the patch of body they covered would be a hole. Fill the part of each arm
    polygon that lies inside the torso's convex hull with torso colour so the swing never uncovers background."""
    from skimage.morphology import convex_hull_image
    hull = convex_hull_image(a > 40)
    arms = Image.new("L", fig.size, 0); d = ImageDraw.Draw(arms)
    for q in spec["parts"]:
        if q["name"] in ("armL", "armR"): d.polygon([tuple(t) for t in q["poly"]], fill=255)
    fill = hull & (np.array(arms) > 0) & (np.array(fig.getchannel("A")) > 40)
    return np.maximum(a, fill * 255.0)

def fillcolour(rgb, orig, filled):
    """Pixels added by backfill take the colour of the nearest original torso pixel."""
    from scipy import ndimage
    idx = ndimage.distance_transform_edt(orig <= 40, return_distances=False, return_indices=True)
    out = rgb.copy(); add = (filled > 40) & (orig <= 40)
    out[add] = rgb[idx[0][add], idx[1][add]]
    return out

def cut(name, view):
    base = os.path.join(os.path.dirname(__file__), "..", "doll", name)
    fig = Image.open(os.path.join(base, view + ".png")).convert("RGBA")
    spec = json.load(open(os.path.join(base, view + "_spec.json")))
    os.makedirs(os.path.join(base, "parts"), exist_ok=True)
    out = []
    for p in spec["parts"]:
        m = Image.new("L", fig.size, 0); ImageDraw.Draw(m).polygon([tuple(q) for q in p["poly"]], fill=255)
        m = m.filter(ImageFilter.GaussianBlur(0.8))
        a = np.minimum(np.array(m).astype(float), np.array(fig.getchannel("A")).astype(float))
        rgb = np.array(fig)
        if p["name"] == "torso":
            a0 = a; a = backfill(fig, spec, p, a); rgb = fillcolour(rgb, a0, a)
        part = Image.fromarray(rgb); part.putalpha(Image.fromarray(a.astype("uint8")))
        bb = part.getchannel("A").point(lambda v: 255 if v > 12 else 0).getbbox()
        part = part.crop(bb)
        fn = f"{view}_{p['name']}.png"; part.save(os.path.join(base, "parts", fn))
        rest = p.get("rest")
        if rest is None and p["name"] in ("armL", "armR"):
            # The brief says walking arms hang at the sides. Figures are drawn arms-out, so the rig rotates each arm about its shoulder until the
            # arm's mass points straight down (anim_brief.ARM_REST_MAX caps it). Unity z is counter-clockwise; image y points down.
            al = np.array(part.getchannel("A")).astype(float); ys, xs = np.nonzero(al > 40)
            px_, py_ = xs + bb[0] - p["pivot"][0], -(ys + bb[1] - p["pivot"][1])
            far = np.hypot(px_, py_) >= np.percentile(np.hypot(px_, py_), 97)   # the hand end of the arm decides where it points
            vx, vy = px_[far].mean(), py_[far].mean()
            rest = float(np.clip(np.degrees(np.arctan2(-vx, -vy)), -80, 80)); rest -= np.sign(rest) * min(abs(rest), 8.0)   # hang a little away from the body
        out.append({"name": p["name"], "file": fn, "bbox": list(bb), "pivot": p["pivot"], "parent": p.get("parent"), "order": p["order"], "rest": rest or 0.0})
    json.dump({"w": fig.width, "h": fig.height, "parts": out}, open(os.path.join(base, view + "_parts.json"), "w"), indent=1)
    print(view, len(out), "parts")

if __name__ == "__main__":
    c = sys.argv[1]
    if c == "matte": matte(sys.argv[2], sys.argv[3], len(sys.argv) > 4)
    elif c == "grid": grid(sys.argv[2], sys.argv[3])
    else: cut(sys.argv[2], sys.argv[3])
