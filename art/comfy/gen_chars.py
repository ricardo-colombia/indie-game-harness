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
    wf.update({
      "2": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt + ", " + suffix, "clip": clip}},
      "3": {"class_type": "CLIPTextEncode", "inputs": {"text": NEG, "clip": clip}},
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
            render(SLOTS[slot], 768, 1024, seed, style).save(out); print(out, flush=True)
