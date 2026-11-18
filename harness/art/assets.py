"""Asset registry: lifecycle, validation report, approval lock. Reads art/manifest.yaml.

  hx assets status                         one line per asset: kind, version, stage -> lifecycle state, approved
  hx assets check [asset] [--no-blender]   validate (schema, files, dependencies, animation spec, locks); exit 1 on failures
  hx assets export <asset> rig.blend [--map Act=Clip]   Blender: model + one <asset>@<Clip>.fbx per action, spec-checked names
  hx assets approve <asset> --by NAME      DIRECTOR ONLY: lock the asset's `outputs` (sha256 in art/locks.json)
  hx assets new-version <asset>            DIRECTOR ONLY: archive the approved files, bump `version`, unlock for rework

Lifecycle (prompt-style names -> manifest `stage`):
  requested/generating = concept, model | art review = pick | technical validation = cleanup
  | unity integration = import | qa = done (not yet approved) | approved = `approved: true`
Approved files are never overwritten: pipeline.py refuses, hooks/guard_assets.py blocks edits, `check` fails on any change.
A check that could not run (no Blender) is reported as `skipped`, never as passed.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from core import REPORTS, ROOT, find_blender, load_config  # noqa: E402

MANIFEST = ROOT / "art" / "manifest.yaml"
SCHEMA = ROOT / "art" / "asset.schema.json"
SPEC = ROOT / "art" / "animation_spec.yaml"
LOCKS = ROOT / "art" / "locks.json"
STAGES = ["concept", "pick", "model", "cleanup", "import", "done"]
LIFECYCLE = {"concept": "generating", "pick": "art_review", "model": "generating", "cleanup": "tech_validation",
             "import": "unity_integration", "done": "qa"}
ID_RE = re.compile(r"^[a-z][a-z0-9_]*$")
ANIMATED = ("character", "enemy", "npc")


# ---------------------------------------------------------------- manifest + locks io
def load_manifest() -> dict:
    return yaml.safe_load(MANIFEST.read_text()) or {}


def save_manifest(man: dict) -> None:
    """safe_dump drops comments; keep the file's leading comment block (the how-to-read header)."""
    head = []
    for line in MANIFEST.read_text().splitlines() if MANIFEST.exists() else []:
        if not line.startswith("#"):
            break
        head.append(line)
    MANIFEST.write_text("\n".join(head) + ("\n" if head else "") + yaml.safe_dump(man, sort_keys=False))


def load_locks() -> dict:
    return json.loads(LOCKS.read_text()) if LOCKS.exists() else {}


def save_locks(locks: dict) -> None:
    LOCKS.write_text(json.dumps(locks, indent=1, sort_keys=True) + "\n")


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with p.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# ---------------------------------------------------------------- tiny JSON-schema subset (no dependency)
def schema_errors(v, s: dict, path: str = "") -> list[str]:
    errs = []
    types = {"string": str, "integer": int, "number": (int, float), "boolean": bool, "object": dict, "array": list, "null": type(None)}
    t = s.get("type")
    if t:
        ok = any(isinstance(v, types[x]) and not (x in ("integer", "number") and isinstance(v, bool))
                 for x in ([t] if isinstance(t, str) else t))
        if not ok:
            return [f"{path or 'value'}: expected {t}, got {type(v).__name__}"]
    if "enum" in s and v not in s["enum"]:
        errs.append(f"{path}: {v!r} not one of {s['enum']}")
    if isinstance(v, str) and "pattern" in s and not re.search(s["pattern"], v):
        errs.append(f"{path}: {v!r} does not match {s['pattern']}")
    if isinstance(v, (int, float)) and not isinstance(v, bool):
        if "minimum" in s and v < s["minimum"]:
            errs.append(f"{path}: {v} < {s['minimum']}")
        if "exclusiveMinimum" in s and v <= s["exclusiveMinimum"]:
            errs.append(f"{path}: {v} <= {s['exclusiveMinimum']}")
    if isinstance(v, dict):
        errs += [f"{path}.{k}: required" for k in s.get("required", []) if k not in v]
        for k, x in v.items():
            if k in s.get("properties", {}):
                errs += schema_errors(x, s["properties"][k], f"{path}.{k}")
            elif isinstance(s.get("additionalProperties"), dict):
                errs += schema_errors(x, s["additionalProperties"], f"{path}.{k}")
    if isinstance(v, list) and "items" in s:
        for i, x in enumerate(v):
            errs += schema_errors(x, s["items"], f"{path}[{i}]")
    return errs


