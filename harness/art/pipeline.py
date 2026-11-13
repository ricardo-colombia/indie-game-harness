"""Asset pipeline driven by art/manifest.yaml.

  python harness/art/pipeline.py status
  python harness/art/pipeline.py concept <asset>     # Magnific text-to-image, N variants   (paid)
  python harness/art/pipeline.py model <asset>       # Tripo image-to-model from `picked`  (paid)
  python harness/art/pipeline.py cleanup <asset>     # Blender headless cleanup + export   (free)
  python harness/art/pipeline.py texture <asset>     # Magnific tileable texture + upscale (paid)
Add --dry-run to print requests without calling paid APIs.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

import requests
import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from core import ROOT, find_blender, ledger_add, load_config  # noqa: E402

MANIFEST = ROOT / "art" / "manifest.yaml"
STYLE = ROOT / "art" / "style.md"


def poll(get, ok_states, fail_states, every=5, timeout=900):
    t0 = time.time()
    while time.time() - t0 < timeout:
        d = get()
        st = str(d.get("status", "")).lower()
        if st in ok_states:
            return d
        if st in fail_states:
            raise RuntimeError(f"task failed: {json.dumps(d)[:500]}")
        time.sleep(every)
    raise TimeoutError("task polling timed out")


def download(url: str, dest: Path) -> Path:
    dest.parent.mkdir(parents=True, exist_ok=True)
    with requests.get(url, stream=True, timeout=120) as r:
        r.raise_for_status()
        with dest.open("wb") as f:
            for chunk in r.iter_content(1 << 16):
                f.write(chunk)
    return dest


class Magnific:
    def __init__(self, c, dry):
        self.c, self.dry = c, dry
        self.h = {"x-magnific-api-key": os.environ.get(c["api_key_env"], ""), "Content-Type": "application/json"}

    def _task(self, endpoint, body):
        if self.dry:
            print("DRY POST", endpoint, json.dumps(body)[:300]); return []
        r = requests.post(self.c["base_url"] + endpoint, headers=self.h, json=body, timeout=60)
        r.raise_for_status()
        tid = r.json()["data"]["task_id"]
        d = poll(lambda: requests.get(f"{self.c['base_url']}{endpoint}/{tid}", headers=self.h, timeout=30).json()["data"],
                 {"completed"}, {"failed", "error"})
        return d.get("generated", [])

    def generate(self, prompt, out: Path, n: int, aspect="square_1_1"):
        files = []
        for i in range(n):
            for j, url in enumerate(self._task(self.c["generate_endpoint"], {"prompt": prompt, "aspect_ratio": aspect})):
                files.append(download(url, out / f"v{i + 1:02d}_{j}.png"))
            if not self.dry:
                ledger_add("art", self.c.get("est_usd_per_image", 0), billed="api", api="magnific", op="generate")
        return files

    def upscale(self, src: Path, dest: Path, scale=2):
        body = {"image": base64.b64encode(src.read_bytes()).decode(), "scale_factor": scale}
        urls = self._task(self.c["upscale_endpoint"], body)
        if not self.dry:
            ledger_add("art", self.c.get("est_usd_per_upscale", 0), billed="api", api="magnific", op="upscale")
        return download(urls[0], dest) if urls else None


class ComfyUI:
    """Free, local generation (your GPU). Workflow = ComfyUI 'API format' JSON exported from the UI, with
    placeholders {{PROMPT}} / {{IMAGE}} / {{SEED}}. Works for text-to-image (e.g. SDXL/Flux) and
    image-to-3D (e.g. a Hunyuan3D or TripoSR workflow) alike: outputs are whatever the workflow saves."""

    def __init__(self, c, dry):
        self.url, self.dry = c.get("url", "http://127.0.0.1:8188"), dry

    def run(self, workflow: Path, out: Path, prompt="", image: Path | None = None, n=1):
        files = []
        for i in range(n):
            text = workflow.read_text().replace("{{PROMPT}}", json.dumps(prompt)[1:-1]).replace("{{SEED}}", str(1000 + i))
            if image is not None:
                if self.dry:
                    name = image.name
                else:
                    up = requests.post(f"{self.url}/upload/image", files={"image": image.open("rb")}, timeout=60)
                    up.raise_for_status()
                    name = up.json()["name"]
                text = text.replace("{{IMAGE}}", name)
            if self.dry:
                print("DRY comfyui", workflow.name, prompt[:80]); continue
            pid = requests.post(f"{self.url}/prompt", json={"prompt": json.loads(text)}, timeout=30).json()["prompt_id"]
            t0 = time.time()
            while time.time() - t0 < 1800:
                h = requests.get(f"{self.url}/history/{pid}", timeout=30).json()
                if pid in h:
                    break
                time.sleep(3)
            for node in h[pid]["outputs"].values():
                for kind in ("images", "gifs", "meshes", "3d", "files"):
                    for f in node.get(kind, []):
                        q = {"filename": f["filename"], "subfolder": f.get("subfolder", ""), "type": f.get("type", "output")}
                        r = requests.get(f"{self.url}/view", params=q, timeout=120)
                        dest = out / f"v{i + 1:02d}_{f['filename']}"
                        dest.parent.mkdir(parents=True, exist_ok=True)
                        dest.write_bytes(r.content)
                        files.append(dest)
        return files


class Tripo:
    def __init__(self, c, dry):
        self.c, self.dry = c, dry
        self.h = {"Authorization": f"Bearer {os.environ.get(c['api_key_env'], '')}"}

    def image_to_model(self, image: Path, dest: Path, face_limit: int, extra: dict):
        body = {"face_limit": face_limit, "texture": self.c["texture"], "pbr": self.c["pbr"], **(extra or {})}
        if self.dry:
            print("DRY tripo image-to-model", image, body); return None
        up = requests.post(f"{self.c['base_url']}/files", headers=self.h, files={"file": image.open("rb")}, timeout=120)
        up.raise_for_status()
        ud = up.json()["data"]
        body["file_token"] = ud.get("file_token") or ud.get("image_token") or ud.get("token")
        r = requests.post(f"{self.c['base_url']}/generation/image-to-model", headers=self.h, json=body, timeout=60)
        r.raise_for_status()
        tid = r.json()["data"]["task_id"]
        d = poll(lambda: requests.get(f"{self.c['base_url']}/tasks/{tid}", headers=self.h, timeout=30).json()["data"],
                 {"success"}, {"failed", "cancelled"}, every=8)
        out = d.get("output", {})
        url = out.get("pbr_model_url") or out.get("model_url") or out.get("pbr_model") or out.get("model")
        ledger_add("art", self.c.get("est_usd_per_model", 0), billed="api", api="tripo", op="image_to_model", task=tid)
        return download(url, dest)  # URLs expire within minutes: download now


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("stage", choices=["status", "concept", "model", "cleanup", "texture"])
    ap.add_argument("asset", nargs="?")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    cfg = load_config()["art"]
    man = yaml.safe_load(MANIFEST.read_text()) or {}
    assets = man.get("assets", {})
    if a.stage == "status":
        for k, v in assets.items():
            print(f"{k:24} {v.get('stage'):8} picked={v.get('picked')}")
        return
    A = assets[a.asset]
    base = ROOT / "art"
    style = STYLE.read_text().split("## Prompt prefix", 1)[-1].split("##", 1)[0].strip() if STYLE.exists() else ""

    prov = {**cfg.get("providers", {}), **A.get("providers", {})}
    if a.stage == "concept":
        out = base / "concepts" / a.asset
        prompt, n = f"{style} {A['prompt']}".strip(), int(A.get("variants", 4))
        p = prov.get("concept", "manual")
        if p == "magnific":
            Magnific(cfg["magnific"], a.dry_run).generate(prompt, out, n)
        elif p == "comfyui":
            ComfyUI(cfg["comfyui"], a.dry_run).run(ROOT / cfg["comfyui"]["concept_workflow"], out, prompt, n=n)
        else:  # manual: $0 — any free tool, a sketch, or a photo
            out.mkdir(parents=True, exist_ok=True)
            (out / "PROMPT.txt").write_text(prompt + "\n")
            print(f"manual: put concept images in {out} (prompt saved to PROMPT.txt), then set `picked:`.")
        A["stage"] = "pick"
        print(f"Concepts in {out}. Director: set `picked:` in art/manifest.yaml.")
    elif a.stage == "model":
        if not A.get("picked"):
            raise SystemExit("No `picked` concept yet — this is the director's call.")
        dest = base / "models_raw" / f"{a.asset}.glb"
        src = base / "concepts" / a.asset / A["picked"]
        p = prov.get("model", "manual")
        if p == "tripo":
            Tripo(cfg["tripo"], a.dry_run).image_to_model(src, dest, int(A.get("face_limit", cfg["tripo"]["face_limit"])),
                                                         A.get("tripo", {}))
        elif p == "comfyui":
            got = ComfyUI(cfg["comfyui"], a.dry_run).run(ROOT / cfg["comfyui"]["model_workflow"], dest.parent / a.asset, image=src)
            meshes = [g for g in got if g.suffix.lower() in (".glb", ".gltf", ".obj", ".fbx")]
            if meshes:
                shutil.copy(meshes[0], dest.with_suffix(meshes[0].suffix))
        elif not any(dest.with_suffix(x).exists() for x in (".glb", ".fbx", ".obj")):
            raise SystemExit(f"manual: drop the model as {dest} (e.g. from a free web tier, Blender, or an asset pack), then rerun.")
        A["stage"] = "cleanup"
    elif a.stage == "cleanup":
        game = ROOT / load_config()["game"]["unity_project"]
        out = game / cfg["import_dir"] / a.asset / f"{a.asset}.fbx"
        raw = next((base / "models_raw" / f"{a.asset}{x}" for x in (".glb", ".fbx", ".obj")
                    if (base / "models_raw" / f"{a.asset}{x}").exists()), base / "models_raw" / f"{a.asset}.glb")
        cmd = [find_blender({"art": cfg}) or cfg["blender"]["path"], "-b", "--factory-startup", "-P", str(Path(__file__).parent / "blender_cleanup.py"), "--",
               "--in", str(raw), "--out", str(out), "--name", a.asset,
               "--height", str(A.get("height_m", 1.0)), "--faces", str(A.get("face_limit", 8000)),
               "--split", A.get("split", "none")]
        print(" ".join(cmd))
        if not a.dry_run:
            subprocess.run(cmd, check=True)
            A["stage"] = "import"
    elif a.stage == "texture":
        if prov.get("texture", "manual") != "magnific":
            raise SystemExit("texture: free path = ComfyUI concept workflow with a 'seamless texture' prompt, "
                             "or CC0 libraries (ambientCG, Poly Haven). Set providers.texture: magnific to pay.")
        m = Magnific(cfg["magnific"], a.dry_run)
        out = base / "textures" / a.asset
        files = m.generate(f"{style} seamless tileable texture, flat top-down, even lighting, {A['prompt']}".strip(), out,
                           int(A.get("variants", 2)))
        for f in files:
            m.upscale(f, f.with_name(f.stem + "_x2.png"))
        A["stage"] = "pick"
    if not a.dry_run:
        MANIFEST.write_text(yaml.safe_dump(man, sort_keys=False))


if __name__ == "__main__":
    main()
