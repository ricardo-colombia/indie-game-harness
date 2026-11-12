"""Headless Unity: build, EditMode/PlayMode tests, autotest the built player, capture camera takes.
All functions return (ok: bool, summary: dict) and write logs under `out`."""
from __future__ import annotations

import json
import os
import platform
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

from core import sh


def _unity(cfg) -> str:
    exe = cfg["unity"]["editor_path"]
    if not exe or "${" in exe:
        raise RuntimeError("Set UNITY_PATH (or unity.editor_path in harness/config.yaml)")
    return exe


def _display_wrap(cmd: list[str]) -> list[str]:
    """Players need a GPU context for screenshots; on headless Linux use xvfb-run."""
    if platform.system() == "Linux" and not os.environ.get("DISPLAY") and shutil.which("xvfb-run"):
        return ["xvfb-run", "-a", "-s", "-screen 0 1920x1080x24"] + cmd
    return cmd


def build(cfg, project: Path, out: Path):
    binary = project / cfg["game"]["player_binary"]
    log = out / "build.log"
    r = sh([_unity(cfg), "-batchmode", "-nographics", "-quit", "-projectPath", str(project),
            "-buildTarget", cfg["game"]["build_target"],
            "-executeMethod", "Harness.Editor.HarnessBuild.BuildPlayer",
            "-harnessBuildPath", str(binary), "-logFile", "-"],
           timeout=cfg["unity"]["build_timeout_min"] * 60, log=log)
    errors = [l for l in r.stdout.splitlines() if "error CS" in l][:20]
    ok = r.returncode == 0 and binary.exists()
    return ok, {"rc": r.returncode, "binary": str(binary), "compile_errors": errors, "log": str(log)}


def tests(cfg, project: Path, out: Path, platform_: str):
    results = out / f"tests-{platform_}.xml"
    log = out / f"tests-{platform_}.log"
    r = sh([_unity(cfg), "-batchmode", "-projectPath", str(project), "-runTests",
            "-testPlatform", platform_, "-testResults", str(results), "-logFile", "-"]
           + (["-nographics"] if platform_ == "EditMode" else []),
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


def autotest(cfg, project: Path, out: Path, scenarios: list[str]):
    binary = project / cfg["game"]["player_binary"]
    sdir = project.parent / "scenarios"
    results, ok = {}, True
    for name in scenarios:
        spec_path = sdir / f"{name}.json"
        spec = json.loads(spec_path.read_text())
        odir = out / "autotest" / name
        odir.mkdir(parents=True, exist_ok=True)
        r = sh(_display_wrap([str(binary), "-autotest", str(spec_path), "-harnessOut", str(odir),
                              "-screen-width", str(cfg["unity"]["screen"]["width"]),
                              "-screen-height", str(cfg["unity"]["screen"]["height"]),
                              "-screen-fullscreen", "0", "-logFile", str(odir / "player.log")]),
               timeout=cfg["unity"]["autotest_timeout_min"] * 60)
        mfile = odir / "metrics.json"
        if not mfile.exists():
            results[name] = {"ok": False, "rc": r.returncode, "fails": ["no metrics.json written"]}
            ok = False
            continue
        m = json.loads(mfile.read_text())
        fails = _check(m.get("metrics", {}), spec.get("expect", {}))
        if m.get("errors", 0) and spec.get("failOnError", True):
            fails.append(f"{m['errors']} errors/exceptions logged (see player.log)")
        if not m.get("completed", False):
            fails.append("scenario did not complete (timeout or crash)")
        results[name] = {"ok": not fails and r.returncode == 0, "rc": r.returncode, "fails": fails,
                         "screenshots": sorted(p.name for p in odir.glob("*.png")), "metrics": m.get("metrics")}
        ok &= results[name]["ok"]
    return ok, results


def capture(cfg, project: Path, takes_file: Path, out: Path):
    binary = project / cfg["game"]["player_binary"]
    c = cfg["marketing"]["capture"]
    out.mkdir(parents=True, exist_ok=True)
    r = sh(_display_wrap([str(binary), "-capture", str(takes_file), "-harnessOut", str(out),
                          "-screen-width", str(c["width"]), "-screen-height", str(c["height"]),
                          "-screen-fullscreen", "0", "-logFile", str(out / "player.log")]),
           timeout=60 * 30)
    return r.returncode == 0, {"rc": r.returncode, "takes": sorted(p.name for p in out.iterdir() if p.is_dir())}