# ---------------------------------------------------------------- report
class Report:
    def __init__(self):
        self.passed, self.failed, self.warnings, self.skipped, self.fixes = [], [], [], [], []

    def ok(self, msg): self.passed.append(msg)
    def fail(self, msg, fix=None):
        self.failed.append(msg)
        if fix: self.fixes.append(fix)
    def warn(self, msg, fix=None):
        self.warnings.append(msg)
        if fix: self.fixes.append(fix)
    def skip(self, msg): self.skipped.append(msg)

    def asdict(self) -> dict:
        return {k: getattr(self, k) for k in ("passed", "failed", "warnings", "skipped", "fixes")}


# ---------------------------------------------------------------- animation judging (pure: testable without Blender)
def judge_animation(m: dict, spec: dict, clip: str | None, rep: Report, tag: str, height_m: float | None = None) -> None:
    """m = measurements from validate_anim.py for one file; clip = clip name this file should hold (None = the model)."""
    if m.get("error"):
        rep.fail(f"{tag}: could not read file ({m['error']})"); return
    n0 = len(rep.failed)
    if m["armatures"] != 1:
        rep.fail(f"{tag}: {m['armatures']} armatures (need exactly 1)", "keep one armature; join/remove the others before export")
    elif m["root_bones"] != ["root"]:
        rep.fail(f"{tag}: root bone(s) {m['root_bones']} (need exactly `root`)", "rename the single top bone to `root`; parent every other bone under it")
    if m["bones"] > spec.get("max_bones", 75):
        rep.fail(f"{tag}: {m['bones']} bones > budget {spec.get('max_bones', 75)}", "merge twist/helper bones or raise max_bones with a measured reason")
    if m["max_influences"] > 4:
        rep.fail(f"{tag}: up to {m['max_influences']} bone influences per vertex (max 4)", "Weights > Limit Total 4, then Normalize All")
    if clip is None and height_m and m.get("height_m") and abs(m["height_m"] - height_m) / height_m > 0.05:
        rep.fail(f"{tag}: model is {m['height_m']} m tall, manifest says {height_m} m (1 unit = 1 m)",
                 "apply scale before rigging; export with FBX_SCALE_ALL (export_anim.py does)")
    if clip is None:
        if len(rep.failed) == n0:
            rep.ok(f"{tag}: skeleton ok ({m['bones']} bones)")
        return
    loop = spec["clips"][clip]["loop"]
    act = m["actions"][0] if m["actions"] else None
    if not act:
        rep.fail(f"{tag}: no animation inside the file", "export the action, not just the mesh"); return
    if act["frames"] < 2:
        rep.fail(f"{tag}: clip is {act['frames']} frame(s) long")
    if abs(m["fps"] - spec.get("fps", 30)) > 0.01:
        rep.warn(f"{tag}: scene fps {m['fps']} != {spec.get('fps', 30)}", "set scene fps before exporting")
    seam = act.get("seam_m")
    if loop and seam is not None and seam > spec.get("seam_tolerance_m", 0.01):
        rep.fail(f"{tag}: loop does not close, bone `{act['seam_bone']}` jumps {seam:.3f} m between last and first frame",
                 f"make the last frame equal the first (copy frame 0 to the end) in {clip}")
    if len(rep.failed) == n0:
        rep.ok(f"{tag}: ok ({act['frames']} frames{', loop closes' if loop else ''})")


def measure(files: list[Path], blender: str) -> dict[str, dict]:
    cmd = [blender, "-b", "--factory-startup", "-P", str(Path(__file__).parent / "validate_anim.py"), "--", *map(str, files)]
    out = subprocess.run(cmd, capture_output=True, text=True, timeout=300).stdout
    res = {}
    for line in out.splitlines():
        if line.startswith("HARNESS_ANIM "):
            d = json.loads(line.split(" ", 1)[1])
            res[d["file"]] = d
    return res


