"""Headless Unity: build, EditMode/PlayMode tests, autotest the built player, capture camera takes.
All functions return (ok: bool, summary: dict) and write logs under `out`."""
from __future__ import annotations

import json
import os
import platform
import secrets
import shutil
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

from core import find_unity, player_exe, sh

HARNESS_MARKER = b"Studio.Harness"   # assembly name; present in any build that compiled the harness in


def _run(cfg, cmd, **kw):
    """Every Unity/player process runs code the agent wrote: run it through the sandbox prefix with secrets
    scrubbed from its environment."""
    sb = (cfg.get("runner") or {}).get("sandbox") or {}
    unset = list(sb.get("scrub_env") or []) + list(kw.pop("unset", []) or [])
    return sh(list(sb.get("prefix") or []) + cmd, unset=unset, **kw)


def _unity(cfg, project: Path | None = None) -> str:
    exe = find_unity(cfg, project)
    if not exe:
        raise RuntimeError("Unity editor not found: install the Unity CLI (unity.com/install.sh) and the project's "
                           "editor (`unity install <version>`), or set unity.editor_path in harness/config.yaml")
    return exe


def _display_wrap(cmd: list[str]) -> list[str]:
    """Players need a GPU context for screenshots; on headless Linux use xvfb-run."""
    if platform.system() == "Linux" and not os.environ.get("DISPLAY") and shutil.which("xvfb-run"):
        return ["xvfb-run", "-a", "-s", "-screen 0 1920x1080x24"] + cmd
    return cmd


def editor_open(project: Path) -> bool:
    """True if a Unity editor has this project open. A second (batch) Unity can't open a locked project."""
    if not shutil.which("unity"):
        return False
    try:
        r = subprocess.run(["unity", "status", "--format", "json", "--no-banner"], capture_output=True, text=True, timeout=20)
        items = (json.loads(r.stdout).get("data") or {}).get("instances") or []
    except (subprocess.SubprocessError, json.JSONDecodeError, OSError):
        return False
    here = project.resolve()
    return any(Path(i.get("projectPath", "")).resolve() == here for i in items if isinstance(i, dict) and i.get("projectPath"))


def _locked(project: Path, log: Path):
    """(ok, summary) failure if an editor holds the project, else None."""
    if editor_open(project):
        msg = (f"the Unity editor has {project.name}/ open, so a batch run can't use it. Close it (`unity close {project}`) "
               "or, to stay in the editor, use `unity command` / `unity test`.")
        return False, {"rc": 1, "error": msg, "log": str(log)}
    return None


def build(cfg, project: Path, out: Path, release: bool = False, binary: Path | None = None, target: str | None = None):
    """target: override the build target (e.g. "WebGL"); `binary` is then the output folder."""
    web = target == "WebGL"
    binary = binary or (project / (_release_binary(cfg) if release else cfg["game"]["player_binary"]))
    log = out / ("build-web.log" if web else "build-release.log" if release else "build.log")
    if (busy := _locked(project, log)):
        return busy
    r = _run(cfg, [_unity(cfg, project), "-batchmode", *([] if web else ["-nographics"]), "-quit", "-projectPath", str(project),
                   "-buildTarget", target or cfg["game"]["build_target"],
                   "-executeMethod", "Harness.Editor.HarnessBuild.BuildPlayer",
                   "-harnessBuildPath", str(binary), *(["-release"] if release else []), "-logFile", "-"],
             timeout=cfg["unity"]["build_timeout_min"] * 60, log=log)
    errors = [l for l in r.stdout.splitlines() if "error CS" in l][:20]
    ok = r.returncode == 0 and binary.exists() and (not web or (binary / "index.html").exists())
    summary = {"rc": r.returncode, "binary": str(binary), "compile_errors": errors, "log": str(log)}
    if ok and release:
        leaks = scan_for_harness(binary)
        summary["harness_leaks"] = leaks
        ok = not leaks
    return ok, summary


def build_web(cfg, project: Path, out: Path):
    """Shareable browser build: release (no harness), uncompressed so GitHub Pages and any static host can serve it."""
    return build(cfg, project, out, release=True, binary=project / "Builds/Web", target="WebGL")


def _release_binary(cfg) -> str:
    """Release goes to its own folder so it never overwrites (or gets confused with) the harness build."""
    b = Path(cfg["game"]["player_binary"])
    return str(Path("Builds/Release") / b.name)


def scan_for_harness(binary: Path) -> list[str]:
    """A release build must contain zero harness code. Mono: Studio.Harness.dll in *_Data/Managed.
    IL2CPP: the assembly name survives in global-metadata.dat / GameAssembly. Scan the whole build folder."""
    root = binary.parent if binary.is_file() else binary
    if binary.suffix == ".app" or (binary.is_dir() and binary.name.endswith(".app")):
        root = binary
    leaks = []
    for f in root.rglob("*"):
        if not f.is_file() or f.stat().st_size > 2_000_000_000:
            continue
        if f.name.startswith("Studio.Harness"):
            leaks.append(str(f.relative_to(root)))
            continue
        if f.suffix.lower() in (".dll", ".dat", ".so", ".dylib", ".json", ".wasm", ".data", "") or f.name.startswith("GameAssembly"):
            with f.open("rb") as fh:
                prev = b""
                while chunk := fh.read(1 << 20):
                    if HARNESS_MARKER in prev[-32:] + chunk:
                        leaks.append(str(f.relative_to(root)))
                        break
                    prev = chunk
    return leaks[:20]


