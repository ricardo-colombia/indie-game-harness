#!/usr/bin/env python3
"""hx setup / hx doctor. Standard library only: this runs before the dependencies are installed.

  hx doctor                         check what the harness needs (exit 1 if a required item is missing)
  hx setup ["Game Name"] [--yes] [--dry-run]
                                    fix everything that can be fixed: Python deps, Unity project in game/,
                                    harness + Pipeline packages, config, git. Idempotent: run it again any time.
                                    --yes allows the big download (Unity editor) without asking.
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROJECT = "game"                      # Unity project folder (config game.unity_project)
INSTALL_CLI = "curl -fsSL https://unity.com/install.sh | bash"


def unity_json(*args: str, timeout: int = 60):
    """Run the Unity CLI and return (returncode, parsed JSON envelope or None)."""
    r = subprocess.run(["unity", *args, "--format", "json", "--no-banner", "--non-interactive"],
                       capture_output=True, text=True, timeout=timeout)
    try:
        return r.returncode, json.loads(r.stdout)
    except json.JSONDecodeError:
        return r.returncode, None


def installed_editors() -> list[str]:
    """Versions of the Unity 6 editors installed, newest first."""
    if not shutil.which("unity"):
        return []
    rc, env = unity_json("editors", "--installed")
    items = ((env or {}).get("data") or {}).get("items") or []
    vers = [str(i.get("version", "")) for i in items if isinstance(i, dict)]
    key = lambda v: [int(x) for x in re.findall(r"\d+", v)]  # noqa: E731
    return sorted((v for v in vers if v.startswith("6000")), key=key, reverse=True)


def manifest() -> dict | None:
    f = ROOT / PROJECT / "Packages/manifest.json"
    return json.loads(f.read_text()) if f.exists() else None


VENV = ROOT / ".venv"
VENV_PY = VENV / ("Scripts/python.exe" if sys.platform == "win32" else "bin/python")


def deps_ok() -> bool:
    """Python dependencies importable by the interpreter `hx` will use (.venv if it exists, else this one)."""
    py = str(VENV_PY) if VENV_PY.exists() else sys.executable
    return subprocess.run([py, "-c", "import requests, yaml"], capture_output=True).returncode == 0


def checks() -> list[tuple[str, bool, bool, str]]:
    """(name, ok, required, how to fix)"""
    m = manifest()
    deps = (m or {}).get("dependencies", {})
    return [
        ("Python packages", deps_ok(), True, "hx setup"),
        ("Unity CLI", bool(shutil.which("unity")), True, INSTALL_CLI),
        ("Unity 6 editor installed", bool(installed_editors()), True, "hx setup (downloads the editor)"),
        ("Unity project in game/", (ROOT / PROJECT / "ProjectSettings/ProjectVersion.txt").exists(), True, "hx setup"),
        ("Harness package in the project", "com.studio.harness" in deps, True, "hx setup"),
        ("Unity Pipeline package (lets agents drive the editor)", "com.unity.pipeline" in deps, True, "hx setup"),
        ("ffmpeg (shorts)", bool(shutil.which("ffmpeg")), False, "brew install ffmpeg"),
        ("Blender (art pipeline)", bool(_blender()), False, "install from blender.org"),
        ("claude CLI (hx run)", bool(shutil.which("claude")), False, "npm i -g @anthropic-ai/claude-code, then /login"),
    ]


def _blender():
    sys.path.insert(0, str(ROOT / "harness"))
    try:
        from core import find_blender
        return find_blender()
    except ImportError:  # yaml missing: fall back to PATH
        return shutil.which("blender")


def doctor() -> int:
    bad = 0
    for name, ok, required, fix in checks():
        print(f"  {'ok  ' if ok else ('FAIL' if required else 'skip')}  {name}" + ("" if ok else f"  ->  {fix}"))
        bad += (not ok) and required
    print("ready" if not bad else f"{bad} required item(s) missing: run `./hx setup`")
    return 1 if bad else 0


def run(cmd: list[str], dry: bool, cwd: Path = ROOT) -> None:
    print("$ " + " ".join(cmd))
    if not dry:
        r = subprocess.run(cmd, cwd=cwd)
        if r.returncode:
            raise SystemExit(f"failed ({r.returncode}): {' '.join(cmd)}")


def setup(name: str | None, yes: bool, dry: bool) -> int:
    # 1. Python dependencies
    if not deps_ok():   # into .venv: macOS/Linux system Pythons refuse `pip install` (PEP 668)
        if not VENV_PY.exists():
            run([sys.executable, "-m", "venv", str(VENV)], dry)
        run([str(VENV_PY), "-m", "pip", "install", "-q", "-r", "harness/requirements.txt"], dry)

    # 2. Unity CLI (the one thing we do not install behind your back)
    if not shutil.which("unity"):
        print(f"The Unity CLI is not installed. Install it, open a new terminal and run this again:\n  {INSTALL_CLI}")
        return 2

    # 3. Unity project (+ editor if none is installed)
    game = ROOT / PROJECT
    if not (game / "ProjectSettings/ProjectVersion.txt").exists():
        if game.exists() and any(p.name != ".gitkeep" for p in game.iterdir()):
            raise SystemExit(f"{PROJECT}/ has files but is not a Unity project; move them away and run setup again.")
        editors = installed_editors()
        if not editors and not (yes or dry):
            if not sys.stdin.isatty():
                print("No Unity 6 editor is installed. Downloading one takes several GB: run again with --yes to allow it.")
                return 2
            if input("No Unity 6 editor is installed. Download the latest LTS now (several GB)? [y/N] ").strip().lower() != "y":
                return 2
        if (game / ".gitkeep").exists() and not dry:
            (game / ".gitkeep").unlink()
        cmd = ["unity", "projects", "create", PROJECT, "--path", str(ROOT), "--editor-version", editors[0] if editors else "lts",
               "--no-cloud", "--non-interactive"]
        run(cmd + ([] if editors else ["--install-editor", "--accept-eula"]), dry)  # --accept-eula only goes with --install-editor

    # 4. Harness package + Pipeline package in the project
    f = game / "Packages/manifest.json"
    if f.exists():
        m = json.loads(f.read_text())
        m.setdefault("dependencies", {})["com.studio.harness"] = "file:../../packages/com.studio.harness"
        m["dependencies"].setdefault("com.unity.test-framework", "1.4.5")
        if "com.studio.harness" not in m.setdefault("testables", []):
            m["testables"].append("com.studio.harness")
        if not dry:
            f.write_text(json.dumps(m, indent=2))
    if "com.unity.pipeline" not in ((manifest() or {}).get("dependencies") or {}):
        run(["unity", "pipeline", "install", "--project-path", str(game)], dry)

    # 5. Tidy the new project: drop Unity's tutorial scaffolding, add the browser (WebGL) page template
    if not dry:
        for junk in ("Assets/TutorialInfo", "Assets/TutorialInfo.meta", "Assets/Readme.asset", "Assets/Readme.asset.meta"):
            p = game / junk
            shutil.rmtree(p, ignore_errors=True) if p.is_dir() else p.unlink(missing_ok=True)
        tpl, dest = ROOT / "packages/com.studio.harness/WebGLTemplates~/Harness", game / "Assets/WebGLTemplates/Harness"
        if tpl.is_dir() and game.exists() and not dest.exists():
            shutil.copytree(tpl, dest)   # Unity writes the .meta files on first import

    # 6. Config: game name (build target and binary name follow it, see core.load_config)
    if name:
        cfg = ROOT / "harness/config.yaml"
        s = re.sub(r"(?m)^  name: .*", lambda _: f"  name: {name}", cfg.read_text())
        if not dry:
            cfg.write_text(s)

    # 7. Git
    if not (ROOT / ".git").exists():
        run(["git", "init", "-q", "-b", "main"], dry)
    if shutil.which("git-lfs") and not dry:
        subprocess.run(["git", "lfs", "install", "--local"], cwd=ROOT, capture_output=True)

    print("\nSetup done. Review the changes and commit them.\n" if not dry else "\n(dry run: nothing was changed)\n")
    return 0 if dry else doctor()


def main() -> int:
    ap = argparse.ArgumentParser(prog="hx")
    ap.add_argument("cmd", choices=["setup", "doctor"])
    ap.add_argument("name", nargs="?", help="game name (setup only)")
    ap.add_argument("--yes", action="store_true", help="allow the Unity editor download without asking")
    ap.add_argument("--dry-run", action="store_true", help="print what would run, change nothing")
    a = ap.parse_args()
    return doctor() if a.cmd == "doctor" else setup(a.name, a.yes, a.dry_run)


if __name__ == "__main__":
    sys.exit(main())