# ---------------------------------------------------------------- checks
def check_asset(name: str, A: dict, assets: dict, schema: dict, spec: dict, cfg: dict, use_blender: bool, locks: dict) -> Report:
    rep = Report()
    errs = schema_errors(A, schema, name)
    for e in errs:
        rep.fail(e, "see art/asset.schema.json")
    if not ID_RE.match(name):
        rep.fail(f"{name}: id must be lowercase snake_case", "rename the manifest key")
    if not errs:
        rep.ok("schema")
    stage = A.get("stage", "concept")
    late = STAGES.index(stage) >= STAGES.index("import") if stage in STAGES else False

    for d in A.get("depends_on", []):
        if d == name or d not in assets:
            rep.fail(f"depends_on `{d}` is not another asset in the manifest")
    if stage in ("model", "cleanup", "import", "done") and not A.get("picked"):
        rep.fail("advanced past `pick` without a director pick", "director: set `picked:` (agents never pick)")
    elif A.get("picked") and not (ROOT / "art/concepts" / name / A["picked"]).exists():
        rep.warn(f"picked concept art/concepts/{name}/{A['picked']} not on disk")

    outs = A.get("outputs", [])
    for o in outs:
        p = ROOT / o
        if not p.exists():
            (rep.fail if late else rep.warn)(f"output missing: {o}", "run the pipeline cleanup/import stage")
        elif "/Assets/" in o.replace("\\", "/") and not Path(str(p) + ".meta").exists():
            rep.warn(f"{o} has no .meta yet", "open the Unity editor once so it imports, then commit the .meta")
        if p.exists() and not p.name.startswith(name):
            rep.warn(f"{o}: file name does not start with the asset id `{name}`")
    if late and not outs:
        rep.fail("stage is import/done but `outputs` is empty", "list the delivered files in the manifest")

    if A.get("kind") in ANIMATED:
        prof = A.get("animation_profile")
        profile = spec.get("profiles", {}).get(prof or "")
        if not prof:
            rep.warn("animated kind without `animation_profile`", "set one of: " + ", ".join(spec.get("profiles", {})))
        elif not profile:
            rep.fail(f"unknown animation_profile `{prof}`")
        else:
            have = set(A.get("animations", []))
            for c in sorted(have - set(spec["clips"])):
                rep.fail(f"clip `{c}` is not in art/animation_spec.yaml", "use the spec's names or add the clip to the spec")
            for c in sorted(set(profile["required"]) - have):
                (rep.fail if late else rep.warn)(f"required clip `{c}` missing for profile {prof}", f"deliver {name}@{c}.fbx")
            if not (set(profile["required"]) - have):
                rep.ok(f"all {len(profile['required'])} required clips listed")
            base = (ROOT / outs[0]).parent if outs else None
            todo = {}
            if base:
                model = ROOT / outs[0]
                if model.exists():
                    todo[model] = None
                for c in sorted(have & set(spec["clips"])):
                    f = base / f"{name}@{c}.fbx"
                    if f.exists():
                        todo[f] = c
                    else:
                        (rep.fail if late else rep.warn)(f"clip file missing: {f.relative_to(ROOT)}")
            if todo and use_blender:
                exe = find_blender(cfg)
                if not exe:
                    rep.skip("skeleton/animation measurement: Blender not found (install it or set art.blender.path)")
                else:
                    got = measure(list(todo), exe)
                    for f, clip in todo.items():
                        m = got.get(str(f)) or {"error": "no measurement returned"}
                        judge_animation(m, spec, clip, rep, f.name, A.get("height_m"))
            elif todo:
                rep.skip("skeleton/animation measurement: --no-blender")

    if A.get("approved"):
        if not A.get("approved_by"):
            rep.fail("approved without `approved_by`")
        mine = locks.get(name, {})
        if not mine:
            rep.fail("approved but nothing is locked", "re-run `hx assets approve`")
        for rel, want in mine.items():
            p = ROOT / rel
            if not p.exists():
                rep.fail(f"approved file deleted: {rel}", f"restore it (git checkout) or `hx assets new-version {name}`")
            elif sha256(p) != want:
                rep.fail(f"approved file changed: {rel}", f"git checkout -- {rel}, or `hx assets new-version {name}` to rework it on purpose")
        if mine and not rep.failed:
            rep.ok(f"approved v{A.get('version', 1)}: {len(mine)} locked file(s) intact")
    return rep


def run_check(only: str | None, use_blender: bool) -> tuple[bool, dict]:
    man, schema = load_manifest(), json.loads(SCHEMA.read_text())
    spec = yaml.safe_load(SPEC.read_text()) if SPEC.exists() else {"clips": {}, "profiles": {}}
    cfg, locks = load_config(), load_locks()
    assets = man.get("assets") or {}
    if only and only not in assets:
        return False, {"error": f"no asset `{only}` in art/manifest.yaml"}
    full, bad = {}, {}
    for n, A in assets.items():
        if only and n != only:
            continue
        r = check_asset(n, A, assets, schema, spec, cfg, use_blender, locks)
        full[n] = r.asdict()
        if r.failed:
            bad[n] = {"failed": r.failed, "fixes": r.fixes}
    out = REPORTS / "assets"
    out.mkdir(parents=True, exist_ok=True)
    (out / "latest.json").write_text(json.dumps({"at": dt.datetime.now().isoformat(), "assets": full}, indent=1))
    warn = {n: r["warnings"] for n, r in full.items() if r["warnings"]}
    skip = sorted({s for r in full.values() for s in r["skipped"]})
    summary = {"checked": len(full), "passed": sum(len(r["passed"]) for r in full.values())}
    if warn: summary["warnings"] = warn
    if skip: summary["skipped"] = skip
    if bad: summary["failed"] = bad
    summary["report"] = str((out / "latest.json").relative_to(ROOT))
    return not bad, summary


