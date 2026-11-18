"""COFFEE-MAN pickups and props in the Botero style (Flux schnell, local ComfyUI, free).
  COMFY_HOST=http://127.0.0.1:8189 python art/comfy/gen_items.py [name[,name]] [seeds]
Raw images land in art/incoming/item_<name>_<seed>.png; art/comfy/cut_items.py cuts them into the theme folder."""
import sys, os, json, time, urllib.request
sys.path.insert(0, os.path.dirname(__file__))
import gen_flux as g

STYLE = ("painted by Fernando Botero, Colombian Boterismo still life: the object is inflated, plump, round and voluminous with smooth rounded forms, "
         "flat matte oil paint on canvas, soft warm saturated colours, soft shading, no hard outline, calm, not a 3D render, no CGI, no text")
ITEMS = {
  "bean": "two plump round shiny-matte red coffee cherries joined on a short stem with one small round green leaf",
  "mug": "a fat round white clay coffee mug filled with dark coffee, small rounded handle, a little soft steam",
  "fruit_coffeecherry": "a bunch of three huge plump red coffee cherries with two round green leaves",
  "fruit_banana": "a single huge plump ripe yellow banana, round and swollen",
  "fruit_arepa": "a plump round golden arepa with a melting pat of butter",
  "fruit_panela": "a chubby rounded block of brown golden panela",
  "fruit_tinto": "a small fat round cup of black coffee tinto, steaming, no saucer",
  "fruit_sack": "a fat round burlap fique sack of coffee beans tied at the top with rope, a red coffee cherry painted on it",
  "bush": "a single plump round coffee shrub standing on a small mound of brown earth, a short thick trunk, big smooth rounded voluminous glossy-matte green leaf masses like soft clouds, many round red coffee cherries, small tufts of grass at the foot, side view, wider than tall",
  "fruit_sombrero": "a plump round white sombrero aguadeno straw hat with a black band",
}
def render(name, seed):
    prompt = "A single object centered on a plain flat pure white background, nothing else: " + ITEMS[name] + ". Style: " + STYLE
    pid = g.post("/prompt", {"prompt": g.workflow(prompt, 1024, 1024, seed)})["prompt_id"]
    while True:
        hist = json.loads(urllib.request.urlopen(g.HOST + "/history/" + pid).read())
        if pid in hist:
            i = hist[pid]["outputs"]["8"]["images"][0]
            data = urllib.request.urlopen(f"{g.HOST}/view?filename={i['filename']}&subfolder={i['subfolder']}&type={i['type']}").read()
            out = os.path.join(g.RAW, f"item_{name}_{seed}.png"); open(out, "wb").write(data); print(out, flush=True); return
        time.sleep(3)
if __name__ == "__main__":
    names = sys.argv[1].split(",") if len(sys.argv) > 1 and sys.argv[1] != "all" else list(ITEMS)
    for s in (sys.argv[2] if len(sys.argv) > 2 else "301").split(","):
        for n in names: render(n, int(s))
