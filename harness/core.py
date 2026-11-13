"""Shared helpers: config, cards, cost ledger, shell."""
from __future__ import annotations

import datetime as dt
import json
import os
import re
import shutil
import signal
import subprocess
from dataclasses import dataclass, field
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent
BACKLOG = ROOT / "backlog"
LEDGER = ROOT / "harness" / "ledger.jsonl"
REPORTS = ROOT / "harness" / "reports"
STATES = ["inbox", "ready", "review", "done", "failed"]


def _json_find(obj, keys=("location", "path")):
    """First string value under one of `keys`, searching the CLI's JSON envelope depth-first."""
    if isinstance(obj, dict):
        for k in keys:
            if isinstance(obj.get(k), str):
                return obj[k]
        obj = list(obj.values())
    if isinstance(obj, list):
        for v in obj:
            hit = _json_find(v, keys)
            if hit:
                return hit
    return None


def _editor_exe(location: str) -> str | None:
    """Editor install location (dir or .app bundle) -> the Unity executable inside it."""
    loc = Path(location)
    for c in [loc, loc / "Contents/MacOS/Unity", loc / "Unity.app/Contents/MacOS/Unity",
              loc / "Editor/Unity.exe", loc / "Unity.exe", loc / "Editor/Unity", loc / "Unity"]:
        if c.is_file():
            return str(c)
    return None


def project_editor_version(project: Path | None) -> str | None:
    f = project / "ProjectSettings/ProjectVersion.txt" if project else None
    if f and f.exists():
        m = re.search(r"m_EditorVersion:\s*(\S+)", f.read_text())
        return m.group(1) if m else None
    return None


def find_unity(cfg: dict, project: Path | None = None) -> str | None:
    """Unity editor binary. `unity.editor_path` in config wins if set; otherwise ask the Unity CLI
    (`unity editors path <version>`) for the project's editor version, or the latest installed one."""
    exe = str((cfg.get("unity") or {}).get("editor_path") or "")
    if exe and "${" not in exe:
        return exe
    if not shutil.which("unity"):
        return None
    for ver in filter(None, [project_editor_version(project), "latest"]):
        r = subprocess.run(["unity", "editors", "path", ver, "--format", "json", "--no-banner"],
                           capture_output=True, text=True)
        try:
            loc = _json_find(json.loads(r.stdout).get("data")) if r.returncode == 0 else None
        except json.JSONDecodeError:
            loc = None
        if loc and (hit := _editor_exe(loc)):
            return hit
    return None


def find_blender(cfg: dict | None = None) -> str | None:
    """Blender binary: config `art.blender.path`, PATH, then the standard app locations (macOS/Windows)."""
    want = str((((cfg or {}).get("art") or {}).get("blender") or {}).get("path") or "blender")
    if (hit := shutil.which(want)) or (Path(want).is_file() and (hit := want)):
        return hit
    cands = [Path("/Applications/Blender.app/Contents/MacOS/Blender"),
             Path.home() / "Applications/Blender.app/Contents/MacOS/Blender",
             *sorted(Path("C:/Program Files/Blender Foundation").glob("Blender*/blender.exe"), reverse=True)]
    return next((str(c) for c in cands if c.is_file()), None)


def load_config(root: Path = ROOT) -> dict:
    text = os.path.expandvars((root / "harness" / "config.yaml").read_text())
    return yaml.safe_load(text)


def sh(cmd: list[str], cwd: Path | None = None, timeout: float | None = None,
       log: Path | None = None, env: dict | None = None, unset: list | None = None,
       stdin: str | None = None) -> subprocess.CompletedProcess:
    """Run a command; tee combined output to `log` if given. Never raises on non-zero exit.
    On timeout the whole process tree is killed (Unity, players and xvfb-run spawn children)."""
    full_env = {k: v for k, v in os.environ.items() if k not in (unset or [])}
    full_env.update(env or {})
    # Windows: npm CLIs (claude) are .cmd shims that CreateProcess won't find by bare name
    exe = shutil.which(cmd[0], path=full_env.get("PATH")) if cmd else None
    if exe:
        cmd = [exe, *cmd[1:]]
    kw = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" else {"start_new_session": True}
    p = subprocess.Popen(cmd, cwd=cwd, stdin=subprocess.PIPE if stdin is not None else subprocess.DEVNULL,
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=full_env,
                         encoding="utf-8", errors="replace", **kw)
    try:
        out, _ = p.communicate(input=stdin, timeout=timeout)
        rc = p.returncode
    except subprocess.TimeoutExpired:
        kill_tree(p)
        out, _ = p.communicate()
        out, rc = f"TIMEOUT after {timeout}s\n{out or ''}", 124
    out = out or ""
    if log:
        log.parent.mkdir(parents=True, exist_ok=True)
        log.write_text(out)
    return subprocess.CompletedProcess(cmd, rc, out, "")


