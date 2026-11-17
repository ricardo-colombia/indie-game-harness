"""COFFEE-MAN character sprites with the local ComfyUI (free, SDXL base [+ pixel-art-xl LoRA]).
  ~/ComfyUI-local/app/.venv/bin/python art/comfy/gen_chars.py <slot> [seeds] [style]
Raw variants land in art/incoming/<slot>_<style>_<seed>.png on a white background."""
import json, sys, time, urllib.request, os
from PIL import Image

HOST = "http://127.0.0.1:8188"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "art", "incoming")
STYLES = {
  "gouache": ("flat vector cartoon game character sprite, sticker with thick white border, clean thick dark brown outline, warm saturated earthy palette, painterly, flat shading, chibi cartoon proportions with a big head and a short body, the entire body visible from hat to boots with both feet, single character, isolated on a plain pure white empty background, no scenery, no text", None),
  "rayman": ("Rayman Legends art style, hand-painted UbiArt illustration, rich soft gradients, volumetric lighting with a glowing rim light, vibrant saturated colours, polished modern AAA game character art, expressive cartoon face with big lively eyes, limbless character: only the head and torso, floating, no arms and no legs, isolated on a plain pure white background, no border, no text", None),
  "render3d": ("Pixar and Rayman Legends quality 3D animated character render, subsurface scattering, soft studio lighting with a warm rim light, big expressive lively eyes, rich saturated colours, no outline, polished modern AAA game character art, full body visible from hat to shoes, single character, isolated on a plain pure white background, no floor, no shadow, no text", None),
  "legends2d": ("2D hand-painted game character illustration in the style of Rayman Legends, gouache and ink, warm earthy palette of coffee browns, leaf greens, cream and cherry red, soft painterly shading, clean bold silhouette, big expressive eyes, cartoon proportions with a big head, front view, full body standing in an A-pose with both arms held out away from the torso and both legs slightly apart, hands and feet fully visible, single character, isolated on a plain pure white background, no floor, no shadow, no text", None),
  "legends3": ("polished 2D game character painting in the style of Rayman Legends and Disney concept art, soft volumetric shading, warm rim light, rich painted textures, warm earthy palette of coffee browns, leaf greens, cream and cherry red, clean bold silhouette, big expressive eyes, cartoon proportions with a big head and long limbs, front view, full body standing with hands on hips and legs slightly apart, hands and feet fully visible, single character, plain warm cream background, no text", None),
  "pixel": ("pixel art, game character sprite, clean dark outline, limited warm palette, full body, single character, isolated on a plain pure white empty background, no scenery, no text", "pixel-art-xl.safetensors"),
}
NEG = "gun, pistol, rifle, weapon, firearm, painting, oil painting, landscape, trees, path, road, fence, forest, scenery, background, ground, grass, shadow, text, watermark, signature, frame, border, photo, 3d render, noisy, blurry, cropped, multiple characters, extra limbs, deformed, collage, sheet, turnaround"
LOOK = "sticker design of a chibi cartoon character, standing, facing the viewer, three quarter view, "
SLOTS = {
  "farmer": LOOK + "an old Colombian coffee farmer campesino, straw-coloured sombrero aguadeno with black band, white long sleeve shirt, blue jeans, rubber boots, big grey moustache, holding a wicker basket of red coffee cherries",
  "doctor": LOOK + "a stern Colombian government tax official in a grey suit and tie, carrying a thick folder and a briefcase, wearing a motorcycle helmet, thin and serious, clipboard",
  "flaco": LOOK + "a thin young man, Colombian rebel fighter with a short black beard in olive green camouflage uniform, black beret with a red star, bandana over his face, rubber boots, holding a red megaphone in one hand, empty other hand",
  "hernan": LOOK + "a wealthy smug Colombian landowner, white linen guayabera shirt, dark sunglasses, gold chain, white cowboy hat, big belly, crossed arms",
}

BODY_NEG = "arms, hands, fingers, legs, feet, shoes, boots, sticker, white border, outline border, frame, text, watermark, scenery, ground, shadow on floor, multiple characters, cropped, blurry, photo, pixel art"
BODY = {
  "farmer": "a cheerful old Colombian coffee farmer, big grey moustache, kind eyes, straw sombrero aguadeno with a black band, white shirt with a brown vest",
  "doctor": "a stern thin Colombian government tax official, round glasses, grey suit, orange tie, slicked brown hair, angry eyebrows",
  "flaco": "a thin young man, Colombian rebel fighter, olive green uniform, black beret with a red star, short black beard, red scarf, serious eyes",
  "hernan": "a smug wealthy Colombian landowner, big belly, white linen guayabera shirt, dark sunglasses, gold chain, white cowboy hat, thick moustache",
}