# ---------------------------------------------------------------- director-only actions
def _director_only():
    if os.environ.get("CLAUDECODE"):
        raise SystemExit("Only the director approves or reopens assets (run this in your own terminal).")


def approve(name: str, by: str):
    _director_only()
    man = load_manifest()
    A = (man.get("assets") or {}).get(name) or sys.exit(f"no asset `{name}`")
    ok, summ = run_check(name, use_blender=True)
    if not ok:
        raise SystemExit("not approvable yet:\n" + json.dumps(summ["failed"], indent=1))
    if not A.get("outputs"):
        raise SystemExit("nothing to lock: list the delivered files in `outputs`")
    locks = load_locks()
    locks[name] = {o: sha256(ROOT / o) for o in A["outputs"]}
    A.update(approved=True, approved_by=by, approved_at=dt.datetime.now().isoformat(timespec="seconds"), stage="done")
    save_locks(locks); save_manifest(man)
    print(f"{name} v{A.get('version', 1)} approved by {by}; {len(locks[name])} file(s) locked in art/locks.json")


def new_version(name: str):
    _director_only()
    man = load_manifest()
    A = (man.get("assets") or {}).get(name) or sys.exit(f"no asset `{name}`")
    locks, v = load_locks(), A.get("version", 1)
    for rel in locks.pop(name, {}):
        src = ROOT / rel
        if src.exists():
            dst = ROOT / "art/archive" / name / f"v{v}" / Path(rel).name
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
    for k in ("approved", "approved_by", "approved_at"):
        A.pop(k, None)
    A.update(version=v + 1, stage="cleanup")
    save_locks(locks); save_manifest(man)
    print(f"{name}: v{v} archived in art/archive/{name}/v{v}/; now v{v + 1} (stage cleanup), unlocked")


def export(name: str, blend: str, mapping: list[str]):
    man = load_manifest()
    A = (man.get("assets") or {}).get(name) or sys.exit(f"no asset `{name}`")
    if A.get("approved"):
        sys.exit(f"{name} is approved and locked; the director runs `hx assets new-version {name}` first")
    spec, cfg = yaml.safe_load(SPEC.read_text()), load_config()
    exe = find_blender(cfg) or sys.exit("Blender not found (install it or set art.blender.path)")
    out = ROOT / cfg["game"]["unity_project"] / cfg["art"]["import_dir"] / name
    cmd = [exe, "-b", str(ROOT / blend), "--factory-startup", "-P", str(Path(__file__).parent / "export_anim.py"), "--",
           "--asset", name, "--out", str(out), "--clips", ",".join(spec["clips"]), "--fps", str(spec.get("fps", 30))]
    for m in mapping:
        cmd += ["--map", m]
    r = subprocess.run(cmd, capture_output=True, text=True)
    line = next((l for l in r.stdout.splitlines() if l.startswith("HARNESS_EXPORT ")), None)
    if r.returncode or not line:
        sys.exit((r.stderr or r.stdout)[-800:])
    got = json.loads(line.split(" ", 1)[1])
    A.update(animations=sorted(got["clips"]), outputs=[str((out / f"{name}.fbx").relative_to(ROOT))],
             stage=A["stage"] if STAGES.index(A["stage"]) > STAGES.index("cleanup") else "import")
    save_manifest(man)
    print(json.dumps({"ok": True, **got, "next": f"hx assets check {name}"}))


def status():
    for n, A in (load_manifest().get("assets") or {}).items():
        state = "approved" if A.get("approved") else LIFECYCLE.get(A.get("stage"), "?")
        print(f"{n:24} {A.get('kind', '-'):11} v{A.get('version', 1)}  stage={A.get('stage'):8} -> {state}")


def main():
    ap = argparse.ArgumentParser(prog="hx assets")
    ap.add_argument("action", choices=["status", "check", "export", "approve", "new-version"])
    ap.add_argument("asset", nargs="?")
    ap.add_argument("blend", nargs="?", help="export: the rigged .blend, relative to the repo root")
    ap.add_argument("--map", action="append", default=[], help="export: Action.Name=Clip")
    ap.add_argument("--by", default="director")
    ap.add_argument("--no-blender", action="store_true")
    a = ap.parse_args()
    if a.action == "status":
        return status()
    if a.action in ("export", "approve", "new-version") and not a.asset:
        ap.error("asset id required")
    if a.action == "export":
        return export(a.asset, a.blend or ap.error("export needs the rigged .blend"), a.map)
    if a.action == "approve":
        return approve(a.asset, a.by)
    if a.action == "new-version":
        return new_version(a.asset)
    ok, summ = run_check(a.asset, not a.no_blender)
    print(json.dumps({"ok": ok, **summ}, **({"separators": (",", ":")} if ok else {"indent": 1})))
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
