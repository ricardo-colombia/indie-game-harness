"""COFFEE-MAN character turnaround sheets with Flux schnell (local ComfyUI, free).
  ~/ComfyUI-local/app/.venv/bin/python art/comfy/gen_flux.py <slot> [seeds]
One image holds the same character three times (front, side, back), so all views share one look.
Raw sheets land in art/incoming/sheet_<slot>_<seed>.png."""
import json, sys, time, urllib.request, os

HOST = os.environ.get("COMFY_HOST", "http://127.0.0.1:8188")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "art", "incoming")

# ONE aesthetic for the whole cast (and the bushes): vintage hand-painted coffee-label illustration.
STYLE = ("in the style of Fernando Botero, the Colombian painter: Boterismo, voluminous inflated rotund figures with exaggerated round plump bodies, "
         "tiny hands and tiny feet, small delicate facial features with a calm serene expression, flat matte oil painting on canvas, not a 3D render, no CGI, "
         "flat warm saturated colours, soft rounded shapes, thick soft outline, full body, game character design")
SHEET = ("character turnaround model sheet, the same character drawn three times side by side on a plain flat cream background: "
         "on the left facing the camera (front view), in the middle in side profile facing right (walking pose, one foot forward), "
         "on the right seen from behind (back view). Consistent design in all three views, arms slightly away from the body, no text, no labels. ")
SLOTS = {
  "farmer": "a plump round old Colombian paisa coffee farmer from the Eje Cafetero with tanned mestizo skin and deep smile lines, white moustache, "
            "cream straw sombrero aguadeno with a black band, a fully buttoned white cotton shirt with rolled sleeves covering the whole belly (never bare-chested), a brown leather carriel antioqueno bag across the chest, "
            "a striped wool ruana poncho folded over one shoulder, cream trousers rolled to the calf, black rubber boots, empty hands",
  "doctor": "a plump round stern Colombian bogotano bureaucrat (a 'doctor' of the tax office) with brown mestizo skin, black moustache and round glasses, bare head with slicked black hair, "
            "plain white long-sleeve cotton shirt buttoned to the collar with no tie, a dark brown wool ruana poncho over the shoulders, dark wool trousers, black leather shoes, empty hands",
  "flaco": "a short round young Colombian campesino organizer with brown mestizo skin and a short black beard, small woven straw sombrero with a red band, "
           "worn olive green cotton work shirt, red bandana around the neck, faded blue denim trousers rolled to the calf, black rubber boots (botas pantaneras), empty hands, no weapon",
  "hernan": "a wealthy smug Colombian hacendado landowner from the Llanos with tanned skin and a thick black moustache, wide-brim cream felt sombrero, white linen guayabera with four pockets, "
            "huge round belly, wide leather belt with a silver buckle, brown leather boots, empty hands",
}

def post(path, data):
    req = urllib.request.Request(HOST + path, json.dumps(data).encode(), {"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req).read())

def workflow(prompt, w, h, seed):
    wf = _workflow(prompt, w, h, seed)
    lora = os.environ.get("LORA")          # e.g. LORA=botero_flux.safetensors LORA_STRENGTH=1.2 (the model-only LoRA goes before the sampler)
    if lora:
        wf["9"] = {"class_type": "LoraLoaderModelOnly", "inputs": {"lora_name": lora, "strength_model": float(os.environ.get("LORA_STRENGTH", "1.0")), "model": ["1", 0]}}
        wf["6"]["inputs"]["model"] = ["9", 0]
        wf["6"]["inputs"]["steps"] = int(os.environ.get("STEPS", "4"))
        prompt = "style of Fernando Botero. " + prompt
        wf["4"]["inputs"]["text"] = prompt
    return wf

def _workflow(prompt, w, h, seed):
    return {
      "1": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "flux1-schnell-Q4_K_S.gguf"}},
      "2": {"class_type": "DualCLIPLoader", "inputs": {"clip_name1": "clip_l.safetensors", "clip_name2": "t5xxl_fp8_e4m3fn.safetensors", "type": "flux"}},
      "3": {"class_type": "VAELoader", "inputs": {"vae_name": "ae.safetensors"}},
      "4": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 0]}},
      "5": {"class_type": "EmptySD3LatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}},
      "6": {"class_type": "KSampler", "inputs": {"seed": seed, "steps": 4, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0,
                                                  "model": ["1", 0], "positive": ["4", 0], "negative": ["4", 0], "latent_image": ["5", 0]}},
      "7": {"class_type": "VAEDecode", "inputs": {"samples": ["6", 0], "vae": ["3", 0]}},
      "8": {"class_type": "SaveImage", "inputs": {"images": ["7", 0], "filename_prefix": "coffee_sheet"}},
    }

def render(slot, seed, w=1536, h=768):
    prompt = SHEET + SLOTS[slot] + ". Style: " + STYLE
    pid = post("/prompt", {"prompt": workflow(prompt, w, h, seed)})["prompt_id"]
    while True:
        hist = json.loads(urllib.request.urlopen(HOST + "/history/" + pid).read())
        if pid in hist:
            info = hist[pid]["outputs"]["8"]["images"][0]
            data = urllib.request.urlopen(f"{HOST}/view?filename={info['filename']}&subfolder={info['subfolder']}&type={info['type']}").read()
            out = os.path.join(RAW, f"sheet_{slot}_{seed}.png")
            open(out, "wb").write(data)
            print(out, flush=True)
            return
        time.sleep(3)

if __name__ == "__main__":
    slot = sys.argv[1]
    for s in (sys.argv[2] if len(sys.argv) > 2 else "1").split(","):
        render(slot, int(s))