NEG3D = "sticker, white border, outline, flat 2d, painting, sketch, cropped, multiple characters, text, watermark, scenery, ground, floor, gun, weapon, photo, pixel art, blurry"
FULL = {
  "farmer": "a cheerful old paisa coffee farmer from the Colombian Eje Cafetero, cream straw sombrero aguadeno with a black band, big grey moustache, white cotton shirt with rolled sleeves, a brown leather carriel antioqueno satchel hanging across his chest, a folded striped wool poncho over one shoulder, cream cotton trousers rolled up to the calves, black rubber boots, empty hands on hips",
  "doctor": "a stern thin Colombian government tax official, round glasses, slicked brown hair, grey suit, orange tie, polished black shoes, angry eyebrows, empty hands on hips",
  "flaco": "a thin young man, Colombian rebel fighter, olive green uniform, black beret with a red star, short black beard, red scarf, brown boots, empty hands on hips, no weapon",
  "hernan": "a smug wealthy Colombian landowner, big belly, white linen guayabera shirt, dark sunglasses, gold chain, white cowboy hat, thick moustache, brown leather boots, empty hands on hips",
}

def post(path, data):
    req = urllib.request.Request(HOST + path, data=json.dumps(data).encode(), headers={"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req).read())

def workflow(prompt, w, h, seed, style):
    suffix, lora = STYLES[style]
    model, clip = ["1", 0], ["1", 1]
    wf = {"1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "sd_xl_base_1.0.safetensors"}}}
    if lora:
        wf["8"] = {"class_type": "LoraLoader", "inputs": {"model": ["1", 0], "clip": ["1", 1], "lora_name": lora, "strength_model": 1.0, "strength_clip": 1.0}}
        model, clip = ["8", 0], ["8", 1]
    neg = BODY_NEG if style == "rayman" else (NEG3D if style in ("render3d", "legends2d", "legends3") else NEG)
    wf.update({
      "2": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt + ", " + suffix, "clip": clip}},
      "3": {"class_type": "CLIPTextEncode", "inputs": {"text": neg, "clip": clip}},
      "4": {"class_type": "EmptyLatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}},
      "5": {"class_type": "KSampler", "inputs": {"model": model, "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0],
            "seed": seed, "steps": 28, "cfg": 6.5, "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0}},
      "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
      "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": "coffee"}},
    })
    return wf

def wait_idle():
    # Share the server politely: never queue behind another session's pending work.
    while json.loads(urllib.request.urlopen(HOST + "/queue").read())["queue_pending"]:
        time.sleep(3)

def render(prompt, w, h, seed, style):
    wait_idle()
    pid = post("/prompt", {"prompt": workflow(prompt, w, h, seed, style)})["prompt_id"]
    while True:
        hist = json.loads(urllib.request.urlopen(HOST + "/history/" + pid).read())
        if pid in hist and hist[pid].get("outputs"):
            im = list(hist[pid]["outputs"].values())[0]["images"][0]
            url = f"{HOST}/view?filename={im['filename']}&subfolder={im['subfolder']}&type={im['type']}"
            return Image.open(urllib.request.urlopen(url)).convert("RGB")
        time.sleep(2)

if __name__ == "__main__":
    slot = sys.argv[1]; seeds = [int(s) for s in (sys.argv[2] if len(sys.argv) > 2 else "1,2,3,4").split(",")]
    for style in (sys.argv[3].split(",") if len(sys.argv) > 3 else ["gouache", "pixel"]):
        for seed in seeds:
            out = os.path.join(RAW, f"{slot}_{style}_{seed}.png")
            if style in ("render3d", "legends2d", "legends3"):
                render(FULL[slot], 768, 1024, seed, style).save(out); print(out, flush=True)
            elif style == "rayman":
                render(BODY[slot], 832, 896, seed, style).save(out); print(out, flush=True)
            else:
                render(SLOTS[slot], 768, 1024, seed, style).save(out); print(out, flush=True)