def tests(cfg, project: Path, out: Path, platform_: str):
    results = out / f"tests-{platform_}.xml"
    log = out / f"tests-{platform_}.log"
    cmd = ([_unity(cfg, project), "-batchmode", "-projectPath", str(project), "-runTests",
            "-testPlatform", platform_, "-testResults", str(results), "-logFile", "-"]
           + (["-nographics"] if platform_ == "EditMode" else []))
    if (busy := _locked(project, log)):
        return busy
    if results.exists():
        results.unlink()
    r = _run(cfg, cmd if platform_ == "EditMode" else _display_wrap(cmd),   # PlayMode needs a display on Linux
             timeout=cfg["unity"]["test_timeout_min"] * 60, log=log)
    summary = {"rc": r.returncode, "log": str(log), "failed": []}
    if results.exists():
        root = ET.parse(results).getroot()
        summary.update({k: root.get(k) for k in ("total", "passed", "failed", "skipped")})
        summary["failed_tests"] = [t.get("fullname") for t in root.iter("test-case") if t.get("result") == "Failed"][:20]
    return r.returncode == 0 and results.exists(), summary


def _check(metrics: dict, expect: dict) -> list[str]:
    """expect: {"metric.stat": {"min": x, "max": y}} where stat in min|max|avg|last|p95|count."""
    fails = []
    for key, rule in (expect or {}).items():
        name, _, stat = key.rpartition(".")
        val = metrics.get(name, {}).get(stat) if name else None
        if val is None:
            fails.append(f"{key}: missing")
            continue
        if "min" in rule and val < rule["min"]:
            fails.append(f"{key}={val:.3f} < min {rule['min']}")
        if "max" in rule and val > rule["max"]:
            fails.append(f"{key}={val:.3f} > max {rule['max']}")
    return fails


def _min_seconds(spec: dict) -> float:
    """Lower bound on real time a scenario takes: sum of waits/holds divided by timeScale."""
    total = sum(float(st.get("duration", 0) or 0) for st in spec.get("steps", []) if st.get("action") in ("wait", "press", "set"))
    return total / max(float(spec.get("timeScale", 1) or 1), 0.01)


def autotest(cfg, project: Path, out: Path, scenarios: list[str], scenario_dir: Path | None = None):
    """scenario_dir: where the scenario JSONs are read from. The runner passes the MAIN checkout's
    scenarios/, so nothing in the card's worktree (or written during its build) can change the contract."""
    binary = player_exe(project / cfg["game"]["player_binary"])
    sdir = scenario_dir or (project.parent / "scenarios")
    results, ok = {}, True
    for name in scenarios:
        spec_path = sdir / f"{name}.json"
        spec = json.loads(spec_path.read_text())
        odir = out / "autotest" / name
        if odir.exists():
            shutil.rmtree(odir)          # never judge a stale or pre-planted metrics.json
        odir.mkdir(parents=True, exist_ok=True)
        nonce = secrets.token_hex(8)
        r = _run(cfg, _display_wrap([str(binary), "-autotest", str(spec_path), "-harnessOut", str(odir),
                                     "-harnessNonce", nonce,
                                     "-screen-width", str(cfg["unity"]["screen"]["width"]),
                                     "-screen-height", str(cfg["unity"]["screen"]["height"]),
                                     "-screen-fullscreen", "0", "-logFile", str(odir / "player.log")]),
                 timeout=cfg["unity"]["autotest_timeout_min"] * 60)
        mfile = odir / "metrics.json"
        if not mfile.exists():
            results[name] = {"ok": False, "rc": r.returncode, "fails": ["no metrics.json written"]}
            ok = False
            continue
        try:
            m = json.loads(mfile.read_text())
        except json.JSONDecodeError as e:
            m, fails = {}, [f"metrics.json unreadable: {e}"]
        else:
            fails = []
        fails += _check(m.get("metrics", {}), spec.get("expect", {}))
        if m.get("nonce") != nonce:
            fails.append("metrics.json nonce mismatch — not written by this harness run")
        if float(m.get("realSeconds", 0) or 0) < _min_seconds(spec) * 0.8:
            fails.append(f"scenario finished in {m.get('realSeconds')}s, faster than its steps allow "
                         f"({_min_seconds(spec):.1f}s) — metrics are not trustworthy")
        shots_expected = [st["name"] for st in spec.get("steps", []) if st.get("action") == "screenshot"]
        missing = [s_ for s_ in shots_expected if not (odir / f"{s_}.png").exists()]
        if missing:
            fails.append(f"screenshots missing: {missing}")
        if m.get("errors", 0) and spec.get("failOnError", True):
            fails.append(f"{m['errors']} errors/exceptions logged (see player.log)")
        if not m.get("completed", False):
            fails.append("scenario did not complete (timeout or crash)")
        probe_errors = m.get("probeErrors") or []
        results[name] = {"ok": not fails and r.returncode == 0, "rc": r.returncode, "fails": fails,
                         "probe_errors": probe_errors,
                         "screenshots": sorted(p.name for p in odir.glob("*.png")), "metrics": m.get("metrics")}
        ok &= results[name]["ok"]
    return ok, results


def capture(cfg, project: Path, takes_file: Path, out: Path):
    binary = player_exe(project / cfg["game"]["player_binary"])
    c = cfg["marketing"]["capture"]
    out.mkdir(parents=True, exist_ok=True)
    r = _run(cfg, _display_wrap([str(binary), "-capture", str(takes_file), "-harnessOut", str(out),
                                 "-screen-width", str(c["width"]), "-screen-height", str(c["height"]),
                                 "-screen-fullscreen", "0", "-logFile", str(out / "player.log")]),
             timeout=60 * 30)
    return r.returncode == 0, {"rc": r.returncode, "takes": sorted(p.name for p in out.iterdir() if p.is_dir())}
