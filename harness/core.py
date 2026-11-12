"""Shared helpers: config, cards, cost ledger, shell."""
from __future__ import annotations

import datetime as dt
import json
import os
import re
import subprocess
from dataclasses import dataclass, field
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent
BACKLOG = ROOT / "backlog"
LEDGER = ROOT / "harness" / "ledger.jsonl"
REPORTS = ROOT / "harness" / "reports"
STATES = ["inbox", "ready", "review", "done", "failed"]


def load_config(root: Path = ROOT) -> dict:
    text = os.path.expandvars((root / "harness" / "config.yaml").read_text())
    return yaml.safe_load(text)


def sh(cmd: list[str], cwd: Path | None = None, timeout: float | None = None,
       log: Path | None = None, env: dict | None = None, unset: list | None = None) -> subprocess.CompletedProcess:
    """Run a command; tee combined output to `log` if given. Never raises on non-zero exit."""
    full_env = {k: v for k, v in os.environ.items() if k not in (unset or [])}
    full_env.update(env or {})
    try:
        p = subprocess.run(cmd, cwd=cwd, timeout=timeout, capture_output=True, text=True, env=full_env)
        out = (p.stdout or "") + (p.stderr or "")
        rc = p.returncode
    except subprocess.TimeoutExpired as e:
        out = f"TIMEOUT after {timeout}s\n{e.stdout or ''}{e.stderr or ''}"
        rc = 124
    if log:
        log.parent.mkdir(parents=True, exist_ok=True)
        log.write_text(out if isinstance(out, str) else out.decode(errors="replace"))
    return subprocess.CompletedProcess(cmd, rc, out, "")


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