def kill_tree(p: subprocess.Popen) -> None:
    try:
        if os.name == "nt":
            subprocess.run(["taskkill", "/T", "/F", "/PID", str(p.pid)], capture_output=True)
        else:
            os.killpg(p.pid, signal.SIGKILL)
    except (ProcessLookupError, PermissionError, OSError):
        p.kill()


# ---------- cards ----------
FRONT = re.compile(r"^---\n(.*?)\n---\n(.*)$", re.S)


@dataclass
class Card:
    path: Path
    meta: dict
    body: str
    state: str = field(default="ready")
    front: str = ""  # original front matter text, kept verbatim (comments survive moves)

    @property
    def id(self) -> str:
        return str(self.meta["id"])

    def get(self, key, cfg):
        """Card value, falling back to runner defaults."""
        if key == "model" and "model" not in self.meta:
            diff = self.meta.get("difficulty", "normal")
            return cfg["runner"]["models"].get(diff, cfg["runner"]["defaults"]["model"])
        return self.meta.get(key, cfg["runner"]["defaults"].get(key))

    def text(self) -> str:
        front = self.front or yaml.safe_dump(self.meta, sort_keys=False).strip()
        return "---\n" + front + "\n---\n" + self.body

    def move(self, state: str, note: str | None = None) -> None:
        if note:
            self.body = self.body.rstrip() + f"\n\n## Runner log\n{note}\n"
        dest = BACKLOG / state / self.path.name
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(self.text())
        if dest != self.path:
            self.path.unlink(missing_ok=True)
        self.path, self.state = dest, state


def parse_card(path: Path) -> Card:
    m = FRONT.match(path.read_text())
    if not m:
        raise ValueError(f"{path}: missing YAML front matter")
    return Card(path, yaml.safe_load(m.group(1)) or {}, m.group(2), path.parent.name, m.group(1))


def list_cards(state: str) -> list[Card]:
    d = BACKLOG / state
    return [parse_card(p) for p in sorted(d.glob("*.md"))] if d.exists() else []


def next_card(exclude: set | None = None) -> Card | None:
    done = {c.id for c in list_cards("done")}
    ready = [c for c in list_cards("ready") if c.id not in (exclude or set())
             and all(str(dep) in done for dep in c.meta.get("depends_on", []) or [])]
    ready.sort(key=lambda c: (int(c.meta.get("priority", 3)), c.id))
    return ready[0] if ready else None


# ---------- ledger ----------
def ledger_add(kind: str, usd: float, billed: str = "plan", **extra) -> None:
    """billed: 'plan' (subscription quota; usd is Claude Code's estimate, not charged) | 'api' (real money)."""
    LEDGER.parent.mkdir(parents=True, exist_ok=True)
    row = {"ts": dt.datetime.now().isoformat(timespec="seconds"), "kind": kind, "billed": billed, "usd": round(usd, 4), **extra}
    with LEDGER.open("a") as f:
        f.write(json.dumps(row) + "\n")


def ledger_total(since: dt.datetime | None = None, kind: str | None = None, billed: str | None = None) -> float:
    if not LEDGER.exists():
        return 0.0
    total = 0.0
    for line in LEDGER.read_text().splitlines():
        r = json.loads(line)
        if since and dt.datetime.fromisoformat(r["ts"]) < since:
            continue
        if kind and r["kind"] != kind:
            continue
        if billed and r.get("billed", "api") != billed:
            continue
        total += r["usd"]
    return total
