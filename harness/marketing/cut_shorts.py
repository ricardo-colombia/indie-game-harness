"""Cut PNG-sequence camera takes into shorts with ffmpeg, then queue them (unapproved).

  python harness/marketing/cut_shorts.py marketing/shorts.yaml [--only <id>]
"""
from __future__ import annotations

import argparse
import functools
import platform
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from core import ROOT  # noqa: E402

PROFILES = {"vertical": (1080, 1920), "wide": (1920, 1080), "square": (1080, 1080)}


def ff(*args):
    r = subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *args], capture_output=True, text=True)
    if r.returncode:
        raise SystemExit(f"ffmpeg failed ({r.returncode}): {r.stderr.strip()[-800:]}")


@functools.lru_cache(maxsize=None)
def has_filter(name: str) -> bool:
    """Some ffmpeg builds (e.g. Homebrew's) ship without drawtext (needs libfreetype)."""
    out = subprocess.run(["ffmpeg", "-hide_banner", "-filters"], capture_output=True, text=True).stdout
    return any(line.split()[1:2] == [name] for line in out.splitlines() if line.strip())


def default_fontfile() -> str | None:
    """drawtext's `font=` needs fontconfig; a real file path works everywhere."""
    cands = {"Darwin": ["/System/Library/Fonts/Supplemental/Arial Bold.ttf", "/System/Library/Fonts/Supplemental/Arial.ttf",
                        "/Library/Fonts/Arial.ttf"],
             "Windows": ["C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf"],
             "Linux": ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"]}
    return next((f for f in cands.get(platform.system(), []) if Path(f).exists()), None)


def esc(path) -> str:
    """Path for a single-quoted ffmpeg filter value: inside '...' ':' is literal; use forward slashes."""
    return str(path).replace("\\", "/").replace("'", "")


def clip(c, fps, w, h, dest: Path):
    src = ROOT / "marketing" / "out" / "raw" / c["set"] / c["take"]
    start = int(float(c.get("in", 0)) * fps) + 1
    frames = int((float(c["out"]) - float(c.get("in", 0))) * fps) if "out" in c else None
    vf = f"scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1"
    ff("-framerate", str(fps), "-start_number", str(start), "-i", str(src / "f_%05d.png"),
       *(["-frames:v", str(frames)] if frames else []), "-vf", vf,
       "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p", str(dest))


def build_short(s, spec, tmp: Path) -> tuple[Path, bool]:
    """Returns (file, caption_burned)."""
    fps = int(spec.get("fps", 30))
    w, h = PROFILES[s.get("profile", spec.get("profile", "vertical"))]
    parts = []
    for i, c in enumerate(s["clips"]):
        p = tmp / f"{s['id']}_{i}.mp4"
        clip(c, fps, w, h, p)
        parts.append(p)
    lst = tmp / f"{s['id']}.txt"
    lst.write_text("".join(f"file '{p}'\n" for p in parts))
    joined = tmp / f"{s['id']}_joined.mp4"
    ff("-f", "concat", "-safe", "0", "-i", str(lst), "-c", "copy", str(joined))

    out = ROOT / spec.get("out_dir", "marketing/out") / f"{s['id']}.mp4"
    out.parent.mkdir(parents=True, exist_ok=True)
    vf, burned = [], False
    if s.get("caption") and not has_filter("drawtext"):
        print(f"warning: this ffmpeg has no drawtext filter — '{s['id']}' is cut WITHOUT its caption. "
              "Install an ffmpeg built with libfreetype (macOS: `brew tap homebrew-ffmpeg/ffmpeg && "
              "brew install homebrew-ffmpeg/ffmpeg/ffmpeg`), or add the caption on the platform.", file=sys.stderr)
    elif s.get("caption"):
        cap = tmp / f"{s['id']}_cap.txt"
        cap.write_text(s["caption"])
        font = spec.get("fontfile") or default_fontfile()
        fontarg = f"fontfile='{esc(font)}'" if font else "font=Sans"
        vf.append(f"drawtext={fontarg}:textfile='{esc(cap)}':fontsize={int(h * 0.045)}:fontcolor=white:"
                  f"borderw=6:bordercolor=black@0.7:x=(w-text_w)/2:y=h*0.12")
        burned = True
    dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(joined)],
                               capture_output=True, text=True).stdout.strip() or 0)
    args = ["-i", str(joined)]
    if s.get("music"):
        args += ["-i", str(ROOT / s["music"]), "-map", "0:v", "-map", "1:a", "-shortest",
                 "-af", f"afade=t=in:d=0.5,afade=t=out:st={max(dur - 1, 0):.2f}:d=1", "-c:a", "aac", "-b:a", "160k"]
    if vf:
        args += ["-vf", ",".join(vf)]
    ff(*args, "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(out))
    return out, burned


def enqueue(s, path: Path, schedule: Path, caption_burned: bool = True):
    data = yaml.safe_load(schedule.read_text()) if schedule.exists() else {}
    items = data.setdefault("items", [])
    if any(i["id"] == s["id"] for i in items):
        return
    items.append({"id": s["id"], "file": str(path.relative_to(ROOT)), "title": s.get("title", s.get("caption", s["id"])),
                  "description": s.get("description", ""), "tags": s.get("tags", []),
                  "platforms": s.get("platforms", data.get("default_platforms", ["youtube"])),
                  "approved": False, "status": "pending_approval", "slot": None,
                  **({} if caption_burned or not s.get("caption") else {"note": "caption NOT burned in (ffmpeg lacks drawtext)"})})
    schedule.write_text(yaml.safe_dump(data, sort_keys=False))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("spec")
    ap.add_argument("--only")
    a = ap.parse_args()
    if not shutil.which("ffmpeg"):
        raise SystemExit("ffmpeg not found")
    spec = yaml.safe_load((ROOT / a.spec).read_text())
    schedule = ROOT / spec.get("schedule", "marketing/schedule.yaml")
    with tempfile.TemporaryDirectory() as t:
        for s in spec["shorts"]:
            if a.only and s["id"] != a.only:
                continue
            out, burned = build_short(s, spec, Path(t))
            enqueue(s, out, schedule, burned)
            print(out.relative_to(ROOT))


if __name__ == "__main__":
    main()
