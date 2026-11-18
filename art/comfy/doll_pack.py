"""Pack a doll's parts into one atlas per view + one json, as TextAssets the game loads (no importer settings to go wrong).
  python art/comfy/doll_pack.py <name>   (needs art/doll/<name>/parts/ and <view>_parts.json from doll_cut.py)
Writes game/Assets/Resources/Themes/Vintage/doll_<name>.json and doll_<name>_<view>.bytes (PNG)."""
import sys, os, json
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "Resources", "Themes", "Vintage")
name = sys.argv[1]; base = os.path.join(ROOT, "art", "doll", name)
views = []
for v in ["front", "back", "side"]:
    meta = json.load(open(os.path.join(base, v + "_parts.json")))
    imgs = [(p, Image.open(os.path.join(base, "parts", p["file"]))) for p in meta["parts"]]
    W = 1024; x = y = rowh = 0; place = []
    for p, im in sorted(imgs, key=lambda t: -t[1].height):
        if x + im.width + 4 > W: x, y, rowh = 0, y + rowh + 4, 0
        place.append((p, im, x, y)); x += im.width + 4; rowh = max(rowh, im.height)
    H = y + rowh + 4
    atlas = Image.new("RGBA", (W, H), (0, 0, 0, 0)); parts = []
    for p, im, ax, ay in place:
        atlas.alpha_composite(im, (ax, ay))
        parts.append({"name": p["name"], "ax": ax, "ay": ay, "aw": im.width, "ah": im.height, "bx": p["bbox"][0], "by": p["bbox"][1],
                      "px": p["pivot"][0], "py": p["pivot"][1], "parent": p["parent"] or "", "order": p["order"]})
    atlas.save(os.path.join(OUT, f"doll_{name}_{v}.bytes"), format="PNG")
    views.append({"name": v, "w": meta["w"], "h": meta["h"], "parts": parts}); print(v, atlas.size)
json.dump({"figureHeight": 768, "views": views}, open(os.path.join(OUT, f"doll_{name}.json"), "w"))
