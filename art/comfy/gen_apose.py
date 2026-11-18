"""One character in a rig-friendly A-pose (arms out, legs apart, nothing overlapping the body) so it can be cut into
separate parts (head, torso, arms, legs...) for a real bone rig. Same descriptions as gen_flux.SLOTS.
  COMFY_HOST=http://127.0.0.1:8189 python art/comfy/gen_apose.py <slot> <view:front|back|side> [seeds]
Output: art/incoming/apose_<slot>_<view>_<seed>.png"""
import sys, os, json, time, urllib.request
sys.path.insert(0, os.path.dirname(__file__))
import gen_flux as g

from anim_brief import POSE, VIEW   # the animation brief decides what is asked for
def render(slot, view, seed):
    desc = g.SLOTS[slot].replace(", a thick folder under one arm", ", empty hands").replace(", a red megaphone in one hand, no weapon", ", empty hands, no weapon")
    prompt = POSE + VIEW[view] + ". " + desc + ". Style: " + g.STYLE
    pid = g.post("/prompt", {"prompt": g.workflow(prompt, 896, 1152, seed)})["prompt_id"]
    while True:
        hist = json.loads(urllib.request.urlopen(g.HOST + "/history/" + pid).read())
        if pid in hist:
            i = hist[pid]["outputs"]["8"]["images"][0]
            data = urllib.request.urlopen(f"{g.HOST}/view?filename={i['filename']}&subfolder={i['subfolder']}&type={i['type']}").read()
            out = os.path.join(g.RAW, f"apose_{slot}_{view}_{seed}.png"); open(out, "wb").write(data); print(out, flush=True); return
        time.sleep(3)
if __name__ == "__main__":
    for s in (sys.argv[3] if len(sys.argv) > 3 else "501").split(","): render(sys.argv[1], sys.argv[2], int(s))
