#!/usr/bin/env python3
"""hx — one entry point for humans and agents.

  hx verify [--scenarios a b]        build + EditMode + PlayMode + autotest on this checkout
  hx build | hx test EditMode|PlayMode | hx autotest smoke drive_straight
  hx capture marketing/takes/launch.json
  hx card new "Title" [--type feature] | hx card ready C-0007 | hx cards
  hx release [--to-steam]            release build (no HARNESS) + scan for harness code; --to-steam stages it
  hx run [--budget 0] [--dry-run]    card runner, on demand ($0 = plan quota only)       (director only)
  hx merge C-0007 [--trusted]        re-gate a review/ card on current main, then merge   (director only)
  hx setup ["Game Name"] [--yes]     one-time setup: deps, Unity project in game/, packages, config, git
  hx doctor                          check what the harness needs
  hx selftest [--unity --live --all] validate the harness at $0
  hx report                          latest report per card + spend
  hx cost [--days 7]
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

if sys.argv[1:2] in (['setup'], ['doctor']):  # stdlib only: must work before dependencies are installed
    raise SystemExit(subprocess.call([sys.executable, str(Path(__file__).parent / 'setup.py'), *sys.argv[1:]]))

sys.path.insert(0, str(Path(__file__).parent))
import unity  # noqa: E402
from core import BACKLOG, LEDGER, REPORTS, ROOT, STATES, list_cards, load_config, parse_card  # noqa: E402


def _out(name: str) -> Path:
    p = REPORTS / "_local" / f"{dt.datetime.now():%Y%m%d-%H%M%S}-{name}"
    p.mkdir(parents=True, exist_ok=True)
    return p


def _print(ok, summary):
    print(json.dumps({"ok": ok, **({"result": summary} if not isinstance(summary, dict) else summary)}, indent=1, default=str))
    return 0 if ok else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(prog="hx")
    sub = ap.add_subparsers(dest="cmd", required=True)
    v = sub.add_parser("verify"); v.add_argument("--scenarios", nargs="*", default=[])
    rl = sub.add_parser("release"); rl.add_argument("--to-steam", action="store_true")
    sub.add_parser("build")
    t = sub.add_parser("test"); t.add_argument("platform", choices=["EditMode", "PlayMode"])
    at = sub.add_parser("autotest"); at.add_argument("scenarios", nargs="+")
    cp = sub.add_parser("capture"); cp.add_argument("takes")
    c = sub.add_parser("card"); c.add_argument("action", choices=["new", "ready"]); c.add_argument("arg")
    c.add_argument("--type", default="feature")
    sub.add_parser("cards")
    r = sub.add_parser("run"); r.add_argument("rest", nargs=argparse.REMAINDER)
    sub.add_parser("report")
    co = sub.add_parser("cost"); co.add_argument("--days", type=int, default=7)
    argv = sys.argv[1:] if argv is None else argv
    fwd = {"run": "runner.py", "selftest": "selftest.py"}
    if argv[:1] and argv[0] in fwd:  # forward everything untouched
        return subprocess.call([sys.executable, str(Path(__file__).parent / fwd[argv[0]]), *argv[1:]])
    if argv[:1] == ["merge"]:
        return subprocess.call([sys.executable, str(Path(__file__).parent / "runner.py"), "--merge", *argv[1:]])
    a = ap.parse_args(argv)

    cfg = load_config()
    project = ROOT / cfg["game"]["unity_project"]

    if a.cmd == "build":
        return _print(*unity.build(cfg, project, _out("build")))
    if a.cmd == "test":
        return _print(*unity.tests(cfg, project, _out("test"), a.platform))
    if a.cmd == "autotest":
        return _print(*unity.autotest(cfg, project, _out("autotest"), a.scenarios))
    if a.cmd == "capture":
        return _print(*unity.capture(cfg, project, ROOT / a.takes, ROOT / cfg["marketing"]["out_dir"] / "raw" / Path(a.takes).stem))
    if a.cmd == "verify":
        out, res = _out("verify"), {}
        for name, fn in [("build", lambda: unity.build(cfg, project, out)),
                         ("release", lambda: unity.build(cfg, project, out, release=True)),
                         ("editmode", lambda: unity.tests(cfg, project, out, "EditMode")),
                         ("playmode", lambda: unity.tests(cfg, project, out, "PlayMode")),
                         ("autotest", lambda: unity.autotest(cfg, project, out, ["smoke", *a.scenarios]))]:
            ok, res[name] = fn()
            if not ok:
                return _print(False, {"failed_gate": name, **res, "out": str(out)})
        return _print(True, {**res, "out": str(out)})

    if a.cmd == "release":
        dirty = subprocess.run(["git", "status", "--porcelain"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
        commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
        ok, summ = unity.build(cfg, project, _out("release"), release=True)
        if not ok or not a.to_steam:
            return _print(ok, summ)
        if dirty:
            return _print(False, {**summ, "error": "working tree is dirty; a Steam build must come from a commit"})
        src = Path(summ["binary"]).parent
        dst = ROOT / "steam" / "content"
        shutil.rmtree(dst, ignore_errors=True)
        shutil.copytree(src, dst)
        marker = ROOT / "steam" / "build_out" / "release_ok.json"
        marker.parent.mkdir(parents=True, exist_ok=True)
        marker.write_text(json.dumps({"commit": commit, "harness_leaks": [], "at": dt.datetime.now().isoformat()}))
        return _print(True, {**summ, "staged": str(dst), "commit": commit})
    if a.cmd == "card":
        if a.action == "ready" and os.environ.get("CLAUDECODE"):
            print("Only the director promotes cards to ready (run this in your own terminal).")
            return 1
        if a.action == "new":
            ids = [int(p.stem.split("-")[1]) for s in STATES for p in (BACKLOG / s).glob("C-*.md")]
            cid = f"C-{(max(ids) + 1 if ids else 1):04d}"
            slug = "-".join(a.arg.lower().split())[:40]
            tpl = (BACKLOG / "_template.md").read_text()
            tpl = tpl.replace("C-0000", cid).replace("TITLE", a.arg).replace("type: feature", f"type: {a.type}")
            dest = BACKLOG / "inbox" / f"{cid}-{slug}.md"
            dest.write_text(tpl)
            print(dest.relative_to(ROOT))
        else:
            hit = next((p for p in (BACKLOG / "inbox").glob(f"{a.arg}*.md")), None)
            if not hit:
                print(f"{a.arg} not in inbox"); return 1
            parse_card(hit).move("ready")
            print(f"{a.arg} -> ready")
        return 0
    if a.cmd == "cards":
        for s in STATES:
            cards = list_cards(s)
            if cards:
                print(f"[{s}]")
                for cd in cards:
                    print(f"  {cd.id} p{cd.meta.get('priority', 3)} {cd.meta.get('type', '')}: {cd.meta.get('title', '')}")
        return 0
    if a.cmd == "run":
        return subprocess.call([sys.executable, str(Path(__file__).parent / "runner.py"), *a.rest])
    if a.cmd == "report":
        for d in sorted(REPORTS.glob("C-*")):
            latest = sorted(d.iterdir())[-1] / "report.md"
            if latest.exists():
                print(f"{d.name}: {latest.read_text().splitlines()[1]}  ({latest.relative_to(ROOT)})")
        return main(["cost", "--days", "1"])
    if a.cmd == "cost":
        since = dt.datetime.now() - dt.timedelta(days=a.days)
        rows = [json.loads(l) for l in LEDGER.read_text().splitlines()] if LEDGER.exists() else []
        rows = [r for r in rows if dt.datetime.fromisoformat(r["ts"]) >= since]
        money: dict = {}
        plan = 0.0
        for r in rows:
            if r.get("billed", "api") == "api":
                money[r["kind"]] = money.get(r["kind"], 0) + r["usd"]
            else:
                plan += r["usd"]
        print(f"last {a.days}d: money ${sum(money.values()):.2f} "
              + " ".join(f"{k}=${v:.2f}" for k, v in sorted(money.items()))
              + f" | plan quota used ≈${plan:.2f} API-equivalent (not billed)")
        return 0
    return 1


if __name__ == "__main__":
    sys.exit(main())
